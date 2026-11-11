namespace Dnui.Tray;

internal sealed record TrayMenuValidationError(string Path, string? Id, string Reason, string? OtherPath = null)
{
    public object ToErrorData() => OtherPath is null
        ? new { path = Path, id = Id, reason = Reason }
        : new { path = Path, id = Id, reason = Reason, otherPath = OtherPath };
}

/// <summary>Validates a whole item tree before anything is applied (data-model.md, FR-019).</summary>
internal static class TrayMenuValidator
{
    public static TrayMenuValidationError? Validate(IReadOnlyList<TrayMenuItem>? items)
    {
        if (items is null)
        {
            return null;
        }

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        return Walk(items, "items", seen);
    }

    private static TrayMenuValidationError? Walk(IReadOnlyList<TrayMenuItem> items, string prefix, Dictionary<string, string> seen)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var path = $"{prefix}[{i}]";
            if (item is null || item.Separator)
            {
                continue;
            }

            if (string.IsNullOrEmpty(item.Id))
            {
                return new(path, null, "missing-id");
            }

            if (string.IsNullOrEmpty(item.Text))
            {
                return new(path, item.Id, "missing-text");
            }

            if (seen.TryGetValue(item.Id, out var firstPath))
            {
                return new(path, item.Id, "duplicate-id", firstPath);
            }

            seen[item.Id] = path;

            if (item.Items is { Count: > 0 } children && Walk(children, $"{path}.items", seen) is { } error)
            {
                return error;
            }
        }

        return null;
    }

    /// <summary>Finds an item by id anywhere in the tree.</summary>
    public static TrayMenuItem? Find(IReadOnlyList<TrayMenuItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id)
            {
                return item;
            }

            if (!item.Separator && item.Items is { Count: > 0 } children && Find(children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
