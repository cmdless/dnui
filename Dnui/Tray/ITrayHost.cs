namespace Dnui.Tray;

/// <summary>What the tray shows. <see cref="Icon"/> is PNG bytes; the tray is visible only with an icon.</summary>
internal sealed record TrayState(byte[]? Icon, string? Tooltip, IReadOnlyList<TrayMenuItem> Items)
{
    public static readonly TrayState Empty = new(null, null, []);

    public bool Visible => Icon is not null;
}

/// <summary>Seam between the protocol and the platform tray (research R8). Called on the UI thread.</summary>
internal interface ITrayHost : IDisposable
{
    /// <summary>Raised when the menu opens.</summary>
    event Action? MenuOpened;

    /// <summary>Raised when an item is selected (id <c>""</c> for the placeholder).</summary>
    event Action<string>? ItemClicked;

    /// <summary>Returns null when the PNG can be shown, else a reason.</summary>
    string? ValidateIcon(byte[] png);

    /// <summary>Shows the state. An empty item list shows the "No items" placeholder.</summary>
    void Apply(TrayState state);
}

/// <summary>Runs work on the UI thread. Abstracted so protocol tests don't need Avalonia.</summary>
internal interface IUiDispatcher
{
    Task<T> InvokeAsync<T>(Func<T> func);
}
