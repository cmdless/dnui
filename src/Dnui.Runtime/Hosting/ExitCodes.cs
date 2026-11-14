using Dnui.Internal;

namespace Dnui.Runtime.Hosting;

/// <summary>
/// Runtime exit codes and their stable error names (contracts/cli.md). 1–63 are reserved for Dnui;
/// the table lives in <see cref="ErrorResults"/>, shared with the library. 40
/// (<c>RuntimeUnavailable</c>) is library-side only and never produced by the runtime.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Unexpected = ErrorResults.Unexpected;
    public const int Usage = ErrorResults.Usage;
    public const int AppAssemblyLoadFailed = ErrorResults.AppAssemblyLoadFailed;
    public const int AppTypeNotFound = ErrorResults.AppTypeNotFound;
    public const int AppTypeInvalid = ErrorResults.AppTypeInvalid;
    public const int DnuiVersionMismatch = ErrorResults.DnuiVersionMismatch;
    public const int WindowAssemblyLoadFailed = ErrorResults.WindowAssemblyLoadFailed;
    public const int WindowTypeNotFound = ErrorResults.WindowTypeNotFound;
    public const int WindowTypeInvalid = ErrorResults.WindowTypeInvalid;
    public const int WindowFailed = ErrorResults.WindowFailed;
    public const int TrayNotRedirected = ErrorResults.TrayNotRedirected;
    public const int TrayHostUnavailable = ErrorResults.TrayHostUnavailable;
    public const int TrayStdioAlreadyClaimed = ErrorResults.TrayStdioAlreadyClaimed;
    public const int DialogUnsupported = ErrorResults.DialogUnsupported;

    public static string NameOf(int code) => ErrorResults.NameOf(code);
}
