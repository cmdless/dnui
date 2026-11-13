using System.Text;
using System.Text.Json;
using Dnui.ToolTests.Support;

namespace Dnui.ToolTests;

/// <summary>US1 scenario 4: a tray session works through the tool exactly as with the runtime.</summary>
public class ToolSessionTests(ToolFixture tool)
{
    [Fact]
    public async Task TraySession_FlowsThroughTheTool()
    {
        using var process = ProcessRunner.Start(tool.ToolExe, ToolFixture.Headless("tray"), tool.FreshEnvironment());
        var stderr = process.StandardError.ReadToEndAsync();

        async Task SendAsync(object message)
        {
            await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + "\n"));
            await process.StandardInput.BaseStream.FlushAsync();
        }

        async Task<JsonElement> ReadAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var line = await process.StandardOutput.ReadLineAsync(cts.Token) ?? throw new EndOfStreamException(await stderr);
            return JsonDocument.Parse(line).RootElement.Clone();
        }

        await SendAsync(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = 1, tooltip = "t", items = new[] { new { id = "open", text = "Open" } } } });
        var init = await ReadAsync();
        Assert.Equal(1, init.GetProperty("id").GetInt32());
        Assert.True(init.TryGetProperty("result", out _), init.ToString());

        await SendAsync(new { jsonrpc = "2.0", id = 2, method = "tray/simulateClick", @params = new { id = "open" } });
        var seen = new List<JsonElement> { await ReadAsync(), await ReadAsync() };
        Assert.Contains(seen, m => m.TryGetProperty("method", out var method) && method.GetString() == "tray/clicked");
        Assert.Contains(seen, m => m.TryGetProperty("id", out var id) && id.GetInt32() == 2);

        process.StandardInput.Close();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);
    }
}
