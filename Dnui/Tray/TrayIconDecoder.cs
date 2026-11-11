namespace Dnui.Tray;

/// <summary>Turns a <see cref="TrayIconSource"/> into PNG bytes (data-model.md Icon). Decoding happens in the host.</summary>
internal static class TrayIconDecoder
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Returns the bytes, or an error reason.</summary>
    public static (byte[]? Png, string? Error) Load(TrayIconSource? source)
    {
        if (source is null)
        {
            return (null, "icon is required");
        }

        var hasPath = !string.IsNullOrEmpty(source.Path);
        var hasPng = !string.IsNullOrEmpty(source.Png);
        if (hasPath == hasPng)
        {
            return (null, "exactly one of 'path' or 'png' must be given");
        }

        byte[] bytes;
        if (hasPath)
        {
            try
            {
                bytes = File.ReadAllBytes(Path.GetFullPath(source.Path!, Environment.CurrentDirectory));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return (null, $"cannot read '{source.Path}': {ex.Message}");
            }
        }
        else
        {
            try
            {
                bytes = Convert.FromBase64String(source.Png!);
            }
            catch (FormatException)
            {
                return (null, "'png' is not valid base64");
            }
        }

        return HasPngSignature(bytes) ? (bytes, null) : (null, "data is not a PNG image");
    }

    public static bool HasPngSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length > PngSignature.Length && bytes[..PngSignature.Length].SequenceEqual(PngSignature);
}
