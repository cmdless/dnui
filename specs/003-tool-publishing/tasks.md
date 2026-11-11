---

description: "Task list for the dnui tool, API cleanup and nuget.org publishing"
---

# Tasks: Dnui Tool, API Cleanup and nuget.org Publishing

**Input**: Design documents from `/specs/003-tool-publishing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (tool-cli.md,
library-api.md, release.md), quickstart.md

**Tests**: included. The constitution requires contract tests for every command-line surface, and
the spec requires tool tests in CI (FR-018). Write each story's tests first and confirm they fail.

**Organization**: phases follow the spec's stories:
- US1 (P1): the tool
- US2 (P1): the library installable from nuget.org (package metadata, symbols, prerelease)
- US3 (P1): public API cleanup
- US4 (P2): release pipeline

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: user story from spec.md (US1–US4)

## Path Conventions

- `Dnui/` (library), `Dnui.Runtime/` (host executable), `Dnui.Tool/` (new tool), `tests/`
- Package assets: `build/package/`
- Workflows: `.github/workflows/`

---

## Phase 1: Setup

- [X] T001 Amend the constitution to 2.4.0 with `/speckit-constitution` (research R11) in .specify/memory/constitution.md:
  - naming: add `Cmdless.Dnui.Tool` to the published package ids
  - add the tool as a project that forwards only: no commands, options or output of its own
  - lockstep (V): the tool ships at `DnuiRuntimeVersion`
  - III: `DNUI_PACKAGE_SOURCE` overrides the package source, with the same precedence as `CMDLESS_ROOT`
  - workflow: releases publish to nuget.org only through trusted publishing, runtime packages first
- [X] T002 [P] Create Dnui.Tool/Dnui.Tool.csproj and add it to Dnui.slnx (research R4):
  - Exe, net10.0, nullable, `PackAsTool=true`, `ToolCommandName=dnui`, `PackageId=Cmdless.Dnui.Tool`, `RollForward=Major`
  - `ProjectReference` to Dnui/Dnui.csproj
  - Dnui.Tool/Program.cs that returns 0 for now
- [X] T003 [P] Create tests/Dnui.ToolTests/Dnui.ToolTests.csproj and add it to Dnui.slnx:
  - xUnit v3, `<Using Include="Xunit" />`, NoWarn xUnit1051
  - `AssemblyMetadata` `DnuiRepoRoot` and `DnuiRuntimeExe`, as in tests/Dnui.PublishTests/Dnui.PublishTests.csproj
  - `ProjectReference` to Dnui.Runtime and Dnui.Tool with `ReferenceOutputAssembly="false"` (build order only)
- [X] T004 [P] Add package assets:
  - build/package/icon.png: a 128×128 placeholder PNG until the final art
  - build/package/readme/Dnui.md, build/package/readme/Tool.md, build/package/readme/Runtime.md: short readmes. Runtime.md says it isn't meant to be referenced directly and points to Cmdless.Dnui

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: one internal launcher for every runtime start, child lifetime, exit code 40 and the
package-source override (research R1–R3, R5, R12 #10–11). The tool, the API cleanup and the
tests all build on it.

**⚠️ CRITICAL**: no user story can start until this phase is complete

### Tests for Foundation

- [X] T005 [P] Write tests/Dnui.Tests/Hosting/RuntimeLaunchTests.cs for `RuntimeLaunch.Arguments()`:
  - no app → command only
  - app → `--app-assembly P --app-type T` before the command
  - headless → `--headless` after the command
  - outcomes → `--outcome` for dialogs and `--dialog-outcome` for windows, in order
  - arguments with spaces, quotes, empty strings and non-ASCII text are kept exactly
- [X] T006 [P] Write tests/Dnui.Tests/Runtime/PackageSourceOverrideTests.cs:
  - `DnuiRuntimeOptions.PackageSource` beats `DNUI_PACKAGE_SOURCE`, which beats nuget.org
  - a prerelease version (`0.1.0-preview.1`) gives lowercase cache paths and flat-container URLs
- [X] T007 [P] *(Done as: `Passthrough` covered end to end by tests/Dnui.ToolTests (the tool is the launcher process); `Capture` by the host contract tests after US3.)* Write tests/Dnui.Runtime.ContractTests/LauncherContractTests.cs, driving the real runtime with `RuntimePath`:
  - `Capture` returns the resolved line and exit code
  - `Passthrough` inherits stdio. Use a small helper process (tests/Dnui.ToolTests/Support or a test-only console in tests/Dnui.TestApps) that calls the launcher; compare its stdout and exit code with the runtime run directly
  - an unresolvable runtime (`RuntimePath` missing) in `Passthrough` → stdout `{"error":{"code":"RuntimeUnavailable",…}}`, exit 40
- [X] T008 [P] *(Written as tests/Dnui.ToolTests/ChildLifetimeTests.cs, using the tool as the launcher process.)* Write tests/Dnui.Runtime.ContractTests/ChildLifetimeTests.cs:
  - Unix: sending `SIGTERM` to a launcher process ends its headless runtime child within 6 s
  - Windows: killing the launcher process ends the child (job object)
  - Skip each case on the other OS family

### Implementation for Foundation

- [X] T009 Add exit code 40 `RuntimeUnavailable` to Dnui/Internal/ErrorResults.cs (constant, name, and an `ErrorResults.RuntimeUnavailable(DnuiRuntimeUnavailableException)` that builds the error line). Make sure no runtime path in Dnui.Runtime/Hosting/ExitCodes.cs can produce 40
- [X] T010 Change `DnuiRuntimeOptions` in Dnui/Runtime/DnuiRuntimeOptions.cs to a `sealed record`. In Dnui/Runtime/DnuiRuntime.cs, read `DNUI_PACKAGE_SOURCE` when `PackageSource` is null (code > environment > nuget.org)
- [X] T011 Create Dnui/Hosting/RuntimeLaunch.cs: an internal record `RuntimeLaunch(DnuiRuntimeOptions? Runtime, AppSelection? App, IReadOnlyList<string> Command, bool Headless)` with `HeadlessOutcomes`, an outcome flag name, and `Arguments()` (contracts/library-api.md); and internal `AppSelection(string AssemblyPath, string TypeName)`
- [X] T012 Create Dnui/Hosting/ChildProcessLifetime.cs (research R3):
  - Windows: create a job object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` once per process and assign each started runtime to it (P/Invoke `CreateJobObject`, `SetInformationJobObject`, `AssignProcessToJobObject`)
  - Unix: register `PosixSignalRegistration` for SIGINT, SIGTERM, SIGHUP and SIGQUIT while children run. Cancel the default handling, send the same signal to each live child, wait up to 5 s, kill the stragglers, then let the launcher return the child's exit code
