# dnui

Cross-platform .NET UI through a **cached, per-platform Avalonia runtime**. Your app references a
small library (`Cmdless.Dnui`); the UI itself runs in a self-contained runtime that is downloaded
once per machine and shared by every app that uses it.

## Show a tray icon

```csharp
using Dnui;

await using var tray = await DnuiTray.StartAsync(new()
{
    Icon = DnuiTrayIcon.FromFile("icon.png"),
    Tooltip = "My App",
    Items = [new("open", "Open"), DnuiTrayItem.Separator, new("quit", "Quit")],
});

tray.Opened += () => Console.WriteLine("menu opened");
tray.Clicked += id => { if (id == "quit") _ = tray.DisposeAsync(); };

var result = await tray.Completion; // DnuiResult(ExitCode, StdOut)
```

The first `StartAsync` on a machine downloads `Cmdless.Dnui.Runtime.<rid>` at exactly the
library's version into the shared cache; every later start reuses it with no network access.

## Where things live

| Path | Contents |
|---|---|
| `CmdlessRoot` | `~/.cmdless`, or `CMDLESS_ROOT`, or `DnuiRuntimeOptions.CmdlessRoot` (code wins) |
| `{CmdlessRoot}/dotnet/packages/{id}/{version}/` | Installed runtime packages (`.dnui-complete` marks a finished install) |
| `{CmdlessRoot}/dotnet/locks/` | Cross-process install locks |

## The runtime

`Dnui.Runtime` is a CLI host:

```bash
Dnui.Runtime [--app-assembly <dll> --app-type <type>] tray [--headless]
```

- **`tray`** speaks newline-delimited JSON-RPC 2.0 on stdin/stdout
  ([protocol](specs/001-tray-command/contracts/tray-protocol.md)). Closing stdin ends it.
- **`--headless`** runs everything but the OS tray icon. With `tray/getState`,
  `tray/simulateOpen`, and `tray/simulateClick` it is how automated tests drive a tray.
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

Point a client at it with `Runtime = new() { PackageSource = "artifacts/packages" }` (or use
`RuntimePath` to run a locally built executable directly).

Design documents: [specs/001-tray-command](specs/001-tray-command/) and the project
[constitution](.specify/memory/constitution.md).
