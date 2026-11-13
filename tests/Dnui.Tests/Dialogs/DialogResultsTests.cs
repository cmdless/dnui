using Dnui.Dialogs;

namespace Dnui.Tests.Dialogs;

public class DialogResultsTests
{
    [Fact]
    public void Shapes_MatchTheContract()
    {
        Assert.Equal("""{"paths":["/a","/b"]}""", DialogResults.Paths(["/a", "/b"]).ToJsonString());
        Assert.Equal("""{"path":"/a"}""", DialogResults.Path("/a").ToJsonString());
        Assert.Equal("""{"paths":null}""", DialogResults.Cancelled(DialogKind.OpenFile).ToJsonString());
        Assert.Equal("""{"paths":null}""", DialogResults.Cancelled(DialogKind.OpenFolder).ToJsonString());
        Assert.Equal("""{"path":null}""", DialogResults.Cancelled(DialogKind.SaveFile).ToJsonString());
    }

    [Fact]
    public void Read_RoundTrips()
    {
        Assert.Equal(["/a", "/b"], DialogResults.ReadPaths(DialogResults.Paths(["/a", "/b"])));
        Assert.Null(DialogResults.ReadPaths(DialogResults.Cancelled(DialogKind.OpenFile)));
        Assert.Equal("/a", DialogResults.ReadPath(DialogResults.Path("/a")));
        Assert.Null(DialogResults.ReadPath(DialogResults.Cancelled(DialogKind.SaveFile)));
    }
}
