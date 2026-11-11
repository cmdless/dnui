# Data Model: Dnui Tool, API Cleanup and nuget.org Publishing

There's no persistent data. These are the shapes the feature works with.

## Packages (all at `DnuiRuntimeVersion`)

| Id | Built from | Contents | Referenced by |
|---|---|---|---|
| `Cmdless.Dnui.Runtime.{win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64}` | `Dnui.Runtime` (`PackRuntime`) | self-contained runtime for one RID | nobody; downloaded by the library and the tool |
| `Cmdless.Dnui` (+ `.snupkg`) | `Dnui` | library, `buildTransitive` targets | consumer projects |
| `Cmdless.Dnui.Tool` (+ `.snupkg`) | `Dnui.Tool` | `dnui` tool + `Dnui.dll` + managed dependencies | `dotnet tool install`, `dnx` |

Common metadata on all packages: authors, MIT license, project and repository URLs, icon, readme.
Runtime package descriptions start with "not meant to be referenced directly".

## Runtime launch (internal `RuntimeLauncher`)

| Field | Meaning |
|---|---|
| `Options` | `DnuiRuntimeOptions?`: how to resolve. Null means defaults plus environment variables |
| `App` | optional `(assemblyPath, typeName)`, which becomes `--app-assembly` / `--app-type` before the command |
| `Arguments` | the command and its arguments |
| `Headless` | adds `--headless` after the command (host-level setting) |
| `Mode` | `Capture` → `DnuiResult`; `Protocol` → process with stdio pipes; `Passthrough` → exit code |

Transitions: **Resolve** (may throw `DnuiRuntimeUnavailableException`, exit 40 in `Passthrough`)
→ **Start** (job object on Windows, signal forwarding on Unix) → **Run** → **Exit** (code
returned or exposed through `Completion`).

## Tray start states

`Created` (after process start; `Configure` runs; methods throw "not started") → `Listening` →
`Initialized` (returned to the caller; events flow) → `Ended` (`Completion` set; methods throw
`DnuiTrayNotRunningException`).

On failure in `Created` (`Configure` throws), the runtime is killed and the tray goes to `Ended`.
On failure before `Initialized` (the runtime exits), `DnuiCommandException` is thrown.

## Host and app binding (research D1)

`DnuiHost` (options, outcome counter) → `WithApp(path, typeName)` → bound host (same options and
outcome counter, plus the app). Every host command on a bound host passes the app.

## Release run

`Tagged` → `Packed` (tag check, size check, artifacts) → `Awaiting approval` → `Runtimes pushed`
→ `Runtimes available` (flat container returns 200 for all 6) → `Library and tool pushed` →
`Released` (GitHub Release). Any step can be re-run; pushes skip existing packages.
