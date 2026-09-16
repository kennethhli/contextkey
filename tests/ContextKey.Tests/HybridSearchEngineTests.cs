using ContextKey.Core.Engines;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class HybridSearchEngineTests
{
    [Fact]
    public void RefundQuery_HitsReturnsSnippetWithoutKeyword()
    {
        var snippets = new[]
        {
            new Snippet { Trigger = "email", Expansion = "my.email@domain.com" },
            new Snippet
            {
                Trigger = "ret",
                Expansion = "We can reverse the charge and restore the original payment.",
                Description = "returns policy"
            }
        };

        var search = new HybridSearchEngine(
            new KeywordSearchEngine(),
            new SemanticSearchEngine(new FakeMiniLm()));

        var hits = search.Search("refund", snippets, []);

        Assert.Contains(hits, h => h.Value.Contains("reverse the charge"));
        Assert.DoesNotContain(hits, h => h.Label == "email");
    }

    [Fact]
    public void EmptyQuery_StaysKeywordOnly()
    {
        var snippets = new[]
        {
            new Snippet { Trigger = "email", Expansion = "my.email@domain.com" }
        };

        var search = new HybridSearchEngine(
            new KeywordSearchEngine(),
            new SemanticSearchEngine(new FakeMiniLm()));

        var hits = search.Search("", snippets, []);

        Assert.Single(hits);
        Assert.Equal("email", hits[0].Label);
        Assert.Equal(1.0, hits[0].Score);
    }

    private sealed class FakeMiniLm : ITextEmbedder
    {
        public int Dimensions => 4;
        public bool IsAvailable => true;

        public float[] Embed(string text)
        {
            var t = text.ToLowerInvariant();
            if (t.Contains("refund") || t.Contains("reverse") || t.Contains("returns"))
            {
                return [1, 0, 0, 0];
            }

            if (t.Contains("email"))
            {
                return [0, 1, 0, 0];
            }

            return [0, 0, 1, 0];
        }
    }
}
