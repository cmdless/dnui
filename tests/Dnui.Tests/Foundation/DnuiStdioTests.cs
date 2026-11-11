using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Foundation;

public class DnuiStdioTests
{
    [Fact]
    public void OpenSession_SucceedsOnce_ThenAlreadyClaimed()
    {
        var (stdio, _) = TestStdio.Create();
        Assert.False(stdio.IsSessionOpen);

        var session = stdio.OpenSession();
        Assert.NotNull(session.Input);
        Assert.True(stdio.IsSessionOpen);

        var ex = Assert.Throws<DnuiStdioException>(() => stdio.OpenSession());
        Assert.Equal(DnuiStdioFailure.AlreadyClaimed, ex.Reason);
    }

    [Fact]
    public void OpenSession_WhenNotRedirected_Throws()
    {
        var (stdio, _) = TestStdio.Create(redirected: false);
        var ex = Assert.Throws<DnuiStdioException>(() => stdio.OpenSession());
        Assert.Equal(DnuiStdioFailure.NotRedirected, ex.Reason);
        Assert.False(stdio.IsSessionOpen);
    }

    [Fact]
    public void DisposingSessionOutput_DoesNotCloseUnderlyingStream()
    {
        var (stdio, output) = TestStdio.Create();
        var session = stdio.OpenSession();
        session.Output.Write("x"u8);
        session.Output.Dispose();

        Assert.True(output.CanWrite);
        output.WriteByte((byte)'y');
        Assert.Equal("xy", TestStdio.ReadOutput(output));
    }
}
