using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Dnui.Commands;

namespace Dnui;

/// <summary>
/// An app that runs the built-in command chosen by the runtime (for example, tray). Derive from it
/// to customize startup (<c>Configure</c>), styles, or resources without taking over the lifecycle.
/// </summary>
public abstract class DnuiCommandApp : DnuiApp
{
    internal DnuiCommand? Command { get; private set; }

    internal void SetCommand(DnuiCommand command) => Command = command;

    internal override void ApplyConfiguration(AppBuilder builder)
    {
        Command?.ConfigureBuilder(builder);
        Configure(builder);
    }

    /// <summary>Runs the command chosen by the runtime. Cannot be overridden.</summary>
    public sealed override void OnFrameworkInitializationCompleted()
    {
        if (Command is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Command.Run(this, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
