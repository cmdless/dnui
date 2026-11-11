# Contract: Dnui Tray Client (public API)

> **Superseded in part by spec 003**: the host, tray-start, and app-selection API changed before 0.1.0 was published. See [../../003-tool-publishing/contracts/library-api.md](../../003-tool-publishing/contracts/library-api.md).

Package `Cmdless.Dnui`, namespace `Dnui`. This is the caller side of
[tray-protocol.md](tray-protocol.md). The client hides the process, pipes, and JSON-RPC.

```csharp
namespace Dnui;

// Same shape for every command. StdOut is null when nothing was resolved (always for tray).
public sealed record DnuiResult(int ExitCode, string? StdOut);

public sealed record DnuiTrayOptions
{
    // Runtime resolution: CmdlessRoot, feed, or a dev RuntimePath override. Null = all defaults,
    // so the runtime is found or downloaded automatically (contracts/runtime-acquisition.md).
    public DnuiRuntimeOptions? Runtime { get; init; }
    public string? AppAssembly { get; init; }           // --app-assembly
    public string? AppType { get; init; }               // --app-type
    public DnuiTrayIcon? Icon { get; init; }
    public string? Tooltip { get; init; }
    public IReadOnlyList<DnuiTrayItem> Items { get; init; } = [];
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool Headless { get; init; }                 // tray --headless: no OS tray icon
}

public sealed record DnuiTrayItem(string Id, string Text)
{
    public bool Enabled { get; init; } = true;
    public bool? Checked { get; init; }                  // null = not a check item
    public IReadOnlyList<DnuiTrayItem>? Items { get; init; }   // submenu
    public static DnuiTrayItem Separator { get; }
}

public sealed class DnuiTrayIcon
{
    public static DnuiTrayIcon FromFile(string path);
    public static DnuiTrayIcon FromPng(ReadOnlyMemory<byte> png);
}

public sealed class DnuiTray : IAsyncDisposable
{
    // Resolves (and if needed downloads) the runtime, starts it, and completes after a
    // successful initialize. Throws DnuiRuntimeUnavailableException if the runtime can't be
    // obtained, DnuiTrayStartException (with Result) if the runtime ends first, or
    // OperationCanceledException (after killing the runtime) when cancelled.
    public static Task<DnuiTray> StartAsync(DnuiTrayOptions options, CancellationToken ct = default);

    public string RuntimeVersion { get; }                // from the initialize result

    // Raised on a background thread, in order received. Handler exceptions are caught and
    // reported through UnhandledHandlerException; they never break the connection.
    public event Action? Opened;
    public event Action<string>? Clicked;
    public event Action<Exception>? UnhandledHandlerException;

    // Complete after the runtime has applied the change.
    // Throw DnuiTrayProtocolException for protocol errors, or
    // DnuiTrayNotRunningException once the tray has ended.
    public Task SetItemsAsync(IReadOnlyList<DnuiTrayItem> items, CancellationToken ct = default);
    public Task SetIconAsync(DnuiTrayIcon icon, string? tooltip = null, CancellationToken ct = default);

    // Inspection and simulation (tray/getState, tray/simulateOpen, tray/simulateClick).
    // Always available; intended for consumers' automated tests and diagnostics.
    public Task<DnuiTrayState> GetStateAsync(CancellationToken ct = default);
    public Task SimulateOpenAsync(CancellationToken ct = default);   // Opened is raised before this completes
    public Task SimulateClickAsync(string id, CancellationToken ct = default);   // 1012 → DnuiTrayProtocolException

    // Completes when the runtime exits, for any reason. Never faults for a non-zero exit code.
    public Task<DnuiResult> Completion { get; }

    // Closes the runtime's stdin, awaits exit up to ShutdownTimeout, then kills it.
    public ValueTask DisposeAsync();
}

public sealed record DnuiTrayState(bool Visible, bool HasIcon, string? Tooltip, IReadOnlyList<DnuiTrayItem> Items);

public sealed class DnuiTrayStartException : Exception
{
    public DnuiResult Result { get; }                    // ExitCode per contracts/cli.md
}

public sealed class DnuiTrayProtocolException : Exception
{
    public int Code { get; }                             // e.g. 1010 InvalidItems
    public System.Text.Json.JsonElement? Data { get; }
}

public sealed class DnuiTrayNotRunningException : InvalidOperationException { }
```

## Behavior

- **Resolve**: `DnuiRuntime.ResolveAsync(options.Runtime)` runs first. On a warm cache it
  makes no network requests.
- **Launch**: `<resolved runtime> [--app-assembly A --app-type T] tray [--headless]`, with stdin and stdout
  redirected and stderr inherited (never redirected, see [cli.md](cli.md)).
- **Framing**: newline-delimited JSON-RPC over the process streams. Notification handlers are
  registered before listening starts, so none are missed.
- **Start failure**: the connection drops before the initialize response arrives. The client
  awaits the process exit and throws `DnuiTrayStartException` with the `DnuiResult`. For tray,
  `StdOut` is always null, so the exit code alone identifies the cause (10–13, 21, 22).
- **Completion**: when the process exits, `Completion` returns `new DnuiResult(exitCode, null)`.
  Any pending update calls fail with `DnuiTrayNotRunningException`.
- **Dispose**: idempotent. Safe to call from inside a `Clicked` handler.

## Minimal use

```csharp
await using var tray = await DnuiTray.StartAsync(new()
{
    Icon = DnuiTrayIcon.FromFile("icon.png"),
    Tooltip = "My App",
    Items = [new("open", "Open"), DnuiTrayItem.Separator, new("quit", "Quit")],
});

tray.Clicked += id => { if (id == "quit") _ = tray.DisposeAsync(); };
var result = await tray.Completion;   // ExitCode 0 after dispose
```

## Compatibility

These types are part of the public contract (constitution V). `DnuiResult` and
`DnuiRuntimeOptions` are shared with future one-shot commands.
