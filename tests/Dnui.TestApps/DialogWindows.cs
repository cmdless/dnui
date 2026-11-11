using System.Text.Json.Nodes;

namespace Dnui.TestApps;

public class PickOneWindow : DnuiWindow
{
    public PickOneWindow() => Opened += async (_, _) =>
    {
        var paths = await Ui.OpenFilesAsync(new() { AllowMultiple = true, Filters = [new("Text", ["*.txt"])] });
        Resolve(new JsonObject { ["paths"] = paths is null ? null : new JsonArray([.. paths.Select(p => (JsonNode?)p)]) });
    };
}

public class PickTwiceWindow : DnuiWindow
{
    public PickTwiceWindow() => Opened += async (_, _) =>
    {
        var order = new List<string>();
        async Task<string?> Pick(string tag)
        {
            order.Add("start:" + tag);
            var path = await Ui.SaveFileAsync(new() { SuggestedName = tag });
            order.Add("end:" + tag);
            return path;
        }

        var results = await Task.WhenAll(Pick("a"), Pick("b"));
        Resolve(new JsonObject
        {
            ["results"] = new JsonArray([.. results.Select(r => (JsonNode?)r)]),
            ["order"] = new JsonArray([.. order.Select(o => (JsonNode?)o)]),
        });
    };
}

public class CloseDuringDialogWindow : DnuiWindow
{
    public CloseDuringDialogWindow() => Opened += async (_, _) =>
    {
        var pending = Ui.OpenFoldersAsync();
        Close();
        var result = await pending;
        Console.Error.WriteLine("dnui-test: dialog-after-close=" + (result is null ? "null" : "value"));
    };
}

/// <summary>Written against IDnuiDialogs only, so it runs on either surface (SC-007).</summary>
public static class SharedComponent
{
    public static async Task<string> DescribeSaveAsync(IDnuiDialogs dialogs) =>
        await dialogs.SaveFileAsync(new() { SuggestedName = "shared", DefaultExtension = "txt" }) ?? "cancelled";
}

public class SharedComponentWindow : DnuiWindow
{
    public SharedComponentWindow() => Opened += async (_, _) =>
        Resolve(new JsonObject { ["described"] = await SharedComponent.DescribeSaveAsync(Ui) });
}
