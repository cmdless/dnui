using System.Text.Json.Nodes;
using Dnui.Hosting;

namespace Dnui.Tests.Hosting;

/// <summary>Spec 003 R1: RuntimeLaunch is the only place the runtime command line is assembled.</summary>
public class RuntimeLaunchTests
{
    [Fact]
    public void NoApp_IsCommandOnly() =>
        Assert.Equal(["dialog", "open-file"], new RuntimeLaunch(null, null, ["dialog", "open-file"], Headless: false).Arguments());

    [Fact]
    public void App_PrecedesCommand() =>
        Assert.Equal(
            ["--app-assembly", "/a/My.dll", "--app-type", "My.App", "window", "/w.dll", "W"],
            new RuntimeLaunch(null, new AppSelection("/a/My.dll", "My.App"), ["window", "/w.dll", "W"], Headless: false).Arguments());

    [Fact]
    public void Headless_FollowsCommand_WithOutcomesInOrder() =>
        Assert.Equal(
            ["dialog", "save-file", "--headless", "--outcome", """{"path":"x"}"""],
            new RuntimeLaunch(null, null, ["dialog", "save-file"], Headless: true) { HeadlessOutcomes = [JsonNode.Parse("""{"path":"x"}""")] }.Arguments());

    [Fact]
    public void WindowOutcomes_UseDialogOutcomeOption() =>
        Assert.Equal(
            ["window", "a", "T", "--headless", "--dialog-outcome", "null", "--dialog-outcome", """{"paths":[]}"""],
            new RuntimeLaunch(null, null, ["window", "a", "T"], Headless: true)
            {
                HeadlessOutcomes = [null, JsonNode.Parse("""{"paths":[]}""")],
                OutcomeOption = "--dialog-outcome",
            }.Arguments());

    [Fact]
    public void Outcomes_AreIgnoredWhenNotHeadless() =>
        Assert.Equal(["dialog", "open-file"], new RuntimeLaunch(null, null, ["dialog", "open-file"], Headless: false) { HeadlessOutcomes = [null] }.Arguments());

    [Theory]
    [InlineData("with space")]
    [InlineData("quote \" and 'single'")]
    [InlineData("")]
    [InlineData("ünïcødé ✓")]
    public void Arguments_AreKeptExactly(string value) =>
        Assert.Equal(["dialog", "open-file", "--title", value], new RuntimeLaunch(null, null, ["dialog", "open-file", "--title", value], Headless: false).Arguments());
}
