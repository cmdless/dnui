using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Dnui.Tray;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dnui;

/// <summary>
/// A tray icon shown by the Dnui runtime and controlled from .NET (contracts/tray-client.md). The
/// runtime is found or downloaded automatically. Dispose to remove the tray.
/// </summary>
public sealed class DnuiTray : IAsyncDisposable
{
    private readonly Process _process;
    private readonly JsonRpc _rpc;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Channel<TrayEvent> _events = Channel.CreateUnbounded<TrayEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentQueue<(TrayEvent Event, TaskCompletionSource Raised)> _waiters = new();
    private int _disposed;

    private DnuiTray(Process process, JsonRpc rpc, TimeSpan shutdownTimeout)
    {
        _process = process;
        _rpc = rpc;
        _shutdownTimeout = shutdownTimeout;
        Completion = WaitForExitAsync();
        _ = Task.Run(RaiseEventsAsync);
    }

    /// <summary>Raised when the menu opens. Raised on a background thread, in order.</summary>
    public event Action? Opened;

    /// <summary>Raised with the item id when a menu item is selected. Raised on a background thread, in order.</summary>
    public event Action<string>? Clicked;

    /// <summary>Raised when an <see cref="Opened"/> or <see cref="Clicked"/> handler throws. The connection is unaffected.</summary>
    public event Action<Exception>? UnhandledHandlerException;

    /// <summary>The runtime version reported by <c>initialize</c>.</summary>
    public string RuntimeVersion { get; private set; } = string.Empty;

    /// <summary>Completes when the runtime exits for any reason. Never faults for a non-zero exit code.</summary>
    public Task<DnuiResult> Completion { get; }

