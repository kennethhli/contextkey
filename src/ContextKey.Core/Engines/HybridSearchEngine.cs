using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class HybridSearchEngine : ISearchEngine
{
    public const double SemanticFloor = 0.28;

    private readonly KeywordSearchEngine _keyword;
    private readonly SemanticSearchEngine _semantic;

    public HybridSearchEngine(KeywordSearchEngine keyword, SemanticSearchEngine semantic)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        ArgumentNullException.ThrowIfNull(semantic);
        _keyword = keyword;
        _semantic = semantic;
    }

    public IReadOnlyList<SearchResult> Search(
        string? query,
        IEnumerable<Snippet> snippets,
        IReadOnlyList<ScrapedWindow> windows,
        int limit = KeywordSearchEngine.DefaultLimit)
    {
        var snippetList = snippets as IReadOnlyList<Snippet> ?? snippets.ToArray();
        var keywordHits = _keyword.Search(query, snippetList, windows, limit: Math.Max(limit, 24));

        var needle = query?.Trim() ?? string.Empty;
        if (needle.Length == 0)
        {
            return keywordHits.Take(limit).ToArray();
        }

        var semanticHits = _semantic.Search(needle, snippetList, windows, limit: Math.Max(limit, 24));
        if (semanticHits.Count == 0)
        {
            return keywordHits.Take(limit).ToArray();
        }

        var merged = new Dictionary<string, SearchResult>(StringComparer.OrdinalIgnoreCase);
        var keywordNorm = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var hit in keywordHits)
        {
            merged[hit.Value] = hit;
            keywordNorm[hit.Value] = Math.Clamp(hit.Score / 3.0, 0, 1);
        }

        foreach (var hit in semanticHits)
        {
            var cosine = Math.Clamp(hit.Score, 0, 1);
            if (merged.TryGetValue(hit.Value, out var existing))
            {
                var kw = keywordNorm.GetValueOrDefault(hit.Value);
                merged[hit.Value] = Clone(existing, score: 0.5 * kw + 0.5 * cosine);
                continue;
            }

            if (cosine < SemanticFloor)
            {
                continue;
            }

            merged[hit.Value] = Clone(hit, score: 0.4 + 0.6 * cosine);
        }

        return merged.Values
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static SearchResult Clone(SearchResult hit, double score) =>
        new()
        {
            Label = hit.Label,
            Value = hit.Value,
            Score = score,
            Source = hit.Source
        };
}
