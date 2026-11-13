using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Dnui.Dialogs;

internal sealed class DialogUnsupportedException(DialogKind kind)
    : Exception($"This platform can't show the {kind} dialog.")
{
    public DialogKind Kind { get; } = kind;
}

/// <summary>Maps Dnui dialog options onto Avalonia's storage pickers and back (research R8).</summary>
internal static class DialogRunner
{
    public static FilePickerOpenOptions Map(DnuiOpenFileOptions o, IStorageFolder? start) => new()
    {
        Title = o.Title,
        AllowMultiple = o.AllowMultiple,
        FileTypeFilter = o.Filters.Count == 0 ? null : [.. o.Filters.Select(ToType)],
        SuggestedStartLocation = start,
    };

    public static FolderPickerOpenOptions Map(DnuiOpenFolderOptions o, IStorageFolder? start) => new()
    {
        Title = o.Title,
        AllowMultiple = o.AllowMultiple,
        SuggestedStartLocation = start,
    };

    public static FilePickerSaveOptions Map(DnuiSaveFileOptions o, IStorageFolder? start) => new()
    {
        Title = o.Title,
        SuggestedFileName = o.SuggestedName,
        DefaultExtension = o.DefaultExtension?.TrimStart('.') is { Length: > 0 } ext ? ext : null,
        FileTypeChoices = o.Filters.Count == 0 ? null : [.. o.Filters.Select(ToType)],
        ShowOverwritePrompt = o.OverwritePrompt,
        SuggestedStartLocation = start,
    };

    private static FilePickerFileType ToType(DnuiFileFilter f) => new(f.Name) { Patterns = [.. f.Patterns] };

    /// <summary>
    /// Shows the dialog parented to <paramref name="owner"/>, through the owner's storage provider.
    /// On the headless platform that's Avalonia's no-op provider (unsupported) unless the app
    /// registered its own <c>IStorageProviderFactory</c>.
    /// </summary>
    public static async Task<JsonObject> RunAsync(TopLevel? owner, DialogKind kind, object options)
    {
        var storage = (owner ?? throw new InvalidOperationException("A dialog needs an owner window.")).StorageProvider;
        var startPath = options switch
        {
            DnuiOpenFileOptions o => o.StartFolder,
            DnuiOpenFolderOptions o => o.StartFolder,
            DnuiSaveFileOptions o => o.StartFolder,
            _ => null,
        };
        var start = await TryGetFolderAsync(storage, startPath).ConfigureAwait(true);

        switch (kind)
        {
            case DialogKind.OpenFile:
                if (!storage.CanOpen) throw new DialogUnsupportedException(kind);
                return Paths(kind, await storage.OpenFilePickerAsync(Map((DnuiOpenFileOptions)options, start)).ConfigureAwait(true));
            case DialogKind.OpenFolder:
                if (!storage.CanPickFolder) throw new DialogUnsupportedException(kind);
                return Paths(kind, await storage.OpenFolderPickerAsync(Map((DnuiOpenFolderOptions)options, start)).ConfigureAwait(true));
            default:
                if (!storage.CanSave) throw new DialogUnsupportedException(kind);
                var file = await storage.SaveFilePickerAsync(Map((DnuiSaveFileOptions)options, start)).ConfigureAwait(true);
                return file?.TryGetLocalPath() is { } path ? DialogResults.Path(path) : DialogResults.Cancelled(kind);
        }
    }

    private static JsonObject Paths(DialogKind kind, IEnumerable<IStorageItem> items)
    {
        var paths = items.Select(i => i.TryGetLocalPath()).OfType<string>().ToList();
        return paths.Count == 0 ? DialogResults.Cancelled(kind) : DialogResults.Paths(paths);
    }

    private static async Task<IStorageFolder?> TryGetFolderAsync(IStorageProvider storage, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.StartsWith('~')
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..]
            : path));
        if (!Directory.Exists(full))
        {
            return null;
        }

        try
        {
            return await storage.TryGetFolderFromPathAsync(full).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
