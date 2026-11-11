using Avalonia;
using Dnui.Commands;

namespace Dnui.Runtime.Platform;

/// <summary>Applies platform-backend options requested by a command's hints (constitution IV adapter).</summary>
internal static class PlatformCommandOptions
{
    public static void Apply(AppBuilder builder, DnuiCommand command)
    {
        if (command.HideDockIcon)
        {
            builder.With(new MacOSPlatformOptions { ShowInDock = false });
        }
    }
}
