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
            "Dnui.DnuiTrayStartException", "Dnui.DnuiTrayProtocolException", "Dnui.DnuiTrayNotRunningException",
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
}
