using System.Text.Json;

namespace Dnui;

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

/// <summary>The tray isn't running: it hasn't started yet (inside <see cref="DnuiTrayOptions.Configure"/>) or it has ended.</summary>
public sealed class DnuiTrayNotRunningException : InvalidOperationException
{
    internal DnuiTrayNotRunningException(Exception? inner = null)
        : base("The tray is not running.", inner)
    {
    }

    internal DnuiTrayNotRunningException(string message)
        : base(message)
    {
    }
}
