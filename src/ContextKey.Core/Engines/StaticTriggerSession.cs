using System.Text;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public readonly record struct StaticTriggerResult(
    bool Handled,
    string? Expansion,
    int EraseCount,
    string? DynamicKey = null,
    bool OpenOverlay = false,
    string OverlayQuery = "")
{
    public static StaticTriggerResult Ignored { get; } = new(false, null, 0);

    public bool ShouldExpand => Expansion is not null;
    public bool ShouldScrape => DynamicKey is not null;
    public bool ShouldOpenOverlay => OpenOverlay;
}

// =snippet or ;email / ;date, then space/enter/tab
public sealed class StaticTriggerSession
{
    public const int MaxTriggerLength = 32;

    private readonly StaticExpansionEngine _engine;
    private readonly RegexContextEngine _regex;
    private readonly StringBuilder _buffer = new();

    public StaticTriggerSession(StaticExpansionEngine engine, RegexContextEngine? regex = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _regex = regex ?? new RegexContextEngine();
    }

    public string Buffer => _buffer.ToString();

    public void Clear() => _buffer.Clear();

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

        if (ch is StaticExpansionEngine.Prefix or AppConfig.DynamicPrefix)
        {
            // second ';' becomes ';;' (overlay), not a new scrape
            if (ch == AppConfig.DynamicPrefix && _buffer.Length == 1 && _buffer[0] == AppConfig.DynamicPrefix)
            {
                _buffer.Append(ch);
                return StaticTriggerResult.Ignored;
            }

            _buffer.Clear();
            _buffer.Append(ch);
            return StaticTriggerResult.Ignored;
        }

        if (_buffer.Length > 0 &&
            IsPrefix(_buffer[0]) &&
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

        if (typed.StartsWith(AppConfig.OverlayPrefix, StringComparison.Ordinal))
        {
            var query = typed.Length > AppConfig.OverlayPrefix.Length
                ? typed[AppConfig.OverlayPrefix.Length..]
                : string.Empty;
            return new StaticTriggerResult(true, null, typed.Length, OpenOverlay: true, OverlayQuery: query);
        }

        if (typed.Length > 1 && typed[0] == AppConfig.DynamicPrefix)
        {
            var key = typed[1..];
            if (_regex.IsKnownKey(key))
            {
                return new StaticTriggerResult(true, null, typed.Length, key);
            }

            return StaticTriggerResult.Ignored;
        }

        if (_engine.TryExpand(typed, out var expansion))
        {
            return new StaticTriggerResult(true, expansion, typed.Length);
        }

        return StaticTriggerResult.Ignored;
    }

    private static bool IsPrefix(char ch) =>
        ch is StaticExpansionEngine.Prefix or AppConfig.DynamicPrefix;

    private static bool IsDelimiter(Keystroke key) =>
        key.IsEnter || key.IsTab || key.Character == ' ';

    private static bool IsTriggerChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-';
}
