using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dnui.Internal;

namespace Dnui;

/// <summary>
/// The process's stdin and stdout, captured by the runtime before the app is created
/// (<see cref="Console.Out"/> is redirected to stderr). Either resolve a single result through
/// <c>DnuiApp.Resolve</c>, or claim both streams once as a session with <see cref="OpenSession"/>.
/// </summary>
public sealed class DnuiStdio
{
    [ThreadStatic]
    private static DnuiStdio? t_current;

    private static readonly JsonSerializerOptions s_lineOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly object _writeLock = new();
    private int _sessionOpen;
    private int _resolved;

    internal DnuiStdio(Stream input, Stream output, bool isRedirected, string outputKind = "stream")
    {
        OutputKind = outputKind;
        _input = input;
        _output = output;
        IsRedirected = isRedirected;
    }

    /// <summary>The instance being handed to an app under construction (set by the runtime).</summary>
    internal static DnuiStdio? Current
    {
        get => t_current;
        set => t_current = value;
    }

    /// <summary>How stdout was opened, for diagnostics ("raw-handle", "console", or "stream").</summary>
    internal string OutputKind { get; }

    /// <summary>True when stdin and stdout are both redirected (pipes or files).</summary>
    public bool IsRedirected { get; }

    /// <summary>True once a session has been opened.</summary>
    public bool IsSessionOpen => Volatile.Read(ref _sessionOpen) == 1;

    /// <summary>
    /// Claims stdin and stdout for a session protocol. Succeeds at most once per process.
    /// After this, non-null resolve results are dropped; end the session with <c>Resolve(null, code)</c>.
    /// </summary>
    /// <exception cref="DnuiStdioException">Not redirected, or already claimed.</exception>
    public DnuiSession OpenSession()
    {
        if (!IsRedirected)
        {
            throw new DnuiStdioException(DnuiStdioFailure.NotRedirected, "stdin and stdout must both be redirected to open a session.");
        }

        if (Interlocked.CompareExchange(ref _sessionOpen, 1, 0) != 0)
        {
            throw new DnuiStdioException(DnuiStdioFailure.AlreadyClaimed, "stdio was already claimed as a session.");
        }

        return new DnuiSession(_input, new NonClosingStream(_output));
    }

    /// <summary>
    /// First-call guard for resolving. Returns false if already resolved. A non-null result is
    /// replaced with null (and reported on stderr) while a session is open.
    /// </summary>
    internal bool TryBeginResolve(ref JsonNode? result)
    {
        if (Interlocked.Exchange(ref _resolved, 1) != 0)
        {
            return false;
        }

        if (result is not null && IsSessionOpen)
        {
            Console.Error.WriteLine("dnui: a Resolve result was dropped because a stdio session is open.");
            result = null;
        }

        return true;
    }

    /// <summary>Writes one JSON line to the captured stdout and flushes. Never throws on a closed pipe.</summary>
    internal void WriteResultLine(JsonNode result)
    {
        var bytes = Encoding.UTF8.GetBytes(result.ToJsonString(s_lineOptions) + "\n");
        lock (_writeLock)
        {
            try
            {
                _output.Write(bytes);
                _output.Flush();
            }
            catch (IOException)
            {
                // The caller is gone; nothing left to tell.
            }
        }
    }

    /// <summary>
    /// Opens stdout as a plain stream over the process's stdout handle. Console streams silently
    /// ignore broken-pipe errors; a FileStream reports them, so a vanished caller ends a session.
    /// </summary>
    private static Stream? TryOpenRawStdout()
    {
        try
        {
            var handle = OperatingSystem.IsWindows()
                ? new SafeFileHandle(GetStdHandle(StdOutputHandle), ownsHandle: false)
                : new SafeFileHandle(1, ownsHandle: false);
            return handle.IsInvalid ? null : new FileStream(handle, FileAccess.Write, bufferSize: 0);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine($"dnui: could not open raw stdout ({ex.GetType().Name}: {ex.Message}); using the console stream.");
            return null;
        }
    }

    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    /// <summary>A stdio that is not redirected and writes nowhere (designer, tests).</summary>
    internal static DnuiStdio CreateDetached() => new(Stream.Null, Stream.Null, isRedirected: false);

    /// <summary>Captures the console streams and redirects <see cref="Console.Out"/> to stderr.</summary>
    internal static DnuiStdio CreateFromConsole()
    {
        var redirected = Console.IsInputRedirected && Console.IsOutputRedirected;
        var input = Console.OpenStandardInput();
        var raw = Console.IsOutputRedirected ? TryOpenRawStdout() : null;
        var output = raw ?? Console.OpenStandardOutput();
        Console.SetOut(Console.Error);
        return new DnuiStdio(input, output, redirected, raw is null ? "console" : "raw-handle");
    }
}
