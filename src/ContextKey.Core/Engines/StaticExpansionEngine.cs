using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class StaticExpansionEngine
{
    public const char Prefix = AppConfig.StaticPrefix;
    public const string DateTrigger = "date";
    public const string ClipTrigger = "clip";

    // concurrent because the hook thread and UI can both touch this
    private readonly ConcurrentDictionary<string, Snippet> _snippets;
    private readonly IClipboardText? _clipboard;
    private readonly Func<DateTime> _clock;

    public StaticExpansionEngine()
        : this(StringComparer.OrdinalIgnoreCase)
    {
    }

    public StaticExpansionEngine(
        IEqualityComparer<string> triggerComparer,
        IClipboardText? clipboard = null,
        Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(triggerComparer);
        _snippets = new ConcurrentDictionary<string, Snippet>(triggerComparer);
        _clipboard = clipboard;
        _clock = clock ?? (() => DateTime.Now);
    }

    public int Count => _snippets.Count;

    public void Load(IEnumerable<Snippet> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);

        _snippets.Clear();
        foreach (var snippet in snippets)
        {
            AddOrUpdate(snippet);
        }
    }

    public void AddOrUpdate(Snippet snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);

        var trigger = NormalizeTrigger(snippet.Trigger);
        _snippets[trigger] = new Snippet
        {
            Trigger = trigger,
            Expansion = snippet.Expansion,
            Description = snippet.Description
        };
    }

    public bool Remove(string trigger) =>
        _snippets.TryRemove(NormalizeTrigger(trigger), out _);

    public void Clear() => _snippets.Clear();

    public bool TryExpand(string trigger, [NotNullWhen(true)] out string? expansion)
    {
        if (TryBuiltIn(trigger, out expansion))
        {
            return true;
        }

        if (TryGetSnippet(trigger, out var snippet))
        {
            expansion = snippet.Expansion;
            return true;
        }

        expansion = null;
        return false;
    }

    public bool TryGetSnippet(string trigger, [NotNullWhen(true)] out Snippet? snippet)
    {
        snippet = null;
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return false;
        }

        if (!TryNormalizeTrigger(trigger, out var key))
        {
            return false;
        }

        return _snippets.TryGetValue(key, out snippet);
    }

    public IReadOnlyList<Snippet> GetAll()
    {
        var map = new Dictionary<string, Snippet>(StringComparer.OrdinalIgnoreCase);
        foreach (var snippet in _snippets.Values)
        {
            map[snippet.Trigger] = snippet;
        }

        map[DateTrigger] = new Snippet
        {
            Trigger = DateTrigger,
            Expansion = FormatToday(),
            Description = "today"
        };

        var clip = _clipboard?.TryGetText();
        if (!string.IsNullOrEmpty(clip))
        {
            map[ClipTrigger] = new Snippet
            {
                Trigger = ClipTrigger,
                Expansion = clip,
                Description = "clipboard"
            };
        }

        return map.Values.OrderBy(s => s.Trigger, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsBuiltIn(string? trigger) =>
        TryNormalizeTrigger(trigger, out var key) &&
        (key.Equals(DateTrigger, StringComparison.OrdinalIgnoreCase) ||
         key.Equals(ClipTrigger, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeTrigger(string trigger)
    {
        if (!TryNormalizeTrigger(trigger, out var normalized))
        {
            throw new ArgumentException("Empty trigger.", nameof(trigger));
        }

        return normalized;
    }

    public static bool TryNormalizeTrigger(string? trigger, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return false;
        }

        var trimmed = trigger.Trim();
        // "=email" and "email" should hit the same entry
        if (trimmed[0] == Prefix)
        {
            trimmed = trimmed[1..].Trim();
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    private bool TryBuiltIn(string trigger, [NotNullWhen(true)] out string? expansion)
    {
        expansion = null;
        if (!TryNormalizeTrigger(trigger, out var key))
        {
            return false;
        }

        if (key.Equals(DateTrigger, StringComparison.OrdinalIgnoreCase))
        {
            expansion = FormatToday();
            return true;
        }

        if (key.Equals(ClipTrigger, StringComparison.OrdinalIgnoreCase))
        {
            var text = _clipboard?.TryGetText();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            expansion = text;
            return true;
        }

        return false;
    }

    private string FormatToday() =>
        _clock().ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
}
