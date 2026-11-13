using System.CommandLine;
using Dnui.Dialogs;

namespace Dnui.Runtime.Cli;

/// <summary>The parsed command line (spec 001 and spec 002 contracts/cli.md).</summary>
internal sealed record RuntimeInvocation(
    string? AppAssembly,
    string? AppType,
    string? CommandName,
    bool SessionRequested,
    IReadOnlyList<string> Errors,
    ParseResult ParseResult,
    bool IsHelpOrVersion)
{
    public string? WindowAssembly { get; init; }

    public string? WindowType { get; init; }

    public DialogKind? DialogKind { get; init; }

    public object? DialogOptions { get; init; }
}

internal static class RuntimeCommandLine
{
    public const string TrayCommandName = "tray";
    public const string WindowCommandName = "window";
    public const string DialogCommandName = "dialog";

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

    // window
    private static readonly Argument<string> s_windowAssembly = new("assembly-path") { Description = "IL assembly containing the window type." };
    private static readonly Argument<string> s_windowType = new("type-name") { Description = "Full name of a Dnui.DnuiWindow subclass." };

    // dialog (shared option instances are not allowed across commands, so each subcommand builds its own)
    private sealed record DialogOptionSet(
        Option<string?> Title, Option<bool> Multiple, Option<string[]> Filter, Option<string?> StartFolder,
        Option<string?> SuggestedName, Option<string?> DefaultExtension, Option<bool> NoOverwritePrompt);

    private static readonly Command s_tray = CreateTray();
    private static readonly Command s_window = CreateWindow();
    private static readonly Dictionary<Command, (DialogKind Kind, DialogOptionSet Options)> s_dialogs = [];
    private static readonly Command s_dialog = CreateDialog();
    private static readonly RootCommand s_root = CreateRoot();

    private static Command CreateTray()
    {
        var tray = new Command(TrayCommandName, "Show one system tray icon controlled by JSON-RPC over stdio.");
        tray.SetAction(_ => 0);
        return tray;
    }

    private static Command CreateWindow()
    {
        var window = new Command(WindowCommandName, "Show a Dnui.DnuiWindow from an assembly and resolve its result.");
        window.Arguments.Add(s_windowAssembly);
        window.Arguments.Add(s_windowType);
        window.SetAction(_ => 0);
        return window;
    }

    private static Command CreateDialog()
    {
        var dialog = new Command(DialogCommandName, "Show a built-in dialog and resolve the chosen path(s).");
        dialog.Subcommands.Add(CreateDialogKind("open-file", DialogKind.OpenFile, "Choose one or more files to open."));
        dialog.Subcommands.Add(CreateDialogKind("open-folder", DialogKind.OpenFolder, "Choose one or more folders."));
        dialog.Subcommands.Add(CreateDialogKind("save-file", DialogKind.SaveFile, "Choose where to save a file."));
        return dialog;
    }

    private static Command CreateDialogKind(string name, DialogKind kind, string description)
    {
        var set = new DialogOptionSet(
            new("--title") { Description = "Dialog title." },
            new("--multiple") { Description = "Allow selecting more than one item." },
            new("--filter") { Description = "File-type filter Name:pattern,pattern (repeatable).", Arity = ArgumentArity.ZeroOrMore },
            new("--start-folder") { Description = "Initial folder (ignored if missing)." },
            new("--suggested-name") { Description = "Prefilled file name." },
            new("--default-extension") { Description = "Extension appended when none is typed." },
            new("--no-overwrite-prompt") { Description = "Don't ask before choosing an existing file." });

        var command = new Command(name, description);
        command.Options.Add(set.Title);
        command.Options.Add(set.StartFolder);
        if (kind != DialogKind.SaveFile)
        {
            command.Options.Add(set.Multiple);
        }

        if (kind != DialogKind.OpenFolder)
        {
            command.Options.Add(set.Filter);
        }

        if (kind == DialogKind.SaveFile)
        {
            command.Options.Add(set.SuggestedName);
            command.Options.Add(set.DefaultExtension);
            command.Options.Add(set.NoOverwritePrompt);
        }

        command.SetAction(_ => 0);
        s_dialogs[command] = (kind, set);
        return command;
    }

