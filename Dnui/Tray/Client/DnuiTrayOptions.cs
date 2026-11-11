using Dnui.Tray;

namespace Dnui;

/// <summary>
/// What a tray shows, for <see cref="IDnuiHostCommands.StartTrayAsync"/>. The runtime, headless
/// mode, and app come from the host (<see cref="DnuiHostOptions"/>, <see cref="IDnuiHostCommands.WithApp{TApp}"/>).
/// </summary>
public sealed record DnuiTrayOptions
{
    /// <summary>The tray icon. The tray is invisible until an icon is set.</summary>
    public DnuiTrayIcon? Icon { get; init; }

    /// <summary>The tooltip.</summary>
    public string? Tooltip { get; init; }

    /// <summary>The menu. Empty shows a disabled "No items" entry.</summary>
    public IReadOnlyList<DnuiTrayItem> Items { get; init; } = [];

    /// <summary>How long dispose waits for the runtime to exit before killing it.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs on the created tray before it starts: attach <see cref="DnuiTray.Opened"/>,
    /// <see cref="DnuiTray.Clicked"/>, and <see cref="DnuiTray.UnhandledHandlerException"/> here so no
    /// event is missed. Tray methods called inside throw <see cref="DnuiTrayNotRunningException"/>.
    /// If it throws, the runtime is stopped and the exception is rethrown from start.
    /// </summary>
    public Action<DnuiTray>? Configure { get; init; }
}

/// <summary>A tray menu item. Ids must be non-empty and unique across the whole menu.</summary>
/// <param name="Id">Reported back in <see cref="DnuiTray.Clicked"/>.</param>
/// <param name="Text">Display text.</param>
public sealed record DnuiTrayItem(string Id, string Text)
{
    /// <summary>A separator line.</summary>
    public static DnuiTrayItem Separator { get; } = new(string.Empty, string.Empty) { IsSeparator = true };

    /// <summary>Whether the item can be selected. Default true.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Null for a normal item; true/false makes it a check item in that state.</summary>
    public bool? Checked { get; init; }

    /// <summary>Child items; makes this a submenu parent (parents are never clicked).</summary>
    public IReadOnlyList<DnuiTrayItem>? Items { get; init; }

    /// <summary>True for <see cref="Separator"/>.</summary>
    public bool IsSeparator { get; private init; }

    internal TrayMenuItem ToWire() => IsSeparator
        ? new TrayMenuItem { Separator = true }
        : new TrayMenuItem { Id = Id, Text = Text, Enabled = Enabled, Checked = Checked, Items = Items?.Select(i => i.ToWire()).ToList() };

    internal static DnuiTrayItem FromWire(TrayMenuItem item) => item.Separator
        ? Separator
        : new DnuiTrayItem(item.Id ?? string.Empty, item.Text ?? string.Empty)
        {
            Enabled = item.Enabled,
            Checked = item.Checked,
            Items = item.Items?.Select(FromWire).ToList(),
        };
}

/// <summary>A PNG tray icon.</summary>
public sealed class DnuiTrayIcon
{
    private DnuiTrayIcon(TrayIconSource source) => Source = source;

    internal TrayIconSource Source { get; }

    /// <summary>A PNG file, resolved to an absolute path now.</summary>
    public static DnuiTrayIcon FromFile(string path) => new(new TrayIconSource { Path = Path.GetFullPath(path) });

    /// <summary>PNG bytes, sent inline.</summary>
    public static DnuiTrayIcon FromPng(ReadOnlyMemory<byte> png) => new(new TrayIconSource { Png = Convert.ToBase64String(png.Span) });
}

/// <summary>What the tray is showing (from <c>tray/getState</c>).</summary>
public sealed record DnuiTrayState(bool Visible, bool HasIcon, string? Tooltip, IReadOnlyList<DnuiTrayItem> Items);
