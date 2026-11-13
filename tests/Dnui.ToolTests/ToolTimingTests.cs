using System.Diagnostics;
using Dnui.ToolTests.Support;

namespace Dnui.ToolTests;

/// <summary>SC-003: with a warm cache the tool adds at most 0.5 s over the runtime run directly.</summary>
[Collection(nameof(TimingCollection))]
public class ToolTimingTests(ToolFixture tool)
{
    [Fact]
    public async Task WarmCacheOverhead_IsUnderHalfASecond()
    {
        string[] args = ToolFixture.Headless("dialog", "save-file");
        var pick = ToolFixture.Pick("null");
        var env = tool.FreshEnvironment();
        env[pick.Key] = pick.Value;
        var directEnv = new Dictionary<string, string?> { [pick.Key] = pick.Value };
        await ProcessRunner.RunAsync(tool.ToolExe, args, env, TimeSpan.FromMinutes(3)); // warm the cache and the disk
        await ProcessRunner.RunAsync(ToolFixture.RuntimeExe, args, directEnv, TimeSpan.FromMinutes(1));

        var viaTool = await BestOfAsync(() => ProcessRunner.RunAsync(tool.ToolExe, args, env, TimeSpan.FromMinutes(1)));
        var direct = await BestOfAsync(() => ProcessRunner.RunAsync(ToolFixture.RuntimeExe, args, directEnv, TimeSpan.FromMinutes(1)));

        Assert.True(viaTool - direct <= TimeSpan.FromMilliseconds(500), $"tool {viaTool.TotalMilliseconds} ms vs runtime {direct.TotalMilliseconds} ms");
    }

    private static async Task<TimeSpan> BestOfAsync(Func<Task<RunResult>> run)
    {
        var best = TimeSpan.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            Assert.Equal(0, (await run()).ExitCode);
            best = watch.Elapsed < best ? watch.Elapsed : best;
        }

        return best;
    }
}

[CollectionDefinition(nameof(TimingCollection), DisableParallelization = true)]
public class TimingCollection;
