# Implementation Plan: Tray Command and Protocol

**Branch**: `001-tray-command` | **Date**: 2026-11-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-tray-command/spec.md`

## Summary

Add the app model to the Dnui library: `DnuiApp`, with overridable builder configuration, and
`DnuiCommandApp`, which runs a built-in command chosen by the runtime. Turn Dnui.Runtime into a
System.CommandLine host that resolves the app type (built-in or `--app-assembly`/`--app-type`),
checks that it is a `DnuiApp` before creating it, hands command apps their parsed command, and
starts Avalonia.

The first built-in command is `tray`. During the synchronous startup phase it captures stdio,
redirects stray stdout writes to stderr, and prepares a newline-delimited JSON-RPC 2.0
connection. After framework initialization it starts listening and drives a single `TrayIcon`
through `initialize`, `tray/setItems`, and `tray/setIcon`. It reports `tray/opened` (menu opening)
and `tray/clicked` (item selection), and shows a disabled placeholder when the menu is empty,
so the behavior is the same on every platform. It shuts down when the input stream ends. On the caller side, the library gains the `DnuiTray`
client (start, events, awaitable updates, a `DnuiResult` completion, and dispose). It finds
the runtime automatically through `DnuiRuntime.ResolveAsync`. That downloads
`Cmdless.Dnui.Runtime.<rid>` at exactly `DnuiRuntimeVersion` into
`{CmdlessRoot}/dotnet/packages/{id}/{version}/` the first time, with a cross-process lock and
an atomic, verified extraction. A `PackRuntime` build target produces the six per-RID
packages. It's also the harness the contract tests use.

Every app gets the same output path: the runtime captures stdout before the app exists and
sends stray console output to stderr. `DnuiApp.Resolve(result, exitCode)` is the only way to
write the outcome and exit. Any app can instead claim stdin and stdout once as a session
through the public `DnuiApp.Stdio.OpenSession()`, which is exactly what the tray command does. Tray resolves with `null`, so stdout only ever carries its
protocol. Decisions are in
[research.md](research.md).

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`), nullable enabled

**Primary Dependencies**:
- Avalonia 12.1.5 (`AvaloniaVersion`). The library uses the base, Fluent theme, Inter font, and
  Debug diagnostics packages. The runtime uses Avalonia.Desktop.
- System.CommandLine 2.0.14 (runtime only).
- StreamJsonRpc 2.26.x (library; justified in research R5).
- No new dependency for runtime download: BCL `HttpClient`, `ZipArchive`, and
  `File.SetUnixFileMode` (research R13).

**Storage**: per-user cache under `CmdlessRoot` (`~/.cmdless`, or `CMDLESS_ROOT`):
`{DnuiRoot}/packages/{id}/{version}/` and `{DnuiRoot}/locks/`
([contracts/runtime-acquisition.md](contracts/runtime-acquisition.md))

