using ContextKey.Core.Models;

namespace ContextKey.Core.Interfaces;

public interface ISearchEngine
{
    IReadOnlyList<SearchResult> Search(
        string? query,
        IEnumerable<Snippet> snippets,
        IReadOnlyList<ScrapedWindow> windows,
        int limit = 8);
}
