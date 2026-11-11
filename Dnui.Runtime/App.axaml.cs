using Avalonia.Markup.Xaml;

namespace Dnui.Runtime;

/// <summary>The built-in app: runs the command chosen on the command line.</summary>
public partial class App : DnuiCommandApp
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
