using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US3/US4: window commands (parented dialogs, child windows), headless.</summary>
public class WindowCommandsTests
{
    private static Task<(int, List<string>, string)> Window(string type, IReadOnlyDictionary<string, string?>? env = null, params string[] outcomes) =>
        RuntimeProcess.RunAsync(env, ["window", TestPaths.TestAppsDll, "Dnui.TestApps." + type, "--headless", .. outcomes.SelectMany(o => new[] { "--dialog-outcome", o })]);

    [Fact]
    public async Task ParentedDialog_ReceivesOutcome()
    {
        var (code, stdout, _) = await Window("PickOneWindow", null, """{"paths":["/x"]}""");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"paths":["/x"]}}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task TwoDialogs_AreServedInRequestOrder_OneAtATime()
    {
        var (code, stdout, _) = await Window("PickTwiceWindow", null, """{"path":"/a"}""", """{"path":"/b"}""");
        Assert.Equal(0, code);
        Assert.Contains("\"results\":[\"/a\",\"/b\"]", Assert.Single(stdout));
        Assert.Contains("\"end:a\",\"end:b\"", stdout[0]);
    }

    [Fact]
    public async Task WindowClosingDuringDialog_CancelsIt()
    {
        // An outer window observes, so the result doesn't race process shutdown.
        var (code, stdout, _) = await Window("ObservesDialogCloseWindow", null, """{"paths":["/q"]}""");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"dialogIsNull":true}}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task MissingOutcome_FailsTheWindow()
    {
        var (code, stdout, _) = await Window("PickOneWindow");
        Assert.Equal(17, code);
        Assert.Contains("WindowFailed", Assert.Single(stdout));
    }

    [Fact]
    public async Task ChildWindow_ResultFlowsToParent()
    {
        var (code, stdout, _) = await Window("ParentOfChildWindow");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"child":{"name":"x"}}}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task ParentClosing_ClosesChild_WithNull()
    {
        // An outer window observes, so the child's result doesn't race process shutdown.
        var (code, stdout, _) = await Window("ObservesParentCloseWindow");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"parentIsNull":true,"childIsNull":true}}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task ChildFromUnloadedAssembly_LoadsInSameProcess()
    {
        var (code, stdout, stderr) = await Window("ParentOfExternalChildWindow", new Dictionary<string, string?> { ["DNUI_TEST_EXTRA"] = TestPaths.TestAppsExtraDll });
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"child":{"extra":true}}}""", Assert.Single(stdout));
        Assert.Contains("extra-loaded=True", stderr);
    }
}
