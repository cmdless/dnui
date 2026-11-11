# Quickstart: Validate Windows and Built-in Dialogs

Contracts: [cli.md](contracts/cli.md), [library-api.md](contracts/library-api.md). Data shapes:
[data-model.md](data-model.md).

## Prerequisites

- .NET 10 SDK
- For desktop steps: a desktop session on each OS
- For the AOT step: the native AOT toolchain (Xcode command-line tools on macOS, `clang` on Linux,
  and Visual Studio C++ tools on Windows)

## 1. Automated

```bash
dotnet build Dnui.slnx
```

```bash
dotnet test Dnui.slnx
```

Expected: everything passes headless, including the dialog and window contract tests and the
single-file publish test.

## 2. Dialogs from a terminal (each OS)

```bash
dotnet run --project Dnui.Runtime -- dialog open-file --title "Pick images" --multiple --filter "Images:*.png,*.jpg" --start-folder ~
```

Expected:
- The native dialog appears **in front**, titled "Pick images", filtering images, and opens in the
  home folder.
- Choosing two files prints `{"paths":["…","…"]}` and exits 0.
- Cancelling prints `{"paths":null}` and exits 0.

Repeat for:
- `dialog open-folder --multiple`
- `dialog save-file --suggested-name report --default-extension pdf --filter "PDF:*.pdf"`: choosing
  an existing file asks to overwrite. With `--no-overwrite-prompt` it doesn't ask.

Record any option that has no visible effect on that OS in the contract's platform table.

## 3. Window command (each OS)

```bash
dotnet run --project Dnui.Runtime -- window tests/Dnui.TestApps/bin/Debug/net10.0/Dnui.TestApps.dll Dnui.TestApps.NameFormWindow
```

Expected: the form appears in front with its own taskbar or Dock entry. Submitting `Ada` prints
`{"result":{"name":"Ada"}}`. Closing it instead prints `{"result":null}`.

## 4. Window commands: parenting and child windows (each OS)

Run `window … Dnui.TestApps.ParentWindow`, which has buttons for "Open file…" and "Edit child…".

Expected:
- "Open file…" opens the dialog modal to the window: a **sheet** on macOS, and the parent can't be
  clicked on Windows and Linux.
- Clicking it twice quickly queues the second dialog.
- "Edit child…" opens a child that stays above the parent and closes with it. Its result shows in
  the parent.

## 5. From a tray click (each OS)

Using the spec 001 tray quickstart, have the tray client call
`DnuiHost.Default.OpenFilesAsync()` when an item is clicked.

Expected: the dialog comes to the **front**, including on Windows (foreground granted by the tray
click) and on macOS (the dialog process activates itself).

## 6. One project, every publish mode

```bash
dotnet publish tests/Dnui.SampleApp -c Release -r <rid> -p:PublishSingleFile=true --self-contained
```

```bash
dotnet publish tests/Dnui.SampleApp -c Release -r <rid> -p:PublishAot=true
```

Expected:
- Each publish folder contains `dnui/Dnui.SampleApp.dll` and `dnui/Dnui.SampleApp.deps.json`, and
  no Avalonia or Dnui assemblies under `dnui/`.
- Running the published executable opens its own window through `OpenWindowAsync<SampleWindow>()`
  with its own app (`OpenWindowAsync<SampleWindow, SampleApp>()`).
- With `-p:DnuiShipUiAssemblies=false`, the same run fails before any window opens, with
  `DnuiAssemblyNotFoundException` naming `dnui/Dnui.SampleApp.dll`.

## 7. Headless

```bash
dotnet run --project Dnui.Runtime -- dialog save-file --headless --outcome '{"path":"/tmp/x.txt"}'
```

Expected: `{"path":"/tmp/x.txt"}`, exit 0, nothing shown. With `--outcome '{"paths":[]}'` it exits
2 (wrong shape).
