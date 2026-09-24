using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ContextKey.Infrastructure.macOS;

[SupportedOSPlatform("macos")]
internal static class MacPasteboard
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(LibObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSendPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    public static string? TryGetString()
    {
        try
        {
            var board = MsgSend(objc_getClass("NSPasteboard"), sel_registerName("generalPasteboard"));
            if (board == IntPtr.Zero)
            {
                return null;
            }

            return ReadType(board, "public.utf8-plain-text")
                ?? ReadType(board, "NSStringPboardType");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"clipboard read failed: {ex.Message}");
            return null;
        }
    }

    private static string? ReadType(IntPtr board, string typeName)
    {
        var type = AxNative.CfString(typeName);
        try
        {
            var ns = MsgSendPtr(board, sel_registerName("stringForType:"), type);
            return AxNative.ToString(ns);
        }
        finally
        {
            AxNative.Release(type);
        }
    }
}
