# Quickstart: Validate the Tool, API Cleanup and Publishing

Contracts: [tool-cli.md](contracts/tool-cli.md), [library-api.md](contracts/library-api.md),
[release.md](contracts/release.md).

## 1. Automated (every OS)

```bash
dotnet test Dnui.slnx
```

Expected: everything passes, including:
- **Tool tests**: pack the runtime for this RID and the tool into a temp feed, install the tool
  from it, and run with `CMDLESS_ROOT` set to an empty temp folder and `DNUI_PACKAGE_SOURCE` set
  to the feed. They check a cold-cache headless `dialog save-file`, a usage error (exit 2), a
  headless `tray` session, and an empty feed (exit 40, `RuntimeUnavailable`). Stdout and exit
  codes are compared with the runtime run directly.
- **API tests**: the public type list, `Configure` catching an event raised immediately, a
  headless host giving a headless tray, and identical command lines from the generic, `Type` and
  string forms.

## 2. Tool from a local feed, by hand

```bash
dotnet build Dnui.Runtime -t:PackRuntime -p:RuntimeIdentifier=osx-arm64
```

```bash
dotnet pack Dnui.Tool -c Release -o artifacts/packages
```

```bash
dotnet tool install --tool-path /tmp/dnui-tool --add-source artifacts/packages Cmdless.Dnui.Tool
```

```bash
CMDLESS_ROOT=/tmp/dnui-root DNUI_PACKAGE_SOURCE=artifacts/packages /tmp/dnui-tool/dnui dialog open-file --title "Pick"
```

Expected: the native dialog appears in front, and picking a file prints `{"paths":["…"]}`. Run it
again, and nothing is downloaded. Press Ctrl+C while the dialog is open: both processes end.

## 3. Release dry run

GitHub → Actions → release → Run workflow.

Expected: the artifact holds 8 `.nupkg` files plus 2 `.snupkg` files, sizes are printed, and
nothing is published.

## 4. First preview release (after the manual setup in release.md)

1. Set `DnuiRuntimeVersion` to `0.1.0-preview.1` and merge.
2. Tag the merge with `v0.1.0-preview.1` and push the tag.
3. Approve the `nuget` environment.

Expected:
- The runtime packages appear first, then the library and the tool.
- `dnx Cmdless.Dnui.Tool@0.1.0-preview.1 dialog save-file --headless --outcome '{"path":"/tmp/x"}'`
  prints `{"path":"/tmp/x"}` on a clean machine.
- A fresh console project referencing `Cmdless.Dnui` 0.1.0-preview.1 opens a headless dialog.
- Re-running the workflow succeeds without changing anything.
