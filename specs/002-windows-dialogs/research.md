# Research: Windows and Built-in Dialogs

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-11

This builds on spec 001's decisions in [../001-tray-command/research.md](../001-tray-command/research.md).

## R1. Command surfaces in code

- **Decision**: three public interfaces in the library:
  - `IDnuiDialogs`, shared: `OpenFilesAsync`, `OpenFoldersAsync`, `SaveFileAsync`.
  - `IDnuiWindowCommands : IDnuiDialogs`: `OpenWindowAsync<TWindow>()`, `OpenWindowAsync(Type)`,
    and `OpenWindowAsync(Assembly, string)`. Implemented in-process by `DnuiWindow.Ui`.
  - `IDnuiHostCommands : IDnuiDialogs`: the same window overloads, plus app-generic variants
    (`OpenWindowAsync<TWindow, TApp>()`, `(Type window, Type? app)`, and
    `(Assembly, string, Assembly?, string?)`) and `StartTrayAsync(options)` /
    `StartTrayAsync<TApp>(options)`. Implemented by `DnuiHost`, which starts one runtime process
    per call.
- **Rationale**: the constitution 2.3.0 split, encoded in types. Components that only need dialogs
  depend on `IDnuiDialogs` and run unchanged on either surface (SC-007). Window commands
  physically have no app or tray members (FR-002).
- **Alternatives**: one interface with runtime checks ("app override not allowed here"). Rejected:
  the constitution says window commands MUST NOT offer them, and types enforce that at compile
  time.

## R2. Host-command plumbing: `DnuiHost` and one-shot execution

- **Decision**:
  - `public sealed class DnuiHost(DnuiHostOptions? options = null) : IDnuiHostCommands`, with a
    `static DnuiHost Default`. `DnuiHostOptions` holds `Runtime` (`DnuiRuntimeOptions`, spec 001),
    `Headless`, and the headless `Outcomes`.
  - Every one-shot host command goes through `public Task<DnuiResult> RunAsync(IReadOnlyList<string> args, CancellationToken)`
    (the library-client design saved earlier):
    1. Resolve the runtime.
    2. Start it with stdout redirected, stdin closed, and stderr inherited.
    3. Read stdout to the end, await exit, and return `DnuiResult(ExitCode, StdOut)`.
    4. On cancellation, kill the process tree and throw `OperationCanceledException`.
  - Typed functions parse the single result line. A non-zero exit becomes `DnuiCommandException`
    carrying the `DnuiResult` and the parsed `error.code` and `message`.
- **Rationale**: one execution path for every one-shot host command, matching the output contract.
  A non-zero exit stays data on `RunAsync`; typed functions throw, because a missing result can't
  be returned as `string[]?`.
- **Stdin**: redirected and closed immediately. One-shot commands don't read stdin, and an
  inherited console stdin could otherwise be grabbed by the child.

## R3. Assembly resolution for host commands (FR-003c)

- **Decision**: `AssemblyLocator.Find(Assembly)` checks, in order:
  1. `assembly.Location`, if not empty.
  2. `Path.Combine(AppContext.BaseDirectory, name + ".dll")`.
  3. `Path.Combine(AppContext.BaseDirectory, "dnui", name + ".dll")`.

  Otherwise it throws `DnuiAssemblyNotFoundException`, naming the third path and the fix. The type
  name is `Type.FullName`.
- **Rationale**: covers normal (1), the two-project layout (2), and single-file or AOT with
  automatic shipping (3). `AppContext.BaseDirectory` is the executable's folder in single-file and
  AOT apps.
- **Generic constraints**: `where TWindow : DnuiWindow` and `where TApp : DnuiCommandApp`.

## R4. Automatic IL shipping for single-file and AOT (FR-003d)

