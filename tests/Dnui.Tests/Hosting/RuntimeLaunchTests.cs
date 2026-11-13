using Dnui.Hosting;

namespace Dnui.Tests.Hosting;

/// <summary>Spec 003 R1: RuntimeLaunch is the only place the runtime command line is assembled.</summary>
public class RuntimeLaunchTests
{
    [Fact]
    public void NoApp_IsCommandOnly() =>
        Assert.Equal(["dialog", "open-file"], new RuntimeLaunch(null, null, ["dialog", "open-file"]).Arguments());

    [Fact]
    public void App_PrecedesCommand() =>
        Assert.Equal(
            ["--app-assembly", "/a/My.dll", "--app-type", "My.App", "window", "/w.dll", "W"],
            new RuntimeLaunch(null, new AppSelection("/a/My.dll", "My.App"), ["window", "/w.dll", "W"]).Arguments());

    [Theory]
    [InlineData("with space")]
    [InlineData("quote \" and 'single'")]
    [InlineData("")]
    [InlineData("ünïcødé ✓")]
    public void Arguments_AreKeptExactly(string value) =>
        Assert.Equal(["dialog", "open-file", "--title", value], new RuntimeLaunch(null, null, ["dialog", "open-file", "--title", value]).Arguments());
}
