using Dnui.Runtime.ContractTests.Support;
using static Dnui.Runtime.ContractTests.Support.RuntimeProcess;

namespace Dnui.Runtime.ContractTests;

/// <summary>US1: dialog host commands, under the headless test app with a fake file picker.</summary>
public class DialogCommandTests
{
    public static TheoryData<string, string, string> Picks => new()
    {
        { "open-file", $"[{Json(PathFor("a"))},{Json(PathFor("b"))}]", "{\"paths\":[" + Json(PathFor("a")) + "," + Json(PathFor("b")) + "]}" },
        { "open-file", "null", """{"paths":null}""" },
        { "open-folder", $"[{Json(PathFor("dir"))}]", "{\"paths\":[" + Json(PathFor("dir")) + "]}" },
        { "save-file", Json(PathFor("x.txt")), "{\"path\":" + Json(PathFor("x.txt")) + "}" },
        { "save-file", "null", """{"path":null}""" },
    };

    [Theory]
    [MemberData(nameof(Picks))]
    public async Task Pick_IsTheSingleResultLine(string kind, string pick, string expected)
    {
        var (code, stdout, _) = await RunAsync(RuntimeProcess.Picks(pick), Headless("dialog", kind, "--title", "T"));
        Assert.Equal(0, code);
        Assert.Equal(expected, Assert.Single(stdout));
    }

    [Fact]
    public async Task NoPicker_IsDialogUnsupported()
    {
        var (code, stdout, _) = await RunAsync(null, WithTestApp("Dnui.TestApps.BareHeadlessApp", "dialog", "open-file"));
        Assert.Equal(30, code);
        Assert.Contains("\"code\":\"DialogUnsupported\"", Assert.Single(stdout));
    }

    [Fact]
    public async Task AppOverride_IsAccepted()
    {
        var (code, stdout, stderr) = await RunAsync(RuntimeProcess.Picks("null"), WithTestApp("Dnui.TestApps.CustomCommandApp", "dialog", "open-folder"));
        Assert.Equal(0, code);
        Assert.Equal("""{"paths":null}""", Assert.Single(stdout));
        Assert.Contains("dnui-test: custom-configure", stderr);
    }

    [Fact]
    public async Task AllOptions_ReachThePicker()
    {
        var (code, stdout, stderr) = await RunAsync(RuntimeProcess.Picks(Json(PathFor("report.pdf"))), Headless("dialog", "save-file", "--title", "Save",
            "--suggested-name", "report", "--default-extension", "pdf", "--filter", "PDF:*.pdf", "--filter", "Text:*.txt,*.md",
            "--start-folder", Path.GetTempPath(), "--no-overwrite-prompt"));
        Assert.Equal(0, code);
        Assert.Single(stdout);
        Assert.Contains("dnui-test: picker save-file title=Save name=report extension=pdf filters=PDF:*.pdf;Text:*.txt,*.md overwritePrompt=False", stderr);
    }
}
