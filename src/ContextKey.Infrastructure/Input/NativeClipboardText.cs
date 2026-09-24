using ContextKey.Core.Interfaces;
using ContextKey.Infrastructure.macOS;
using ContextKey.Infrastructure.Windows;

namespace ContextKey.Infrastructure.Input;

public sealed class NativeClipboardText : IClipboardText
{
    public string? TryGetText()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                return MacPasteboard.TryGetString();
            }

            if (OperatingSystem.IsWindows())
            {
                return WindowsClipboard.TryGetText();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"clipboard failed: {ex.Message}");
        }

        return null;
    }
}
