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
    public ParentClosesChildWindow() => Opened += async (_, _) =>
    {
        var child = Ui.OpenWindowAsync<NeverResolvingWindow>();
        await Task.Delay(200);
        Close();
        var result = await child;
        Console.Error.WriteLine("dnui-test: child-after-parent-close=" + (result is null ? "null" : "value"));
    };
}

public class ParentOfExternalChildWindow : DnuiWindow
{
    public ParentOfExternalChildWindow() => Opened += async (_, _) =>
    {
        var path = Environment.GetEnvironmentVariable("DNUI_TEST_EXTRA")!;
        var extra = Assembly.LoadFrom(path);
        var result = await Ui.OpenWindowAsync(extra, "Dnui.TestApps.Extra.ExtraChildWindow");
        Console.Error.WriteLine($"dnui-test: pid={Environment.ProcessId} extra-loaded={AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Dnui.TestApps.Extra")}");
        Resolve(new JsonObject { ["child"] = result });
    };
}
