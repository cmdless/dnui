namespace Dnui.Runtime.Hosting;

/// <summary>Runtime exit codes and their stable error names (contracts/cli.md). 1–63 are reserved for the runtime.</summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Unexpected = 1;
    public const int Usage = 2;
    public const int AppAssemblyLoadFailed = 10;
    public const int AppTypeNotFound = 11;
    public const int AppTypeInvalid = 12;
    public const int DnuiVersionMismatch = 13;
    public const int WindowAssemblyLoadFailed = Dnui.Internal.ErrorResults.WindowAssemblyLoadFailed;
    public const int WindowTypeNotFound = Dnui.Internal.ErrorResults.WindowTypeNotFound;
    public const int WindowTypeInvalid = Dnui.Internal.ErrorResults.WindowTypeInvalid;
    public const int WindowFailed = Dnui.Internal.ErrorResults.WindowFailed;
    public const int TrayNotRedirected = 20;
    public const int TrayHostUnavailable = 21;
    public const int TrayStdioAlreadyClaimed = 22;
    public const int DialogUnsupported = Dnui.Internal.ErrorResults.DialogUnsupported;

    public static string NameOf(int code) => code switch
    {
        Unexpected => "Unexpected",
        Usage => "Usage",
        AppAssemblyLoadFailed => "AppAssemblyLoadFailed",
        AppTypeNotFound => "AppTypeNotFound",
        AppTypeInvalid => "AppTypeInvalid",
        DnuiVersionMismatch => "DnuiVersionMismatch",
        _ => Dnui.Internal.ErrorResults.NameOf(code),
    };
}
