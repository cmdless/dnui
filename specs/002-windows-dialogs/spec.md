# Feature Specification: Windows and Built-in Dialogs

**Feature Branch**: `002-windows-dialogs`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description, refined in discussion (2026-11-11):
- One process = one app = one top-level thing.
- **Host commands** (`tray`, `window`, `dialog`) each start a new runtime process, may override the
  app, and can be invoked by anyone at any time. The library's convenience functions are host
  commands.
- **Window commands** run inside a window's process through `DnuiWindow.Ui`: child windows and
  parented dialogs. They can't override the app and have no tray.
- Windows are consumer-compiled (`window <assembly> <type>`, deriving from `DnuiWindow`) and
  one-shot: the caller only gets the resolved result. There's no window protocol or session.
- Only the built-in Avalonia dialogs (open file, open folder, save file) for now, with basic
  options passed down to Avalonia. The message box and other dialog kinds are later extensions.
- Convenience functions take window and app types as generics (for example
  `OpenWindowAsync<TWindow>()` and `OpenWindowAsync<TWindow, TApp>()`), translated to assembly
  and type arguments, and work the same on every path. A process can load as many window
  assemblies as it needs, but only one app. A different app means a host command (a new
  process).

## User Scenarios & Testing *(mandatory)*

The **caller** is a script or a .NET program (often a tray app from spec 001) that runs host
commands, directly or through the Dnui library. A **window author** writes an Avalonia window
(XAML plus code) deriving from the Dnui window base. End users interact with the windows and
dialogs.

### User Story 1 - Pick files, a folder, or a save location (Priority: P1)

A caller needs a path from the user: one or more files to open, a folder, or where to save. It
runs the matching dialog command (or the library function) with a title, filters, and a starting
folder, and gets back the chosen path or paths, or "cancelled".

**Why this priority**: it's the most common UI a background tool or tray app needs, and the
simplest end-to-end use of host commands.

**Independent Test**: run each dialog command in headless mode with a simulated choice and with a
simulated cancel, and check the single result line and exit code. On a desktop, run each one and
check the native dialog shows the title, filters, and start folder.

**Acceptance Scenarios**:

1. **Given** a terminal or a .NET caller, **When** it requests an open-file dialog with a title,
   multiple selection, two file-type filters, and a start folder, **Then** the platform's file
   dialog appears in front with those settings, and the result is the selected paths.
2. **Given** an open-folder request, **When** the user picks a folder, **Then** the result is that
   folder's path (several paths when multiple selection is allowed).
3. **Given** a save-file request with a suggested name, default extension, and file-type choices,
   **When** the user picks an existing file, **Then** they're asked to confirm overwriting (unless
   the caller turned that off), and the result is the chosen path.
4. **Given** any dialog, **When** the user cancels, **Then** the command still succeeds (exit 0)
   with a result that says nothing was chosen.
5. **Given** a platform where a dialog kind isn't available, **When** it's requested, **Then** the
   command fails with a distinct "unsupported" error, never a silent cancel.

---

### User Story 2 - Open a consumer window and get its result (Priority: P1)

A caller opens a window from its own assembly: a form, a settings page, or a viewer. It's a
separate top-level app (its own process, Dock or taskbar entry, optionally its own app type).
When the user finishes, the window resolves a result, and the caller gets it, or a "closed"
result if the user just closed it.

**Why this priority**: windows are the other half of this feature, and they're required before
window commands (Stories 3 and 4) can exist.

**Independent Test**: run the window command with a test window that resolves `{"name":"x"}`, and
check the result line. Run one that is closed without resolving, and check the null result. Run
with a bad type, and check the error.

**Acceptance Scenarios**:

1. **Given** an assembly path and window type name, **When** the caller runs the window command,
   **Then** the window appears in front using its own XAML and resources, styled by its app.
2. **Given** an open window, **When** it resolves a result, **Then** the window closes, and that
   result is the command's single result line.
3. **Given** an open window, **When** the user closes it without it resolving, **Then** the result
   is null, with exit code 0.
4. **Given** an unloadable assembly, a missing type, a type not deriving from the Dnui window base,
   or a Dnui version mismatch, **When** the window command runs, **Then** it fails with a distinct,
   documented code for each case, and no instance of the type is created.
5. **Given** an app override and a window type from the same assembly, **When** the window command
   runs, **Then** the assembly is loaded once, and the window uses that app's theme and resources.
