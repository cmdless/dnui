using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

public class HostCliTests
{
    [Theory]
    [InlineData("dialog")]
    [InlineData("dialog", "open-file", "--headless")]
    [InlineData("dialog", "open-file", "--outcome", "{\"paths\":null}")]
    [InlineData("dialog", "open-file", "--filter", "bad")]
    [InlineData("window", "only-one-arg")]
    [InlineData("window", "a.dll", "T", "--dialog-outcome", "{\"path\":null}")]
    public async Task UsageErrors_ResolveUsageLine(params string[] args)
    {
        var (code, stdout, _) = await RuntimeProcess.RunAsync(null, args);
        Assert.Equal(2, code);
        Assert.Contains("\"code\":\"Usage\"", Assert.Single(stdout));
    }
}
