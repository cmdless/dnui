using System.Collections.Concurrent;

namespace Dnui.Runtime.ContractTests;

/// <summary>Spec 003 FR-020 / SC-008: handlers attached in Configure never miss an event.</summary>
public class TrayConfigureTests
{
    [Fact]
    public async Task HandlersFromConfigure_SeeEventsRaisedImmediatelyAfterStart()
    {
        for (var i = 0; i < 100; i++)
        {
            var clicks = new ConcurrentQueue<string>();
            await using var tray = await TrayClientTests.Host.StartTrayAsync(TrayClientTests.Options() with
            {
                Configure = t => t.Clicked += clicks.Enqueue,
            });

            await tray.SimulateClickAsync("open");
            Assert.Equal(["open"], clicks);
        }
    }

    [Fact]
    public async Task TrayMethodInsideConfigure_FailsStart_AndStopsRuntime()
    {
        DnuiTray? created = null;
        var ex = await Assert.ThrowsAsync<DnuiTrayNotRunningException>(() => TrayClientTests.Host.StartTrayAsync(TrayClientTests.Options() with
        {
            Configure = t =>
            {
                created = t;
                t.SetItemsAsync([]).GetAwaiter().GetResult();
            },
        }));

        Assert.Contains("hasn't started", ex.Message);
        Assert.NotNull(created);
        await created!.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ThrowingConfigure_IsRethrown_AndStopsRuntime()
    {
        DnuiTray? created = null;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TrayClientTests.Host.StartTrayAsync(TrayClientTests.Options() with
        {
            Configure = t =>
            {
                created = t;
                throw new InvalidOperationException("configure failed");
            },
        }));

        Assert.Equal("configure failed", ex.Message);
        await created!.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
