using Dnui.Hosting;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Hosting;

public class DnuiTypeLoaderTests
{
    private static string DnuiDll => typeof(DnuiApp).Assembly.Location;

    [Fact]
    public void LoadedAssembly_IsReused_AndTypeChecked()
    {
        var result = DnuiTypeLoader.Load(DnuiDll, "Dnui.DnuiApp", typeof(DnuiApp));
        Assert.Equal(TypeLoadFailure.TypeInvalid, result.Failure); // abstract
        Assert.Same(typeof(DnuiApp).Assembly, DnuiTypeLoader.FindType(typeof(DnuiApp).Assembly, "Dnui.DnuiOpenFileOptions", typeof(object)).Type?.Assembly);
    }

    [Fact]
    public void UnloadablePath_IsAssemblyLoadFailed() =>
        Assert.Equal(TypeLoadFailure.AssemblyLoadFailed, DnuiTypeLoader.Load("/no/such.dll", "X", typeof(DnuiWindow)).Failure);

    [Fact]
    public void MissingType_IsTypeNotFound() =>
        Assert.Equal(TypeLoadFailure.TypeNotFound, DnuiTypeLoader.Load(DnuiDll, "Dnui.Nope", typeof(DnuiWindow)).Failure);

    [Theory]
    [InlineData(typeof(DnuiResult))]     // wrong base
    [InlineData(typeof(DnuiWindow))]     // abstract
    public void WrongOrAbstractType_IsTypeInvalid(Type type) =>
        Assert.Equal(TypeLoadFailure.TypeInvalid, DnuiTypeLoader.CheckType(type, typeof(DnuiWindow)).Failure);

    [Fact]
    public void NoParameterlessCtor_IsTypeInvalid() =>
        Assert.Equal(TypeLoadFailure.TypeInvalid, DnuiTypeLoader.CheckType(typeof(NeedsArgWindow), typeof(DnuiWindow)).Failure);

    [Fact]
    public void OtherDnuiVersion_IsVersionMismatch()
    {
        using var dir = new TempDir();
        var dll = Path.Combine(dir.Path, "Mismatch.dll");
        AssemblyEmitter.EmitAssemblyReferencingDnui(dll, new Version(9, 9, 9, 9));
        Assert.Equal(TypeLoadFailure.VersionMismatch, DnuiTypeLoader.Load(dll, "OtherVersionApp.App", typeof(DnuiWindow)).Failure);
    }

    private sealed class NeedsArgWindow(int x) : DnuiWindow
    {
        public int X => x;
    }
}
