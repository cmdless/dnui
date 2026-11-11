using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text.Json;
using Dnui.Tray;
using StreamJsonRpc;

namespace Dnui.Tests.TestSupport;

/// <summary>A tray protocol server (TrayProtocolTarget + FakeTrayHost) and a raw JSON-RPC client over in-memory pipes.</summary>
internal sealed class InMemoryTraySession : IAsyncDisposable
{
    private readonly JsonRpc _server;
    private readonly TrayNotifier _notifier;

    public InMemoryTraySession()
    {
        var toServer = new Pipe();
        var toClient = new Pipe();

        _server = TrayProtocol.CreateConnection(toClient.Writer.AsStream(), toServer.Reader.AsStream());
        _notifier = new TrayNotifier(_server);
        Target = new TrayProtocolTarget(new SynchronousUiDispatcher(), _notifier, "9.8.7");
        Target.AttachHost(Host);
        _server.AddLocalRpcTarget(Target);
        _notifier.Start();
        _server.StartListening();

        Client = TrayProtocol.CreateConnection(toServer.Writer.AsStream(), toClient.Reader.AsStream());
        Client.AddLocalRpcTarget(new Recorder(Notifications));
        Client.StartListening();
    }

    public FakeTrayHost Host { get; } = new();

    public TrayProtocolTarget Target { get; }

    public JsonRpc Client { get; }

    /// <summary>Notifications in arrival order: (method, params JSON).</summary>
    public ConcurrentQueue<(string Method, string? Id)> Notifications { get; } = new();

    public Task<JsonElement> CallAsync(string method, object? parameters = null) =>
        Client.InvokeWithParameterObjectAsync<JsonElement>(method, parameters ?? new { });

    public Task InitializeAsync(object? items = null, string? png = null, string? tooltip = "tip") =>
        CallAsync("initialize", new { protocolVersion = 1, icon = png is null ? null : new { png }, tooltip, items = items ?? new[] { new { id = "open", text = "Open" } } });

    public async Task WaitForNotificationsAsync(int count, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (Notifications.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _server.Dispose();
        _notifier.Complete();
        await Task.CompletedTask;
    }

    private sealed class Recorder(ConcurrentQueue<(string, string?)> queue)
    {
        [JsonRpcMethod("tray/opened")]
        public void Opened() => queue.Enqueue(("tray/opened", null));

        [JsonRpcMethod("tray/clicked", UseSingleObjectParameterDeserialization = true)]
        public void Clicked(ClickedNotification n) => queue.Enqueue(("tray/clicked", n.Id));
    }
}
