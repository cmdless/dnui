using Dnui.Tests.TestSupport;
using StreamJsonRpc;

namespace Dnui.Tests.Tray;

public class TraySetItemsTests
{
    [Fact]
    public async Task SetItems_ReplacesMenu_BeforeResponding()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        await session.CallAsync("tray/setItems", new { items = new[] { new { id = "a", text = "A" }, new { id = "b", text = "B" } } });

        Assert.Equal(["a", "b"], session.Host.Last!.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task DuplicateAcrossSubmenus_IsRejected_AndMenuUnchanged()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        var before = session.Host.Applied.Count;

        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.CallAsync("tray/setItems", new
        {
            items = new object[] { new { id = "x", text = "X" }, new { id = "p", text = "P", items = new[] { new { id = "x", text = "X2" } } } },
        }));

        Assert.Equal(1010, ex.ErrorCode);
        Assert.Contains("\"reason\":\"duplicate-id\"", ex.DeserializedErrorData?.ToString());
        Assert.Equal(before, session.Host.Applied.Count);
        var state = await session.CallAsync("tray/getState");
        Assert.Equal("open", state.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task EmptyList_AppliesEmptyMenu()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        await session.CallAsync("tray/setItems", new { items = Array.Empty<object>() });
        Assert.Empty(session.Host.Last!.Items);
    }

    [Fact]
    public async Task SubmenuChildClick_ReportsChildId()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        await session.CallAsync("tray/setItems", new { items = new[] { new { id = "p", text = "P", items = new[] { new { id = "c", text = "C" } } } } });
        session.Host.RaiseClicked("c");
        await session.WaitForNotificationsAsync(1);
        Assert.Equal(("tray/clicked", (string?)"c"), Assert.Single(session.Notifications));
    }
}
