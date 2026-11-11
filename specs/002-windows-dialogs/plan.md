# Implementation Plan: Windows and Built-in Dialogs

**Branch**: `002-windows-dialogs` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-windows-dialogs/spec.md`

## Summary

This feature adds the `window` and `dialog` host commands and the window-command surface from
constitution 2.3.0.

**Host commands** (one runtime process each):
- `window <assembly> <type>` shows a consumer `DnuiWindow` and resolves `{"result": …}`.
- `dialog open-file|open-folder|save-file` shows Avalonia's storage pickers, with title, filter,
  start-folder, multiple, suggested-name, default-extension, and overwrite options. It resolves
  `{"paths": …}` or `{"path": …}`, and cancelling resolves `null`.

**Window commands** (`DnuiWindow.Ui`) run in-process: child windows owned by the window, and
dialogs parented to it.

**Library**:
- `DnuiHost` (`IDnuiHostCommands`) wraps every host command as typed, generic-friendly functions:
  `OpenWindowAsync<TWindow>()` and `OpenWindowAsync<TWindow, TApp>()`. It runs on top of a public
  one-shot `RunAsync`.
- Assemblies are found through `Location`, then `{name}.dll` beside the executable, then
  `dnui/{name}.dll`.
- A `buildTransitive` target ships the project's IL into `dnui/` for single-file and AOT
  publishes, so one project works in every publish mode.

**Headless** mode uses Avalonia's headless platform, with caller-supplied dialog outcomes.

Decisions are in [research.md](research.md).

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`), nullable enabled

**Primary Dependencies**:
- Existing: Avalonia 12.1.5, System.CommandLine 2.0.14 (runtime), and StreamJsonRpc 2.26.x
  (library; tray only).
- New: `Avalonia.Headless` 12.1.5, in the runtime only, for headless windows (R10).
- No new library dependency.

**Storage**: none new. Uses spec 001's cache layout. The publish output gains a `dnui/` folder
(R4).

**Testing**: xUnit v3 on Microsoft Testing Platform. The suites are unit, contract (headless
runtime processes), and publish (single-file on all runners, AOT on Linux).

**Target Platform**: Windows, macOS, and Linux desktops, x64 and arm64.

**Project Type**: library (Dnui) plus a CLI host (Dnui.Runtime). This adds a packaged MSBuild
`buildTransitive` target.

**Performance Goals**: no new hard targets. A dialog or window process should be visible within
spec 001's 2 s warm-start budget.

**Constraints**:
- One app per process.
- Window commands expose no app and no tray (enforced by types).
- Every host command resolves exactly one line.
- Publish shipping must exclude runtime-provided assemblies.

**Scale/Scope**: three dialog kinds, one window command, and three public interfaces with about
12 public types.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Status | Evidence |
|---|---|---|
| I. Host commands start a process, may override the app, callable anywhere | PASS | `window`/`dialog` are separate runtime processes. `DnuiHost` takes app generics. It's usable from window code (R1, R2) |
| I. Window commands are in-process, inherit the app, have no tray; parenting is in-process only | PASS | `IDnuiWindowCommands` has no app or tray members. Dialogs parent to their own `DnuiWindow` (R1, R7) |
| I. Output contract: one-shot commands resolve one JSON line; errors resolved with codes; cancellation is a result | PASS | `{"result"}`, `{"paths"}`, `{"path"}`; codes 14–17 and 30; null with exit 0 (R6, R8, R11) |
| I. Small command set | PASS | Only `window` and `dialog` (three subcommands) are added, both named in the constitution |
| II. Library references no platform backends | PASS | `Avalonia.Headless` and the macOS and Windows foreground code live in the runtime (R9, R10) |
| II. Minimal public API, each type with a consumer reason | PASS | `DnuiWindow`, three interfaces, `DnuiHost`/`DnuiHostOptions`, three option records plus `DnuiFileFilter`, and two exceptions ([contracts/library-api.md](contracts/library-api.md)) |
| II. New library dependencies justified | PASS | None added |
| III. Runtime acquisition unchanged; root layout | PASS | `DnuiHost` uses `DnuiRuntime.ResolveAsync`. `dnui/` is in the *consumer's* publish output, not `CmdlessRoot` |
| IV. Parity; gaps documented; platform code in adapters | PASS | macOS sheets/owner sizing, Windows foreground rights, and Linux focus are platform adapters plus documented gaps (R9). "Unsupported" pickers use exit 30 |
| V. Lockstep: contract documented and versioned | PASS | [contracts/cli.md](contracts/cli.md) and [contracts/library-api.md](contracts/library-api.md). The `dnui/` folder excludes Dnui and Avalonia so the runtime's lockstep copies win |
| Workflow: contract tests per command; test matrix | PASS | Headless contract tests for every subcommand and failure code; publish tests (R12) |
| Workflow: build and test on 3 OSes | PASS | Existing CI, plus a publish-test step. AOT publish on Linux only (native toolchain), documented |

