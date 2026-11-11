# Feature Specification: Host Session — Windows, Dialogs, and Trays as Hosted Objects

**Feature Branch**: `002-host-session`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description: "Host session: one private runtime process per client hosting trays,
windows, and dialogs as objects, with the same UI operations available remotely (from the caller)
and locally (inside a DnuiWindow)." (Full input: host-level initialize; per-object ids and
results; tray becomes a hosted object; consumer-compiled windows loaded by assembly and type,
deriving from DnuiWindow, with per-window resolve and caller-defined protocol; one UI-operations
interface with remote and local implementations covering windows, file/folder/save pickers, and a
message box; one-shot `dialog` and `window` CLI commands for scripts; headless testing of windows
and dialogs. Out of scope: shared named hosts, cross-process connection, app single-instance,
OS notifications, clipboard, web views, publishing.)

## User Scenarios & Testing *(mandatory)*

The **caller** is a developer's .NET program using the Dnui library. A **host** is one private
runtime process started by that caller; it shows the caller's trays, windows, and dialogs. A
**window author** is a developer who writes an Avalonia window (XAML and code) that the host
shows. End users see and interact with the UI.

### User Story 1 - Open a consumer window and get its result (Priority: P1)

A caller starts a host and opens a window defined in its own assembly (XAML, code-behind,
resources). The window behaves like any Avalonia window. When the user finishes (for example,
submits a form), the window resolves a result, and the caller receives it.

**Why this priority**: windows are the foundation the rest of this feature builds on (dialogs
parent to them, protocols attach to them). Without them, Dnui only offers a tray.

**Independent Test**: start a host, open a test window type from a test assembly, have the
window resolve `{"name":"x"}`, and confirm the caller's window completion returns that result.
Open it again and close it without resolving, and confirm the result is null.

**Acceptance Scenarios**:

1. **Given** a running host, **When** the caller opens a window by assembly path and type name,
   **Then** the window appears using its own XAML and resources, styled by the app's theme, and
   the call returns a window handle once it is shown.
2. **Given** an open window, **When** the window resolves a result, **Then** the caller's
   completion for that window returns exactly that result, and the window closes.
3. **Given** an open window, **When** the user closes it without the window resolving, **Then**
   the caller's completion returns a null result.
4. **Given** a type that doesn't derive from the Dnui window base, a missing type, or an
   unloadable assembly, **When** the caller opens it, **Then** the call fails with a distinct
   error for each case, and no instance of the type is created.
5. **Given** a window type in the same assembly as the host's app type, **When** it is opened,
   **Then** the assembly is reused, not loaded a second time.

---

### User Story 2 - Show dialogs from the caller (Priority: P1)

