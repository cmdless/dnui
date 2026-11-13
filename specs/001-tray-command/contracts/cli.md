# Contract: Dnui.Runtime CLI

> **Superseded in part (after 0.1.0-preview.1)**: `--headless`, `--outcome`, `--dialog-outcome`,
> and `DnuiHostOptions.Headless`/`Outcomes` were removed. Headless is the app's choice (Avalonia's
> `UseHeadless` in `Configure`); a headless tray still works as described. See the README's
> "Testing your UI".

**Contract version**: tied to `DnuiRuntimeVersion` (constitution V)

## Synopsis

```text
Dnui.Runtime [--app-assembly <path> --app-type <type>] <command> [command options]
Dnui.Runtime [--app-assembly <path> --app-type <type>]          # full-takeover DnuiApp only
```

## Global options

| Option | Value | Notes |
|---|---|---|
| `--app-assembly <path>` | path to a .NET assembly | Must be given together with `--app-type`. |
| `--app-type <type>` | full type name, for example `MyCo.MyApp.TrayApp` | Resolved in `--app-assembly`. Must derive from `Dnui.DnuiApp`. |
| `--help`, `--version` | — | Standard behavior. Output goes to stdout. These exit before any app runs, so the output contract below doesn't apply. |

Without the app options, the built-in `App` (a `DnuiCommandApp`) is used.

## Output contract

- The runtime owns stdout. Before any app code runs, it captures the raw stdout stream for the
  app and points `Console.Out` at stderr.
- **One-shot** (full-takeover apps that don't open a session; window and dialog later):
  stdout receives exactly one JSON line, written by `Resolve`, then the process exits.
- **Session** (`tray`, or any app that calls `Stdio.OpenSession()`): stdout carries only the
  protocol. The session ends with `Resolve(null, code)`, which writes nothing.
- **Error result**, for one-shot and pre-app failures without a session command:
  ```json
  {"error":{"code":"AppTypeNotFound","message":"Type 'X' was not found in 'app.dll'."}}
  ```
  `code` is the stable name from the exit code table, and `message` is human-readable.
- **Session commands never write an error result.** Their failures are reported by exit code
  only.
- **stderr** is diagnostics for humans. It isn't part of the contract, and its content may
  change at any time. Callers should let it pass through, or drain it if they redirect it.

## App resolution order

1. Parse arguments. Usage errors → exit 2.
2. If the app options are given: load the assembly (fail → 10), resolve the type (fail → 11),
   check that it derives from `DnuiApp` (fail → 12), and check that the Dnui version it
   references matches the runtime's (fail → 13). No instance is created before these pass.
3. Create the app instance.
4. If it's a `DnuiCommandApp`, a command is required (missing → 2). Prepare the command (tray:
   stdio check → 20, Linux tray host probe → 21) and set it on the app. If it's a plain
   `DnuiApp`, any command given is a usage error (2).
5. Build: `AppBuilder.Configure(() => app).UsePlatformDetect()`, then command builder options,
   then the app's `Configure`.
6. Start the classic desktop lifetime. The process exit code is the lifetime's exit code.

## Commands

### `tray`

Shows one system tray icon controlled over stdio. The protocol is in
[tray-protocol.md](tray-protocol.md).

| Option | Meaning |
|---|---|
| `--headless` | Run without a real OS tray icon. Everything else (protocol, state, notifications, lifecycle, exit codes) is identical. No tray host is needed, so exit code 21 never happens. Use it for automated tests and CI. |

Requirements: stdin and stdout must both be redirected (pipes). In tray mode, stdout carries
only protocol messages. Diagnostics and any `Console.Write*` output go to stderr.

## Exit codes

| Code | `error.code` | Meaning |
|---|---|---|
| 0 | — | Normal exit (for tray: input stream ended) |
| 1 | `Unexpected` | Unexpected error |
| 2 | `Usage` | Usage error: invalid arguments, missing or extra command, only one app option given |
| 10 | `AppAssemblyLoadFailed` | `--app-assembly` could not be loaded |
| 11 | `AppTypeNotFound` | `--app-type` not found in the assembly |
| 12 | `AppTypeInvalid` | `--app-type` does not derive from `Dnui.DnuiApp` |
| 13 | `DnuiVersionMismatch` | The app's referenced Dnui version doesn't match the runtime's |
| 20 | — | `tray`: stdin or stdout not redirected (exit code only) |
| 21 | — | `tray`: no system tray host available, Linux: no `org.kde.StatusNotifierWatcher` (exit code only) |
| 22 | — | `tray`: stdio was already claimed as a session by app code (exit code only) |

When the requested command is `tray`, codes 1–13 are also exit code only. Otherwise they come
with an error result on stdout. Codes 1–63 are reserved for the runtime. Apps resolving their
own errors should use codes 64–255. Exit codes above 255 are truncated on macOS and Linux.

An unexpected crash (an unhandled exception, or the process being killed) may exit without any
stdout output, so callers must handle "non-zero exit code and no result".

## Testing support

There are no build-specific or environment-variable test hooks. Automated tests use the public
`tray --headless` option, together with the `tray/getState`, `tray/simulateOpen`, and
`tray/simulateClick` protocol methods. These work identically in every released build.
