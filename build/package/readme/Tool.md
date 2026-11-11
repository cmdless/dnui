# Cmdless.Dnui.Tool

The `dnui` command: Dnui UI from any shell. Each command prints one JSON result line and exits
with a meaningful code, so scripts can use it directly.

```bash
dotnet tool install -g Cmdless.Dnui.Tool
dnui dialog open-file --title "Pick" --multiple
dnui dialog save-file --suggested-name report --default-extension pdf
```

Or without installing: `dnx Cmdless.Dnui.Tool dialog open-folder`.

The runtime is downloaded on first use into `~/.cmdless` (override with `CMDLESS_ROOT`; feed with
`DNUI_PACKAGE_SOURCE`). Docs: https://github.com/cmdless/dnui
