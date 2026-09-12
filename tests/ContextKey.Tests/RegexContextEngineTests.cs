using ContextKey.Core.Engines;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class RegexContextEngineTests
{
    private readonly RegexContextEngine _engine = new();

    [Fact]
    public void Email_TakesFirstRealAddress()
    {
        var windows = new[]
        {
            Win("Slack", "noreply@alerts.example.com hey, ping ada@client.com thanks")
        };

        Assert.True(_engine.TryExtract("email", windows, out var value));
        Assert.Equal("ada@client.com", value);
    }

    [Fact]
    public void Email_IsCaseInsensitiveKey()
    {
        var windows = new[] { Win("Mail", "Contact:  Alex.Roe@Firm.io") };

        Assert.True(_engine.TryExtract("EMAIL", windows, out var value));
        Assert.Equal("Alex.Roe@Firm.io", value);
    }

    [Fact]
    public void Date_PrefersIso()
    {
        var windows = new[] { Win("PDF", "Invoice 08/15/2026 due 2026-08-30") };

        Assert.True(_engine.TryExtract("date", windows, out var value));
        Assert.Equal("2026-08-30", value);
    }

    [Fact]
    public void Date_NormalizesSlash()
    {
        var windows = new[] { Win("Safari", "ship by 9/7/2026 please") };

        Assert.True(_engine.TryExtract("date", windows, out var value));
        Assert.Equal("2026-09-07", value);
    }

    [Fact]
    public void Missing_ReturnsFalse()
    {
        var windows = new[] { Win("Notes", "no contacts in here") };

        Assert.False(_engine.TryExtract("email", windows, out var email));
        Assert.Null(email);
        Assert.False(_engine.TryExtract("date", windows, out var date));
        Assert.Null(date);
    }

    [Fact]
    public void UnknownKey_ReturnsFalse()
    {
        Assert.False(_engine.IsKnownKey("phone"));
        Assert.False(_engine.TryExtract("phone", [Win("x", "a@b.com")], out _));
    }

    private static ScrapedWindow Win(string title, string text) =>
        new() { ProcessName = "test", Title = title, Text = text };
}
