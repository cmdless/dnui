using Avalonia;
using Avalonia.Headless;
using Dnui.Runtime.Cli;

namespace Dnui.Runtime.Platform;

/// <summary>
/// Every `--headless` command runs on Avalonia's headless platform (spec 002 research R10): no
/// display, no Dock icon, and no focus changes on the desktop running the tests. `tray --headless`
/// fakes the OS tray icon on top of it.
/// </summary>
internal static class HeadlessPlatform
{
    public static AppBuilder Use(AppBuilder builder, RuntimeInvocation cli) =>
        cli.Headless && cli.CommandName is RuntimeCommandLine.WindowCommandName or RuntimeCommandLine.DialogCommandName or RuntimeCommandLine.TrayCommandName
            ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            : builder.UsePlatformDetect();
}
