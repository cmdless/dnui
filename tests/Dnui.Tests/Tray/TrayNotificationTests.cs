using Dnui.Tests.TestSupport;
using StreamJsonRpc;

namespace Dnui.Tests.Tray;

public class TrayNotificationTests
{
    [Fact]
    public async Task HostEvents_BecomeNotifications_InOrder()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();

        for (var i = 0; i < 500; i++)
        {
            session.Host.RaiseOpened();
            session.Host.RaiseClicked($"id{i}");
        }

        await session.WaitForNotificationsAsync(1000);
        var all = session.Notifications.ToArray();
        Assert.Equal(1000, all.Length);
        for (var i = 0; i < 500; i++)
        {
            Assert.Equal(("tray/opened", (string?)null), all[2 * i]);
            Assert.Equal(("tray/clicked", (string?)$"id{i}"), all[(2 * i) + 1]);
        }
    }

    [Fact]
    public async Task EmptyItems_AreAppliedAsEmpty_ForPlaceholder()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(items: Array.Empty<object>());
        Assert.Empty(session.Host.Last!.Items);
    }

    [Fact]
    public async Task SimulateOpen_NotificationPrecedesResponse()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        await session.CallAsync("tray/simulateOpen");
        await session.WaitForNotificationsAsync(1);
        Assert.Equal(("tray/opened", (string?)null), Assert.Single(session.Notifications));
    }

    [Fact]
    public async Task SimulateClick_NotificationPrecedesResponse()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        await session.CallAsync("tray/simulateClick", new { id = "open" });
        await session.WaitForNotificationsAsync(1);
        Assert.Equal(("tray/clicked", (string?)"open"), Assert.Single(session.Notifications));
    }

    [Theory]
    [InlineData("nope", "unknown")]
    [InlineData("off", "disabled")]
    [InlineData("sep", "separator")]
    [InlineData("parent", "parent")]
    public async Task SimulateClick_FollowsUserRules(string id, string reason)
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(items: new object[]
        {
            new { id = "open", text = "Open" },
            new { id = "off", text = "Off", enabled = false },
            new { id = "sep", separator = true },
            new { id = "parent", text = "More", items = new[] { new { id = "child", text = "Child" } } },
        });

        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.CallAsync("tray/simulateClick", new { id }));
        Assert.Equal(1012, ex.ErrorCode);
        Assert.Contains($"\"reason\":\"{reason}\"", ex.DeserializedErrorData?.ToString());

        await session.CallAsync("tray/simulateClick", new { id = "child" });
        await session.WaitForNotificationsAsync(1);
        Assert.Equal(("tray/clicked", (string?)"child"), Assert.Single(session.Notifications));
    }

    [Fact]
    public async Task SimulateClick_OnPlaceholder_IsUnknown()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(items: Array.Empty<object>());
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.CallAsync("tray/simulateClick", new { id = "" }));
        Assert.Equal(1012, ex.ErrorCode);
    }

    [Fact]
    public async Task GetState_ReturnsCallerItems()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync(png: TestPng.Base64, tooltip: "tip");
        var state = await session.CallAsync("tray/getState");

        Assert.True(state.GetProperty("visible").GetBoolean());
        Assert.True(state.GetProperty("hasIcon").GetBoolean());
        Assert.Equal("tip", state.GetProperty("tooltip").GetString());
        Assert.Equal("open", state.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.False(state.GetProperty("items")[0].TryGetProperty("isParent", out _));
    }
}