    /// <summary>
    /// Resolves (and if needed downloads) the runtime, starts it, and completes once the tray has
    /// been initialized with the icon, tooltip, and items from <paramref name="options"/>.
    /// </summary>
    /// <exception cref="DnuiRuntimeUnavailableException">The runtime could not be obtained.</exception>
    /// <exception cref="DnuiTrayStartException">The runtime exited before the tray started.</exception>
    /// <exception cref="DnuiTrayProtocolException">The runtime rejected the initial icon or items.</exception>
    public static async Task<DnuiTray> StartAsync(DnuiTrayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runtimePath = await DnuiRuntime.ResolveAsync(options.Runtime, cancellationToken).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo(runtimePath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };
        if (options.AppAssembly is not null || options.AppType is not null)
        {
            startInfo.ArgumentList.Add("--app-assembly");
            startInfo.ArgumentList.Add(options.AppAssembly ?? string.Empty);
            startInfo.ArgumentList.Add("--app-type");
            startInfo.ArgumentList.Add(options.AppType ?? string.Empty);
        }

        startInfo.ArgumentList.Add("tray");
        if (options.Headless)
        {
            startInfo.ArgumentList.Add("--headless");
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{runtimePath}'.");
        var rpc = TrayProtocol.CreateConnection(process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        var tray = new DnuiTray(process, rpc, options.ShutdownTimeout);
        rpc.AddLocalRpcTarget(new NotificationTarget(tray), new JsonRpcTargetOptions { DisposeOnDisconnect = false });
        rpc.StartListening();

        try
        {
            var result = await rpc.InvokeWithParameterObjectAsync<InitializeResult>(
                "initialize",
                new InitializeParams
                {
                    ProtocolVersion = TrayProtocolTarget.ProtocolVersion,
                    Icon = options.Icon?.Source,
                    Tooltip = options.Tooltip,
                    Items = options.Items.Select(i => i.ToWire()).ToList(),
                },
                cancellationToken).ConfigureAwait(false);
            tray.RuntimeVersion = result.RuntimeVersion;
            return tray;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            tray.Kill();
            await tray.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (RemoteInvocationException ex)
        {
            await tray.DisposeAsync().ConfigureAwait(false);
            throw ToProtocolException(ex);
        }
        catch (Exception ex) when (ex is ConnectionLostException or ObjectDisposedException or IOException)
        {
            var exit = await tray.Completion.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
            await tray.DisposeAsync().ConfigureAwait(false);
            throw new DnuiTrayStartException(exit, ex);
        }
    }

    /// <summary>Replaces the whole menu; completes after the runtime applied it.</summary>
    public Task SetItemsAsync(IReadOnlyList<DnuiTrayItem> items, CancellationToken cancellationToken = default) =>
        CallAsync("tray/setItems", new SetItemsParams { Items = items.Select(i => i.ToWire()).ToList() }, cancellationToken);

    /// <summary>Changes the icon (and tooltip, unless null); completes after the runtime applied it.</summary>
    public Task SetIconAsync(DnuiTrayIcon icon, string? tooltip = null, CancellationToken cancellationToken = default) =>
        CallAsync("tray/setIcon", new SetIconParams { Icon = icon.Source, Tooltip = tooltip }, cancellationToken);

    /// <summary>Returns what the tray is showing.</summary>
    public async Task<DnuiTrayState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await CallAsync<TrayStateResult>("tray/getState", new { }, cancellationToken).ConfigureAwait(false);
        return new DnuiTrayState(state.Visible, state.HasIcon, state.Tooltip, state.Items.Select(DnuiTrayItem.FromWire).ToList());
    }

    /// <summary>Simulates the menu opening. <see cref="Opened"/> has been raised when this completes.</summary>
    public async Task SimulateOpenAsync(CancellationToken cancellationToken = default)
    {
        var raised = ExpectEvent(new TrayEvent(null));
        await CallAsync("tray/simulateOpen", new { }, cancellationToken).ConfigureAwait(false);
        await raised.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Simulates selecting an item. <see cref="Clicked"/> has been raised when this completes.</summary>
    public async Task SimulateClickAsync(string id, CancellationToken cancellationToken = default)
    {
        var raised = ExpectEvent(new TrayEvent(id));
        await CallAsync("tray/simulateClick", new SimulateClickParams { Id = id }, cancellationToken).ConfigureAwait(false);
        await raised.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes the runtime's stdin, waits up to the shutdown timeout, then kills it. Idempotent.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await Completion.ConfigureAwait(false);
            return;
        }

        _rpc.Dispose();
        try
        {
            await _process.WaitForExitAsync().WaitAsync(_shutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Kill();
        }

        await Completion.ConfigureAwait(false);
        _events.Writer.TryComplete();
    }

    private Task CallAsync(string method, object parameters, CancellationToken cancellationToken) =>
        CallAsync<JsonElement>(method, parameters, cancellationToken);

    private async Task<T> CallAsync<T>(string method, object parameters, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0 || _process.HasExited)
        {
            throw new DnuiTrayNotRunningException();
        }

        try
        {
            return await _rpc.InvokeWithParameterObjectAsync<T>(method, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException ex)
        {
            throw ToProtocolException(ex);
        }
        catch (Exception ex) when (ex is ConnectionLostException or ObjectDisposedException or IOException)
        {
            throw new DnuiTrayNotRunningException(ex);
        }
    }

    private static DnuiTrayProtocolException ToProtocolException(RemoteInvocationException ex)
    {
        JsonElement? data = ex.DeserializedErrorData switch
        {
            JsonElement element => element,
            null => null,
            var other => JsonSerializer.SerializeToElement(other),
        };

        if (data is null && ex.ErrorData is JsonElement raw)
        {
            data = raw;
        }

        return new DnuiTrayProtocolException(ex.ErrorCode, ex.Message, data, ex);
    }

    private async Task<DnuiResult> WaitForExitAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);
        _rpc.Dispose();
        return new DnuiResult(_process.ExitCode, null);
    }

    private void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Registers interest in the next matching event; completes after its handlers ran.</summary>
    private Task ExpectEvent(TrayEvent expected)
    {
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters.Enqueue((expected, raised));
        return raised.Task;
    }

    private async Task RaiseEventsAsync()
    {
        await foreach (var trayEvent in _events.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (trayEvent.ClickedId is { } id)
                {
                    Clicked?.Invoke(id);
                }
                else
                {
                    Opened?.Invoke();
                }
            }
            catch (Exception ex)
            {
                try { UnhandledHandlerException?.Invoke(ex); } catch { }
            }

            SignalWaiter(trayEvent);
        }
    }

    private void SignalWaiter(TrayEvent raised)
    {
        // Waiters are few and short-lived; complete the oldest one matching this event.
        var pending = new List<(TrayEvent, TaskCompletionSource)>();
        var signaled = false;
        while (_waiters.TryDequeue(out var waiter))
        {
            if (!signaled && waiter.Event == raised)
            {
                waiter.Raised.TrySetResult();
                signaled = true;
            }
            else
            {
                pending.Add(waiter);
            }
        }

        foreach (var waiter in pending)
        {
            _waiters.Enqueue(waiter);
        }
    }

    /// <summary>A menu opening (<see cref="ClickedId"/> null) or an item selection.</summary>
    private sealed record TrayEvent(string? ClickedId);

    // Notifications are queued and raised in order on one loop. Simulate methods wait for their
    // matching event rather than relying on notification/response processing order.
    private sealed class NotificationTarget(DnuiTray tray)
    {
        [JsonRpcMethod("tray/opened")]
        public void OnOpened() => tray._events.Writer.TryWrite(new TrayEvent(null));

        [JsonRpcMethod("tray/clicked", UseSingleObjectParameterDeserialization = true)]
        public void OnClicked(ClickedNotification notification) => tray._events.Writer.TryWrite(new TrayEvent(notification.Id));
    }
}
