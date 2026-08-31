namespace ContextKey.Core.Models;

public readonly record struct Keystroke(
    char? Character,
    string KeyName,
    bool IsBackspace,
    bool IsEscape,
    bool IsEnter,
    bool Alt,
    bool Control,
    bool Shift,
    bool Meta);

public sealed class KeystrokeEventArgs : EventArgs
{
    public required Keystroke Keystroke { get; init; }

    // true = we already dealt with this key, don't let it hit the focused field
    public bool Handled { get; set; }
}
