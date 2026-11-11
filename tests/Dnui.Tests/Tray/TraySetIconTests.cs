using Dnui.Tests.TestSupport;
using StreamJsonRpc;

namespace Dnui.Tests.Tray;

public class TraySetIconTests
{
    [Fact]
    public async Task SetIcon_AppliesIconAndTooltip()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(tooltip: "old");
        await session.CallAsync("tray/setIcon", new { icon = new { png = TestPng.Base64 }, tooltip = "new" });
        Assert.True(session.Host.Last!.Visible);
        Assert.Equal("new", session.Host.Last.Tooltip);
    }

    [Fact]
    public async Task OmittedTooltip_KeepsPrevious()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(tooltip: "keep");
        await session.CallAsync("tray/setIcon", new { icon = new { png = TestPng.Base64 } });
        Assert.Equal("keep", session.Host.Last!.Tooltip);
    }

    [Fact]
    public async Task BothPathAndPng_Fails_AndIconUnchanged()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(png: TestPng.Base64);
        var icon = session.Host.Last!.Icon;

        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
            session.CallAsync("tray/setIcon", new { icon = new { png = TestPng.Base64, path = "a.png" } }));
        Assert.Equal(1011, ex.ErrorCode);
        Assert.Same(icon, session.Host.Last!.Icon);
    }

    [Fact]
    public async Task UnreadablePath_Fails()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
            session.CallAsync("tray/setIcon", new { icon = new { path = "/no/such.png" } }));
        Assert.Equal(1011, ex.ErrorCode);
    }
}
