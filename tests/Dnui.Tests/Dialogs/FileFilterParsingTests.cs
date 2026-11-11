using Dnui.Dialogs;

namespace Dnui.Tests.Dialogs;

public class FileFilterParsingTests
{
    [Fact]
    public void ParsesNameAndPatterns()
    {
        var (filter, error) = FileFilterParser.Parse("Images:*.png, *.jpg");
        Assert.Null(error);
        Assert.Equal("Images", filter!.Name);
        Assert.Equal(["*.png", "*.jpg"], filter.Patterns);
        Assert.Equal("Images:*.png,*.jpg", FileFilterParser.Format(filter));
    }

    [Theory]
    [InlineData("Images")]
    [InlineData(":*.png")]
    [InlineData("Images:")]
    [InlineData("Images:*.png,,*.jpg")]
    public void RejectsMalformed(string text) => Assert.NotNull(FileFilterParser.Parse(text).Error);
}
