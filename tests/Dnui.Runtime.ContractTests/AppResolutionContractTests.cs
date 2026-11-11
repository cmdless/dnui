using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US4: custom app types (constitution test matrix).</summary>
public class AppResolutionContractTests
{
    [Fact]
    public async Task DefaultApp_RunsTray()
    {
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        var (init, _) = await runtime.RequestAsync("initialize", new { protocolVersion = 1 });
        Assert.True(init.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task CustomCommandApp_RunsTray_WithCustomConfigure()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.CustomCommandApp", "tray", "--headless"));
        var (init, _) = await runtime.RequestAsync("initialize", new { protocolVersion = 1 });
        Assert.True(init.TryGetProperty("result", out _));
        runtime.CloseStdin();
        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.Contains("dnui-test: custom-configure", runtime.StdErr);
    }

    [Fact]
    public async Task TakeoverApp_ResolvesSingleResult()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.TakeoverApp"));
        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.Equal("""{"takeover":true}""", Assert.Single(runtime.AllStdOut));
    }

    [Fact]
    public async Task EchoSessionApp_RunsItsOwnProtocol()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.EchoSessionApp"));
        await runtime.WriteLineAsync("hello");
        Assert.Equal("echo: hello", await runtime.ReadLineAsync());
        await runtime.WriteLineAsync("again");
        Assert.Equal("echo: again", await runtime.ReadLineAsync());
        runtime.CloseStdin();
        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.Equal(2, runtime.AllStdOut.Count);
    }

    [Theory]
    [InlineData(false, "/no/such/app.dll", "X", 10, "AppAssemblyLoadFailed")]
    [InlineData(false, null, "Dnui.TestApps.Missing", 11, "AppTypeNotFound")]
    [InlineData(false, null, "Dnui.TestApps.NotAnApp", 12, "AppTypeInvalid")]
    [InlineData(true, "/no/such/app.dll", "X", 10, null)]
    [InlineData(true, null, "Dnui.TestApps.Missing", 11, null)]
    [InlineData(true, null, "Dnui.TestApps.NotAnApp", 12, null)]
    public async Task ResolutionFailures(bool tray, string? assembly, string type, int exitCode, string? errorCode)
    {
        string[] args = ["--app-assembly", assembly ?? TestPaths.TestAppsDll, "--app-type", type, .. (tray ? new[] { "tray", "--headless" } : [])];
        await using var runtime = RuntimeProcess.Start(args);
        Assert.Equal(exitCode, await runtime.WaitForExitAsync());

        if (errorCode is null)
        {
            Assert.Empty(runtime.AllStdOut);
        }
        else
        {
            Assert.Contains($"\"code\":\"{errorCode}\"", Assert.Single(runtime.AllStdOut));
        }

        Assert.DoesNotContain("NotAnApp-initialized", runtime.StdErr);
    }

    [Fact]
    public async Task TakeoverApp_WithCommand_IsUsageError()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.TakeoverApp", "tray"));
        Assert.Equal(2, await runtime.WaitForExitAsync());
        Assert.Empty(runtime.AllStdOut);
    }

    [Fact]
    public async Task EagerSessionClaim_MakesTrayExit22()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.EagerSessionCommandApp", "tray", "--headless"));
        Assert.Equal(22, await runtime.WaitForExitAsync());
        Assert.Empty(runtime.AllStdOut);
    }
}
