using ContextKey.Core.Engines;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class StaticExpansionEngineTests
{
    private static StaticExpansionEngine CreateSeededEngine()
    {
        var engine = new StaticExpansionEngine();
        engine.Load(
        [
            new Snippet { Trigger = "email", Expansion = "my.email@domain.com", Description = "Work email" },
            new Snippet { Trigger = "br", Expansion = "Best regards,\nAlex" }
        ]);
        return engine;
    }

    [Fact]
    public void TryExpand_KnownTrigger_ReturnsExpansion()
    {
        var engine = CreateSeededEngine();

        var found = engine.TryExpand("email", out var expansion);

        Assert.True(found);
        Assert.Equal("my.email@domain.com", expansion);
    }

    [Fact]
    public void TryExpand_PrefixedTrigger_StripsEqualsAndLooksUp()
    {
        var engine = CreateSeededEngine();

        var found = engine.TryExpand("=br", out var expansion);

        Assert.True(found);
        Assert.Equal("Best regards,\nAlex", expansion);
    }

    [Fact]
    public void TryExpand_IsCaseInsensitive()
    {
        var engine = CreateSeededEngine();

        Assert.True(engine.TryExpand("EMAIL", out var expansion));
        Assert.Equal("my.email@domain.com", expansion);
    }

    [Fact]
    public void TryExpand_UnknownTrigger_ReturnsFalse()
    {
        var engine = CreateSeededEngine();

        var found = engine.TryExpand("missing", out var expansion);

        Assert.False(found);
        Assert.Null(expansion);
    }

    [Fact]
    public void TryExpand_BarePrefix_ReturnsFalse()
    {
        var engine = CreateSeededEngine();

        Assert.False(engine.TryExpand("=", out _));
        Assert.False(engine.TryExpand("   ", out _));
    }

    [Fact]
    public void AddOrUpdate_ReplacesExistingExpansion()
    {
        var engine = CreateSeededEngine();

        engine.AddOrUpdate(new Snippet { Trigger = "=email", Expansion = "new@domain.com" });

        Assert.True(engine.TryExpand("email", out var expansion));
        Assert.Equal("new@domain.com", expansion);
        Assert.Equal(2, engine.Count);
    }

    [Fact]
    public void Remove_DropsSnippet()
    {
        var engine = CreateSeededEngine();

        Assert.True(engine.Remove("=email"));
        Assert.False(engine.TryExpand("email", out _));
        Assert.Equal(1, engine.Count);
    }

    [Fact]
    public void Load_ReplacesCatalog()
    {
        var engine = CreateSeededEngine();

        engine.Load([new Snippet { Trigger = "sig", Expansion = "Alex" }]);

        Assert.Equal(1, engine.Count);
        Assert.False(engine.TryExpand("email", out _));
        Assert.True(engine.TryExpand("sig", out var expansion));
        Assert.Equal("Alex", expansion);
    }

    [Fact]
    public void GetAll_ReturnsNormalizedTriggersSorted()
    {
        var engine = CreateSeededEngine();
        engine.AddOrUpdate(new Snippet { Trigger = "=zzz", Expansion = "z" });

        var triggers = engine.GetAll().Select(s => s.Trigger).ToArray();

        Assert.Equal(["br", "email", "zzz"], triggers);
    }

    [Fact]
    public void NormalizeTrigger_RejectsEmptyRemainder()
    {
        Assert.Throws<ArgumentException>(() => StaticExpansionEngine.NormalizeTrigger("="));
        Assert.Throws<ArgumentException>(() => StaticExpansionEngine.NormalizeTrigger("   "));
    }

    [Fact]
    public void TryExpand_LookupIsSubMillisecondForTypicalCatalog()
    {
        var engine = new StaticExpansionEngine();
        engine.Load(Enumerable.Range(0, 1_000).Select(i => new Snippet
        {
            Trigger = $"key{i}",
            Expansion = $"value{i}"
        }));

        engine.TryExpand("key500", out _); // warmup

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10_000; i++)
        {
            engine.TryExpand("key500", out _);
        }
        sw.Stop();

        var averageMs = sw.Elapsed.TotalMilliseconds / 10_000;
        Assert.True(averageMs < 1.0, $"Average lookup was {averageMs:F4} ms; expected < 1 ms.");
    }
}