The caller shows an open-file, open-folder, save-file, or message dialog. It can parent the
dialog to one of its windows (it then behaves as that window's modal dialog) or show it with no
parent (for example, in response to a tray click). Cancelling is a normal result.

**Why this priority**: file pickers and message boxes are the most common UI that background
tools and tray apps need, and parenting is the reason the host exists.

**Independent Test**: in headless mode, request each dialog kind, simulate a choice and a
cancellation, and confirm the results (`paths`, `path`, `button`, or null). In a desktop session,
open a file dialog parented to a window, and confirm it is modal to that window.

**Acceptance Scenarios**:

1. **Given** a running host, **When** the caller requests an open-file dialog allowing multiple
   files with a file-type filter, **Then** the native dialog appears, and the caller receives the
   selected paths, or null if cancelled.
2. **Given** an open window, **When** the caller requests a dialog parented to that window,
   **Then** the dialog is modal to that window (on macOS, attached to it as a sheet).
3. **Given** a host with only a tray (no windows, no Dock icon on macOS), **When** the caller
   shows a dialog with no parent, **Then** the dialog appears in front of other apps.
4. **Given** a running host, **When** the caller shows a message box with a title, message, and
   buttons `ok`/`cancel`, **Then** the caller receives the clicked button's id, or null if the
   box is dismissed without a button.
5. **Given** a save dialog with a suggested name, default extension, and overwrite prompt,
   **When** the user picks an existing file, **Then** they are asked to confirm overwriting
   before the path is returned.

---

### User Story 3 - Talk to a window through its own protocol (Priority: P2)

A window author defines methods and events specific to their window (for example, "load
document", "progress changed"). The caller invokes those methods on that window and receives
its events, either by name or through a typed interface, without touching the built-in protocol.

**Why this priority**: it turns windows from fire-and-forget forms into live, app-specific UI,
which is what makes custom windows worth building. It depends on User Story 1.

**Independent Test**: a test window exposes `setTitle(text)` and raises `clicked(count)`. The
caller calls `setTitle` (window updates), triggers the event in headless mode, and receives it.

**Acceptance Scenarios**:

1. **Given** an open window that exposes a method, **When** the caller invokes it with
   arguments, **Then** the window's code runs and the caller receives its return value.
2. **Given** an open window, **When** the window sends a notification, **Then** the caller's
   handler for that window receives it, in order with that window's other notifications.
3. **Given** a window method that throws, **When** the caller invokes it, **Then** the caller gets
   an error carrying the window's error code and message, and the window stays open.
4. **Given** a window author who names a method with a reserved prefix, **When** the window
   registers it, **Then** registration fails with a clear error.
5. **Given** two open windows of the same type, **When** the caller invokes a method on one,
   **Then** only that window receives it.

---

### User Story 4 - Trays as hosted objects, with dialogs from tray clicks (Priority: P2)

Trays from spec 001 become objects in the host. A caller can create a tray and, when an item is
clicked, open dialogs or windows in the same host. The simple "just a tray" usage keeps working.

**Why this priority**: tray apps are the first real consumers, and "click the tray, pick a file"
is the motivating case for one host. It reuses spec 001 behavior, so it is mostly integration.

**Independent Test**: start a host, create a tray, simulate a click, open a message box from the
click handler, simulate its button, and confirm the result. Separately, confirm the one-call tray
shortcut and the standalone `tray` command still pass spec 001's contract tests (updated for the
reshaped protocol).

**Acceptance Scenarios**:

1. **Given** a running host, **When** the caller creates a tray, **Then** it behaves as in spec
   001 (icon, items, opened/clicked events, updates, getState, simulation), scoped to that tray.
2. **Given** a tray click, **When** the caller's handler opens a dialog or window, **Then** it
   appears from the same host process, with no new runtime started.
3. **Given** a caller that only wants a tray, **When** it uses the one-call tray shortcut,
   **Then** it gets a working tray without dealing with the host explicitly.
4. **Given** a host with more than one tray, **When** a tray is clicked, **Then** the event
   identifies which tray.

---

### User Story 5 - Window code uses the same UI operations locally (Priority: P2)

Code inside a window opens child windows and dialogs through the same UI-operations interface the
caller uses. Those operations run in the same process, parented to that window, with no protocol
round trip. Code written against the interface works in both places.

**Why this priority**: without it, window code would either start a new runtime (losing the
parent) or need a different API. It depends on User Stories 1 and 2.

**Independent Test**: a test window, when its method is invoked, opens a message box through its
local UI interface, and returns the clicked button. In headless mode the message box is simulated
and the method returns the simulated button.

**Acceptance Scenarios**:

1. **Given** an open window, **When** its code opens a dialog through its local UI interface,
   **Then** the dialog is parented to that window and modal to it.
2. **Given** an open window, **When** its code opens a child window, **Then** the child is owned
   by the window (stays above it, closes with it) and reports its result back to the window code.
3. **Given** a component written against the UI interface, **When** it is used once from the
   caller and once from window code, **Then** it behaves the same apart from parenting.

---

### User Story 6 - Shell scripts show a dialog or window with one command (Priority: P3)

A script runs the runtime once to show a dialog or window and reads the single JSON result line
from standard output, as in spec 001's output contract, without any session.

**Why this priority**: it keeps Dnui usable from any language and from shell scripts, but .NET
callers will use the host, so it comes last.

**Independent Test**: run the runtime with `dialog message` and each picker kind in headless
mode with a simulated choice, and with `window <assembly> <type>`, and check the single result
line and exit code.

**Acceptance Scenarios**:

1. **Given** a terminal, **When** a script runs a dialog command, **Then** the dialog appears and
   the runtime writes one JSON result line and exits 0, including when the user cancels (null
   result).
2. **Given** a terminal, **When** a script runs the window command with an assembly and type,
   **Then** the window appears, and its resolved result (or null when closed) is the single
   result line.
3. **Given** an invalid window type or assembly, **When** the window command runs, **Then** it
   exits with the documented code and a single error result line.

---

### User Story 7 - Windows and dialogs are testable without a desktop (Priority: P3)

Automated tests drive windows and dialogs in headless mode: inspect a window's state, simulate a
dialog's choice or cancellation, and simulate a message box button, using the same public
protocol as everything else.

**Why this priority**: required to keep CI meaningful on all three OSes (constitution), but it
supports the other stories rather than delivering user value on its own.

**Independent Test**: the whole contract test suite for this feature runs headless in CI.

**Acceptance Scenarios**:

1. **Given** a headless host, **When** a dialog is requested, **Then** it doesn't block on a real
   dialog; the caller can supply its outcome through the simulation methods.
2. **Given** a headless host with an open window, **When** the caller asks for the window's
   state, **Then** it gets the window's id, title, open/closed state, and owner.

---

### Edge Cases

- **Disposing the client** closes every tray, window, and dialog. Pending window and dialog
  completions return null results, and pending calls fail with a "host ended" error.
- **Closing a window with open child windows or dialogs** closes them first. Their completions
  return null.
- **Invoking a closed window**: calls to a window's methods after it closed fail with a "window
  closed" error.
- **Parent window closes while a dialog is pending**: the dialog closes and resolves null.
- **Dialog requested for a closed or unknown window id**: fails with an "unknown object" error.
- **Many dialogs at once**: several unparented dialogs may be open at the same time. Dialogs
  parented to the same window are queued, one at a time (a window can show one modal at a time).
- **Window resolves twice**: only the first result counts, as with app resolve.
- **Window author throws in a constructor**: opening fails with a "window failed to open" error
  carrying the message. The host keeps running.
- **Unhandled exception in window code after opening**: the window is closed, its completion
  returns a "window failed" error result, and the host keeps running. Other windows are
  unaffected.
- **Caller-defined notification storm**: notifications are delivered in order per window, and a
  slow caller doesn't block the UI thread (same rule as tray notifications).
- **Picker unsupported** (for example, no folder picker on some Linux setups): the request fails
  with an "unsupported" error, distinct from cancellation.
- **macOS tray-only host shows a window**: the app activates first, so the window is in front.
- **Host process crashes**: the caller's client completion reports the exit code, and every
  pending window and dialog completion fails with "host ended".

## Requirements *(mandatory)*

### Functional Requirements

**Host session**

- **FR-001**: The library MUST provide a client that starts one private host process (runtime
  resolved as in spec 001) and talks to it over a session on standard streams. Disposing the
  client MUST end the host and close all its UI.
- **FR-002**: A host-level initialize MUST perform the protocol-version handshake, return the
  runtime version, and accept the headless option. Every other request before it MUST fail with
  a not-initialized error.
- **FR-003**: The protocol MUST NOT depend on the transport, so a later feature can carry it
  over named pipes or sockets unchanged.
- **FR-004**: Every tray, window, and dialog MUST have a host-unique id. Each has its own
  completion and result, independent of the host's lifetime.
- **FR-005**: Built-in protocol methods MUST use reserved prefixes (`dnui/`, `host/`, `tray/`,
  `window/`, `dialog/`). Methods defined by window authors MUST NOT use them.

**Trays (reshaped from spec 001)**

- **FR-006**: Trays MUST be created and addressed by id within a host, keeping every spec 001
  tray behavior (items, icon, opened/clicked, getState, simulation, placeholder, validation and
  error codes), with tray events identifying their tray.
- **FR-007**: The library MUST keep a one-call tray shortcut (a host with one tray), and the
  standalone `tray` command MUST keep working as a host that starts with one tray.

**Windows**

- **FR-008**: The host MUST open windows given an assembly path and a type name. The type MUST be
  checked to derive from the Dnui window base before any instance is created, with distinct
  errors for unloadable assembly, missing type, wrong base type, and Dnui version mismatch.
- **FR-009**: An assembly already loaded in the host (for example, the app assembly) MUST be
  reused, not loaded again. Windows' own XAML and resources MUST load normally, and the app's
  theme and resources MUST apply.
- **FR-010**: A window MUST be able to resolve one result. The first resolve wins. Closing
  without resolving MUST yield a null result.
- **FR-011**: Opening a window MUST return once it is shown. The caller MUST be able to close a
  window, and to optionally set an owner window when opening it.
- **FR-012**: A window author MUST be able to register their own methods for that window, and
  to send notifications to the caller. The caller MUST be able to invoke them by name or through
  a typed interface, and to subscribe to that window's notifications. Calls MUST reach only the
  addressed window.
- **FR-013**: Errors from a window's own methods MUST reach the caller with the window's error
  code and message, without closing the window.

**UI operations**

- **FR-014**: The library MUST define one UI-operations interface with: open window, open-file
  picker (single or multiple, file-type filters, title, suggested start folder), open-folder
  picker (single or multiple), save-file picker (suggested name, default extension, file-type
  choices, overwrite prompt), and message box (title, message, buttons with ids, optional
  default and cancel buttons).
- **FR-015**: The caller MUST have a remote implementation of the interface: unparented on the
  client, and parented to a window when obtained from that window.
- **FR-016**: Window code MUST have a local implementation of the interface that runs in the
  host process with that window as the parent (modal; a sheet on macOS where the dialog kind
  supports it), with no protocol round trip.
- **FR-017**: Cancelling any dialog MUST be a null result, not an error. Unsupported dialog kinds
  MUST fail with a distinct "unsupported" error.
- **FR-018**: The message box MUST be a built-in Dnui window that returns the clicked button's
  id, or null when dismissed.
- **FR-019**: Dialogs and windows shown with no parent MUST appear in front of other apps,
  including from a tray-only host on macOS.
- **FR-020**: Dialogs parented to the same window MUST be shown one at a time, in request order.

**Shell commands**

- **FR-021**: The runtime MUST provide one-shot `dialog` commands (`open-file`, `open-folder`,
  `save-file`, `message`) and a one-shot `window <assembly> <type>` command. Each resolves
  exactly one JSON line per the output contract, with cancellation and close resolving null with
  exit 0.
- **FR-022**: The one-shot commands MUST reuse the same dialog and window behavior as the host,
  and use the documented exit codes for resolution failures.

**Testing support (always available, as in spec 001)**

- **FR-023**: In headless mode, dialogs MUST NOT block on real UI. Simulation methods MUST let the
  caller choose a dialog's outcome (paths, path, button, or cancel). Windows MUST be inspectable
  (id, type, title, open state, owner) and closable by request.
- **FR-024**: Every protocol method, error code, and the one-shot commands' options and exit
  codes MUST be documented in the public contract.

**Platforms**

- **FR-025**: All of the above MUST work on Windows, macOS, and Linux desktops. Platform gaps
  (for example, a missing folder picker on a Linux desktop) MUST be documented and reported as
  "unsupported" errors.

### Key Entities

- **Client**: the caller-side object that owns one host process. It exposes UI operations,
  tray creation, and window opening.
- **Host**: the private runtime process. It holds trays, windows, and dialogs by id.
- **Tray**: as in spec 001, now addressed by id within a host.
- **Window**: a consumer-compiled window (assembly plus type) deriving from the Dnui window base.
  It has an id, an optional owner, one result, and optional author-defined methods and
  notifications.
- **Dialog**: an open-file, open-folder, save-file, or message request with an optional parent
  window, a result (paths, path, button id, or null), and an id while pending.
- **UI operations interface**: the common set of operations, with a remote implementation (client
  and window clients) and a local one (window code).
- **Reserved prefixes**: method-name prefixes owned by Dnui's built-in protocol.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can open their own XAML window from a .NET app and get its result back
  in under 10 lines of their own code, with no setup beyond referencing the library.
- **SC-002**: Opening a dialog or window in a running host takes under 500 ms until it is shown,
  with no new process started.
- **SC-003**: A dialog parented to a window is modal to that window in 100% of desktop
  verification runs on all three OSes (as a sheet on macOS for supported dialog kinds).
- **SC-004**: Cancelling any dialog kind yields a null result, never an error, in 100% of test
  runs.
- **SC-005**: Window-author methods and notifications for 10 simultaneously open windows are
  delivered only to the addressed window, in order, across 1,000 calls with no cross-talk.
- **SC-006**: Disposing the client leaves no host process and no open UI in 100% of test runs,
  and every pending completion finishes.
- **SC-007**: The same component, written against the UI-operations interface, passes the same
  behavioral tests when used from the caller and from window code.
- **SC-008**: The full contract suite for this feature runs headless on all three OSes in CI.
- **SC-009**: Every spec 001 tray scenario still passes (adjusted for tray ids), using both the
  hosted tray and the one-call shortcut.

## Assumptions

- The protocol stays newline-delimited JSON-RPC 2.0 over standard streams, with application
  error codes in the 1001–1099 range (spec 001 uses 1001–1012; this feature continues from
  1020).
- Version 0.1.0 is unreleased, so the spec 001 tray protocol is reshaped (tray ids, host-level
  initialize) without a compatibility layer. The protocol version stays at 1.
- Window results are JSON values. Windows that need richer exchange use their own methods and
  notifications.
- Dialogs inside a host use Avalonia's storage pickers. The message box is Dnui's own window, so
  it looks the same on every OS.
- Native modality across processes (parenting to a non-Dnui window from another process) is out
  of scope. Parenting is in-process only.
- A crash in window code closes that window, not the host. A crash of the host process ends
  everything.
- Out of scope: shared named hosts and connecting from other processes, app single-instance and
  activation forwarding (later spec), OS notifications, clipboard, web views, and publishing.
