using System.Reflection;
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
/// (constitution I). Get them from <see cref="DnuiWindow.Ui"/>.
/// </summary>
public interface IDnuiWindowCommands : IDnuiDialogs
{
    /// <summary>Opens an owned child window and returns its result (null when closed).</summary>
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow, new();

    /// <summary>Opens an owned child window of the given type and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken cancellationToken = default);

    /// <summary>Opens an owned child window by type name from an assembly and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync(Assembly assembly, string typeName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Host commands: each call starts a new runtime process. App overrides are allowed, and they can
/// be called from anywhere, including window code (the UI is then top-level and unparented).
/// </summary>
public interface IDnuiHostCommands : IDnuiDialogs
{
    /// <summary>Opens a window in a new process with the built-in app and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow;

    /// <summary>Opens a window in a new process with a custom app and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync<TWindow, TApp>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow where TApp : DnuiCommandApp;

    /// <summary>Opens a window type (and optional app type) in a new process and returns its result.</summary>
    Task<JsonNode?> OpenWindowAsync(Type windowType, Type? appType = null, CancellationToken cancellationToken = default);

    /// <summary>Opens a window by type name from an assembly (and optional app) in a new process.</summary>
    Task<JsonNode?> OpenWindowAsync(Assembly windowAssembly, string windowTypeName, Assembly? appAssembly = null, string? appTypeName = null, CancellationToken cancellationToken = default);

    /// <summary>Starts a tray with the built-in app.</summary>
    Task<DnuiTray> StartTrayAsync(DnuiTrayOptions options, CancellationToken cancellationToken = default);

    /// <summary>Starts a tray with a custom app.</summary>
    Task<DnuiTray> StartTrayAsync<TApp>(DnuiTrayOptions options, CancellationToken cancellationToken = default) where TApp : DnuiCommandApp;
}
