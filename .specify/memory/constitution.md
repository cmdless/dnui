# Dnui Constitution

## Core Principles

### I. Runtime Is a Cached CLI Host

Dnui.Runtime is a cached, per-platform Avalonia host executable. A consumer process
launches it with command-line arguments and gets UI.

- The runtime MUST be a System.CommandLine CLI. Results MUST come back through stdout and
  exit codes (see Output contract below).
- The global options `--app-assembly` and `--app-type` select the application:
  - If they are provided, the runtime MUST load the assembly and resolve the type.
    Otherwise it MUST use its built-in `App`, which is a `DnuiCommandApp`.
  - The runtime MUST check that the resolved type derives from `DnuiApp` before creating an
    instance. Otherwise it MUST exit with a distinct, documented error.
  - For every app, the runtime MUST build `AppBuilder` with platform detection and then call
    the app's `Configure(AppBuilder)`.
  - If the type derives from `DnuiCommandApp`, the runtime MUST also parse the remaining
    arguments into one built-in command and its options and set it on the app before
    starting Avalonia. That command is the only difference between the two modes.
- The built-in command set MUST stay small. The initial commands are window, dialog, and
  tray. A new command needs a justification showing that existing commands cannot express
  the UI.
- Output contract:
  - The runtime owns stdout. Before any app code runs, it captures the raw stdout stream for
    the app and redirects `Console.Out` to stderr. Stray writes from the runtime, the app,
    or libraries MUST NOT reach stdout.
  - An app or command is either one-shot or a session. It becomes a session by claiming
    stdio through `DnuiApp.Stdio.OpenSession()`, which can succeed at most once per process.
    This applies equally to built-in commands and to full-takeover apps.
  - One-shot apps and commands MUST write their outcome only through `Resolve`: exactly one
    single-line JSON document, then exit. User choices, including
    cancelling, are results with exit code 0. Errors are results too: a non-zero exit code
    with an `{"error":{"code","message"}}` body.
  - Sessions (for example, tray) MUST write only their protocol to stdout. While a session
    is open, a non-null `Resolve` result MUST be dropped. Sessions end with
    `Resolve(null, code)`, which writes nothing. Their startup failures are reported by
    exit code only.
  - Runtime failures before the app runs (bad arguments, app resolution) MUST be resolved the
    same way as one-shot errors, except when the requested command is a session command,
    where the exit code alone is used.
  - stderr is diagnostics for humans only and is not part of the contract. Callers SHOULD NOT
    redirect it unless they drain it.
- Consumers MUST NOT reference Dnui.Runtime. Consumer code reaches the runtime only by being
  loaded into it, or by launching it.

Rationale: one cached host per platform serves every app. Consumers either drive the
standard commands or bring their own app type, without shipping Avalonia platform
backends themselves.

### II. Library Provides the App Model

The Dnui library is what consumers reference. It holds the app base classes and the runtime
acquisition helpers.

- The library MAY reference Avalonia packages that are not platform backends (base
  `Avalonia`, themes, fonts, and Debug-only diagnostics). Platform backends
  (`Avalonia.Desktop` and similar) MUST belong to the runtime only.
- The library MUST provide two extension points:
  - `DnuiApp : Application`, with a virtual `Configure(AppBuilder)`. The base implementation
    applies the default builder setup (fonts, logging, Debug-only developer tools), so
    overrides customize the defaults instead of rebuilding them. Deriving from `DnuiApp`
    directly is the full-takeover mode: the consumer owns framework initialization and uses
    Dnui only as a cached Avalonia host.
  - `DnuiApp` also provides `Resolve(result, exitCode)`, the single path that writes the
    resolved result to stdout and shuts down. The first call wins, and it is safe to call
    from any thread.
  - `DnuiApp` exposes the captured stdio as `Stdio` (`DnuiStdio`). Its `OpenSession()` hands
    out the raw stdin and stdout streams (`DnuiSession`) to exactly one claimant. Built-in
    session commands MUST use this same public API, with no private path.
  - `DnuiCommandApp : DnuiApp`, which seals framework initialization to run the built-in
    command chosen by the runtime. Deriving from it customizes how the built-in commands
    run, through overridable members, without taking over the app lifecycle.
- Other base implementations (for example, runtime locator, downloader, launcher) MUST be
  overridable through virtual or abstract members, or through injectable interfaces.
- Public API surface MUST be minimal. Every public type or member needs a consumer-facing
  reason to exist.

Rationale: consumers who package their own apps need clear override points. The two-tier
app model gives a low-effort path (customize commands) and a full-control path (own the
app).

### III. Centralized, Cached Runtime Acquisition

Getting the runtime onto a machine is a library responsibility.

- The library MUST provide helpers that determine the current platform's RID and resolve
  `Cmdless.Dnui.Runtime.<rid>` at exactly the version in the library's embedded
  `DnuiRuntimeVersion`. The helpers MUST NOT pick "latest" or float to another version.
  They download and extract the package into one centralized per-user cache location and
  return the absolute path to the runtime executable.
- The cache MUST be keyed by runtime version and RID, so multiple apps and versions coexist
  and are reused without re-downloading.
