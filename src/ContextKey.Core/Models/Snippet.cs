namespace ContextKey.Core.Models;

public sealed class Snippet
{
    public required string Trigger { get; init; }
    public required string Expansion { get; init; }
    public string? Description { get; init; }
}
