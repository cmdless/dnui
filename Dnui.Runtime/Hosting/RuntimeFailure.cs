using System.Text.Json.Nodes;

namespace Dnui.Runtime.Hosting;

/// <summary>Reports runtime-level failures per the output contract.</summary>
internal static class RuntimeFailure
{
    /// <summary>
    /// Writes a diagnostic to stderr and, unless a session command was requested, a single
    /// <c>{"error":{"code","message"}}</c> line to stdout. Returns <paramref name="code"/>.
    /// </summary>
    public static int Fail(DnuiStdio stdio, bool sessionRequested, int code, string message)
    {
        Console.Error.WriteLine($"dnui: {message}");
        if (!sessionRequested)
        {
            stdio.WriteResultLine(new JsonObject
            {
                ["error"] = new JsonObject
                {
                    ["code"] = ExitCodes.NameOf(code),
                    ["message"] = message,
                },
            });
        }

        return code;
    }
}
