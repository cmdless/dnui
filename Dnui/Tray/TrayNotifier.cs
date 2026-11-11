using System.Threading.Channels;
using StreamJsonRpc;

namespace Dnui.Tray;

/// <summary>
/// Writes notifications in event order through one queue, never blocking the UI thread
/// (research R7). Simulation awaits the write so the notification precedes the response.
/// </summary>
internal sealed class TrayNotifier
{
    private readonly Func<string, object, Task> _send;
    private readonly Channel<(string Method, object Payload, TaskCompletionSource? Written)> _queue =
        Channel.CreateUnbounded<(string, object, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    private Task? _loop;
    private int _pending;

    /// <summary>True when every queued notification has been written (or failed).</summary>
    public bool IsIdle => Volatile.Read(ref _pending) == 0;

    public TrayNotifier(JsonRpc rpc)
        : this((method, payload) => rpc.NotifyWithParameterObjectAsync(method, payload))
    {
    }

    internal TrayNotifier(Func<string, object, Task> send) => _send = send;

    public void Start() => _loop ??= Task.Run(DrainAsync);

    public void Opened() => Enqueue("tray/opened", new { }, null);

    public void Clicked(string id) => Enqueue("tray/clicked", new ClickedNotification(id), null);

    public Task OpenedAndWaitAsync() => EnqueueAndWait("tray/opened", new { });

    public Task ClickedAndWaitAsync(string id) => EnqueueAndWait("tray/clicked", new ClickedNotification(id));

    public void Complete() => _queue.Writer.TryComplete();

    private Task EnqueueAndWait(string method, object payload)
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(method, payload, written);
        return written.Task;
    }

    private void Enqueue(string method, object payload, TaskCompletionSource? written)
    {
        Interlocked.Increment(ref _pending);
        if (!_queue.Writer.TryWrite((method, payload, written)))
        {
            Interlocked.Decrement(ref _pending);
            written?.TrySetException(new ConnectionLostException());
        }
    }

    private async Task DrainAsync()
    {
        await foreach (var (method, payload, written) in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await _send(method, payload).ConfigureAwait(false);
                written?.TrySetResult();
            }
            catch (Exception ex)
            {
                // The connection is gone; the session's completion handles shutdown.
                written?.TrySetException(ex);
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
            }
        }
    }
}
