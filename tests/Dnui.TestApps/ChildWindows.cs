using System.Reflection;
using System.Text.Json.Nodes;

namespace Dnui.TestApps;

public class ParentOfChildWindow : DnuiWindow
{
    public ParentOfChildWindow() => Opened += async (_, _) =>
        Resolve(new JsonObject { ["child"] = await Ui.OpenWindowAsync<ResolveOnOpenWindow>() });
}

public class NeverResolvingWindow : DnuiWindow
{
}

public class ParentClosesChildWindow : DnuiWindow
{
    /// <summary>The child's result task, observed by <see cref="ObservesParentCloseWindow"/>, which outlives this window.</summary>
    public static Task<JsonNode?>? LastChild { get; private set; }

    public ParentClosesChildWindow() => Opened += async (_, _) =>
    {
        LastChild = Ui.OpenWindowAsync<NeverResolvingWindow>();
        await Task.Delay(200);
        Close();
    };
}

/// <summary>Stays open while its child (which owns a grandchild) closes, then reports both results.</summary>
public class ObservesParentCloseWindow : DnuiWindow
{
    public ObservesParentCloseWindow() => Opened += async (_, _) =>
    {
        var parent = await Ui.OpenWindowAsync<ParentClosesChildWindow>();
        var child = await ParentClosesChildWindow.LastChild!;
        Resolve(new JsonObject { ["parentIsNull"] = parent is null, ["childIsNull"] = child is null });
    };
}

public class ParentOfExternalChildWindow : DnuiWindow
{
    public ParentOfExternalChildWindow() => Opened += async (_, _) =>
    {
        var path = Environment.GetEnvironmentVariable("DNUI_TEST_EXTRA")!;
        var result = await Ui.OpenWindowAsync(path, "Dnui.TestApps.Extra.ExtraChildWindow");
        Console.Error.WriteLine($"dnui-test: pid={Environment.ProcessId} extra-loaded={AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Dnui.TestApps.Extra")}");
        Resolve(new JsonObject { ["child"] = result });
    };
}
