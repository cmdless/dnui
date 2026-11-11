using System.Text.Json;

namespace Dnui;

/// <summary>The runtime ended before the tray started. <see cref="Result"/> carries its exit code.</summary>
public sealed class DnuiTrayStartException : Exception
{
    internal DnuiTrayStartException(DnuiResult result, Exception? inner = null)
        : base($"The Dnui runtime exited with code {result.ExitCode} before the tray started.", inner)
    {
        Result = result;
    }

    /// <summary>The runtime's exit code (see contracts/cli.md) and stdout (always null for tray).</summary>
    public DnuiResult Result { get; }
}

/// <summary>The runtime rejected a request (contracts/tray-protocol.md error codes).</summary>
public sealed class DnuiTrayProtocolException : Exception
{
    internal DnuiTrayProtocolException(int code, string message, JsonElement? data, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Data = data;
    }

    /// <summary>The JSON-RPC error code, for example 1010 (InvalidItems).</summary>
    public int Code { get; }

    /// <summary>The error's <c>data</c>, if any.</summary>
    public new JsonElement? Data { get; }
}

/// <summary>The tray has ended; no further requests are possible.</summary>
public sealed class DnuiTrayNotRunningException : InvalidOperationException
{
    internal DnuiTrayNotRunningException(Exception? inner = null)
        : base("The tray is not running.", inner)
    {
    }
}
