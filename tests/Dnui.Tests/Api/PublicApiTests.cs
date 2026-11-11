using System.Reflection;

namespace Dnui.Tests.Api;

/// <summary>Constitution II: the public surface is exactly what the contracts document.</summary>
public class PublicApiTests
{
    [Fact]
    public void PublicTypes_MatchContracts()
    {
        string[] expected =
        [
            // contracts/app-model.md
            "Dnui.DnuiApp", "Dnui.DnuiCommandApp", "Dnui.DnuiStdio", "Dnui.DnuiSession", "Dnui.DnuiStdioException", "Dnui.DnuiStdioFailure",
            // contracts/tray-client.md
            "Dnui.DnuiResult", "Dnui.DnuiTray", "Dnui.DnuiTrayOptions", "Dnui.DnuiTrayItem", "Dnui.DnuiTrayIcon", "Dnui.DnuiTrayState",
            "Dnui.DnuiTrayProtocolException", "Dnui.DnuiTrayNotRunningException",
            // contracts/runtime-acquisition.md
            "Dnui.DnuiRuntime", "Dnui.DnuiRuntimeOptions", "Dnui.DnuiRuntimeUnavailableException", "Dnui.DnuiRuntimeFailure",
            // specs/002-windows-dialogs/contracts/library-api.md
            "Dnui.DnuiWindow", "Dnui.IDnuiDialogs", "Dnui.IDnuiWindowCommands", "Dnui.IDnuiHostCommands",
            "Dnui.DnuiFileFilter", "Dnui.DnuiOpenFileOptions", "Dnui.DnuiOpenFolderOptions", "Dnui.DnuiSaveFileOptions",
            "Dnui.DnuiHost", "Dnui.DnuiHostOptions", "Dnui.DnuiCommandException", "Dnui.DnuiAssemblyNotFoundException",
        ];

        var actual = typeof(DnuiApp).Assembly.GetExportedTypes().Select(t => t.FullName!).Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), actual);
    }

    // Spec 003 FR-021–FR-025: one public way per form, all through the host.
    [Fact]
    public void Host_IsTheOnlyEntryPoint()
    {
        Assert.Null(typeof(DnuiHost).GetMethod("RunAsync"));
        Assert.Null(typeof(DnuiTray).GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void TrayOptions_DescribeOnlyTheTray()
    {
        var names = typeof(DnuiTrayOptions).GetProperties().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["Configure", "Icon", "Items", "ShutdownTimeout", "Tooltip"], names);
        Assert.Equal(typeof(Action<DnuiTray>), typeof(DnuiTrayOptions).GetProperty("Configure")!.PropertyType);
    }

    [Fact]
    public void NoPublicMethod_TakesAnAssembly()
    {
        var offenders = typeof(DnuiApp).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(Assembly)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void RuntimeOptions_IsARecord() =>
        Assert.NotNull(typeof(DnuiRuntimeOptions).GetMethod("<Clone>$"));
}
