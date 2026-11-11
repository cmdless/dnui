using Dnui.Tests.TestSupport;
using StreamJsonRpc;

namespace Dnui.Tests.Tray;

public class TrayInitializeTests
{
    [Fact]
    public async Task Initialize_AppliesState_ThenReturnsRuntimeVersion()
    {
        await using var session = new InMemoryTraySession();
        var result = await session.CallAsync("initialize", new { protocolVersion = 1, icon = new { png = TestPng.Base64 }, tooltip = "hi", items = new[] { new { id = "open", text = "Open" } } });

        Assert.Equal("9.8.7", result.GetProperty("runtimeVersion").GetString());
        var state = Assert.Single(session.Host.Applied);
        Assert.Equal("hi", state.Tooltip);
        Assert.True(state.Visible);
        Assert.Equal("open", Assert.Single(state.Items).Id);
    }

    [Fact]
    public async Task UnsupportedVersion_Fails_WithData_AndAppliesNothing()
    {
        await using var session = new InMemoryTraySession();
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.CallAsync("initialize", new { protocolVersion = 2 }));
        Assert.Equal(1003, ex.ErrorCode);
        Assert.Contains("\"requested\":2", ex.DeserializedErrorData?.ToString());
        Assert.Empty(session.Host.Applied);
    }

    [Theory]
    [InlineData("tray/setItems")]
    [InlineData("tray/setIcon")]
    [InlineData("tray/getState")]
    [InlineData("tray/simulateOpen")]
    [InlineData("tray/simulateClick")]
    public async Task TrayRequests_BeforeInitialize_AreNotInitialized(string method)
    {
        await using var session = new InMemoryTraySession();
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.CallAsync(method, new { id = "x", items = Array.Empty<object>() }));
        Assert.Equal(1001, ex.ErrorCode);
    }

    [Fact]
    public async Task SecondInitialize_IsRejected()
    {
        await using var session = new InMemoryTraySession();
        await session.InitializeAsync();
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() => session.InitializeAsync());
        Assert.Equal(1002, ex.ErrorCode);
    }

    [Fact]
    public async Task InitializeWithInvalidItems_AppliesNothing()
    {
        await using var session = new InMemoryTraySession();
        var ex = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
            session.InitializeAsync(items: new[] { new { id = "a", text = "A" }, new { id = "a", text = "B" } }));
        Assert.Equal(1010, ex.ErrorCode);
        Assert.Empty(session.Host.Applied);

        // Still awaiting initialize: a valid one now succeeds.
        await session.InitializeAsync();
    }
}
