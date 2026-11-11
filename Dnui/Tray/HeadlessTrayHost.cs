namespace Dnui.Tray;

/// <summary><c>tray --headless</c>: keeps state, shows nothing, needs no tray host (FR-047).</summary>
internal sealed class HeadlessTrayHost : ITrayHost
{
    public event Action? MenuOpened
    {
        add { }
        remove { }
    }

    public event Action<string>? ItemClicked
    {
        add { }
        remove { }
    }

    public TrayState State { get; private set; } = TrayState.Empty;

    public string? ValidateIcon(byte[] png) => TrayIconDecoder.HasPngSignature(png) ? null : "data is not a PNG image";

    public void Apply(TrayState state) => State = state;

    public void Dispose()
    {
    }
}
