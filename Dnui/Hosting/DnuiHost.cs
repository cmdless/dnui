using System.Text.Json.Nodes;
using Dnui.Dialogs;
using Dnui.Hosting;

namespace Dnui;

/// <summary>Options for <see cref="DnuiHost"/>. They apply to every command the host starts: dialogs, windows, and trays.</summary>
public sealed record DnuiHostOptions
{
    /// <summary>How the runtime is found. Null means defaults (found or downloaded automatically).</summary>
    public DnuiRuntimeOptions? Runtime { get; init; }

    /// <summary>Run every command headless (no display). Dialogs take <see cref="Outcomes"/>; trays are driven by their simulate methods.</summary>
    public bool Headless { get; init; }

    /// <summary>Headless dialog outcomes, consumed in order (one per dialog call, or per parented dialog in a window).</summary>
    public IReadOnlyList<JsonNode?> Outcomes { get; init; } = [];
}

/// <summary>
/// Host commands (constitution I): every call starts a new runtime process. It can be used from
/// anywhere, including window code. Choose a custom app with <see cref="WithApp{TApp}"/>.
/// </summary>
public sealed class DnuiHost : IDnuiHostCommands
{
    private readonly DnuiHostOptions _options;
    private readonly OutcomeQueue _outcomes;
    private readonly AppSelection? _app;
    private readonly Func<RuntimeLaunch, CancellationToken, Task<DnuiResult>> _capture;

    /// <summary>Creates a host with the given options.</summary>
    public DnuiHost(DnuiHostOptions? options = null)
        : this(options ?? new DnuiHostOptions(), outcomes: null, app: null, capture: null)
    {
    }

    /// <summary>Test seam: <paramref name="capture"/> replaces starting one-shot runtimes.</summary>
    internal DnuiHost(DnuiHostOptions options, OutcomeQueue? outcomes, AppSelection? app, Func<RuntimeLaunch, CancellationToken, Task<DnuiResult>>? capture)
    {
        _options = options;
        _outcomes = outcomes ?? new OutcomeQueue(options.Outcomes);
        _app = app;
        _capture = capture ?? RuntimeLauncher.CaptureAsync;
    }

    /// <summary>A host with default options and the built-in app.</summary>
    public static DnuiHost Default { get; } = new();

    // ---- App selection: generic → Type → strings (the CLI's --app-assembly/--app-type) ----

    /// <inheritdoc />
    public IDnuiHostCommands WithApp<TApp>() where TApp : DnuiCommandApp => WithApp(typeof(TApp));

    /// <inheritdoc />
    public IDnuiHostCommands WithApp(Type appType)
    {
        ArgumentNullException.ThrowIfNull(appType);
        if (!typeof(DnuiCommandApp).IsAssignableFrom(appType))
        {
            throw new ArgumentException($"'{appType}' must derive from {nameof(DnuiCommandApp)}.", nameof(appType));
        }

        return WithApp(AssemblyLocator.Find(appType.Assembly), appType.FullName!);
    }

    /// <inheritdoc />
    public IDnuiHostCommands WithApp(string appAssemblyPath, string appTypeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(appAssemblyPath);
        ArgumentException.ThrowIfNullOrEmpty(appTypeName);
        return new DnuiHost(_options, _outcomes, new AppSelection(appAssemblyPath, appTypeName), _capture);
    }

    // ---- Dialogs ----

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> OpenFilesAsync(DnuiOpenFileOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        List<string> args = ["dialog", "open-file"];
        AddCommon(args, options.Title, options.StartFolder);
        if (options.AllowMultiple) args.Add("--multiple");
        AddFilters(args, options.Filters);
        return DialogOutcome.ReadPaths(await RunDialogAsync(args, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> OpenFoldersAsync(DnuiOpenFolderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        List<string> args = ["dialog", "open-folder"];
        AddCommon(args, options.Title, options.StartFolder);
        if (options.AllowMultiple) args.Add("--multiple");
        return DialogOutcome.ReadPaths(await RunDialogAsync(args, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(DnuiSaveFileOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        List<string> args = ["dialog", "save-file"];
        AddCommon(args, options.Title, options.StartFolder);
        if (options.SuggestedName is { } name) args.AddRange(["--suggested-name", name]);
        if (options.DefaultExtension is { } ext) args.AddRange(["--default-extension", ext]);
        AddFilters(args, options.Filters);
        if (!options.OverwritePrompt) args.Add("--no-overwrite-prompt");
        return DialogOutcome.ReadPath(await RunDialogAsync(args, cancellationToken).ConfigureAwait(false));
    }

    // ---- Windows: generic → Type → strings (the CLI's window <assembly> <type>) ----

    /// <inheritdoc />
    public Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow =>
        OpenWindowAsync(typeof(TWindow), cancellationToken);

    /// <inheritdoc />
    public Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(windowType);
        if (!typeof(DnuiWindow).IsAssignableFrom(windowType))
        {
            throw new ArgumentException($"'{windowType}' must derive from {nameof(DnuiWindow)}.", nameof(windowType));
        }

        return OpenWindowAsync(AssemblyLocator.Find(windowType.Assembly), windowType.FullName!, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JsonNode?> OpenWindowAsync(string windowAssemblyPath, string windowTypeName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowAssemblyPath);
        ArgumentException.ThrowIfNullOrEmpty(windowTypeName);
        var launch = Launch(["window", windowAssemblyPath, windowTypeName]) with
        {
            HeadlessOutcomes = _options.Headless ? _outcomes.TakeRemaining() : [],
            OutcomeOption = "--dialog-outcome",
        };
        var result = await RunAsync(launch, cancellationToken).ConfigureAwait(false);
        return result?["result"]?.DeepClone();
    }

    // ---- Tray ----

    /// <inheritdoc />
    public Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return DnuiTray.StartAsync(Launch(["tray"]), options, cancellationToken);
    }

    // ---- Plumbing ----

    private RuntimeLaunch Launch(IReadOnlyList<string> command) => new(_options.Runtime, _app, command, _options.Headless);

    private Task<JsonNode?> RunDialogAsync(List<string> args, CancellationToken cancellationToken)
    {
        var launch = Launch(args) with { HeadlessOutcomes = _options.Headless ? [_outcomes.Take()] : [] };
        return RunAsync(launch, cancellationToken);
    }

    private async Task<JsonNode?> RunAsync(RuntimeLaunch launch, CancellationToken cancellationToken)
    {
        var result = await _capture(launch, cancellationToken).ConfigureAwait(false);
        var node = result.StdOut is null ? null : JsonNode.Parse(result.StdOut);
        if (result.ExitCode != 0)
        {
            var code = node?["error"]?["code"]?.GetValue<string>();
            var message = node?["error"]?["message"]?.GetValue<string>() ?? $"The Dnui runtime exited with code {result.ExitCode}.";
            throw new DnuiCommandException(result, code, message);
        }

        return node;
    }

    private static void AddCommon(List<string> args, string? title, string? startFolder)
    {
        if (title is not null) args.AddRange(["--title", title]);
        if (startFolder is not null) args.AddRange(["--start-folder", startFolder]);
    }

    private static void AddFilters(List<string> args, IReadOnlyList<DnuiFileFilter> filters)
    {
        foreach (var filter in filters)
        {
            args.AddRange(["--filter", FileFilterParser.Format(filter)]);
        }
    }
}
