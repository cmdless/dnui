namespace Dnui;

/// <summary>
/// The outcome of running a Dnui runtime command: its exit code and the single resolved line it
/// wrote to stdout, or <see langword="null"/> when nothing was resolved (always for sessions such as tray).
/// </summary>
/// <param name="ExitCode">The runtime process exit code. Non-zero is a result, not an exception.</param>
/// <param name="StdOut">The resolved JSON line, or <see langword="null"/> when nothing was written.</param>
public sealed record DnuiResult(int ExitCode, string? StdOut);
