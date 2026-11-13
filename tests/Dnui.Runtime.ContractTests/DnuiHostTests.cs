using Dnui.Runtime.ContractTests.Support;
using Dnui.TestApps;
using static Dnui.Runtime.ContractTests.Support.RuntimeProcess;

namespace Dnui.Runtime.ContractTests;

/// <summary>
/// US1/US2 through the library's host commands (DnuiHost), under the headless test app. Every
/// call is its own runtime process, which inherits this process's environment, so fake picks are
/// set here (<see cref="Picking"/>) and this class runs alone.
/// </summary>
[Collection(nameof(HostPicksCollection))]
public class DnuiHostTests
{
    private static readonly DnuiHost Plain = new(new() { Runtime = new() { RuntimePath = TestPaths.RuntimeExe } });

    private static IDnuiHostCommands Host() => Plain.WithApp<HeadlessApp>();

    /// <summary>Queues fake picks for the runtimes started until disposal.</summary>
    private static IDisposable Picking(params string[] picks)
    {
        Environment.SetEnvironmentVariable(TestStorage.PicksVariable, "[" + string.Join(",", picks) + "]");
        return new Reset();
    }

    private sealed class Reset : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable(TestStorage.PicksVariable, null);
    }

    [Fact]
    public async Task Dialogs_ReturnTypedResults()
    {
        var host = Host();
        using (Picking($"[{Json(PathFor("a"))},{Json(PathFor("b"))}]"))
        {
            Assert.Equal([PathFor("a"), PathFor("b")], await host.OpenFilesAsync(new() { AllowMultiple = true, Filters = [new("Images", ["*.png"])] }));
        }

        using (Picking("null"))
        {
            Assert.Null(await host.OpenFoldersAsync());
        }

        using (Picking(Json(PathFor("s"))))
        {
            Assert.Equal(PathFor("s"), await host.SaveFileAsync(new() { SuggestedName = "s", DefaultExtension = "txt", OverwritePrompt = false }));
        }
    }

    [Fact]
    public async Task HeadlessApp_StartsHeadlessTray_WithNoTraySetting()
    {
        // The app chooses the platform. Without the headless app, a runner with no tray host
        // (Linux CI) would fail to start with TrayHostUnavailable.
        await using var tray = await Host().StartTrayAsync(new() { Icon = DnuiTrayIcon.FromPng(TestPaths.Png), Items = [new("open", "Open")] });
        await tray.SimulateClickAsync("open");
        Assert.True((await tray.GetStateAsync()).HasIcon);
    }

    [Fact]
    public async Task HostRuntimeSetting_AppliesToEveryCommand()
    {
        var missing = new DnuiHost(new() { Runtime = new() { RuntimePath = "/no/such/runtime" } }).WithApp<HeadlessApp>();
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.SaveFileAsync());
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.OpenWindowAsync<ResolveOnOpenWindow>());
        await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => missing.StartTrayAsync(new()));
    }

    [Fact]
    public async Task WithApp_AllFormsRunTheCustomApp()
    {
        var host = Plain;
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
        using var picks = Picking(Json(PathFor("same.txt")));
        var fromHost = await SharedComponent.DescribeSaveAsync(Host());
        var fromWindow = await Host().OpenWindowAsync<SharedComponentWindow>();
        Assert.Equal(fromHost, fromWindow?["described"]?.GetValue<string>());
    }
}

[CollectionDefinition(nameof(HostPicksCollection), DisableParallelization = true)]
public sealed class HostPicksCollection;
