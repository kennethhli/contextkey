namespace ContextKey.Core.Models;

public sealed class SearchResult
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required double Score { get; init; }
    public required SearchSource Source { get; init; }
}

public enum SearchSource
{
    StaticSnippet,
    WindowContext
}
