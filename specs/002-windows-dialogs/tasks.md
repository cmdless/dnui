---

description: "Task list for windows and built-in dialogs"
---

# Tasks: Windows and Built-in Dialogs

**Input**: Design documents from `/specs/002-windows-dialogs/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (cli.md,
library-api.md), quickstart.md

**Tests**: included. The constitution requires contract tests for every command and the type
resolution matrix. Write each story's tests first and confirm they fail.

**Organization**: phases follow spec priority: US1 (P1 dialogs), US2 (P1 windows, including
publish-mode support), US3 (P2 parented dialogs), US4 (P2 child windows), US5 (P3 headless window
commands and CI).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: User story from spec.md (US1–US5)

## Path Conventions

Same as spec 001: `Dnui/` (library), `Dnui.Runtime/` (host executable), `tests/`. New library
folders: `Dnui/Windows/`, `Dnui/Dialogs/`, `Dnui/Hosting/`, `Dnui/build/`.

---

## Phase 1: Setup

- [ ] T001 Add `<PackageReference Include="Avalonia.Headless" Version="$(AvaloniaVersion)" />` to Dnui.Runtime/Dnui.Runtime.csproj (research R10). Build and confirm 0 warnings
- [ ] T002 [P] Create the single-project sample app tests/Dnui.SampleApp/Dnui.SampleApp.csproj (Exe, net10.0, references Dnui/Dnui.csproj, and imports Dnui/build/Cmdless.Dnui.targets directly with `<Import Project="..\..\Dnui\build\Cmdless.Dnui.targets" />`, standing in for the package's buildTransitive import). Don't add it to Dnui.slnx; publish tests build it explicitly
- [ ] T003 [P] Create test project tests/Dnui.PublishTests/Dnui.PublishTests.csproj (xUnit v3, `<Using Include="Xunit" />`, NoWarn xUnit1051, `AssemblyMetadata("DnuiRepoRoot", …)`) and add it to Dnui.slnx

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the shared type loader, the dialog and window public types, the one-shot host
plumbing, CLI surface, headless platform, and exit codes (research R1–R3, R5, R10, R11)

**⚠️ CRITICAL**: no user story can start until this phase is complete

### Tests for Foundation

- [ ] T004 [P] Write tests/Dnui.Tests/Hosting/AssemblyLocatorTests.cs:
  - order is `Location`, then `{base}/{name}.dll`, then `{base}/dnui/{name}.dll`
  - a missing file → `DnuiAssemblyNotFoundException`, listing all three searched paths

  Use an internal seam `AssemblyLocator.Find(Assembly, string baseDirectory, Func<Assembly,string> location)` so the tests can fake an empty `Location`
- [ ] T005 [P] Write tests/Dnui.Tests/Hosting/DnuiTypeLoaderTests.cs:
  - a loaded assembly is reused
  - an unloadable path → `AssemblyLoadFailed`
  - a missing type → `TypeNotFound`
  - wrong base, abstract, or no public parameterless constructor → `TypeInvalid`
  - a Dnui version mismatch (an emitted assembly referencing Dnui 9.9.9.9, as in spec 001's VersionMismatch test helper; move that helper to tests/Dnui.Tests/TestSupport/AssemblyEmitter.cs) → `VersionMismatch`
  - the required base is a parameter (`DnuiApp` or `DnuiWindow`)
- [ ] T006 [P] Write tests/Dnui.Tests/Dialogs/FileFilterParsingTests.cs: `"Images:*.png,*.jpg"` → `DnuiFileFilter("Images", ["*.png","*.jpg"])`. A missing name, a missing `:`, an empty pattern, and blank entries are each rejected with a message
- [ ] T007 [P] Write tests/Dnui.Tests/Dialogs/DialogOutcomeTests.cs against data-model.md "Headless outcome":
  - open-file and open-folder accept `{"paths":[…]}` (non-empty strings) or `{"paths":null}`; `{"paths":[]}`, a missing `paths`, and extra types are rejected
  - save-file accepts `{"path":"…"}` or `{"path":null}`
- [ ] T008 [P] Write tests/Dnui.Runtime.ContractTests/HostCliTests.cs (exit 2 plus a `Usage` error line):
  - `dialog` with no kind
  - `dialog open-file --headless` with no `--outcome`
  - `--outcome` without `--headless`
  - `dialog open-file --filter bad`
  - `window` with one argument

### Implementation for Foundation

- [ ] T009 [P] Create Dnui/Dialogs/DnuiDialogOptions.cs per contracts/library-api.md: `DnuiFileFilter`, `DnuiOpenFileOptions`, `DnuiOpenFolderOptions`, `DnuiSaveFileOptions` (`OverwritePrompt = true`), and an internal `FileFilterParser.Parse(string)` per data-model.md FileFilter
- [ ] T010 [P] Create Dnui/Dialogs/IDnuiDialogs.cs with the public interfaces `IDnuiDialogs`, `IDnuiWindowCommands`, and `IDnuiHostCommands`, exactly as in contracts/library-api.md (window commands: no app or tray members)
- [ ] T011 [P] Create Dnui/Dialogs/DialogOutcome.cs: an internal `DialogKind { OpenFile, OpenFolder, SaveFile }` and `DialogOutcome.Validate(DialogKind, JsonNode?) → (JsonObject result | error)` per data-model.md
- [ ] T012 Create Dnui/Hosting/DnuiTypeLoader.cs by moving the logic from Dnui.Runtime/Hosting/AppTypeResolver.cs: `Load(string assemblyPath, string typeName, Type requiredBase)` → result with `Type?`, `Failure` (`AssemblyLoadFailed`, `TypeNotFound`, `TypeInvalid`, `VersionMismatch`), and message. It uses the default load context, one chained `AssemblyDependencyResolver` per loaded file, reuses an already-loaded assembly with the same name, and checks the version before `GetType`. Rewrite Dnui.Runtime/Hosting/AppTypeResolver.cs as a thin wrapper mapping failures to exit codes 10–13. Keep spec 001's AppResolution contract tests green (depends on T005)
- [ ] T013 [P] Create Dnui/Hosting/AssemblyLocator.cs per research R3, with the T004 seam, and Dnui/Hosting/DnuiAssemblyNotFoundException.cs (`AssemblyName`, `SearchedPaths`, a message naming `dnui/{name}.dll` and the fix)
- [ ] T014 [P] Create Dnui/Hosting/DnuiCommandException.cs (`Result`, `ErrorCode`, message from `error.message`) and Dnui/Hosting/DnuiHostOptions.cs (`Runtime`, `Headless`, `Outcomes`)
- [ ] T015 Create Dnui/Hosting/DnuiHost.cs with `Default` and `RunAsync(args, ct)`:
  1. Resolve the runtime.
  2. Call `AllowSetForegroundWindow(-1)` on Windows (P/Invoke, research R9).
  3. Start the runtime with stdin redirected and closed immediately, stdout redirected, and stderr inherited.
  4. Read stdout to the end, await exit, and return `DnuiResult(code, trimmed line or null)`.
  5. On cancellation, kill the process tree and rethrow.

  Add an internal `RunTypedAsync(args, Func<JsonNode, T> parse)` that throws `DnuiCommandException` on a non-zero exit (depends on T014)
- [ ] T016 Extend Dnui.Runtime/Hosting/ExitCodes.cs with 14 `WindowAssemblyLoadFailed`, 15 `WindowTypeNotFound`, 16 `WindowTypeInvalid`, 17 `WindowFailed`, and 30 `DialogUnsupported` (research R11), including `NameOf`
- [ ] T017 Extend Dnui.Runtime/Cli/RuntimeCommandLine.cs per contracts/cli.md:
  - `window <assembly-path> <type-name> [--headless] [--dialog-outcome <json>]…`
  - `dialog open-file [--title --multiple --filter… --start-folder]`, `dialog open-folder [--title --multiple --start-folder]`, and `dialog save-file [--title --suggested-name --default-extension --filter… --start-folder --no-overwrite-prompt]`, each with `[--headless --outcome <json>]`
  - Validate the filter syntax and the `--outcome`/`--headless` pairing as parse errors. Expose the parsed values on `RuntimeInvocation`
  - `window` and `dialog` aren't session commands (depends on T009, T011)
- [ ] T018 Create Dnui.Runtime/Platform/HeadlessPlatform.cs: when a `window`/`dialog` invocation has `--headless`, build with `.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })` instead of `UsePlatformDetect()`. Wire it into Program.cs (the builder step) (depends on T001)

**Checkpoint**: unit tests T004–T007 pass; the CLI rejects bad usage (T008); spec 001's suites stay green.

---

## Phase 3: User Story 1 - Pick files, a folder, or a save location (Priority: P1) 🎯 MVP

**Goal**: `dialog open-file|open-folder|save-file` host commands and the `DnuiHost` dialog
functions, unparented, in front, with all basic options.

**Independent Test**: each subcommand in headless mode with an outcome and with a cancel produces
the right single line and exit 0. On a desktop, quickstart §2.

### Tests for User Story 1

- [ ] T019 [P] [US1] Write tests/Dnui.Tests/Dialogs/DialogOptionMappingTests.cs: `DialogRunner.Map(...)` turns each options record into the Avalonia option object as in research R8: Title, AllowMultiple, `FilePickerFileType(name){Patterns}` for filters and choices, SuggestedFileName, DefaultExtension with or without a leading dot, and ShowOverwritePrompt. The start folder lookup is a seam: a missing folder → `null`
- [ ] T020 [P] [US1] Write tests/Dnui.Runtime.ContractTests/DialogCommandTests.cs (headless):
  - `open-file --outcome {"paths":["/a","/b"]}` → that line, exit 0
  - `{"paths":null}` → that line, exit 0
  - `open-folder` and `save-file` equivalents
  - a wrong-shape outcome → exit 2
  - an app override (`--app-assembly TestApps --app-type CustomCommandApp`) works
  - stdout always has exactly one line
- [ ] T021 [P] [US1] Write tests/Dnui.Runtime.ContractTests/DnuiHostDialogTests.cs: with `new DnuiHost(new() { Runtime = new() { RuntimePath = built runtime }, Headless = true, Outcomes = [...] })`:
  - `OpenFilesAsync` returns the outcome's paths, or null
  - `SaveFileAsync` returns the path or null
  - a cancelled `CancellationToken` → `OperationCanceledException`, with no process left behind
  - `RunAsync(["dialog","open-file","--headless","--outcome","{\"paths\":null}"])` → `DnuiResult(0, "{\"paths\":null}")`

### Implementation for User Story 1

- [ ] T022 [US1] Create Dnui/Dialogs/DialogRunner.cs:
  - `Map` functions per R8.
  - `RunAsync(TopLevel owner, DialogKind, options)` checks `CanOpen`/`CanSave`/`CanPickFolder` (else throws an internal `DialogUnsupportedException`), resolves the start folder via `TryGetFolderFromPathAsync` (null when missing), calls the picker, maps the items via `TryGetLocalPath()`, drops the ones without a path, and returns `{"paths":[…]}`/`{"paths":null}` or `{"path":…}`/`{"path":null}`.
  - Has a headless branch that takes the next validated outcome instead.

  (depends on T009, T011)
- [ ] T023 [US1] Create Dnui/Dialogs/DialogCommand.cs (`DnuiCommand`, one-shot). `Run`:
  1. Sets `ShutdownMode.OnExplicitShutdown`.
  2. Creates the invisible owner window (R9): `SystemDecorations.None`, `Opacity = 0`, `ShowInTaskbar = true`, 1×1 centered on Windows and Linux, normal size centered on macOS. Then shows it and activates it.
  3. Awaits `DialogRunner`.
  4. Calls `app.ResolveInternal(result, 0)`.
  5. On `DialogUnsupportedException`, resolves the error `{"error":{"code":"DialogUnsupported",…}}` with exit 30.

  Headless skips the owner window and UI (depends on T022)
- [ ] T024 [P] [US1] Create Dnui.Runtime/Platform/MacForeground.cs: on macOS, `objc_msgSend(NSApp, activateIgnoringOtherApps:, YES)` via `[DllImport("/usr/lib/libobjc.A.dylib")]` (`objc_getClass`, `sel_registerName`, `objc_msgSend`). Called by the runtime before a `window`/`dialog` command shows UI (a hook on `DnuiCommand`, `bool BringToFront`). It's a no-op elsewhere, and Windows uses `Activate()` on the owner
- [ ] T025 [US1] Register `dialog` in Program.cs `CreateCommand` (`new DialogCommand(kind, options, headless, outcome)`) (depends on T017, T023)
- [ ] T026 [US1] Implement `IDnuiDialogs` on Dnui/Hosting/DnuiHost.cs: build the `dialog …` arguments from options (filters as `Name:p1,p2`, flags), add `--headless --outcome` from `Options.Outcomes` in order when headless, and parse `paths`/`path` (depends on T015, T025)
- [ ] T027 [US1] Windows foreground at tray clicks: in Dnui/Tray/AvaloniaTrayHost.cs, call `AllowSetForegroundWindow(-1)` (P/Invoke, Windows only) in the item `Click` handler before raising `ItemClicked`. Document it in specs/001-tray-command/contracts/tray-protocol.md as a platform note

**Checkpoint**: US1 tests pass; quickstart §2 and §5 on the dev machine.

---

## Phase 4: User Story 2 - Open a consumer window and get its result (Priority: P1)

**Goal**: the `window` host command, `DnuiWindow`, `DnuiHost.OpenWindowAsync` overloads with app
generics, and automatic `dnui/` shipping, so one project works in every publish mode.

**Independent Test**: `window` with test windows resolves `{"result":…}`, `{"result":null}` on
close, and exits 14–17 for bad types. The published SampleApp (single-file) opens its own window
through generics.

### Tests for User Story 2

- [ ] T028 [P] [US2] Add test windows to tests/Dnui.TestApps/Windows.cs (code-only `DnuiWindow`s, plus one with XAML in tests/Dnui.TestApps/NameFormWindow.axaml(.cs)):
  - `ResolveOnOpenWindow` (resolves `{"name":"x"}` on `Opened`)
  - `CloseOnOpenWindow` (calls `Close()` on `Opened`)
  - `ThrowingCtorWindow`
  - `ThrowAfterOpenWindow` (throws from a dispatcher callback after `Opened`)
  - `NameFormWindow` (a text box plus Submit, for manual quickstart §3)
  - `NotAWindow` (plain class)
  - `AbstractWindow` (abstract)

  Enable Avalonia XAML in Dnui.TestApps.csproj (`<AvaloniaResource>` / compiled XAML)
- [ ] T029 [P] [US2] Write tests/Dnui.Runtime.ContractTests/WindowCommandTests.cs (headless):
  - `ResolveOnOpenWindow` → `{"result":{"name":"x"}}`, exit 0
  - `CloseOnOpenWindow` → `{"result":null}`, exit 0
  - `ThrowingCtorWindow` and `ThrowAfterOpenWindow` → exit 17, `WindowFailed`
  - a missing assembly → 14
  - a missing type → 15
  - `NotAWindow` and `AbstractWindow` → 16
  - an emitted assembly referencing Dnui 9.9.9.9 → 13
  - with `--app-assembly`/`--app-type` = TestApps `CustomCommandApp`: the window opens, and stderr has the custom-configure marker (the same assembly is loaded once)
- [ ] T030 [P] [US2] Write tests/Dnui.Runtime.ContractTests/DnuiHostWindowTests.cs (headless):
  - `OpenWindowAsync<ResolveOnOpenWindow>()` → `{"name":"x"}`
  - `OpenWindowAsync<ResolveOnOpenWindow, CustomCommandApp>()` works
  - `OpenWindowAsync(Type)` and `(Assembly, string)` overloads behave the same
  - `ThrowingCtorWindow` → `DnuiCommandException` with `ErrorCode == "WindowFailed"`
  - cancellation kills the process

  (TestApps referenced with `ReferenceOutputAssembly="true"` in this test project for typed access)
- [ ] T031 [P] [US2] Write tests/Dnui.Tests/Build/ShipFilterTests.cs: run the `DnuiShipUiAssemblies` item filter (factored into a small inline MSBuild task or an item-transform target invoked through `dotnet msbuild -getItem`) over a fake item list, and assert that `Avalonia*.dll`, `Dnui.dll` and `StreamJsonRpc*.dll` are excluded, and that other managed `.dll` files and satellite folders are kept
- [ ] T032 [P] [US2] Write tests/Dnui.PublishTests/SingleFilePublishTests.cs:
  - `dotnet publish tests/Dnui.SampleApp -c Release -r <current rid> -p:PublishSingleFile=true --self-contained` into a temp folder
  - assert that `dnui/Dnui.SampleApp.dll` and `.deps.json` exist and that `dnui/` has no Avalonia or Dnui assemblies
  - run the published executable with `--self-test` (SampleApp's mode: `DnuiHost` with `RuntimePath` = the built runtime and `Headless = true`, `OpenWindowAsync<SampleWindow, SampleApp>()`, printing the result) and assert the result
  - also publish with `-p:DnuiShipUiAssemblies=false` and assert `DnuiAssemblyNotFoundException` naming `dnui/Dnui.SampleApp.dll`

### Implementation for User Story 2

- [ ] T033 [US2] Create Dnui/Windows/DnuiWindow.cs (public abstract `: Window`):
  - `protected void Resolve(JsonNode?)`: first call wins, completes `Completion`, calls `Close()`
  - `Closed` → completes with `null` if not resolved
  - `internal Task<JsonNode?> Completion`
  - `public IDnuiWindowCommands Ui` (lazy `DnuiWindowCommands(this)`; the dialog and child members are filled in by US3 and US4)
- [ ] T034 [US2] Create Dnui/Windows/WindowCommand.cs (`DnuiCommand`, one-shot, `BringToFront`):
  - `Prepare` loads through `DnuiTypeLoader` with base `DnuiWindow` and maps the failures to 14/15/16/13 (error lines, not sessions).
  - `Run` sets `OnExplicitShutdown`, constructs the window in a try block (an exception → resolve the `WindowFailed` error with exit 17), shows it, activates it, then `Completion.ContinueWith(r => app.ResolveInternal(new JsonObject{["result"]=r?.DeepClone()}, 0))`.
  - Stores the `--dialog-outcome` queue for US3.

  (depends on T012, T033)
- [ ] T035 [US2] Map unhandled UI-thread exceptions during a `window` command to exit 17: subscribe to `Dispatcher.UIThread.UnhandledException` in WindowCommand.Run, mark it handled, and resolve the `WindowFailed` error (message = exception message) (depends on T034)
- [ ] T036 [US2] Register `window` in Program.cs `CreateCommand` (depends on T017, T034)
- [ ] T037 [US2] Implement the window and tray members of `IDnuiHostCommands` on Dnui/Hosting/DnuiHost.cs:
  - every overload reduces to `(assemblyPath via AssemblyLocator, type.FullName)`, plus an optional app pair → `[--app-assembly P --app-type T] window <path> <type> [--headless --dialog-outcome …]`
  - parse `{"result":…}`
  - `StartTrayAsync`/`StartTrayAsync<TApp>` delegate to `DnuiTray.StartAsync` with the app options

  (depends on T013, T015, T036)
- [ ] T038 [US2] Create Dnui/build/Cmdless.Dnui.targets (research R4). Target `DnuiShipUiAssemblies`:
  - runs `AfterTargets="Publish"`, conditioned on `PublishAot` or `PublishSingleFile` and `DnuiShipUiAssemblies != false`
  - copies `@(IntermediateAssembly)`, `$(ProjectDepsFilePath)`, and the filtered managed `@(ReferenceCopyLocalPaths)` (keeping `DestinationSubDirectory` for satellites) to `$(PublishDir)dnui/`

  Pack it in Dnui/Dnui.csproj as `buildTransitive/Cmdless.Dnui.targets` (`<None Include="build/Cmdless.Dnui.targets" Pack="true" PackagePath="buildTransitive" />`)
- [ ] T039 [US2] Write tests/Dnui.SampleApp: `SampleWindow : DnuiWindow` (resolves `{"sample":true}` on open), `SampleApp : DnuiCommandApp`, and Program.cs with `--self-test` (as in T032) and a normal mode that opens the window non-headless (depends on T037, T038)
- [ ] T040 [US2] Add a publish-tests step to .github/workflows/ci.yml (all three runners run `dotnet test tests/Dnui.PublishTests`), and an `aot-publish` job on ubuntu-latest that installs `clang zlib1g-dev`, publishes SampleApp with `-p:PublishAot=true`, and runs the same assertions via an environment variable telling the PublishTests to use AOT (depends on T032, T039)

**Checkpoint**: US1 and US2 pass; quickstart §3 and §6 on the dev machine.

---

## Phase 5: User Story 3 - Window code shows parented dialogs (Priority: P2)

**Goal**: `DnuiWindow.Ui` dialogs: modal to the window, queued, cancelled when the window closes,
same options and results.

**Independent Test**: a headless test window requests open-file through `Ui` with a
`--dialog-outcome` and resolves with it; the `window` result line contains the paths.

### Tests for User Story 3

- [ ] T041 [P] [US3] Add test windows to tests/Dnui.TestApps/DialogWindows.cs:
  - `PickOneWindow`: on open, `await Ui.OpenFilesAsync(new(){AllowMultiple=true})`, then resolves `{"paths":…}`
  - `PickTwiceWindow`: requests two dialogs at once, resolves both results in order, and records the request and completion order to prove queueing
  - `CloseDuringDialogWindow`: requests a dialog, then closes itself before the outcome; the awaited result must be null
  - `SharedComponent`: a class taking `IDnuiDialogs` and returning `SaveFileAsync(...)`, used by `SharedComponentWindow`
- [ ] T042 [P] [US3] Write tests/Dnui.Runtime.ContractTests/WindowDialogTests.cs (headless):
  - `PickOneWindow` with `--dialog-outcome {"paths":["/x"]}` → `{"result":{"paths":["/x"]}}`
  - `PickTwiceWindow` with two outcomes → both in request order, with the order recorded as sequential
  - `CloseDuringDialogWindow` → `{"result":null}`-style evidence of a null dialog result
  - running out of outcomes → exit 17
  - **SC-007**: `SharedComponent` from the host (`DnuiHost` with an outcome) and from `SharedComponentWindow` (via `--dialog-outcome`) gives identical results

### Implementation for User Story 3

- [ ] T043 [US3] Create Dnui/Windows/DnuiWindowCommands.cs, the dialog part:
  - implements `IDnuiDialogs` on the owner's `StorageProvider` through `DialogRunner.RunAsync(owner, …)`
  - a per-window `SemaphoreSlim(1)` queue
  - `owner.Closed` cancels waiting and in-flight requests → null
  - an unsupported kind → `DnuiCommandException` with `ErrorCode = "DialogUnsupported"`
  - in headless mode, outcomes come from the `--dialog-outcome` queue held by WindowCommand (passed through an internal property on `DnuiWindow`); an empty queue → `InvalidOperationException`

  (depends on T022, T033, T034)

**Checkpoint**: US3 tests pass; quickstart §4 (dialog half) on the dev machine.

---

## Phase 6: User Story 4 - Window code opens child windows (Priority: P2)

**Goal**: `DnuiWindow.Ui.OpenWindowAsync` overloads. Children are owned, run in the same app,
return their result, may load new assemblies, and the surface has no tray or app override.

**Independent Test**: a parent opens a child that resolves; the parent resolves with the child's
result.

### Tests for User Story 4

- [ ] T044 [P] [US4] Add test windows to tests/Dnui.TestApps/ChildWindows.cs:
  - `ParentOfChildWindow` (`await Ui.OpenWindowAsync<ResolveOnOpenWindow>()`, resolves `{"child":…}`)
  - `ParentClosesChildWindow` (opens a child that never resolves, then closes itself; the child's completion must be null and the child closed)
  - `ParentOfExternalChildWindow` (opens a child by `(Assembly path-loaded, name)` from a second test assembly, tests/Dnui.TestApps.Extra, not otherwise loaded)

  Create tests/Dnui.TestApps.Extra (class library with `ExtraChildWindow`)
- [ ] T045 [P] [US4] Write tests/Dnui.Runtime.ContractTests/ChildWindowTests.cs (headless):
  - `ParentOfChildWindow` → `{"result":{"child":{"name":"x"}}}`
  - `ParentClosesChildWindow` → null evidence
  - `ParentOfExternalChildWindow` → the child result, with the Extra assembly loaded into the same process (the window prints its `AppDomain` assembly list to stderr; assert there's one runtime process)
- [ ] T046 [P] [US4] Write tests/Dnui.Tests/Api/WindowSurfaceTests.cs: reflection over `IDnuiWindowCommands` asserts no member mentions `DnuiCommandApp`, `DnuiApp`, or `DnuiTray`, and no parameter named `app*` (FR-002, constitution I)

### Implementation for User Story 4

- [ ] T047 [US4] Complete Dnui/Windows/DnuiWindowCommands.cs, the child-window part:
  - `OpenWindowAsync<T>()` does `new T()`.
  - `(Type)` uses `Activator.CreateInstance` after checking the base.
  - `(Assembly, string)` uses `assembly.GetType` (already loaded), or `DnuiTypeLoader` for unloaded-by-path cases. Failures → `DnuiCommandException` with codes 14–16 or 13 and names.
  - Then `child.Show(owner)`, and returns `child.Completion`.
  - The owner closing closes the children (Avalonia owned-window behavior), so they complete with null.

  (depends on T012, T033)

**Checkpoint**: US4 tests pass; quickstart §4 (child half).

---

## Phase 7: User Story 5 - Windows and dialogs testable without a desktop (Priority: P3)

**Goal**: confirm the whole suite is headless-only and CI-green on three OSes, and document the
testing approach.

- [ ] T048 [P] [US5] Write tests/Dnui.Runtime.ContractTests/HeadlessGuardTests.cs: run each `window`/`dialog` contract scenario with `DISPLAY` and `WAYLAND_DISPLAY` unset (Linux) or under the normal runner (Windows/macOS), and assert no failure related to a missing display, proving `--headless` needs no display server
- [ ] T049 [US5] Update README.md "Testing your UI" with how consumer tests use `DnuiHost(new(){ Headless = true, Outcomes = [...] })` and `--headless --outcome`/`--dialog-outcome`

---

## Phase 8: Polish & Cross-Cutting

- [ ] T050 [P] Extend tests/Dnui.Tests/Api/PublicApiTests.cs with the new public types from contracts/library-api.md (`DnuiWindow`, `IDnuiDialogs`, `IDnuiWindowCommands`, `IDnuiHostCommands`, the three option records, `DnuiFileFilter`, `DnuiHost`, `DnuiHostOptions`, `DnuiCommandException`, `DnuiAssemblyNotFoundException`)
- [ ] T051 [P] XML docs for all new public members (the build is already warning-free with `GenerateDocumentationFile`)
- [ ] T052 [P] README.md: add "Windows and dialogs", covering the generic convenience functions, the host vs window commands summary, and "one project, any publish mode" (`dnui/`)
- [ ] T053 Run quickstart.md §2–§6 on Windows, macOS, and Linux desktops. In particular, verify the macOS sheet and owner sizing for unparented dialogs (R9) and foreground from tray clicks. Record results in contracts/cli.md's platform table and fix any gaps
- [ ] T054 Final check: 0 warnings; `dotnet test` green on three runners (including PublishTests); the AOT publish job green on Linux

---

## Dependencies & Execution Order

- **Setup → Foundational → stories.** Foundational blocks everything.
- **US1** needs Foundational.
- **US2** needs Foundational. T037 needs T015, which US1 also uses, so US1 and US2 can run in
  parallel if T015 lands first.
- **US3** needs US1 (`DialogRunner`) and US2 (`DnuiWindow`, `WindowCommand`).
- **US4** needs US2. It's independent of US3, except that both edit DnuiWindowCommands.cs, so do
  T043 and T047 one after the other.
- **US5** needs US1–US4.
- **Polish** comes last.

### Parallel Opportunities

- Setup: T002 and T003.
- Foundational tests T004–T008 together; T009, T010, T011, T013, T014 together.
- US1: T019–T021 together; T024 in parallel with T022–T023.
- US2: T028–T032 together; T038 in parallel with T033–T037.
- US3 and US4 tests in parallel (T041, T042, T044–T046).

## Parallel Example: Foundational

```bash
Task: "AssemblyLocatorTests in tests/Dnui.Tests/Hosting/AssemblyLocatorTests.cs"
Task: "DnuiTypeLoaderTests in tests/Dnui.Tests/Hosting/DnuiTypeLoaderTests.cs"
Task: "FileFilterParsingTests in tests/Dnui.Tests/Dialogs/FileFilterParsingTests.cs"
Task: "DialogOutcomeTests in tests/Dnui.Tests/Dialogs/DialogOutcomeTests.cs"
```

## Implementation Strategy

### MVP

Setup, then Foundational, then **US1**: dialogs from a terminal or a tray app. That alone covers
"pick a file from my tray".

### Incremental

US1 → US2 (windows, generics, publish modes) → US3 (parented dialogs) → US4 (child windows) → US5 → Polish.
Each phase ends at a checkpoint where its tests pass on their own.

## Notes

- `window` and `dialog` are always one-shot. Never open a session in their commands.
- Window commands must not gain app or tray members (T046 guards this).
- Platform code (macOS activation, Windows foreground) lives in the runtime, except the one
  Windows P/Invoke in the tray host and `DnuiHost` (research R9).
