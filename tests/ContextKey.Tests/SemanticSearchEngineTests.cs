using ContextKey.Core.Engines;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class SemanticSearchEngineTests
{
    [Fact]
    public void Prefetch_WarmsCache_SearchOnlyEmbedsQuery()
    {
        var embedder = new CountingEmbedder();
        var semantic = new SemanticSearchEngine(embedder);
        var snippets = new[]
        {
            new Snippet { Trigger = "ret", Expansion = "reverse the charge", Description = "returns" }
        };
        var windows = new[]
        {
            new ScrapedWindow { ProcessName = "Notes", Title = "inbox", Text = "please refund this order" }
        };

        semantic.Prefetch(snippets, windows);
        var afterPrefetch = embedder.Calls;

        semantic.Search("refund", snippets, windows);

        Assert.Equal(2, afterPrefetch);
        Assert.Equal(afterPrefetch + 1, embedder.Calls);
    }

    [Fact]
    public void Prefetch_UnavailableEmbedder_DoesNothing()
    {
        var semantic = new SemanticSearchEngine(NullTextEmbedder.Instance);
        semantic.Prefetch(
            [new Snippet { Trigger = "a", Expansion = "alpha" }],
            []);
    }

    private sealed class CountingEmbedder : ITextEmbedder
    {
        public int Calls { get; private set; }
        public int Dimensions => 2;
        public bool IsAvailable => true;

        public float[] Embed(string text)
        {
            Calls++;
            return text.Contains("refund", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("reverse", StringComparison.OrdinalIgnoreCase)
                ? [1, 0]
                : [0, 1];
        }
    }
}
