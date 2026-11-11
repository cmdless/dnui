using System.Text.Json;
using Dnui.ToolTests.Support;

namespace Dnui.ToolTests;

/// <summary>FR-004: a runtime that can't be resolved → the standard error line and exit 40.</summary>
public class ToolFailureTests(ToolFixture tool)
{
    [Theory]
    [InlineData("dialog", "open-file")]
    [InlineData("tray", "--headless")]
    public async Task EmptyFeed_ResolvesRuntimeUnavailable(string command, string option)
    {
        var result = await ProcessRunner.RunAsync(tool.ToolExe, [command, option], tool.FreshEnvironment(tool.EmptyFeed), TimeSpan.FromMinutes(2));

        Assert.Equal(40, result.ExitCode);
        var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var error = Assert.Single(lines);
        var root = JsonDocument.Parse(error).RootElement.GetProperty("error");
        Assert.Equal("RuntimeUnavailable", root.GetProperty("code").GetString());
        Assert.Contains("PackageNotFound", root.GetProperty("message").GetString());
        Assert.Contains("PackageNotFound", result.StdErr);
    }
}
