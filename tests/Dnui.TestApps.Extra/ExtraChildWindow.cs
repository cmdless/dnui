using System.Text.Json.Nodes;

namespace Dnui.TestApps.Extra;

/// <summary>A child window in an assembly the parent's process hasn't loaded yet.</summary>
public class ExtraChildWindow : DnuiWindow
{
    public ExtraChildWindow() => Opened += (_, _) => Resolve(new JsonObject { ["extra"] = true });
}
