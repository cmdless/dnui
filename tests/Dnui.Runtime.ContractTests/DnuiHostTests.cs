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
    public async Task RunAsync_ReturnsRawResult()
    {
        var result = await Host().RunAsync(["dialog", "open-file", "--headless", "--outcome", """{"paths":null}"""]);
        Assert.Equal(new DnuiResult(0, """{"paths":null}"""), result);
    }

    [Fact]
    public async Task Windows_AllOverloadsBehaveTheSame()
    {
        var host = Host();
        Assert.Equal("x", (await host.OpenWindowAsync<ResolveOnOpenWindow>())?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.OpenWindowAsync<ResolveOnOpenWindow, CustomCommandApp>())?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.OpenWindowAsync(typeof(ResolveOnOpenWindow)))?["name"]?.GetValue<string>());
        Assert.Equal("x", (await host.OpenWindowAsync(typeof(ResolveOnOpenWindow).Assembly, typeof(ResolveOnOpenWindow).FullName!))?["name"]?.GetValue<string>());
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
