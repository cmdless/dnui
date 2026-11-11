using System.Diagnostics;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>
/// SC-002. Runs alone (no other runtime processes competing for the machine) and measures a warm
/// start: the first start pays one-time OS costs (file cache, code signing checks, xvfb fonts).
/// </summary>
[Collection(nameof(TimingCollection))]
public class TimingTests
{
    [Fact]
    public async Task TrayInitialized_WithinTwoSeconds_OfProcessStart()
    {
        await StartAndInitializeAsync(); // warm-up, not measured

        var elapsed = await StartAndInitializeAsync();
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"took {elapsed}");
    }

    private static async Task<TimeSpan> StartAndInitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.RequestAsync("initialize", new { protocolVersion = 1, icon = new { png = Convert.ToBase64String(TestPaths.Png) } });
        var elapsed = stopwatch.Elapsed;
        runtime.CloseStdin();
        await runtime.WaitForExitAsync();
        return elapsed;
    }
}

[CollectionDefinition(nameof(TimingCollection), DisableParallelization = true)]
public class TimingCollection
{
}
