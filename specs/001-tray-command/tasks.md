---

description: "Task list for the tray command, protocol, tray client, and runtime acquisition"
---

# Tasks: Tray Command and Protocol

**Input**: Design documents from `/specs/001-tray-command/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (cli.md,
tray-protocol.md, app-model.md, tray-client.md, runtime-acquisition.md), quickstart.md

**Tests**: included. The constitution's Development Workflow requires CLI contract tests for
every built-in command, the app-resolution test matrix, and acquisition tests (cache hit, cache
miss, concurrency, corrupted or partial cache). Write each story's tests first, and make sure
they fail before implementing.

**Organization**: phases follow spec priority: US1 (P1), US6 (P1), US2 (P2), US3 (P2), US5 (P2),
US4 (P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: User story from spec.md (US1–US6)

## Path Conventions

Root-level projects: `Dnui/` (library, package `Cmdless.Dnui`), `Dnui.Runtime/` (host
executable), and `tests/`. C# namespaces stay `Dnui.*`; only package ids use `Cmdless.*`.
Shared versions come from `Directory.Build.props` (`DnuiRuntimeVersion`, `Version`,
`AvaloniaVersion`).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependencies, test projects, CI skeleton

- [ ] T001 Add `<PackageReference Include="StreamJsonRpc" Version="2.26.150" />` to Dnui/Dnui.csproj and `<PackageReference Include="System.CommandLine" Version="2.0.14" />` to Dnui.Runtime/Dnui.Runtime.csproj; run `dotnet build Dnui.slnx` and confirm 0 warnings
- [ ] T002 [P] Create xUnit v3 test project tests/Dnui.Tests/Dnui.Tests.csproj (net10.0, `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) referencing Dnui/Dnui.csproj, and add it to Dnui.slnx
- [ ] T003 [P] Create xUnit v3 test project tests/Dnui.Runtime.ContractTests/Dnui.Runtime.ContractTests.csproj referencing Dnui/Dnui.csproj and Dnui.Runtime/Dnui.Runtime.csproj (`ReferenceOutputAssembly="false"`, so the runtime builds first). Embed the runtime's output path as `AssemblyMetadata("DnuiRuntimeExe", "<path to Dnui.Runtime[.exe]>")`, then add the project to Dnui.slnx
- [ ] T004 [P] Create class library tests/Dnui.TestApps/Dnui.TestApps.csproj (net10.0) referencing Dnui/Dnui.csproj, add it to Dnui.slnx, and make tests/Dnui.Runtime.ContractTests reference it with `ReferenceOutputAssembly="false"` so it builds first
- [ ] T005 [P] Delete Dnui.Runtime/MainWindow.axaml and Dnui.Runtime/MainWindow.axaml.cs (the window command is future work)
- [ ] T006 [P] Create Dnui/Properties/AssemblyInfo.cs with `InternalsVisibleTo("Dnui.Runtime")`, `InternalsVisibleTo("Dnui.Tests")`. Contract tests use only public API
- [ ] T007 [P] Create .github/workflows/ci.yml: matrix `windows-latest`, `macos-latest`, `ubuntu-latest`. Set up .NET 10, `dotnet build Dnui.slnx -c Debug`, and `dotnet test Dnui.slnx -c Debug`. On Ubuntu, wrap the test step in `xvfb-run -a`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the app model, stdio ownership, `Resolve`, the runtime CLI skeleton, and exit
codes ([contracts/app-model.md](contracts/app-model.md), [contracts/cli.md](contracts/cli.md),
research R1–R4, R6, R11)

**⚠️ CRITICAL**: no user story work can begin until this phase is complete

### Tests for Foundation

- [ ] T008 [P] Write tests/Dnui.Tests/TestSupport/TestStdio.cs: builds an internal `DnuiStdio` over in-memory pipes (redirected or not), and an `AmbientStdioScope` that sets the ambient slot while a test constructs a `DnuiApp` subclass
- [ ] T009 [P] Write tests/Dnui.Tests/Foundation/DnuiStdioTests.cs:
  - `OpenSession()` succeeds once, and the second call throws `DnuiStdioException` with `Reason = AlreadyClaimed`
  - it throws `NotRedirected` when not redirected
  - disposing `session.Output` doesn't close the underlying stream
  - `IsSessionOpen` flips
