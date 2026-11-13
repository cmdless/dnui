using System.Diagnostics;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US3: the tray goes away with its caller.</summary>
public class TrayLifecycleContractTests
{
    [Fact]
    public async Task ClosingStdin_ExitsZero_WithinTwoSeconds()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
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
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
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
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
        await runtime.RequestAsync("initialize", new { protocolVersion = 1 });

        var stopwatch = Stopwatch.StartNew();
        runtime.ClosePipesAbruptly();
        await runtime.WaitForExitAsync();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task BrokenStdout_RuntimeExits()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
        await runtime.RequestAsync("initialize", new { protocolVersion = 1, items = new[] { new { id = "a", text = "A" } } });

        runtime.CloseStdoutReader();

        // The runtime notices a gone reader on its next failed write. On Windows the handle stays
        // open until the harness's pending read completes, so the first write can still succeed
        // (it satisfies that read). Keep writing until one fails and the runtime exits.
        for (var attempt = 0; attempt < 25 && !runtime.HasExited; attempt++)
        {
            try
            {
                await runtime.SendAsync("tray/simulateClick", new { id = "a" });
            }
            catch (IOException)
            {
                break; // stdin already closed by the exiting runtime
            }

            await Task.Delay(200);
        }

        await runtime.WaitForExitAsync();
        Assert.True(runtime.HasExited);
        Assert.Contains("a write to stdout failed", runtime.StdErr);
    }
}
