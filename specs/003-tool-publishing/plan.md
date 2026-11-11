# Implementation Plan: Dnui Tool, API Cleanup and nuget.org Publishing

**Branch**: `003-tool-publishing` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-tool-publishing/spec.md`

## Summary

This feature has three parts.

**Tool**: a new `Dnui.Tool` project, packed as `Cmdless.Dnui.Tool` with the command `dnui`. Its
whole program is one call to the library's internal launcher in passthrough mode: resolve the
lockstep runtime, start it with inherited stdio, and return its exit code. Resolution failures
become exit 40 `RuntimeUnavailable` (R1, R2).

**API cleanup before the first publish**:
- One internal `RuntimeLauncher` replaces the two separate launch paths. It brings Windows job
  objects and Unix signal forwarding to every runtime the library starts (R3).
- `DnuiTrayOptions.Configure` lets handlers be attached before the tray starts.
- `DnuiTray.StartAsync` and `DnuiHost.RunAsync` become internal.
- `Runtime` and `Headless` live only on `DnuiHostOptions`.
- Windows are chosen through an overload chain that ends in path strings.
- The app is chosen through `WithApp` on the host (D1).
- `DnuiTrayStartException` is merged into `DnuiCommandException`.
- `DnuiRuntimeOptions` becomes a record.
- A new `DNUI_PACKAGE_SOURCE` environment override (R5, R12).

**Publishing**:
- Shared package metadata, symbols and SourceLink.
- `release.yml` grows into three jobs: pack, publish and github-release. Publishing uses nuget.org
  trusted publishing behind an approval environment, pushes the runtime packages first, waits for
  them to be downloadable, and skips duplicates on re-runs (R6–R10).

**Governance**: constitution 2.4.0 adds the tool and the publishing rules (R11).

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`), nullable enabled.

**Primary Dependencies**: no new library dependencies. The tool references `Dnui` only. CI adds
the `NuGet/login@v1` GitHub Action. SourceLink comes with the SDK.

**Storage**: no change. The `CmdlessRoot` cache is shared by the tool and the library.

**Testing**: xUnit v3 on Microsoft Testing Platform. A new `tests/Dnui.ToolTests` project packs
the runtime and tool into a temp feed and installs the tool from it. Contract and unit tests
follow the API changes. The public-API guard test is updated.

**Target Platform**: Windows, macOS and Linux, x64 and arm64. The tool is platform-independent.

**Project Type**: library (`Dnui`) + CLI host (`Dnui.Runtime`) + .NET tool (`Dnui.Tool`) + CI
workflow.

**Performance Goals**: with a warm cache, the tool adds ≤ 0.5 s over running the runtime directly
(SC-003). A release completes within 30 min of approval (SC-004).

**Constraints**:
- The tool has no own commands or output.
- No public run-any-command function.
- No long-lived publishing secret.
- Each package ≤ 250 MB (currently 49–78 MB per runtime package).

**Scale/Scope**: 1 new project, 1 new test project, about 10 public API changes, 1 workflow
rewrite, and 8 published packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Status | Evidence |
|---|---|---|
| I. Runtime is the CLI host; small command set | PASS | No new runtime commands. The tool forwards arguments unchanged |
| I. Output contract: runtime owns stdout | PASS | Inherited stdio means byte-identical output. The tool writes stdout only when no runtime ran (exit 40), in the standard error shape (R2) |
| I. Consumers never reference Dnui.Runtime | PASS | The tool references `Dnui` only |
| II. Library references no platform backends | PASS | The job object and signal code are P/Invoke and BCL inside the launcher, like the existing tray foreground call |
| II. Minimal public API | PASS | The net public surface shrinks: `RunAsync`, the public tray start and `DnuiTrayStartException` go; `WithApp` is added (R12, D1) |
| III. Exact-version acquisition, shared cache, overrides | PASS | The tool uses the library's resolver. `DNUI_PACKAGE_SOURCE` follows the `CMDLESS_ROOT` precedence rule |
| IV. Parity; gaps documented | PASS | The Unix `SIGKILL` orphan gap is documented in [tool-cli.md](contracts/tool-cli.md) |
| V. Lockstep versioning | PASS | The tool builds at `DnuiRuntimeVersion` and carries the matching `Dnui` |
| Packaging: `Cmdless.*` ids; runtime only per RID | PASS **after amendment** | The naming rule lists only `Cmdless.Dnui` and the runtime packages. R11 amends it to 2.4.0 as the first task |
| Workflow: contract tests, three OSes | PASS | Tool tests run in the existing matrix |
| Public contract changes documented | PASS | [contracts/](contracts/), and 001 and 002 contracts get pointers |

