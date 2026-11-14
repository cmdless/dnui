using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Dnui;

/// <summary>
/// Base class for every app hosted by the Dnui runtime. Derive from it directly to take over the
/// whole app (Dnui is then just a cached Avalonia host), or from <see cref="DnuiCommandApp"/> to
/// customize the built-in commands.
/// </summary>
public abstract class DnuiApp : Application
{
    /// <summary>Creates the app, picking up the stdio the runtime captured.</summary>
    protected DnuiApp()
    {
        // The runtime sets Current around construction; designers and tools get a detached stdio.
        Stdio = DnuiStdio.Current ?? DnuiStdio.CreateDetached();
    }

    /// <summary>
    /// The process's captured stdin/stdout. Captured, with <see cref="Console.Out"/> redirected to
    /// stderr, before this instance is constructed.
    /// </summary>
    protected DnuiStdio Stdio { get; }

    internal DnuiStdio StdioInternal => Stdio;

    /// <summary>A resolution requested before the lifetime started; the runtime writes it and exits.</summary>
    internal (JsonNode? Result, int ExitCode)? PendingResolution { get; private set; }

    /// <summary>
    /// Customizes the app builder. The base applies the defaults: Inter font, trace logging, and
    /// developer tools in Debug builds. Call <c>base.Configure(builder)</c> to keep them.
    /// </summary>
    protected virtual void Configure(AppBuilder builder)
    {
        builder.WithInterFont().LogToTrace();
#if DEBUG
        builder.WithDeveloperTools();
#endif
    }

    internal virtual void ApplyConfiguration(AppBuilder builder) => Configure(builder);

    /// <summary>
    /// The single way to produce the app's outcome: writes <paramref name="result"/> as one JSON line
    /// to stdout (nothing when null), then shuts down with <paramref name="exitCode"/>. The first call
    /// wins; later calls are ignored. Safe from any thread. For errors pass
    /// <c>{"error":{"code","message"}}</c> with a non-zero exit code (64–255 for app codes).
    /// </summary>
    protected void Resolve(JsonNode? result, int exitCode = 0) => ResolveInternal(result, exitCode);

    internal void ResolveInternal(JsonNode? result, int exitCode)
    {
        if (!Stdio.TryBeginResolve(ref result))
        {
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Dispatcher.UIThread.Post(() =>
            {
                WriteResult(result);
                desktop.Shutdown(exitCode);
            });
        }
        else
        {
            PendingResolution = (result, exitCode);
        }
    }

    internal void WriteResult(JsonNode? result)
    {
        if (result is not null)
        {
            Stdio.WriteResultLine(result);
        }
    }
}
