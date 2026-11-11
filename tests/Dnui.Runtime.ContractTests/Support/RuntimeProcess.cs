using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Dnui.Runtime.ContractTests.Support;

/// <summary>Runs the built runtime with redirected stdio. stderr is drained into a buffer.</summary>
internal sealed class RuntimeProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
    private readonly StringBuilder _stderr = new();
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private int _nextId;

    private RuntimeProcess(Process process)
    {
        _process = process;
        _stdoutPump = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    AllStdOut.Add(line);
                    await _lines.Writer.WriteAsync(line);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Closed on purpose by a test.
            }

            _lines.Writer.TryComplete();
        });
        _stderrPump = Task.Run(async () =>
        {
            var buffer = new char[4096];
            int read;
            while ((read = await process.StandardError.ReadAsync(buffer)) > 0)
            {
                lock (_stderr)
                {
                    _stderr.Append(buffer, 0, read);
                }
            }
        });
    }

    public List<string> AllStdOut { get; } = [];

    public string StdErr
    {
        get
        {
            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    public int ProcessId => _process.Id;

    public static RuntimeProcess Start(params string[] args)
    {
        var info = new ProcessStartInfo(TestPaths.RuntimeExe)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        return new RuntimeProcess(Process.Start(info)!);
    }

    public static string[] WithTestApp(string type, params string[] args) =>
        ["--app-assembly", TestPaths.TestAppsDll, "--app-type", type, .. args];

    public async Task WriteLineAsync(string line)
    {
        await _process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        await _process.StandardInput.BaseStream.FlushAsync();
    }

    /// <summary>Sends a JSON-RPC request and returns its id.</summary>
    public async Task<int> SendAsync(string method, object? parameters = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        await WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters ?? new { } }));
        return id;
    }

    /// <summary>Sends a request and waits for its response, collecting any notifications seen first.</summary>
    public async Task<(JsonElement Response, List<JsonElement> Notifications)> RequestAsync(string method, object? parameters = null, int timeoutMs = 15000)
    {
        var id = await SendAsync(method, parameters);
        var notifications = new List<JsonElement>();
        using var cts = new CancellationTokenSource(timeoutMs);
        while (true)
        {
            var line = await _lines.Reader.ReadAsync(cts.Token);
            var message = JsonDocument.Parse(line).RootElement.Clone();
            if (message.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
            {
                return (message, notifications);
            }

            notifications.Add(message);
        }
    }

    public async Task<string?> ReadLineAsync(int timeoutMs = 15000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            return await _lines.Reader.ReadAsync(cts.Token);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void CloseStdin() => _process.StandardInput.Close();

    /// <summary>Stops reading stdout; the runtime's next write fails.</summary>
    public void CloseStdoutReader() => _process.StandardOutput.BaseStream.Dispose();

    /// <summary>Closes both pipe ends without any protocol, as the OS does when a caller crashes.</summary>
    public void ClosePipesAbruptly()
    {
        try { _process.StandardInput.BaseStream.Dispose(); } catch (IOException) { }
        try { _process.StandardOutput.BaseStream.Dispose(); } catch (IOException) { }
    }

    public async Task<int> WaitForExitAsync(int timeoutMs = 15000)
    {
        try
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Runtime did not exit within {timeoutMs} ms. stderr:\n{StdErr}", ex);
        }

        await Task.WhenAll(_stdoutPump, _stderrPump).WaitAsync(TimeSpan.FromSeconds(5));
        return _process.ExitCode;
    }

    public bool HasExited => _process.HasExited;

    public void Kill()
    {
        try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            Kill();
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }
}
