# Research: Dnui Tool, API Cleanup and nuget.org Publishing

Each entry gives the decision, why, and what else was considered. Facts checked against the
repository on 2026-11-11 are marked *(checked)*.

## R1. How the tool reaches the runtime

**Decision**: one internal launcher in the library, `RuntimeLauncher`, starts every runtime
process. It has three modes:

| Mode | Used by | stdin | stdout | Returns |
|---|---|---|---|---|
| `Capture` | dialogs and windows (`DnuiHost`) | closed | captured, one line | `DnuiResult` |
| `Protocol` | tray (`DnuiTray`) | pipe | pipe | the started process |
| `Passthrough` | the tool | inherited | inherited | exit code |

The tool reaches `Passthrough` through `InternalsVisibleTo("Dnui.Tool")`. Its `Program.cs` is
one call: `return await RuntimeLauncher.PassthroughAsync(args, runtimeOptions: null);`.

**Why**:
- FR-024 rules out a public run-any-command function, so the tool needs a non-public way in.
- Today two places start the runtime, and they've already drifted *(checked)*.
  `DnuiHost.RunAsync` grants Windows foreground rights (`AllowForeground`), and
  `DnuiTray.StartAsync` doesn't. One launcher gives every mode the same resolve, foreground,
  child-lifetime (R3) and error handling.
- The tool stays a forwarder with no logic of its own (FR-002).

**Considered**:
- *Tool calls the public `DnuiRuntime.ResolveAsync` and starts the process itself.* It works,
  but it duplicates launching, foreground handling and child cleanup in a third place.
- *Public passthrough function.* Rejected by FR-024.
- *Package the runtime itself as a RID-specific .NET tool.* That would put a second runtime copy
  outside `CmdlessRoot`, and it breaks the constitution rule that `Dnui.Runtime` ships only as
  `Cmdless.Dnui.Runtime.<rid>`.

## R2. Exit code for "runtime unavailable"

**Decision**: exit code **40**, `error.code` `"RuntimeUnavailable"`. `error.message` includes the
`DnuiRuntimeFailure` reason, the package id and the version. The code is reserved in the
library's `ErrorResults`, so the runtime can never use it.

**Why**: runtime codes are 0–2, 10–17, 20–22 and 30 *(checked)*, and apps use 64–255. 40 starts
a new decade for library-side launch failures. The tool doesn't use one code per reason, because
a script only needs "the UI never ran"; the reason is in the message.

## R3. Interrupts and child lifetime

**Decision**: the launcher ties the runtime's lifetime to its parent, for every mode:
- **Unix**: on `SIGINT`, `SIGTERM`, `SIGHUP` and `SIGQUIT` (`PosixSignalRegistration`), the
  parent cancels its own termination. It sends the same signal to the runtime, waits up to 5 s,
  then kills it, and finally exits with the runtime's exit code. On Ctrl+C in a terminal, both
  processes already get `SIGINT` from the terminal, and the forwarded signal is harmless.
- **Windows**: the runtime is put in a job object with `KILL_ON_JOB_CLOSE`. If the parent dies
  for any reason, including a hard kill, the runtime dies too. Ctrl+C reaches both through the
  shared console.
- **Documented gap**: on Unix, `SIGKILL` on the parent can't be forwarded, so the runtime may
  outlive it. A tray session still ends, because its stdin pipe closes. A dialog or window stays
  open until the user closes it. This goes in the CLI contract's platform notes.

**Why**: this meets FR-005 for every catchable case. Using the same code for library users
fixes the same orphan problem for them (a dialog left open after its app crashed).

**Considered**: Linux `PR_SET_PDEATHSIG`. It has to be set in the child between fork and exec,
which `Process.Start` doesn't allow.

## R4. Tool packaging

**Decision**: `Dnui.Tool/Dnui.Tool.csproj` is a `net10.0` console app with:
- `PackAsTool=true`, `ToolCommandName=dnui`, `PackageId=Cmdless.Dnui.Tool`
- `RollForward=Major`, so it also runs on newer .NET runtimes
- a project reference to `Dnui`
- framework-dependent and platform-independent (no RID)

The package carries `Dnui.dll` and its managed dependencies: Avalonia's managed core and
StreamJsonRpc. It carries no platform backends or native UI binaries (FR-007). Lockstep comes
free, because it builds with `Version=$(DnuiRuntimeVersion)` and the `Dnui` it carries has the
same embedded runtime version.

**Considered**: a tool with no `Dnui` reference and its own copy of runtime resolution.
Rejected: it would create a second acquisition implementation (constitution III).

`dnx Cmdless.Dnui.Tool …` (.NET 10) and `dotnet tool install -g|--local` need nothing extra.
Prerelease versions need `--prerelease` or an explicit `--version`.

## R5. Package source override and CI testing of the tool

