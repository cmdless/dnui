using System.Reflection;
using System.Runtime.Loader;

namespace Dnui.Hosting;

internal enum TypeLoadFailure
{
    None,
    AssemblyLoadFailed,
    TypeNotFound,
    TypeInvalid,
    VersionMismatch,
}

internal sealed record TypeLoadResult(Type? Type, TypeLoadFailure Failure, string? Message)
{
    public static TypeLoadResult Ok(Type type) => new(type, TypeLoadFailure.None, null);

    public static TypeLoadResult Fail(TypeLoadFailure failure, string message) => new(null, failure, message);
}

/// <summary>
/// Loads app and window types from assembly files into the default load context, so Dnui and
/// Avalonia are shared with the runtime (spec 001 research R3, spec 002 research R5). Checks run
/// before any instance exists. Assemblies already loaded by name are reused.
/// </summary>
internal static class DnuiTypeLoader
{
    private static readonly object s_gate = new();
    private static readonly List<AssemblyDependencyResolver> s_resolvers = [];
    private static bool s_hooked;

    public static TypeLoadResult Load(string assemblyPath, string typeName, Type requiredBase)
    {
        Assembly assembly;
        try
        {
            assembly = LoadAssembly(Path.GetFullPath(assemblyPath));
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        {
            return TypeLoadResult.Fail(TypeLoadFailure.AssemblyLoadFailed, $"Could not load assembly '{assemblyPath}': {ex.Message}");
        }

        return FindType(assembly, typeName, requiredBase, assemblyPath);
    }

    /// <summary>Resolves a type in an assembly that is already loaded.</summary>
    public static TypeLoadResult FindType(Assembly assembly, string typeName, Type requiredBase, string? origin = null)
    {
        // Checked before touching the type: a mismatched Dnui reference would fail type loading.
        var runtimeDnui = typeof(DnuiApp).Assembly.GetName().Version;
        var referencedDnui = assembly.GetReferencedAssemblies().FirstOrDefault(n => n.Name == "Dnui");
        if (referencedDnui is not null && referencedDnui.Version != runtimeDnui)
        {
            return TypeLoadResult.Fail(TypeLoadFailure.VersionMismatch, $"'{origin ?? assembly.GetName().Name}' references Dnui {referencedDnui.Version}, but this process has Dnui {runtimeDnui}.");
        }

        Type? type;
        try
        {
            type = assembly.GetType(typeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is TypeLoadException or FileLoadException or FileNotFoundException or BadImageFormatException)
        {
            return TypeLoadResult.Fail(TypeLoadFailure.TypeNotFound, $"Could not load type '{typeName}': {ex.Message}");
        }

        if (type is null)
        {
            return TypeLoadResult.Fail(TypeLoadFailure.TypeNotFound, $"Type '{typeName}' was not found in '{origin ?? assembly.GetName().Name}'.");
        }

        return CheckType(type, requiredBase);
    }

    public static TypeLoadResult CheckType(Type type, Type requiredBase)
    {
        if (!requiredBase.IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return TypeLoadResult.Fail(TypeLoadFailure.TypeInvalid, $"Type '{type.FullName}' must be a non-abstract {requiredBase.FullName} with a public parameterless constructor.");
        }

        return TypeLoadResult.Ok(type);
    }

    private static Assembly LoadAssembly(string fullPath)
    {
        lock (s_gate)
        {
            var name = AssemblyName.GetAssemblyName(fullPath);
            var loaded = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name) && a.GetName().Name == name.Name);
            if (loaded is not null)
            {
                return loaded;
            }

            s_resolvers.Add(new AssemblyDependencyResolver(fullPath));
            if (!s_hooked)
            {
                AssemblyLoadContext.Default.Resolving += Resolve;
                s_hooked = true;
            }

            return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }

    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        AssemblyDependencyResolver[] resolvers;
        lock (s_gate)
        {
            resolvers = [.. s_resolvers];
        }

        foreach (var resolver in resolvers)
        {
            if (resolver.ResolveAssemblyToPath(name) is { } path)
            {
                return context.LoadFromAssemblyPath(path);
            }
        }

        return null;
    }
}
