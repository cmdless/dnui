using Dnui.Dialogs;

namespace Dnui.Tests.Dialogs;

public class DialogOptionMappingTests
{
    [Fact]
    public void OpenFile_MapsEveryOption()
    {
        var mapped = DialogRunner.Map(new DnuiOpenFileOptions { Title = "T", AllowMultiple = true, Filters = [new("Images", ["*.png", "*.jpg"])] }, null);
        Assert.Equal("T", mapped.Title);
        Assert.True(mapped.AllowMultiple);
        var type = Assert.Single(mapped.FileTypeFilter!);
        Assert.Equal("Images", type.Name);
        Assert.Equal(["*.png", "*.jpg"], type.Patterns);
    }

    [Fact]
    public void OpenFolder_MapsEveryOption()
    {
        var mapped = DialogRunner.Map(new DnuiOpenFolderOptions { Title = "F", AllowMultiple = true }, null);
        Assert.Equal("F", mapped.Title);
        Assert.True(mapped.AllowMultiple);
    }

    [Theory]
    [InlineData("png", "png")]
    [InlineData(".png", "png")]
    [InlineData(null, null)]
    public void SaveFile_MapsEveryOption(string? extension, string? expected)
    {
        var mapped = DialogRunner.Map(new DnuiSaveFileOptions { Title = "S", SuggestedName = "a", DefaultExtension = extension, Filters = [new("PDF", ["*.pdf"])], OverwritePrompt = false }, null);
        Assert.Equal("S", mapped.Title);
        Assert.Equal("a", mapped.SuggestedFileName);
        Assert.Equal(expected, mapped.DefaultExtension);
        Assert.Equal("PDF", Assert.Single(mapped.FileTypeChoices!).Name);
        Assert.False(mapped.ShowOverwritePrompt);
    }

    [Fact]
    public void NoFilters_MapToNull() => Assert.Null(DialogRunner.Map(new DnuiOpenFileOptions(), null).FileTypeFilter);
}
