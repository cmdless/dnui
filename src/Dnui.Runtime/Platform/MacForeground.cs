using System.Runtime.InteropServices;

namespace Dnui.Runtime.Platform;

/// <summary>macOS: activate this app so windows and dialogs appear in front (spec 002 research R9).</summary>
internal static class MacForeground
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    public static void Activate()
    {
        try
        {
            var nsApp = IntPtr_objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (nsApp != IntPtr.Zero)
            {
                void_objc_msgSend_bool(nsApp, sel_registerName("activateIgnoringOtherApps:"), true);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Console.Error.WriteLine($"dnui: could not activate the app: {ex.Message}");
        }
    }

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);
}
