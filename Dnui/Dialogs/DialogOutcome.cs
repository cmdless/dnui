using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Dnui.Dialogs;

internal enum DialogKind
{
    OpenFile,
    OpenFolder,
    SaveFile,
}

/// <summary>Result shapes and headless outcome validation (data-model.md).</summary>
internal static class DialogOutcome
{
    public static string Key(DialogKind kind) => kind == DialogKind.SaveFile ? "path" : "paths";

    public static JsonObject Cancelled(DialogKind kind) => new() { [Key(kind)] = null };

    public static JsonObject Paths(IReadOnlyList<string> paths) => new() { ["paths"] = new JsonArray([.. paths.Select(p => (JsonNode?)JsonValue.Create(p))]) };

    public static JsonObject Path(string path) => new() { ["path"] = path };

    /// <summary>Validates a supplied outcome against the kind's result shape.</summary>
    public static (JsonObject? Result, string? Error) Validate(DialogKind kind, JsonNode? outcome)
    {
        if (outcome is not JsonObject obj || obj.Count != 1 || !obj.ContainsKey(Key(kind)))
        {
            return (null, $"outcome must be an object with exactly one property '{Key(kind)}'");
        }

        var value = obj[Key(kind)];
        if (value is null)
        {
            return (Cancelled(kind), null);
        }

        if (kind == DialogKind.SaveFile)
        {
            return value is JsonValue v && v.TryGetValue<string>(out var path) && path.Length > 0
                ? (Path(path), null)
                : (null, "'path' must be a non-empty string or null");
        }

        if (value is not JsonArray array || array.Count == 0)
        {
            return (null, "'paths' must be a non-empty array of strings or null");
        }

        var paths = new List<string>();
        foreach (var item in array)
        {
            if (item is not JsonValue iv || !iv.TryGetValue<string>(out var s) || s.Length == 0)
            {
                return (null, "'paths' entries must be non-empty strings");
            }

            paths.Add(s);
        }

        return (Paths(paths), null);
    }

    public static IReadOnlyList<string>? ReadPaths(JsonNode? result) =>
        result?["paths"] is JsonArray array ? [.. array.Select(n => n!.GetValue<string>())] : null;

    public static string? ReadPath(JsonNode? result) => result?["path"]?.GetValue<string>();
}

/// <summary>
/// Headless mode for one-shot window/dialog processes (research R10): dialogs take supplied
/// outcomes, in order, instead of showing UI.
/// </summary>
internal static class HeadlessOutcomes
{
    private static readonly ConcurrentQueue<JsonNode?> s_queue = new();

    public static bool IsHeadless { get; private set; }

    public static void Enable(IEnumerable<JsonNode?> outcomes)
    {
        IsHeadless = true;
        foreach (var outcome in outcomes)
        {
            s_queue.Enqueue(outcome);
        }
    }

    public static JsonObject Next(DialogKind kind)
    {
        if (!s_queue.TryDequeue(out var outcome))
        {
            throw new InvalidOperationException($"Headless mode: no outcome was supplied for this {kind} dialog.");
        }

        var (result, error) = DialogOutcome.Validate(kind, outcome);
        return result ?? throw new InvalidOperationException($"Headless mode: invalid outcome for {kind}: {error}.");
    }

    internal static void ResetForTests()
    {
        IsHeadless = false;
        s_queue.Clear();
    }
}

/// <summary>Parses the CLI filter form <c>Name:pattern,pattern</c>.</summary>
internal static class FileFilterParser
{
    public static (DnuiFileFilter? Filter, string? Error) Parse(string text)
    {
        var colon = text.IndexOf(':');
        if (colon <= 0)
        {
            return (null, $"filter '{text}' must look like Name:pattern,pattern");
        }

        var name = text[..colon].Trim();
        var patterns = text[(colon + 1)..].Split(',', StringSplitOptions.TrimEntries);
        if (name.Length == 0 || patterns.Length == 0 || patterns.Any(p => p.Length == 0))
        {
            return (null, $"filter '{text}' needs a name and one or more non-empty patterns");
        }

        return (new DnuiFileFilter(name, patterns), null);
    }

    public static string Format(DnuiFileFilter filter) => $"{filter.Name}:{string.Join(',', filter.Patterns)}";
}
