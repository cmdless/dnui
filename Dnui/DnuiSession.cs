namespace Dnui;

/// <summary>
/// stdin and stdout claimed for a session protocol by <see cref="DnuiStdio.OpenSession"/>.
/// End the session with <c>Resolve(null, exitCode)</c>.
/// </summary>
public sealed class DnuiSession
{
    internal DnuiSession(Stream input, Stream output)
    {
        Input = input;
        Output = output;
    }

    /// <summary>The raw stdin stream.</summary>
    public Stream Input { get; }

    /// <summary>The raw stdout stream. Disposing it flushes but does not close the process handle.</summary>
    public Stream Output { get; }
}
