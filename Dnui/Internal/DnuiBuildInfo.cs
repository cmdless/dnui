using System.Reflection;

namespace Dnui.Internal;

internal static class DnuiBuildInfo
{
    private static readonly Lazy<string> s_runtimeVersion = new(() =>
        typeof(DnuiBuildInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "DnuiRuntimeVersion")?.Value
        ?? throw new InvalidOperationException("Dnui assembly is missing DnuiRuntimeVersion metadata."));

    /// <summary>The runtime version this library was built with (lockstep with the runtime packages).</summary>
    public static string RuntimeVersion => s_runtimeVersion.Value;
}