- [X] T013 Create Dnui/Hosting/RuntimeLauncher.cs (research R1):
  - `CaptureAsync(RuntimeLaunch, ct)` → `DnuiResult`. Move the logic from `DnuiHost.RunAsync`: stdin closed, stdout read to the end, kill on cancel
  - `StartProtocol(RuntimeLaunch)` → `Process` with stdin and stdout pipes and stderr inherited
  - `PassthroughAsync(IReadOnlyList<string> args, DnuiRuntimeOptions?, ct)` → exit code, no redirection. `DnuiRuntimeUnavailableException` → write the error line from T009 to stdout, details to stderr, return 40
  - every mode resolves through `DnuiRuntime.ResolveAsync`, calls the Windows `AllowSetForegroundWindow(ASFW_ANY)` (moved from DnuiHost) and registers the child with `ChildProcessLifetime`
- [X] T014 Add `[assembly: InternalsVisibleTo("Dnui.Tool")]` to Dnui/Properties/AssemblyInfo.cs, plus the helper process from T007 if it's a separate assembly

**Checkpoint**: T005–T008 pass on the dev machine. Existing tests still pass, since nothing calls
the launcher yet.

---

## Phase 3: User Story 1 - Run dnui commands from a shell (Priority: P1) 🎯 MVP

**Goal**: `dnui <args>` behaves exactly like the runtime, resolving it on first use.

**Independent Test**: install the packed tool from a local feed and compare with the runtime run
directly (quickstart §1, §2).

### Tests for User Story 1

- [X] T015 [US1] Write the fixture tests/Dnui.ToolTests/Support/ToolFixture.cs:
  - pack Dnui.Tool into a temp feed with `dotnet pack Dnui.Tool -c Debug -o <feed>`, plus the runtime package for this RID from `artifacts/packages`. Pack the runtime with `PackRuntime` if it's missing
  - `dotnet tool install --tool-path <temp>/tools --add-source <feed> Cmdless.Dnui.Tool --version <DnuiRuntimeVersion>`
  - a `RunToolAsync(args, env, stdin?)` helper that sets `CMDLESS_ROOT` to a fresh temp folder and `DNUI_PACKAGE_SOURCE` to the feed by default
