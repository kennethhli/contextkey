using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class KeywordSearchEngine : ISearchEngine
{
    public const int DefaultLimit = 8;

    private readonly RegexContextEngine _regex;

    public KeywordSearchEngine(RegexContextEngine? regex = null)
    {
        _regex = regex ?? new RegexContextEngine();
    }

    public IReadOnlyList<SearchResult> Search(
        string? query,
        IEnumerable<Snippet> snippets,
        IReadOnlyList<ScrapedWindow> windows,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        ArgumentNullException.ThrowIfNull(windows);

        var needle = query?.Trim() ?? string.Empty;
        var hits = new List<SearchResult>();

        foreach (var snippet in snippets)
        {
            if (TryScoreSnippet(snippet, needle, out var result))
            {
                hits.Add(result);
            }
        }

        foreach (var window in windows)
        {
            hits.AddRange(WindowHits(window, needle));
        }

        return hits
            .GroupBy(h => h.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(h => h.Score).First())
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static bool TryScoreSnippet(Snippet snippet, string needle, out SearchResult result)
    {
        result = null!;
        var trigger = snippet.Trigger;
        var expansion = snippet.Expansion;
        var description = snippet.Description ?? string.Empty;

        double score;
        if (needle.Length == 0)
        {
            score = 1.0;
        }
        else if (trigger.Equals(needle, StringComparison.OrdinalIgnoreCase))
        {
            score = 3.0;
        }
        else if (trigger.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
        {
            score = 2.0;
        }
        else if (trigger.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                 description.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            score = 1.5;
        }
        else if (expansion.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            score = 1.0;
        }
        else
        {
            return false;
        }

        result = new SearchResult
        {
            Label = trigger,
            Value = expansion,
            Score = score,
            Source = SearchSource.StaticSnippet
        };
        return true;
    }

    private IEnumerable<SearchResult> WindowHits(ScrapedWindow window, string needle)
    {
        var label = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title;

        if (needle.Length == 0)
        {
            if (_regex.TryExtract(RegexContextEngine.EmailKey, [window], out var email))
            {
                yield return new SearchResult
                {
                    Label = $"{label} · email",
                    Value = email,
                    Score = 0.9,
                    Source = SearchSource.WindowContext
                };
            }

            if (_regex.TryExtract(RegexContextEngine.DateKey, [window], out var date))
            {
                yield return new SearchResult
                {
                    Label = $"{label} · date",
                    Value = date,
                    Score = 0.8,
                    Source = SearchSource.WindowContext
                };
            }

            yield break;
        }

        if (label.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            var value = Excerpt(window.Text, needle) ?? Excerpt(label, needle) ?? label;
            yield return new SearchResult
            {
                Label = label,
                Value = value,
                Score = 1.2,
                Source = SearchSource.WindowContext
            };
            yield break;
        }

        var excerpt = Excerpt(window.Text, needle);
        if (excerpt is not null)
        {
            yield return new SearchResult
            {
                Label = label,
                Value = excerpt,
                Score = 0.7,
                Source = SearchSource.WindowContext
            };
        }
    }

    private static string? Excerpt(string? text, string needle)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var index = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = Math.Max(0, index - 24);
        var length = Math.Min(96, text.Length - start);
        var slice = text.AsSpan(start, length).Trim();
        return slice.ToString();
    }
}
