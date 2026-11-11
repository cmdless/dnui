using System.Text.Json.Nodes;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Foundation;

public class ResolveTests
{
    [Fact]
    public void FirstResolveWins()
    {
        var (stdio, _) = TestStdio.Create();
        JsonNode? first = new JsonObject { ["a"] = 1 };
        JsonNode? second = new JsonObject { ["b"] = 2 };

        Assert.True(stdio.TryBeginResolve(ref first));
        Assert.False(stdio.TryBeginResolve(ref second));
        Assert.NotNull(first);
    }

    [Fact]
    public void WriteResultLine_WritesExactlyOneLine()
    {
        var (stdio, output) = TestStdio.Create();
        stdio.WriteResultLine(new JsonObject { ["text"] = "multi\nline it's", ["n"] = 1 });

        var written = TestStdio.ReadOutput(output);
        Assert.EndsWith("\n", written);
        Assert.Single(written.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("multi\nline it's", JsonNode.Parse(written)!["text"]!.GetValue<string>());
        Assert.Contains("it's", written);
    }

    [Fact]
    public void ResolveWhileSessionOpen_DropsResult_KeepsResolution()
    {
        var (stdio, output) = TestStdio.Create();
        stdio.OpenSession();
        JsonNode? result = new JsonObject { ["a"] = 1 };

        Assert.True(stdio.TryBeginResolve(ref result));
        Assert.Null(result);
        Assert.Equal(string.Empty, TestStdio.ReadOutput(output));
    }

    [Fact]
    public void ResolveNull_WritesNothing()
    {
        var (stdio, output) = TestStdio.Create();
        JsonNode? result = null;
        Assert.True(stdio.TryBeginResolve(ref result));
        Assert.Null(result);
        Assert.Equal(string.Empty, TestStdio.ReadOutput(output));
    }
}
