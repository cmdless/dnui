using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dnui.PublishTests;

/// <summary>
/// SC-009 / FR-003d: a single-project app opens its own window and app through the generic host
/// commands in every publish mode. Native AOT needs a native toolchain, so it runs only when
/// DNUI_TEST_AOT=1 (the Linux CI job sets it).
/// </summary>
public class PublishModeTests
{
    private static string Metadata(string key) => typeof(PublishModeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    private static string RepoRoot => Metadata("DnuiRepoRoot");

    private static string RuntimeExe => Metadata("DnuiRuntimeExe");

    private static string Exe(string dir) => Path.Combine(dir, OperatingSystem.IsWindows() ? "Dnui.SampleApp.exe" : "Dnui.SampleApp");

    [Fact]
    public async Task SingleFile_ShipsIl_AndOpensOwnWindow()
    {
        var dir = await PublishAsync("-p:PublishSingleFile=true", "--self-contained");
        AssertShipped(dir);
        Assert.Equal("""{"sample":true,"app":"SampleApp"}""", await RunSelfTestAsync(dir, expectExit: 0));
    }

    [Fact]
    public async Task SingleFile_WithShippingOff_FailsClearly()
    {
        var dir = await PublishAsync("-p:PublishSingleFile=true", "--self-contained", "-p:DnuiShipUiAssemblies=false");
        Assert.False(Directory.Exists(Path.Combine(dir, "dnui")));
        var output = await RunSelfTestAsync(dir, expectExit: 3);
        Assert.StartsWith("assembly-not-found:", output);
        Assert.EndsWith(Path.Combine("dnui", "Dnui.SampleApp.dll"), output);
    }

    [Fact]
    public async Task NativeAot_ShipsIl_AndOpensOwnWindow()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("DNUI_TEST_AOT") == "1", "Set DNUI_TEST_AOT=1 (needs the native AOT toolchain).");
        var dir = await PublishAsync("-p:PublishAot=true");
        AssertShipped(dir);
        Assert.Equal("""{"sample":true,"app":"SampleApp"}""", await RunSelfTestAsync(dir, expectExit: 0));
    }

    private static void AssertShipped(string dir)
    {
        var dnui = Path.Combine(dir, "dnui");
        Assert.True(File.Exists(Path.Combine(dnui, "Dnui.SampleApp.dll")));
        Assert.True(File.Exists(Path.Combine(dnui, "Dnui.SampleApp.deps.json")));
        var shipped = Directory.EnumerateFiles(dnui, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
        Assert.DoesNotContain(shipped, f => f!.StartsWith("Avalonia", StringComparison.Ordinal) || f == "Dnui.dll" || f!.StartsWith("System.", StringComparison.Ordinal));
    }

    private static async Task<string> PublishAsync(params string[] extra)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dnui-publish-" + Guid.NewGuid().ToString("N"));
        string[] args = ["publish", Path.Combine(RepoRoot, "tests", "Dnui.SampleApp"), "-c", "Release", "-r", RuntimeInformation.RuntimeIdentifier, "-o", dir, .. extra];
        var (code, output) = await RunAsync("dotnet", args, env: null, timeout: TimeSpan.FromMinutes(10));
        Assert.True(code == 0, $"dotnet publish failed ({code}):\n{output}");
        return dir;
    }

    private static async Task<string> RunSelfTestAsync(string dir, int expectExit)
    {
        var (code, output) = await RunAsync(Exe(dir), ["--self-test"], new() { ["DNUI_TEST_RUNTIME"] = RuntimeExe }, TimeSpan.FromMinutes(2));
        Assert.True(code == expectExit, $"sample exited {code}:\n{output}");
        return output.Trim().Split('\n')[^1].Trim();
    }

    internal static async Task<(int Code, string Output)> RunAsync(string file, string[] args, Dictionary<string, string>? env, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env ?? [])
        {
            info.Environment[key] = value;
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(timeout);
        return (process.ExitCode, await stdout + await stderr);
    }
}