6. **Given** a .NET caller awaiting a window, **When** it cancels, **Then** the window's process
   ends and the call reports cancellation.

---

### User Story 3 - Window code shows dialogs parented to its window (Priority: P2)

Code inside a window asks the user for a file, folder, or save location through its window
commands. The dialog belongs to that window: modal to it, and on macOS shown as a sheet where the
platform supports it. It uses the same options and results as the host dialog commands.

**Why this priority**: parented dialogs are the main reason windows get their own command surface.
It depends on Story 2.

**Independent Test**: a test window, in headless mode, requests an open-file dialog through its
window commands with a simulated choice, resolves with the result, and the window command's
result line contains the paths.

**Acceptance Scenarios**:

1. **Given** an open window, **When** its code requests a dialog through its window commands,
   **Then** the dialog is modal to that window, and the code receives the same result shape as the
   host command.
2. **Given** a dialog already open on a window, **When** its code requests another, **Then** the
   second waits until the first finishes.
3. **Given** a window closing while one of its dialogs is open, **When** the window closes, **Then**
   the dialog closes and the waiting code receives a cancelled result.
4. **Given** a component written against the shared dialog interface, **When** it's used from a
   .NET caller (host command) and from window code (window command), **Then** it behaves the same,
   apart from parenting.

---

### User Story 4 - Window code opens child windows (Priority: P2)

Code inside a window opens another window type (for example, an "Edit item" form) as a child: it
stays above its parent, closes with it, uses the same app, and returns its result to the parent's
code.

**Why this priority**: multi-window UIs need it, but a first version of most tools can use one
window plus dialogs. It depends on Story 2.

**Independent Test**: a test window opens a child window type that resolves a value. The parent
resolves with the child's value, and the window command's result line reflects it.

**Acceptance Scenarios**:

1. **Given** an open window, **When** its code opens a child window type, **Then** the child is
   owned by the parent (above it, closed with it) and runs in the same process and app.
2. **Given** an open child window, **When** it resolves or is closed, **Then** the parent's code
   receives the child's result or null.
3. **Given** a child window type from an assembly not yet loaded in the process, **When** window
   code opens it, **Then** the assembly is loaded into the same process, and the child uses the
   parent's app.
4. **Given** window code, **When** it looks for a way to start a tray or override the app through
   its window commands, **Then** none exists. It must use host commands, which start a new process.

---

### User Story 5 - Windows and dialogs are testable without a desktop (Priority: P3)

Automated tests run the window and dialog commands in CI with no display interaction: dialogs
take a supplied outcome instead of showing, and windows run without being displayed.

**Why this priority**: required to keep CI meaningful on all three OSes (constitution). It supports
the other stories rather than delivering user value on its own.

**Independent Test**: the contract test suite for this feature runs headless in CI on all three
OSes.

**Acceptance Scenarios**:

1. **Given** headless mode and a supplied outcome, **When** a dialog command runs, **Then** it
   resolves that outcome (after checking its shape) without showing any UI.
2. **Given** headless mode, **When** the window command runs a window that resolves on its own,
   **Then** the result line is produced with no display.
3. **Given** headless mode and a window whose code requests a parented dialog, **When** an outcome
   was supplied for it, **Then** the window code receives that outcome.

---

### Edge Cases

- **Start folder doesn't exist**: the dialog opens at the platform's default location. It isn't
  an error.
- **Filter syntax errors** (for example, a filter with no patterns): usage error before any UI.
- **Save dialog with no file-type choices**: any name is allowed. The default extension is added
  only if the user typed none.
- **Multiple selection off**: the result still carries a list (of one) for open-file and
  open-folder, so callers handle a single shape.
- **Window resolves twice**: only the first result counts.
- **Window constructor throws**: the window command fails with a "window failed to open" error
  result. Nothing is shown.
- **Unhandled exception in window code after opening**: the process ends with an error result and
  a non-zero exit code. Child windows and dialogs go with it.
- **Window opened with no foreground permission** (started from a background app): it still comes
  to the front, including on macOS.
- **macOS identity**: each window or dialog process shows its own Dock icon while open. An
  unbundled runtime shows a generic name. That's a documented limitation.
- **Dialog command given window-only options, or a window command given an app override**: the
  first is a usage error. The second can't happen, because window commands have no such option.
- **Executable copied without its `dnui/` folder** (single-file or native ahead-of-time
  publish): opening a consumer window or app fails before any process starts, with an error
  naming the missing `dnui/{assembly}.dll`. The built-in tray and dialogs still work.
