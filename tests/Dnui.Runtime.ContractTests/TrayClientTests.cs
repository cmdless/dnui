using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US5: the .NET tray client, public API only, headless.</summary>
public class TrayClientTests
{
    internal static readonly IDnuiHostCommands Host = new DnuiHost(new() { Runtime = new() { RuntimePath = TestPaths.RuntimeExe } }).WithApp<Dnui.TestApps.HeadlessApp>();

    internal static DnuiTrayOptions Options(params DnuiTrayItem[] items) => new()
    {
        Icon = DnuiTrayIcon.FromPng(TestPaths.Png),
        Tooltip = "tip",
        Items = items.Length == 0 ? [new("open", "Open"), DnuiTrayItem.Separator, new("off", "Off") { Enabled = false }] : items,
    };

    [Fact]
    public async Task Start_Simulate_Events_State_Dispose()
    {
        await using var tray = await Host.StartTrayAsync(Options());
        Assert.Equal(DnuiRuntime.Version, tray.RuntimeVersion);

        var events = new List<string>();
        tray.Opened += () => events.Add("opened");
        tray.Clicked += id => events.Add("clicked:" + id);

        await tray.SimulateOpenAsync();
        Assert.Equal(["opened"], events);
        await tray.SimulateClickAsync("open");
        Assert.Equal(["opened", "clicked:open"], events);

        await tray.SetItemsAsync([new("a", "A") { Checked = true }]);
        var state = await tray.GetStateAsync();
        Assert.True(state.Visible);
        Assert.Equal("tip", state.Tooltip);
        Assert.Equal(new DnuiTrayItem("a", "A") { Checked = true }, Assert.Single(state.Items) with { Items = null });

        await tray.DisposeAsync();
        Assert.Equal(new DnuiResult(0, null), await tray.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ProtocolErrors_SurfaceAsProtocolException_AndTrayKeepsRunning()
    {
        await using var tray = await Host.StartTrayAsync(Options());

        var dup = await Assert.ThrowsAsync<DnuiTrayProtocolException>(() => tray.SetItemsAsync([new("a", "A"), new("a", "B")]));
        Assert.Equal(1010, dup.Code);
        Assert.Equal("duplicate-id", dup.Data?.GetProperty("reason").GetString());

        var disabled = await Assert.ThrowsAsync<DnuiTrayProtocolException>(() => tray.SimulateClickAsync("off"));
        Assert.Equal(1012, disabled.Code);

        Assert.NotNull(await tray.GetStateAsync());
    }

    [Fact]
    public async Task RuntimeKilled_CompletionReportsExitCode_AndCallsFail()
    {
        await using var tray = await Host.StartTrayAsync(Options());
        var state = await tray.GetStateAsync();
        Assert.NotNull(state);

        KillRuntime(tray);
        var result = await tray.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(result.StdOut);

        await Assert.ThrowsAsync<DnuiTrayNotRunningException>(() => tray.SetItemsAsync([]));
    }

    [Fact]
    public async Task StartFailure_CarriesExitCode()
    {
        // Spec 003 R12 #9: tray start failures are DnuiCommandException, like every host command.
        var ex = await Assert.ThrowsAsync<DnuiCommandException>(() =>
            Host.WithApp(TestPaths.TestAppsDll, "Dnui.TestApps.NotAnApp").StartTrayAsync(Options()));
        Assert.Equal(new DnuiResult(12, null), ex.Result);
        Assert.Equal("AppTypeInvalid", ex.ErrorCode);
    }

    [Fact]
    public async Task CancelDuringStart_ThrowsCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Host.StartTrayAsync(Options(), cts.Token));
    }

    [Fact]
    public async Task ThrowingHandler_IsReported_ConnectionStaysUp()
    {
        await using var tray = await Host.StartTrayAsync(Options());
        Exception? reported = null;
        tray.UnhandledHandlerException += ex => reported = ex;
        tray.Opened += () => throw new InvalidOperationException("boom");

        await tray.SimulateOpenAsync();
        Assert.IsType<InvalidOperationException>(reported);
        await tray.SimulateClickAsync("open");
    }

    private static void KillRuntime(DnuiTray tray)
    {
        var field = typeof(DnuiTray).GetField("_process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((System.Diagnostics.Process)field.GetValue(tray)!).Kill();
    }
}
