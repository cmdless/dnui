using System.Net;
using System.Text.Json;

namespace Dnui.Acquisition;

/// <summary>Opens a .nupkg by id and version. Returns null when the package does not exist.</summary>
internal interface IPackageSource
{
    string Description { get; }

    Task<Stream?> OpenAsync(string id, string version, CancellationToken cancellationToken);
}

internal static class PackageFeed
{
    public const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    internal static HttpClient SharedHttpClient { get; set; } = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static IPackageSource Create(string? source)
    {
        source ??= NuGetOrg;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new HttpPackageSource(uri, SharedHttpClient);
        }

        return new FolderPackageSource(Path.GetFullPath(source));
    }
}

/// <summary>A local folder of .nupkg files, matched case-insensitively.</summary>
internal sealed class FolderPackageSource(string folder) : IPackageSource
{
    public string Description => folder;

    public Task<Stream?> OpenAsync(string id, string version, CancellationToken cancellationToken)
    {
        var wanted = $"{id}.{version}.nupkg";
        var match = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.nupkg").FirstOrDefault(f => string.Equals(Path.GetFileName(f), wanted, StringComparison.OrdinalIgnoreCase))
            : null;
        return Task.FromResult<Stream?>(match is null ? null : File.OpenRead(match));
    }
}

/// <summary>A NuGet v3 feed, using its PackageBaseAddress flat container.</summary>
internal sealed class HttpPackageSource(Uri serviceIndex, HttpClient http) : IPackageSource
{
    private readonly SemaphoreSlim _baseLock = new(1, 1);
    private Uri? _packageBase;

    public string Description => serviceIndex.ToString();

    public async Task<Stream?> OpenAsync(string id, string version, CancellationToken cancellationToken)
    {
        var packageBase = await GetPackageBaseAsync(cancellationToken).ConfigureAwait(false);
        var lowerId = id.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        var url = new Uri(packageBase, $"{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg");

        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Uri> GetPackageBaseAsync(CancellationToken cancellationToken)
    {
        if (_packageBase is { } cached)
        {
            return cached;
        }

        await _baseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_packageBase is null)
            {
                using var response = await http.GetAsync(serviceIndex, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var index = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var resource = index.RootElement.GetProperty("resources").EnumerateArray()
                    .FirstOrDefault(r => r.TryGetProperty("@type", out var type) && type.GetString() is "PackageBaseAddress/3.0.0");
                var address = resource.ValueKind == JsonValueKind.Object ? resource.GetProperty("@id").GetString() : null;
                if (address is null)
                {
                    throw new HttpRequestException($"Feed '{serviceIndex}' has no PackageBaseAddress/3.0.0 resource.");
                }

                _packageBase = new Uri(address.EndsWith('/') ? address : address + "/");
            }

            return _packageBase;
        }
        finally
        {
            _baseLock.Release();
        }
    }
}
