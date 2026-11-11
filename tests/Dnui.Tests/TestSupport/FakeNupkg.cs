using System.IO.Compression;
using System.Text;

namespace Dnui.Tests.TestSupport;

/// <summary>Builds small .nupkg files for acquisition tests.</summary>
internal static class FakeNupkg
{
    public const string Payload = "PAYLOAD-0123456789-dnui-runtime-stub";

    public static string Write(string folder, string id, string version, string? nuspecId = null, string? nuspecVersion = null, bool corrupt = false, string exeName = "Dnui.Runtime")
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{id}.{version}.nupkg");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var nuspec = zip.CreateEntry($"{id.ToLowerInvariant()}.nuspec");
            using (var writer = new StreamWriter(nuspec.Open()))
            {
                writer.Write($"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                      <metadata><id>{nuspecId ?? id}</id><version>{nuspecVersion ?? version}</version><authors>t</authors><description>t</description></metadata>
                    </package>
                    """);
            }

            var exe = zip.CreateEntry($"runtime/{exeName}", CompressionLevel.NoCompression);
            using (var stream = exe.Open())
            {
                stream.Write(Encoding.ASCII.GetBytes("#!/bin/sh\necho " + Payload + "\n"));
            }
        }

        if (corrupt)
        {
            // Flip a byte inside the stored (uncompressed) payload so its CRC-32 no longer matches.
            var bytes = File.ReadAllBytes(path);
            var marker = Encoding.ASCII.GetBytes(Payload);
            var index = bytes.AsSpan().IndexOf(marker);
            bytes[index + 3] ^= 0xFF;
            File.WriteAllBytes(path, bytes);
        }

        return path;
    }
}

/// <summary>Wraps a source and counts how often it is opened.</summary>
internal sealed class CountingSource(Dnui.Acquisition.IPackageSource inner, TimeSpan delay = default) : Dnui.Acquisition.IPackageSource
{
    private int _opens;

    public int Opens => Volatile.Read(ref _opens);

    public string Description => inner.Description;

    public async Task<Stream?> OpenAsync(string id, string version, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _opens);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return await inner.OpenAsync(id, version, cancellationToken);
    }
}

/// <summary>A temp directory deleted on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dnui-test-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