- **Window and app types from the same assembly**: the assembly is loaded once.

## Requirements *(mandatory)*

### Functional Requirements

**Command surfaces**

- **FR-001**: The runtime MUST provide the host commands `dialog open-file`, `dialog open-folder`,
  `dialog save-file`, and `window <assembly> <type>`. Each runs as its own process, accepts the
  app override, and resolves exactly one JSON result line per the output contract.
- **FR-002**: The window base MUST provide window commands for code inside a window: the three
  dialogs, parented to that window, and opening a child window. Window commands MUST NOT accept
  an app override and MUST NOT include a tray.
- **FR-003**: The library MUST provide host-command convenience functions for the three dialogs,
  for opening a window, and for starting a tray, returning typed results. These are usable from
  any .NET code, including code inside a window, which then starts a new, unparented process.
- **FR-003a**: Opening a window MUST be expressible as a generic over the window type
  (`OpenWindowAsync<TWindow>()`), with overloads taking a `Type`, or an assembly plus type name.
  All overloads reduce to an assembly plus a type name, and MUST behave identically on both
  surfaces: as a host command (new process) and as a window command (a child in the same
  process, which uses already-loaded types directly).
- **FR-003b**: Host-command convenience functions MUST also accept the app as a generic (for
  example, `OpenWindowAsync<TWindow, TApp>()`, `StartTrayAsync<TApp>(...)`, and the dialog
  functions), translated to `--app-assembly`/`--app-type`. The app type MUST be a command app.
  Window commands MUST NOT offer an app generic.
- **FR-003c**: Host-command convenience functions MUST find the assembly file to pass to the
  runtime in this order: the assembly's own location; `{app folder}/{assembly name}.dll`;
  `{app folder}/dnui/{assembly name}.dll`. If none exists, they MUST fail before starting any
  process, with an error that names the expected file and how to fix it.
- **FR-003d**: Normal usage MUST just work in every publish mode, with no configuration. When a
  project referencing the Dnui package is published as a single file or with native
  ahead-of-time compilation, the package's build integration MUST automatically ship the
  project's IL assembly, its dependency manifest, and its private IL dependencies (including
  referenced projects) into a `dnui/` folder in the publish output. Assemblies the runtime
  provides (Dnui, Avalonia) are excluded. Normal publishes are unchanged. A documented property
  MUST let a project turn this off.
- **FR-004**: The library MUST define one dialog interface, implemented by the host-command
  functions and by window commands, with the same options and result shapes.

**Dialogs (built-in only)**

- **FR-005**: `open-file` MUST support: title, multiple selection, file-type filters (a name plus
  one or more patterns, repeatable), and a start folder.
- **FR-006**: `open-folder` MUST support: title, multiple selection, and a start folder.
- **FR-007**: `save-file` MUST support: title, suggested file name, default extension, file-type
  choices (repeatable), a start folder, and turning off the overwrite prompt (on by default).
- **FR-008**: Results MUST be: `open-file` and `open-folder` → `{"paths":[...]}`; `save-file` →
  `{"path":"..."}`. Cancelling MUST yield `{"paths":null}` or `{"path":null}` with exit code 0.
- **FR-009**: A dialog kind the platform can't show MUST fail with a distinct, documented
  "unsupported" exit code and error result.
- **FR-010**: Dialogs shown by host commands MUST appear in front of other apps, centered on the
  active screen.
- **FR-011**: Dialogs shown through window commands MUST be modal to their window (as a sheet on
  macOS where the platform supports it). A window MUST show its dialogs one at a time, in request
  order. A dialog MUST close with a cancelled result if its window closes.

**Windows**