**Result**: PASS. No complexity-tracking entries.

## Project Structure

### Documentation (this feature)

```text
specs/002-windows-dialogs/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli.md            # window/dialog commands, options, results, exit codes
│   └── library-api.md    # DnuiWindow, IDnuiDialogs/IDnuiWindowCommands/IDnuiHostCommands, DnuiHost
├── checklists/requirements.md
└── tasks.md              # /speckit-tasks
```

### Source Code (repository root)

```text
Dnui/
├── Windows/
│   ├── DnuiWindow.cs               # public: Resolve, Ui, internal Completion
│   └── DnuiWindowCommands.cs       # internal: IDnuiWindowCommands, in-process, dialog queue
├── Dialogs/
│   ├── IDnuiDialogs.cs             # public: IDnuiDialogs, IDnuiWindowCommands, IDnuiHostCommands
│   ├── DnuiDialogOptions.cs        # public: open-file/open-folder/save-file options, DnuiFileFilter
│   ├── DialogRunner.cs             # internal: options → Avalonia pickers → results; unsupported
│   ├── DialogOutcome.cs            # internal: outcome JSON shape validation (headless)
│   └── DialogCommand.cs            # internal: DnuiCommand for `dialog …` (invisible owner)
├── Windows/WindowCommand.cs        # internal: DnuiCommand for `window …`
├── Hosting/
│   ├── DnuiHost.cs                 # public: IDnuiHostCommands, RunAsync, typed wrappers
│   ├── DnuiHostOptions.cs          # public: Runtime, Headless, Outcomes
│   ├── DnuiCommandException.cs     # public: non-zero exit with error code/message
│   ├── AssemblyLocator.cs          # internal: Location → {name}.dll → dnui/{name}.dll
│   └── DnuiTypeLoader.cs           # internal: shared app/window loader (moved from runtime)
├── build/Cmdless.Dnui.targets      # packed to buildTransitive/: dnui/ IL shipping
└── Dnui.csproj                     # + pack build/*.targets

Dnui.Runtime/
├── Cli/RuntimeCommandLine.cs       # + window, dialog subcommands, --headless, --outcome, --dialog-outcome
├── Hosting/AppTypeResolver.cs      # → thin wrapper over DnuiTypeLoader
├── Hosting/ExitCodes.cs            # + 14–17, 30
├── Platform/HeadlessPlatform.cs    # UseHeadless when --headless (window/dialog)
├── Platform/MacForeground.cs       # activateIgnoringOtherApps via objc runtime
├── Platform/WindowsForeground.cs   # AllowSetForegroundWindow / Activate helpers
└── Dnui.Runtime.csproj             # + Avalonia.Headless

Dnui/Tray/AvaloniaTrayHost.cs       # + AllowSetForegroundWindow(ASFW_ANY) on Windows item clicks (R9)

tests/
├── Dnui.Tests/                     # + dialog mapping, outcomes, locator, loader, ship-filter tests
├── Dnui.TestApps/                  # + test windows: resolving, closing, throwing, child-opening, dialog-using
├── Dnui.Runtime.ContractTests/     # + DialogCommandTests, WindowCommandTests, WindowCommandsTests, DnuiHostTests
├── Dnui.SampleApp/                 # single-project app with its own DnuiWindow + DnuiCommandApp (publish tests)
└── Dnui.PublishTests/              # publishes SampleApp single-file (and AOT on Linux), runs it headless
```

**Structure Decision**: keep the two projects. New library code is grouped by surface (`Windows/`,
`Dialogs/`, `Hosting/`). The type loader moves into the library because window commands load
types in-process (R5). Platform foreground code stays in the runtime, except the one Windows call
the tray host needs at click time. That call is a P/Invoke in the tray host's Windows branch, not a
platform package reference.

## Complexity Tracking

No violations.

## Post-Design Constitution Re-check

After [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md):
- The public API in [contracts/library-api.md](contracts/library-api.md) matches the constitution
  II list. The window surface has no app or tray members (I).
- Every new exit code and result shape is documented (V).
- Platform gaps (macOS Dock name for unbundled executables, Linux focus stealing, unsupported
  pickers) are documented (IV).

**Result**: PASS.
