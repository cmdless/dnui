using System.Diagnostics;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US6 against a real packed runtime (artifacts/packages, produced by PackRuntime).</summary>
public class RuntimeAcquisitionContractTests
{
    [Fact]
    public async Task ResolveFromLocalFeed_InstallsRunnableRuntime()
    {
        Assert.SkipUnless(TestPaths.HasPackageForCurrentRid, $"Run PackRuntime for {DnuiRuntime.CurrentRid} first.");
        using var root = new TempDir();

        var exe = await DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path, PackageSource = TestPaths.PackagesFeed });

        Assert.StartsWith(Path.Combine(root.Path, "dotnet", "packages", DnuiRuntime.PackageId.ToLowerInvariant(), DnuiRuntime.Version), exe);
        var info = new ProcessStartInfo(exe, "--version") { RedirectStandardOutput = true };
        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.StartsWith(DnuiRuntime.Version, output);

        // Warm path: well under 100 ms, no feed needed.
        var stopwatch = Stopwatch.StartNew();
        Assert.Equal(exe, await DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path, PackageSource = "/no/feed" }));
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"warm resolve took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task TrayClient_DownloadsAndStarts_TenAtOnce()
    {
        Assert.SkipUnless(TestPaths.HasPackageForCurrentRid, $"Run PackRuntime for {DnuiRuntime.CurrentRid} first.");
        using var root = new TempDir();
        var options = new DnuiTrayOptions
        {
            Runtime = new() { CmdlessRoot = root.Path, PackageSource = TestPaths.PackagesFeed },
            Headless = true,
            Items = [new("open", "Open")],
        };

        var trays = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => DnuiTray.StartAsync(options)));
        try
        {
            Assert.All(trays, t => Assert.Equal(DnuiRuntime.Version, t.RuntimeVersion));
            var installs = Directory.GetDirectories(Path.Combine(root.Path, "dotnet", "packages", DnuiRuntime.PackageId.ToLowerInvariant()));
            Assert.Equal([DnuiRuntime.Version], installs.Select(Path.GetFileName));
        }
        finally
        {
            foreach (var tray in trays)
            {
                await tray.DisposeAsync();
            }
        }
    }
}
