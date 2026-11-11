namespace Dnui;

/// <summary>
/// A command failed. Host commands raise it for non-zero exits; window commands raise it with the
/// same <see cref="ErrorCode"/> in-process.
/// </summary>
public sealed class DnuiCommandException : Exception
{
    internal DnuiCommandException(DnuiResult result, string? errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        Result = result;
        ErrorCode = errorCode;
    }

    /// <summary>The exit code and resolved line (for window commands, the code that would have been the exit code).</summary>
    public DnuiResult Result { get; }

    /// <summary>The stable <c>error.code</c> name, for example <c>WindowTypeInvalid</c> or <c>DialogUnsupported</c>.</summary>
    public string? ErrorCode { get; }
}
