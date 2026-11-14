using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dnui.Tray;

/// <summary>Shared wire setup: newline-delimited JSON-RPC 2.0 with camelCase System.Text.Json.</summary>
internal static class TrayProtocol
{
    public static JsonRpc CreateConnection(Stream output, Stream input)
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        formatter.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        formatter.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        formatter.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        var handler = new NewLineDelimitedMessageHandler(output, input, formatter)
        {
            NewLine = NewLineDelimitedMessageHandler.NewLineStyle.Lf,
        };

        return new TrayJsonRpc(handler);
    }

    /// <summary>Reads error <c>data</c> as raw JSON instead of StreamJsonRpc's CommonErrorData shape.</summary>
    private sealed class TrayJsonRpc(IJsonRpcMessageHandler handler) : JsonRpc(handler)
    {
        protected override Type? GetErrorDetailsDataType(JsonRpcError error) => typeof(JsonElement);
    }
}