    private static RootCommand CreateRoot()
    {
        var root = new RootCommand("Dnui runtime: a cached Avalonia host driven by commands.");
        root.Options.Add(s_appAssembly);
        root.Options.Add(s_appType);
        root.Subcommands.Add(s_tray);
        root.Subcommands.Add(s_window);
        root.Subcommands.Add(s_dialog);
        root.SetAction(_ => 0);
        return root;
    }

    public static RuntimeInvocation Parse(string[] args)
    {
        var result = s_root.Parse(args);
        var command = result.CommandResult.Command;
        var errors = result.Errors.Select(e => e.Message).ToList();

        var ownActions = new[] { s_root.Action, s_tray.Action, s_window.Action }.Concat(s_dialogs.Keys.Select(k => k.Action));
        var isOwnAction = result.Action is null || ownActions.Any(a => ReferenceEquals(a, result.Action));
        var isHelpOrVersion = errors.Count == 0 && !isOwnAction;

        string? commandName = ReferenceEquals(command, s_root) ? null
            : s_dialogs.ContainsKey(command) || ReferenceEquals(command, s_dialog) ? DialogCommandName
            : command.Name;
        var appAssembly = result.GetValue(s_appAssembly);
        var appType = result.GetValue(s_appType);

        if ((appAssembly is null) != (appType is null))
        {
            errors.Add("--app-assembly and --app-type must be given together.");
        }

        if (ReferenceEquals(command, s_dialog) && errors.Count == 0)
        {
            errors.Add("dialog needs a kind: open-file, open-folder, or save-file.");
        }

        var sessionRequested = commandName is not null
            ? s_sessionCommands.Contains(commandName)
            : errors.Count > 0 && FirstPositional(args) is { } first && s_sessionCommands.Contains(first);

        var invocation = new RuntimeInvocation(
            appAssembly,
            appType,
            commandName,
            sessionRequested,
            errors,
            result,
            isHelpOrVersion);

        if (ReferenceEquals(command, s_window))
        {
            invocation = invocation with
            {
                WindowAssembly = SafeValue(result, s_windowAssembly),
                WindowType = SafeValue(result, s_windowType),
            };
        }
        else if (s_dialogs.TryGetValue(command, out var dialog))
        {
            invocation = invocation with
            {
                DialogKind = dialog.Kind,
                DialogOptions = BuildDialogOptions(result, dialog.Kind, dialog.Options, errors),
            };
        }

        return invocation;
    }

    /// <summary>Reads an argument without throwing when it is missing (that's already a parse error).</summary>
    private static string? SafeValue(ParseResult result, Argument<string> argument)
    {
        try
        {
            return result.GetResult(argument) is null ? null : result.GetValue(argument);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static object BuildDialogOptions(ParseResult result, DialogKind kind, DialogOptionSet o, List<string> errors)
    {
        var filters = new List<DnuiFileFilter>();
        foreach (var text in result.GetValue(o.Filter) ?? [])
        {
            var (filter, error) = FileFilterParser.Parse(text);
            if (filter is null)
            {
                errors.Add($"--filter: {error}.");
            }
            else
            {
                filters.Add(filter);
            }
        }

        return kind switch
        {
            DialogKind.OpenFile => new DnuiOpenFileOptions
            {
                Title = result.GetValue(o.Title),
                AllowMultiple = result.GetValue(o.Multiple),
                Filters = filters,
                StartFolder = result.GetValue(o.StartFolder),
            },
            DialogKind.OpenFolder => new DnuiOpenFolderOptions
            {
                Title = result.GetValue(o.Title),
                AllowMultiple = result.GetValue(o.Multiple),
                StartFolder = result.GetValue(o.StartFolder),
            },
            _ => new DnuiSaveFileOptions
            {
                Title = result.GetValue(o.Title),
                SuggestedName = result.GetValue(o.SuggestedName),
                DefaultExtension = result.GetValue(o.DefaultExtension),
                Filters = filters,
                StartFolder = result.GetValue(o.StartFolder),
                OverwritePrompt = !result.GetValue(o.NoOverwritePrompt),
            },
        };
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
