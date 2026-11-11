using System.Text.Json.Nodes;

namespace Dnui.Internal;

/// <summary>Exit codes and error names owned by the library's commands (spec 002 research R11).</summary>
internal static class ErrorResults
{
    public const int Usage = 2;
    public const int DnuiVersionMismatch = 13;
    public const int WindowAssemblyLoadFailed = 14;
    public const int WindowTypeNotFound = 15;
    public const int WindowTypeInvalid = 16;
    public const int WindowFailed = 17;
    public const int DialogUnsupported = 30;

    public static string NameOf(int code) => code switch
    {
        Usage => "Usage",
        DnuiVersionMismatch => "DnuiVersionMismatch",
        WindowAssemblyLoadFailed => "WindowAssemblyLoadFailed",
        WindowTypeNotFound => "WindowTypeNotFound",
        WindowTypeInvalid => "WindowTypeInvalid",
        WindowFailed => "WindowFailed",
        DialogUnsupported => "DialogUnsupported",
        _ => "Unexpected",
    };

    public static JsonObject Error(int code, string message) => new()
    {
        ["error"] = new JsonObject { ["code"] = NameOf(code), ["message"] = message },
    };

    /// <summary>Maps a window type load failure to its exit code.</summary>
    public static int ForWindow(Hosting.TypeLoadFailure failure) => failure switch
    {
        Hosting.TypeLoadFailure.AssemblyLoadFailed => WindowAssemblyLoadFailed,
        Hosting.TypeLoadFailure.TypeNotFound => WindowTypeNotFound,
        Hosting.TypeLoadFailure.VersionMismatch => DnuiVersionMismatch,
        _ => WindowTypeInvalid,
    };
}

/// <summary>Process-wide hooks the runtime installs into the library (platform code stays in the runtime).</summary>
internal static class DnuiPlatformHooks
{
    /// <summary>Brings the process to the front before showing UI (macOS activation, research R9).</summary>
    public static Action? BringToFront { get; set; }
}
