using System.Text.Json.Nodes;
using Dnui.Runtime.ContractTests.Support;
using Dnui.TestApps;

namespace Dnui.Runtime.ContractTests;

/// <summary>US1/US2 through the library's host commands (DnuiHost), headless.</summary>
public class DnuiHostTests
{
    private static DnuiHost Host(params JsonNode?[] outcomes) => new(new()
    {
        Runtime = new() { RuntimePath = TestPaths.RuntimeExe },
        Headless = true,
        Outcomes = outcomes,
    });

    [Fact]
    public async Task Dialogs_ReturnTypedResults()
    {
        var host = Host(JsonNode.Parse("""{"paths":["/a","/b"]}"""), JsonNode.Parse("""{"paths":null}"""), JsonNode.Parse("""{"path":"/s"}"""));
        Assert.Equal(["/a", "/b"], await host.OpenFilesAsync(new() { AllowMultiple = true, Filters = [new("Images", ["*.png"])] }));
        Assert.Null(await host.OpenFoldersAsync());
        Assert.Equal("/s", await host.SaveFileAsync(new() { SuggestedName = "s", DefaultExtension = "txt", OverwritePrompt = false }));
    }

    [Fact]
    public async Task HeadlessHost_StartsHeadlessTray_WithNoTraySetting()
    {
        // Spec 003 FR-022: headless and runtime settings come only from the host. Without headless,
        // a runner with no tray host (Linux CI) would fail to start with TrayHostUnavailable.
        await using var tray = await Host().StartTrayAsync(new() { Icon = DnuiTrayIcon.FromPng(TestPaths.Png), Items = [new("open", "Open")] });
        await tray.SimulateClickAsync("open");
        Assert.True((await tray.GetStateAsync()).HasIcon);
    }

    [Fact]
    public async Task HostRuntimeSetting_AppliesToEveryCommand()
    {
        var missing = new DnuiHost(new() { Runtime = new() { RuntimePath = "/no/such/runtime" }, Headless = true });
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.SaveFileAsync());
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.OpenWindowAsync<ResolveOnOpenWindow>());
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.StartTrayAsync(new()));
    }

    [Fact]
    public async Task WithApp_AllFormsRunTheCustomApp()
    {
        var host = Host();
        Assert.Equal("x", (await host.WithApp<CustomCommandApp>().OpenWindowAsync<ResolveOnOpenWindow>())?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.WithApp(typeof(CustomCommandApp)).OpenWindowAsync<ResolveOnOpenWindow>())?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.WithApp(TestPaths.TestAppsDll, typeof(CustomCommandApp).FullName!).OpenWindowAsync<ResolveOnOpenWindow>())?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task Windows_AllOverloadsBehaveTheSame()
    {
        var host = Host();
        Assert.Equal("x", (await host.OpenWindowAsync<ResolveOnOpenWindow>())?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.OpenWindowAsync(typeof(ResolveOnOpenWindow)))?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.OpenWindowAsync(TestPaths.TestAppsDll, typeof(ResolveOnOpenWindow).FullName!))?["name"]?.GetValue<string>());
        Assert.Null(await host.OpenWindowAsync<CloseOnOpenWindow>());
    }

    [Fact]
    public async Task FailingWindow_ThrowsCommandException()
    {
        var ex = await Assert.ThrowsAsync<DnuiCommandException>(() => Host().OpenWindowAsync<ThrowingCtorWindow>());
        Assert.Equal("WindowFailed", ex.ErrorCode);
        Assert.Equal(17, ex.Result.ExitCode);
    }

    [Fact]
    public async Task Cancellation_ThrowsCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Host().OpenWindowAsync<ResolveOnOpenWindow>(cts.Token));
    }

    [Fact]
    public async Task SharedComponent_BehavesTheSameOnBothSurfaces()
    {
        // SC-007: the same IDnuiDialogs-based component from the host and from window code.
        var fromHost = await SharedComponent.DescribeSaveAsync(Host(JsonNode.Parse("""{"path":"/same.txt"}""")));
        var fromWindow = await Host(JsonNode.Parse("""{"path":"/same.txt"}""")).OpenWindowAsync<SharedComponentWindow>();
        Assert.Equal(fromHost, fromWindow?["described"]?.GetValue<string>());
    }
}