- On-disk layout (shared by every Cmdless product, so tools can find each other's caches):
  - `CmdlessRoot` defaults to `~/.cmdless` (the user's home directory on every OS) and is
    overridden by the `CMDLESS_ROOT` environment variable.
  - `DnuiRoot` defaults to `{CmdlessRoot}/dotnet`.
  - Extracted NuGet packages go in `{DnuiRoot}/packages/{id}/{version}/`, where `{id}` is the
    full package id, lowercased. This matches NuGet's global packages folder. RID-specific
    packages carry the RID in their id, so different RIDs never collide (for example,
    `{DnuiRoot}/packages/cmdless.dnui.runtime.osx-arm64/0.1.0/`).
  - Cross-process locks go in `{DnuiRoot}/locks/`.
  - New kinds of Dnui state MUST go in a named subfolder of `DnuiRoot`, never directly in
    `CmdlessRoot`, and never anywhere else unless a plan justifies it.
- Acquisition MUST be idempotent and safe under concurrent callers (multiple processes
  resolving at once MUST NOT corrupt the cache). Partial extractions MUST NOT be reported as
  usable.
- Downloaded packages MUST be integrity-checked before extraction is treated as complete.
- The roots MUST be overridable by consumers in code, and `CmdlessRoot` through
  `CMDLESS_ROOT`. A code override takes precedence over the environment variable.

Rationale: an exact match guarantees that the runtime loading a consumer's app was built
against the same Dnui and Avalonia binaries as that app.

### IV. Cross-Platform Parity

- Every built-in command MUST work on Windows, macOS, and Linux. A platform gap MUST be
  documented in the command's contract and reported at runtime with a distinct, documented
  error. It MUST NOT fail silently.
- Platform-specific code MUST be isolated behind platform adapters. It MUST NOT appear in
  command handling or library logic.
- Runtime packages MUST be produced for each supported RID (at minimum x64 and arm64 for each
  supported OS) unless an exclusion is documented.

Rationale: the reason Dnui exists is cross-platform UI, so parity is part of the product.

### V. Lockstep Versioning

- The Dnui library, Dnui.Runtime, and every `Cmdless.Dnui.Runtime.<rid>` package share one
  version, `DnuiRuntimeVersion`, and ship together.
- The public contract consists of the CLI (commands, options, resolved outputs and their
  JSON shapes, error codes, exit codes) and the
  library's app-model API (`DnuiApp`, `DnuiCommandApp`, and their overridable members). It
  MUST be documented.
- Removing or changing the meaning of a command, option, output field, or overridable member
  is a MAJOR change. Additive, optional changes are MINOR.
- When the runtime loads an app assembly, it SHOULD verify that the assembly's Dnui
  reference matches its own version, and fail with a clear error on a mismatch.

Rationale: app assemblies run inside the runtime process, so version drift becomes a binary
incompatibility rather than a protocol error.

## Technology & Packaging Constraints

- Language and platform: C# on .NET 10 (`net10.0`) with nullable reference types enabled.
  New code MUST NOT introduce nullable warnings.
- UI framework: Avalonia, with a single version set by `AvaloniaVersion` in the root
  `Directory.Build.props` and used by both the library and the runtime. Developer and
  diagnostic tooling MUST be Debug-only and MUST NOT ship in release runtime packages.
- CLI: Dnui.Runtime uses System.CommandLine for all argument parsing.
- Naming: projects, assemblies, and C# namespaces use the `Dnui.*` prefix. Published NuGet
  package IDs MUST use the `Cmdless.*` prefix (`Cmdless.Dnui`, `Cmdless.Dnui.Runtime.<rid>`).
  The `Cmdless` prefix MUST NOT be added to namespaces or folder names.
- Runtime packaging: Dnui.Runtime itself MUST NOT be published as a package. It is published
  only as one self-contained package per supported RID, named `Cmdless.Dnui.Runtime.<rid>`
  (for example, `Cmdless.Dnui.Runtime.osx-arm64`). Consumers never reference these packages.
  The library downloads them at run time.
- Version source: `DnuiRuntimeVersion` in the root `Directory.Build.props` is the single
  source of the version. Dnui.Runtime and its per-RID packages MUST build with it, and the
  Dnui library MUST embed it as `AssemblyMetadata("DnuiRuntimeVersion", ...)`. Hard-coding
  the version anywhere else is prohibited.
- The library targets `net10.0`. Adding a broader target is allowed only with a justification
  in the plan, and the Avalonia dependency constrains it.
- Formatting follows the repository `.editorconfig` (LF endings, UTF-8, 4-space indent for
  C#, 2-space elsewhere).
- New third-party dependencies in the library need a justification in the feature plan.

## Development Workflow & Quality Gates

- Features follow the Spec Kit flow: specify → (clarify) → plan → tasks → implement. Each
  plan MUST include a Constitution Check against the principles above.
- Every built-in command MUST have contract tests that drive the runtime executable through
  its CLI and assert on outputs and exit codes.
- App resolution MUST have tests covering the default `App`, a custom `DnuiCommandApp`, a
  custom `DnuiApp`, and the invalid cases (missing assembly, missing type, type not deriving
  from `DnuiApp`).
- Library acquisition helpers MUST have tests covering cache hit, cache miss, concurrent
  resolution, and corrupted or partial cache recovery.
- Changes MUST build and pass tests on Windows, macOS, and Linux before merge.
- Any change to the public contract (Principle V) MUST update its documentation in the same
  change.
- Reviews MUST verify that no consumer-facing path requires referencing Dnui.Runtime or
  Avalonia platform backends.

## Governance

- This constitution overrides conflicting practices, templates, and conventions in this
  repository.
- Amendments are made through `/speckit-constitution`, recorded in this file with an updated
  version and amendment date, and reviewed like any other change. Amendments that remove or
  redefine a principle MUST include a migration note for in-flight specs and plans.
- Versioning policy: MAJOR for removing or redefining a principle or governance rule; MINOR
  for adding a principle or section or materially expanding guidance; PATCH for
  clarifications and wording.
- Compliance: each feature plan's Constitution Check MUST pass, or list every violation with
  its justification in the plan's complexity tracking. Unjustified violations block merge.

**Version**: 2.2.1 | **Ratified**: 2026-11-08 | **Last Amended**: 2026-11-10
