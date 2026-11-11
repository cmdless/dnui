using System.Text.Json.Nodes;

namespace Dnui.Hosting;

/// <summary>An app chosen for a host command, exactly as the CLI takes it (<c>--app-assembly</c>/<c>--app-type</c>).</summary>
internal sealed record AppSelection(string AssemblyPath, string TypeName);

/// <summary>
/// Everything needed to start the runtime once. <see cref="Arguments"/> is the only place the final
/// command line is assembled, so the library and the CLI cannot drift apart.
/// </summary>
internal sealed record RuntimeLaunch(DnuiRuntimeOptions? Runtime, AppSelection? App, IReadOnlyList<string> Command, bool Headless)
{
    /// <summary>Headless outcomes, passed as <see cref="OutcomeOption"/> values in order.</summary>
    public IReadOnlyList<JsonNode?> HeadlessOutcomes { get; init; } = [];

    /// <summary><c>--outcome</c> for dialogs, <c>--dialog-outcome</c> for windows.</summary>
    public string OutcomeOption { get; init; } = "--outcome";

    /// <summary>The runtime command line: app options, the command, then headless options.</summary>
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

        if (Headless)
        {
            yield return "--headless";
            foreach (var outcome in HeadlessOutcomes)
            {
                yield return OutcomeOption;
                yield return outcome?.ToJsonString() ?? "null";
            }
        }
    }
}
