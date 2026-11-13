using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US5: window and dialog commands under a headless app need no display server.</summary>
public class HeadlessGuardTests
{
    private static readonly Dictionary<string, string?> NoDisplay = new() { ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null };

    [Fact]
    public async Task WindowAndDialog_RunWithoutDisplay()
    {
        var (windowCode, windowOut, _) = await RuntimeProcess.RunAsync(NoDisplay, RuntimeProcess.Headless("window", TestPaths.TestAppsDll, "Dnui.TestApps.ParentOfChildWindow"));
        Assert.Equal(0, windowCode);
        Assert.Single(windowOut);

        var env = new Dictionary<string, string?>(NoDisplay) { ["DNUI_TEST_PICKS"] = "[null]" };
        var (dialogCode, dialogOut, _) = await RuntimeProcess.RunAsync(env, RuntimeProcess.Headless("dialog", "open-file"));
        Assert.Equal(0, dialogCode);
        Assert.Single(dialogOut);
    }
}
