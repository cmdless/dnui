using System.Text.Json;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>Raw newline-delimited JSON-RPC against a headless `tray` (US1, US2).</summary>
public class TrayProtocolContractTests
{
    private static readonly object[] Items = [new { id = "open", text = "Open" }, new { id = "off", text = "Off", enabled = false }];

    [Fact]
    public async Task Initialize_ThenStateAndSimulation()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
        var (init, _) = await runtime.RequestAsync("initialize", new { protocolVersion = 1, icon = new { png = Convert.ToBase64String(TestPaths.Png) }, tooltip = "tip", items = Items });
        Assert.Equal(DnuiRuntime.Version, init.GetProperty("result").GetProperty("runtimeVersion").GetString());

        var (state, _) = await runtime.RequestAsync("tray/getState");
        var result = state.GetProperty("result");
        Assert.True(result.GetProperty("visible").GetBoolean());
        Assert.True(result.GetProperty("hasIcon").GetBoolean());
        Assert.Equal("tip", result.GetProperty("tooltip").GetString());
        Assert.Equal(2, result.GetProperty("items").GetArrayLength());

        var (_, openNotes) = await runtime.RequestAsync("tray/simulateOpen");
        Assert.Equal("tray/opened", Assert.Single(openNotes).GetProperty("method").GetString());

        var (_, clickNotes) = await runtime.RequestAsync("tray/simulateClick", new { id = "open" });
        var click = Assert.Single(clickNotes);
        Assert.Equal("tray/clicked", click.GetProperty("method").GetString());
        Assert.Equal("open", click.GetProperty("params").GetProperty("id").GetString());

        var (disabled, _) = await runtime.RequestAsync("tray/simulateClick", new { id = "off" });
        Assert.Equal(1012, disabled.GetProperty("error").GetProperty("code").GetInt32());

        // Every stdout line is a JSON-RPC message.
        Assert.All(runtime.AllStdOut, line => Assert.Equal("2.0", JsonDocument.Parse(line).RootElement.GetProperty("jsonrpc").GetString()));
    }

    [Fact]
    public async Task UnsupportedProtocolVersion_IsRejected()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
        var (response, _) = await runtime.RequestAsync("initialize", new { protocolVersion = 7 });
        var error = response.GetProperty("error");
        Assert.Equal(1003, error.GetProperty("code").GetInt32());
        Assert.Equal(7, error.GetProperty("data").GetProperty("requested").GetInt32());
    }

    [Fact]
    public async Task SetItemsAndSetIcon_RoundTrip()
    {
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.Headless("tray"));
        await runtime.RequestAsync("initialize", new { protocolVersion = 1, tooltip = "old", items = Items });

        await runtime.RequestAsync("tray/setItems", new { items = new[] { new { id = "x", text = "X" } } });
        var (state, _) = await runtime.RequestAsync("tray/getState");
        Assert.Equal("x", state.GetProperty("result").GetProperty("items")[0].GetProperty("id").GetString());

        var (dup, _) = await runtime.RequestAsync("tray/setItems", new { items = new[] { new { id = "a", text = "A" }, new { id = "a", text = "B" } } });
        Assert.Equal(1010, dup.GetProperty("error").GetProperty("code").GetInt32());
        (state, _) = await runtime.RequestAsync("tray/getState");
        Assert.Equal("x", state.GetProperty("result").GetProperty("items")[0].GetProperty("id").GetString());

        await runtime.RequestAsync("tray/setIcon", new { icon = new { png = Convert.ToBase64String(TestPaths.Png) } });
        (state, _) = await runtime.RequestAsync("tray/getState");
        Assert.True(state.GetProperty("result").GetProperty("hasIcon").GetBoolean());
        Assert.Equal("old", state.GetProperty("result").GetProperty("tooltip").GetString());

        var (badIcon, _) = await runtime.RequestAsync("tray/setIcon", new { icon = new { path = "/no/such/icon.png" } });
        Assert.Equal(1011, badIcon.GetProperty("error").GetProperty("code").GetInt32());
    }
}
