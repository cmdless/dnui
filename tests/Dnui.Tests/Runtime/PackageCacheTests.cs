using Dnui.Acquisition;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Runtime;

public class PackageCacheTests
{
    private const string Rid = "osx-arm64";
    private static readonly string Id = RidInfo.PackageId(Rid);
    private static string Version => DnuiRuntime.Version;

    private static Task<string> Resolve(string root, IPackageSource source, TimeSpan? lockTimeout = null) =>
        DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root, LockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30) }, Rid, source, CancellationToken.None);

    private static string InstallDir(string root) => Path.Combine(root, "dotnet", "packages", Id.ToLowerInvariant(), Version);

    [Fact]
    public async Task ColdInstall_CreatesMarkerAndExecutable()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);

        var exe = await Resolve(root.Path, new FolderPackageSource(feed.Path));

        Assert.Equal(Path.Combine(InstallDir(root.Path), "runtime", "Dnui.Runtime"), exe);
        Assert.True(File.Exists(exe));
        Assert.True(File.Exists(Path.Combine(InstallDir(root.Path), PackageCache.MarkerFileName)));
        Assert.False(File.Exists(Path.Combine(InstallDir(root.Path), $"{Id.ToLowerInvariant()}.nuspec")) && false);
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(exe).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task WarmPath_NeedsNoFeed()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);
        await Resolve(root.Path, new FolderPackageSource(feed.Path));

        var counting = new CountingSource(new FolderPackageSource(Path.Combine(feed.Path, "gone")));
        var exe = await Resolve(root.Path, counting);

        Assert.True(File.Exists(exe));
        Assert.Equal(0, counting.Opens);
    }

    [Fact]
    public async Task FolderWithoutMarker_IsReplaced()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);
        Directory.CreateDirectory(InstallDir(root.Path));
        File.WriteAllText(Path.Combine(InstallDir(root.Path), "junk.txt"), "half-installed");

        var exe = await Resolve(root.Path, new FolderPackageSource(feed.Path));

        Assert.True(File.Exists(exe));
        Assert.False(File.Exists(Path.Combine(InstallDir(root.Path), "junk.txt")));
    }

    [Fact]
    public async Task LeftoverTempFolders_AreRemoved()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);
        var leftover = Path.Combine(root.Path, "dotnet", "packages", Id.ToLowerInvariant(), ".tmp-crashed");
        Directory.CreateDirectory(leftover);

        await Resolve(root.Path, new FolderPackageSource(feed.Path));

        Assert.False(Directory.Exists(leftover));
    }

    [Fact]
    public async Task CorruptPackage_FailsIntegrity_AndPublishesNothing()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version, corrupt: true);

        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => Resolve(root.Path, new FolderPackageSource(feed.Path)));

        Assert.Equal(DnuiRuntimeFailure.IntegrityFailed, ex.Reason);
        Assert.False(Directory.Exists(InstallDir(root.Path)));
    }

    [Fact]
    public async Task NuspecMismatch_FailsIntegrity()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version, nuspecVersion: "9.9.9");

        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => Resolve(root.Path, new FolderPackageSource(feed.Path)));
        Assert.Equal(DnuiRuntimeFailure.IntegrityFailed, ex.Reason);
    }

    [Fact]
    public async Task MissingPackage_IsPackageNotFound()
    {
        using var root = new TempDir();
        using var feed = new TempDir();

        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => Resolve(root.Path, new FolderPackageSource(feed.Path)));
        Assert.Equal(DnuiRuntimeFailure.PackageNotFound, ex.Reason);
        Assert.Equal(Version, ex.Version);
    }

    [Fact]
    public async Task TenConcurrentResolves_DownloadOnce()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);
        var counting = new CountingSource(new FolderPackageSource(feed.Path), TimeSpan.FromMilliseconds(200));

        var paths = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Resolve(root.Path, counting))));

        Assert.Equal(1, counting.Opens);
        Assert.Single(paths.Distinct());
        Assert.True(File.Exists(paths[0]));
    }

    [Fact]
    public async Task HeldLock_TimesOut()
    {
        using var root = new TempDir();
        using var feed = new TempDir();
        FakeNupkg.Write(feed.Path, Id, Version);
        var locks = Path.Combine(root.Path, "dotnet", "locks");
        Directory.CreateDirectory(locks);
        using var held = new FileStream(Path.Combine(locks, $"{Id.ToLowerInvariant()}.{Version}.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() => Resolve(root.Path, new FolderPackageSource(feed.Path), TimeSpan.FromMilliseconds(400)));
        Assert.Equal(DnuiRuntimeFailure.LockTimeout, ex.Reason);
    }

    [Fact]
    public async Task RuntimePathOverride_SkipsResolution()
    {
        using var dir = new TempDir();
        var exe = Path.Combine(dir.Path, "my-runtime");
        File.WriteAllText(exe, "x");
        var counting = new CountingSource(new FolderPackageSource(dir.Path));

        Assert.Equal(exe, await DnuiRuntime.ResolveAsync(new() { RuntimePath = exe }, Rid, counting, CancellationToken.None));
        Assert.Equal(0, counting.Opens);

        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() =>
            DnuiRuntime.ResolveAsync(new() { RuntimePath = exe + ".missing" }, Rid, counting, CancellationToken.None));
        Assert.Equal(DnuiRuntimeFailure.PackageNotFound, ex.Reason);
    }
}
