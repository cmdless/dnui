using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Dnui.Commands;
using Dnui.Internal;
using StreamJsonRpc;

namespace Dnui.Tray;

/// <summary>
/// <c>tray</c>: a session command that drives one tray icon over JSON-RPC on the stdio session
/// (research R6–R8). Uses the same public <see cref="DnuiStdio.OpenSession"/> as consumers.
/// </summary>
internal sealed class TrayCommand(bool headless) : DnuiCommand
{
    private JsonRpc? _rpc;
    private TrayNotifier? _notifier;
    private TrayProtocolTarget? _target;

    public override bool IsSession => true;

    public override bool HideDockIcon => true;

    public bool Headless => headless;

    public override int? Prepare(DnuiCommandApp app)
    {
        DnuiSession session;
        try
        {
            session = app.StdioInternal.OpenSession();
        }
        catch (DnuiStdioException ex)
        {
            Console.Error.WriteLine($"dnui: tray: {ex.Message}");
            return ex.Reason == DnuiStdioFailure.NotRedirected ? TrayExitCodes.NotRedirected : TrayExitCodes.AlreadyClaimed;
        }

        Console.Error.WriteLine($"dnui: tray: session open (stdout: {app.StdioInternal.OutputKind}).");

        // Created now (synchronous phase); listening starts only once the UI is ready (FR-010).
        TrayProtocolTarget? target = null;
        var input = new DrainOnEndOfInputStream(session.Input, () => target?.WaitUntilIdleAsync() ?? Task.CompletedTask);
        JsonRpc? rpc = null;
        // A failed write means the caller is gone or stopped reading: end the session now. The
        // connection's own completion may never come while stdin is still open.
        var output = new WriteFailureStream(session.Output, () =>
        {
            Console.Error.WriteLine("dnui: tray: stdout is closed; ending the session.");
            app.ResolveInternal(null, 0);
            rpc?.Dispose();
        });
        _rpc = rpc = TrayProtocol.CreateConnection(output, input);
        _notifier = new TrayNotifier(_rpc);
        _target = target = new TrayProtocolTarget(AvaloniaUiDispatcher.Instance, _notifier, DnuiBuildInfo.RuntimeVersion);
        _rpc.AddLocalRpcTarget(_target, new JsonRpcTargetOptions { DisposeOnDisconnect = false });
        return null;
    }

    public override void Run(DnuiCommandApp app, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var rpc = _rpc ?? throw new InvalidOperationException("tray was not prepared.");
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ITrayHost host = headless ? new HeadlessTrayHost() : new AvaloniaTrayHost(app);
        _target!.AttachHost(host);
        lifetime.Exit += (_, _) =>
        {
            _notifier!.Complete();
            host.Dispose();
            rpc.Dispose();
        };

        _notifier!.Start();
        rpc.StartListening();

        // Input end-of-file or a broken stdout completes the connection: end the session.
        rpc.Completion.ContinueWith(_ => app.ResolveInternal(null, 0), TaskScheduler.Default);
    }
}
