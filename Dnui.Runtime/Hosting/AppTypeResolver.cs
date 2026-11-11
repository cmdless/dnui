using System.Reflection;
using System.Runtime.Loader;

namespace Dnui.Runtime.Hosting;

internal sealed record AppTypeResolution(Type? AppType, int ExitCode, string? Error)
{
    public static AppTypeResolution Ok(Type type) => new(type, ExitCodes.Success, null);

    public static AppTypeResolution Fail(int code, string error) => new(null, code, error);
}

/// <summary>
/// Resolves the app type from --app-assembly/--app-type into the default load context, so the
/// runtime's Dnui and Avalonia are shared (research R3). No instance is ever created here.
/// </summary>
internal static class AppTypeResolver
{
    public static AppTypeResolution Resolve(string? assemblyPath, string? typeName)
    {
        if (assemblyPath is null || typeName is null)
        {
            return AppTypeResolution.Ok(typeof(App));
        }

        Assembly assembly;
        try
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var resolver = new AssemblyDependencyResolver(fullPath);
            AssemblyLoadContext.Default.Resolving += (context, name) =>
                resolver.ResolveAssemblyToPath(name) is { } path ? context.LoadFromAssemblyPath(path) : null;
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        {
            return AppTypeResolution.Fail(ExitCodes.AppAssemblyLoadFailed, $"Could not load app assembly '{assemblyPath}': {ex.Message}");
        }

        // Checked before touching the type: a mismatched Dnui reference would fail type loading.
        var runtimeDnui = typeof(DnuiApp).Assembly.GetName().Version;
        var referencedDnui = assembly.GetReferencedAssemblies().FirstOrDefault(n => n.Name == "Dnui");
        if (referencedDnui is not null && referencedDnui.Version != runtimeDnui)
        {
            return AppTypeResolution.Fail(ExitCodes.DnuiVersionMismatch, $"App assembly references Dnui {referencedDnui.Version}, but the runtime has Dnui {runtimeDnui}.");
        }

        Type? type;
        try
        {
            type = assembly.GetType(typeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is TypeLoadException or FileLoadException or FileNotFoundException or BadImageFormatException)
        {
            return AppTypeResolution.Fail(ExitCodes.AppTypeNotFound, $"Could not load type '{typeName}': {ex.Message}");
        }

        if (type is null)
        {
            return AppTypeResolution.Fail(ExitCodes.AppTypeNotFound, $"Type '{typeName}' was not found in '{assemblyPath}'.");
        }

        if (!typeof(DnuiApp).IsAssignableFrom(type) || type.IsAbstract)
        {
            return AppTypeResolution.Fail(ExitCodes.AppTypeInvalid, $"Type '{typeName}' does not derive from Dnui.DnuiApp (or is abstract).");
        }

        return AppTypeResolution.Ok(type);
    }
}