**Decision**: the library honors a new `DNUI_PACKAGE_SOURCE` environment variable for
`DnuiRuntimeOptions.PackageSource`. The precedence is the same as for `CmdlessRoot`: code
override, then the environment variable, then nuget.org. The tool gets it for free.

**Why**:
- It's a real feature: organizations with offline machines or package mirrors can point every
  Dnui consumer, including the tool, at their feed without code changes.
- It makes CI testing honest. The tool tests set `CMDLESS_ROOT` to an empty temp folder and
  `DNUI_PACKAGE_SOURCE` to a folder holding a locally packed runtime. The tool then does a real
  cold-cache download and install (Story 1, scenario 1).
- Pointing `DNUI_PACKAGE_SOURCE` at an empty folder gives a deterministic `PackageNotFound` →
  exit 40. Without it, the failure test would go to nuget.org and silently start passing once
  0.1.0 is published.

**Considered**: pre-seeding the cache from the test process. That tests the warm path only, and
it doesn't give a deterministic failure case.

## R6. nuget.org trusted publishing

**Decision**: the publish job uses the `NuGet/login@v1` action, which exchanges the workflow's
GitHub OIDC token for a short-lived nuget.org API key (valid about one hour). The job needs
`permissions: id-token: write` and `environment: nuget`. The action's `user` input is the
nuget.org profile name that owns the policy, stored as the repository secret `NUGET_USER` (it isn't a
credential, but a secret keeps it out of logs).

The maintainer creates the trusted-publishing policy on nuget.org by hand. It names:
- repository owner `cmdless`
- repository `dnui`
- workflow file `release.yml`
- environment `nuget`

It's owned by the `cmdless` nuget.org organization, so any package that organization owns can be
pushed through it. The package ids don't need to be listed. The first push of a new id creates it
under the policy owner.

**Verify during setup**: the exact `user` value the action wants for an organization-owned
policy. The plan assumes the organization's profile name, with the maintainer's own profile name
as the fallback.

**Why**: FR-009, with no long-lived key anywhere. `cmdless/dnui` is public *(checked)*, so
environment required-reviewers are available on the free plan.

## R7. Push order, waiting, and re-runs

**Decision**:
1. Push the six runtime `.nupkg` files with `dotnet nuget push --skip-duplicate`.
2. Wait until each one returns HTTP 200 from the flat container,
   `https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg` (lowercase id and
   version). Poll every 30 s, for at most 30 minutes.
3. Run `NuGet/login` again for a fresh key, then push `Cmdless.Dnui` and `Cmdless.Dnui.Tool`
   with `--skip-duplicate`. Their `.snupkg` files go up automatically, because they sit next to
   the `.nupkg` files.

**Why**: the library downloads from the flat container *(checked:* `PackageFeed` *uses*
`PackageBaseAddress/3.0.0`*)*. So "downloadable from the flat container" is exactly what
"available" means for users (FR-011, SC-005). `--skip-duplicate` makes re-runs safe (FR-012).
The second login keeps the key from expiring during a long wait.

The GitHub Release step runs after publishing. On a re-run it uploads with `--clobber` to an
existing release instead of failing.

## R8. Package metadata, symbols and determinism

**Decision**:
- **Shared metadata** goes in `Directory.Build.props`:
  - `Authors` and `Company` `Cmdless`
  - `PackageLicenseExpression` `MIT`
  - `PackageProjectUrl` and `RepositoryUrl` `https://github.com/cmdless/dnui`
  - `PackageIcon` `icon.png` from `build/package/icon.png`
  - `ContinuousIntegrationBuild=true` when `GITHUB_ACTIONS=true`
- **Per-package readme**: `PackageReadmeFile`, from `build/package/readme/{Dnui,Tool,Runtime}.md`.
- **Library and tool symbols**: `IncludeSymbols=true` and `SymbolPackageFormat=snupkg`.
- **SourceLink**: built into the .NET 8+ SDK for GitHub repositories, so it needs no package.
  Add `EmbedUntrackedSources=true`.
- **Runtime nuspec**: gets the same fields through `NuspecProperties`. The description starts
  with "Runtime for Cmdless.Dnui — not meant to be referenced directly."

**Icon**: the maintainer supplies the final art. Tasks add a placeholder PNG so packing works.

## R9. Package size limit

