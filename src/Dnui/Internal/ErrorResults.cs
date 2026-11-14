using System.Text.Json.Nodes;

namespace Dnui.Internal;

/// <summary>
/// The exit-code table shared by the runtime and the library (spec 002 research R11, spec 003 R2).
/// Codes 40+ are library-side launch failures that the runtime never produces.
/// </summary>
internal static class ErrorResults
{
    public const int Unexpected = 1;
    public const int Usage = 2;
    public const int AppAssemblyLoadFailed = 10;
    public const int AppTypeNotFound = 11;
    public const int AppTypeInvalid = 12;
    public const int DnuiVersionMismatch = 13;
    public const int WindowAssemblyLoadFailed = 14;
    public const int WindowTypeNotFound = 15;
    public const int WindowTypeInvalid = 16;
    public const int WindowFailed = 17;
    public const int TrayNotRedirected = 20;
    public const int TrayHostUnavailable = 21;
    public const int TrayStdioAlreadyClaimed = 22;
    public const int DialogUnsupported = 30;
    public const int RuntimeUnavailable = 40;

    public static string NameOf(int code) => code switch
    {
        Usage => "Usage",
        AppAssemblyLoadFailed => "AppAssemblyLoadFailed",
        AppTypeNotFound => "AppTypeNotFound",
        AppTypeInvalid => "AppTypeInvalid",
        TrayNotRedirected => "TrayNotRedirected",
        TrayHostUnavailable => "TrayHostUnavailable",
        TrayStdioAlreadyClaimed => "TrayStdioAlreadyClaimed",
        RuntimeUnavailable => "RuntimeUnavailable",
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

    /// <summary>The error line for a runtime that could not be resolved (exit 40, never produced by the runtime).</summary>
    public static JsonObject RuntimeUnavailableError(DnuiRuntimeUnavailableException ex) =>
        Error(RuntimeUnavailable, ex.Message);

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

    /// <summary>True when the app chose Avalonia's headless platform: nothing is shown, and the tray keeps state only.</summary>
    public static bool IsHeadless { get; set; }
}
