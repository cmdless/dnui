using Dnui.Tests.TestSupport;
using Dnui.Tray;

namespace Dnui.Tests.Tray;

public class TrayIconDecoderTests
{
    [Fact]
    public void Png_Base64_Decodes()
    {
        var (png, error) = TrayIconDecoder.Load(new TrayIconSource { Png = TestPng.Base64 });
        Assert.Null(error);
        Assert.Equal(TestPng.Bytes, png);
    }

    [Fact]
    public void Path_Reads_File()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dnui-icon-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(file, TestPng.Bytes);
        try
        {
            var (png, error) = TrayIconDecoder.Load(new TrayIconSource { Path = file });
            Assert.Null(error);
            Assert.Equal(TestPng.Bytes, png);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ExactlyOneOfPathOrPng(bool withPath, bool withPng)
    {
        var (png, error) = TrayIconDecoder.Load(new TrayIconSource { Path = withPath ? "x.png" : null, Png = withPng ? TestPng.Base64 : null });
        Assert.Null(png);
        Assert.Contains("exactly one", error);
    }

    [Fact]
    public void UnreadableFile_Fails() =>
        Assert.Contains("cannot read", TrayIconDecoder.Load(new TrayIconSource { Path = "/no/such/icon.png" }).Error);

    [Fact]
    public void BadBase64_Fails() =>
        Assert.Contains("base64", TrayIconDecoder.Load(new TrayIconSource { Png = "%%%" }).Error);

    [Fact]
    public void NotAPng_Fails() =>
        Assert.Contains("not a PNG", TrayIconDecoder.Load(new TrayIconSource { Png = Convert.ToBase64String("hello world"u8.ToArray()) }).Error);
}
