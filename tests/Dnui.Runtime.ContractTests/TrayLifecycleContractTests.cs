using System.Diagnostics;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US3: the tray goes away with its caller.</summary>
public class TrayLifecycleContractTests
{
    [Fact]
    public async Task ClosingStdin_ExitsZero_WithinTwoSeconds()
    {
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.RequestAsync("initialize", new { protocolVersion = 1 });
        var linesBefore = runtime.AllStdOut.Count;

        var stopwatch = Stopwatch.StartNew();
        runtime.CloseStdin();
        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
        Assert.Equal(linesBefore, runtime.AllStdOut.Count);
    }

    [Fact]
    public async Task RequestsSentBeforeClosingStdin_StillGetResponses()
    {
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.SendAsync("initialize", new { protocolVersion = 1, items = new[] { new { id = "a", text = "A" } } });
        await runtime.SendAsync("tray/simulateClick", new { id = "a" });
        await runtime.SendAsync("tray/getState");
        runtime.CloseStdin();

        Assert.Equal(0, await runtime.WaitForExitAsync());
        Assert.Equal(4, runtime.AllStdOut.Count); // 3 responses + 1 notification
    }

    [Fact]
    public async Task CallerDisappears_PipesClosedAbruptly_RuntimeExits()
    {
        // When a caller crashes, the OS closes its ends of both pipes; do exactly that.
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.RequestAsync("initialize", new { protocolVersion = 1 });

        var stopwatch = Stopwatch.StartNew();
        runtime.ClosePipesAbruptly();
        await runtime.WaitForExitAsync();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task BrokenStdout_RuntimeExits()
    {
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.RequestAsync("initialize", new { protocolVersion = 1, items = new[] { new { id = "a", text = "A" } } });

        runtime.CloseStdoutReader();
        await runtime.SendAsync("tray/simulateClick", new { id = "a" });
        await runtime.WaitForExitAsync();
        Assert.True(runtime.HasExited);
    }
}
