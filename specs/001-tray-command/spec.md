# Feature Specification: Tray Command and Protocol

**Feature Branch**: `001-tray-command`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "Tray command for Dnui.Runtime, with its stdio JSON-RPC protocol, plus
the minimal runtime/app-model foundation it needs." (Full input: an initialize request that can
also set the icon, tooltip, and items; set-items and set-icon requests; item-clicked and
icon-clicked notifications; shutdown when the stdio stream ends; the runtime's app-type options
and the two-tier app model. Out of scope: the daemon helper, runtime download, the window and
dialog commands, and per-RID packaging.) Later added: the .NET tray client is part of this
feature, and so are automatic runtime download and caching under `CmdlessRoot`, plus per-RID
runtime packaging, because the platform depends on them.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are developers whose program (the **caller**) launches the runtime
to show a system tray icon. People at the desktop are the end users who see and click the icon.

### User Story 1 - Show a tray icon and react to menu clicks (Priority: P1)

A caller launches the runtime with the tray command and connected standard streams. In one
initialize exchange it supplies an icon, a tooltip, and a menu. The tray icon appears with that
menu. When the end user picks a menu item, the caller is told which item was picked.

**Why this priority**: this is the whole point of the tray command. Without it nothing else in
this feature has value.

**Independent Test**: start the runtime from a test harness with the tray command, send
initialize with an icon and two items, check that the icon and menu are visible, click an
item, and confirm the harness receives a menu-opened notification and then an item-clicked
notification with that item's id.

**Acceptance Scenarios**:

1. **Given** the runtime was started with the tray command and redirected streams, **When** the
   caller sends initialize with a matching protocol version, an icon, a tooltip, and items,
   **Then** the response arrives only after the icon, tooltip, and menu are visible, and it
   contains the runtime version.
2. **Given** an initialized tray with a menu item whose id is `open`, **When** the end user
   selects that item, **Then** the caller receives exactly one item-clicked notification with
   id `open`.
3. **Given** a running tray, **When** the end user opens the tray menu (by whatever click the
   platform uses), **Then** the caller receives exactly one menu-opened notification.
4. **Given** a tray with an icon and no menu items, **When** the end user opens the tray menu,
   **Then** a single placeholder entry that can't be selected is shown, and the caller still
   receives a menu-opened notification.
5. **Given** the runtime was started with the tray command, **When** the caller sends
   initialize with a protocol version the runtime does not support, **Then** the caller gets
   an error response naming both versions, and no tray icon is shown.

---

### User Story 2 - Update the menu and icon while running (Priority: P2)

While the tray is running, the caller replaces the whole menu or changes the icon and tooltip
to reflect its own state. It knows when each change has taken effect, and is told when a
change is invalid.

**Why this priority**: most tray apps change their menu or icon as their state changes (for
example, connected or disconnected). A static tray covers only the simplest cases.

**Independent Test**: after initialize, send set-items with a new menu, confirm the response
arrives and the visible menu matches. Then send set-items with duplicate ids and confirm an
error response and an unchanged menu.

**Acceptance Scenarios**:

1. **Given** an initialized tray, **When** the caller sends set-items with a new item list,
   **Then** the menu shown afterwards contains exactly those items, and the response is sent
   only after the menu is updated.
2. **Given** an initialized tray, **When** the caller sends set-items containing two items with
   the same id (including across submenus), **Then** the caller gets an error response
   identifying the duplicate id, and the previous menu stays unchanged.
3. **Given** an initialized tray, **When** the caller sends set-icon with a new image and
   tooltip, **Then** both are shown before the response arrives.
4. **Given** an initialized tray, **When** the caller sends set-icon with an unreadable or
   invalid image, **Then** the caller gets an error response and the previous icon stays.
5. **Given** a menu with a submenu, **When** the end user selects an item inside the submenu,
   **Then** the caller receives an item-clicked notification with that inner item's id.

---

### User Story 3 - Tray goes away with its caller (Priority: P2)

