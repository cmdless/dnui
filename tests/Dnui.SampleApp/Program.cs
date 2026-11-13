using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Headless;
using Dnui;

// --self-test: open this project's own window with its own app through the generic host
// commands, headless, against the runtime at DNUI_TEST_RUNTIME, and print the result.
try
{
    if (args.Contains("--self-test"))
    {
        // The runtime inherits this, and SampleApp then chooses Avalonia's headless platform.
        Environment.SetEnvironmentVariable(SampleApp.HeadlessVariable, "1");
    }

    var host = new DnuiHost(new()
    {
        Runtime = Environment.GetEnvironmentVariable("DNUI_TEST_RUNTIME") is { } path ? new() { RuntimePath = path } : null,
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
    public const string HeadlessVariable = "DNUI_SAMPLE_HEADLESS";

    protected override void Configure(AppBuilder builder)
    {
        base.Configure(builder);
        if (Environment.GetEnvironmentVariable(HeadlessVariable) == "1")
        {
            builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
        }
    }
}
