using System.Text.Json.Nodes;
using Dnui.Dialogs;

namespace Dnui.Tests.Dialogs;

public class DialogOutcomeTests
{
    [Theory]
    [InlineData((int)DialogKind.OpenFile, """{"paths":["/a","/b"]}""", true)]
    [InlineData((int)DialogKind.OpenFolder, """{"paths":null}""", true)]
    [InlineData((int)DialogKind.OpenFile, """{"paths":[]}""", false)]
    [InlineData((int)DialogKind.OpenFile, """{"paths":[""]}""", false)]
    [InlineData((int)DialogKind.OpenFile, """{"path":"/a"}""", false)]
    [InlineData((int)DialogKind.OpenFile, """{"paths":["/a"],"x":1}""", false)]
    [InlineData((int)DialogKind.SaveFile, """{"path":"/a"}""", true)]
    [InlineData((int)DialogKind.SaveFile, """{"path":null}""", true)]
    [InlineData((int)DialogKind.SaveFile, """{"path":""}""", false)]
    [InlineData((int)DialogKind.SaveFile, """null""", false)]
    public void ValidatesShape(int kind, string json, bool valid)
    {
        var (result, error) = DialogOutcome.Validate((DialogKind)kind, JsonNode.Parse(json));
        Assert.Equal(valid, result is not null);
        Assert.Equal(valid, error is null);
    }
}
