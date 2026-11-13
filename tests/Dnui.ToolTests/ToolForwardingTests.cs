using System.Text.Json;
using Dnui.ToolTests.Support;

namespace Dnui.ToolTests;

/// <summary>US1 / SC-002: the tool's stdout and exit code equal the runtime's.</summary>
public class ToolForwardingTests(ToolFixture tool)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task ColdCache_DownloadsRuntime_AndForwardsResult()
    {
        var env = tool.FreshEnvironment();
        var path = Path.Combine(Path.GetTempPath(), "x.txt");
        var pick = ToolFixture.Pick(JsonSerializer.Serialize(path));
        env[pick.Key] = pick.Value;
        var result = await ProcessRunner.RunAsync(tool.ToolExe, ToolFixture.Headless("dialog", "save-file"), env, Timeout);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal($"{{\"path\":{JsonSerializer.Serialize(path)}}}\n", result.StdOut.Replace("\r\n", "\n"));
        var installed = Path.Combine(env["CMDLESS_ROOT"]!, "dotnet", "packages", DnuiRuntime.PackageId.ToLowerInvariant(), ToolFixture.Version.ToLowerInvariant());
        Assert.True(Directory.Exists(installed), installed);
    }

    [Fact]
    public async Task WarmCache_NeedsNoFeed()
    {
        var env = tool.FreshEnvironment();
        var pick = ToolFixture.Pick("null");
        env[pick.Key] = pick.Value;
        await ProcessRunner.RunAsync(tool.ToolExe, ToolFixture.Headless("dialog", "save-file"), env, Timeout);

        env["DNUI_PACKAGE_SOURCE"] = tool.EmptyFeed;
        var result = await ProcessRunner.RunAsync(tool.ToolExe, ToolFixture.Headless("dialog", "save-file"), env, Timeout);
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("""{"path":null}""", result.StdOut.Trim());
    }

    [Theory]
    [InlineData("--filter", "bad")]                    // invalid filter → Usage (2)
    [InlineData("--title", "with space é ✓ \"q\"")]  // spaces, quotes, non-ASCII survive the hop
    public async Task MatchesRuntimeDirectly(string option, string value)
    {
        string[] args = ToolFixture.Headless("dialog", "save-file", option, value);
        var pick = ToolFixture.Pick(JsonSerializer.Serialize(Path.Combine(Path.GetTempPath(), "with space", "é ✓ \"q\"")));
        var env = tool.FreshEnvironment();
        env[pick.Key] = pick.Value;
        var viaTool = await ProcessRunner.RunAsync(tool.ToolExe, args, env, Timeout);
        var direct = await ProcessRunner.RunAsync(ToolFixture.RuntimeExe, args, new Dictionary<string, string?> { [pick.Key] = pick.Value }, Timeout);

        Assert.Equal(direct.ExitCode, viaTool.ExitCode);
        Assert.Equal(direct.StdOut, viaTool.StdOut);
    }

    [Fact]
    public async Task Version_IsTheLockstepVersion()
    {
        var result = await ProcessRunner.RunAsync(tool.ToolExe, ["--version"], tool.FreshEnvironment(), Timeout);
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(ToolFixture.Version, result.StdOut.Trim());
    }
}
