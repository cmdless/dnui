using System.Text.Json.Serialization;

namespace Dnui.Tray;

// Wire DTOs for contracts/tray-protocol.md (camelCase JSON).

internal sealed class TrayMenuItem
{
    public string? Id { get; set; }
    public string? Text { get; set; }
    public bool Enabled { get; set; } = true;
    public bool? Checked { get; set; }
    public bool Separator { get; set; }
    public List<TrayMenuItem>? Items { get; set; }

    [JsonIgnore]
    public bool IsParent => !Separator && Items is { Count: > 0 };
}

internal sealed class TrayIconSource
{
    public string? Path { get; set; }
    public string? Png { get; set; }
}

internal sealed class InitializeParams
{
    public int ProtocolVersion { get; set; }
    public TrayIconSource? Icon { get; set; }
    public string? Tooltip { get; set; }
    public List<TrayMenuItem>? Items { get; set; }
}

internal sealed record InitializeResult(string RuntimeVersion);

internal sealed class SetItemsParams
{
    public List<TrayMenuItem>? Items { get; set; }
}

internal sealed class SetIconParams
{
    public TrayIconSource? Icon { get; set; }
    public string? Tooltip { get; set; }
}

/// <summary>Accepts <c>params: {}</c> (or none) for methods without parameters.</summary>
internal sealed class NoParams
{
}

internal sealed class SimulateClickParams
{
    public string? Id { get; set; }
}

internal sealed record TrayStateResult(bool Visible, bool HasIcon, string? Tooltip, IReadOnlyList<TrayMenuItem> Items);

internal sealed record ClickedNotification(string Id);

internal sealed record EmptyResult
{
    public static readonly EmptyResult Instance = new();
}