**Testing**: xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`):
- unit tests with a fake tray host over in-memory pipes
- process-level contract tests against the built runtime in `tray --headless` mode, using the
  public `tray/getState` and `tray/simulate*` methods

**Target Platform**: Windows, macOS, and Linux desktops (x64 and arm64). Linux needs a
StatusNotifierItem tray host.

**Project Type**: library (Dnui) plus a desktop CLI host executable (Dnui.Runtime)

**Performance Goals**:
- tray visible within 2 s of process start (SC-002)
- shutdown within 2 s of the caller exiting (SC-004)
- 10,000 messages with stray-output noise and zero protocol corruption (SC-005)

**Constraints**:
- Protocol responses are sent only after the UI change is applied.
- Nothing except protocol messages may reach stdout in tray mode.
- `Main` stays synchronous until Avalonia starts.
- Public API is limited to `DnuiApp` and `DnuiCommandApp`.

**Scale/Scope**: one tray icon per runtime process, one caller per runtime, menus up to a few
hundred items

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Status | Evidence |
|---|---|---|
| I. Runtime is a cached CLI host: System.CommandLine | PASS | R4; the root command and `tray` subcommand use System.CommandLine |
| I. Output contract (runtime owns stdout, one `Resolve` line for one-shot, protocol only for session, errors resolved with non-zero code, stderr outside the contract) | PASS | R6; output contract in [contracts/cli.md](contracts/cli.md); `Resolve` in [contracts/app-model.md](contracts/app-model.md) |
| I. `--app-assembly`/`--app-type`, `DnuiApp` check before creating an instance | PASS | R3; the type check runs before `Activator.CreateInstance` |
| I. `Configure` for every app, command only for `DnuiCommandApp` | PASS | R2; `ApplyConfiguration` runs for all apps |
| I. Small command set | PASS | Only `tray` in this feature |
| I. Consumers never reference Dnui.Runtime | PASS | Custom apps reference only Dnui; the runtime loads them |
| II. Library references no platform backends | PASS | Dnui has no Avalonia.Desktop; the Linux DBus probe lives in the runtime (R9) |
| II. `DnuiApp` / `DnuiCommandApp` extension points, `Resolve`, `Stdio`, minimal public API | PASS | Public types: `DnuiApp`, `DnuiCommandApp`, `DnuiStdio`, `DnuiSession`, `DnuiStdioException`/`DnuiStdioFailure`, plus the tray client set (`DnuiTray`, `DnuiTrayOptions`, `DnuiTrayItem`, `DnuiTrayIcon`, `DnuiResult`, and three exceptions). Each is a type consumers use directly, and the tray client exists so consumers don't have to speak raw JSON-RPC (R12). The stdio types exist so takeover apps can run their own protocol, and the tray dogfoods them. Everything else is internal (R2, R6) |
| II. New library dependency justified | PASS | StreamJsonRpc justified in R5 |
| III. Exact-version runtime package for the current RID, central cache, safe under concurrency, verified, root overridable, layout per 2.2.1 | PASS | R13 and [contracts/runtime-acquisition.md](contracts/runtime-acquisition.md): exact `DnuiRuntimeVersion`, lock plus atomic rename, CRC and manifest check, `CMDLESS_ROOT` and code override, `packages/{id}/{version}` |
| IV. Cross-platform parity, documented gaps, platform adapters | PASS | macOS Dock option and Linux probe are isolated adapters; `tray/opened` uses menu opening, so it's the same on every platform (R8, R9) |
| V. Contract documented and versioned | PASS | [contracts/](contracts/) covers the CLI, protocol, and app model; protocol version 1 |
| V. Loaded app's Dnui version checked (SHOULD) | PASS | Exit code 13 (R3) |
| Workflow: CLI contract tests per command | PASS | `tests/Dnui.Runtime.ContractTests` (R10) |
| Workflow: app resolution test matrix | PASS | Default `App`, custom `DnuiCommandApp`, custom `DnuiApp`, missing assembly or type, non-`DnuiApp` (R10) |
| Workflow: build and test on Windows, macOS, and Linux | PASS | CI matrix workflow added by this feature (Linux under `xvfb-run`). CI also packs the six runtime packages into `artifacts/packages/`, and contract tests resolve the runtime from that folder feed |

**Result**: PASS, with no deviations.

## Project Structure

### Documentation (this feature)

```text
specs/001-tray-command/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 entities and validation
├── quickstart.md        # Phase 1 validation guide
├── contracts/
│   ├── cli.md           # Runtime CLI options, commands, exit codes
│   ├── tray-protocol.md # JSON-RPC protocol v1
│   ├── app-model.md     # DnuiApp / DnuiCommandApp / DnuiStdio public API
│   └── tray-client.md   # DnuiTray client public API
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
Directory.Build.props              # DnuiRuntimeVersion, AvaloniaVersion
Dnui.slnx                          # + test projects
.github/workflows/ci.yml           # build + test matrix: windows, macos, ubuntu (xvfb)

