using System.Reflection;

namespace Dnui.Hosting;

/// <summary>Finds the IL file for an assembly (research R3): Location, then {base}/{name}.dll, then {base}/dnui/{name}.dll.</summary>
internal static class AssemblyLocator
{
    public static string Find(Assembly assembly) => Find(assembly, AppContext.BaseDirectory, a => a.Location);

    internal static string Find(Assembly assembly, string baseDirectory, Func<Assembly, string> location)
    {
        var name = assembly.GetName().Name!;
        var own = location(assembly);
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(own))
        {
            candidates.Add(own);
        }

        candidates.Add(Path.Combine(baseDirectory, name + ".dll"));
        candidates.Add(Path.Combine(baseDirectory, "dnui", name + ".dll"));

        return candidates.FirstOrDefault(File.Exists) ?? throw new DnuiAssemblyNotFoundException(name, candidates);
    }
}
