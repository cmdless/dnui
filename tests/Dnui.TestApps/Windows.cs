using System.Text.Json.Nodes;
using Avalonia.Threading;

namespace Dnui.TestApps;

public class ResolveOnOpenWindow : DnuiWindow
{
    public ResolveOnOpenWindow() => Opened += (_, _) => Resolve(new JsonObject { ["name"] = "x" });
}

public class CloseOnOpenWindow : DnuiWindow
{
    public CloseOnOpenWindow() => Opened += (_, _) => Close();
}

public class ThrowingCtorWindow : DnuiWindow
{
    public ThrowingCtorWindow() => throw new InvalidOperationException("ctor boom");
}

public class ThrowAfterOpenWindow : DnuiWindow
{
    public ThrowAfterOpenWindow() => Opened += (_, _) => Dispatcher.UIThread.Post(() => throw new InvalidOperationException("later boom"));
}

public class NotAWindow
{
}

public abstract class AbstractWindow : DnuiWindow
{
}