Dnui/                              # library, package Cmdless.Dnui
├── DnuiApp.cs                     # public: Application + protected virtual Configure
├── DnuiCommandApp.cs              # public: sealed OnFrameworkInitializationCompleted
├── DnuiStdio.cs                   # public: captured stdio, OpenSession (once), resolve writer
├── DnuiSession.cs                 # public: raw Input/Output streams (non-closing Output)
├── DnuiResult.cs                  # public: record (ExitCode, StdOut), shared by all commands
├── Commands/
│   └── DnuiCommand.cs             # internal: ConfigureBuilder / Run
├── Tray/
│   ├── TrayCommand.cs             # stdio capture, JsonRpc setup, run/shutdown
│   ├── TrayProtocolTarget.cs      # initialize, tray/setItems, tray/setIcon handlers
│   ├── TrayProtocolMessages.cs    # request/response/notification DTOs
│   ├── TrayErrorCodes.cs
│   ├── TrayMenuValidator.cs       # whole-list validation (ids, separators)
│   ├── TrayIconDecoder.cs         # path / base64 PNG → icon or error
│   ├── ITrayHost.cs               # seam
│   ├── AvaloniaTrayHost.cs        # TrayIcon + NativeMenu implementation
│   ├── HeadlessTrayHost.cs        # tray --headless: same state, no OS icon (always available)
│   └── Client/
│       ├── DnuiTray.cs            # public: process + JsonRpc session, events, updates
│       ├── DnuiTrayOptions.cs     # public: options, DnuiTrayItem, DnuiTrayIcon
│       └── DnuiTrayExceptions.cs  # public: start / protocol / not-running exceptions
└── Properties/AssemblyInfo.cs     # InternalsVisibleTo Dnui.Runtime, Dnui.Tests

Dnui/Runtime/                      # runtime acquisition (public DnuiRuntime API)
├── DnuiRuntime.cs                 # public: Version, CurrentRid, roots, ResolveAsync
├── DnuiRuntimeOptions.cs          # public: CmdlessRoot, PackageSource, RuntimePath, LockTimeout
├── DnuiRuntimeUnavailableException.cs
├── PackageFeed.cs                 # v3 flat container / local folder source
├── PackageCache.cs                # lock, verify, extract, atomic publish, exec bit
└── RidInfo.cs                     # supported RIDs, executable name

Dnui.Runtime/                      # host executable; packed per RID by the PackRuntime target
├── Packaging/PackRuntime.targets  # publish self-contained → nuspec → artifacts/packages
├── Program.cs                     # sync Main: parse → resolve → SetCommand → build → start
├── App.axaml / App.axaml.cs       # built-in App : DnuiCommandApp
├── Cli/RuntimeCommandLine.cs      # System.CommandLine tree → DnuiCommand
├── Hosting/AppTypeResolver.cs     # assembly load, type check, version check
├── Hosting/ExitCodes.cs
└── Platform/LinuxTrayHostProbe.cs # StatusNotifierWatcher check
                                   # (MainWindow.axaml removed; window command is future work)

tests/
├── Dnui.Tests/                    # unit + in-memory protocol tests; acquisition tests with
│                                  #   synthetic .nupkg files in temp roots (concurrency, corrupt
│                                  #   zip, partial cache, CMDLESS_ROOT, offline)
├── Dnui.Runtime.ContractTests/    # spawn runtime process; exit codes, protocol, lifecycle
└── Dnui.TestApps/                 # custom DnuiCommandApp, DnuiApp, non-DnuiApp, spammer
```

**Structure Decision**: keep the existing root-level `Dnui/` and `Dnui.Runtime/` projects and
add a `tests/` folder. Tray execution lives in the library because `DnuiCommandApp` runs it.
Argument parsing, assembly loading, and platform probes live in the runtime (research R1).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | No violations. Test support is the public `--headless` option and the `tray/getState` / `tray/simulate*` protocol methods (FR-047–FR-050), not build-specific hooks. | — |

## Post-Design Constitution Re-check

Re-checked after writing [data-model.md](data-model.md), [contracts/](contracts/), and
[quickstart.md](quickstart.md):
- The public API in [contracts/app-model.md](contracts/app-model.md) and
  [contracts/tray-client.md](contracts/tray-client.md) is limited to the documented types and
  members, each with a consumer-facing reason (II).
- Every exit code and protocol error is documented (V).
- No platform-specific code appears outside `Platform/` or the tray host (IV).

**Result**: PASS.
