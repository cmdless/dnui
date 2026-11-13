using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Dnui.Commands;
using Dnui.Internal;

namespace Dnui.Dialogs;

/// <summary><c>dialog open-file|open-folder|save-file</c>: a one-shot, unparented host command (research R8, R9).</summary>
internal sealed class DialogCommand(DialogKind kind, object options) : DnuiCommand
{
    public override bool IsSession => false;

    public override void Run(DnuiCommandApp app, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var owner = CreateOwner();
        DnuiPlatformHooks.BringToFront?.Invoke();
        owner.Show();
        owner.Activate();
        _ = RunAsync(app, owner);
    }

    private async Task RunAsync(DnuiCommandApp app, Window owner)
    {
        try
        {
            var result = await DialogRunner.RunAsync(owner, kind, options).ConfigureAwait(true);
            app.ResolveInternal(result, 0);
        }
        catch (DialogUnsupportedException ex)
        {
            app.ResolveInternal(ErrorResults.Error(ErrorResults.DialogUnsupported, ex.Message), ErrorResults.DialogUnsupported);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"dnui: dialog failed: {ex}");
            app.ResolveInternal(ErrorResults.Error(1, ex.Message), 1);
        }
    }

    /// <summary>
    /// An invisible owner for the picker (research R9). On macOS pickers show as sheets on their
    /// owner, so the owner is normal-sized and centered so the sheet lands in the right place.
    /// </summary>
    /// <summary>How far above the screen's middle a macOS sheet starts (half the old 480-pixel owner).</summary>
    private const double MacSheetDrop = 240;

    private static Window CreateOwner()
    {
        var owner = new Window
        {
            Width = 1,
            Height = 1,
            CanResize = false,
            WindowDecorations = WindowDecorations.None,
            Opacity = 0,
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Title = "Dnui",
        };

        // A macOS sheet hangs from its owner's top centre, so put that point a little above the
        // middle of the screen and the panel lands roughly centred.
        if (OperatingSystem.IsMacOS() && owner.Screens.Primary is { } screen)
        {
            var area = screen.WorkingArea;
            owner.WindowStartupLocation = WindowStartupLocation.Manual;
            owner.Position = new PixelPoint(
                area.X + area.Width / 2,
                area.Y + area.Height / 2 - (int)(MacSheetDrop * screen.Scaling));
        }

        return owner;
    }
}
