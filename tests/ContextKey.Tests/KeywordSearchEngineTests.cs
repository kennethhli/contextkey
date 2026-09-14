using ContextKey.Core.Engines;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class KeywordSearchEngineTests
{
    private readonly KeywordSearchEngine _search = new();

    private static IReadOnlyList<Snippet> Snippets() =>
    [
        new Snippet { Trigger = "email", Expansion = "my.email@domain.com", Description = "work email" },
        new Snippet { Trigger = "br", Expansion = "Best regards,\nAlex" }
    ];

    [Fact]
    public void EmptyQuery_ListsSnippets()
    {
        var hits = _search.Search("", Snippets(), []);

        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.Label == "email" && h.Value == "my.email@domain.com");
    }

    [Fact]
    public void Query_RanksTriggerPrefixFirst()
    {
        var hits = _search.Search("em", Snippets(), []);

        Assert.Equal("email", hits[0].Label);
        Assert.True(hits[0].Score > 1.5);
    }

    [Fact]
    public void WindowText_ReturnsExcerpt()
    {
        var windows = new[]
        {
            new ScrapedWindow
            {
                ProcessName = "Slack",
                Title = "Ada",
                Text = "please ping ada@client.com about the refund"
            }
        };

        var hits = _search.Search("refund", Snippets(), windows);

        Assert.Contains(hits, h => h.Source == SearchSource.WindowContext && h.Value.Contains("refund"));
    }

    [Fact]
    public void EmptyQuery_IncludesWindowEmail()
    {
        var windows = new[]
        {
            new ScrapedWindow { ProcessName = "Mail", Title = "Inbox", Text = "From ada@client.com" }
        };

        var hits = _search.Search("", Snippets(), windows);

        Assert.Contains(hits, h => h.Value == "ada@client.com" && h.Source == SearchSource.WindowContext);
    }
}