- **Decision**: `Dnui/build/Cmdless.Dnui.targets`, packed to `buildTransitive/`. Target
  `DnuiShipUiAssemblies` runs `AfterTargets="Publish"` when
  `'$(PublishAot)' == 'true' or '$(PublishSingleFile)' == 'true'` and
  `'$(DnuiShipUiAssemblies)' != 'false'`. It copies into `$(PublishDir)dnui/`:
  - The project's IL `.dll` from `obj/` (`$(IntermediateOutputPath)$(TargetName).dll`).
    `@(IntermediateAssembly)` can't be used: under native AOT it's retargeted to the native
    binary, as found during implementation.
  - `$(ProjectDepsFilePath)`: `MyApp.deps.json`, so the runtime's `AssemblyDependencyResolver` can
    resolve private dependencies.
  - `@(ReferenceCopyLocalPaths)` filtered to managed `.dll` files: NuGet and project-reference
    dependencies. It excludes `Avalonia*.dll`, `Dnui.dll`, `StreamJsonRpc*`, and anything the
    runtime ships, because the runtime's copies must win (lockstep). Satellite resource folders are
    copied with their relative paths.
- **Verification**:
  - A contract test publishes `tests/Dnui.SampleApp`, a single project with its own window and
    app, as single-file. It checks `dnui/Dnui.SampleApp.dll` and `.deps.json`, then runs the
    published executable, which opens its own window through `OpenWindowAsync<T>()` in headless
    mode.
  - An AOT publish needs a native toolchain, so it runs as a separate CI job on `ubuntu-latest`
    only, with the same assertions. The other OSes are checked by hand.
- **Alternatives**:
  - Have the runtime read IL from the single-file bundle. Rejected: it doesn't work for AOT, and
    it ties us to bundle internals.
  - A source generator. Not needed for shipping; deferred (spec Assumptions).

## R5. Loading window types (shared with app types)

- **Decision**: move spec 001's `AppTypeResolver` logic into the library as an internal
  `DnuiTypeLoader` (`Load(assemblyPath, typeName, requiredBase)`), shared by:
  - app resolution (runtime, `requiredBase = DnuiApp`)
  - the `window` host command (`requiredBase = DnuiWindow`)
  - window commands given an `(Assembly, string)` that isn't loaded yet

  Loading uses the default load context plus one `AssemblyDependencyResolver` per loaded file,
  with all of them chained on `Resolving`. If an assembly with the same name is already loaded,
  it's reused, and a different version is a mismatch. The Dnui reference version is checked
  before `GetType`.
- **Exit codes** (window): 14 `WindowAssemblyLoadFailed`, 15 `WindowTypeNotFound`,
  16 `WindowTypeInvalid`; 13 `DnuiVersionMismatch` is shared. Window commands surface the same
  failures as `DnuiCommandException` with the same `ErrorCode`, one exception type on both surfaces.
- **Rationale**: one loader, so apps and windows obey identical rules (FR-012).

## R6. The `window` host command and `DnuiWindow`

- **Decision**:
  - `public abstract class DnuiWindow : Window`:
    - `protected void Resolve(JsonNode? result)`: first call wins, then closes the window.
    - `public IDnuiWindowCommands Ui`: lazy, with `this` as the parent.
    - `internal Task<JsonNode?> Completion`: completes on resolve, or with `null` when closed.
  - `WindowCommand : DnuiCommand` (one-shot, not a session):
    - `Prepare` loads and checks the type (codes 14–16, 13) before anything is constructed.
    - `Run` sets `ShutdownMode.OnExplicitShutdown`, constructs the window on the UI thread (a
      constructor exception is exit 17 `WindowFailed`), shows it, brings it to the front (R9), and
      then `window.Completion.ContinueWith(r => app.ResolveInternal(new JsonObject { ["result"] = r }, 0))`.
  - Result line: `{"result": <window result or null>}`.
  - An unhandled exception in window code after opening → exit 17 with an error result. The
    runtime's existing unexpected-error path is extended to map exceptions from the UI thread
    while a window command runs.
- **Rationale**: wrapping the result in `{"result":…}` keeps the line an object, so `null` stays
  distinguishable from an error result, and it leaves room for later fields.

## R7. Window commands in-process (`DnuiWindow.Ui`)

