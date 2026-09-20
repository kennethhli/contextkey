using ContextKey.Core.Models;

namespace ContextKey.Core.Interfaces;

public interface IWindowScraper
{
    Task<IReadOnlyList<ScrapedWindow>> ScrapeAsync(CancellationToken cancellationToken = default);
    bool TryGetCaretScreenPosition(out int x, out int y);
    bool IsSecureFocus();
    void ClearCache();
}
