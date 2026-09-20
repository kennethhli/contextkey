using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Infrastructure.Windows;

public sealed class WindowsWindowScraper : IWindowScraper
{
    public Task<IReadOnlyList<ScrapedWindow>> ScrapeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScrapedWindow>>([]);

    public bool TryGetCaretScreenPosition(out int x, out int y)
    {
        x = 0;
        y = 0;
        return false;
    }

    public bool IsSecureFocus() => false;

    public void ClearCache()
    {
    }
}