- [X] T016 [P] [US1] Write tests/Dnui.ToolTests/ToolForwardingTests.cs:
  - cold cache: `dialog save-file --headless --outcome '{"path":"/tmp/x.txt"}'` → stdout exactly `{"path":"/tmp/x.txt"}`, exit 0, and the runtime is now installed under `CMDLESS_ROOT`
  - a second run uses the warm cache: with `DNUI_PACKAGE_SOURCE` pointed at an empty folder, it still succeeds
  - usage error (`dialog save-file --headless --outcome '{"paths":[]}'`) → the same stdout and exit 2 as the runtime run directly
  - arguments with spaces, quotes and non-ASCII text reach the runtime (a headless window with a title argument, or an outcome path that contains them)
  - `--version` prints the runtime's version, which equals `DnuiRuntimeVersion`
- [X] T017 [P] [US1] Write tests/Dnui.ToolTests/ToolSessionTests.cs: `dnui tray --headless` with redirected stdio. `initialize` → response, `tray/simulateClick` → `tray/clicked` notification, closing stdin → exit 0 (reuse the protocol helpers from tests/Dnui.Runtime.ContractTests/Support/RuntimeProcess.cs or copy the minimal framing)
- [X] T018 [P] [US1] Write tests/Dnui.ToolTests/ToolFailureTests.cs:
  - empty `DNUI_PACKAGE_SOURCE` folder and empty `CMDLESS_ROOT` → stdout `{"error":{"code":"RuntimeUnavailable",…PackageNotFound…}}`, exit 40, details on stderr
  - the same with `tray` as the command → still the error line and 40

### Implementation for User Story 1