When the caller closes its end of the connection or exits for any reason, including a crash,
the runtime removes the tray icon and exits. No orphaned tray icons are left behind.

**Why this priority**: an orphaned tray icon that points at a dead program is a visible defect
end users notice right away. It shares P2 with live updates because both are needed before any
real app ships.

**Independent Test**: start the runtime and initialize it, then kill the harness process and
confirm the runtime process exits and the icon disappears.

**Acceptance Scenarios**:

1. **Given** a running tray, **When** the caller closes the runtime's input stream, **Then**
   the runtime removes the icon and exits with a success code.
2. **Given** a running tray, **When** the caller process is terminated abruptly, **Then** the
   runtime removes the icon and exits.
3. **Given** the runtime was started with the tray command, **When** its standard input or
   output is not redirected (for example, it was launched from a desktop shortcut), **Then** it
   exits right away with a distinct, documented exit code and writes nothing to standard
   output.

---

### User Story 4 - Use a custom app type with the runtime (Priority: P3)

A developer who packages their own app tells the runtime to load their app type instead of the
built-in one. They can either keep the built-in commands (including tray) and customize the
default startup setup, or take over the whole app and use the runtime only as a cached UI host.
App types that don't follow the app model are rejected before anything starts.

**Why this priority**: the built-in app already covers the tray. Custom app types matter for
consumers packaging their own apps, but nothing earlier depends on them.

**Independent Test**: launch the runtime with options pointing at a test assembly and three
test types: a command-app subclass that changes the startup setup, a full-takeover app, and a
type outside the app model. Check that the first runs the tray command with the customization
applied, the second runs its own logic, and the third exits with the documented error.

**Acceptance Scenarios**:

1. **Given** no app options, **When** the runtime is launched with the tray command, **Then**
   it uses the built-in app and runs the tray command.
2. **Given** app options naming a command-app subclass, **When** the runtime is launched with
   the tray command, **Then** the subclass's startup customization is applied and the tray
   command runs normally.
3. **Given** app options naming a full-takeover app, **When** the runtime is launched, **Then**
   the runtime applies the app's startup customization and hands control to the app without
   parsing or running any built-in command.
4. **Given** app options naming a type that is not part of the app model, a type that does not
   exist, or an assembly that cannot be loaded, **When** the runtime is launched, **Then** no
   instance of the type is created, and the runtime exits with a distinct, documented exit code
   for each case. For the tray command, nothing is written to standard output. Without a
   session command, a single error result is written.

---

### User Story 5 - Drive the tray from .NET with the Dnui client (Priority: P2)

A .NET developer references the Dnui library and shows a tray by calling one start method with
an icon, tooltip, and items. They don't say where the runtime is: it's resolved and, if
needed, downloaded automatically (User Story 6). They get events when the menu opens or an
item is selected, update the menu and icon with awaitable calls, and end the tray by
disposing it. They never deal with processes, pipes, or JSON-RPC directly.

**Why this priority**: this is how .NET consumers will actually use the tray. It also drives
the runtime end to end, which makes it the main harness for the runtime's contract tests.
It's P2 because the protocol itself (User Stories 1–3) has to exist first.

**Independent Test**: from a test, start a tray through the client against a built runtime,
check that start completes, update the items, simulate an item selection, and confirm the
event fires with the right id. Dispose and confirm completion reports exit code 0 with no
standard output.

**Acceptance Scenarios**:

1. **Given** a path to the runtime executable, **When** the developer starts a tray with an
   icon, tooltip, and items, **Then** start completes only after the runtime has acknowledged
   initialize, and the tray is visible.
2. **Given** a started tray, **When** the end user opens the menu and selects item `open`,
   **Then** the client raises an opened event and then a clicked event with id `open`.
3. **Given** a started tray, **When** the developer updates the items with a duplicate id,
   **Then** the update call fails with an error carrying the protocol error code and details,
   and the tray keeps running.
