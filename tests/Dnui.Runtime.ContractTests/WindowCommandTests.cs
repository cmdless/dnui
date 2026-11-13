using System.Reflection;
using System.Reflection.Metadata;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US2: the window host command, under the headless test app.</summary>
public class WindowCommandTests
{
    private static Task<(int, List<string>, string)> Window(string type, params string[] extra) =>
        RuntimeProcess.RunAsync(null, RuntimeProcess.Headless(["window", TestPaths.TestAppsDll, "Dnui.TestApps." + type, .. extra]));

    [Fact]
    public async Task ResolvingWindow_ReturnsResult()
    {
        var (code, stdout, _) = await Window("ResolveOnOpenWindow");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"name":"x"}}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task ClosedWindow_ReturnsNull()
    {
        var (code, stdout, _) = await Window("CloseOnOpenWindow");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":null}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task XamlWindow_Works()
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(new Dictionary<string, string?> { ["DNUI_TEST_AUTOFILL"] = "Ada" },
            RuntimeProcess.Headless("window", TestPaths.TestAppsDll, "Dnui.TestApps.NameFormWindow"));
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"name":"Ada"}}""", Assert.Single(stdout));
    }

    [Theory]
    [InlineData("ThrowingCtorWindow")]
    [InlineData("ThrowAfterOpenWindow")]
    public async Task FailingWindow_Exits17(string type)
    {
        var (code, stdout, _) = await Window(type);
        Assert.Equal(17, code);
        Assert.Contains("\"code\":\"WindowFailed\"", Assert.Single(stdout));
    }

    [Theory]
    [InlineData("/no/such.dll", "X", 14, "WindowAssemblyLoadFailed")]
    [InlineData(null, "Dnui.TestApps.Missing", 15, "WindowTypeNotFound")]
    [InlineData(null, "Dnui.TestApps.NotAWindow", 16, "WindowTypeInvalid")]
    [InlineData(null, "Dnui.TestApps.AbstractWindow", 16, "WindowTypeInvalid")]
    public async Task BadTypes_HaveDistinctCodes(string? assembly, string type, int exit, string name)
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, RuntimeProcess.Headless("window", assembly ?? TestPaths.TestAppsDll, type));
        Assert.Equal(exit, code);
        Assert.Contains($"\"code\":\"{name}\"", Assert.Single(stdout));
    }

    [Fact]
    public async Task OtherDnuiVersion_Exits13()
    {
        using var dir = new TempDir();
        var dll = Path.Combine(dir.Path, "Mismatch.dll");
        VersionMismatchContractTests.Emit(dll, new Version(9, 9, 9, 9));
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, RuntimeProcess.Headless("window", dll, "OtherVersionApp.App"));
        Assert.Equal(13, code);
        Assert.Contains("DnuiVersionMismatch", Assert.Single(stdout));
    }

    [Fact]
    public async Task AppOverride_FromSameAssembly_Works()
    {
        var (code, stdout, stderr) = await RuntimeProcess.RunAsync(null, "--app-assembly", TestPaths.TestAppsDll, "--app-type", "Dnui.TestApps.CustomCommandApp",
            "window", TestPaths.TestAppsDll, "Dnui.TestApps.ResolveOnOpenWindow");
        Assert.Equal(0, code);
        Assert.Equal("""{"result":{"name":"x"}}""", Assert.Single(stdout));
        Assert.Contains("dnui-test: custom-configure", stderr);
    }
}
