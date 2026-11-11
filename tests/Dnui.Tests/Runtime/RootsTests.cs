namespace Dnui.Tests.Runtime;

[Collection(nameof(EnvironmentCollection))]
public class RootsTests
{
    [Fact]
    public void Default_IsUserProfileDotCmdless()
    {
        using var _ = new EnvVar(DnuiRuntime.CmdlessRootVariable, null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cmdless");
        Assert.Equal(expected, DnuiRuntime.GetCmdlessRoot());
        Assert.Equal(Path.Combine(expected, "dotnet"), DnuiRuntime.GetDnuiRoot());
    }

    [Fact]
    public void EnvironmentVariable_OverridesDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "from-env");
        using var _ = new EnvVar(DnuiRuntime.CmdlessRootVariable, root);
        Assert.Equal(Path.GetFullPath(root), DnuiRuntime.GetCmdlessRoot());
    }

    [Fact]
    public void CodeOption_WinsOverEnvironmentVariable()
    {
        var fromCode = Path.Combine(Path.GetTempPath(), "from-code");
        using var _ = new EnvVar(DnuiRuntime.CmdlessRootVariable, Path.Combine(Path.GetTempPath(), "from-env"));
        Assert.Equal(Path.Combine(Path.GetFullPath(fromCode), "dotnet"), DnuiRuntime.GetDnuiRoot(new() { CmdlessRoot = fromCode }));
    }

    private sealed class EnvVar : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvVar(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}

[CollectionDefinition(nameof(EnvironmentCollection), DisableParallelization = true)]
public class EnvironmentCollection
{
}