**Result**: PASS, with the constitution amendment as the first task. No complexity-tracking
entries.

## Project Structure

### Documentation (this feature)

```text
specs/003-tool-publishing/
├── plan.md
├── research.md            # R1–R12, D1
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── tool-cli.md        # dnui command, exit 40, interrupts, env vars
│   ├── library-api.md     # API after cleanup
│   └── release.md         # release.yml jobs, permissions, manual setup
├── checklists/requirements.md
└── tasks.md               # /speckit-tasks
```

### Source Code (repository root)

```text
Directory.Build.props              # + shared package metadata, CI determinism, icon/readme
build/package/
├── icon.png                       # placeholder until final art
└── readme/{Dnui,Tool,Runtime}.md  # per-package readmes
build/RuntimePackage/              # nuspec: + readme, icon, urls via NuspecProperties

Dnui/
├── Hosting/
│   ├── RuntimeLauncher.cs         # NEW internal: Capture / Protocol / Passthrough
│   ├── ChildProcessLifetime.cs    # NEW internal: Windows job object, Unix signal forwarding
│   ├── DnuiHost.cs                # RunAsync internal; WithApp chain (internal app + shared outcomes); window chain to strings
├── Dialogs/IDnuiDialogs.cs        # interface changes (host + window commands)
├── Windows/DnuiWindowCommands.cs  # string-path overload; drop new()
├── Tray/Client/
│   ├── DnuiTray.cs                # StartAsync internal; Configure; not-started guard; launcher
│   ├── DnuiTrayOptions.cs         # - Runtime/Headless/AppAssembly/AppType; + Configure
│   └── DnuiTrayExceptions.cs      # - DnuiTrayStartException
├── Runtime/DnuiRuntimeOptions.cs  # record; DNUI_PACKAGE_SOURCE
├── Internal/ErrorResults.cs       # + 40 RuntimeUnavailable
├── Properties/AssemblyInfo.cs     # + InternalsVisibleTo("Dnui.Tool")
└── Dnui.csproj                    # symbols, readme

Dnui.Tool/                         # NEW
├── Dnui.Tool.csproj               # PackAsTool, ToolCommandName=dnui, RollForward=Major
└── Program.cs                     # return await RuntimeLauncher.PassthroughAsync(args)

tests/
├── Dnui.ToolTests/                # NEW: pack + install tool from temp feed, compare with runtime
├── Dnui.Tests/                    # + PublicApiTests update, prerelease paths, env precedence
├── Dnui.Runtime.ContractTests/    # tray/host tests moved to new API; Configure, headless host
└── Dnui.SampleApp/                # uses the new API

.github/workflows/
├── ci.yml                         # + tool tests in the matrix
└── release.yml                    # pack → publish (OIDC, env nuget) → github-release

.specify/memory/constitution.md    # 2.4.0 (R11)
README.md                          # + tool install, DNUI_PACKAGE_SOURCE
```

**Structure Decision**: add one project, `Dnui.Tool`, beside `Dnui` and `Dnui.Runtime`. All
launching moves into the library's internal `RuntimeLauncher`, so the tool, `DnuiHost` and
`DnuiTray` share one path. The tool reaches it through `InternalsVisibleTo`, which keeps the
public API free of a run-any-command function.

## Complexity Tracking

No violations.

## Post-Design Constitution Re-check

After [data-model.md](data-model.md), [contracts/](contracts/) and [quickstart.md](quickstart.md):
- The public API in [library-api.md](contracts/library-api.md) is smaller than before and has one
  way to do each thing (II).
- The new exit code, environment variable and tool behavior are documented (V).
- The single remaining platform gap is documented (IV).
- The packaging rule needs the 2.4.0 amendment, which is scheduled as the first task.

**Result**: PASS.

## Decisions confirmed

- **D1** ([research.md](research.md#d1-how-dialogs-and-everything-else-pick-an-app)): the app is
  chosen with `WithApp` on the host (confirmed 2026-11-11). A bound host is a `DnuiHost` instance
  carrying an internal app selection, so no extra public or internal host type is needed.
