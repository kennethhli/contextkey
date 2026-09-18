using ContextKey.Core.Engines;
using ContextKey.Core.Models;

namespace ContextKey.Tests;

public sealed class StaticTriggerSessionTests
{
    private static StaticTriggerSession CreateSession()
    {
        var engine = new StaticExpansionEngine();
        engine.Load(
        [
            new Snippet { Trigger = "email", Expansion = "my.email@domain.com" },
            new Snippet { Trigger = "br", Expansion = "Best regards,\nAlex" }
        ]);
        return new StaticTriggerSession(engine);
    }

    [Fact]
    public void EqualsEmailThenSpace_Expands()
    {
        var session = CreateSession();
        Type(session, "=email");

        var result = session.Handle(Space());

        Assert.True(result.ShouldExpand);
        Assert.True(result.Handled);
        Assert.Equal("my.email@domain.com", result.Expansion);
        Assert.Equal(6, result.EraseCount);
    }

    [Fact]
    public void EqualsBrThenEnter_Expands()
    {
        var session = CreateSession();
        Type(session, "=br");

        var result = session.Handle(Enter());

        Assert.True(result.ShouldExpand);
        Assert.Equal("Best regards,\nAlex", result.Expansion);
        Assert.Equal(3, result.EraseCount);
    }

    [Fact]
    public void UnknownTrigger_DoesNotExpand()
    {
        var session = CreateSession();
        Type(session, "=nope");

        var result = session.Handle(Space());

        Assert.False(result.ShouldExpand);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Backspace_RemovesLastChar()
    {
        var session = CreateSession();
        Type(session, "=emx");
        session.Handle(Backspace());
        Type(session, "ail");

        var result = session.Handle(Space());

        Assert.Equal("my.email@domain.com", result.Expansion);
    }

    [Fact]
    public void Escape_ClearsBuffer()
    {
        var session = CreateSession();
        Type(session, "=email");
        session.Handle(Escape());

        var result = session.Handle(Space());

        Assert.False(result.ShouldExpand);
        Assert.Equal(string.Empty, session.Buffer);
    }

    [Fact]
    public void Clear_DropsPartialTrigger()
    {
        var session = CreateSession();
        Type(session, "=em");
        session.Clear();

        Assert.Equal(string.Empty, session.Buffer);
        Type(session, "=email");
        var result = session.Handle(Space());
        Assert.Equal("my.email@domain.com", result.Expansion);
    }

    [Fact]
    public void TypingWithoutPrefix_DoesNothing()
    {
        var session = CreateSession();
        Type(session, "email");

        var result = session.Handle(Space());

        Assert.False(result.ShouldExpand);
    }

    [Fact]
    public void NewEquals_RestartsTrigger()
    {
        var session = CreateSession();
        Type(session, "=em");
        Type(session, "=email");

        var result = session.Handle(Tab());

        Assert.Equal("my.email@domain.com", result.Expansion);
    }

    [Fact]
    public void SemicolonEmailThenSpace_RequestsScrape()
    {
        var session = CreateSession();
        Type(session, ";email");

        var result = session.Handle(Space());

        Assert.True(result.ShouldScrape);
        Assert.True(result.Handled);
        Assert.Equal("email", result.DynamicKey);
        Assert.Equal(6, result.EraseCount);
        Assert.False(result.ShouldExpand);
    }

    [Fact]
    public void SemicolonDate_IsKnown()
    {
        var session = CreateSession();
        Type(session, ";date");

        var result = session.Handle(Enter());

        Assert.Equal("date", result.DynamicKey);
        Assert.True(result.ShouldScrape);
    }

    [Fact]
    public void DoubleSemicolon_OpensOverlay()
    {
        var session = CreateSession();
        Type(session, ";;");

        var result = session.Handle(Space());

        Assert.False(result.ShouldScrape);
        Assert.True(result.ShouldOpenOverlay);
        Assert.True(result.Handled);
        Assert.Equal(string.Empty, result.OverlayQuery);
        Assert.Equal(2, result.EraseCount);
    }

    [Fact]
    public void OverlayPrefixPlusQuery_OpensOverlay()
    {
        var session = CreateSession();
        Type(session, ";;email");

        var result = session.Handle(Space());

        Assert.False(result.ShouldScrape);
        Assert.True(result.ShouldOpenOverlay);
        Assert.Equal("email", result.OverlayQuery);
        Assert.Equal(7, result.EraseCount);
    }

    [Fact]
    public void SemicolonUnknown_DoesNotScrape()
    {
        var session = CreateSession();
        Type(session, ";phone");

        var result = session.Handle(Space());

        Assert.False(result.ShouldScrape);
    }

    private static void Type(StaticTriggerSession session, string text)
    {
        foreach (var ch in text)
        {
            session.Handle(CharKey(ch));
        }
    }

    private static Keystroke CharKey(char ch) =>
        new(ch, ch.ToString(), false, false, false, false, false, false, false, false);

    private static Keystroke Space() =>
        new(' ', "Space", false, false, false, false, false, false, false, false);

    private static Keystroke Enter() =>
        new(null, "Enter", false, false, true, false, false, false, false, false);

    private static Keystroke Tab() =>
        new(null, "Tab", false, false, false, true, false, false, false, false);

    private static Keystroke Backspace() =>
        new(null, "Backspace", true, false, false, false, false, false, false, false);

    private static Keystroke Escape() =>
        new(null, "Escape", false, true, false, false, false, false, false, false);
}
