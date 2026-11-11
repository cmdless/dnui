# Contract: the `dnui` Tool

Package `Cmdless.Dnui.Tool`, command `dnui`. The tool forwards to the runtime. Its command line
is exactly the runtime's ([001 cli.md](../../001-tray-command/contracts/cli.md),
[002 cli.md](../../002-windows-dialogs/contracts/cli.md)).

```text
dnui <any runtime arguments>
```

## Install and run

```bash
dotnet tool install -g Cmdless.Dnui.Tool
```

```bash
dnx Cmdless.Dnui.Tool dialog open-file
```

Prerelease versions need `--prerelease` or `--version <v>`.

## Behavior

| Step | Behavior |
|---|---|
| Resolve | Resolves `Cmdless.Dnui.Runtime.<rid>` at exactly the tool's version, the same way the library does. It honors `CMDLESS_ROOT` and `DNUI_PACKAGE_SOURCE`. A warm cache means no network. |
| Run | Starts the runtime with the tool's arguments, unchanged and in order. stdin, stdout and stderr are **inherited**, not piped, so the output is byte-identical to running the runtime directly. Sessions (`tray`) work. |
| Exit | The runtime's exit code. |
| `--help`, `--version` | Forwarded. The version shown is the runtime's, which equals the tool's. |

## Error added

| Exit code | `error.code` | When | stdout |
|---|---|---|---|
| 40 | `RuntimeUnavailable` | The runtime can't be resolved: `UnsupportedPlatform`, `PackageNotFound`, `DownloadFailed`, `IntegrityFailed`, `LockTimeout` | `{"error":{"code":"RuntimeUnavailable","message":"<reason>: <package id> <version>: <detail>"}}` |

The error line is written even when the arguments name a session command. No runtime ever ran,
so no session exists. Details go to stderr. The runtime never uses exit code 40.

## Interrupts and lifetime

| Event | Windows | macOS / Linux |
|---|---|---|
| Ctrl+C in a terminal | both get it through the console; the tool waits for the runtime and exits with its code | both get `SIGINT`; the tool forwards it, waits up to 5 s, then kills |
| `SIGTERM`/`SIGHUP`/`SIGQUIT` to the tool | n/a | forwarded to the runtime as above |
| The tool is hard-killed | the runtime dies too (job object) | **gap**: the runtime may outlive it. A tray session still ends when stdin closes; a dialog or window stays until closed |

## Environment

| Variable | Effect |
|---|---|
| `CMDLESS_ROOT` | cache root (default `~/.cmdless`) |
| `DNUI_PACKAGE_SOURCE` | NuGet v3 service index URL, or a local folder of `.nupkg` files (default nuget.org) |
