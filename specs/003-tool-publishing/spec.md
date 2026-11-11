# Feature Specification: Dnui Tool, API Cleanup and nuget.org Publishing

**Feature Branch**: `003-tool-publishing`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description, refined in discussion (2026-11-11):
- A new command-line tool, `Dnui.Tool`, published as the .NET tool package `Cmdless.Dnui.Tool`
  with the command name `dnui`. It's a thin forwarder: it passes its arguments unchanged to the
  lockstep runtime, resolved by the Dnui library into the shared CmdlessRoot cache. The tool has
  no commands of its own and ships no runtime.
- Tagged releases publish every package to nuget.org with trusted publishing from the `cmdless`
  GitHub organization (no long-lived API key). That also ties the GitHub org to the nuget.org
  org that owns the packages.
- Setting up the nuget.org org, the trusted-publishing policy, and the `Cmdless.*` prefix
  reservation stay manual. They need the package names this spec fixes.
- Before the first publish, the public library API is cleaned up, because nothing published can
  change for free afterwards:
  - The tray client gets a `Configure` hook on its options. It runs on the created tray before
    anything starts, so handlers are attached before any event can be raised.
  - `DnuiHost` is the only public way to start a host command. The tray client's own start
    function and the raw run-any-command function become internal.
  - Runtime and headless settings live only on the host options, not on the tray options.
  - Every convenience function is a chain of overloads (generic → `Type` → strings). The string
    overload (assembly path and type name, for both window and app) does the real work. It
    matches the CLI arguments exactly.

## User Scenarios & Testing *(mandatory)*

A **shell user** runs dnui commands from a terminal or script without writing .NET code. A
**library user** references `Cmdless.Dnui` from a .NET app. The **maintainer** cuts releases by
pushing a version tag.

### User Story 1 - Run dnui commands from a shell after a one-line install (Priority: P1)

A shell user installs the tool once (or runs it one-off without installing) and then runs any
runtime command, for example `dnui dialog open-file --title "Pick"` or
`dnui window <assembly> <type>`. The first run downloads the matching runtime into the shared
cache. Every run prints exactly what the runtime prints and exits with the runtime's exit code,
so scripts can parse the result line and branch on the exit code.

**Why this priority**: it's the reason for the tool. Today shell users have to find and run a
runtime executable from a cache folder.

**Independent Test**: install the packed tool from a local package source and run headless
commands through it. The output and exit codes must equal running the runtime directly.

**Acceptance Scenarios**:

1. **Given** the tool is installed and the runtime isn't cached, **When** the user runs
   `dnui dialog save-file --headless --outcome '{"path":"/tmp/x.txt"}'`, **Then** the runtime is
   downloaded into the shared cache, stdout is exactly `{"path":"/tmp/x.txt"}`, and the exit code
   is 0.
2. **Given** the runtime is already cached (by the tool or by a library user), **When** the user
   runs any command, **Then** nothing is downloaded and the cached runtime runs.
3. **Given** a command the runtime rejects (for example a malformed `--outcome`), **When** the
   user runs it through the tool, **Then** stdout and the exit code are identical to running the
   runtime directly.
4. **Given** the user runs `dnui tray` with redirected stdin and stdout, **When** the caller speaks
   the tray protocol, **Then** the session works exactly as with the runtime directly: requests
   flow in, responses and notifications flow out, and ending stdin ends the session.
5. **Given** the user has never installed the tool, **When** they run it one-off through the .NET
   SDK's run-a-tool command, **Then** it behaves the same as an installed tool.

---

### User Story 2 - Install the library from nuget.org and have it just work (Priority: P1)

A library user adds `Cmdless.Dnui` from nuget.org. On first use the library downloads the
matching runtime package for their platform from nuget.org, exactly as the release published it.

**Why this priority**: nothing published so far is reachable by anyone else. Without this the
library can't acquire a runtime outside the repository.

**Independent Test**: after a release, a fresh project on each OS references the published
library version, opens a headless dialog, and gets the right result with the runtime downloaded
from nuget.org.

**Acceptance Scenarios**:

1. **Given** a release has completed, **When** a user references that library version from
   nuget.org, **Then** the runtime package for their platform at the same version is already
   available, and the first call succeeds.
2. **Given** a release is in progress, **When** the library becomes visible on nuget.org,
   **Then** all six runtime packages for that version are already downloadable.
