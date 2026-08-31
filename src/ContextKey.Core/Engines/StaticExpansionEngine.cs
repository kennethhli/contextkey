using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class StaticExpansionEngine
{
    public const char Prefix = AppConfig.StaticPrefix;

    // concurrent because the hook thread and UI can both touch this
    private readonly ConcurrentDictionary<string, Snippet> _snippets;

    public StaticExpansionEngine()
        : this(StringComparer.OrdinalIgnoreCase)
    {
    }

    public StaticExpansionEngine(IEqualityComparer<string> triggerComparer)
    {
        ArgumentNullException.ThrowIfNull(triggerComparer);
        _snippets = new ConcurrentDictionary<string, Snippet>(triggerComparer);
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

    public IReadOnlyList<Snippet> GetAll() =>
        _snippets.Values.OrderBy(s => s.Trigger, StringComparer.OrdinalIgnoreCase).ToArray();

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
}
