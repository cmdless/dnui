# Data Model: Windows and Built-in Dialogs

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## FileFilter

| Field | Type | Rules |
|---|---|---|
| `name` | string | Non-empty. Shown in the dialog's type list |
| `patterns` | string[] | One or more, for example `*.png`. Non-empty entries |

CLI form: `Name:pattern,pattern`. A missing name, colon, or pattern is a usage error (exit 2).

## Dialog requests

| Kind | Options | Defaults |
|---|---|---|
| open-file | `title?`, `multiple`, `filters[]`, `startFolder?` | single selection, no filters, platform start folder |
| open-folder | `title?`, `multiple`, `startFolder?` | single selection |
| save-file | `title?`, `suggestedName?`, `defaultExtension?`, `filters[]` (choices), `startFolder?`, `overwritePrompt` | overwrite prompt on |

- `startFolder` that doesn't exist → ignored (platform default). Not an error.
- `defaultExtension` without a leading dot is accepted (`png` is the same as `.png`).

## Dialog results

| Kind | Chosen | Cancelled |
|---|---|---|
| open-file | `{"paths":["/abs/a","/abs/b"]}` (one or more, even when single) | `{"paths":null}` |
| open-folder | `{"paths":["/abs/dir"]}` | `{"paths":null}` |
| save-file | `{"path":"/abs/file.ext"}` | `{"path":null}` |

Paths are absolute local file-system paths. Selections without a local path are dropped, and if
none remain, the result is cancelled.

## Headless outcome

The same JSON shape as the dialog's result, supplied up front:
- `dialog … --headless --outcome '<result json>'`: validated against the dialog kind's shape.
  `{"paths":[]}` is invalid; use `null` for cancel.
- `window … --headless --dialog-outcome '<result json>'` (repeatable): consumed by parented
  dialogs in request order, each validated against the requesting dialog's kind.

## Window

| Field | Notes |
|---|---|
| assembly | file path (host command) or already-loaded assembly (window command) |
| type | full type name. Must derive from `DnuiWindow`, be non-abstract, and have a public parameterless constructor |
| owner | none (host command: top-level), or the parent `DnuiWindow` (window command child) |
| result | JSON value or `null`. First `Resolve` wins |

### Window lifecycle

```text
 Loading ──type checks ok──▶ Constructing ──ctor ok──▶ Shown ──Resolve(r)──▶ Closed(result = r)
    │                            │                       └─user closes──▶ Closed(result = null)
    │                            └─ctor throws──▶ Failed (host: exit 17)
    └─assembly/type/base/version bad──▶ Rejected (exit 14/15/16/13; window commands: exception)
 Shown ──owner closes──▶ Closed(result = null)          (child windows)
 Shown ──unhandled exception in window code──▶ Failed (host: exit 17)
```

Host `window` result line: `{"result": <result>}`.

## Assembly resolution (host commands)

| Step | Path |
|---|---|
| 1 | `Assembly.Location` (if non-empty) |
| 2 | `{AppContext.BaseDirectory}/{AssemblyName}.dll` |
| 3 | `{AppContext.BaseDirectory}/dnui/{AssemblyName}.dll` |
| — | otherwise `DnuiAssemblyNotFoundException` before any process starts |

## Published `dnui/` folder (single-file or AOT)

| Item | Source | Notes |
|---|---|---|
| `{App}.dll` | `@(IntermediateAssembly)` | the project's IL |
| `{App}.deps.json` | `$(ProjectDepsFilePath)` | dependency manifest for the runtime's resolver |
| dependency `.dll`s (and satellite folders) | `@(ReferenceCopyLocalPaths)` (managed) | excludes `Avalonia*`, `Dnui`, `StreamJsonRpc*`, and other runtime-provided assemblies |