- **Decision**: `DnuiWindowCommands(DnuiWindow owner)`:
  - **Child windows**: `OpenWindowAsync<T>()` does `new T()` directly. `(Assembly, string)` goes
    through `DnuiTypeLoader` if the type isn't loaded yet. Then `child.Show(owner)` (owned, not
    modal), and the method returns `child.Completion`. An owner closing closes its owned windows,
    so their completions resolve `null`.
  - **Dialogs**: `owner.StorageProvider.OpenFilePickerAsync(...)` and so on, which Avalonia shows
    modal to `owner` (a sheet on macOS).
  - **Queueing**: a per-window `SemaphoreSlim(1)` shows dialogs one at a time (FR-011).
  - **Owner closing during a dialog**: `owner.Closed` cancels waiting requests (they resolve
    `null`). An already-open native dialog closes with its owner on every platform, and Avalonia
    then returns an empty result, which maps to `null`.

## R8. Dialog option mapping (FR-005–FR-008)

- **Decision**: one internal `DialogRunner` used by both surfaces. It maps options onto Avalonia's
  `FilePickerOpenOptions`, `FolderPickerOpenOptions`, and `FilePickerSaveOptions`:
  - `Title` → `Title`.
  - `AllowMultiple` → `AllowMultiple`.
  - Filters → `FileTypeFilter` / `FileTypeChoices` as `FilePickerFileType(name) { Patterns }`.
  - Start folder → `SuggestedStartLocation`, via `StorageProvider.TryGetFolderFromPathAsync`.
    `null` when the folder is missing (falls back to the platform default; edge case).
  - Suggested name → `SuggestedFileName`.
  - Default extension → `DefaultExtension`.
  - Overwrite prompt → `ShowOverwritePrompt` (default true).

  Results come back as `IStorageItem.TryGetLocalPath()` values. Items without a local path (rare
  portal or virtual locations) are skipped. If nothing remains, the result is `null`, which is a
  cancel.
- **Unsupported** (FR-009): `CanOpen`, `CanSave`, or `CanPickFolder` false → exit 30
  `DialogUnsupported` (host), or `DnuiCommandException` with `ErrorCode = "DialogUnsupported"` (window
  commands).
- **CLI filter syntax**: `--filter "Images:*.png,*.jpg"`, repeatable. A missing name, `:`, or
  pattern is a usage error before any UI.

## R9. Unparented dialogs and coming to the front

- **Decision**: the `dialog` host command needs a `TopLevel` for `StorageProvider`. It creates an
  invisible owner window: `SystemDecorations.None`, `ShowInTaskbar = true` (so the process has a
  taskbar or Dock presence while the dialog is up), `Opacity = 0`, 1×1 pixels, centered on the
  active screen.
  - On macOS a picker parented to a window shows as a **sheet**, which would attach to the
    invisible window. So on macOS, the owner is created at a normal size (centered) and stays
    transparent, letting the sheet appear centered in the right place.
  - Real-desktop verification of this is an explicit quickstart step, and a task.
- **Foreground**: a process started by a background app may not be allowed to take focus.
  - **macOS**: the window and dialog commands call `NSApplication.activateIgnoringOtherApps:`
    through the Objective-C runtime (`objc_msgSend`), in `Dnui.Runtime/Platform/MacForeground.cs`,
    before showing.
  - **Windows**: the foreground lock is lifted by the *caller*. The tray runtime calls
    `AllowSetForegroundWindow(ASFW_ANY)` when the user clicks a tray item, while it still holds
    foreground rights from the click. `DnuiHost` calls it too before starting a process, which is
    harmless when not allowed. The started process then calls `Activate()` on its window.
  - **Linux**: `Activate()`, best-effort. Focus-stealing prevention is a documented limitation.
- **Rationale**: FR-010 and FR-017, and SC-006.

## R10. Headless mode and outcomes (FR-019, FR-020)

