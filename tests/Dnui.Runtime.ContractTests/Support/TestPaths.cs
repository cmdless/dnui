using System.Reflection;

namespace Dnui.Runtime.ContractTests.Support;

internal static class TestPaths
{
    private static string Metadata(string key) =>
        typeof(TestPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    public static string RuntimeExe => Metadata("DnuiRuntimeExe");

    public static string TestAppsDll => Metadata("DnuiTestAppsDll");

    public static string RepoRoot => Metadata("DnuiRepoRoot");

    /// <summary>The local folder feed produced by PackRuntime.</summary>
    public static string PackagesFeed => Path.Combine(RepoRoot, "artifacts", "packages");

    public static bool HasPackageForCurrentRid =>
        Directory.Exists(PackagesFeed) && Directory.EnumerateFiles(PackagesFeed, $"{DnuiRuntime.PackageId}.{DnuiRuntime.Version}.nupkg").Any();

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
