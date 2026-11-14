using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Dnui;

/// <summary>
/// Base class for windows shown by the <c>window</c> host command or opened as child windows.
/// Resolve one result with <see cref="Resolve"/>; closing without resolving yields null.
/// </summary>
public abstract class DnuiWindow : Window
{
    private readonly TaskCompletionSource<JsonNode?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Windows.DnuiWindowCommands? _ui;

    /// <summary>Creates the window.</summary>
    protected DnuiWindow()
    {
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    /// <summary>Window commands for this window: parented dialogs and owned child windows.</summary>
    public IDnuiWindowCommands Ui => _ui ??= new Windows.DnuiWindowCommands(this);

    /// <summary>Completes with the resolved result, or null when the window closed without resolving.</summary>
    internal Task<JsonNode?> Completion => _completion.Task;

    /// <summary>Resolves this window's result and closes it. The first call wins; safe from any thread.</summary>
    protected void Resolve(JsonNode? result)
    {
        if (!_completion.TrySetResult(result?.DeepClone()))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Close();
        }
        else
        {
            Dispatcher.UIThread.Post(Close);
        }
    }
}
