using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Dnui.Dialogs;
using Dnui.Hosting;

namespace Dnui;

/// <summary>Options for <see cref="DnuiHost"/>.</summary>
public sealed record DnuiHostOptions
{
    /// <summary>How the runtime is found. Null means defaults (found or downloaded automatically).</summary>
    public DnuiRuntimeOptions? Runtime { get; init; }

    /// <summary>Run window and dialog commands headless (no display; dialogs take <see cref="Outcomes"/>).</summary>
    public bool Headless { get; init; }

    /// <summary>Headless dialog outcomes, consumed in order (one per dialog call, or per parented dialog in a window).</summary>
    public IReadOnlyList<JsonNode?> Outcomes { get; init; } = [];
}

/// <summary>
/// Host commands (constitution I): every call starts a new runtime process. App overrides are
/// allowed, and it can be used from anywhere, including window code.
/// </summary>
public sealed class DnuiHost : IDnuiHostCommands
{
    private readonly DnuiHostOptions _options;
    private int _nextOutcome;

    /// <summary>Creates a host with the given options.</summary>
    public DnuiHost(DnuiHostOptions? options = null) => _options = options ?? new DnuiHostOptions();

    /// <summary>A host with default options.</summary>
    public static DnuiHost Default { get; } = new();

    /// <summary>Runs any one-shot runtime command line and returns its exit code and resolved line.</summary>
    public async Task<DnuiResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var runtime = await DnuiRuntime.ResolveAsync(_options.Runtime, cancellationToken).ConfigureAwait(false);
        AllowForeground();

        var info = new ProcessStartInfo(runtime)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start '{runtime}'.");
        process.StandardInput.Close();
        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var line = stdout.Trim();
            return new DnuiResult(process.ExitCode, line.Length == 0 ? null : line);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> OpenFilesAsync(DnuiOpenFileOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        List<string> args = ["dialog", "open-file"];
        AddCommon(args, options.Title, options.StartFolder);
        if (options.AllowMultiple) args.Add("--multiple");
        AddFilters(args, options.Filters);
        return DialogOutcome.ReadPaths(await RunTypedAsync(args, headlessOutcome: true, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> OpenFoldersAsync(DnuiOpenFolderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        List<string> args = ["dialog", "open-folder"];
        AddCommon(args, options.Title, options.StartFolder);
        if (options.AllowMultiple) args.Add("--multiple");
        return DialogOutcome.ReadPaths(await RunTypedAsync(args, headlessOutcome: true, cancellationToken).ConfigureAwait(false));
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
        return DialogOutcome.ReadPath(await RunTypedAsync(args, headlessOutcome: true, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow =>
        OpenWindowAsync(typeof(TWindow), null, cancellationToken);

    /// <inheritdoc />
    public Task<JsonNode?> OpenWindowAsync<TWindow, TApp>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow where TApp : DnuiCommandApp =>
        OpenWindowAsync(typeof(TWindow), typeof(TApp), cancellationToken);

    /// <inheritdoc />
    public Task<JsonNode?> OpenWindowAsync(Type windowType, Type? appType = null, CancellationToken cancellationToken = default) =>
        OpenWindowAsync(windowType.Assembly, windowType.FullName!, appType?.Assembly, appType?.FullName, cancellationToken);

    /// <inheritdoc />
    public async Task<JsonNode?> OpenWindowAsync(Assembly windowAssembly, string windowTypeName, Assembly? appAssembly = null, string? appTypeName = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string>();
        AddApp(args, appAssembly, appTypeName);
        args.AddRange(["window", AssemblyLocator.Find(windowAssembly), windowTypeName]);
        if (_options.Headless)
        {
            args.Add("--headless");
            foreach (var outcome in TakeRemainingOutcomes())
            {
                args.AddRange(["--dialog-outcome", outcome?.ToJsonString() ?? "null"]);
            }
        }

        var result = await RunTypedAsync(args, headlessOutcome: false, cancellationToken).ConfigureAwait(false);
        return result?["result"]?.DeepClone();
    }

    /// <inheritdoc />
    public Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken cancellationToken = default) =>
        DnuiTray.StartAsync(options with { Runtime = options.Runtime ?? _options.Runtime }, cancellationToken);

    /// <inheritdoc />
    public Task<DnuiTray> StartTrayAsync<TApp>(DnuiTrayOptions options, CancellationToken cancellationToken = default) where TApp : DnuiCommandApp =>
        StartTrayAsync(options with { AppAssembly = AssemblyLocator.Find(typeof(TApp).Assembly), AppType = typeof(TApp).FullName }, cancellationToken);

    private void AddApp(List<string> args, Assembly? appAssembly, string? appTypeName)
    {
        if (appAssembly is not null && appTypeName is not null)
        {
            args.AddRange(["--app-assembly", AssemblyLocator.Find(appAssembly), "--app-type", appTypeName]);
        }
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

    private async Task<JsonNode?> RunTypedAsync(List<string> args, bool headlessOutcome, CancellationToken cancellationToken)
    {
        if (_options.Headless && headlessOutcome)
        {
            args.AddRange(["--headless", "--outcome", TakeOutcome()?.ToJsonString() ?? "null"]);
        }

        var result = await RunAsync(args, cancellationToken).ConfigureAwait(false);
        var node = result.StdOut is null ? null : JsonNode.Parse(result.StdOut);
        if (result.ExitCode != 0)
        {
            var code = node?["error"]?["code"]?.GetValue<string>();
            var message = node?["error"]?["message"]?.GetValue<string>() ?? $"The Dnui runtime exited with code {result.ExitCode}.";
            throw new DnuiCommandException(result, code, message);
        }

        return node;
    }

    private JsonNode? TakeOutcome()
    {
        var index = Interlocked.Increment(ref _nextOutcome) - 1;
        return index < _options.Outcomes.Count ? _options.Outcomes[index]?.DeepClone() : null;
    }

    private IEnumerable<JsonNode?> TakeRemainingOutcomes()
    {
        var start = Interlocked.Exchange(ref _nextOutcome, _options.Outcomes.Count);
        for (var i = start; i < _options.Outcomes.Count; i++)
        {
            yield return _options.Outcomes[i]?.DeepClone();
        }
    }

    // Windows: let the process we're about to start take the foreground (research R9). Harmless
    // when this process has no foreground rights to give.
    private static void AllowForeground()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = AllowSetForegroundWindow(-1);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
