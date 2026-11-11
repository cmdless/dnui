using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Dnui.Tray;

/// <summary>The real OS tray: one <see cref="TrayIcon"/> with a <see cref="NativeMenu"/> (research R8).</summary>
internal sealed class AvaloniaTrayHost : ITrayHost
{
    internal const string PlaceholderText = "No items";

    private readonly Application _app;
    private readonly TrayIcon _tray;
    private readonly NativeMenu _menu = new();
    private byte[]? _shownIcon;

    public AvaloniaTrayHost(Application app)
    {
        _app = app;
        // Avalonia.Native throws on a null tooltip; always pass a string.
        // One menu for the tray's lifetime: Avalonia.Native rejects swapping in a new NativeMenu,
        // so updates replace its items instead.
        _menu.Opening += (_, _) => MenuOpened?.Invoke();
        _tray = new TrayIcon { IsVisible = false, ToolTipText = string.Empty, Menu = _menu };
        TrayIcon.SetIcons(app, [_tray]);
    }

    public event Action? MenuOpened;

    public event Action<string>? ItemClicked;

    public string? ValidateIcon(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);
            using var bitmap = new Bitmap(stream);
            return bitmap.PixelSize.Width > 0 ? null : "image has no pixels";
        }
        catch (Exception ex)
        {
            return $"cannot decode image: {ex.Message}";
        }
    }

    public void Apply(TrayState state)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (!ReferenceEquals(state.Icon, _shownIcon))
        {
            _tray.Icon = state.Icon is null ? null : new WindowIcon(new Bitmap(new MemoryStream(state.Icon)));
            _shownIcon = state.Icon;
        }

        _tray.ToolTipText = state.Tooltip ?? string.Empty;
        FillMenu(state.Items);
        _tray.IsVisible = state.Visible;
    }

    private void FillMenu(IReadOnlyList<TrayMenuItem> items)
    {
        _menu.Items.Clear();
        if (items.Count == 0)
        {
            var placeholder = new NativeMenuItem(PlaceholderText) { IsEnabled = false };
            placeholder.Click += (_, _) => ItemClicked?.Invoke(string.Empty);
            _menu.Items.Add(placeholder);
            return;
        }

        AddItems(_menu, items);
    }

    private void AddItems(NativeMenu menu, IReadOnlyList<TrayMenuItem> items)
    {
        foreach (var item in items)
        {
            if (item.Separator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }

            var menuItem = new NativeMenuItem(item.Text!) { IsEnabled = item.Enabled };
            if (item.Checked is { } isChecked)
            {
                menuItem.ToggleType = MenuItemToggleType.CheckBox;
                menuItem.IsChecked = isChecked;
            }

            if (item.IsParent)
            {
                var submenu = new NativeMenu();
                AddItems(submenu, item.Items!);
                menuItem.Menu = submenu;
            }
            else
            {
                var id = item.Id!;
                menuItem.Click += (_, _) =>
                {
                    if (menuItem.IsEnabled)
                    {
                        AllowForegroundForCaller();
                        ItemClicked?.Invoke(id);
                    }
                };
            }

            menu.Items.Add(menuItem);
        }
    }

    // Windows: a tray click gives this process foreground rights for a moment. Pass them on so a
    // dialog or window the caller starts in response can come to the front (spec 002 research R9).
    private static void AllowForegroundForCaller()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = AllowSetForegroundWindow(-1);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _tray.IsVisible = false;
        TrayIcon.SetIcons(_app, null);
        _tray.Dispose();
    }
}

internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public static readonly AvaloniaUiDispatcher Instance = new();

    public Task<T> InvokeAsync<T>(Func<T> func) => Dispatcher.UIThread.InvokeAsync(func).GetTask();
}
