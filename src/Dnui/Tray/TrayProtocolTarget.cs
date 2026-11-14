using StreamJsonRpc;

namespace Dnui.Tray;

/// <summary>
/// The JSON-RPC methods of tray protocol v1 (contracts/tray-protocol.md). Requests are handled one
/// at a time in arrival order; UI work runs on the UI thread and responses follow visible changes.
/// </summary>
internal sealed class TrayProtocolTarget
{
    public const int ProtocolVersion = 1;

    private readonly IUiDispatcher _ui;
    private readonly TrayNotifier _notifier;
    private readonly string _runtimeVersion;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITrayHost? _host;
    private TrayState _state = TrayState.Empty;
    private bool _initialized;
    private int _inFlight;

    public TrayProtocolTarget(IUiDispatcher ui, TrayNotifier notifier, string runtimeVersion)
    {
        _ui = ui;
        _notifier = notifier;
        _runtimeVersion = runtimeVersion;
    }

    /// <summary>Connects the host before listening starts; host events become notifications.</summary>
    public void AttachHost(ITrayHost host)
    {
        _host = host;
        host.MenuOpened += _notifier.Opened;
        host.ItemClicked += _notifier.Clicked;
    }

    [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
    public Task<InitializeResult> InitializeAsync(InitializeParams? request) => Serialized(async () =>
    {
        if (_initialized)
        {
            throw TrayErrorCodes.Error(TrayErrorCodes.AlreadyInitialized, "initialize was already called.");
        }

        request ??= new InitializeParams();
        if (request.ProtocolVersion != ProtocolVersion)
        {
            throw TrayErrorCodes.Error(
                TrayErrorCodes.ProtocolVersionMismatch,
                $"Protocol version {request.ProtocolVersion} is not supported; this runtime speaks {ProtocolVersion}.",
                new { requested = request.ProtocolVersion, supported = new[] { ProtocolVersion } });
        }

        ThrowIfInvalid(request.Items);
        var icon = request.Icon is null ? null : LoadIcon(request.Icon);
        var next = new TrayState(icon, request.Tooltip, request.Items ?? []);

        await ApplyAsync(next, icon).ConfigureAwait(false);
        _initialized = true;
        return new InitializeResult(_runtimeVersion);
    });

    [JsonRpcMethod("tray/setItems", UseSingleObjectParameterDeserialization = true)]
    public Task<EmptyResult> SetItemsAsync(SetItemsParams? request) => Serialized(async () =>
    {
        RequireInitialized();
        var items = request?.Items ?? [];
        ThrowIfInvalid(items);
        await ApplyAsync(_state with { Items = items }, null).ConfigureAwait(false);
        return EmptyResult.Instance;
    });

    [JsonRpcMethod("tray/setIcon", UseSingleObjectParameterDeserialization = true)]
    public Task<EmptyResult> SetIconAsync(SetIconParams? request) => Serialized(async () =>
    {
        RequireInitialized();
        var icon = LoadIcon(request?.Icon);
        await ApplyAsync(_state with { Icon = icon, Tooltip = request?.Tooltip ?? _state.Tooltip }, icon).ConfigureAwait(false);
        return EmptyResult.Instance;
    });

    [JsonRpcMethod("tray/getState", UseSingleObjectParameterDeserialization = true)]
    public Task<TrayStateResult> GetStateAsync(NoParams? request = null) => Serialized(() =>
    {
        RequireInitialized();
        return Task.FromResult(new TrayStateResult(_state.Visible, _state.Icon is not null, _state.Tooltip, _state.Items));
    });

    [JsonRpcMethod("tray/simulateOpen", UseSingleObjectParameterDeserialization = true)]
    public Task<EmptyResult> SimulateOpenAsync(NoParams? request = null) => Serialized(async () =>
    {
        RequireInitialized();
        await _notifier.OpenedAndWaitAsync().ConfigureAwait(false);
        return EmptyResult.Instance;
    });

    [JsonRpcMethod("tray/simulateClick", UseSingleObjectParameterDeserialization = true)]
    public Task<EmptyResult> SimulateClickAsync(SimulateClickParams? request) => Serialized(async () =>
    {
        RequireInitialized();
        var id = request?.Id ?? string.Empty;
        var item = id.Length == 0 ? null : TrayMenuValidator.Find(_state.Items, id);
        var reason = item switch
        {
            null => "unknown",
            { Separator: true } => "separator",
            { IsParent: true } => "parent",
            { Enabled: false } => "disabled",
            _ => null,
        };

        if (reason is not null)
        {
            throw TrayErrorCodes.Error(TrayErrorCodes.InvalidSimulation, $"Cannot simulate a click on '{id}': {reason}.", new { id, reason });
        }

        await _notifier.ClickedAndWaitAsync(id).ConfigureAwait(false);
        return EmptyResult.Instance;
    });

    /// <summary>
    /// Completes once no request is in flight and no notification is queued, stable for a short
    /// period (requests already read may still be on their way to dispatch). Gives up after 10 s.
    /// </summary>
    public async Task WaitUntilIdleAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (IsIdle)
            {
                await Task.Delay(50).ConfigureAwait(false);
                if (IsIdle)
                {
                    return;
                }
            }
            else
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }
    }

    private bool IsIdle => Volatile.Read(ref _inFlight) == 0 && _notifier.IsIdle;

    private async Task<T> Serialized<T>(Func<Task<T>> body)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await body().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            // Released after the response task completes; StreamJsonRpc writes it right after.
            _ = Task.Delay(20).ContinueWith(_ => Interlocked.Decrement(ref _inFlight), TaskScheduler.Default);
        }
    }

    private void RequireInitialized()
    {
        if (!_initialized)
        {
            throw TrayErrorCodes.Error(TrayErrorCodes.NotInitialized, "initialize must be called first.");
        }
    }

    private static void ThrowIfInvalid(IReadOnlyList<TrayMenuItem>? items)
    {
        if (TrayMenuValidator.Validate(items) is { } error)
        {
            throw TrayErrorCodes.Error(TrayErrorCodes.InvalidItems, $"Invalid item at {error.Path}: {error.Reason}.", error.ToErrorData());
        }
    }

    private static byte[] LoadIcon(TrayIconSource? source)
    {
        var (png, error) = TrayIconDecoder.Load(source);
        return png ?? throw TrayErrorCodes.Error(TrayErrorCodes.InvalidIcon, $"Invalid icon: {error}.", new { reason = error });
    }

    private async Task ApplyAsync(TrayState next, byte[]? iconToValidate)
    {
        var host = _host ?? throw new InvalidOperationException("No tray host attached.");
        await _ui.InvokeAsync(() =>
        {
            if (iconToValidate is not null && host.ValidateIcon(iconToValidate) is { } reason)
            {
                throw TrayErrorCodes.Error(TrayErrorCodes.InvalidIcon, $"Invalid icon: {reason}.", new { reason });
            }

            host.Apply(next);
            return true;
        }).ConfigureAwait(false);

        _state = next;
    }
}
