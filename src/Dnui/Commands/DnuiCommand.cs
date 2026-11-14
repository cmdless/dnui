using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Dnui.Commands;

/// <summary>A built-in runtime command run by <see cref="DnuiCommandApp"/>.</summary>
internal abstract class DnuiCommand
{
    /// <summary>True when the command claims stdio as a session (protocol on stdout).</summary>
    public abstract bool IsSession { get; }

    /// <summary>Platform hint applied by the runtime (macOS Dock icon). The library never references platform backends.</summary>
    public virtual bool HideDockIcon => false;

    /// <summary>Synchronous phase before Avalonia starts. Return an exit code to abort without starting.</summary>
    public virtual int? Prepare(DnuiCommandApp app) => null;

    /// <summary>Non-platform builder options, applied before the app's <c>Configure</c>.</summary>
    public virtual void ConfigureBuilder(AppBuilder builder)
    {
    }

    /// <summary>Runs after framework initialization on the UI thread.</summary>
    public abstract void Run(DnuiCommandApp app, IClassicDesktopStyleApplicationLifetime lifetime);
}
