using System.Text.Json;
using StreamJsonRpc;

namespace Dnui.Tray;

/// <summary>Application error codes from contracts/tray-protocol.md.</summary>
internal static class TrayErrorCodes
{
    public const int NotInitialized = 1001;
    public const int AlreadyInitialized = 1002;
    public const int ProtocolVersionMismatch = 1003;
    public const int InvalidItems = 1010;
    public const int InvalidIcon = 1011;
    public const int InvalidSimulation = 1012;

    public static LocalRpcException Error(int code, string message, object? data = null) =>
        new(message) { ErrorCode = code, ErrorData = data is null ? null : JsonSerializer.SerializeToElement(data, s_options) };

    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);
}

/// <summary>Exit codes the tray command returns from its synchronous phase (contracts/cli.md).</summary>
internal static class TrayExitCodes
{
    public const int NotRedirected = 20;
    public const int AlreadyClaimed = 22;
}
