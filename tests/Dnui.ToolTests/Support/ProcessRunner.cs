using System.Diagnostics;
using System.Text;

namespace Dnui.ToolTests.Support;

public sealed record RunResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Starts processes with redirected stdio. stdout is read as raw UTF-8 bytes so comparisons are exact.</summary>
public static class ProcessRunner
{
    public static Process Start(string file, IEnumerable<string> args, IReadOnlyDictionary<string, string?>? env)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env ?? new Dictionary<string, string?>())
        {
            if (value is null) info.Environment.Remove(key); else info.Environment[key] = value;
        }

        return Process.Start(info)!;
    }

    public static async Task<RunResult> RunAsync(string file, IEnumerable<string> args, IReadOnlyDictionary<string, string?>? env, TimeSpan timeout)
    {
        using var process = Start(file, args, env);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new RunResult(process.ExitCode, await stdout, await stderr);
    }
}
