using System.Collections.Concurrent;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class SemanticSearchEngine
{
    public const int DefaultLimit = 8;
    public const int MaxEmbedChars = 480;

    private readonly ITextEmbedder _embedder;
    private readonly ConcurrentDictionary<string, float[]> _cache = new(StringComparer.Ordinal);

    public SemanticSearchEngine(ITextEmbedder embedder)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        _embedder = embedder;
    }

    public IReadOnlyList<SearchResult> Search(
        string? query,
        IEnumerable<Snippet> snippets,
        IReadOnlyList<ScrapedWindow> windows,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        ArgumentNullException.ThrowIfNull(windows);

        if (!_embedder.IsAvailable)
        {
            return [];
        }

        var needle = query?.Trim() ?? string.Empty;
        if (needle.Length == 0)
        {
            return [];
        }

        var queryVec = EmbedCached(needle);
        var hits = new List<SearchResult>();

        foreach (var snippet in snippets)
        {
            var text = SnippetText(snippet);
            if (text.Length == 0)
            {
                continue;
            }

            var score = Cosine(queryVec, EmbedCached(text));
            hits.Add(new SearchResult
            {
                Label = snippet.Trigger,
                Value = snippet.Expansion,
                Score = score,
                Source = SearchSource.StaticSnippet
            });
        }

        foreach (var window in windows)
        {
            var text = WindowText(window);
            if (text.Length == 0)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title;
            var score = Cosine(queryVec, EmbedCached(text));
            hits.Add(new SearchResult
            {
                Label = label,
                Value = text.Length > 96 ? text[..96].Trim() : text,
                Score = score,
                Source = SearchSource.WindowContext
            });
        }

        return hits
            .OrderByDescending(h => h.Score)
            .Take(limit)
            .ToArray();
    }

    private float[] EmbedCached(string text) =>
        _cache.GetOrAdd(text, static (t, embedder) => embedder.Embed(t), _embedder);

    private static string SnippetText(Snippet snippet)
    {
        var desc = snippet.Description is { Length: > 0 } d ? d : string.Empty;
        return Trim($"{snippet.Trigger}. {desc} {snippet.Expansion}");
    }

    private static string WindowText(ScrapedWindow window)
    {
        var blob = string.IsNullOrWhiteSpace(window.Text) ? window.Title : window.Text;
        return Trim(blob);
    }

    private static string Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= MaxEmbedChars ? trimmed : trimmed[..MaxEmbedChars];
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0;
        }

        double dot = 0;
        double na = 0;
        double nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        return denom < 1e-8 ? 0 : dot / denom;
    }
}
