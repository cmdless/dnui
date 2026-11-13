using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

public class HostCliTests
{
    [Theory]
    [InlineData("dialog")]
    [InlineData("dialog", "open-file", "--headless")]                       // removed: headless is the app's choice
    [InlineData("dialog", "open-file", "--outcome", "{\"paths\":null}")]  // removed with outcomes
    [InlineData("dialog", "open-file", "--filter", "bad")]
    [InlineData("window", "only-one-arg")]
    [InlineData("window", "a.dll", "T", "--dialog-outcome", "{\"path\":null}")] // removed with outcomes
    public async Task UsageErrors_ResolveUsageLine(params string[] args)
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, args);
        Assert.Equal(2, code);
        Assert.Contains("\"code\":\"Usage\"", Assert.Single(stdout));
    }
}