4. **Given** a started tray, **When** the developer disposes it, **Then** the runtime exits,
   the icon disappears, and completion reports exit code 0 and no standard output.
5. **Given** a runtime that fails during startup (for example, no tray host, or a bad app
   type), **When** the developer starts a tray, **Then** start fails with an error that
   carries the runtime's exit code.
6. **Given** a started tray, **When** the runtime process dies unexpectedly, **Then**
   completion reports its non-zero exit code. It doesn't throw, and later update calls fail
   with a "tray not running" error.
7. **Given** a start in progress, **When** the developer's cancellation token fires, **Then**
   the runtime process is killed and start fails with a cancellation error.

---

### User Story 6 - The runtime is found or downloaded automatically (Priority: P1)

The first time any Dnui-based app on a machine needs the runtime, the library downloads the
exact matching runtime package for the current platform from the package feed and extracts it
into the shared Cmdless cache. Every later use, by any app, reuses the cached copy with no
network access. The developer never configures a path. They can optionally point at a
different Cmdless root, a different feed, or (for development) a specific runtime executable.

**Why this priority**: the rest of the platform depends on it. Without it, every consumer
would have to ship or locate the runtime themselves, which defeats the shared cached host
(constitution III).

**Independent Test**: with an empty Cmdless root and a feed containing a runtime package for
the current platform, resolve the runtime. Check that it's downloaded into
`{DnuiRoot}/packages/{id}/{version}/` and that the returned path runs. Resolve again with the
feed unreachable, and check that it succeeds from the cache.

**Acceptance Scenarios**:

1. **Given** an empty cache, **When** the runtime is resolved, **Then** the package for the
   current platform at exactly the library's runtime version is downloaded, verified, and
   extracted, and the path to an executable runtime is returned.
2. **Given** a complete cached copy, **When** the runtime is resolved with no network, **Then**
   it succeeds without any download.
3. **Given** several processes resolving the same runtime at once on an empty cache, **When**
   they all finish, **Then** the package was downloaded and extracted once, and every process
   gets the same working path.
4. **Given** a cache left half-extracted by a crash, or a corrupted download, **When** the
   runtime is resolved, **Then** the broken copy is never returned. It's replaced by a fresh,
   verified copy.
5. **Given** `CMDLESS_ROOT` is set, or a root is passed in code, **When** the runtime is
   resolved, **Then** the cache lives under that root. A root passed in code wins over the
   environment variable.
6. **Given** the current platform has no runtime package, or the package can't be found or
   downloaded, **When** the runtime is resolved, **Then** it fails with an error that says
   which case it is, including the package id and version.
7. **Given** a developer passes an explicit runtime executable path, **When** a tray is
   started, **Then** that executable is used and no resolution or download happens.

---

### Edge Cases

- **Requests before initialize:** any tray request sent before a successful initialize gets an
  error response saying initialize is required. A second initialize after a successful one is
  rejected with an error.
- **Stray output:** text printed to standard output by any code in the runtime process (the
  runtime, a custom app type, or a library) MUST NOT reach the real standard output. It goes
  to the error stream instead.
- **Resolve during a session:** if app code calls resolve with a non-null result while the tray
  session is running, the result is not written (it would corrupt the protocol). It's reported
  on the error stream, and the app still shuts down with the given exit code.
- **Resolve called twice:** only the first call counts. Later calls are ignored.
- **Stdio claimed twice:** the second session claim fails. If app code claimed stdio before the
  tray command (the app instance is created before the command is set), tray exits with the
  "already claimed" code.
- **Full-takeover app with its own protocol:** it opens a session, speaks whatever protocol it
  likes on the session streams, and ends with a resolve that has no result.
- **Requests before the UI is ready:** messages the caller sends before the UI is ready are
  not lost. They are handled in order once the UI is ready.
- **No icon set yet:** no tray icon is visible until an icon is supplied, through initialize or
  set-icon. Menu items supplied without an icon are kept and appear once an icon is set.