**Decision**: the pack job fails if any `.nupkg` is over 250 MB (nuget.org's limit). It prints
each package's size either way.

*(checked)* Current local packs: `osx-arm64` is 49 MB and `win-x64` is 78 MB, so there's plenty
of headroom.

## R10. Prerelease versions

**Decision**: no special handling beyond tests.
- The tag check is a string comparison of `v$(DnuiRuntimeVersion)`, so it already accepts
  `v0.1.0-preview.1`.
- The cache path and feed URLs lowercase the version *(checked:* `PackageCache`*,*
  `PackageFeed`*)*.

A unit test covers a prerelease version through id and path construction. The first real
release is a preview (spec assumption).

## R11. Constitution amendment (v2.4.0, MINOR)

**Decision**: amend through `/speckit-constitution`:
- **Naming**: add `Cmdless.Dnui.Tool` to the published packages.
- **Projects**: add `Dnui.Tool` as a forwarder with no commands, options or output of its own.
- **Lockstep (V)**: the tool shares `DnuiRuntimeVersion`.
- **Workflow**: releases publish to nuget.org through trusted publishing only, runtime packages
  first.
- **III**: `DNUI_PACKAGE_SOURCE` joins `CMDLESS_ROOT` as an environment override.

## R12. Public API inconsistencies found (FR-026)

Items 1–6 are the spec's cleanup list. Items 7–12 were found while planning.

| # | Inconsistency *(checked)* | Fix |
|---|---|---|
| 1 | Tray handlers can only be attached after start | `DnuiTrayOptions.Configure` (`Action<DnuiTray>?`), run before `initialize` |
| 2 | Two public ways to start a tray | `DnuiTray.StartAsync` becomes internal |
| 3 | `Runtime` and `Headless` exist on both host and tray options; the host's `Headless` never reaches trays | Remove both from `DnuiTrayOptions`; trays use the host's |
| 4 | The app passed to trays as raw strings | Overload chain generic → `Type` → strings |
| 5 | Public `DnuiHost.RunAsync` | Becomes internal (the launcher's `Capture` mode) |
| 6 | `(Assembly, string)` overloads | Removed; `Type` plus path strings cover them |
| 7 | Dialog host commands can't select an app, though the CLI allows `--app-assembly` on `dialog` | See D1 below |
| 8 | `IDnuiWindowCommands.OpenWindowAsync<TWindow>` requires `new()`, but the host version doesn't | Drop `new()`; the loader checks for a public parameterless constructor at run time, like every other path |
| 9 | Tray start failures throw `DnuiTrayStartException(Result)`, while host commands throw `DnuiCommandException(Result, ErrorCode)` for the same "runtime exited with an error" | Remove `DnuiTrayStartException`; tray start failures throw `DnuiCommandException`, with `ErrorCode` named from the exit-code table |
| 10 | `DnuiRuntimeOptions` is a `sealed class`, but every other options type is a `sealed record` (needed for `with`) | Make it a `sealed record` |
| 11 | The tray launch skips Windows foreground rights, but dialogs and windows grant them | One launcher (R1) |
| 12 | `DnuiRuntime.ResolveAsync` is public although `RunAsync` won't be | **Keep**: it lets installers and apps pre-download the runtime ahead of first use, and it doesn't run anything |

**Implementation review (T039)**: the rest of the public surface was checked after the cleanup.
- `DnuiResult` stays public: `DnuiTray.Completion` and `DnuiCommandException.Result` carry it.
- `DnuiTrayNotRunningException` now also covers "not started yet" (inside `Configure`), so no new
  type was needed.
- `DnuiHost.OpenWindowAsync(Type)` and `WithApp(Type)` check the base type up front
  (`ArgumentException`), matching the generic constraints. The string forms leave validation to
  the runtime (exit codes 10–16), as the CLI does.

No further inconsistencies found.

### D1. How dialogs (and everything else) pick an app

Today an app is picked per call, and only windows and trays can do it. Making dialogs consistent
per call would add the chain to each dialog kind: 3 kinds × 3 forms. A host-level choice is
simpler.

**Recommended**: the app is chosen on the host, through a chain that returns a host bound to that
app:

```csharp
DnuiHost.Default.WithApp<MyApp>()              // → WithApp(typeof(MyApp))
DnuiHost.Default.WithApp(typeof(MyApp))        // → WithApp(path, typeName) via AssemblyLocator
DnuiHost.Default.WithApp("path/My.dll", "My.App")   // the real one; maps to --app-assembly/--app-type
```

`WithApp` returns an `IDnuiHostCommands` that shares the host's options. Every host command on it
(dialogs, windows, tray) passes the app. Command methods then take only what they show:
- `OpenWindowAsync<TWindow>()`, `OpenWindowAsync(Type)`, `OpenWindowAsync(string path, string typeName)`
- `StartTrayAsync(options)`

There's one way to pick an app for every command, and no `<TWindow, TApp>` pairs.

**Alternative (the per-call chain as discussed)**: keep `OpenWindowAsync<TWindow, TApp>()` and
the others. Add `StartTrayAsync<TApp>` / `(options, Type)` / `(options, string, string)`. Add the
same three app forms to each dialog function, or leave dialogs without app selection and document
that gap.

**Status**: confirmed by the maintainer (2026-11-11). `WithApp` returns a new `DnuiHost` that
shares the options and outcome queue and carries an internal app selection.
