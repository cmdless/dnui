namespace Dnui;

/// <summary>How the runtime is located (contracts/runtime-acquisition.md). All properties are optional.</summary>
public sealed class DnuiRuntimeOptions
{
    /// <summary>Overrides the <c>CMDLESS_ROOT</c> environment variable and the <c>~/.cmdless</c> default.</summary>
    public string? CmdlessRoot { get; init; }

    /// <summary>A NuGet v3 service index URL, or a local folder of .nupkg files. Default: nuget.org.</summary>
    public string? PackageSource { get; init; }

    /// <summary>Development/test override: use this runtime executable and skip resolution entirely.</summary>
    public string? RuntimePath { get; init; }

    /// <summary>How long to wait for another process installing the same runtime. Default: 5 minutes.</summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
