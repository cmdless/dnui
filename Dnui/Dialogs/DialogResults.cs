using System.Text.Json.Nodes;

namespace Dnui.Dialogs;

internal enum DialogKind
{
    OpenFile,
    OpenFolder,
    SaveFile,
}

/// <summary>Dialog result shapes (data-model.md).</summary>
internal static class DialogResults
{
    public static string Key(DialogKind kind) => kind == DialogKind.SaveFile ? "path" : "paths";

    public static JsonObject Cancelled(DialogKind kind) => new() { [Key(kind)] = null };

    public static JsonObject Paths(IReadOnlyList<string> paths) => new() { ["paths"] = new JsonArray([.. paths.Select(p => (JsonNode?)JsonValue.Create(p))]) };

    public static JsonObject Path(string path) => new() { ["path"] = path };

    public static IReadOnlyList<string>? ReadPaths(JsonNode? result) =>
        result?["paths"] is JsonArray array ? [.. array.Select(n => n!.GetValue<string>())] : null;

    public static string? ReadPath(JsonNode? result) => result?["path"]?.GetValue<string>();
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
