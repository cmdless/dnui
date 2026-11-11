# Research: Tray Command and Protocol

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-08

Each entry records a decision, why it was made, and what else was considered.

## R1. Where command code lives (runtime or library)

- **Decision**: command *parsing* stays in Dnui.Runtime, using System.CommandLine. Command
  *definitions and execution* (the `TrayCommand` model, its JSON-RPC session, and the tray
  host) live in the Dnui library as internal types. The runtime builds a command object from
  the parsed arguments and hands it to the app through an internal `SetCommand`.
- **Rationale**: `DnuiCommandApp` is a library type with a sealed
  `OnFrameworkInitializationCompleted`, so it has to be able to run any built-in command,
  including when a consumer's subclass is loaded from another assembly. Keeping parsing in the
  runtime keeps System.CommandLine out of the library (constitution: "Dnui.Runtime uses
  System.CommandLine"), and keeps the consumer-facing package small.
- **Alternatives**: commands entirely in the runtime. Rejected: `DnuiCommandApp` would have to
  call back into runtime code it can't reference. Parsing in the library. Rejected: adds a
  dependency to the library with no consumer benefit.

## R2. App model API shape

- **Decision**:
  - `DnuiApp : Application` has a `protected virtual void Configure(AppBuilder builder)`, whose
    base applies `WithInterFont()`, `LogToTrace()`, and in Debug `WithDeveloperTools()`.
  - An `internal void ApplyConfiguration(AppBuilder)` calls `Configure`. `DnuiCommandApp`
    first applies the command's builder options and then calls `Configure`, so a subclass can
    still override what the command set.
  - `DnuiCommandApp` has `internal void SetCommand(DnuiCommand)` and a
    `sealed override OnFrameworkInitializationCompleted`.
  - The Dnui assembly declares `InternalsVisibleTo("Dnui.Runtime")` and
    `InternalsVisibleTo("Dnui.Tests")`.
- **Platform builder options**: the library can't reference Avalonia.Native (constitution II),
  so a command exposes hints such as `HideDockIcon`. The runtime's platform adapter applies them
  (`.With(new MacOSPlatformOptions { ShowInDock = false })`) right before
  `ApplyConfiguration`, which keeps the "command options before `Configure`" order.
- **Rationale**: constitution Principle II asks for a minimal public surface. Only the runtime
  calls `SetCommand` and `ApplyConfiguration`. Applying command options before `Configure`
  lets a consumer intentionally override, for example, the hidden Dock icon.
- **Alternatives**: a public virtual `Configure` that the runtime calls directly, as in the
  draft. Rejected: it can't guarantee the command's builder options are applied when a
  subclass overrides `Configure` without calling `base`.

## R3. Loading a custom app assembly

- **Decision**: load the assembly with `AssemblyLoadContext.Default.LoadFromAssemblyPath`. The
  app's private dependencies are resolved through an `AssemblyDependencyResolver` hooked to
  `AssemblyLoadContext.Default.Resolving`. Shared assemblies (Dnui, Avalonia) are already
  loaded by the runtime, so the runtime's copies win.
- **Type check**: resolve the type with `assembly.GetType(name, throwOnError: false)`, check
  `typeof(DnuiApp).IsAssignableFrom(type)`, and only then call `Activator.CreateInstance`.
- **Version check** (constitution V, SHOULD): compare the `Dnui` `AssemblyName.Version` that
  the app references with the runtime's loaded Dnui version. A mismatch exits with a distinct
  code.
- **Rationale**: a custom `AssemblyLoadContext` would load a second copy of Dnui and Avalonia,
  making `DnuiApp` type checks fail and breaking Avalonia's static state. The default context
  with a resolver is the standard plugin pattern when sharing types.
- **Alternatives**: an isolated `AssemblyLoadContext` with shared-assembly forwarding. More
  moving parts for no isolation benefit, since the runtime hosts exactly one app per process.

## R4. CLI parsing

- **Decision**: System.CommandLine 2.0.x (latest stable 2.0.14).
  - The root command has recursive options `--app-assembly <path>` and `--app-type <name>`,
    plus a `tray` subcommand with no arguments in v1.
  - The program calls `Parse(args)`, then inspects `ParseResult` itself, instead of using the
    invocation pipeline. This keeps `Main` synchronous and in control of exit codes.
  - Parse errors are written to stderr and return the usage exit code.
  - `--help` and `--version` keep their default behavior and write to stdout. They are never
    combined with tray mode.
- **Rationale**: the user requires System.CommandLine. Parse-only usage fits the "everything
  synchronous until Avalonia starts" design.
- **Alternatives**: the invocation pipeline (`SetAction` / `Invoke`). It would hide the
  ordering between parsing, `SetCommand`, and builder setup, and adds async handling the
  synchronous `Main` doesn't need.

## R5. JSON-RPC library and framing

- **Decision**: StreamJsonRpc (2.26.x) with `NewLineDelimitedMessageHandler` and
  `SystemTextJsonFormatter`, over the raw stdin and stdout streams.
  - Methods are mapped with `[JsonRpcMethod("initialize")]`, `[JsonRpcMethod("tray/setItems")]`,
    and so on.
  - Errors are thrown as `LocalRpcException` with application error codes (see
    [contracts/tray-protocol.md](contracts/tray-protocol.md)).
- **Rationale**: StreamJsonRpc supports newline-delimited framing out of the box, and it will
  be reused for the caller-side client and the future daemon. System.Text.Json avoids a
  Newtonsoft dependency.
- **Dependency justification** (constitution: new library dependencies must be justified): the
  tray command runs inside the library's `DnuiCommandApp`, so the library needs a JSON-RPC
  implementation. Writing one by hand (framing, request correlation, error objects,
  cancellation) costs more and carries more risk than this Microsoft-maintained package.
- **Alternatives**: a hand-written JSON-RPC layer over System.Text.Json. Smaller dependency
  footprint, but recreates a protocol stack. Header-delimited framing was rejected in spec
  clarification (FR-014).

## R6. Stdio capture, Resolve, and protecting stdout

- **Decision**: stdout capture is done for **every app**, not just tray:
  1. After app resolution succeeds and **before creating the instance**, the runtime builds the
     public `DnuiStdio` from `Console.OpenStandardInput()` and `Console.OpenStandardOutput()`,
     records `IsRedirected`, and calls `Console.SetOut(Console.Error)`.
  2. `DnuiStdio` is handed to the base `DnuiApp` constructor through an internal ambient slot
     that the runtime sets before calling `Activator.CreateInstance` and clears right after.
     This makes `Stdio` valid inside subclass constructors without a constructor parameter.
     Tests construct apps the same way.
  3. `DnuiApp.Resolve(JsonNode?, int)` uses an `Interlocked` first-call guard and posts to the
     UI thread:
     - If the result isn't null and no session is open, write
       `result.ToJsonString()` + `\n` to the raw stream and flush it.
     - Then call `lifetime.Shutdown(exitCode)`.
  4. Before the lifetime starts, `Resolve` doesn't post. It records the result and exit code,
     and the runtime writes them and exits without starting Avalonia.
- **Runtime-level failures** (exit codes 1, 2, 10–13) go through the same `DnuiStdio` writer.
  For failures before any instance exists, the runtime creates `DnuiStdio` early just for
  that.
  When the requested command is a session command, the runtime writes nothing. "Requested"
  means the parse result names `tray`, or, when parsing fails, the first non-option token is
  `tray`.
- **Tray**: when `SetCommand(TrayCommand)` runs (synchronous phase), the tray command:
  1. Calls `app.Stdio.OpenSession()`, the same public API consumers use. A `DnuiStdioException`
     with reason `NotRedirected` exits with code 20, and `AlreadyClaimed` exits with code 22.
     Neither writes any output. From then on, non-null `Resolve` results are dropped and
     reported on stderr.
  2. Creates the `JsonRpc` object over `session.Input` and `session.Output`, and registers
     targets, but doesn't call `StartListening()`. `session.Output` is a wrapper whose
     `Dispose` doesn't close the raw stdout handle, so `Resolve` and the runtime can still
     flush it.
- **Rationale**: the raw handles stay valid after `SetOut`, and only the protocol writer holds
  the stdout stream. Unread input waits in the OS pipe buffer until listening starts (FR-010).
- **Known limit**: native code writing directly to file descriptor 1 bypasses `Console.SetOut`.
  This is acceptable, since no current dependency does that. Documented in the contract.
- **Alternatives**: redirecting file descriptor 1 at the OS level (`dup2`, `SetStdHandle`).
  Bulletproof, but platform-specific code for a hypothetical risk. Revisit if it ever shows up.

## R7. Starting to listen, threading, and shutdown

- **Decision**: in `DnuiCommandApp.OnFrameworkInitializationCompleted`, the tray command:
  - Sets `ShutdownMode.OnExplicitShutdown`.
  - Creates the tray host, then calls `rpc.StartListening()`.
  - Attaches `rpc.Completion.ContinueWith(_ => app.ResolveInternal(null, 0))`, so tray goes
    through the same shutdown path as every other command. `Resolve` is `protected`, so
    internal callers (commands, the runtime) use an `internal` twin. The protected method is a
    thin wrapper over it.

  Handlers `await Dispatcher.UIThread.InvokeAsync(...)` for every UI change and return only
  after it completes (FR-013).
- **Notification ordering**: click events are raised on the UI thread and written to an
  unbounded single-reader `Channel<Notification>`. One background loop drains it with
  `await rpc.NotifyAsync(...)`. This keeps order and never blocks the UI thread when the caller
  reads slowly (edge case: caller stops reading).
- **Caller exit**: when the caller exits or crashes, stdin reaches end-of-file and the
  connection completes, which triggers shutdown. A write to a closed stdout throws
  `IOException`, which also completes the connection. .NET ignores SIGPIPE, so there's no
  crash.
- **Alternatives**: calling `StartListening()` during the synchronous phase. Rejected: handlers
  could run before the dispatcher and platform are ready.

## R8. Tray implementation on Avalonia 12

- **Decision**:
  - Use `TrayIcon` with `TrayIcon.SetIcons(app, new TrayIcons { icon })`.
  - The menu is a `NativeMenu` of `NativeMenuItem`s (`Header`, `IsEnabled`,
    `ToggleType = CheckBox` with `IsChecked`, `Menu` for submenus) and
    `NativeMenuItemSeparator`.
  - Item clicks come from `NativeMenuItem.Click` and become `tray/clicked`. The menu opening
    comes from `NativeMenu.Opening` and becomes `tray/opened`. `TrayIcon.Clicked` isn't used,
    because platforms disagree on whether clicking the icon opens the menu, while the menu
    opening is the same everywhere.
  - An empty item list builds a single disabled `NativeMenuItem` ("No items", reserved id `""`)
    instead of an empty `NativeMenu`, because an empty menu may not open at all on some
    platforms.
  - `TrayIcon.IsVisible` stays false until an icon is set.
  - `setItems` builds a fresh `NativeMenu` and assigns it in one step, after validation.
- **Icons**: decode with `new Bitmap(stream)` from a file stream or base64 bytes, on the UI
  thread, then `new WindowIcon(bitmap)`. A decode failure becomes an `InvalidIcon` error and
  the previous icon stays.
- **Seam**: an internal `ITrayHost` interface (`Apply(TrayState)`, `ItemClicked`,
  `MenuOpened`). `AvaloniaTrayHost` is the production implementation, and tests use a fake.
  This lets protocol logic be unit-tested without a desktop.
- **Platform notes**: `NativeMenu.Opening` is expected to fire for tray menus on all three
  platforms (Win32 popup menu, macOS `NSMenu` will-open, and DBusMenu `AboutToShow` on Linux).
  This is unverified. Quickstart validation confirms it on each OS. If a platform doesn't
  raise it, its tray host adapter must raise `MenuOpened` some other way, because FR-023
  requires it everywhere.
- **Alternatives**: reporting `TrayIcon.Clicked` as an icon-click event. Rejected: macOS opens
  the menu on any click and reports no click, so it can't be made consistent.

## R9. Linux without a tray host (FR-026)

- **Decision**: before starting Avalonia, when the parsed command is `tray` and the OS is
  Linux, the runtime checks whether the session bus has an owner for
  `org.kde.StatusNotifierWatcher`. It uses Tmds.DBus.Protocol, which Avalonia.Desktop already
  brings in, so the runtime gets no new direct dependency. If there's no owner, or no session
  bus, it exits with code 21. The check lives in `Dnui.Runtime/Platform/LinuxTrayHostProbe.cs`
  (a platform adapter, constitution IV).
- **Rationale**: Avalonia's Linux tray uses StatusNotifierItem over DBus and doesn't show
  anything when no watcher exists. Failing loudly meets FR-026.
- **Alternatives**: falling back to the XEmbed system tray protocol. Avalonia doesn't support
  it, so out of scope.

## R10. Testing strategy

- **Decision**: xUnit v3, three test projects plus a test-apps assembly:
  - `tests/Dnui.Tests`: unit tests for menu validation, icon decoding errors, and protocol
    handlers driven end-to-end through StreamJsonRpc over in-memory pipes with a fake
    `ITrayHost`. Covers ordering, click notifications, not-initialized and
    already-initialized errors, and the 1,000-click count (SC-007).
  - `tests/Dnui.Runtime.ContractTests`: start the built runtime executable, mostly through the
    `DnuiTray` client (R12), and with raw process streams for framing and stray-output checks. Covers exit codes (every case in FR-003, plus 13, 20, 21),
    `initialize`/`setItems`/`setIcon` round trips, end-of-file and kill shutdown timing
    (SC-004), and the stray-output flood (SC-005).
  - `tests/Dnui.TestApps`: a class library containing a custom `DnuiCommandApp`, a
    full-takeover `DnuiApp`, a non-`DnuiApp` type, and a `Console.WriteLine` spammer. It also
    has `EchoSessionApp`, a takeover app that opens a session and echoes lines, and
    `EagerSessionCommandApp`, a command app that claims stdio in its constructor to trigger
    exit 22. The contract tests load it with `--app-assembly`.
- **Desktop in CI**: contract tests run `tray --headless`, a public, always-available option
  (FR-047) that swaps `AvaloniaTrayHost` for `HeadlessTrayHost`. That host keeps the same
  `TrayState`, but shows nothing. Tests assert with the public `tray/getState`, and drive
  events with `tray/simulateOpen` and `tray/simulateClick` (FR-048–FR-049). These are real
  protocol features, not test hooks, so the suite runs against Release packages too (SC-013),
  and consumers can use the same approach for their own apps. CI still exercises the full
  process, Avalonia startup, and dispatcher path. Linux CI runs under `xvfb-run`. The real tray
  on each OS is checked manually with [quickstart.md](quickstart.md).
- **Simulation semantics**: `simulateOpen` and `simulateClick` call the same `TrayNotifier`
  path that host events use, and wait until the notification has been written before
  responding. `simulateClick` checks the target against the current state using the user's
  rules (enabled leaf only), else -32012 `InvalidSimulation`. Both work in headless and real
  mode. The earlier Debug-only `DNUI_TRAY_HOST` and `$/dnui/test/*` design was dropped at the
  user's request (2026-11-11), because always-on public features are simpler and more useful.
- **Alternatives**: Avalonia.Headless. It has no tray platform implementation, so it can't
  replace the host seam. Purely manual tray testing fails the constitution's contract-test
  gate.

## R11. Exit codes

- **Decision**: see [contracts/cli.md](contracts/cli.md).
  - 0 success, 1 unexpected error, 2 usage error.
  - 10–13: app resolution (assembly, type, not a `DnuiApp`, Dnui version mismatch).
  - 20–21: tray environment (streams not redirected, no tray host).
  - 64 and above: free for apps' own error results.
  - Each runtime code has a stable `error.code` name, used in error results for non-session
    commands.
- **Rationale**: grouping by tens leaves room for new commands without renumbering.
  System.CommandLine's default parse error code is 1, so it's overridden to 2 to separate
  usage errors from crashes.

## R12. Tray client in the library

- **Decision**: the .NET tray client (`DnuiTray`, see [contracts/tray-client.md](contracts/tray-client.md))
  is part of this feature and lives in the Dnui library.
  - It starts the runtime with `System.Diagnostics.Process`: stdin and stdout redirected,
    stderr not redirected, `UseShellExecute = false`.
  - It uses StreamJsonRpc with `NewLineDelimitedMessageHandler` over
    `process.StandardInput.BaseStream` and `process.StandardOutput.BaseStream`.
  - Notifications go to a private target object that forwards them through a single
    `Channel`. One consumer loop raises `Opened` and `Clicked` in order, and catches handler
    exceptions.
  - `Completion` is `process.WaitForExitAsync()` mapped to `DnuiResult(exitCode, null)`.
  - `StartAsync` races the initialize call against `rpc.Completion` and process exit. On a lost
    connection it awaits exit and throws `DnuiTrayStartException`. On cancellation it calls
    `process.Kill(entireProcessTree: true)`.
  - `DisposeAsync` disposes the `JsonRpc` (closing stdin), awaits exit up to
    `ShutdownTimeout`, then kills the process.
- **Rationale**: the client is the other half of the protocol, and the contract tests need a
  .NET driver anyway. Writing it once, as product code, gives both the consumer API and the
  test harness. The user decided the tray client belongs to the tray spec.
- **Runtime**: `StartAsync` first calls `DnuiRuntime.ResolveAsync(options.Runtime)` (R13).
  `RunAsync` comes in a later feature, and `DnuiResult` and `DnuiRuntimeOptions` are
  introduced now so it can reuse them.
- **Alternatives**: a test-only client. That duplicates work and leaves consumers with raw
  JSON-RPC.

## R13. Runtime acquisition (download, cache, lock, verify)

- **Decision**: implement acquisition in the library with only BCL APIs (`HttpClient`,
  `System.IO.Compression.ZipArchive`, `System.Text.Json`, `System.Xml.Linq`, and
  `File.SetUnixFileMode`), following the algorithm in
  [contracts/runtime-acquisition.md](contracts/runtime-acquisition.md).
  - **Feed**: read the v3 service index once per process (cached in memory). Take the
    `PackageBaseAddress/3.0.0` resource and download from the flat container
    `{base}/{id-lower}/{version-lower}/{id-lower}.{version-lower}.nupkg`. A local folder source
    looks for `{id}.{version}.nupkg`, ignoring case.
  - **RID**: `RuntimeInformation.RuntimeIdentifier`, which is the portable RID on .NET 8 and
    later, checked against the six supported RIDs.
  - **Lock**: `new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)`,
    retried every 250 ms until `LockTimeout` or cancellation. The OS releases it if the process
    dies, so stale locks never block.
  - **Atomic publish**: extract to `.tmp-{guid}` under the same parent folder, write
    `.dnui-complete` last, then `Directory.Move` it to `{version}`. A rename within one
    folder is atomic on every supported OS. Leftover `.tmp-*` folders are deleted while
    holding the lock.
  - **Verify**: open the archive and read every entry to the end, which makes `ZipArchive`
    validate each CRC-32. Parse the `.nuspec` and require an exact id and version match.
  - **Executable bit**: `File.SetUnixFileMode(exe, UserRead|UserWrite|UserExecute|GroupRead|GroupExecute|OtherRead|OtherExecute)`
    on macOS and Linux. Downloads through `HttpClient` don't get the macOS quarantine
    attribute, so Gatekeeper doesn't block the extracted runtime.
- **Rationale**: the BCL covers everything needed. NuGet.Protocol/NuGet.Packaging would bring
  a large dependency tree into every consumer (constitution II: library dependencies must be
  justified) for one flat-container download and a zip extract.
- **Integrity scope**: CRC plus manifest checks catch truncated or corrupted downloads and
  wrong packages. They don't defend against a malicious feed. Repository-signature or
  SHA-512 verification is a later hardening step, confirmed with the user (2026-11-11).
- **Verification cost**: checks run once, at install time. The `.dnui-complete` marker is
  written only after they pass, and later resolutions only test that the marker exists (one
  `File.Exists`), so the warm path stays fast (SC-011). A forced reinstall option is planned
  for later.
- **Alternatives**: `dotnet tool`-style installation, or shelling out to `nuget`/`dotnet`.
  Rejected: consumers can't be assumed to have an SDK, and it's slow. NuGet.Protocol:
  rejected for size, as above.

## R14. Runtime packaging per RID

- **Decision**: add a `PackRuntime` MSBuild target, invoked as
  `dotnet build Dnui.Runtime -t:PackRuntime -p:RuntimeIdentifier=<rid>`. It runs a
  self-contained `dotnet publish` for the RID into
  `artifacts/runtime/<rid>/`, then packs a generated `.nuspec`
  (`Cmdless.Dnui.Runtime.<rid>`, version `$(DnuiRuntimeVersion)`) with the publish output
  under `runtime/`. The output goes to `artifacts/packages/`, which doubles as the local
  folder feed for tests and development.
  - CI packs all six RIDs. Each RID can be cross-published from any OS, because no native
    compilation is involved.
  - Publishing settings: `SelfContained=true`, `PublishSingleFile=false` (Avalonia native
    libraries load more reliably from a folder), `PublishTrimmed=false` (app assemblies loaded
    at run time need the full framework).
- **Rationale**: one target keeps packaging reproducible locally and in CI. A folder feed lets
  the contract tests exercise real download and extraction without network access.
- **Alternatives**: a separate packaging `.csproj` per RID (six near-identical projects), or
  `dotnet pack` with `RuntimeIdentifiers` (NuGet's built-in per-RID packing produces one
  package with `runtimes/<rid>/`, not separate packages).

