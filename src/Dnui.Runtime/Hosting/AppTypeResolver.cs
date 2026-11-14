using Dnui.Hosting;

namespace Dnui.Runtime.Hosting;

internal sealed record AppTypeResolution(Type? AppType, int ExitCode, string? Error)
{
    public static AppTypeResolution Ok(Type type) => new(type, ExitCodes.Success, null);

    public static AppTypeResolution Fail(int code, string error) => new(null, code, error);
}

/// <summary>Resolves the app type (spec 001 research R3) through the shared <see cref="DnuiTypeLoader"/>.</summary>
internal static class AppTypeResolver
{
    public static AppTypeResolution Resolve(string? assemblyPath, string? typeName)
    {
        if (assemblyPath is null || typeName is null)
        {
            return AppTypeResolution.Ok(typeof(App));
        }

        var result = DnuiTypeLoader.Load(assemblyPath, typeName, typeof(DnuiApp));
        return result.Failure switch
        {
            TypeLoadFailure.None => AppTypeResolution.Ok(result.Type!),
            TypeLoadFailure.AssemblyLoadFailed => AppTypeResolution.Fail(ExitCodes.AppAssemblyLoadFailed, result.Message!),
            TypeLoadFailure.TypeNotFound => AppTypeResolution.Fail(ExitCodes.AppTypeNotFound, result.Message!),
            TypeLoadFailure.VersionMismatch => AppTypeResolution.Fail(ExitCodes.DnuiVersionMismatch, result.Message!),
            _ => AppTypeResolution.Fail(ExitCodes.AppTypeInvalid, result.Message!),
        };
    }
}
