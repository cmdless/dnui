using System.Text.Json.Nodes;

namespace Dnui.Hosting;

/// <summary>
/// Headless dialog outcomes, consumed in order. Shared by a host and every host derived from it
/// with <see cref="DnuiHost.WithApp(string, string)"/>, so one sequence covers them all.
/// </summary>
internal sealed class OutcomeQueue(IReadOnlyList<JsonNode?> outcomes)
{
    private int _next;

    /// <summary>The next outcome, or null (cancel) once they run out.</summary>
    public JsonNode? Take()
    {
        var index = Interlocked.Increment(ref _next) - 1;
        return index < outcomes.Count ? outcomes[index]?.DeepClone() : null;
    }

    /// <summary>All outcomes not yet taken (a window's parented dialogs).</summary>
    public IReadOnlyList<JsonNode?> TakeRemaining()
    {
        var start = Interlocked.Exchange(ref _next, outcomes.Count);
        return [.. outcomes.Skip(Math.Min(start, outcomes.Count)).Select(o => o?.DeepClone())];
    }
}
