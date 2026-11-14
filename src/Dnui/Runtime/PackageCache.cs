using System.IO.Compression;
using System.Xml.Linq;

namespace Dnui.Acquisition;

/// <summary>
/// Installs packages into <c>{DnuiRoot}/packages/{id}/{version}/</c> (contracts/runtime-acquisition.md):
/// marker-only warm path, cross-process lock, verify once, atomic publish.
/// </summary>
internal sealed class PackageCache(string dnuiRoot)
{
    public const string MarkerFileName = ".dnui-complete";

    private static readonly TimeSpan s_lockRetry = TimeSpan.FromMilliseconds(250);

    public string PackagesRoot => Path.Combine(dnuiRoot, "packages");

    public string LocksRoot => Path.Combine(dnuiRoot, "locks");

    public string GetInstallPath(string id, string version) => Path.Combine(PackagesRoot, id.ToLowerInvariant(), version.ToLowerInvariant());

    /// <summary>True when the version folder carries the install marker. The only warm-path check.</summary>
    public bool IsInstalled(string id, string version) => File.Exists(Path.Combine(GetInstallPath(id, version), MarkerFileName));

    /// <summary>Returns the install folder, installing from <paramref name="source"/> if needed.</summary>
    public async Task<string> EnsureInstalledAsync(
        IPackageSource source,
        string id,
        string version,
        string? executableRelativePath,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        var installPath = GetInstallPath(id, version);
        if (IsInstalled(id, version))
        {
            return installPath;
        }

        Directory.CreateDirectory(LocksRoot);
        await using var lockHandle = await AcquireLockAsync(id, version, lockTimeout, cancellationToken).ConfigureAwait(false);

        // Another process may have finished while we waited.
        if (IsInstalled(id, version))
        {
            return installPath;
        }

        var idFolder = Path.GetDirectoryName(installPath)!;
        Directory.CreateDirectory(idFolder);
        foreach (var leftover in Directory.EnumerateDirectories(idFolder, ".tmp-*"))
        {
            TryDeleteDirectory(leftover);
        }

        var tempFile = Path.Combine(idFolder, $".download-{Guid.NewGuid():N}.nupkg");
        var tempDir = Path.Combine(idFolder, $".tmp-{Guid.NewGuid():N}");
        try
        {
            await DownloadAsync(source, id, version, tempFile, cancellationToken).ConfigureAwait(false);
            VerifyAndExtract(tempFile, id, version, tempDir);

            if (executableRelativePath is not null && !OperatingSystem.IsWindows())
            {
                var exe = Path.Combine(tempDir, executableRelativePath);
                if (File.Exists(exe))
                {
                    File.SetUnixFileMode(exe,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }

            await File.WriteAllTextAsync(Path.Combine(tempDir, MarkerFileName), $"{id} {version}\n", cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(installPath))
            {
                // A folder without the marker: interrupted or stale.
                Directory.Delete(installPath, recursive: true);
            }

            Directory.Move(tempDir, installPath);
            return installPath;
        }
        finally
        {
            TryDeleteFile(tempFile);
            TryDeleteDirectory(tempDir);
        }
    }

    private async Task<FileStream> AcquireLockAsync(string id, string version, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var lockPath = Path.Combine(LocksRoot, $"{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.lock");
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(s_lockRetry, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.LockTimeout, id, version, $"timed out after {timeout} waiting for '{lockPath}'.", ex);
            }
        }
    }

    private static async Task DownloadAsync(IPackageSource source, string id, string version, string destination, CancellationToken cancellationToken)
    {
        Stream? stream;
        try
        {
            stream = await source.OpenAsync(id, version, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.DownloadFailed, id, version, $"download from '{source.Description}' failed: {ex.Message}", ex);
        }

        if (stream is null)
        {
            throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.PackageNotFound, id, version, $"not found in '{source.Description}'.");
        }

        try
        {
            await using (stream.ConfigureAwait(false))
            await using (var file = File.Create(destination))
            {
                await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException && !cancellationToken.IsCancellationRequested)
        {
            throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.DownloadFailed, id, version, $"download from '{source.Description}' failed: {ex.Message}", ex);
        }
    }

    /// <summary>Checks every entry's CRC-32 and the nuspec id/version, then extracts.</summary>
    private static void VerifyAndExtract(string nupkg, string id, string version, string destination)
    {
        try
        {
            using var archive = ZipFile.OpenRead(nupkg);
            XDocument? nuspec = null;
            foreach (var entry in archive.Entries)
            {
                using (var entryStream = entry.Open())
                {
                    if (Crc32.Compute(entryStream) != entry.Crc32)
                    {
                        throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.IntegrityFailed, id, version, $"checksum mismatch in '{entry.FullName}'.");
                    }
                }

                if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains('/'))
                {
                    using var nuspecStream = entry.Open();
                    nuspec = XDocument.Load(nuspecStream);
                }
            }

            var metadata = nuspec?.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
            var nuspecId = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            var nuspecVersion = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "version")?.Value;
            if (!string.Equals(nuspecId, id, StringComparison.OrdinalIgnoreCase) || !string.Equals(nuspecVersion, version, StringComparison.OrdinalIgnoreCase))
            {
                throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.IntegrityFailed, id, version, $"package manifest says '{nuspecId} {nuspecVersion}'.");
            }

            Directory.CreateDirectory(destination);
            foreach (var entry in archive.Entries)
            {
                if (IsPackagingMetadata(entry.FullName) || entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.IntegrityFailed, id, version, $"entry '{entry.FullName}' escapes the package folder.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new DnuiRuntimeUnavailableException(DnuiRuntimeFailure.IntegrityFailed, id, version, $"corrupt package: {ex.Message}", ex);
        }
    }

    private static bool IsPackagingMetadata(string name) =>
        name.StartsWith("_rels/", StringComparison.Ordinal) ||
        name.StartsWith("package/", StringComparison.Ordinal) ||
        name.Equals("[Content_Types].xml", StringComparison.Ordinal) ||
        name.EndsWith(".signature.p7s", StringComparison.Ordinal);

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
