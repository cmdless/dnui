using Avalonia;
using Dnui.Commands;
using Dnui.Dialogs;
using Dnui.Internal;
using Dnui.Windows;
using Dnui.Runtime.Cli;
using Dnui.Runtime.Hosting;
using Dnui.Runtime.Platform;
using Dnui.Tray;

namespace Dnui.Runtime;

internal static class Program
{
    // Fully synchronous until Avalonia starts (research R1–R6). Don't use Avalonia or any
    // SynchronizationContext-reliant code before BuildApp.
    [STAThread]
    public static int Main(string[] args)
    {
        RuntimeInvocation cli;
        try
        {
            cli = RuntimeCommandLine.Parse(args);
        }
        catch (Exception ex)
        {
            // Never crash on bad input: report it like any unexpected failure.
            Console.Error.WriteLine(ex);
            return RuntimeFailure.Fail(DnuiStdio.CreateFromConsole(), sessionRequested: false, ExitCodes.Unexpected, ex.Message);
        }

        if (cli.IsHelpOrVersion)
        {
            return cli.ParseResult.Invoke();
        }

        var stdio = DnuiStdio.CreateFromConsole();
        if (OperatingSystem.IsMacOS())
        {
            DnuiPlatformHooks.BringToFront = MacForeground.Activate;
        }

        try
        {
            return Run(cli, stdio);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return RuntimeFailure.Fail(stdio, cli.SessionRequested, ExitCodes.Unexpected, ex.Message);
        }
    }

    private static int Run(RuntimeInvocation cli, DnuiStdio stdio)
    {
        if (cli.Errors.Count > 0)
        {
            return RuntimeFailure.Fail(stdio, cli.SessionRequested, ExitCodes.Usage, string.Join(" ", cli.Errors));
        }

        var resolution = AppTypeResolver.Resolve(cli.AppAssembly, cli.AppType);
        if (resolution.AppType is not { } appType)
        {
            return RuntimeFailure.Fail(stdio, cli.SessionRequested, resolution.ExitCode, resolution.Error!);
        }

        var app = CreateApp(appType, stdio);
        DnuiCommand? command = null;

        if (app is DnuiCommandApp commandApp)
        {
            if (cli.CommandName is null)
            {
                return RuntimeFailure.Fail(stdio, cli.SessionRequested, ExitCodes.Usage, "A command is required (for example: tray).");
            }

            command = CreateCommand(cli);
            if (cli.CommandName == RuntimeCommandLine.WindowCommandName && cli.Headless)
            {
                HeadlessOutcomes.Enable(cli.DialogOutcomes);
            }

            if (command.Prepare(commandApp) is { } abortCode)
            {
                // A one-shot command may have resolved its outcome (or an error) already.
                if (app.PendingResolution is { } early)
                {
                    app.WriteResult(early.Result);
                    return early.ExitCode;
                }

                return abortCode;
            }

            if (cli.CommandName == RuntimeCommandLine.TrayCommandName && !cli.Headless && OperatingSystem.IsLinux()
                && !LinuxTrayHostProbe.IsTrayHostAvailable(TimeSpan.FromSeconds(5)))
            {
                Console.Error.WriteLine("dnui: no system tray host (org.kde.StatusNotifierWatcher) on the session bus.");
                return ExitCodes.TrayHostUnavailable;
            }

            commandApp.SetCommand(command);
        }
        else if (cli.CommandName is not null)
        {
            return RuntimeFailure.Fail(stdio, cli.SessionRequested, ExitCodes.Usage, $"'{appType.FullName}' takes over the app; it does not accept the '{cli.CommandName}' command.");
        }

        if (app.PendingResolution is { } pending)
        {
            app.WriteResult(pending.Result);
            return pending.ExitCode;
        }

        var builder = HeadlessPlatform.Use(AppBuilder.Configure(() => app), cli);
        if (command is not null)
        {
            PlatformCommandOptions.Apply(builder, command);
        }

        app.ApplyConfiguration(builder);
        var exitCode = builder.StartWithClassicDesktopLifetime([]);

        // A Resolve that landed after the lifetime ended (or never ran) is still honored.
        if (app.PendingResolution is { } late)
        {
            app.WriteResult(late.Result);
            return late.ExitCode;
        }

        return exitCode;
    }

    private static DnuiApp CreateApp(Type appType, DnuiStdio stdio)
    {
        DnuiStdio.Current = stdio;
        try
        {
            return (DnuiApp)Activator.CreateInstance(appType)!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        finally
        {
            DnuiStdio.Current = null;
        }
    }

    private static DnuiCommand CreateCommand(RuntimeInvocation cli) => cli.CommandName switch
    {
        RuntimeCommandLine.TrayCommandName => new TrayCommand(cli.Headless),
        RuntimeCommandLine.WindowCommandName => new WindowCommand(cli.WindowAssembly!, cli.WindowType!),
        RuntimeCommandLine.DialogCommandName => new DialogCommand(cli.DialogKind!.Value, cli.DialogOptions!, cli.Headless, cli.Outcome),
        _ => throw new InvalidOperationException($"Unknown command '{cli.CommandName}'."),
    };

    // Avalonia configuration for the visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
