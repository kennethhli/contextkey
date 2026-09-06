using System.Text;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public readonly record struct StaticTriggerResult(bool Handled, string? Expansion, int EraseCount)
{
    public static StaticTriggerResult Ignored { get; } = new(false, null, 0);

    public bool ShouldExpand => Expansion is not null;
}

// watches the key stream for "=trigger" then space/enter/tab
public sealed class StaticTriggerSession
{
    public const int MaxTriggerLength = 32;

    private readonly StaticExpansionEngine _engine;
    private readonly StringBuilder _buffer = new();

    public StaticTriggerSession(StaticExpansionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    public string Buffer => _buffer.ToString();

    public StaticTriggerResult Handle(Keystroke key)
    {
        if (key.Control || key.Alt || key.Meta)
        {
            _buffer.Clear();
            return StaticTriggerResult.Ignored;
        }

        if (key.IsBackspace)
        {
            if (_buffer.Length > 0)
            {
                _buffer.Remove(_buffer.Length - 1, 1);
            }

            return StaticTriggerResult.Ignored;
        }

        if (key.IsEscape)
        {
            _buffer.Clear();
            return StaticTriggerResult.Ignored;
        }

        if (IsDelimiter(key))
        {
            return TryExpand();
        }

        if (key.Character is not { } ch)
        {
            return StaticTriggerResult.Ignored;
        }

        if (ch == StaticExpansionEngine.Prefix)
        {
            _buffer.Clear();
            _buffer.Append(ch);
            return StaticTriggerResult.Ignored;
        }

        if (_buffer.Length > 0 &&
            _buffer[0] == StaticExpansionEngine.Prefix &&
            IsTriggerChar(ch) &&
            _buffer.Length < MaxTriggerLength)
        {
            _buffer.Append(ch);
            return StaticTriggerResult.Ignored;
        }

        _buffer.Clear();
        return StaticTriggerResult.Ignored;
    }

    private StaticTriggerResult TryExpand()
    {
        var typed = _buffer.ToString();
        _buffer.Clear();

        if (_engine.TryExpand(typed, out var expansion))
        {
            return new StaticTriggerResult(true, expansion, typed.Length);
        }

        return StaticTriggerResult.Ignored;
    }

    private static bool IsDelimiter(Keystroke key) =>
        key.IsEnter || key.IsTab || key.Character == ' ';

    private static bool IsTriggerChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-';
}