- [X] T019 [US1] Implement Dnui.Tool/Program.cs: `return await RuntimeLauncher.PassthroughAsync(args, runtimeOptions: null, CancellationToken.None);`. Nothing else: no parsing and no output
- [X] T020 [US1] Add the tool's package metadata to Dnui.Tool/Dnui.Tool.csproj: `PackageReadmeFile` from build/package/readme/Tool.md, and a description that names `dnui` and the install command. The shared metadata comes in T023
- [X] T021 [US1] *(The tool tests pack the tool themselves from the build output, so ci.yml needs no new step; Dnui.ToolTests runs through Dnui.slnx in the matrix.)* Add the tool to .github/workflows/ci.yml: after "Pack runtime for this runner", add `dotnet pack Dnui.Tool -c Debug -o artifacts/packages`. Make sure tests/Dnui.ToolTests runs in the matrix (it's in Dnui.slnx; on Linux under xvfb like the rest)
- [X] T022 [US1] Measure the tool's overhead with a warm cache against running the runtime directly (SC-003, ≤ 0.5 s) in tests/Dnui.ToolTests/ToolTimingTests.cs, isolated like tests/Dnui.Runtime.ContractTests/TimingTests.cs

**Checkpoint**: tool tests pass on all three OSes; quickstart §2 works by hand.

---

## Phase 4: User Story 2 - Install the library from nuget.org and have it just work (Priority: P1)

**Goal**: every package carries complete metadata, symbols and SourceLink, and the library
resolves prerelease runtimes from a NuGet feed.

**Independent Test**: inspect the packed `.nupkg` and `.snupkg` files; resolve a prerelease
runtime from a local feed.

### Tests for User Story 2

- [X] T023 [P] [US2] Write tests/Dnui.PublishTests/PackageMetadataTests.cs. It packs Dnui, Dnui.Tool and the runtime for this RID into a temp folder and reads each `.nuspec`:
  - authors `Cmdless`, MIT license, project and repository URLs `https://github.com/cmdless/dnui`, icon, readme
  - the runtime description says it isn't meant to be referenced directly
  - `.snupkg` exists for Dnui and Dnui.Tool
  - the tool package contains no `runtimes/` native folders and no `Avalonia.Desktop`/`Avalonia.Native`/`Avalonia.Win32`/`Avalonia.X11` assemblies (FR-007)

### Implementation for User Story 2

- [X] T024 [US2] Add shared package metadata to Directory.Build.props:
  - `Authors`/`Company` `Cmdless`, `PackageLicenseExpression` MIT
  - `PackageProjectUrl`/`RepositoryUrl` `https://github.com/cmdless/dnui`, `RepositoryType` git
  - `PackageIcon` icon.png, packed from build/package/icon.png for packable projects
  - `ContinuousIntegrationBuild=true` when `GITHUB_ACTIONS` is `true`; `EmbedUntrackedSources=true`

  Remove the duplicated `Authors`/license lines from Dnui/Dnui.csproj
- [X] T025 [US2] In Dnui/Dnui.csproj add `PackageReadmeFile` (build/package/readme/Dnui.md), `IncludeSymbols=true` and `SymbolPackageFormat=snupkg`. Update the description to cover trays, dialogs and windows. Do the same symbols setup in Dnui.Tool/Dnui.Tool.csproj
- [X] T026 [US2] Add to build/RuntimePackage/Cmdless.Dnui.Runtime.nuspec and build/RuntimePackage/RuntimePackage.csproj (through `NuspecProperties`): description ("Runtime for Cmdless.Dnui — not meant to be referenced directly."), `projectUrl`, `repository`, `icon`, `readme` (Runtime.md), and the files entries for icon.png and the readme
- [X] T027 [US2] Run `dotnet pack` for all three kinds locally and confirm T023 passes and nuget's pack warnings for missing metadata are gone

**Checkpoint**: packages are complete; prerelease resolution is covered by T006.

---

## Phase 5: User Story 3 - A consistent public API before it's frozen (Priority: P1)

**Goal**: the API in contracts/library-api.md: one host entry point, `WithApp`, overload chains
ending in strings, tray `Configure`, no public raw run, a single exception type for runtime
failures.

**Independent Test**: the public type guard, plus contract tests for `Configure`, the headless
host tray and command-line equality across the overload forms.

### Tests for User Story 3

- [X] T028 [P] [US3] Update tests/Dnui.Tests/Api/PublicApiTests.cs to the new type list:
  - remove `Dnui.DnuiTrayStartException`
  - everything else stays

  Add member-level assertions:
  - `DnuiHost` has no public `RunAsync`
  - `DnuiTray` has no public static `StartAsync`
  - `DnuiTrayOptions` has no `Runtime`/`Headless`/`AppAssembly`/`AppType` and has `Configure`
  - no public method takes an `Assembly` parameter
  - `DnuiRuntimeOptions` is a record
- [X] T029 [P] [US3] Write tests/Dnui.Tests/Hosting/HostArgumentTests.cs: with an internal seam that captures the `RuntimeLaunch` instead of starting a process, check that:
  - `WithApp<TApp>()`, `WithApp(typeof(TApp))` and `WithApp(path, name)` give identical arguments
  - `OpenWindowAsync<TWindow>()`, `OpenWindowAsync(typeof(TWindow))` and `OpenWindowAsync(path, name)` give identical arguments
  - a dialog on an app-bound host includes `--app-assembly/--app-type`
  - `WithApp(Type)` with a non-`DnuiCommandApp` type → `ArgumentException`
  - `WithApp` on a bound host replaces the app
  - bound hosts share the headless outcome sequence
- [X] T030 [P] [US3] Write tests/Dnui.Runtime.ContractTests/TrayConfigureTests.cs, headless:
  - handlers attached in `Configure` receive a `SimulateClickAsync` issued immediately after start, 100 times in a row (SC-008)
  - a tray method called inside `Configure` → `DnuiTrayNotRunningException`, start rethrows it, and the runtime process has exited
  - `Configure` throwing → the same exception from `StartTrayAsync`, and the runtime has exited
- [X] T031 [P] [US3] Extend tests/Dnui.Runtime.ContractTests/DnuiHostTests.cs:
  - `new DnuiHost(new() { Headless = true }).StartTrayAsync(…)` → `GetStateAsync` shows a headless tray, with no tray option set
  - `Runtime` set on the host is used by trays, dialogs and windows (`RuntimePath` pointing at a copy of the runtime, observed through a marker)
  - a tray start failure (for example an app type that doesn't exist via `WithApp(path, "Nope")`) → `DnuiCommandException` with `ErrorCode` `AppTypeNotFound` and exit 11
- [X] T032 [P] [US3] Extend tests/Dnui.Runtime.ContractTests/WindowCommandsTests.cs: `this.Ui.OpenWindowAsync(path, typeName)` opens a child from an assembly path (tests/Dnui.TestApps.Extra), and the generic overload works for a window without `new()` in the constraint

### Implementation for User Story 3

- [X] T033 [US3] Rework Dnui/Dialogs/IDnuiDialogs.cs to contracts/library-api.md:
  - `IDnuiHostCommands`: the `WithApp` ×3 chain; `OpenWindowAsync` generic, `(Type)` and `(string, string)`; `StartTrayAsync(options)`
  - `IDnuiWindowCommands`: generic without `new()`, `(Type)` and `(string, string)`
  - remove the `Assembly` overloads and `<TWindow, TApp>`
- [X] T034 [US3] Rework Dnui/Hosting/DnuiHost.cs:
  - private constructor `(options, OutcomeQueue, AppSelection?)`
  - the `WithApp` chain (`Type` → `AssemblyLocator.Find` + `FullName`; validates `DnuiCommandApp`)
  - the window chain ending in strings
  - dialogs, windows and trays built through `RuntimeLaunch` and `RuntimeLauncher.CaptureAsync`
  - `RunAsync` becomes internal or is removed in favor of the launcher
  - remove `AddApp`, `AllowForeground` and the `TakeOutcome` fields in favor of a new internal Dnui/Hosting/OutcomeQueue.cs shared by bound hosts
  - add the internal seam used by T029
- [X] T035 [US3] Rework Dnui/Tray/Client/DnuiTray.cs:
  - `StartAsync` becomes internal and takes `(RuntimeLaunch launch, DnuiTrayOptions options, ct)`
  - start the process through `RuntimeLauncher.StartProtocol`, construct the tray, run `options.Configure` in a try/catch (kill, dispose, rethrow), then `StartListening` and `initialize`
  - add a `_started` flag: `CallAsync` throws `DnuiTrayNotRunningException("The tray hasn't started yet.")` before `initialize` completes
  - start failures throw `DnuiCommandException` (name from the exit-code table, null StdOut) instead of `DnuiTrayStartException`
- [X] T036 [US3] Update Dnui/Tray/Client/DnuiTrayOptions.cs: remove `Runtime`, `Headless`, `AppAssembly` and `AppType`; add `Action<DnuiTray>? Configure` with docs. Remove `DnuiTrayStartException` from Dnui/Tray/Client/DnuiTrayExceptions.cs
- [X] T037 [US3] Update Dnui/Windows/DnuiWindowCommands.cs:
  - the generic overload uses `OpenWindowAsync(typeof(TWindow))`
  - add `(string assemblyPath, string typeName)` through `DnuiTypeLoader`, which loads by path, reusing a loaded assembly
  - remove the `Assembly` overload
- [X] T038 [US3] Update every caller to the new API:
  - tests/Dnui.Runtime.ContractTests (TrayClientTests, DnuiHostTests, DialogCommandTests, WindowCommandTests, HeadlessGuardTests, RuntimeAcquisitionContractTests)
  - tests/Dnui.SampleApp/Program.cs
  - tests/Dnui.TestApps
  - the README samples

  Trays start through `new DnuiHost(new() { Runtime = …, Headless = true }).StartTrayAsync(…)`
- [X] T039 [US3] Check the rest of the public surface for anything T028 doesn't cover. Record any further inconsistency, and its fix or the reason to keep it, in specs/003-tool-publishing/research.md R12 (FR-026)

**Checkpoint**: the full suite passes with the new API on the dev machine.

---

## Phase 6: User Story 4 - Release with one tag and no stored secrets (Priority: P2)

**Goal**: release.yml from contracts/release.md: pack → publish (OIDC, `nuget` environment,
runtime packages first, wait, skip duplicates) → GitHub Release.

**Independent Test**: a `workflow_dispatch` dry run packs 8 `.nupkg` + 2 `.snupkg` files and
publishes nothing; a preview tag publishes end to end (quickstart §3–4).

### Implementation for User Story 4

- [X] T040 [US4] Create scripts/release/check-sizes.sh: print the size of each `.nupkg` in a folder and exit 1 naming any over 250 MB (262,144,000 bytes)
- [X] T041 [US4] Create scripts/release/wait-for-packages.sh `<version> <id>...`: for each id, poll `https://api.nuget.org/v3-flatcontainer/{lower id}/{lower version}/{lower id}.{lower version}.nupkg` with HEAD every 30 s until 200. Fail after 30 minutes, naming the packages still missing
- [X] T042 [US4] Rewrite .github/workflows/release.yml to contracts/release.md:
  - job `pack` (`contents: read`): the tag check first, the runtime packs for 6 RIDs, `dotnet pack` Dnui and Dnui.Tool `-c Release`, check-sizes.sh, upload the artifact
  - job `publish` (tags only, `needs: pack`, `environment: nuget`, `permissions: id-token: write, contents: read`):
    - download the artifact
    - `NuGet/login@v1` with `user: ${{ secrets.NUGET_USER }}`
    - push `Cmdless.Dnui.Runtime.*.nupkg` with `--skip-duplicate`
    - wait-for-packages.sh for the 6 ids
    - `NuGet/login@v1` again
    - push `Cmdless.Dnui.*.nupkg` (not the runtime packages) and `Cmdless.Dnui.Tool.*.nupkg` with `--skip-duplicate` (their `.snupkg` files go up alongside)
  - job `github-release` (tags only, `needs: publish`, `contents: write`): `gh release create`, or `gh release upload --clobber` if the release exists
- [ ] T043 [US4] Run the workflow with `workflow_dispatch` on the branch, after it's pushed and with the maintainer's go-ahead. Confirm the artifact contents and the printed sizes, and that `publish` and `github-release` were skipped

**Checkpoint**: the dry run is green. The preview release (quickstart §4) happens after merge, by
the maintainer.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T044 [P] Update README.md: installing and using the tool (`dotnet tool install -g Cmdless.Dnui.Tool`, `dnx`), `CMDLESS_ROOT` and `DNUI_PACKAGE_SOURCE`, the new API (`WithApp`, `Configure`), and releasing (tag + approval)
- [X] T045 [P] Add a "Superseded by spec 003" note at the top of the affected sections in specs/001-tray-command/contracts/tray-client.md and specs/002-windows-dialogs/contracts/library-api.md, linking to specs/003-tool-publishing/contracts/library-api.md
- [X] T046 [P] Add exit code 40 to the exit-code table in specs/002-windows-dialogs/contracts/cli.md, as "library-side, never produced by the runtime"
- [ ] T047 Run `dotnet build Dnui.slnx` (0 warnings) and `dotnet test Dnui.slnx` locally, then push and confirm CI is green on all three OSes plus the AOT job
- [ ] T048 Run quickstart.md §1–2 by hand on macOS (a real dialog through the tool; Ctrl+C ends both processes). Record the results in specs/003-tool-publishing/research.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies. T001 comes first, since the amendment gates the new package.
- **Foundational (Phase 2)**: depends on Setup. It blocks all stories.
- **US1 (Phase 3)**: depends on Foundational (launcher, exit 40, env override).
- **US2 (Phase 4)**: depends on Setup T004 (assets). It's independent of US1 and US3, except that
  T023 packs the tool, so it needs T002.
- **US3 (Phase 5)**: depends on Foundational. It's independent of US1 and US2.
- **US4 (Phase 6)**: depends on US1 (the tool package exists) and US2 (metadata and symbols).
  T043 needs the branch pushed.
- **Polish (Phase 7)**: after all stories.

### Within Each Story

- Tests first (they fail), then implementation.
- US3: T033 (interfaces) → T034/T035/T036/T037 → T038 (callers).

### Parallel Opportunities

- Setup: T002, T003, T004.
- Foundational tests: T005–T008.
- After Foundational: US1, US2 and US3 can proceed in parallel (different files). The exceptions
  are Dnui.Tool/Dnui.Tool.csproj (T020 and T025, run in order) and .github/workflows/ci.yml (T021
  only).
- US3 tests: T028–T032.
- Polish: T044–T046.

## Parallel Example: User Story 3

```text
T028 PublicApiTests.cs | T029 HostArgumentTests.cs | T030 TrayConfigureTests.cs | T031 DnuiHostTests.cs | T032 WindowCommandsTests.cs
```

## Implementation Strategy

### MVP

Phases 1–3: the tool works from a local feed on every OS. Shell users get value even before any
publishing.

### Incremental Delivery

1. US1: the tool.
2. US3: the API cleanup. It has to land before any publish.
3. US2: package metadata and symbols.
4. US4: the release pipeline (dry run in this branch).
5. After merge, the maintainer tags `v0.1.0-preview.1` (quickstart §4).

### Notes

- Nothing is pushed to nuget.org from this branch. The first real publish is the maintainer's
  preview tag after merge.
- The manual nuget.org and GitHub setup is done (contracts/release.md).
