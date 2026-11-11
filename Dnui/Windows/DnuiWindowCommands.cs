using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Dnui.Dialogs;
using Dnui.Hosting;
using Dnui.Internal;

namespace Dnui.Windows;

/// <summary>
/// Window commands, in-process (research R7): dialogs parented to the window and shown one at a
/// time, and owned child windows. No app override, no tray.
/// </summary>
internal sealed class DnuiWindowCommands : IDnuiWindowCommands
{
    private readonly DnuiWindow _owner;
    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private readonly TaskCompletionSource _ownerClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DnuiWindowCommands(DnuiWindow owner)
    {
        _owner = owner;
        owner.Closed += (_, _) => _ownerClosed.TrySetResult();
    }

    public async Task<IReadOnlyList<string>?> OpenFilesAsync(DnuiOpenFileOptions? options = null, CancellationToken cancellationToken = default) =>
        DialogOutcome.ReadPaths(await RunDialogAsync(DialogKind.OpenFile, options ?? new(), cancellationToken).ConfigureAwait(true));

    public async Task<IReadOnlyList<string>?> OpenFoldersAsync(DnuiOpenFolderOptions? options = null, CancellationToken cancellationToken = default) =>
        DialogOutcome.ReadPaths(await RunDialogAsync(DialogKind.OpenFolder, options ?? new(), cancellationToken).ConfigureAwait(true));

    public async Task<string?> SaveFileAsync(DnuiSaveFileOptions? options = null, CancellationToken cancellationToken = default) =>
        DialogOutcome.ReadPath(await RunDialogAsync(DialogKind.SaveFile, options ?? new(), cancellationToken).ConfigureAwait(true));

    public Task<JsonNode?> OpenWindowAsync<TWindow>(CancellationToken cancellationToken = default) where TWindow : DnuiWindow, new() =>
        ShowChildAsync(() => new TWindow(), cancellationToken);

    public Task<JsonNode?> OpenWindowAsync(Type windowType, CancellationToken cancellationToken = default)
    {
        var check = DnuiTypeLoader.CheckType(windowType, typeof(DnuiWindow));
        return check.Type is { } type
            ? ShowChildAsync(() => (DnuiWindow)Activator.CreateInstance(type)!, cancellationToken)
            : Task.FromException<JsonNode?>(Fail(check));
    }

    public Task<JsonNode?> OpenWindowAsync(Assembly assembly, string typeName, CancellationToken cancellationToken = default)
    {
        var found = DnuiTypeLoader.FindType(assembly, typeName, typeof(DnuiWindow));
        return found.Type is { } type
            ? ShowChildAsync(() => (DnuiWindow)Activator.CreateInstance(type)!, cancellationToken)
            : Task.FromException<JsonNode?>(Fail(found));
    }

    private static DnuiCommandException Fail(TypeLoadResult result)
    {
        var code = ErrorResults.ForWindow(result.Failure);
        return new DnuiCommandException(new DnuiResult(code, null), ErrorResults.NameOf(code), result.Message ?? "Window type rejected.");
    }

    private async Task<JsonNode?> ShowChildAsync(Func<DnuiWindow> create, CancellationToken cancellationToken)
    {
        var child = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = create();
            window.Show(_owner);
            return window;
        });

        using (cancellationToken.Register(() => Dispatcher.UIThread.Post(child.Close)))
        {
            var result = await child.Completion.ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    private async Task<JsonObject> RunDialogAsync(DialogKind kind, object options, CancellationToken cancellationToken)
    {
        await _dialogGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (_ownerClosed.Task.IsCompleted)
            {
                return DialogOutcome.Cancelled(kind);
            }

            var dialog = Dispatcher.UIThread.InvokeAsync(() => DialogRunner.RunAsync(_owner, kind, options));
            var cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            var finished = await Task.WhenAny(dialog, _ownerClosed.Task, cancelled).ConfigureAwait(true);
            if (finished == cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return finished == dialog ? await dialog.ConfigureAwait(true) : DialogOutcome.Cancelled(kind);
        }
        catch (DialogUnsupportedException ex)
        {
            throw new DnuiCommandException(new DnuiResult(ErrorResults.DialogUnsupported, null), ErrorResults.NameOf(ErrorResults.DialogUnsupported), ex.Message, ex);
        }
        finally
        {
            _dialogGate.Release();
        }
    }
}
