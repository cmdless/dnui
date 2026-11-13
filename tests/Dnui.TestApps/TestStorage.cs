using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;

// Avalonia marks IStorageProvider and IStorageItem "not client implementable"; this project
// compiles against Avalonia.Base's implementation assembly to allow a test fake (see the csproj).

namespace Dnui.TestApps;

/// <summary>
/// A fake file picker for tests on Avalonia's headless platform. Each picker call takes the next
/// queued pick, in call order, and writes what it was asked for to stderr (<c>dnui-test: picker …</c>).
/// <list type="bullet">
/// <item>An array of paths: chosen (open file/folder).</item>
/// <item>A string: chosen (save).</item>
/// <item><c>null</c>: cancelled.</item>
/// <item><c>"wait"</c>: never answers, like a user who hasn't decided yet.</item>
/// </list>
/// With nothing queued, a picker call throws, which fails the window or dialog that asked.
/// Process tests queue picks with the <c>DNUI_TEST_PICKS</c> environment variable (a JSON array).
/// </summary>
public static class TestStorage
{
    public const string PicksVariable = "DNUI_TEST_PICKS";

    private static readonly ConcurrentQueue<JsonNode?> s_picks = new();

    /// <summary>The picker calls made so far, as written to stderr.</summary>
    public static ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>Registers the fake as every window's storage provider.</summary>
    public static AppBuilder UseTestStorage(this AppBuilder builder) => builder.AfterSetup(_ =>
    {
        if (Environment.GetEnvironmentVariable(PicksVariable) is { } json && JsonNode.Parse(json) is JsonArray picks)
        {
            Queue([.. picks.Select(p => p?.DeepClone())]);
        }

        AvaloniaLocator.CurrentMutable.Bind<IStorageProviderFactory>().ToConstant(new Factory());
    });

    public static void Queue(params JsonNode?[] picks)
    {
        foreach (var pick in picks)
        {
            s_picks.Enqueue(pick);
        }
    }

    public static void Reset()
    {
        s_picks.Clear();
        Calls.Clear();
    }

    private static async Task<JsonNode?> NextAsync(string call)
    {
        Calls.Enqueue(call);
        Console.Error.WriteLine("dnui-test: picker " + call);
        if (!s_picks.TryDequeue(out var pick))
        {
            throw new InvalidOperationException($"No test pick was queued for: {call}.");
        }

        if (pick is JsonValue value && value.TryGetValue<string>(out var text) && text == "wait")
        {
            await Task.Delay(Timeout.Infinite).ConfigureAwait(true);
        }

        return pick;
    }

    private static string Describe(PickerOptions options, params (string Name, object? Value)[] extra) =>
        string.Join(' ', new[] { ("title", (object?)options.Title) }.Concat(extra)
            .Where(p => p.Item2 is not null)
            .Select(p => $"{p.Item1}={p.Item2}"));

    private static string Filters(IReadOnlyList<FilePickerFileType>? types) =>
        types is null ? "" : string.Join(';', types.Select(t => $"{t.Name}:{string.Join(',', t.Patterns ?? [])}"));

    private sealed class Factory : IStorageProviderFactory
    {
        public IStorageProvider CreateProvider(TopLevel topLevel) => new Provider();
    }

    private sealed class Provider : IStorageProvider
    {
        public bool CanOpen => true;

        public bool CanSave => true;

        public bool CanPickFolder => true;

        public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options) =>
            ToItems(await NextAsync("open-file " + Describe(options, ("multiple", options.AllowMultiple), ("filters", Filters(options.FileTypeFilter)))).ConfigureAwait(true));

        public async Task<OpenFilePickerResult> OpenFilePickerWithResultAsync(FilePickerOpenOptions options) =>
            new() { Files = await OpenFilePickerAsync(options).ConfigureAwait(true) };

        public async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
        {
            var pick = await NextAsync("save-file " + Describe(options,
                ("name", options.SuggestedFileName), ("extension", options.DefaultExtension),
                ("filters", Filters(options.FileTypeChoices)), ("overwritePrompt", options.ShowOverwritePrompt))).ConfigureAwait(true);
            return pick is null ? null : new Item(pick.GetValue<string>());
        }

        public async Task<SaveFilePickerResult> SaveFilePickerWithResultAsync(FilePickerSaveOptions options) =>
            new() { File = await SaveFilePickerAsync(options).ConfigureAwait(true) };

        public async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options) =>
            ToItems(await NextAsync("open-folder " + Describe(options, ("multiple", options.AllowMultiple))).ConfigureAwait(true));

        public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark) => Task.FromResult<IStorageBookmarkFile?>(null);

        public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark) => Task.FromResult<IStorageBookmarkFolder?>(null);

        public Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath) => Task.FromResult<IStorageFile?>(null);

        public Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath) => Task.FromResult<IStorageFolder?>(null);

        public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder) => Task.FromResult<IStorageFolder?>(null);

        private static Item[] ToItems(JsonNode? pick) =>
            pick is JsonArray paths ? [.. paths.Select(p => new Item(p!.GetValue<string>()))] : [];
    }

    /// <summary>Just enough of a file or folder for <c>TryGetLocalPath</c>.</summary>
    private sealed class Item(string path) : IStorageFile, IStorageFolder
    {
        public string Name => System.IO.Path.GetFileName(path);

        public Uri Path { get; } = new(System.IO.Path.GetFullPath(path));

        public bool CanBookmark => false;

        public Task<StorageItemProperties> GetBasicPropertiesAsync() => Task.FromResult(new StorageItemProperties());

        public Task<string?> SaveBookmarkAsync() => Task.FromResult<string?>(null);

        public Task<IStorageFolder?> GetParentAsync() => Task.FromResult<IStorageFolder?>(null);

        public Task DeleteAsync() => throw new NotSupportedException();

        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync() => throw new NotSupportedException();

        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();

        public IAsyncEnumerable<IStorageItem> GetItemsAsync() => throw new NotSupportedException();

        public Task<IStorageFile?> GetFileAsync(string name) => throw new NotSupportedException();

        public Task<IStorageFolder?> GetFolderAsync(string name) => throw new NotSupportedException();

        public Task<IStorageFile?> CreateFileAsync(string name) => throw new NotSupportedException();

        public Task<IStorageFolder?> CreateFolderAsync(string name) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
