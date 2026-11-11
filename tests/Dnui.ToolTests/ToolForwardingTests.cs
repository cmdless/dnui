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
        var result = await ProcessRunner.RunAsync(tool.ToolExe, ["dialog", "save-file", "--headless", "--outcome", """{"path":"/tmp/x.txt"}"""], env, Timeout);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("{\"path\":\"/tmp/x.txt\"}\n", result.StdOut.Replace("\r\n", "\n"));
        var installed = Path.Combine(env["CMDLESS_ROOT"]!, "dotnet", "packages", DnuiRuntime.PackageId.ToLowerInvariant(), ToolFixture.Version.ToLowerInvariant());
        Assert.True(Directory.Exists(installed), installed);
    }

    [Fact]
    public async Task WarmCache_NeedsNoFeed()
    {
        var env = tool.FreshEnvironment();
        await ProcessRunner.RunAsync(tool.ToolExe, ["dialog", "save-file", "--headless", "--outcome", """{"path":"a"}"""], env, Timeout);

        env["DNUI_PACKAGE_SOURCE"] = tool.EmptyFeed;
        var result = await ProcessRunner.RunAsync(tool.ToolExe, ["dialog", "save-file", "--headless", "--outcome", """{"path":"b"}"""], env, Timeout);
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("""{"path":"b"}""", result.StdOut.Trim());
    }

    [Theory]
    [InlineData("""{"paths":[]}""")]                       // wrong shape → Usage (2)
    [InlineData("""{"path":"/tmp/with space/é ✓ \"q\""}""")] // spaces, quotes, non-ASCII survive
    public async Task MatchesRuntimeDirectly(string outcome)
    {
        string[] args = ["dialog", "save-file", "--headless", "--outcome", outcome];
        var viaTool = await ProcessRunner.RunAsync(tool.ToolExe, args, tool.FreshEnvironment(), Timeout);
        var direct = await ProcessRunner.RunAsync(ToolFixture.RuntimeExe, args, null, Timeout);

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
