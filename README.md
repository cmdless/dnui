# dnui

Cross-platform .NET UI through a **cached, per-platform Avalonia runtime**. Your app references a
small library (`Cmdless.Dnui`); the UI itself runs in a self-contained runtime that is downloaded
once per machine and shared by every app that uses it.

## From a shell: the `dnui` tool

```bash
dotnet tool install -g Cmdless.Dnui.Tool
dnui dialog open-file --multiple --filter "Images:*.png,*.jpg"   # prints {"paths":[…]} or {"paths":null}
dnx Cmdless.Dnui.Tool dialog save-file --suggested-name report     # one-off, no install
```

`dnui` forwards its arguments to the runtime unchanged: same output, same exit codes, and
`dnui tray` speaks the tray protocol. If the runtime can't be downloaded it prints
`{"error":{"code":"RuntimeUnavailable",…}}` and exits 40.

## Show a tray icon

```csharp
using Dnui;

await using var tray = await DnuiHost.Default.StartTrayAsync(new()
{
    Icon = DnuiTrayIcon.FromFile("icon.png"),
    Tooltip = "My App",
    Items = [new("open", "Open"), DnuiTrayItem.Separator, new("quit", "Quit")],
    // Runs before the tray starts, so no event is missed.
    Configure = t =>
    {
        t.Opened += () => Console.WriteLine("menu opened");
        t.Clicked += id => { if (id == "quit") _ = t.DisposeAsync(); };
    },
});

var result = await tray.Completion; // DnuiResult(ExitCode, StdOut)
```

The first start on a machine downloads `Cmdless.Dnui.Runtime.<rid>` at exactly the
library's version into the shared cache; every later start reuses it with no network access.

## Windows and dialogs

```csharp
// Host commands: each call starts its own runtime process, and may use its own app.
var files  = await DnuiHost.Default.OpenFilesAsync(new() { AllowMultiple = true, Filters = [new("Images", ["*.png", "*.jpg"])] });
var result = await DnuiHost.Default.OpenWindowAsync<SettingsWindow>();          // your XAML window
var other  = await DnuiHost.Default.WithApp<MyApp>().OpenWindowAsync<SettingsWindow>(); // with your own app

// Window commands: inside a DnuiWindow, parented to it (modal; a sheet on macOS).
public partial class SettingsWindow : DnuiWindow
{
    async void Browse() => Folder = (await Ui.OpenFoldersAsync())?[0];
    async void Edit()   => Item   = await Ui.OpenWindowAsync<EditItemWindow>(); // owned child window
    void Save()         => Resolve(new JsonObject { ["saved"] = true });
}
```

- **Host commands** (`DnuiHost`, and `tray`/`window`/`dialog` on the CLI) start a new process
  and may override the app with `WithApp<TApp>()` (or `WithApp(Type)`, or an assembly path and
  type name). Windows likewise take a generic type, a `Type`, or an assembly path and type name. **Window commands** (`DnuiWindow.Ui`) stay in the window's process,
  inherit its app, and are parented to the window.
- **One project, any publish mode.** Your windows and apps run inside the Dnui runtime, which
  loads IL. For single-file and native AOT publishes, `Cmdless.Dnui` automatically copies your
  project's IL into `dnui/` next to the executable. Turn that off with
  `<DnuiShipUiAssemblies>false</DnuiShipUiAssemblies>`.

## Testing your UI

There's no headless flag. Headless is a choice your app makes, the same way it is in any
Avalonia app: choose an app whose `Configure` uses Avalonia's headless platform.

```csharp
public class HeadlessTestApp : DnuiCommandApp
{
    protected override void Configure(AppBuilder builder)
    {
        base.Configure(builder);
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions()); // package Avalonia.Headless
    }
}

var form = await new DnuiHost().WithApp<HeadlessTestApp>().OpenWindowAsync<MyForm>();
```

On the CLI that's `--app-assembly <dll> --app-type <HeadlessTestApp>`. Under a headless app nothing
is shown and nothing takes focus, and a tray keeps its state without an OS icon (drive it with
`tray/simulateOpen` and `tray/simulateClick`).

Headless has no file picker: a dialog fails with `DialogUnsupported` (exit 30) unless the app
registers its own `IStorageProviderFactory` in `Configure`, the Avalonia way to supply picks.
For code that only calls dialogs, substitute a fake `IDnuiDialogs` instead.

## Where things live

| Path | Contents |
|---|---|
| `CmdlessRoot` | `~/.cmdless`, or `CMDLESS_ROOT`, or `DnuiRuntimeOptions.CmdlessRoot` (code wins) |
| Package source | nuget.org, or `DNUI_PACKAGE_SOURCE` (a NuGet v3 feed URL or a folder of `.nupkg` files), or `DnuiRuntimeOptions.PackageSource` (code wins) |
| `{CmdlessRoot}/dotnet/packages/{id}/{version}/` | Installed runtime packages (`.dnui-complete` marks a finished install) |
| `{CmdlessRoot}/dotnet/locks/` | Cross-process install locks |

## The runtime

`Dnui.Runtime` is a CLI host:

```bash
Dnui.Runtime [--app-assembly <dll> --app-type <type>] tray
```

- **`tray`** speaks newline-delimited JSON-RPC 2.0 on stdin/stdout
  ([protocol](specs/001-tray-command/contracts/tray-protocol.md)). Closing stdin ends it.
- **Headless**: an app that uses Avalonia's headless platform gets a tray with no OS icon: no
  display, no Dock icon, no focus changes. With `tray/getState`, `tray/simulateOpen`, and
  `tray/simulateClick` it is how automated tests drive a tray.
- **Custom apps**: derive from `DnuiCommandApp` to customize the built-in commands, or from
  `DnuiApp` to take over entirely — then either `Resolve(json, exitCode)` once, or claim
  `Stdio.OpenSession()` and speak your own protocol.

Exit codes and the output contract are in [cli.md](specs/001-tray-command/contracts/cli.md).

## Building

```bash
dotnet build Dnui.slnx
dotnet test Dnui.slnx
```

Pack a runtime for one platform into the local feed `artifacts/packages/`:

```bash
dotnet build Dnui.Runtime -t:PackRuntime -p:RuntimeIdentifier=osx-arm64
```

Point a client at it with `new DnuiHost(new() { Runtime = new() { PackageSource = "artifacts/packages" } })`,
or set `DNUI_PACKAGE_SOURCE=artifacts/packages` (works for the `dnui` tool too). `RuntimePath`
runs a locally built executable directly.

## Releasing

Set `DnuiRuntimeVersion` in `Directory.Build.props`, merge, then tag and push `v<version>`. The
release workflow packs everything, waits for approval in the `nuget` environment, publishes to
nuget.org with trusted publishing (runtime packages first), and creates the GitHub Release.
Details: [release.md](specs/003-tool-publishing/contracts/release.md).

Design documents: [specs/001-tray-command](specs/001-tray-command/),
[specs/002-windows-dialogs](specs/002-windows-dialogs/),
[specs/003-tool-publishing](specs/003-tool-publishing/), and the project
[constitution](.specify/memory/constitution.md).
