using System.Text.Json;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>SC-005: stray console output never reaches the protocol stream.</summary>
public class StrayOutputContractTests
{
    [Fact]
    public async Task TenThousandMessages_WithNoise_StayClean()
    {
        const int count = 10_000;
        await using var runtime = RuntimeProcess.Start(RuntimeProcess.WithTestApp("Dnui.TestApps.SpammerCommandApp", "tray"));
        await runtime.RequestAsync("initialize", new { protocolVersion = 1 });

        for (var i = 0; i < count; i++)
        {
            await runtime.SendAsync("tray/setItems", new { items = new[] { new { id = $"i{i}", text = "Item" } } });
        }

        var seen = new HashSet<int>();
        while (seen.Count < count)
        {
            var line = await runtime.ReadLineAsync(60_000);
            Assert.NotNull(line);
            var message = JsonDocument.Parse(line).RootElement;
            Assert.Equal("2.0", message.GetProperty("jsonrpc").GetString());
            Assert.True(message.TryGetProperty("result", out _), line);
            seen.Add(message.GetProperty("id").GetInt32());
        }

        Assert.Contains("noise", runtime.StdErr);
    }
}