- **Empty item list:** an empty item list (from initialize or set-items, or never supplied)
  shows a single placeholder entry labeled "No items" instead of an empty menu. The menu then
  opens the same way on every platform. The placeholder is disabled. On a platform that can't
  disable it, a selection is reported with the reserved empty id `""`, which callers can never
  use for their own items.
- **Separators:** a separator item takes no text and never produces click notifications. Any
  text or state given on it is ignored.
- **Disabled items and submenu parents:** neither produces click notifications.
- **Checked items:** the runtime does not toggle check state itself. The caller owns item state
  and sends a new item list to change it.
- **Missing ids:** every non-separator item needs a non-empty id. Otherwise the request fails
  with an error that identifies the item.
- **Clicks on the icon itself:** these aren't reported. Only the menu opening is reported, so
  behavior is the same whether the platform opens the menu with a left or right click. A
  Windows left click that doesn't open the menu produces no notification.
- **Changing the menu that's opening:** the menu-opened notification arrives after the menu has
  started opening. Changes the caller makes in response are guaranteed to apply by the next
  time the menu opens. Whether they show up in the menu that's already open depends on the
  platform.
- **Malformed messages:** malformed protocol messages get a protocol-level error response where
  possible. They do not crash the runtime.
- **Caller stops reading:** if the caller stops reading notifications, the runtime does not
  lose them or reorder them. It waits until the caller reads again.

## Requirements *(mandatory)*

### Functional Requirements

**Runtime and app model (foundation)**

- **FR-001**: The runtime MUST accept two global options that name an app assembly and an app
  type. When they are omitted, it MUST use its built-in app.
- **FR-002**: The runtime MUST check that the resolved app type belongs to the app model (it
  derives from the Dnui base app) before creating any instance. Otherwise it MUST exit with a
  distinct, documented error code.
- **FR-003**: The runtime MUST use distinct, documented exit codes for: assembly not loadable,
  type not found, type not part of the app model, Dnui version mismatch, unknown or invalid
  command arguments, tray streams not redirected, and no tray host. When the requested command
  is not a session command, these failures MUST also write a single error result (FR-007c).
- **FR-004**: The Dnui library MUST provide a base app with an overridable startup-configuration
  step. The base step MUST apply the default setup (default font, trace logging, and developer
  tools in Debug builds only).
- **FR-005**: The Dnui library MUST provide a command app built on the base app. It runs the
  built-in command chosen by the runtime, and consumers cannot override how it starts up.
  The runtime's built-in app MUST be a command app.
- **FR-006**: For command apps, the runtime MUST parse the remaining arguments into a built-in
  command and give it to the app before startup configuration runs, so the command can affect
  startup (for example, a tray app hiding its macOS Dock icon).
- **FR-007**: The runtime MUST run startup configuration for every app, both command apps and
  full-takeover apps. Full-takeover apps receive no built-in command.
- **FR-007a** (output ownership): before any app code runs, the runtime MUST capture the raw
  standard output stream for the app and send all other standard-output writes in the process
  to the error stream. This applies to every app and command, not just tray.
- **FR-007a1** (stdio object): the base app MUST expose the captured stdio as an object that
  reports whether stdin and stdout are both redirected. It can open a session at most once,
  handing out the raw input and output streams. A second claim, or a claim when the streams
  aren't redirected, MUST fail with a distinct error. Built-in commands MUST use this same
  object. Full-takeover apps and command-app subclasses get the same access.
- **FR-007b** (resolve): the base app MUST provide a resolve operation that takes an optional
  JSON result and an exit code. It writes the result as a single JSON line to the captured
  standard output (nothing if the result is null), then shuts the app down with that exit
  code. The first call wins, later calls are ignored, and it can be called from any thread.
  While a session is open, a non-null result MUST NOT be written. It's reported on the error
  stream instead, and the exit code still applies.
- **FR-007c** (error results): an error result MUST have the shape
  `{"error":{"code":"<StableName>","message":"<text>"}}` and use a non-zero exit code.
