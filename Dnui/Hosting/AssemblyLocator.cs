using System.Reflection;

namespace Dnui;

/// <summary>An app or window assembly has no IL file the runtime can load (spec 002 FR-003c).</summary>
public sealed class DnuiAssemblyNotFoundException : Exception
{
    internal DnuiAssemblyNotFoundException(string assemblyName, IReadOnlyList<string> searched)
        : base($"The Dnui runtime can't load '{assemblyName}': no IL file was found (searched: {string.Join(", ", searched)}). " +
               $"Publish normally, or let Cmdless.Dnui ship it to '{searched[^1]}' (single-file/native AOT publishes do this by default), " +
               "or keep Dnui windows and apps in a separate class library whose .dll ships next to the executable.")
    {
        AssemblyName = assemblyName;
        SearchedPaths = searched;
    }

    /// <summary>The assembly's simple name.</summary>
    public string AssemblyName { get; }

    /// <summary>Every path that was checked, in order.</summary>
    public IReadOnlyList<string> SearchedPaths { get; }
}