3. **Given** the published packages, **When** a user views them on nuget.org, **Then** each shows
   a description, readme, icon, license, and project and repository links. The runtime packages
   say they aren't meant to be referenced directly.
4. **Given** a user debugging into the library or tool, **When** they step into its code,
   **Then** source and symbols are available for the published version.

---

### User Story 3 - A consistent public API before it's frozen (Priority: P1)

A library user starts trays, dialogs and windows through one entry point that's configured once.
They attach tray handlers before anything can happen. They pick windows and apps by generic
type, by `Type`, or by assembly path and type name, and every form behaves identically.

**Why this priority**: once `Cmdless.Dnui` is on nuget.org, changing its public API breaks users.
These fixes cost nothing before the first publish.

**Independent Test**: the public API surface matches the updated library contract. Tests show a
handler attached in `Configure` receives an event raised immediately after start, a headless host
starts a headless tray, and the generic, `Type` and string forms of the same call give identical
runtime command lines.

**Acceptance Scenarios**:

1. **Given** tray options with a `Configure` hook that attaches click and open handlers, **When**
   the tray starts and the menu is opened or clicked at the first possible moment, **Then** the
   handlers receive every event.
2. **Given** a `Configure` hook that calls a tray method such as changing the items, **When** the
   tray starts, **Then** that call fails with a clear "not started" error, and start fails with it
   after the runtime is stopped.
3. **Given** a `Configure` hook that throws, **When** the tray starts, **Then** the runtime is
   stopped and the exception reaches the caller of start.
4. **Given** a host configured as headless, **When** it starts a tray, **Then** the tray is
   headless, with no setting needed on the tray itself.
5. **Given** a host configured with runtime settings (cache root, package source, explicit runtime
   path), **When** it starts a tray, dialog or window, **Then** every one of them uses those
   settings.
6. **Given** a window or app chosen by generic type, by `Type`, or by assembly path and type name,
   **When** the same window and app are opened all three ways, **Then** the runtime receives the
   same command line each time.
7. **Given** the published library, **When** a user looks for a way to start a tray or run a raw
   command other than through the host, **Then** none is public.

---

### User Story 4 - Release with one tag and no stored secrets (Priority: P2)

The maintainer pushes a version tag that matches the shared version. CI checks the tag, packs
every package, waits for the maintainer's approval, publishes everything to nuget.org in a safe
order, and creates the GitHub Release. No long-lived nuget.org key is stored anywhere.

**Why this priority**: it makes releases repeatable and safe. It depends on Stories 1 to 3
having something worth publishing.

**Independent Test**: a manual dry run packs every package (including the tool) and uploads them
as build artifacts without publishing. A preview tag publishes end to end.

**Acceptance Scenarios**:

1. **Given** a tag that doesn't match the shared version, **When** it's pushed, **Then** the
   release fails before packing or publishing anything.
2. **Given** a matching tag, **When** the maintainer hasn't approved the publish step yet,
   **Then** nothing is pushed to nuget.org.
3. **Given** approval, **When** publishing runs, **Then** the six runtime packages are pushed
   first, the release waits until they can be downloaded from nuget.org, and only then are the
   library and the tool pushed.
4. **Given** a release that failed partway, **When** it's re-run for the same version, **Then**
   packages already on nuget.org are skipped without failing, and the rest are published.
5. **Given** a prerelease tag such as `v0.1.0-preview.1`, **When** it's pushed, **Then** the tag
   check, publishing, and runtime resolution by the published library and tool all work with the
   prerelease version.
6. **Given** a manual run of the release workflow, **When** it completes, **Then** every package
   is packed and uploaded as an artifact, and nothing is published or released.

---

### Edge Cases

- **Download fails before the runtime runs** (offline, feed unreachable, version not on the
  feed, integrity check fails): the tool prints the standard error line
  (`{"error":{"code":…,"message":…}}`) on stdout and exits with a dedicated non-zero exit code
  that the runtime never uses. Details go to stderr.
- **Ctrl+C or the caller kills the tool**: the runtime it started ends too. No orphaned UI
  process is left behind.
- **Arguments with spaces, quotes, empty strings, or non-ASCII text** reach the runtime
  unchanged.
