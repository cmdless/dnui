using System.Runtime.InteropServices;

namespace Dnui.Acquisition;

/// <summary>Supported runtime identifiers and the package/executable naming for each.</summary>
internal static class RidInfo
{
    public const string PackageIdPrefix = "Cmdless.Dnui.Runtime.";

    public static readonly IReadOnlyList<string> Supported = ["win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"];

    public static string Current => RuntimeInformation.RuntimeIdentifier;

    public static bool IsSupported(string rid) => Supported.Contains(rid, StringComparer.Ordinal);

    public static string PackageId(string rid) => PackageIdPrefix + rid;

    /// <summary>Executable path inside an extracted package, for example <c>runtime/Dnui.Runtime.exe</c>.</summary>
    public static string ExecutableRelativePath(string rid) =>
        Path.Combine("runtime", rid.StartsWith("win-", StringComparison.Ordinal) ? "Dnui.Runtime.exe" : "Dnui.Runtime");
}