- **FR-012**: The window command and window commands MUST load window types from their assemblies
  with the same rules as app types: dependency resolution, the Dnui version check, and a type
  check against the window base before any instance is created. A process MAY load any number of
  window assemblies; an already-loaded assembly (for example, the app's own) MUST be reused, not
  loaded again. A process has exactly one app.
- **FR-013**: Window-loading failures MUST have distinct, documented exit codes and error results
  (unloadable assembly, missing type, wrong base type), separate from the app-type codes. Version
  mismatch shares the existing code.
- **FR-014**: A window MUST resolve at most one result. The first resolve wins and closes the
  window. Closing without resolving MUST yield a null result with exit code 0.
- **FR-015**: Windows MUST use their own XAML and resources, styled by the process's app.
- **FR-016**: Child windows opened through window commands MUST be owned by their parent (kept
  above it, closed with it), run in the same process and app (loading the child's assembly if
  it isn't loaded yet), and deliver their result or null to the parent's code.
- **FR-017**: Windows started by host commands MUST appear in front, with their own taskbar or Dock
  entry.
- **FR-018**: Cancelling a library window or dialog call MUST end its process and report
  cancellation.

**Testing support (always available)**

- **FR-019**: `--headless` MUST be accepted by the dialog and window commands. In headless mode,
  windows run without a display, and dialogs don't show. They resolve an outcome supplied by the
  caller, which is checked against the dialog's result shape. Missing an outcome is a usage error.
- **FR-020**: In headless mode, window code that requests parented dialogs MUST receive outcomes
  supplied by the caller, in request order.

**Documentation and platforms**

- **FR-021**: Every option, result shape, and exit code MUST be documented in the public contract.
- **FR-022**: All of the above MUST work on Windows, macOS, and Linux desktops. Platform gaps
  (unsupported dialog kinds, macOS Dock naming for unbundled executables) MUST be documented.

### Key Entities

- **Host command**: a new runtime process (`tray`, `window`, or `dialog`) that may override the app
  and resolves one result (`tray` is a session, as in spec 001).
- **Window command**: an operation on a window's own `Ui` (a parented dialog or child window), run
  in-process and inheriting the app.
- **Dialog request**: a kind (open-file, open-folder, or save-file), its options (title, filters,
  start folder, multiple, suggested name, default extension, overwrite prompt), and a result
  (paths, path, or null).
- **Window**: a consumer type from an assembly, deriving from the Dnui window base, with one
  result and an optional owner (for child windows).
- **Dialog interface**: the shared operations, implemented by host commands and window commands.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer gets a file path from the user in one line of their own code, or one
  shell command, on all three OSes.
- **SC-002**: A developer shows their own XAML window and gets its result in one call
  (`OpenWindowAsync<MyWindow>()`), and the same call opens it as a child when made from window
  code.
- **SC-003**: Cancelling any dialog yields a null result with exit code 0 in 100% of test runs.
- **SC-004**: Every option documented for each dialog visibly takes effect in desktop verification
  on all three OSes, or its absence is documented as a platform gap.
- **SC-005**: A dialog shown through window commands is modal to its window in 100% of desktop
  verification runs.
- **SC-006**: Dialogs and windows started from a background caller (for example, a tray app)
  appear in front in 100% of desktop verification runs.
- **SC-007**: The same component, written against the dialog interface, passes the same tests from
  a .NET caller and from window code.
- **SC-008**: The full contract suite for this feature runs headless in CI on all three OSes.
- **SC-009**: A single-project app that opens its own window and app types through the generic
  functions behaves identically when published normally, as a single file, and with native
  ahead-of-time compilation, with no project changes.

## Assumptions

- Dialogs are Avalonia's storage pickers: native on Windows and macOS; the desktop portal, GTK, or
  Avalonia's managed fallback on Linux.
- Filter syntax on the command line is `Name:pattern,pattern` (for example `Images:*.png,*.jpg`).
  The exact option spelling is settled in the plan's CLI contract.
- One-shot commands follow spec 001's output contract. There is no new session in this feature, and
  `tray` is unchanged.
- Window commands may load additional window assemblies into the window's process. Only the
  app is fixed per process. A window that needs a different app is opened through a host
  command (a new process).
- Window and app code always runs in the runtime process, which loads IL. Under native
  ahead-of-time compilation, the consumer's executable has no IL. The automatic `dnui/` folder
  (FR-003d) supplies it, so one project works in every publish mode.
- In a single-project native AOT app, the UI types (and the Avalonia code they reference) are
  also compiled into the executable, though never run there. Expect a larger binary and possibly
  publish-time trimming warnings. Keeping windows and apps in a separate library opened by name
  is an optional size optimization, not a requirement.
- Avalonia and Dnui assemblies come from the runtime, not the consumer's `dnui/` folder.
  Lockstep versioning (constitution V) keeps them compatible.
- A source generator (build-time checks, typed names without a reference) is a later, optional
  extension, shipped inside the same package.
- Error and exit codes continue spec 001's scheme: new runtime exit codes stay below 64.
- Out of scope, for later extensions: message box and other dialog kinds, window-author protocols
  and window sessions, parenting across processes, shared named hosts, app single-instance, OS
  notifications, clipboard, and publishing.