- **FR-007d**: the error stream is diagnostics only and not part of the contract. The runtime
  MUST NOT depend on anyone reading it.

**Tray command: transport and lifecycle**

- **FR-008**: The tray command is a session command. It MUST open its session through the
  stdio object (FR-007a1) during the synchronous startup phase, and use the session's
  streams only for the protocol.
- **FR-009**: The tray command MUST end by resolving with no result (FR-007b). It MUST NOT
  write a non-null resolve result while the session runs. Tray startup failures (FR-012,
  FR-026) MUST be reported by exit code only.
- **FR-010**: The tray command MUST NOT start handling protocol messages until the UI framework
  is ready. Messages the caller sends earlier MUST be kept and handled in order.
- **FR-011**: The tray command MUST keep the runtime running with no windows open. When the
  protocol input stream ends, it MUST remove the icon and resolve with no result and exit
  code 0.
- **FR-012**: The tray command MUST exit with a distinct error code, before showing any UI, when
  its standard input or output is not redirected, or when stdio was already claimed as a
  session by app code (for example, a command-app subclass's constructor).
- **FR-013**: All UI changes requested through the protocol MUST be applied on the UI thread.
  Responses MUST be sent only after the change is visible.
- **FR-014**: The protocol stream MUST use newline-delimited JSON: each message is one JSON
  object on a single line, ending with a line feed. Messages MUST NOT contain raw line breaks.

**Tray protocol v1**

- **FR-015**: The runtime MUST support an `initialize` request. It carries a protocol version
  and optionally an icon, tooltip, and item list. It responds with the runtime version only
  after any supplied icon, tooltip, and items are shown.
- **FR-016**: `initialize` MUST fail with an error naming both versions when the protocol
  version is not supported. Initialize MUST be the first accepted request and MUST succeed at
  most once.
- **FR-017**: The runtime MUST support a `tray/setItems` request that replaces the entire menu
  with the given item list.
- **FR-018**: A menu item MUST support an id, display text, enabled state (default enabled),
  checked state (default unchecked), a separator flag, and an optional nested list of items
  that forms a submenu.
- **FR-019**: Item lists MUST be validated as a whole before anything is applied. Duplicate
  ids at any depth, and missing ids on items that need one, MUST cause an error response that
  identifies the offending item, and the previous menu MUST stay unchanged.
- **FR-020**: The runtime MUST support a `tray/setIcon` request that takes an icon (a file path
  or base64-encoded PNG) and an optional tooltip. If the tooltip is omitted, the current
  tooltip stays.
- **FR-021**: An icon that cannot be read or decoded MUST cause an error response, and the
  previous icon MUST stay.
- **FR-022**: The runtime MUST send a `tray/clicked` notification with the item's id when the
  end user selects an enabled, non-separator, non-submenu item.
- **FR-023**: The runtime MUST send a `tray/opened` notification each time the tray menu opens,
  on every supported platform. Clicks on the icon that don't open the menu MUST NOT be
  reported.
- **FR-023a**: When the item list is empty, the runtime MUST show one placeholder item labeled
  "No items" with the reserved id `""`, disabled where the platform supports disabled items.
  Callers MUST NOT use `""` as an item id (it already fails FR-019's non-empty id rule).
- **FR-024**: The protocol MUST NOT include a shutdown method. The input stream ending is the
  only shutdown signal.
- **FR-025**: The protocol (methods, parameters, results, errors, and the protocol version) and
  the exit codes MUST be documented as part of the public contract.

**Tray client (Dnui library)**

- **FR-027**: The library MUST provide a tray client that starts the runtime with the tray
  command and optional app assembly and type. It MUST obtain the runtime through runtime
  resolution (FR-034–FR-043) unless an explicit runtime path override is given. It redirects
  standard input and output, and MUST NOT redirect the error stream.
- **FR-028**: Starting MUST send initialize with the caller's icon, tooltip, and items, and
  complete only after a successful response. If the connection ends first, start MUST fail
  with an error that carries the runtime's exit code (and the standard output, if any).
- **FR-029**: The client MUST raise an opened event for each `tray/opened` and a clicked event,
  with the id, for each `tray/clicked`. Events MUST be raised in the order received, and an
  exception in a handler MUST NOT break the connection.
- **FR-030**: The client MUST provide awaitable item and icon updates. Protocol errors MUST
  surface as an error that carries the protocol error code and data.
- **FR-031**: The client MUST expose a completion result made of the exit code and the standard
  output (null for tray). A non-zero exit code is a result, not an error.
- **FR-032**: Disposing the client MUST close the runtime's standard input and wait for it to
  exit. If it hasn't exited within a bounded time (default 5 seconds), it MUST be killed.
- **FR-033**: Cancelling during start MUST kill the runtime process and fail start with a
  cancellation error. After the tray has stopped running, update calls MUST fail with a
  "not running" error.

**Runtime acquisition (Dnui library)**

- **FR-034**: The library MUST resolve the runtime automatically. The developer MUST NOT be
  required to supply a runtime path or root.
- **FR-035**: Roots: `CmdlessRoot` is `~/.cmdless` unless the `CMDLESS_ROOT` environment
  variable is set, or a root is passed in code (code wins). `DnuiRoot` is
  `{CmdlessRoot}/dotnet`.
- **FR-036**: The runtime package MUST be `Cmdless.Dnui.Runtime.<rid>` for the current
  platform's RID, at exactly the library's embedded runtime version. It MUST never fall back
  to another version.
- **FR-037**: Packages MUST be extracted to `{DnuiRoot}/packages/{id}/{version}/`, with the full
  package id lowercased. A cached copy is used only if it's marked complete.
- **FR-038**: Resolution MUST work offline when a complete cached copy exists.
- **FR-039**: Downloading and extracting MUST be serialized across processes with a lock in
  `{DnuiRoot}/locks/`. Extraction MUST happen in a temporary location and become visible
  atomically, so no process ever sees a partial copy.
- **FR-040**: A downloaded package MUST be verified before use. At minimum, every archive
  entry's checksum is checked, and the package's own manifest must name the expected id and
  version. A failed check MUST discard the download and fail with an integrity error.
- **FR-041**: On macOS and Linux, the runtime executable MUST be marked executable after
  extraction.
- **FR-042**: The package feed defaults to nuget.org. It MUST be overridable in code by a
  NuGet v3 feed URL or by a local folder of `.nupkg` files.
- **FR-043**: Failures MUST be distinguishable: unsupported platform, package not found on the
  feed, download failure, integrity failure, and lock timeout. Each MUST name the package id
  and version.
- **FR-044**: The resolution API MUST be public, so other library features and consumers can
  get the runtime path without starting a tray.

**Runtime packaging**

- **FR-045**: The build MUST be able to produce one self-contained runtime package per
  supported RID (`win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`),
  named `Cmdless.Dnui.Runtime.<rid>`, at version `DnuiRuntimeVersion`, with the runtime
  executable at a fixed path inside the package.
- **FR-046**: Publishing the packages to nuget.org is a manual release step, not part of this
  feature. Packing them, and testing against a local folder feed, is part of it.

**Platforms**

- **FR-026**: All of the above MUST work on Windows, macOS, and Linux desktops that have a
  system tray. If a Linux environment has no tray host, the runtime MUST report it with a
  distinct, documented error.

### Key Entities

- **Caller**: the program that launches the runtime and speaks the protocol over its standard
  streams. It owns all tray state.
- **Resolved result**: the single JSON line a one-shot app writes to standard output, with its
  exit code. Sessions resolve with no result.
- **Runtime package**: `Cmdless.Dnui.Runtime.<rid>`. It holds a self-contained runtime for one
  platform, versioned in lockstep with the library.
- **Cmdless root / Dnui root**: the per-user folders that hold cached packages and locks, shared
  by every Cmdless-based app on the machine.
- **Tray client**: the library object that owns one runtime process and its tray session. It
  exposes events, updates, completion, and disposal.
- **Run result**: the exit code plus standard output (null when nothing was resolved). It's the
  same shape for every command, so later one-shot commands return it too.
- **Stdio / session**: the captured standard streams, owned by the app. They can be claimed
  once as a session, which turns the app from one-shot into a session.
- **App type**: the application the runtime hosts. Either a command app (runs built-in commands,
  including the built-in app) or a full-takeover app (runs its own logic).
- **Tray command**: the built-in command that shows one tray icon controlled over the protocol.
- **Tray icon state**: the current icon image, tooltip, and menu. Changed only by caller
  requests.
- **Menu item**: id, text, enabled, checked, separator flag, optional child items.
- **Protocol version**: a single version number that both sides must agree on during
  initialize.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can show a tray icon with a working menu by launching the runtime and
  sending a single initialize message, with no other setup steps.
- **SC-002**: The tray icon and menu are visible within 2 seconds of the runtime process
  starting, on a machine where the runtime is already cached.
- **SC-003**: After any successful set-items or set-icon response, the visible tray matches the
  request in 100% of automated test runs on all three operating systems.
- **SC-004**: The runtime exits and removes the icon within 2 seconds after the caller exits or
  closes the stream, in 100% of test runs, including abrupt termination.
- **SC-005**: Over a test run of 10,000 protocol messages mixed with deliberate stray output
  from the hosted app, there are zero protocol corruption errors.
- **SC-006**: Each documented failure case (bad assembly, bad type, type outside the app model,
  version mismatch, bad arguments, streams not redirected, no tray host, unsupported protocol
  version) produces its documented exit code, and its error result or protocol error, in 100%
  of test runs. Tray failures write nothing to standard output.
- **SC-007**: Every menu opening and every item selection produces exactly one notification.
  None are lost or duplicated across 1,000 automated interactions.
- **SC-008**: A .NET developer can show a working tray with a menu and handle clicks in under
  15 lines of their own code using the client.
- **SC-009**: Disposing the client leaves no runtime process running in 100% of test runs,
  including when the runtime ignores end-of-file (it's killed after the timeout).
- **SC-010**: A developer can go from referencing the library to a visible tray on a machine
  with an empty cache with no setup steps beyond their own code (one download, then cached).
- **SC-011**: With a cached runtime, resolution takes under 100 ms and makes no network
  requests.
- **SC-012**: With 10 processes resolving at once on an empty cache, the package is downloaded
  exactly once and all 10 get a working runtime, in 100% of test runs.

## Assumptions

- The protocol is JSON-RPC 2.0 with newline-delimited framing (FR-014), so callers in any
  language, including shell scripts, can write and read it without a framing parser.
- The protocol version is a single integer (starting at `1`) that must match exactly. Additive
  protocol changes within one runtime version don't change it.
- Icons are PNG images. Platform-specific formats (for example, macOS template images) are
  out of scope for v1.
- Text output written by hosted app code is diagnostic only, so sending it to the error stream
  is acceptable. Callers aren't expected to read the error stream.
- Linux support targets desktop environments that provide a system tray. Others get the
  documented error (FR-026).
- Until the runtime packages are published to nuget.org, real use needs a local folder feed
  (built by the packaging step) or the explicit runtime path override. Both are supported for
  development and tests.
- Private or authenticated feeds, package signature verification, and cache cleanup of old
  versions are not in this feature.
- Starting with the tray command is deliberate. It's the hardest command (a long-lived
  session over stdio), so it builds the shared foundation (app model, app resolution, stdio
  ownership, `Resolve`, exit codes, run result) that the window and dialog commands will reuse.
- Out of scope: the daemon/single-instance helper, `RunAsync` for one-shot commands, the
  window and dialog commands, and publishing packages to nuget.org.
