namespace ContextKey.Core.Models;

public sealed class ScrapedWindow
{
    public required string ProcessName { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }
}
