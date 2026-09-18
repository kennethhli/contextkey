using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ContextKey.Infrastructure.macOS;

[SupportedOSPlatform("macos")]
internal static class AxNative
{
    public const string ApplicationServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    public const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public const string CoreGraphics =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public const string HiServices =
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/HIServices.framework/HIServices";

    public const uint Utf8 = 0x08000100;
    public const int AxSuccess = 0;
    public const int CfNumberIntType = 9;
    public const uint WindowOnScreenOnly = 1;
    public const uint WindowExcludeDesktop = 16;
    public const uint AxValueCgRectType = 3;
    public const uint AxValueCfRangeType = 4;

    [DllImport(ApplicationServices)]
    public static extern IntPtr AXUIElementCreateApplication(int pid);

    [DllImport(ApplicationServices)]
    public static extern IntPtr AXUIElementCreateSystemWide();

    [DllImport(ApplicationServices)]
    public static extern int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [DllImport(ApplicationServices)]
    public static extern int AXUIElementCopyParameterizedAttributeValue(
        IntPtr element,
        IntPtr parameterizedAttribute,
        IntPtr parameter,
        out IntPtr value);

    [DllImport(ApplicationServices)]
    public static extern byte AXValueGetValue(IntPtr value, uint type, IntPtr buffer);

    [DllImport(ApplicationServices)]
    public static extern byte AXIsProcessTrusted();

    [DllImport(ApplicationServices)]
    public static extern byte AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDictionaryCreateMutable(
        IntPtr allocator,
        nint capacity,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    [DllImport(CoreFoundation)]
    public static extern void CFDictionarySetValue(IntPtr theDict, IntPtr key, IntPtr value);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    public static extern uint CGMainDisplayID();

    [DllImport(CoreGraphics)]
    public static extern CgRect CGDisplayBounds(uint display);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string value, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation)]
    public static extern nint CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundation)]
    public static extern byte CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport(CoreFoundation)]
    public static extern byte CFNumberGetValue(IntPtr number, int theType, out int value);

    [DllImport(CoreFoundation)]
    public static extern ulong CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern ulong CFStringGetTypeID();

    public static readonly IntPtr AxWindows = CfString("AXWindows");
    public static readonly IntPtr AxChildren = CfString("AXChildren");
    public static readonly IntPtr AxValue = CfString("AXValue");
    public static readonly IntPtr AxTitle = CfString("AXTitle");
    public static readonly IntPtr AxDescription = CfString("AXDescription");
    public static readonly IntPtr AxFocusedUiElement = CfString("AXFocusedUIElement");
    public static readonly IntPtr AxSelectedTextRange = CfString("AXSelectedTextRange");
    public static readonly IntPtr AxBoundsForRange = CfString("AXBoundsForRange");
    public static readonly IntPtr CgOwnerPid = CfString("kCGWindowOwnerPID");
    public static readonly IntPtr CgOwnerName = CfString("kCGWindowOwnerName");
    public static readonly IntPtr CgWindowName = CfString("kCGWindowName");

    public static IntPtr CfString(string value) =>
        CFStringCreateWithCString(IntPtr.Zero, value, Utf8);

    public static string? ToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
        {
            return null;
        }

        if (CFGetTypeID(cfString) != CFStringGetTypeID())
        {
            return null;
        }

        var length = (int)CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new byte[(length * 4) + 8];
        if (CFStringGetCString(cfString, buffer, buffer.Length, Utf8) == 0)
        {
            return null;
        }

        var n = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, n < 0 ? buffer.Length : n);
    }

    public static void Release(IntPtr cf)
    {
        if (cf != IntPtr.Zero)
        {
            CFRelease(cf);
        }
    }

    public static bool EnsureAccessibility(bool prompt)
    {
        try
        {
            if (AXIsProcessTrusted() != 0)
            {
                return true;
            }

            if (!prompt)
            {
                return false;
            }

            var key = ReadExportedPointer(ApplicationServices, "kAXTrustedCheckOptionPrompt");
            if (key == IntPtr.Zero)
            {
                key = ReadExportedPointer(HiServices, "kAXTrustedCheckOptionPrompt");
            }

            var truth = ReadExportedPointer(CoreFoundation, "kCFBooleanTrue");
            if (key == IntPtr.Zero || truth == IntPtr.Zero)
            {
                return false;
            }

            var dict = CFDictionaryCreateMutable(IntPtr.Zero, 1, IntPtr.Zero, IntPtr.Zero);
            if (dict == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                CFDictionarySetValue(dict, key, truth);
                return AXIsProcessTrustedWithOptions(dict) != 0;
            }
            finally
            {
                Release(dict);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"accessibility check failed: {ex.Message}");
            return AXIsProcessTrusted() != 0;
        }
    }

    private static IntPtr ReadExportedPointer(string library, string symbol)
    {
        if (!NativeLibrary.TryLoad(library, out var handle) ||
            !NativeLibrary.TryGetExport(handle, symbol, out var address))
        {
            return IntPtr.Zero;
        }

        return Marshal.ReadIntPtr(address);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct CgRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CfRange
{
    public nint Location;
    public nint Length;
}
