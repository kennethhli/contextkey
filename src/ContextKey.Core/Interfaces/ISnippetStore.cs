using ContextKey.Core.Models;

namespace ContextKey.Core.Interfaces;

public interface ISnippetStore
{
    IReadOnlyList<Snippet> Load();
    void Save(IEnumerable<Snippet> snippets);
}
