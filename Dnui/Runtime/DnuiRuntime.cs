using Dnui.Internal;
using Dnui.Acquisition;

namespace Dnui;

/// <summary>
/// Locates the Dnui runtime, downloading <c>Cmdless.Dnui.Runtime.&lt;rid&gt;</c> at exactly
/// <see cref="Version"/> into the shared Cmdless cache the first time (constitution III).
/// </summary>
public static class DnuiRuntime
{
    /// <summary>The environment variable that overrides the Cmdless root.</summary>
    public const string CmdlessRootVariable = "CMDLESS_ROOT";

    /// <summary>The runtime version this library requires (lockstep, exact match).</summary>
    public static string Version => DnuiBuildInfo.RuntimeVersion;

    /// <summary>This platform's runtime identifier, for example <c>osx-arm64</c>.</summary>
    public static string CurrentRid => RidInfo.Current;

    /// <summary>The runtime package id for this platform.</summary>
    public static string PackageId => RidInfo.PackageId(CurrentRid);

    /// <summary>The Cmdless root: the option, else <c>CMDLESS_ROOT</c>, else <c>~/.cmdless</c>. No I/O.</summary>
    public static string GetCmdlessRoot(DnuiRuntimeOptions? options = null)
    {
        var root = options?.CmdlessRoot;
        if (string.IsNullOrEmpty(root))
        {
            root = Environment.GetEnvironmentVariable(CmdlessRootVariable);
        }

        if (string.IsNullOrEmpty(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cmdless");
        }

        return Path.GetFullPath(root);
    }

    /// <summary>The Dnui root, <c>{CmdlessRoot}/dotnet</c>. No I/O.</summary>
    public static string GetDnuiRoot(DnuiRuntimeOptions? options = null) => Path.Combine(GetCmdlessRoot(options), "dotnet");

    /// <summary>
    /// Returns the absolute path of a runnable runtime executable, downloading and installing it
    /// the first time. A cached install is used with no lock, no network, and no re-verification.
    /// </summary>
    /// <exception cref="DnuiRuntimeUnavailableException">The runtime could not be obtained.</exception>
    public static Task<string> ResolveAsync(DnuiRuntimeOptions? options = null, CancellationToken cancellationToken = default) =>
        ResolveAsync(options, CurrentRid, PackageFeed.Create(options?.PackageSource), cancellationToken);

    internal static async Task<string> ResolveAsync(DnuiRuntimeOptions? options, string rid, IPackageSource source, CancellationToken cancellationToken)
    {
        var packageId = RidInfo.PackageId(rid);
        if (options?.RuntimePath is { } explicitPath)
        {
            var full = Path.GetFullPath(explicitPath);
            return File.Exists(full)
                ? full
                : throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.PackageNotFound, packageId, Version, $"RuntimePath '{full}' does not exist.");
        }

        if (!RidInfo.IsSupported(rid))
        {
            throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.UnsupportedPlatform, packageId, Version, $"'{rid}' is not a supported runtime identifier.");
        }

        var exeRelative = RidInfo.ExecutableRelativePath(rid);
        var cache = new PackageCache(GetDnuiRoot(options));
        var installPath = await cache.EnsureInstalledAsync(source, packageId, Version, exeRelative, options?.LockTimeout ?? TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
        return Path.Combine(installPath, exeRelative);
    }
}
