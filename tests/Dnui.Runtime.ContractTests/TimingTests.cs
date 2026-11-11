using System.Diagnostics;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

public class TimingTests
{
    [Fact]
    public async Task TrayInitialized_WithinTwoSeconds_OfProcessStart()
    {
        var stopwatch = Stopwatch.StartNew();
        await using var runtime = RuntimeProcess.Start("tray", "--headless");
        await runtime.RequestAsync("initialize", new { protocolVersion = 1, icon = new { png = Convert.ToBase64String(TestPaths.Png) } });
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }
}
