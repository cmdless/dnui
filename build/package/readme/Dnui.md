# Cmdless.Dnui

Cross-platform UI for any .NET app through a cached, per-platform Avalonia runtime: system tray
icons, native file and folder pickers, and your own Avalonia windows, each in its own process.
Your app references no UI platform backends; the matching runtime package
(`Cmdless.Dnui.Runtime.<rid>`) is downloaded on first use into `~/.cmdless`.

```csharp
using Dnui;

var files = await DnuiHost.Default.OpenFilesAsync(new() { Title = "Pick files", AllowMultiple = true });
var result = await DnuiHost.Default.WithApp<MyApp>().OpenWindowAsync<MyWindow>();
```

Docs and source: https://github.com/cmdless/dnui
