using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dnui.ToolTests.Support;

[assembly: AssemblyFixture(typeof(ToolFixture))]

namespace Dnui.ToolTests.Support;

/// <summary>
/// Packs the tool and installs it from a temp feed that also holds this RID's runtime package, the
/// way a user would install it (FR-018). Each run gets a fresh, empty CMDLESS_ROOT by default.
/// </summary>
public sealed class ToolFixture : IAsyncLifetime
{
    private static string Metadata(string key) =>
        typeof(ToolFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    public static string RepoRoot => Metadata("DnuiRepoRoot");

    public static string RuntimeExe => Metadata("DnuiRuntimeExe");

    public static string Version => Metadata("DnuiRuntimeVersion");

    private static string Configuration => Metadata("DnuiConfiguration");

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "dnui-tool-" + Guid.NewGuid().ToString("N"));

    public string Feed => Path.Combine(Root, "feed");

    public string EmptyFeed => Path.Combine(Root, "empty-feed");

    public string ToolExe => Path.Combine(Root, "tools", OperatingSystem.IsWindows() ? "dnui.exe" : "dnui");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Feed);
        Directory.CreateDirectory(EmptyFeed);

        var rid = RuntimeInformation.RuntimeIdentifier;
        var runtimePackage = $"Cmdless.Dnui.Runtime.{rid}.{Version}.nupkg";
        var packed = Path.Combine(RepoRoot, "artifacts", "packages", runtimePackage);
        if (!IsCurrent(packed))
        {
            // Missing or older than the runtime build: pack a private copy, so the tool never installs
            // stale runtime code and nothing shared is rewritten while other test projects read it.
            var artifacts = Path.Combine(Root, "artifacts");
            await RunChecked("dotnet", ["build", Path.Combine(RepoRoot, "Dnui.Runtime"), "-t:PackRuntime", $"-p:RuntimeIdentifier={rid}", $"-p:DnuiArtifactsDir={artifacts}"]);
            packed = Path.Combine(artifacts, "packages", runtimePackage);
        }

        File.Copy(packed, Path.Combine(Feed, runtimePackage));
        await RunChecked("dotnet", ["pack", Path.Combine(RepoRoot, "Dnui.Tool"), "-c", Configuration, "--no-build", "-o", Feed]);

        // A private NuGet cache so a re-packed tool at the same version is never served stale.
        await RunChecked("dotnet", ["tool", "install", "--tool-path", Path.Combine(Root, "tools"), "--add-source", Feed, "--ignore-failed-sources",
            "--version", Version, "Cmdless.Dnui.Tool"], new() { ["NUGET_PACKAGES"] = Path.Combine(Root, "nuget") });
    }

    public ValueTask DisposeAsync()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A local runtime package is current when it is newer than the runtime and library binaries the
    /// tests were built with (the runtime build output is the reference).
    /// </summary>
    public static bool IsCurrent(string package)
    {
        if (!File.Exists(package))
        {
            return false;
        }

        var built = Path.GetDirectoryName(RuntimeExe)!;
        var newestBinary = new[] { "Dnui.Runtime.dll", "Dnui.dll" }
            .Select(name => Path.Combine(built, name))
            .Where(File.Exists)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        return File.GetLastWriteTimeUtc(package) >= newestBinary;
    }

    /// <summary>A fresh CMDLESS_ROOT with the feed as the package source.</summary>
    public Dictionary<string, string?> FreshEnvironment(string? packageSource = null) => new()
    {
        ["CMDLESS_ROOT"] = Path.Combine(Root, "cmdless-" + Guid.NewGuid().ToString("N")),
        ["DNUI_PACKAGE_SOURCE"] = packageSource ?? Feed,
    };

    private static async Task RunChecked(string file, string[] args, Dictionary<string, string?>? env = null)
    {
        var result = await ProcessRunner.RunAsync(file, args, env, TimeSpan.FromMinutes(10));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{file} {string.Join(' ', args)} failed ({result.ExitCode}):\n{result.StdOut}\n{result.StdErr}");
        }
    }
}
