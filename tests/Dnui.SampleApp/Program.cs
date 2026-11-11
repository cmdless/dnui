using System.Text.Json.Nodes;
using Avalonia;
using Dnui;

// --self-test: open this project's own window with its own app through the generic host
// commands, headless, against the runtime at DNUI_TEST_RUNTIME, and print the result.
try
{
    var host = new DnuiHost(new()
    {
        Runtime = Environment.GetEnvironmentVariable("DNUI_TEST_RUNTIME") is { } path ? new() { RuntimePath = path } : null,
        Headless = args.Contains("--self-test"),
    });

    var result = await host.WithApp<SampleApp>().OpenWindowAsync<SampleWindow>();
    Console.WriteLine(result?.ToJsonString() ?? "null");
    return 0;
}
catch (DnuiAssemblyNotFoundException ex)
{
    Console.WriteLine("assembly-not-found: " + ex.SearchedPaths[^1]);
    return 3;
}

public class SampleWindow : DnuiWindow
{
    public SampleWindow()
    {
        Title = "Dnui sample";
        Opened += (_, _) => Resolve(new JsonObject { ["sample"] = true, ["app"] = Application.Current?.GetType().Name });
    }
}

public class SampleApp : DnuiCommandApp
{
}
