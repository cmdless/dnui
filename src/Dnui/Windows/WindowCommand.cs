using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Dnui.Commands;
using Dnui.Hosting;
using Dnui.Internal;

namespace Dnui.Windows;

/// <summary><c>window &lt;assembly&gt; &lt;type&gt;</c>: a one-shot host command (research R6).</summary>
internal sealed class WindowCommand(string assemblyPath, string typeName) : DnuiCommand
{
    private Type? _windowType;

    public override bool IsSession => false;

    public override int? Prepare(DnuiCommandApp app)
    {
        var result = DnuiTypeLoader.Load(assemblyPath, typeName, typeof(DnuiWindow));
        if (result.Type is null)
        {
            var code = ErrorResults.ForWindow(result.Failure);
            app.ResolveInternal(ErrorResults.Error(code, result.Message!), code);
            return code;
        }

        _windowType = result.Type;
        return null;
    }

    public override void Run(DnuiCommandApp app, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Fail(app, e.Exception);
        };

        DnuiWindow window;
        try
        {
            window = (DnuiWindow)Activator.CreateInstance(_windowType!)!;
        }
        catch (TargetInvocationException ex)
        {
            Fail(app, ex.InnerException ?? ex);
            return;
        }

        DnuiPlatformHooks.BringToFront?.Invoke();
        window.Show();
        window.Activate();
        window.Completion.ContinueWith(
            t => app.ResolveInternal(new JsonObject { ["result"] = t.Result }, 0),
            TaskScheduler.Default);
    }

    private static void Fail(DnuiCommandApp app, Exception ex)
    {
        Console.Error.WriteLine($"dnui: window failed: {ex}");
        app.ResolveInternal(ErrorResults.Error(ErrorResults.WindowFailed, ex.Message), ErrorResults.WindowFailed);
    }
}
