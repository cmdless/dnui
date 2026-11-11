using System.CommandLine;

namespace Dnui.Runtime.Cli;

/// <summary>The parsed command line (contracts/cli.md).</summary>
internal sealed record RuntimeInvocation(
    string? AppAssembly,
    string? AppType,
    string? CommandName,
    bool Headless,
    bool SessionRequested,
    IReadOnlyList<string> Errors,
    ParseResult ParseResult,
    bool IsHelpOrVersion);

internal static class RuntimeCommandLine
{
    public const string TrayCommandName = "tray";

    private static readonly HashSet<string> s_sessionCommands = [TrayCommandName];

    private static readonly Option<string?> s_appAssembly = new("--app-assembly")
    {
        Description = "Path to a .NET assembly containing the app type.",
        Recursive = true,
    };

    private static readonly Option<string?> s_appType = new("--app-type")
    {
        Description = "Full name of a type deriving from Dnui.DnuiApp, resolved in --app-assembly.",
        Recursive = true,
    };

    private static readonly Option<bool> s_headless = new("--headless")
    {
        Description = "Run the tray without a real OS tray icon (tests, CI).",
    };

    private static readonly Command s_tray = CreateTray();
    private static readonly RootCommand s_root = CreateRoot();

    private static Command CreateTray()
    {
        var tray = new Command(TrayCommandName, "Show one system tray icon controlled by JSON-RPC over stdio.");
        tray.Options.Add(s_headless);
        tray.SetAction(_ => 0);
        return tray;
    }

    private static RootCommand CreateRoot()
    {
        var root = new RootCommand("Dnui runtime: a cached Avalonia host driven by commands.");
        root.Options.Add(s_appAssembly);
        root.Options.Add(s_appType);
        root.Subcommands.Add(s_tray);
        root.SetAction(_ => 0);
        return root;
    }

    public static RuntimeInvocation Parse(string[] args)
    {
        var result = s_root.Parse(args);
        var command = result.CommandResult.Command;
        var errors = result.Errors.Select(e => e.Message).ToList();

        var isOwnAction = result.Action is null || ReferenceEquals(result.Action, s_root.Action) || ReferenceEquals(result.Action, s_tray.Action);
        var isHelpOrVersion = errors.Count == 0 && !isOwnAction;

        string? commandName = ReferenceEquals(command, s_root) ? null : command.Name;
        var appAssembly = result.GetValue(s_appAssembly);
        var appType = result.GetValue(s_appType);

        if ((appAssembly is null) != (appType is null))
        {
            errors.Add("--app-assembly and --app-type must be given together.");
        }

        var sessionRequested = commandName is not null
            ? s_sessionCommands.Contains(commandName)
            : errors.Count > 0 && FirstPositional(args) is { } first && s_sessionCommands.Contains(first);

        return new RuntimeInvocation(
            appAssembly,
            appType,
            commandName,
            ReferenceEquals(command, s_tray) && result.GetValue(s_headless),
            sessionRequested,
            errors,
            result,
            isHelpOrVersion);
    }

    private static string? FirstPositional(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--app-assembly" or "--app-type")
            {
                i++;
                continue;
            }

            if (!arg.StartsWith('-'))
            {
                return arg;
            }
        }

        return null;
    }
}
