using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US5: `--headless` window and dialog commands need no display server.</summary>
public class HeadlessGuardTests
{
    private static readonly Dictionary<string, string?> NoDisplay = new() { ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null };

    [Fact]
    public async Task WindowAndDialog_RunWithoutDisplay()
    {
        var (windowCode, windowOut, _) = await RuntimeProcess.RunAsync(NoDisplay, "window", TestPaths.TestAppsDll, "Dnui.TestApps.ParentOfChildWindow", "--headless");
        Assert.Equal(0, windowCode);
        Assert.Single(windowOut);

        var (dialogCode, dialogOut, _) = await RuntimeProcess.RunAsync(NoDisplay, "dialog", "open-file", "--headless", "--outcome", """{"paths":null}""");
        Assert.Equal(0, dialogCode);
        Assert.Single(dialogOut);
    }
}
