namespace Dnui;

/// <summary>Why <see cref="DnuiStdio.OpenSession"/> failed.</summary>
public enum DnuiStdioFailure
{
    /// <summary>stdin and stdout are not both redirected, so no session protocol is possible.</summary>
    NotRedirected,

    /// <summary>A session was already opened; stdio can be claimed only once per process.</summary>
    AlreadyClaimed,
}

/// <summary>Thrown when stdio cannot be claimed as a session.</summary>
public sealed class DnuiStdioException : InvalidOperationException
{
    internal DnuiStdioException(DnuiStdioFailure reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Why the claim failed.</summary>
    public DnuiStdioFailure Reason { get; }
}
