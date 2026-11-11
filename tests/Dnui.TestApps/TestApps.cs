using System.Text;
using System.Text.Json.Nodes;
using Avalonia;

namespace Dnui.TestApps;

/// <summary>A command app that customizes startup and leaves a marker on stderr.</summary>
public class CustomCommandApp : DnuiCommandApp
{
    protected override void Configure(AppBuilder builder)
    {
        base.Configure(builder);
        Console.Error.WriteLine("dnui-test: custom-configure");
    }
}

/// <summary>Takes over the app and resolves a single result.</summary>
public class TakeoverApp : DnuiApp
{
    public override void OnFrameworkInitializationCompleted()
    {
        Resolve(new JsonObject { ["takeover"] = true });
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Takes over the app and runs its own line-echo protocol on a stdio session.</summary>
public class EchoSessionApp : DnuiApp
{
    public override void OnFrameworkInitializationCompleted()
    {
        var session = Stdio.OpenSession();
        _ = Task.Run(async () =>
        {
            using var reader = new StreamReader(session.Input, Encoding.UTF8);
            await using var writer = new StreamWriter(session.Output, new UTF8Encoding(false)) { AutoFlush = true };
            while (await reader.ReadLineAsync() is { } line)
            {
                await writer.WriteLineAsync("echo: " + line);
            }

            Resolve(null, 0);
        });
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Not part of the app model; its static constructor must never run.</summary>
public class NotAnApp
{
    static NotAnApp() => Console.Error.WriteLine("dnui-test: NotAnApp-initialized");
}

/// <summary>Claims stdio in its constructor, so a session command can't.</summary>
public class EagerSessionCommandApp : DnuiCommandApp
{
    public EagerSessionCommandApp() => Stdio.OpenSession();
}

/// <summary>Writes stray console output constantly while running a command.</summary>
public class SpammerCommandApp : DnuiCommandApp
{
    private readonly Timer _timer;

    public SpammerCommandApp() => _timer = new Timer(_ => Console.WriteLine("noise noise noise"), null, 0, 1);
}