- **Decision**:
  - `--headless` on `window` and `dialog` uses Avalonia's headless platform
    (`Avalonia.Headless`, 12.1.5) in place of `UsePlatformDetect()`. Windows are created and shown
    with no display, and window code runs normally.
  - The runtime gains the `Avalonia.Headless` package, a small platform with no native code,
    justified by CI testability.
  - **Dialog outcomes**: `dialog … --headless --outcome '<json>'` resolves that JSON after
    checking its shape (`{"paths":[…]|null}` or `{"path":"…"|null}`) without showing anything. A
    missing `--outcome` in headless mode is a usage error.
  - **Window outcomes**: `window … --headless --dialog-outcome '<json>'`, repeatable, feeds
    parented dialogs in request order. Running out is a usage error surfaced to window code as
    `InvalidOperationException`, and its uncaught form becomes exit 17.
  - **Library**: `DnuiHostOptions.Headless` and `DnuiHostOptions.Outcomes`, a queue passed as
    `--outcome` / `--dialog-outcome`.
- **Rationale**: the same public, always-on testing approach as spec 001, extended to one-shot
  commands. With no session, outcomes are supplied up front.

## R11. Exit codes added

| Code | `error.code` | Meaning |
|---|---|---|
| 14 | `WindowAssemblyLoadFailed` | window assembly can't be loaded |
| 15 | `WindowTypeNotFound` | window type not in assembly |
| 16 | `WindowTypeInvalid` | type doesn't derive from `DnuiWindow` (or is abstract) |
| 17 | `WindowFailed` | window constructor or window code threw |
| 30 | `DialogUnsupported` | the platform can't show this dialog kind |

All codes stay below 64 (runtime range). Cancellation of a dialog or window is exit 0 with a
`null` result.

## R12. Testing strategy

- **Unit** (`tests/Dnui.Tests`):
  - option-to-Avalonia mapping
  - filter parsing
  - outcome shape validation
  - `AssemblyLocator` order (using temp folders and a fake `AppContext.BaseDirectory` seam)
  - `DnuiTypeLoader` rules
  - the IL-shipping item filter (exclusions)
- **Contract** (`tests/Dnui.Runtime.ContractTests`, headless):
  - every `dialog` subcommand with an outcome, a cancel, and usage errors
  - `window` with test windows: resolve, close-null, constructor-throw (17), bad type (14–16)
  - window commands: a child window result and parented dialogs with `--dialog-outcome`
  - `DnuiHost` typed functions, cancellation, and `DnuiCommandException`
  - the same `IDnuiDialogs`-based component run from the host and from window code (SC-007)
- **Publish** (`tests/Dnui.PublishTests`): single-file on all three runners, AOT on Linux only (R4).
- **Real desktop**: quickstart steps for modality, sheets, foreground and option rendering per OS.

## R13. Findings during implementation

- **Runtime-provided assemblies are excluded by package id and framework metadata.** A first
  version shipped the whole self-contained .NET framework and StreamJsonRpc's dependency closure
  into `dnui/`. The target now drops `ReferenceCopyLocalPaths` items with a
  `FrameworkReferenceName` and items whose `NuGetPackageId` matches the runtime-provided list.
  Those would never load anyway, because the runtime's own copies are on its trusted platform
  list. Verified: `dnui/` holds only the app's `.dll` and `.deps.json`, in both single-file and
  native AOT publishes on osx-arm64.
- **Avalonia 12 renamed `SystemDecorations` to `WindowDecorations`.** The invisible dialog owner
  uses `WindowDecorations.None`.
- **XAML windows must call the generated `InitializeComponent()`.** `AvaloniaXamlLoader.Load`
  doesn't set named-control fields.
- **`DnuiTrayOptions` became a `sealed record`,** so host commands can derive tray options with
  `with`. That's a spec 001 contract change, allowed because 0.1.0 is unreleased.
- **Missing positional arguments crashed the parser** (System.CommandLine's `GetValue` throws).
  The arguments are now read safely, and parsing itself is wrapped so bad input never crashes
  the runtime.
- **The app-type check gained a public parameterless constructor rule,** since the shared loader
  applies the same rule to apps and windows. The runtime always constructs apps with
  `Activator`, so this only moves a failure earlier.

