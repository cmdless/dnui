namespace Dnui;

/// <summary>Why the runtime could not be obtained.</summary>
public enum DnuiRuntimeFailure
{
    /// <summary>No runtime package exists for this platform's RID.</summary>
    UnsupportedPlatform,

    /// <summary>The feed has no package with this id and version (or <c>RuntimePath</c> does not exist).</summary>
    PackageNotFound,

    /// <summary>A network or HTTP failure while downloading.</summary>
    DownloadFailed,

    /// <summary>The download failed verification (checksum or manifest mismatch).</summary>
    IntegrityFailed,

    /// <summary>Another process held the install lock longer than <see cref="DnuiRuntimeOptions.LockTimeout"/>.</summary>
    LockTimeout,
}

/// <summary>Thrown when the runtime cannot be resolved or installed.</summary>
public sealed class DnuiRuntimeUnavailableException : Exception
{
    internal DnuiRuntimeUnavailableException(DnuiRuntimeFailure reason, string packageId, string version, string detail, Exception? inner = null)
        : base($"Dnui runtime {packageId} {version} is unavailable ({reason}): {detail}", inner)
    {
        Reason = reason;
        PackageId = packageId;
        Version = version;
    }

    /// <summary>Which failure occurred.</summary>
    public DnuiRuntimeFailure Reason { get; }

    /// <summary>The runtime package id, for example <c>Cmdless.Dnui.Runtime.osx-arm64</c>.</summary>
    public string PackageId { get; }

    /// <summary>The exact runtime version requested.</summary>
    public string Version { get; }
}
