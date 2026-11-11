using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>US1: dialog host commands, headless.</summary>
public class DialogCommandTests
{
    [Theory]
    [InlineData("open-file", """{"paths":["/a","/b"]}""")]
    [InlineData("open-file", """{"paths":null}""")]
    [InlineData("open-folder", """{"paths":["/dir"]}""")]
    [InlineData("save-file", """{"path":"/x.txt"}""")]
    [InlineData("save-file", """{"path":null}""")]
    public async Task Outcome_IsTheSingleResultLine(string kind, string outcome)
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, "dialog", kind, "--title", "T", "--headless", "--outcome", outcome);
        Assert.Equal(0, code);
        Assert.Equal(outcome, Assert.Single(stdout));
    }

    [Fact]
    public async Task WrongShape_IsUsageError()
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, "dialog", "save-file", "--headless", "--outcome", """{"paths":["/a"]}""");
        Assert.Equal(2, code);
        Assert.Contains("Usage", Assert.Single(stdout));
    }

    [Fact]
    public async Task AppOverride_IsAccepted()
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, "--app-assembly", TestPaths.TestAppsDll, "--app-type", "Dnui.TestApps.CustomCommandApp",
            "dialog", "open-folder", "--headless", "--outcome", """{"paths":null}""");
        Assert.Equal(0, code);
        Assert.Equal("""{"paths":null}""", Assert.Single(stdout));
    }

    [Fact]
    public async Task AllOptions_Parse()
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, "dialog", "save-file", "--title", "Save", "--suggested-name", "report",
            "--default-extension", "pdf", "--filter", "PDF:*.pdf", "--filter", "Text:*.txt,*.md", "--start-folder", "/tmp", "--no-overwrite-prompt",
            "--headless", "--outcome", """{"path":"/tmp/report.pdf"}""");
        Assert.Equal(0, code);
        Assert.Single(stdout);
    }
}
