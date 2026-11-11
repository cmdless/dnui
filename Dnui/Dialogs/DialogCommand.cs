using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Dnui.Commands;
using Dnui.Internal;

namespace Dnui.Dialogs;

/// <summary><c>dialog open-file|open-folder|save-file</c>: a one-shot, unparented host command (research R8, R9).</summary>
internal sealed class DialogCommand(DialogKind kind, object options, bool headless, JsonNode? outcome) : DnuiCommand
{
    public override bool IsSession => false;

    public override int? Prepare(DnuiCommandApp app)
    {
        if (!headless)
        {
            return null;
        }

        // Headless: resolve the supplied outcome without starting any UI.
        var (result, error) = DialogOutcome.Validate(kind, outcome);
        if (result is null)
        {
            app.ResolveInternal(ErrorResults.Error(ErrorResults.Usage, $"--outcome: {error}."), ErrorResults.Usage);
            return ErrorResults.Usage;
        }

        app.ResolveInternal(result, 0);
        return 0;
    }

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
    private static Window CreateOwner()
    {
        var size = OperatingSystem.IsMacOS() ? new Size(640, 480) : new Size(1, 1);
        return new Window
        {
            Width = size.Width,
            Height = size.Height,
            CanResize = false,
            WindowDecorations = WindowDecorations.None,
            Opacity = 0,
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Title = "Dnui",
        };
    }
}
