using System.Text.Json.Nodes;

namespace Dnui;

/// <summary>The built-in dialogs, shared by host commands and window commands.</summary>
public interface IDnuiDialogs
{
    /// <summary>Asks for one or more files. Returns null when cancelled.</summary>
    Task<IReadOnlyList<string>?> OpenFilesAsync(DnuiOpenFileOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Asks for one or more folders. Returns null when cancelled.</summary>
    Task<IReadOnlyList<string>?> OpenFoldersAsync(DnuiOpenFolderOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Asks where to save. Returns null when cancelled.</summary>
    Task<string?> SaveFileAsync(DnuiSaveFileOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Window commands: run inside a window's process, parented to it. No app override and no tray
/// (constitution I). Get them from <see cref="DnuiWindow.Ui"/>. The overloads form a chain:
/// generic → <see cref="Type"/> → assembly path and type name.
/// </summary>
public interface IDnuiWindowCommands : IDnuiDialogs
{
    /// <summary>Opens an owned child window and returns its result (null when closed).</summary>
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow;

    /// <summary>Opens an owned child window of the given type and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken cancellationToken = default);

    /// <summary>Opens an owned child window by type name from an assembly path (loaded once, reused if already loaded).</summary>
    Task<JsonNode?> OpenWindowAsync(string windowAssemblyPath, string windowTypeName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Host commands: each call starts a new runtime process. They can be called from anywhere,
/// including window code (the UI is then top-level and unparented). Every overload chain ends in
/// the string form, which maps one-to-one to the CLI arguments.
/// </summary>
public interface IDnuiHostCommands : IDnuiDialogs
{
    /// <summary>Host commands that run with a custom app (<c>--app-assembly</c>/<c>--app-type</c>).</summary>
    IDnuiHostCommands WithApp<TApp>() where TApp : DnuiCommandApp;

    /// <summary>Host commands that run with the given app type, which must derive from <see cref="DnuiCommandApp"/>.</summary>
    IDnuiHostCommands WithApp(Type appType);

    /// <summary>Host commands that run with the app type named in the assembly at the given path.</summary>
    IDnuiHostCommands WithApp(string appAssemblyPath, string appTypeName);

    /// <summary>Opens a window in a new process and returns its result (null when closed).</summary>
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow;

    /// <summary>Opens a window of the given type in a new process and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken cancellationToken = default);

    /// <summary>Opens a window by type name from an assembly path in a new process (<c>window &lt;assembly&gt; &lt;type&gt;</c>).</summary>
    Task<JsonNode?> OpenWindowAsync(string windowAssemblyPath, string windowTypeName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a tray session. Attach event handlers in <see cref="DnuiTrayOptions.Configure"/> so no
    /// event is missed. Dispose the returned tray to remove it.
    /// </summary>
    Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken cancellationToken = default);
}
