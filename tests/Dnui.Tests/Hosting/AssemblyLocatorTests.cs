using Dnui.Hosting;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Hosting;

public class AssemblyLocatorTests
{
    private static readonly System.Reflection.Assembly Self = typeof(AssemblyLocatorTests).Assembly;

    [Fact]
    public void UsesLocation_WhenPresent() =>
        Assert.Equal(Self.Location, AssemblyLocator.Find(Self, "/nowhere", a => a.Location));

    [Fact]
    public void FallsBackToSiblingDll_ThenDnuiFolder()
    {
        using var dir = new TempDir();
        var name = Self.GetName().Name!;
        var dnui = Path.Combine(dir.Path, "dnui", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dnui)!);
        File.WriteAllText(dnui, "x");
        Assert.Equal(dnui, AssemblyLocator.Find(Self, dir.Path, _ => string.Empty));

        var sibling = Path.Combine(dir.Path, name + ".dll");
        File.WriteAllText(sibling, "x");
        Assert.Equal(sibling, AssemblyLocator.Find(Self, dir.Path, _ => string.Empty));
    }

    [Fact]
    public void NothingFound_ListsAllSearchedPaths()
    {
        using var dir = new TempDir();
        var ex = Assert.Throws<DnuiAssemblyNotFoundException>(() => AssemblyLocator.Find(Self, dir.Path, _ => string.Empty));
        Assert.Equal(2, ex.SearchedPaths.Count);
        Assert.EndsWith(Path.Combine("dnui", Self.GetName().Name + ".dll"), ex.SearchedPaths[^1]);
        Assert.Contains("dnui", ex.Message);
    }
}
