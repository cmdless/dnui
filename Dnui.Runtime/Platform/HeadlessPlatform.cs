using Avalonia;
using Avalonia.Headless;
using Dnui.Runtime.Cli;

namespace Dnui.Runtime.Platform;

/// <summary>
/// `window --headless` runs on Avalonia's headless platform (spec 002 research R10). Other
/// commands use the real platform (`tray --headless` keeps a real platform and fakes only the tray).
/// </summary>
internal static class HeadlessPlatform
{
    public static AppBuilder Use(AppBuilder builder, RuntimeInvocation cli) =>
        cli.Headless && cli.CommandName is RuntimeCommandLine.WindowCommandName or RuntimeCommandLine.DialogCommandName
            ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            : builder.UsePlatformDetect();
}
