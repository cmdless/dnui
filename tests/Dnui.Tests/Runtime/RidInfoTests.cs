using Dnui.Acquisition;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Runtime;

public class RidInfoTests
{
    [Theory]
    [InlineData("win-x64", "runtime/Dnui.Runtime.exe")]
    [InlineData("win-arm64", "runtime/Dnui.Runtime.exe")]
    [InlineData("osx-x64", "runtime/Dnui.Runtime")]
    [InlineData("osx-arm64", "runtime/Dnui.Runtime")]
    [InlineData("linux-x64", "runtime/Dnui.Runtime")]
    [InlineData("linux-arm64", "runtime/Dnui.Runtime")]
    public void SupportedRids_MapToPackageAndExecutable(string rid, string exe)
    {
        Assert.True(RidInfo.IsSupported(rid));
        Assert.Equal($"Cmdless.Dnui.Runtime.{rid}", RidInfo.PackageId(rid));
        Assert.Equal(exe.Replace('/', Path.DirectorySeparatorChar), RidInfo.ExecutableRelativePath(rid));
    }

    [Fact]
    public async Task UnsupportedRid_FailsWithUnsupportedPlatform()
    {
        using var root = new TempDir();
        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() =>
            DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path }, "linux-musl-x64", new FolderPackageSource(root.Path), CancellationToken.None));
        Assert.Equal(DnuiRuntimeFailure.UnsupportedPlatform, ex.Reason);
        Assert.Equal("Cmdless.Dnui.Runtime.linux-musl-x64", ex.PackageId);
    }
}
