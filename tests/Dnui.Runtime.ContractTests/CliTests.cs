using System.Text.Json;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

public class CliTests
{
    [Fact]
    public async Task UnknownOption_WithoutCommand_ResolvesUsageError()
    {
        await using var runtime = RuntimeProcess.Start("--bogus");
        Assert.Equal(2, await runtime.WaitForExitAsync());
        var line = Assert.Single(runtime.AllStdOut);
        Assert.Equal("Usage", JsonDocument.Parse(line).RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TrayParseError_WritesNothing()
    {
        await using var runtime = RuntimeProcess.Start("tray", "--bogus");
        Assert.Equal(2, await runtime.WaitForExitAsync());
        Assert.Empty(runtime.AllStdOut);
    }

    [Fact]
    public async Task AppTypeWithoutAssembly_IsUsageError()
    {
        await using var runtime = RuntimeProcess.Start("--app-type", "X");
        Assert.Equal(2, await runtime.WaitForExitAsync());
        Assert.Contains("\"Usage\"", Assert.Single(runtime.AllStdOut));
    }

    [Fact]
    public async Task BuiltInAppWithoutCommand_IsUsageError()
    {
        await using var runtime = RuntimeProcess.Start();
        Assert.Equal(2, await runtime.WaitForExitAsync());
        Assert.Contains("\"Usage\"", Assert.Single(runtime.AllStdOut));
    }

    [Fact]
    public async Task Version_PrintsRuntimeVersion()
    {
        await using var runtime = RuntimeProcess.Start("--version");
        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.StartsWith(DnuiRuntime.Version, Assert.Single(runtime.AllStdOut));
    }
}
