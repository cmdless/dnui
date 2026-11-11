# Contract: Library API for Windows and Dialogs

Package `Cmdless.Dnui`, namespace `Dnui`. Adds to spec 001's public API (app model, tray client,
runtime acquisition).

```csharp
namespace Dnui;

// ---- Dialog options (shared by both surfaces) ----
public sealed record DnuiFileFilter(string Name, IReadOnlyList<string> Patterns);

public sealed record DnuiOpenFileOptions
{
    public string? Title { get; init; }
    public bool AllowMultiple { get; init; }
    public IReadOnlyList<DnuiFileFilter> Filters { get; init; } = [];
    public string? StartFolder { get; init; }
}

public sealed record DnuiOpenFolderOptions
{
    public string? Title { get; init; }
    public bool AllowMultiple { get; init; }
    public string? StartFolder { get; init; }
}

public sealed record DnuiSaveFileOptions
{
    public string? Title { get; init; }
    public string? SuggestedName { get; init; }
    public string? DefaultExtension { get; init; }
    public IReadOnlyList<DnuiFileFilter> Filters { get; init; } = [];
    public string? StartFolder { get; init; }
    public bool OverwritePrompt { get; init; } = true;
}

// ---- Surfaces ----
public interface IDnuiDialogs
{
    Task<IReadOnlyList<string>?> OpenFilesAsync(DnuiOpenFileOptions? options = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>?> OpenFoldersAsync(DnuiOpenFolderOptions? options = null, CancellationToken ct = default);
    Task<string?> SaveFileAsync(DnuiSaveFileOptions? options = null, CancellationToken ct = default);
}

/// In-process, parented to the owning window. No app override, no tray.
public interface IDnuiWindowCommands : IDnuiDialogs
{
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken ct = default) where TWindow : DnuiWindow, new();
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken ct = default);
    Task<JsonNode?> OpenWindowAsync(Assembly assembly, string typeName, CancellationToken ct = default);
}

/// New runtime process per call. App override allowed. Usable from anywhere.
public interface IDnuiHostCommands : IDnuiDialogs
{
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken ct = default) where TWindow : DnuiWindow;
    Task<JsonNode?> OpenWindowAsync<TWindow, TApp>(CancellationToken ct = default) where TWindow : DnuiWindow where TApp : DnuiCommandApp;
    Task<JsonNode?> OpenWindowAsync(Type windowType, Type? appType = null, CancellationToken ct = default);
    Task<JsonNode?> OpenWindowAsync(Assembly windowAssembly, string windowTypeName,
                                    Assembly? appAssembly = null, string? appTypeName = null, CancellationToken ct = default);

    Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken ct = default);
    Task<DnuiTray> StartTrayAsync<TApp>(DnuiTrayOptions options, CancellationToken ct = default) where TApp : DnuiCommandApp;
}

// ---- Windows ----
public abstract class DnuiWindow : Avalonia.Controls.Window
{
    /// Window commands for this window: parented dialogs and owned child windows.
    public IDnuiWindowCommands Ui { get; }

    /// Resolves this window's result and closes it. First call wins; closing without resolving yields null.
    protected void Resolve(JsonNode? result);
}

// ---- Host commands ----
public sealed record DnuiHostOptions
{
    public DnuiRuntimeOptions? Runtime { get; init; }
    public bool Headless { get; init; }
    /// Headless dialog outcomes, consumed in order (one per dialog call, or per parented dialog in a window).
    public IReadOnlyList<JsonNode?> Outcomes { get; init; } = [];
}

public sealed class DnuiHost(DnuiHostOptions? options = null) : IDnuiHostCommands
{
    public static DnuiHost Default { get; }

    /// Runs any one-shot runtime command line and returns its exit code and resolved line.
    public Task<DnuiResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct = default);

    // + IDnuiHostCommands members
}

public sealed class DnuiCommandException : Exception
{
    public DnuiResult Result { get; }
    public string? ErrorCode { get; }       // error.code, e.g. "WindowTypeInvalid", "DialogUnsupported"
}

public sealed class DnuiAssemblyNotFoundException : Exception
{
    public string AssemblyName { get; }
    public IReadOnlyList<string> SearchedPaths { get; }
}
```

## Semantics

- **Cancellation (user)**: dialogs return `null`; windows return `null` when closed without
  resolving.
- **Cancellation (`CancellationToken`)**: host commands kill their process and throw
  `OperationCanceledException`. Window commands close the dialog or child window, then throw.
- **Errors**: host commands throw `DnuiCommandException` for non-zero exits (window loading 13–17,
  `DialogUnsupported` 30). Window commands throw `DnuiCommandException` with the same
  `ErrorCode`. There's no process, so `Result` holds the code that would have been the exit code and
  a null `StdOut`.
- **Assembly resolution** (host commands): see [data-model.md](../data-model.md). The tray shortcut
  `DnuiTray.StartAsync` is unchanged. `DnuiHost.StartTrayAsync` delegates to it.
- **Same component, both surfaces**: code taking `IDnuiDialogs` works with `DnuiHost.Default`
  (unparented processes) and with `this.Ui` inside a window (parented, in-process).

## Build integration (`buildTransitive/Cmdless.Dnui.targets`)

| Property | Default | Effect |
|---|---|---|
| `DnuiShipUiAssemblies` | `true` | on single-file or native AOT publish, copy the project's IL, `.deps.json`, and private IL dependencies to `$(PublishDir)dnui/` |

Normal publishes are unchanged.

## Compatibility

All types above join the public contract (constitution V). The public-API guard test lists them.
