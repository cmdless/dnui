using System.Diagnostics;
using Dnui.ToolTests.Support;

namespace Dnui.ToolTests;

/// <summary>FR-005 / research R3: ending the tool ends the runtime it started.</summary>
public class ChildLifetimeTests(ToolFixture tool)
{
    [Fact]
    public async Task Unix_Sigterm_IsForwarded_AndToolReturnsRuntimeExit()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix signals only.");
        using var process = await StartIdleTrayAsync();

        using (var kill = Process.Start("kill", ["-TERM", process.Id.ToString()])!)
        {
            await kill.WaitForExitAsync();
        }

        // The runtime holds the same stdout pipe: EOF means both processes are gone.
        await AssertStdoutClosesAsync(process);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Windows_HardKill_EndsRuntime()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows job objects only.");
        using var process = await StartIdleTrayAsync();

        process.Kill(entireProcessTree: false);
        await AssertStdoutClosesAsync(process);
    }

    // A headless tray waits on stdin forever, so it stays alive until something ends it.
    private async Task<Process> StartIdleTrayAsync()
    {
        var process = ProcessRunner.Start(tool.ToolExe, ["tray", "--headless"], tool.FreshEnvironment());
        _ = process.StandardError.ReadToEndAsync();
        await process.StandardInput.BaseStream.WriteAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}"""u8.ToArray().Concat("\n"u8.ToArray()).ToArray());
        await process.StandardInput.BaseStream.FlushAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Assert.NotNull(await process.StandardOutput.ReadLineAsync(cts.Token)); // runtime is up
        return process;
    }

    private static async Task AssertStdoutClosesAsync(Process process)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var rest = await process.StandardOutput.ReadToEndAsync(cts.Token);
        Assert.NotNull(rest);
    }
}
