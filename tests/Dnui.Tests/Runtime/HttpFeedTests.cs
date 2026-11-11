using System.Net;
using Dnui.Acquisition;
using Dnui.Tests.TestSupport;

namespace Dnui.Tests.Runtime;

public class HttpFeedTests
{
    private const string Index = """{"version":"3.0.0","resources":[{"@id":"https://feed.test/flat/","@type":"PackageBaseAddress/3.0.0"}]}""";

    [Fact]
    public async Task UsesFlatContainer_WithLowercaseIdAndVersion()
    {
        using var dir = new TempDir();
        var nupkg = File.ReadAllBytes(FakeNupkg.Write(dir.Path, "Cmdless.Dnui.Runtime.osx-arm64", "1.2.3-Beta"));
        var requested = new List<string>();
        var handler = new StubHandler(request =>
        {
            requested.Add(request.RequestUri!.ToString());
            return request.RequestUri!.AbsolutePath.EndsWith("index.json")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Index) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(nupkg) };
        });

        var source = new HttpPackageSource(new Uri("https://feed.test/v3/index.json"), new HttpClient(handler));
        await using var stream = await source.OpenAsync("Cmdless.Dnui.Runtime.osx-arm64", "1.2.3-Beta", CancellationToken.None);

        Assert.NotNull(stream);
        Assert.Equal("https://feed.test/flat/cmdless.dnui.runtime.osx-arm64/1.2.3-beta/cmdless.dnui.runtime.osx-arm64.1.2.3-beta.nupkg", requested[^1]);
    }

    [Fact]
    public async Task NotFound_IsPackageNotFound()
    {
        using var root = new TempDir();
        var source = Source(path => path.EndsWith("index.json") ? Ok(Index) : new HttpResponseMessage(HttpStatusCode.NotFound));
        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() =>
            DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path }, "linux-x64", source, CancellationToken.None));
        Assert.Equal(DnuiRuntimeFailure.PackageNotFound, ex.Reason);
    }

    [Fact]
    public async Task ServerError_IsDownloadFailed()
    {
        using var root = new TempDir();
        var source = Source(path => path.EndsWith("index.json") ? Ok(Index) : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() =>
            DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path }, "linux-x64", source, CancellationToken.None));
        Assert.Equal(DnuiRuntimeFailure.DownloadFailed, ex.Reason);
    }

    [Fact]
    public async Task NetworkError_IsDownloadFailed()
    {
        using var root = new TempDir();
        var source = Source(_ => throw new HttpRequestException("offline"));
        var ex = await Assert.ThrowsAsync<DnuiRuntimeUnavailableException>(() =>
            DnuiRuntime.ResolveAsync(new() { CmdlessRoot = root.Path }, "linux-x64", source, CancellationToken.None));
        Assert.Equal(DnuiRuntimeFailure.DownloadFailed, ex.Reason);
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static HttpPackageSource Source(Func<string, HttpResponseMessage> respond) =>
        new(new Uri("https://feed.test/v3/index.json"), new HttpClient(new StubHandler(r => respond(r.RequestUri!.AbsolutePath))));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
