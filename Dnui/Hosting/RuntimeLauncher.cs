using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dnui.Internal;

namespace Dnui.Hosting;

/// <summary>
/// The one place the library starts the runtime (spec 003 research R1). Every mode resolves the
/// lockstep runtime, grants Windows foreground rights, and ties the child to this process
/// (<see cref="ChildProcessLifetime"/>).
/// </summary>
internal static class RuntimeLauncher
{
    private static readonly JsonSerializerOptions LineOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>One-shot commands: stdin closed, stdout captured as the resolved line.</summary>
    public static async Task<DnuiResult> CaptureAsync(RuntimeLaunch launch, CancellationToken cancellationToken)
    {
        var runtime = await DnuiRuntime.ResolveAsync(launch.Runtime, cancellationToken).ConfigureAwait(false);
        var info = CreateStartInfo(runtime, launch.Arguments());
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.CreateNoWindow = true;

        using var process = Start(info);
        using var tracking = ChildProcessLifetime.Track(process, holdParent: false);
        process.StandardInput.Close();
        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var line = stdout.Trim();
            return new DnuiResult(process.ExitCode, line.Length == 0 ? null : line);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
    }

    /// <summary>Sessions: stdin and stdout piped for a protocol, stderr inherited. Dispose the tracking handle when the process ends.</summary>
    public static async Task<(Process Process, IDisposable Tracking)> StartProtocolAsync(RuntimeLaunch launch, CancellationToken cancellationToken)
    {
        var runtime = await DnuiRuntime.ResolveAsync(launch.Runtime, cancellationToken).ConfigureAwait(false);
        var info = CreateStartInfo(runtime, launch.Arguments());
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.CreateNoWindow = true;

        var process = Start(info);
        return (process, ChildProcessLifetime.Track(process, holdParent: false));
    }

    /// <summary>
    /// The tool: run the runtime with these exact arguments and this process's own stdio, and return
    /// its exit code. A runtime that can't be resolved writes the standard error line and returns 40.
    /// </summary>
    public static async Task<int> PassthroughAsync(IReadOnlyList<string> args, DnuiRuntimeOptions? runtimeOptions, CancellationToken cancellationToken)
    {
        string runtime;
        try
        {
            runtime = await DnuiRuntime.ResolveAsync(runtimeOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (DnuiRuntimeUnavailableException ex)
        {
            await Console.Error.WriteLineAsync($"dnui: {ex.Message}").ConfigureAwait(false);
            WriteStdoutLine(ErrorResults.RuntimeUnavailableError(ex).ToJsonString(LineOptions));
            return ErrorResults.RuntimeUnavailable;
        }

        var launch = new RuntimeLaunch(runtimeOptions, App: null, Command: args);
        using var process = Start(CreateStartInfo(runtime, launch.Arguments()));
        using var tracking = ChildProcessLifetime.Track(process, holdParent: true);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static ProcessStartInfo CreateStartInfo(string runtime, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(runtime) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static Process Start(ProcessStartInfo info)
    {
        AllowForeground();
        return Process.Start(info) ?? throw new InvalidOperationException($"Could not start '{info.FileName}'.");
    }

    // The raw stdout handle, bypassing any Console.Out redirection, so the line is the only output (constitution I).
    private static void WriteStdoutLine(string line)
    {
        using var stdout = Console.OpenStandardOutput();
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        stdout.Write(bytes);
        stdout.Flush();
    }

    // Windows: let the process we're about to start take the foreground (spec 002 research R9).
    // Harmless when this process has no foreground rights to give.
    private static void AllowForeground()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = AllowSetForegroundWindow(-1);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