- [ ] T010 [P] Write tests/Dnui.Tests/Foundation/ResolveTests.cs:
  - the first `Resolve` wins
  - a non-null result writes exactly one line (`JsonNode.ToJsonString()` + `\n`, no inner newlines)
  - `Resolve(null, n)` writes nothing
  - a non-null result while a session is open is dropped (and a warning is written to the test's stderr capture), but the exit code is kept
  - `Resolve` before the lifetime starts records a pending resolution instead of posting to the dispatcher
- [ ] T011 [P] Write tests/Dnui.Runtime.ContractTests/Support/RuntimeProcess.cs:
  - starts the runtime executable from `AssemblyMetadata("DnuiRuntimeExe")` with given args (tray tests pass `tray --headless`)
  - redirects stdin and stdout, and redirects stderr only to **drain** it into a buffer on a background task
  - exposes `StdOutLines` (async enumerable), `WriteLineAsync`, `CloseStdin()`, and `WaitForExitAsync(timeout)` → exit code
- [ ] T012 [P] Write tests/Dnui.Runtime.ContractTests/CliTests.cs. Each of these exits with 2:
  - an unknown option without a command writes exactly one stdout line `{"error":{"code":"Usage",...}}`
  - `tray --bogus` writes nothing to stdout
  - `--app-type X` without `--app-assembly` (with a command) writes a `Usage` error line

### Implementation for Foundation

- [ ] T013 [P] Create Dnui/DnuiResult.cs: `public sealed record DnuiResult(int ExitCode, string? StdOut);`
- [ ] T014 [P] Create Dnui/DnuiStdioException.cs with `public enum DnuiStdioFailure { NotRedirected, AlreadyClaimed }` and `public sealed class DnuiStdioException : InvalidOperationException` exposing `Reason`
- [ ] T015 [P] Create Dnui/Internal/DnuiBuildInfo.cs: `internal static string RuntimeVersion`, read once from this assembly's `AssemblyMetadata("DnuiRuntimeVersion")`
- [ ] T016 Create Dnui/DnuiSession.cs: `public sealed class DnuiSession` with `Input` (raw stdin) and `Output`, where `Output` is a private `NonClosingStream` wrapper whose `Dispose` flushes without closing the inner stream (depends on T014)
- [ ] T017 Create Dnui/DnuiStdio.cs: `public sealed class DnuiStdio`
  - internal ctor `(Stream stdin, Stream stdout, bool isRedirected)`
  - `IsRedirected`, `IsSessionOpen`
  - `OpenSession()`, guarded once with `Interlocked`, throwing `DnuiStdioException` (`NotRedirected` or `AlreadyClaimed`)
  - `internal void WriteResultLine(JsonNode)`, which writes one line and flushes
  - `internal static DnuiStdio CreateFromConsole()`: captures `Console.OpenStandardInput()` and `Console.OpenStandardOutput()`, sets `isRedirected = Console.IsInputRedirected && Console.IsOutputRedirected`, then calls `Console.SetOut(Console.Error)`
  - an `internal static` ambient slot (`[ThreadStatic] Current`), set by the runtime only around app construction (depends on T016)
- [ ] T018 Create Dnui/Commands/DnuiCommand.cs: `internal abstract class DnuiCommand` with:
  - `bool IsSession`
  - `bool HideDockIcon` (platform hint, applied by the runtime; the library never references Avalonia.Native)
  - `int? Prepare(DnuiCommandApp app)`: the synchronous phase, returning an exit code to abort with
  - `void ConfigureBuilder(AppBuilder builder)`: non-platform builder options only
  - `void Run(DnuiCommandApp app, IClassicDesktopStyleApplicationLifetime lifetime)`
- [ ] T019 Create Dnui/DnuiApp.cs: `public abstract class DnuiApp : Application`
  - The ctor takes `DnuiStdio.Current`, and throws `InvalidOperationException("DnuiApp instances are created by Dnui.Runtime")` if it's null.
  - `protected DnuiStdio Stdio`.
  - `protected virtual void Configure(AppBuilder builder)` applies `WithInterFont()`, `LogToTrace()`, and in `#if DEBUG` `WithDeveloperTools()`.
  - `internal virtual void ApplyConfiguration(AppBuilder b) => Configure(b)`.
  - `protected void Resolve(JsonNode? result, int exitCode = 0) => ResolveInternal(result, exitCode)`.
  - `internal void ResolveInternal`:
    - an `Interlocked` first-call guard
    - drops a non-null result while `Stdio.IsSessionOpen`, writing a warning to `Console.Error`
    - if the lifetime hasn't started, stores `internal (JsonNode? Result, int ExitCode)? PendingResolution`
    - otherwise `Dispatcher.UIThread.Post` → `WriteResultLine` (when not null) → `((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).Shutdown(exitCode)`

  (depends on T017, T018)
- [ ] T020 Create Dnui/DnuiCommandApp.cs: `public abstract class DnuiCommandApp : DnuiApp`
  - `internal DnuiCommand? Command` and `internal void SetCommand(DnuiCommand)`
  - `internal override ApplyConfiguration`: `Command.ConfigureBuilder(b)`, then `Configure(b)`
  - `public sealed override void OnFrameworkInitializationCompleted()`: runs `Command.Run(this, desktopLifetime)`, then `base.OnFrameworkInitializationCompleted()`

  (depends on T019)
- [ ] T021 [P] Create Dnui.Runtime/Hosting/ExitCodes.cs with constants and stable `error.code` names exactly per [contracts/cli.md](contracts/cli.md): `0`; `1 Unexpected`; `2 Usage`; `10 AppAssemblyLoadFailed`; `11 AppTypeNotFound`; `12 AppTypeInvalid`; `13 DnuiVersionMismatch`; `20` and `21` (exit code only); `22` (exit code only, already claimed)
- [ ] T022 Create Dnui.Runtime/Hosting/RuntimeFailure.cs: `Fail(DnuiStdio stdio, bool sessionRequested, int code, string name, string message)`. It writes `{"error":{"code":name,"message":message}}` through `WriteResultLine` unless `sessionRequested`, writes a diagnostic to stderr, and returns `code` (depends on T017, T021)
- [ ] T023 Create Dnui.Runtime/Cli/RuntimeCommandLine.cs using System.CommandLine 2.0.14:
  - a root command with recursive options `--app-assembly <path>` and `--app-type <name>`
  - an empty `tray` subcommand placeholder
  - `Parse(string[] args)` returns a `RuntimeInvocation` record (`AppAssembly`, `AppType`, `CommandName?`, `SessionRequested`, `Errors`, `IsHelpOrVersion`)
  - `SessionRequested` is true when the parsed command is a session command, or when parsing failed and the first non-option token is `tray`
  - `--help` and `--version` keep default behavior (stdout)
- [ ] T024 Change Dnui.Runtime/App.axaml.cs so `App : DnuiCommandApp` (remove the `OnFrameworkInitializationCompleted` override and the `MainWindow` usage). Keep Dnui.Runtime/App.axaml's root as `<Application>` with `x:Class="Dnui.Runtime.App"` (depends on T020)
- [ ] T025 Create Dnui.Runtime/Platform/PlatformCommandOptions.cs: `Apply(AppBuilder, DnuiCommand)` calls `.With(new MacOSPlatformOptions { ShowInDock = !command.HideDockIcon })` (research R2; platform adapter per constitution IV) (depends on T018)
- [ ] T026 Rewrite Dnui.Runtime/Program.cs as `[STAThread] static int Main(string[] args)`, fully synchronous, in this order:
  1. Parse (help or version → return the parser's exit code).
  2. `var stdio = DnuiStdio.CreateFromConsole()`.
  3. Report parse errors as `Usage` through `RuntimeFailure`.
  4. Resolve the app type (only the built-in `typeof(App)` in this phase; US4 adds `AppTypeResolver`).
  5. Create the instance with `DnuiStdio.Current = stdio` set, clearing it in `finally`.
  6. For a `DnuiCommandApp`, require a command (missing → `Usage`), create it, call `Prepare` (a non-null exit code → return it), and call `SetCommand`. For a plain `DnuiApp`, a command is a `Usage` error.
  7. If `app.PendingResolution` is set, write it and return its code.
  8. `var builder = AppBuilder.Configure(() => app).UsePlatformDetect()`, then `PlatformCommandOptions.Apply`, then `app.ApplyConfiguration(builder)`.
  9. `return builder.StartWithClassicDesktopLifetime([])`.

  Wrap everything in `try/catch`: an unhandled exception → `RuntimeFailure` `1 Unexpected`. Keep `BuildAvaloniaApp()` for the designer (depends on T019–T025)

**Checkpoint**: `dotnet test` passes T009, T010, and T012. The runtime starts the built-in `App` and reports usage errors per the contract.

---

## Phase 3: User Story 1 - Show a tray icon and react to menu clicks (Priority: P1) 🎯 MVP

**Goal**: `tray` shows an icon and menu from `initialize`, and reports `tray/opened` and
`tray/clicked`, over newline-delimited JSON-RPC on the stdio session.

**Independent Test**: start the runtime with `tray`, send `initialize` with an icon and items,
see the icon and menu, open the menu (`tray/opened`), click an item (`tray/clicked` with its id).

### Tests for User Story 1

- [ ] T027 [P] [US1] Write tests/Dnui.Tests/Tray/TrayMenuValidatorTests.cs covering [data-model.md](data-model.md) MenuItem validation:
  - "A non-separator item without a non-empty `id` → `InvalidItems` with the item's path, for example `items[2].items[0]`"
  - "A non-separator item without `text` → `InvalidItems` with the path"
  - "An `id` that appears more than once at any depth → `InvalidItems` with the duplicate `id` and both paths"
  - separators ignore all other fields
  - `items: []` counts as no submenu
  - the first failure is the one reported
- [ ] T028 [P] [US1] Write tests/Dnui.Tests/Tray/TrayIconDecoderTests.cs: exactly one of `path`/`png` is allowed ("Both, neither, an unreadable file, bad base64, or undecodable image data → `InvalidIcon`"); a valid 16×16 PNG decodes
- [ ] T029 [P] [US1] Write tests/Dnui.Tests/TestSupport/FakeTrayHost.cs (an `ITrayHost` that records every `Apply(TrayState)` and can raise `MenuOpened` and `ItemClicked(id)` on demand), plus tests/Dnui.Tests/TestSupport/InMemoryTraySession.cs (StreamJsonRpc over a pair of `Pipe`s with `NewLineDelimitedMessageHandler`, wired to `TrayProtocolTarget` and `TrayNotifier` with the fake host and a synchronous test dispatcher)
- [ ] T030 [P] [US1] Write tests/Dnui.Tests/Tray/TrayInitializeTests.cs using InMemoryTraySession:
  - `initialize {protocolVersion:1, icon, tooltip, items}` → `{runtimeVersion}` equals `DnuiBuildInfo.RuntimeVersion`, after `Apply` was called
  - `protocolVersion:2` → -32003 with data `{"requested":2,"supported":[1]}`, and nothing applied
  - `tray/setItems` before initialize → -32001
  - a second initialize → -32002
  - initialize with invalid items → -32010, nothing applied
- [ ] T031 [P] [US1] Write tests/Dnui.Tests/Tray/TrayNotificationTests.cs:
  - the fake host raises `MenuOpened` → `tray/opened {}`
  - `ItemClicked("open")` → `tray/clicked {"id":"open"}`
  - 1,000 interleaved events arrive in order with no loss or duplicates (SC-007)
  - an empty item list produces the placeholder (`id ""`, text `No items`, disabled) in the applied `TrayState`
  - `tray/simulateOpen` → `tray/opened` is written **before** its response, and `tray/simulateClick {"id":"open"}` → `tray/clicked` before its response
  - `simulateClick` on an unknown id, a disabled item, a separator, or a submenu parent → -32012 with `reason` `unknown`, `disabled`, `separator`, or `parent`
  - `tray/getState` returns `{visible, hasIcon, tooltip, items}` with the caller's items (never the placeholder)
  - all three methods return -32001 before initialize
- [ ] T032 [P] [US1] Write tests/Dnui.Runtime.ContractTests/TrayInitializeContractTests.cs:
  - start `tray --headless` with RuntimeProcess, send a raw newline-delimited `initialize` line, and assert one JSON-RPC response line with `result.runtimeVersion`
  - `tray/getState` shows `visible:true`, `hasIcon:true`, the tooltip, and the items
  - `tray/simulateOpen` and `tray/simulateClick` produce the notifications on stdout before their responses
  - assert no non-JSON-RPC line ever appears on stdout

### Implementation for User Story 1

- [ ] T033 [P] [US1] Create Dnui/Tray/TrayProtocolMessages.cs: System.Text.Json DTOs (camelCase) per [contracts/tray-protocol.md](contracts/tray-protocol.md):
  - `InitializeParams { int ProtocolVersion; TrayIconSource? Icon; string? Tooltip; List<TrayMenuItem>? Items }` and `InitializeResult { string RuntimeVersion }`
  - `TrayMenuItem { string? Id; string? Text; bool Enabled = true; bool? Checked; bool Separator; List<TrayMenuItem>? Items }`
  - `TrayIconSource { string? Path; string? Png }`
  - `SetItemsParams { List<TrayMenuItem> Items }` and `SetIconParams { TrayIconSource Icon; string? Tooltip }`
  - `ClickedNotification { string Id }`
- [ ] T034 [P] [US1] Create Dnui/Tray/TrayErrorCodes.cs: `NotInitialized = -32001`, `AlreadyInitialized = -32002`, `ProtocolVersionMismatch = -32003`, `InvalidItems = -32010`, `InvalidIcon = -32011`, `InvalidSimulation = -32012`, plus a `Throw(code, message, object? data)` helper that throws `LocalRpcException { ErrorCode, ErrorData }`
- [ ] T035 [P] [US1] Create Dnui/Tray/TrayMenuValidator.cs: validates the whole tree before anything is applied, returning the first failure as `(path, id?, reason: "missing-id" | "missing-text" | "duplicate-id")` per [data-model.md](data-model.md)
- [ ] T036 [P] [US1] Create Dnui/Tray/TrayIconDecoder.cs: from a `TrayIconSource`, requires exactly one of `path`/`png`, reads the file (relative paths resolved against `Environment.CurrentDirectory`) or decodes base64, then creates `new Bitmap(stream)` and `new WindowIcon(bitmap)`. Must run on the UI thread. Any failure → `InvalidIcon` with `{"reason":"..."}`
- [ ] T037 [P] [US1] Create Dnui/Tray/ITrayHost.cs: `internal interface ITrayHost : IDisposable { void Apply(TrayState state); event Action? MenuOpened; event Action<string>? ItemClicked; }` and `internal sealed record TrayState(WindowIcon? Icon, string? Tooltip, IReadOnlyList<TrayMenuItem> Items)`, where the host shows the icon only when `Icon != null`
- [ ] T038 [US1] Create Dnui/Tray/AvaloniaTrayHost.cs (depends on T037):
  - `TrayIcon` registered with `TrayIcon.SetIcons(app, new TrayIcons { icon })`, with `IsVisible = state.Icon != null` and `ToolTipText`
  - builds a new `NativeMenu` on each `Apply`:
    - `NativeMenuItem { Header, IsEnabled, ToggleType = CheckBox when Checked != null, IsChecked }`, submenus through `Menu`, and `NativeMenuItemSeparator`
    - `Click` → `ItemClicked(id)` only for enabled leaves
    - subscribes `NativeMenu.Opening` → `MenuOpened`
  - an empty list → a single `NativeMenuItem { Header = "No items", IsEnabled = false }`, whose click reports id `""`
  - `Dispose` hides and removes the icon
- [ ] T039 [P] [US1] Create Dnui/Tray/HeadlessTrayHost.cs (always compiled, used by `tray --headless`): an `ITrayHost` that keeps the last applied `TrayState` and shows nothing. It raises no events of its own; simulation goes through `TrayProtocolTarget`
- [ ] T040 [US1] Create Dnui/Tray/TrayNotifier.cs: an unbounded single-reader `Channel` of notifications, drained by one loop calling `await rpc.NotifyAsync("tray/opened", new {})` or `NotifyAsync("tray/clicked", new { id })` in order. It's fed from host events on the UI thread without blocking (research R7)
- [ ] T041 [US1] Create Dnui/Tray/TrayProtocolTarget.cs: `[JsonRpcMethod("initialize")]` with the AwaitingInitialize → Running state machine per [data-model.md](data-model.md) Session lifecycle:
  - check the version
  - validate items and decode the icon **inside one `Dispatcher.UIThread.InvokeAsync`**, then apply the state and return `InitializeResult(DnuiBuildInfo.RuntimeVersion)`
  - every other `tray/*` method checks the state first (-32001)

  Also add these, available in every build, per [contracts/tray-protocol.md](contracts/tray-protocol.md) (depends on T033–T040):
  - `[JsonRpcMethod("tray/getState")]`: returns `{visible, hasIcon, tooltip, items}` from the current state (caller items, never the placeholder)
  - `[JsonRpcMethod("tray/simulateOpen")]`: awaits `TrayNotifier` writing `tray/opened`, then returns `{}`
  - `[JsonRpcMethod("tray/simulateClick")]`: finds the id in the current items, allowing only enabled, non-separator leaves (else -32012 with `reason` `unknown`, `disabled`, `separator`, or `parent`; while the placeholder is shown nothing is selectable), awaits `TrayNotifier` writing `tray/clicked`, then returns `{}`

  `TrayNotifier` (T040) needs an `EnqueueAndWaitAsync` that completes once the notification has been flushed.
- [ ] T042 [US1] Create Dnui/Tray/TrayCommand.cs (`IsSession = true`, `HideDockIcon = true`):
  - `Prepare`:
    - `app.Stdio.OpenSession()`, mapping `DnuiStdioException` to 20 (`NotRedirected`) or 22 (`AlreadyClaimed`)
    - `new JsonRpc(new NewLineDelimitedMessageHandler(session.Output, session.Input, new SystemTextJsonFormatter()))` with camelCase JSON options
    - `AddLocalRpcTarget(target)`
    - no `StartListening` yet
  - `Run`:
    - `lifetime.ShutdownMode = OnExplicitShutdown`
    - create the host (`HeadlessTrayHost` when the command was parsed with `--headless`, else `AvaloniaTrayHost`)
    - `rpc.StartListening()`
    - `rpc.Completion.ContinueWith(_ => app.ResolveInternal(null, 0))`, disposing the host on shutdown

  (depends on T041)
- [ ] T043 [US1] Register `tray` in Dnui.Runtime/Cli/RuntimeCommandLine.cs as a session command with a `--headless` option, and make Program.cs create `new TrayCommand(headless)` for it (depends on T042)

**Checkpoint**: US1 tests pass, and quickstart §2 (initialize, opened, clicked) works manually on the dev machine.

---

## Phase 4: User Story 6 - The runtime is found or downloaded automatically (Priority: P1)

**Goal**: `DnuiRuntime.ResolveAsync` returns a runnable runtime path, downloading the exact
`Cmdless.Dnui.Runtime.<rid>` into `{CmdlessRoot}/dotnet/packages/{id}/{version}/` once, with a
lock and an atomic, verified install. A `PackRuntime` target produces the packages.

**Independent Test**: with an empty temp `CmdlessRoot` and a local folder feed, resolve, and
check the install folder, the marker, and the executable bit. Delete the feed and resolve
again, which must succeed offline.

### Tests for User Story 6

- [ ] T044 [P] [US6] Write tests/Dnui.Tests/TestSupport/FakeNupkg.cs: builds an in-memory or on-disk `.nupkg` (zip) with `{id}.nuspec` (`<id>`, `<version>`) and `runtime/Dnui.Runtime[.exe]` (a small script or stub), with options to corrupt bytes after writing (bad CRC) or to mismatch the nuspec id or version
- [ ] T045 [P] [US6] Write tests/Dnui.Tests/Runtime/RootsTests.cs: `GetCmdlessRoot` is the code option > `CMDLESS_ROOT` > `~/.cmdless` (UserProfile), and `GetDnuiRoot` = `{CmdlessRoot}/dotnet`
- [ ] T046 [P] [US6] Write tests/Dnui.Tests/Runtime/RidInfoTests.cs: maps the supported RIDs `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64` to package ids `Cmdless.Dnui.Runtime.<rid>` and executable names, and rejects `linux-musl-x64` with `UnsupportedPlatform`
- [ ] T047 [P] [US6] Write tests/Dnui.Tests/Runtime/PackageCacheTests.cs, each against a temp root and a folder feed:
  - a cold install creates `packages/{id-lower}/{version}/` containing `.dnui-complete` and an executable `runtime/Dnui.Runtime` (Unix mode includes user execute)
  - the warm path with the feed folder deleted succeeds, takes no lock, and doesn't re-verify (assert the feed isn't touched)
  - a folder without `.dnui-complete` is replaced
  - leftover `.tmp-*` folders are removed
  - a corrupt CRC → `IntegrityFailed`, and no `{version}` folder exists afterwards
  - a nuspec mismatch → `IntegrityFailed`
  - a missing package → `PackageNotFound`
  - 10 parallel `ResolveAsync` calls on a cold cache → exactly one download (count source reads) and the same path for all
  - a lock held longer than `LockTimeout` → `LockTimeout`
- [ ] T048 [P] [US6] Write tests/Dnui.Tests/Runtime/HttpFeedTests.cs using a fake `HttpMessageHandler`: the service index → the `PackageBaseAddress/3.0.0` base is used; the nupkg URL is `{base}/{id-lower}/{version-lower}/{id-lower}.{version-lower}.nupkg`; a 404 → `PackageNotFound`; a 500 or network error → `DownloadFailed`
- [ ] T049 [P] [US6] Write tests/Dnui.Runtime.ContractTests/RuntimePackageTests.cs: with a packed runtime for the current RID in `artifacts/packages/` (skip with a clear message if missing), `ResolveAsync(new() { CmdlessRoot = temp, PackageSource = "<repo>/artifacts/packages" })` returns a path, and running it with `--version` exits 0 printing `DnuiRuntimeVersion`

### Implementation for User Story 6

- [ ] T050 [P] [US6] Create Dnui/Runtime/DnuiRuntimeOptions.cs per [contracts/runtime-acquisition.md](contracts/runtime-acquisition.md) (`CmdlessRoot`, `PackageSource`, `RuntimePath`, `LockTimeout` = 5 min)
- [ ] T051 [P] [US6] Create Dnui/Runtime/DnuiRuntimeUnavailableException.cs with `DnuiRuntimeFailure { UnsupportedPlatform, PackageNotFound, DownloadFailed, IntegrityFailed, LockTimeout }`, `PackageId`, `Version`, and a message naming both
- [ ] T052 [P] [US6] Create Dnui/Runtime/RidInfo.cs: supported RIDs, `CurrentRid` from `RuntimeInformation.RuntimeIdentifier`, package id, and executable relative path `runtime/Dnui.Runtime` (`.exe` on Windows)
- [ ] T053 [US6] Create Dnui/Runtime/PackageFeed.cs:
  - `internal interface IPackageSource { Task<Stream?> OpenAsync(string id, string version, CancellationToken ct); }`
  - `FolderPackageSource`: matches `{id}.{version}.nupkg` case-insensitively
  - `HttpPackageSource`: an injectable internal `HttpClient`; the service index is cached per process, and the flat container URL uses lowercase id and version
  - `PackageFeed.Create(string? source)`: default `https://api.nuget.org/v3/index.json`; a path that exists → folder

  (depends on T050, T051)
- [ ] T054 [US6] Create Dnui/Runtime/PackageCache.cs implementing [contracts/runtime-acquisition.md](contracts/runtime-acquisition.md) steps 3–9:
  - **Warm path**: `File.Exists(.dnui-complete)` only.
  - **Lock**: `FileStream(locks/{id-lower}.{version}.lock, OpenOrCreate, ReadWrite, FileShare.None)`, retried every 250 ms until `LockTimeout` or cancellation, then recheck the marker.
  - **Download** to a temp file.
  - **Verify**: read every zip entry to the end (CRC); `.nuspec` id and version must match exactly.
  - **Extract** to `packages/{id-lower}/.tmp-{guid}`, then on non-Windows run `File.SetUnixFileMode` on the executable with `UserRead|UserWrite|UserExecute|GroupRead|GroupExecute|OtherRead|OtherExecute`.
  - **Publish**: write `.dnui-complete` last, delete a stale `{version}` folder and `.tmp-*` leftovers, then `Directory.Move` to `{version}`.

  (depends on T052, T053)
- [ ] T055 [US6] Create Dnui/Runtime/DnuiRuntime.cs: `public static class DnuiRuntime` with:
  - `Version` (from `DnuiBuildInfo`), `CurrentRid`, `PackageId`
  - `GetCmdlessRoot` (option > `CMDLESS_ROOT` > `Path.Combine(UserProfile, ".cmdless")`) and `GetDnuiRoot` (`{CmdlessRoot}/dotnet`)
  - `ResolveAsync`: `RuntimePath` override (must exist, else `PackageNotFound`) → `UnsupportedPlatform` check → `PackageCache`

  (depends on T054)
- [ ] T056 [P] [US6] Create Dnui.Runtime/Packaging/Cmdless.Dnui.Runtime.nuspec (tokens `$id$`, `$version$`, files `runtime/**` from the publish dir) and Dnui.Runtime/Packaging/PackRuntime.targets with a `PackRuntime` target that:
  - requires `RuntimeIdentifier`
  - runs `dotnet publish` with `-c Release -r $(RuntimeIdentifier) --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/runtime/$(RuntimeIdentifier)`
  - packs the nuspec with id `Cmdless.Dnui.Runtime.$(RuntimeIdentifier)` and version `$(DnuiRuntimeVersion)` into `artifacts/packages/`

  Import the targets from Dnui.Runtime/Dnui.Runtime.csproj
- [ ] T057 [US6] Extend .github/workflows/ci.yml: before tests, run `dotnet build Dnui.Runtime -t:PackRuntime -p:RuntimeIdentifier=<runner rid>`, so T049 runs against a real package. Add a separate `pack` job on ubuntu-latest that packs all six RIDs and uploads `artifacts/packages/*.nupkg` as a build artifact (depends on T056)

**Checkpoint**: US6 tests pass, and quickstart §7 works with the local feed.

---

## Phase 5: User Story 2 - Update the menu and icon while running (Priority: P2)

**Goal**: `tray/setItems` and `tray/setIcon` replace state atomically and respond only after
the change is visible. Invalid input leaves the previous state.

**Independent Test**: after initialize, `setItems` with a new list → the response arrives and
the menu matches. Duplicate ids → -32010 and the menu is unchanged. A bad icon → -32011 and the
icon is unchanged.

### Tests for User Story 2

- [ ] T058 [P] [US2] Write tests/Dnui.Tests/Tray/TraySetItemsTests.cs:
  - replacing the list applies exactly the new items before the response
  - a duplicate id across submenus → -32010 with data `{"path":...,"id":...,"reason":"duplicate-id"}`, and the last applied state is unchanged
  - `[]` → placeholder
  - a click on a submenu child → `tray/clicked` with the child id
- [ ] T059 [P] [US2] Write tests/Dnui.Tests/Tray/TraySetIconTests.cs:
  - a valid `png` and `tooltip` → both applied
  - `tooltip` omitted → the previous tooltip is kept
  - both `path` and `png` → -32011, with the previous icon kept
  - an unreadable path → -32011
- [ ] T060 [P] [US2] Write tests/Dnui.Runtime.ContractTests/TrayUpdateContractTests.cs: raw `tray/setItems` and `tray/setIcon` round trips against `tray --headless`, asserting `tray/getState` after each response, and the error codes for the invalid cases

### Implementation for User Story 2

- [ ] T061 [US2] Add `[JsonRpcMethod("tray/setItems")]` to Dnui/Tray/TrayProtocolTarget.cs: require Running, validate with `TrayMenuValidator` (-32010 on failure), then apply `state with { Items }` inside `Dispatcher.UIThread.InvokeAsync`, and return `{}` after `Apply`
- [ ] T062 [US2] Add `[JsonRpcMethod("tray/setIcon")]` to Dnui/Tray/TrayProtocolTarget.cs: require Running, decode with `TrayIconDecoder` on the UI thread (-32011 on failure), apply `state with { Icon, Tooltip = params.Tooltip ?? state.Tooltip }`, and return `{}` (depends on T061, same file)

**Checkpoint**: US1 and US2 tests pass.

---

## Phase 6: User Story 3 - Tray goes away with its caller (Priority: P2)

**Goal**: end-of-file on stdin, or the caller dying, removes the icon and exits with code 0. A
missing tray host or stdio that isn't redirected fails fast with an exit code and no stdout.

**Independent Test**: start the runtime and initialize it, then close stdin (or kill the
harness side), and the runtime exits within 2 s.

### Tests for User Story 3

- [ ] T063 [P] [US3] Write tests/Dnui.Runtime.ContractTests/TrayLifecycleContractTests.cs:
  - after initialize, `CloseStdin()` → exit code 0 within 2 s, and no stdout output after the last response (SC-004)
  - abruptly disposing both pipe ends without closing gracefully → the process exits within 2 s
  - a stdout write failure (dispose the stdout reader, then send `tray/simulateClick`) → the process exits
- [ ] T064 [P] [US3] Write tests/Dnui.Tests/Tray/TrayCommandPrepareTests.cs: `Prepare` with a non-redirected `DnuiStdio` returns 20, and with an already-claimed one returns 22. Neither writes to stdout

### Implementation for User Story 3

- [ ] T065 [US3] In Dnui/Tray/TrayCommand.cs, also handle `rpc.Disconnected` (covers write failures), make shutdown idempotent with the completion path, and dispose the tray host and `JsonRpc` inside the lifetime's `Exit` handler so the icon is removed before the process ends
- [ ] T066 [P] [US3] Create Dnui.Runtime/Platform/LinuxTrayHostProbe.cs using `Tmds.DBus.Protocol` (already transitive through Avalonia.Desktop): connect to the session bus and call `org.freedesktop.DBus.NameHasOwner("org.kde.StatusNotifierWatcher")`. No bus, or false → unavailable. Skipped when `tray --headless`
- [ ] T067 [US3] Call `LinuxTrayHostProbe` from Dnui.Runtime/Program.cs for the `tray` command on Linux after `Prepare`, returning 21 with no stdout output when no tray host exists (depends on T066)

**Checkpoint**: US1–US3 pass, and no orphaned tray icons remain.

---

## Phase 7: User Story 5 - Drive the tray from .NET with the Dnui client (Priority: P2)

**Goal**: `DnuiTray.StartAsync` (with automatic runtime resolution), events, updates,
`Completion` → `DnuiResult`, and dispose, per [contracts/tray-client.md](contracts/tray-client.md).

**Independent Test**: start through the client against the built runtime (RuntimePath or local
feed), update items, simulate a click, receive the event, dispose → `DnuiResult(0, null)`.

### Tests for User Story 5

- [ ] T068 [P] [US5] Write tests/Dnui.Runtime.ContractTests/TrayClientTests.cs, each using `DnuiTray` with `Runtime = new() { RuntimePath = <built runtime> }` and `Headless = true`, through public API only:
  - `StartAsync` completes after initialize, and `RuntimeVersion` matches
  - `SimulateOpenAsync()` then `SimulateClickAsync("open")` raise `Opened`, then `Clicked("open")`, each before its call completes
  - `GetStateAsync()` returns a `DnuiTrayState` matching the last `SetItemsAsync`
  - `SimulateClickAsync` on a disabled item throws `DnuiTrayProtocolException` with `Code = -32012`
  - a duplicate-id `SetItemsAsync` throws `DnuiTrayProtocolException` with `Code = -32010`, and the tray keeps running
  - `DisposeAsync` → `Completion` returns `DnuiResult(0, null)` within 2 s
  - killing the process → `Completion` returns a non-zero code without throwing, and later `SetItemsAsync` throws `DnuiTrayNotRunningException`
  - cancelling during `StartAsync` → `OperationCanceledException`, with no process left
  - a handler that throws raises `UnhandledHandlerException`, and the connection stays up
- [ ] T069 [P] [US5] Write tests/Dnui.Runtime.ContractTests/TrayClientAcquisitionTests.cs: `DnuiTray.StartAsync` with `Runtime = new() { CmdlessRoot = temp, PackageSource = artifacts/packages }` and no `RuntimePath` downloads, installs, and starts (skip if the package is missing). Ten concurrent `StartAsync` calls on an empty root install once, and all start (SC-012)

### Implementation for User Story 5

- [ ] T070 [P] [US5] Create Dnui/Tray/Client/DnuiTrayOptions.cs per [contracts/tray-client.md](contracts/tray-client.md): `DnuiTrayOptions` (`Runtime`, `AppAssembly`, `AppType`, `Icon`, `Tooltip`, `Items`, `ShutdownTimeout` = 5 s, `Headless`), `DnuiTrayState` (record `Visible`, `HasIcon`, `Tooltip`, `Items`), `DnuiTrayItem` (record with `Enabled = true`, `Checked`, `Items`, and a static `Separator`), and `DnuiTrayIcon` (`FromFile`, `FromPng`)
- [ ] T071 [P] [US5] Create Dnui/Tray/Client/DnuiTrayExceptions.cs: `DnuiTrayStartException(DnuiResult Result)`, `DnuiTrayProtocolException(int Code, JsonElement? Data)`, and `DnuiTrayNotRunningException : InvalidOperationException`
- [ ] T072 [US5] Create Dnui/Tray/Client/DnuiTray.cs:
  - `StartAsync`:
    1. Resolve the runtime with `DnuiRuntime.ResolveAsync(options.Runtime)`.
    2. Start a `Process` (`UseShellExecute=false`, stdin and stdout redirected, **stderr not redirected**) with args `[--app-assembly A --app-type T] tray [--headless]`.
    3. Create `JsonRpc` with `NewLineDelimitedMessageHandler` over the process streams, adding a private notification target **before** `StartListening`. It forwards to a `Channel` drained by one loop that raises `Opened` and `Clicked`, catching handler exceptions into `UnhandledHandlerException`.
    4. Map the options to `initialize` and race it against process exit. A lost connection → await exit → `DnuiTrayStartException(new DnuiResult(code, null))`. Cancellation → `Kill(entireProcessTree: true)` → rethrow.
  - `SetItemsAsync` and `SetIconAsync`: map `RemoteInvocationException` to `DnuiTrayProtocolException`, and after exit to `DnuiTrayNotRunningException`.
  - `Completion = WaitForExitAsync` → `DnuiResult(ExitCode, null)`.
  - `DisposeAsync` is idempotent: dispose the `JsonRpc` (closing stdin), await exit up to `ShutdownTimeout`, then kill.
  - `GetStateAsync`, `SimulateOpenAsync`, and `SimulateClickAsync`: thin wrappers over `tray/getState`, `tray/simulateOpen`, and `tray/simulateClick`, with the same exception mapping. `SimulateOpenAsync` and `SimulateClickAsync` complete only after the matching event handlers have been invoked (the response arrives after the notification).

  (depends on T070, T071)

**Checkpoint**: the client works end to end, with the runtime found automatically from the local feed.

---

## Phase 8: User Story 4 - Use a custom app type with the runtime (Priority: P3)

**Goal**: `--app-assembly`/`--app-type` load consumer apps, checked as `DnuiApp` before
instantiation, with distinct exit codes. Takeover apps resolve or run their own session.

**Independent Test**: the test apps produce the expected behavior and exit codes for the custom
command app, takeover, takeover session, a non-`DnuiApp` type, a missing type or assembly, and
an eager session claim.

### Tests for User Story 4

- [ ] T073 [P] [US4] Create the test apps in tests/Dnui.TestApps/:
  - `CustomCommandApp.cs`: a `DnuiCommandApp` whose `Configure` calls `base.Configure` and writes `dnui-test: custom-configure` to stderr
  - `TakeoverApp.cs`: a `DnuiApp` whose `OnFrameworkInitializationCompleted` calls `Resolve(new JsonObject { ["takeover"] = true })`
  - `EchoSessionApp.cs`: a `DnuiApp` that opens `Stdio.OpenSession()`, echoes each input line to `session.Output`, and on end-of-file calls `Resolve(null, 0)`
  - `NotAnApp.cs`: a plain class
  - `EagerSessionCommandApp.cs`: a `DnuiCommandApp` whose constructor calls `Stdio.OpenSession()`
  - `SpammerCommandApp.cs`: a `DnuiCommandApp` that starts a timer in its constructor calling `Console.WriteLine("noise")` every 1 ms
- [ ] T074 [P] [US4] Write tests/Dnui.Runtime.ContractTests/AppResolutionContractTests.cs:
  - the default `App` with `tray` works
  - `CustomCommandApp tray` → works, and stderr contains the marker
  - `TakeoverApp` with no command → stdout is exactly `{"takeover":true}`, exit 0
  - `EchoSessionApp` → echoes lines, and closing stdin exits 0 with nothing extra
  - a missing assembly → 10 plus an error line `AppAssemblyLoadFailed`
  - a missing type → 11 `AppTypeNotFound`
  - `NotAnApp` → 12 `AppTypeInvalid`, and no instance was created (the type's static ctor writes a marker that must be absent)
  - `TakeoverApp tray` → 2 `Usage`
  - the same failures with `tray` → the same exit codes with **empty** stdout
  - `EagerSessionCommandApp tray` → 22, empty stdout
- [ ] T075 [P] [US4] Write tests/Dnui.Runtime.ContractTests/StrayOutputContractTests.cs: `SpammerCommandApp tray`, then send 10,000 `tray/setItems` requests, and every stdout line parses as JSON-RPC with a matching response (SC-005)
- [ ] T076 [P] [US4] Write tests/Dnui.Runtime.ContractTests/VersionMismatchContractTests.cs: build or point at a test assembly that references a different `Dnui` assembly version (for example, a copy of Dnui.TestApps compiled with `-p:Version=9.9.9` for its Dnui reference) → exit 13 `DnuiVersionMismatch`

### Implementation for User Story 4

- [ ] T077 [US4] Create Dnui.Runtime/Hosting/AppTypeResolver.cs:
  - `Resolve(string? assemblyPath, string? typeName)`: both or neither, else `Usage`.
  - Hook `AssemblyLoadContext.Default.Resolving` with an `AssemblyDependencyResolver(assemblyPath)` for app-private dependencies.
  - `LoadFromAssemblyPath(Path.GetFullPath(...))`, failing → 10.
  - `GetType(typeName, throwOnError: false)`, null → 11.
  - `typeof(DnuiApp).IsAssignableFrom(type)`, false → 12.
  - Compare the referenced `Dnui` `AssemblyName.Version` with `typeof(DnuiApp).Assembly.GetName().Version`, mismatch → 13.
  - All checks run before any instance is created (research R3).
- [ ] T078 [US4] Use `AppTypeResolver` in Dnui.Runtime/Program.cs in place of the built-in-only resolution. Failures go through `RuntimeFailure`, using `SessionRequested` from the parse result. Plain `DnuiApp` types skip command handling, and a command given to them is `Usage` (depends on T077)

**Checkpoint**: all user stories pass.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T079 [P] Write README.md at the repo root: what Dnui is, `CmdlessRoot` and `CMDLESS_ROOT`, a minimal `DnuiTray` example, the takeover app model (`Resolve`, `Stdio.OpenSession`), and how to pack the runtime (`PackRuntime`) and use the local feed
- [ ] T080 [P] Add XML doc comments to every public type and member in Dnui/ (`DnuiApp`, `DnuiCommandApp`, `DnuiStdio`, `DnuiSession`, `DnuiStdioException`, `DnuiResult`, `DnuiRuntime*`, `DnuiTray*`), and enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in Dnui/Dnui.csproj with no missing-doc warnings
- [ ] T081 [P] Add tests/Dnui.Runtime.ContractTests/TimingTests.cs: tray visible (initialize response) within 2 s of process start (SC-002), and a warm-cache `ResolveAsync` under 100 ms with no source access (SC-011)
- [ ] T082 Add a public-API guard test, tests/Dnui.Tests/PublicApiTests.cs, that lists all public types in the Dnui assembly and asserts the set equals the contracts' list (constitution II minimal surface)
- [ ] T083 Run [quickstart.md](quickstart.md) §1–§7 on Windows, macOS, and Linux with a real tray. Record whether `tray/opened` fires and whether the disabled placeholder can be selected on each OS in [contracts/tray-protocol.md](contracts/tray-protocol.md), and fix any platform gap (FR-023 requires `tray/opened` everywhere)
- [ ] T084 Final check: `dotnet build Dnui.slnx` with 0 warnings, `dotnet test` green on all three CI runners in Debug **and** with the contract tests pointed at a Release-packed runtime (SC-013), and all six `Cmdless.Dnui.Runtime.<rid>` packages produced by the `pack` job

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: needs Setup. Blocks every story.
- **US1 (Phase 3)**: needs Foundational.
- **US6 (Phase 4)**: needs Foundational, and is independent of US1 (except T049, which needs the runtime to pack and run `--version`). It can run in parallel with US1.
- **US2 (Phase 5)**: needs US1 (it extends `TrayProtocolTarget`).
- **US3 (Phase 6)**: needs US1.
- **US5 (Phase 7)**: needs US1 and US6 (the client resolves the runtime). It works best after US2, since update-error tests use `setItems`.
- **US4 (Phase 8)**: needs Foundational. Its contract tests use `tray`, so US1 too.
- **Polish (Phase 9)**: after the desired stories.

### Within Each Story

Tests first (they must fail), then DTOs and helpers, then hosts, then protocol or service, then
wiring into the runtime.

### Parallel Opportunities

- Setup: T002–T007 together after T001.
- Foundational tests T008–T012 together. T013, T014, T015, and T021 together.
- US1: T027–T032 together. T033–T037 together, then T038 and T039 in parallel.
- US6: T044–T049 together. T050–T052 together. T056 in parallel with T053–T055.
- After Foundational: US1 and US6 in parallel. After US1: US2, US3, and US4 in parallel
  (different files, except that US3's T067 and US4's T078 both touch Program.cs, so do those
  one after the other).

---

## Parallel Example: User Story 1

```bash
# Tests together:
Task: "TrayMenuValidatorTests in tests/Dnui.Tests/Tray/TrayMenuValidatorTests.cs"
Task: "TrayIconDecoderTests in tests/Dnui.Tests/Tray/TrayIconDecoderTests.cs"
Task: "TrayInitializeContractTests in tests/Dnui.Runtime.ContractTests/TrayInitializeContractTests.cs"

# Building blocks together:
Task: "TrayProtocolMessages in Dnui/Tray/TrayProtocolMessages.cs"
Task: "TrayErrorCodes in Dnui/Tray/TrayErrorCodes.cs"
Task: "TrayMenuValidator in Dnui/Tray/TrayMenuValidator.cs"
Task: "TrayIconDecoder in Dnui/Tray/TrayIconDecoder.cs"
Task: "ITrayHost in Dnui/Tray/ITrayHost.cs"
```

## Parallel Example: User Story 6

```bash
Task: "RootsTests in tests/Dnui.Tests/Runtime/RootsTests.cs"
Task: "PackageCacheTests in tests/Dnui.Tests/Runtime/PackageCacheTests.cs"
Task: "HttpFeedTests in tests/Dnui.Tests/Runtime/HttpFeedTests.cs"
Task: "PackRuntime target in Dnui.Runtime/Packaging/PackRuntime.targets"
```

---

## Implementation Strategy

### MVP

1. Phase 1 Setup, then Phase 2 Foundational.
2. Phase 3 (US1): a working tray over raw stdio. **Stop and validate** with quickstart §2.
3. Phase 4 (US6): automatic runtime download. This is the platform-critical piece.

### Incremental Delivery

US1 → US6 → US2 (live updates) → US3 (lifecycle and failures) → US5 (the .NET client, which
by then can find the runtime automatically) → US4 (custom apps) → Polish. Each phase ends at a
checkpoint where its tests pass on their own.

---

## Notes

- `[P]` means different files and no dependency on unfinished tasks.
- There are no build-specific test hooks. Tests use the public `tray --headless`,
  `tray/getState`, and `tray/simulate*` (FR-047–FR-050), which behave the same in Release
  packages.
- The library must never reference Avalonia.Desktop or Avalonia.Native. Platform options
  (macOS `ShowInDock`) are applied by the runtime from the `DnuiCommand.HideDockIcon` hint
  (T018, T025).
- Commit after each task or logical group.
