using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ContextKey.Infrastructure.macOS;

[SupportedOSPlatform("macos")]
public static class MacFocus
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const nuint ActivateIgnoringOtherApps = 2;

    [DllImport(LibObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSendInt(IntPtr receiver, IntPtr selector, int arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern int MsgSendGetInt(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern byte MsgSendNuint(IntPtr receiver, IntPtr selector, nuint arg);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void MsgSendByte(IntPtr receiver, IntPtr selector, byte arg);

    public static int FrontmostPid()
    {
        try
        {
            var workspace = MsgSend(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            var app = MsgSend(workspace, sel_registerName("frontmostApplication"));
            if (app == IntPtr.Zero)
            {
                return 0;
            }

            return MsgSendGetInt(app, sel_registerName("processIdentifier"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"frontmost pid failed: {ex.Message}");
            return 0;
        }
    }

    public static string? FrontmostProcessName()
    {
        var pid = FrontmostPid();
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(pid).ProcessName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void ActivateThisApp()
    {
        try
        {
            var app = MsgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            MsgSendByte(app, sel_registerName("activateIgnoringOtherApps:"), 1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"activate app failed: {ex.Message}");
        }
    }

    public static void ActivatePid(int pid)
    {
        if (pid <= 0 || pid == Environment.ProcessId)
        {
            return;
        }

        try
        {
            var cls = objc_getClass("NSRunningApplication");
            var app = MsgSendInt(cls, sel_registerName("runningApplicationWithProcessIdentifier:"), pid);
            if (app == IntPtr.Zero)
            {
                return;
            }

            MsgSendNuint(app, sel_registerName("activateWithOptions:"), ActivateIgnoringOtherApps);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"restore focus failed: {ex.Message}");
        }
    }
}
