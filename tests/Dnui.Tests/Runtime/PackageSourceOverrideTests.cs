using Dnui.Acquisition;

namespace Dnui.Tests.Runtime;

/// <summary>Spec 003 R5/R10: DNUI_PACKAGE_SOURCE precedence, and prerelease versions in cache paths.</summary>
[Collection(nameof(EnvironmentCollection))]
public class PackageSourceOverrideTests
{
    [Fact]
    public void CodeOption_BeatsEnvironment()
    {
        using var env = new EnvScope(DnuiRuntime.PackageSourceVariable, "/from/env");
        Assert.Equal("/from/code", DnuiRuntime.GetPackageSource(new() { PackageSource = "/from/code" }));
    }

    [Fact]
    public void Environment_BeatsDefault()
    {
        using var env = new EnvScope(DnuiRuntime.PackageSourceVariable, "/from/env");
        Assert.Equal("/from/env", DnuiRuntime.GetPackageSource(null));
    }

    [Fact]
    public void Default_IsNull_MeaningNuGetOrg()
    {
        using var env = new EnvScope(DnuiRuntime.PackageSourceVariable, null);
        Assert.Null(DnuiRuntime.GetPackageSource(new()));
    }

    [Fact]
    public void PrereleaseVersion_IsLowercasedInCachePath()
    {
        var cache = new PackageCache("/root");
        Assert.Equal(
            Path.Combine("/root", "packages", "cmdless.dnui.runtime.osx-arm64", "0.1.0-preview.1"),
            cache.GetInstallPath("Cmdless.Dnui.Runtime.osx-arm64", "0.1.0-Preview.1"));
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