- **`--help` and `--version`**: these go to the runtime too, so help shows the runtime's commands.
  The version the runtime reports is the tool's version, because of lockstep.
- **Several tool invocations at once on a cold cache**: they share the existing install lock, and
  exactly one download happens.
- **Platform with no runtime package** (an RID outside the six): the tool fails with the error
  line and dedicated code, naming the unsupported platform.
- **Cache root overrides** (`CMDLESS_ROOT`) are honored, so the tool and library users share one
  cache.
- **A package larger than nuget.org's size limit**: the release fails at pack time, before
  anything is published, naming the package and its size.
- **The tool's version differs from an installed library's version** in the same cache: each runs
  its own exact runtime version side by side. Nothing is upgraded or shared across versions.

## Requirements *(mandatory)*

### Functional Requirements

**Tool**

- **FR-001**: A new tool package, `Cmdless.Dnui.Tool`, MUST provide the command `dnui`. It MUST
  be installable globally or locally through the .NET SDK's tool commands, and runnable one-off
  without installing.
- **FR-002**: The tool MUST resolve the lockstep runtime through the Dnui library, then run it
  with its own arguments, unchanged and in order. The tool MUST NOT define commands, options, or
  output of its own. This MUST NOT add a public "run any command" function to the library
  (FR-024).
- **FR-003**: The runtime MUST be connected directly to the tool's standard input, output, and
  error. That way both one-shot commands and session commands (`tray`) work, and the runtime
  keeps sole ownership of stdout. The tool MUST exit with the runtime's exit code.
- **FR-004**: When the runtime can't be resolved (download, integrity, unsupported platform), the
  tool MUST print one standard error line on stdout, exit with a dedicated documented exit code
  outside the runtime's codes, and write diagnostics only to stderr.
- **FR-005**: When the tool is interrupted or terminated, it MUST end the runtime it started.
- **FR-006**: The tool's version MUST equal the shared version. It MUST always run exactly that
  runtime version from the shared cache layout, honoring the same overrides as the library.
- **FR-007**: The tool package MUST be a single platform-independent package. It MUST NOT contain
  the runtime or any UI platform binaries.

**Publishing**

- **FR-008**: A release tag matching the shared version MUST publish all eight packages to
  nuget.org: the six `Cmdless.Dnui.Runtime.<rid>` packages, `Cmdless.Dnui`, and
  `Cmdless.Dnui.Tool`. It MUST still create the GitHub Release with the packages attached.
- **FR-009**: Publishing MUST authenticate with short-lived credentials issued to the `cmdless`
  GitHub organization's release workflow (trusted publishing). No long-lived nuget.org API key is
  stored in the repository or its secrets.
- **FR-010**: The publish step MUST run only after a maintainer approves it. Manual runs of the
  release workflow MUST stay dry runs: pack and upload artifacts, publish nothing.
- **FR-011**: The runtime packages MUST be published, and confirmed downloadable from the public
  feed, before the library and the tool are published.
- **FR-012**: Re-running a release for a version that's partly or fully published MUST skip
  packages that already exist, without failing.
- **FR-013**: A tag that doesn't match the shared version MUST fail the release before anything
  is packed or published. Prerelease versions MUST work end to end.
- **FR-014**: Every package MUST carry a description, readme, icon, license, and project and
  repository links. The runtime packages' descriptions MUST say they aren't meant to be
  referenced directly.
- **FR-015**: The library and the tool MUST ship with source linking and symbol packages, and
  release builds MUST be deterministic.
- **FR-016**: The release MUST fail before publishing if any package exceeds nuget.org's package
  size limit.

**Governance and testing**

- **FR-017**: The constitution MUST be amended to list `Cmdless.Dnui.Tool` as a published package
  and the tool as a project, with the forwarding-only rule (FR-002) and the lockstep rule
  (FR-006).
- **FR-018**: CI MUST pack the tool, install it from a local package source, and run headless
  commands through it on every OS in the test matrix. That includes a success case, a runtime
  error case, and a pre-runtime failure case, comparing stdout and exit codes with the runtime
  run directly.
- **FR-019**: The CLI and library contracts MUST document the tool, the new exit code, and the
  public API after the cleanup (FR-020 to FR-026).

**Public API cleanup (before the first publish)**

