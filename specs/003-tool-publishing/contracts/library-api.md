# Contract: Library API After the Cleanup

Package `Cmdless.Dnui`, namespace `Dnui`. This replaces the host and tray parts of
[001 tray-client.md](../../001-tray-command/contracts/tray-client.md) and
[002 library-api.md](../../002-windows-dialogs/contracts/library-api.md). Types not shown here
are unchanged. The app selection (`WithApp`) follows research D1 (confirmed).

```csharp
namespace Dnui;

// ---- Host commands: a new runtime process per call ----
public interface IDnuiHostCommands : IDnuiDialogs
{
    // App selection: chain ends in the string form (= --app-assembly / --app-type).
    IDnuiHostCommands WithApp<TApp>() where TApp : DnuiCommandApp;          // → WithApp(Type)
    IDnuiHostCommands WithApp(Type appType);                                 // → WithApp(string, string)
    IDnuiHostCommands WithApp(string appAssemblyPath, string appTypeName);

    // Windows: chain ends in the string form (= window <assembly> <type>).
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken ct = default) where TWindow : DnuiWindow;  // → (Type)
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken ct = default);                    // → (string, string)
    Task<JsonNode?> OpenWindowAsync(string windowAssemblyPath, string windowTypeName, CancellationToken ct = default);

    // Tray: a session. Options describe only the tray.
    Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken ct = default);
}

public sealed record DnuiHostOptions
{
    public DnuiRuntimeOptions? Runtime { get; init; }        // applies to every command
    public bool Headless { get; init; }                      // applies to every command, trays included
    public IReadOnlyList<JsonNode?> Outcomes { get; init; } = [];   // headless dialog outcomes, in order
}

public sealed class DnuiHost(DnuiHostOptions? options = null) : IDnuiHostCommands
{
    public static DnuiHost Default { get; }
    // RunAsync: removed from the public API (internal).
}

// ---- Window commands: in-process, parented; no app, no tray ----
public interface IDnuiWindowCommands : IDnuiDialogs
{
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken ct = default) where TWindow : DnuiWindow;  // `new()` dropped
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken ct = default);
    Task<JsonNode?> OpenWindowAsync(string windowAssemblyPath, string windowTypeName, CancellationToken ct = default);
}

// ---- Tray ----
public sealed record DnuiTrayOptions
{
    public DnuiTrayIcon? Icon { get; init; }
    public string? Tooltip { get; init; }
    public IReadOnlyList<DnuiTrayItem> Items { get; init; } = [];
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// Runs on the created tray before it is initialized: attach Opened/Clicked/UnhandledHandlerException here.
    /// Tray methods called inside throw DnuiTrayNotRunningException ("not started").
    public Action<DnuiTray>? Configure { get; init; }
    // Removed: Runtime, Headless (host options), AppAssembly, AppType (WithApp).
}

public sealed class DnuiTray : IAsyncDisposable
{
    // StartAsync: internal. Start through IDnuiHostCommands.StartTrayAsync.
    // Everything else unchanged (events, SetItemsAsync, SetIconAsync, GetStateAsync, Simulate*, Completion, DisposeAsync).
}

// ---- Runtime acquisition ----
public sealed record DnuiRuntimeOptions          // was sealed class
{
    public string? CmdlessRoot { get; init; }     // code > CMDLESS_ROOT > ~/.cmdless
    public string? PackageSource { get; init; }   // code > DNUI_PACKAGE_SOURCE > nuget.org   (new env var)
    public string? RuntimePath { get; init; }
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
// DnuiRuntime.ResolveAsync stays public: pre-downloading the runtime (installers, first-run setup).
```

## Removed public types

| Type | Replacement |
|---|---|
| `DnuiTrayStartException` | `DnuiCommandException`. `Result` is the runtime's exit, `StdOut` is null, and `ErrorCode` is the exit code's name (for example `TrayHostUnavailable`) |

## Semantics

- **Start order for a tray**: resolve the runtime → start the process → create the `DnuiTray` →
  run `Configure` → start listening → `initialize`. No event can be raised before `Configure`
  returns.
- **`Configure` throws**: the runtime is killed, the tray is disposed, and the exception is
  rethrown unchanged from `StartTrayAsync`.
- **Overload chains**: each overload only translates its arguments (`typeof`, then
  `AssemblyLocator.Find` and `FullName`) and calls the next. Only the string overload does the
  work. The generic, `Type` and string forms of the same call produce the same runtime command
  line.
- **`WithApp`**: returns a lightweight host bound to the app. It shares the parent's options and
  its headless outcome sequence. Calling `WithApp` on a bound host replaces the app.
- **Child lifetime**: every runtime started by the library ends when the starting process
  receives a catchable termination signal, or on Windows when it dies for any reason
  ([tool-cli.md](tool-cli.md)).

## Compatibility

Changing the API before 0.1.0 is published breaks no consumers. The public-API guard test
(`PublicApiTests`) is updated to the new type list. Specs 001 and 002 contracts get a note
pointing here.
