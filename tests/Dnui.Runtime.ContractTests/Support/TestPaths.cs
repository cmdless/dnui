using System.Reflection;

namespace Dnui.Runtime.ContractTests.Support;

internal static class TestPaths
{
    private static string Metadata(string key) =>
        typeof(TestPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    public static string RuntimeExe => Metadata("DnuiRuntimeExe");

    public static string TestAppsDll => Metadata("DnuiTestAppsDll");

    public static string TestAppsExtraDll => Metadata("DnuiTestAppsExtraDll");

    public static string RepoRoot => Metadata("DnuiRepoRoot");

    /// <summary>The local folder feed produced by PackRuntime.</summary>
    public static string PackagesFeed => Path.Combine(RepoRoot, "artifacts", "packages");

    /// <summary>
    /// A local runtime package exists and is not older than the runtime build. A stale package would
    /// install old runtime code (for example one that still takes desktop focus in headless mode).
    /// </summary>
    public static bool HasPackageForCurrentRid
    {
        get
        {
            var package = Path.Combine(PackagesFeed, $"{DnuiRuntime.PackageId}.{DnuiRuntime.Version}.nupkg");
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
    }

    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}

/// <summary>A temp directory deleted on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dnui-ct-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