- **FR-020**: Tray options MUST have a `Configure` hook that receives the created tray. It MUST
  run before the tray is initialized, so no event can be raised before it returns. Tray methods
  called inside it MUST fail with a clear "not started" error. If it throws, start MUST stop the
  runtime and pass the exception to the caller.
- **FR-021**: The host MUST be the only public way to start a tray. The tray client's own start
  function MUST become internal.
- **FR-022**: Runtime settings and the headless setting MUST exist only on the host options and
  apply to every tray, dialog and window that host starts. Tray options MUST describe only the
  tray (icon, tooltip, items, shutdown timeout, `Configure`).
- **FR-023**: Tray options MUST NOT carry the app as strings. Every host command that accepts a
  window or an app MUST offer, as a chain of overloads: generic type parameters, `Type`
  parameters, and assembly path plus type name strings (for the window and the app alike). Each
  overload MUST translate its arguments and call the next. Only the string overload does the
  work, and its parameters MUST map one-to-one to the CLI arguments. Window commands follow the
  same chain for the window only, since they can't change the app.
- **FR-024**: The raw "run any command line" function MUST NOT be public. The typed convenience
  functions are the only public way to run host commands.
- **FR-025**: The overloads that take a loaded assembly plus a type name string MUST be removed;
  `Type` covers loaded types and the path strings cover the rest.
- **FR-026**: Any other inconsistency in the public API found during planning MUST be listed in
  the plan with a fix or an explicit reason to keep it.

### Key Entities

- **Tool package**: `Cmdless.Dnui.Tool`, command `dnui`. It's platform-independent and versioned
  with everything else.
- **Runtime packages**: `Cmdless.Dnui.Runtime.{win-x64, win-arm64, osx-x64, osx-arm64, linux-x64,
  linux-arm64}`. They're downloaded by the library and tool, never referenced directly.
- **Library package**: `Cmdless.Dnui`.
- **Release**: one version tag `v{version}`. It produces eight packages, a GitHub Release, and one
  nuget.org publish run.
- **Trusted-publishing policy**: configured by hand on nuget.org. It names the `cmdless` GitHub
  organization, the repository, the release workflow file, and the approval environment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user on any of the six supported platforms goes from nothing to a working dialog
  result with one install command and one run command, with no manual download.
- **SC-002**: For every command in the contract test suite, running through the tool gives
  byte-identical stdout and the same exit code as running the runtime directly.
- **SC-003**: With the runtime cached, the tool adds no more than 0.5 s to a command compared
  with running the runtime directly.
- **SC-004**: A release needs exactly one action from the maintainer after pushing the tag: the
  approval. All eight packages are then available on nuget.org within 30 minutes.
- **SC-005**: At no point during or after a release can the library or tool be installed while
  its runtime package for the user's platform is unavailable.
- **SC-006**: Re-running any release, whether it failed partway or succeeded, finishes without
  error and without changing already-published packages.
- **SC-007**: No long-lived publishing credential exists in the repository, its settings, or CI
  logs.
- **SC-008**: In tests that open or click a tray menu immediately after start, 100% of events
  reach handlers attached in `Configure`.
- **SC-009**: Each host command has exactly one public way to call it per argument form (generic,
  `Type`, strings), all reached through the host.

## Assumptions

- How the tool reaches the library is left to the plan, within FR-002 and FR-024. For example,
  the tool could use the public runtime-resolve function and start the runtime itself, or use an
  internal library helper visible only to the tool. The library's one-shot run function captures
  stdout and closes stdin, so the tool can't use it as is.
- 0.1.0 has never been published, so the API cleanup breaks no released consumers. Specs 001 and
  002 contracts are updated to match.
- The tool needs a .NET 10 runtime, like any .NET tool. The runtime it downloads is
  self-contained, as before.
- The maintainer does these steps by hand on nuget.org before the first real release: create the
  `cmdless` organization, add the trusted-publishing policy for the release workflow, and
  optionally request the `Cmdless.*` prefix reservation. A preview release is used to check the
  pipeline before 0.1.0.
- "Available on nuget.org" means downloadable from the public package content feed that the
  library already uses for runtime acquisition, not only listed in search.
- nuget.org's own repository signature is enough. No author signing certificate.
- CI tests of the tool use a locally packed runtime (through the existing runtime override or a
  local package source), not nuget.org.
- Out of scope: author signing, feeds other than nuget.org, tool self-update, and new runtime
  commands.
