using System.IO.Pipelines;
using Dnui;

namespace Dnui.Tests.TestSupport;

/// <summary>Builds DnuiStdio instances over in-memory streams.</summary>
internal static class TestStdio
{
    public static (DnuiStdio Stdio, MemoryStream Output) Create(bool redirected = true)
    {
        var output = new MemoryStream();
        return (new DnuiStdio(new MemoryStream(), output, redirected), output);
    }

    public static string ReadOutput(MemoryStream output) => System.Text.Encoding.UTF8.GetString(output.ToArray());
}

/// <summary>Sets DnuiStdio.Current while a test constructs a DnuiApp subclass.</summary>
internal sealed class AmbientStdioScope : IDisposable
{
    public AmbientStdioScope(DnuiStdio stdio) => DnuiStdio.Current = stdio;

    public void Dispose() => DnuiStdio.Current = null;
}
