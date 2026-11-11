using Dnui.Tray;

namespace Dnui.Tests.TestSupport;

/// <summary>Records applied states and raises host events on demand.</summary>
internal sealed class FakeTrayHost : ITrayHost
{
    public List<TrayState> Applied { get; } = [];

    public TrayState? Last => Applied.Count == 0 ? null : Applied[^1];

    public event Action? MenuOpened;

    public event Action<string>? ItemClicked;

    public string? ValidateIcon(byte[] png) => TrayIconDecoder.HasPngSignature(png) ? null : "bad png";

    public void Apply(TrayState state) => Applied.Add(state);

    public void RaiseOpened() => MenuOpened?.Invoke();

    public void RaiseClicked(string id) => ItemClicked?.Invoke(id);

    public void Dispose()
    {
    }
}

internal sealed class SynchronousUiDispatcher : IUiDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        try
        {
            return Task.FromResult(func());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}

internal static class TestPng
{
    // A valid 1x1 transparent PNG.
    public static readonly byte[] Bytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static string Base64 => Convert.ToBase64String(Bytes);
}
