namespace Dnui.Hosting;

/// <summary>An app chosen for a host command, exactly as the CLI takes it (<c>--app-assembly</c>/<c>--app-type</c>).</summary>
internal sealed record AppSelection(string AssemblyPath, string TypeName);

/// <summary>
/// Everything needed to start the runtime once. <see cref="Arguments"/> is the only place the final
/// command line is assembled, so the library and the CLI cannot drift apart.
/// </summary>
internal sealed record RuntimeLaunch(DnuiRuntimeOptions? Runtime, AppSelection? App, IReadOnlyList<string> Command)
{
    /// <summary>The runtime command line: app options, then the command.</summary>
    public IEnumerable<string> Arguments()
    {
        if (App is { } app)
        {
            yield return "--app-assembly";
            yield return app.AssemblyPath;
            yield return "--app-type";
            yield return app.TypeName;
        }

        foreach (var argument in Command)
        {
            yield return argument;
        }
    }
}
