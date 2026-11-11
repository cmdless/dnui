using Tmds.DBus.Protocol;

namespace Dnui.Runtime.Platform;

/// <summary>
/// Checks for a StatusNotifierItem tray host on Linux (research R9): the session bus must have an
/// owner for org.kde.StatusNotifierWatcher.
/// </summary>
internal static class LinuxTrayHostProbe
{
    public static bool IsTrayHostAvailable(TimeSpan timeout)
    {
        try
        {
            return CheckAsync().WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"dnui: tray host probe failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CheckAsync()
    {
        if (DBusAddress.Session is not { } address)
        {
            return false;
        }

        using var connection = new DBusConnection(address);
        await connection.ConnectAsync().ConfigureAwait(false);

        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                destination: "org.freedesktop.DBus",
                path: "/org/freedesktop/DBus",
                @interface: "org.freedesktop.DBus",
                member: "NameHasOwner",
                signature: "s");
            writer.WriteString("org.kde.StatusNotifierWatcher");
            message = writer.CreateMessage();
        }

        return await connection.CallMethodAsync(
            message,
            static (Message reply, object? _) => reply.GetBodyReader().ReadBool(),
            null).ConfigureAwait(false);
    }
}
