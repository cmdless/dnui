# Contract: `window` and `dialog` Host Commands

> **Superseded in part (after 0.1.0-preview.1)**: `--headless`, `--outcome`, `--dialog-outcome`,
> and `DnuiHostOptions.Headless`/`Outcomes` were removed. Headless is the app's choice (Avalonia's
> `UseHeadless` in `Configure`); a headless tray still works as described. See the README's
> "Testing your UI".

Extends [../../001-tray-command/contracts/cli.md](../../001-tray-command/contracts/cli.md). The
global options (`--app-assembly`, `--app-type`), the output contract, and exit codes 0–22 apply
unchanged. Both commands are **one-shot**: exactly one JSON result line on stdout, then exit.

## `window`

```text
Dnui.Runtime [--app-assembly A --app-type T] window <assembly-path> <type-name>
             [--headless] [--dialog-outcome <json>]...
```

| Argument / option | Meaning |
|---|---|
| `<assembly-path>` | path to the IL assembly containing the window type (may equal the app assembly) |
| `<type-name>` | full name of a non-abstract `Dnui.DnuiWindow` subclass with a public parameterless constructor |
| `--headless` | Avalonia headless platform: no display; window code runs normally |
| `--dialog-outcome <json>` | headless only, repeatable: outcomes for the window's parented dialogs, in request order |

Result: `{"result": <JSON value or null>}`, exit 0. The window resolves `null` when closed without
resolving.

## `dialog`

```text
Dnui.Runtime [--app-assembly A --app-type T] dialog <kind> [options] [--headless --outcome <json>]
```

### `dialog open-file`

| Option | Meaning |
|---|---|
| `--title <text>` | dialog title |
| `--multiple` | allow selecting more than one file |
| `--filter <Name:pat,pat>` | file-type filter, repeatable, in display order |
| `--start-folder <path>` | initial folder (ignored if missing) |

Result: `{"paths":[…]}` or `{"paths":null}`.

### `dialog open-folder`

| Option | Meaning |
|---|---|
| `--title <text>` | dialog title |
| `--multiple` | allow selecting more than one folder |
| `--start-folder <path>` | initial folder (ignored if missing) |

Result: `{"paths":[…]}` or `{"paths":null}`.

### `dialog save-file`

| Option | Meaning |
|---|---|
| `--title <text>` | dialog title |
| `--suggested-name <name>` | prefilled file name |
| `--default-extension <ext>` | appended when the user types no extension (`png` or `.png`) |
| `--filter <Name:pat,pat>` | file-type choice, repeatable |
| `--start-folder <path>` | initial folder (ignored if missing) |
| `--no-overwrite-prompt` | don't ask before choosing an existing file |

Result: `{"path":"…"}` or `{"path":null}`.

### Headless

`--headless --outcome '<json>'`: show nothing, validate the JSON against the subcommand's result
shape, and resolve it. `--outcome` is required with `--headless` and invalid without it (exit 2).

## Exit codes added

| Code | `error.code` | Meaning |
|---|---|---|
| 14 | `WindowAssemblyLoadFailed` | `<assembly-path>` can't be loaded |
| 15 | `WindowTypeNotFound` | `<type-name>` isn't in the assembly |
| 16 | `WindowTypeInvalid` | not a non-abstract `DnuiWindow` with a public parameterless constructor |
| 17 | `WindowFailed` | the window's constructor or code threw (message in `error.message`) |
| 30 | `DialogUnsupported` | this platform can't show the requested dialog kind |
| 40 | `RuntimeUnavailable` | library-side only (the `dnui` tool, spec 003): the runtime couldn't be resolved. Never produced by the runtime |

Version mismatch of a window assembly uses 13 `DnuiVersionMismatch`. Usage errors (bad filter
syntax, `--outcome` problems, unknown options) use 2 `Usage`. All of these resolve an error line;
`window` and `dialog` are never sessions.

## Platform notes

| Behavior | Windows | macOS | Linux |
|---|---|---|---|
| Comes to front when started by a background app | yes (caller grants foreground: tray clicks and `DnuiHost`) | yes (`activateIgnoringOtherApps`) | best effort (window manager focus rules) |
| Parented dialogs | modal | sheet | modal (portal or GTK) |
| Dock/taskbar entry while open | taskbar | Dock icon, generic name for an unbundled runtime | taskbar |
| `open-folder` | yes | yes | depends on the portal or GTK; otherwise exit 30 |

Confirm each cell during quickstart validation, and update this table if a result differs.
