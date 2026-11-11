# Contract: Runtime Acquisition (public API, on-disk layout, package layout)

Package `Cmdless.Dnui`, namespace `Dnui`. Implements constitution Principle III.

## Public API

```csharp
namespace Dnui;

public sealed class DnuiRuntimeOptions
{
    // Overrides CMDLESS_ROOT and the ~/.cmdless default.
    public string? CmdlessRoot { get; init; }

    // NuGet v3 service index URL, or a local folder of .nupkg files.
    // Default: https://api.nuget.org/v3/index.json
    public string? PackageSource { get; init; }

    // Development/test override: use this executable and skip resolution and download.
    public string? RuntimePath { get; init; }

    // How long to wait for another process holding the package lock. Default: 5 minutes.
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

public static class DnuiRuntime
{
    public static string Version { get; }                       // embedded DnuiRuntimeVersion
    public static string CurrentRid { get; }                    // e.g. "osx-arm64"
    public static string PackageId { get; }                     // "Cmdless.Dnui.Runtime." + CurrentRid

    // Root folders after applying options, env var, and defaults (no I/O).
    public static string GetCmdlessRoot(DnuiRuntimeOptions? options = null);
    public static string GetDnuiRoot(DnuiRuntimeOptions? options = null);

    // Returns the absolute path of a runnable runtime executable,
    // downloading and extracting it the first time.
    public static Task<string> ResolveAsync(DnuiRuntimeOptions? options = null,
                                            CancellationToken ct = default);
}

public sealed class DnuiRuntimeUnavailableException : Exception
{
    public DnuiRuntimeFailure Reason { get; }
    public string PackageId { get; }
    public string Version { get; }
}

public enum DnuiRuntimeFailure
{
    UnsupportedPlatform,   // no package exists for this RID
    PackageNotFound,       // the feed has no package with this id and version
    DownloadFailed,        // network or HTTP failure
    IntegrityFailed,       // checksum or manifest mismatch
    LockTimeout,           // another process held the lock past LockTimeout
}
```

## On-disk layout

| Path | Contents |
|---|---|
| `CmdlessRoot` | `CmdlessRoot` option → `CMDLESS_ROOT` env var → `~/.cmdless` (`Environment.SpecialFolder.UserProfile`) |
| `DnuiRoot` | `{CmdlessRoot}/dotnet` |
| `{DnuiRoot}/packages/{id}/{version}/` | Extracted package. `{id}` is the full lowercased package id, for example `cmdless.dnui.runtime.osx-arm64` |
| `{DnuiRoot}/packages/{id}/{version}/.dnui-complete` | Install marker, written last after verification succeeds. Its presence alone means "installed": nothing is re-verified on later resolutions. A folder without it is treated as absent |
| `{DnuiRoot}/packages/{id}/.tmp-{guid}/` | Extraction in progress. Renamed to `{version}` when complete |
| `{DnuiRoot}/locks/{id}.{version}.lock` | Cross-process lock file |

## Runtime package layout

`Cmdless.Dnui.Runtime.<rid>.<version>.nupkg`:

```text
cmdless.dnui.runtime.<rid>.nuspec     # id, version
runtime/
├── Dnui.Runtime (.exe on Windows)    # self-contained single-folder publish
└── …                                 # all other publish output
```

Executable path: `{DnuiRoot}/packages/{id}/{version}/runtime/Dnui.Runtime[.exe]`.

Supported RIDs: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`.
Any other RID (for example `linux-musl-x64`) → `UnsupportedPlatform`.

## Resolution algorithm

1. If `RuntimePath` is set, return it. It must exist; otherwise `PackageNotFound`.
2. Work out the RID and package id. An unsupported RID → `UnsupportedPlatform`.
3. If `{version}/.dnui-complete` exists → return the executable path. No lock, no network,
   and no verification: checks run only during installation (steps 7–8), never on this warm
   path.
4. Acquire `locks/{id}.{version}.lock`, waiting up to `LockTimeout` (cancellable).
5. Check again for the complete marker. Another process may have finished meanwhile.
6. Download the `.nupkg`. For a URL source, use the feed's `PackageBaseAddress` flat container,
   `{base}/{id}/{version}/{id}.{version}.nupkg`. For a folder source, copy the matching file.
   A 404 or missing file → `PackageNotFound`.
7. Verify: every zip entry's CRC is valid, and the `.nuspec` id and version match. Failure →
   `IntegrityFailed`.
8. Extract into `.tmp-{guid}`. On macOS and Linux, set the executable's user/group/other
   execute bits. Write `.dnui-complete`, delete any stale `{version}` folder (one without a
   marker), then rename `.tmp-{guid}` to `{version}`.
9. Release the lock and return the executable path.

## Tray client integration

`DnuiTrayOptions.Runtime` (a `DnuiRuntimeOptions?`) is passed to `ResolveAsync` before
launching. See [tray-client.md](tray-client.md).
