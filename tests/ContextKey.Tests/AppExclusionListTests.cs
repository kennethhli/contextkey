using ContextKey.Core;

namespace ContextKey.Tests;

public sealed class AppExclusionListTests
{
    [Fact]
    public void SkipScrape_MatchesPasswordManagersAndSystemApps()
    {
        var list = new AppExclusionList();

        Assert.True(list.ShouldSkipScrape("1Password"));
        Assert.True(list.ShouldSkipScrape("1Password 8"));
        Assert.True(list.ShouldSkipScrape("Bitwarden"));
        Assert.True(list.ShouldSkipScrape("LastPass"));
        Assert.True(list.ShouldSkipScrape("Keychain Access"));
        Assert.True(list.ShouldSkipScrape("Dock"));
        Assert.False(list.ShouldSkipScrape("Notes"));
        Assert.False(list.ShouldSkipScrape("Safari"));
    }

    [Fact]
    public void PauseExpansion_OnlyUserList_NotDock()
    {
        var list = new AppExclusionList();

        Assert.True(list.ShouldPauseExpansion("1Password"));
        Assert.False(list.ShouldPauseExpansion("Dock"));
        Assert.False(list.ShouldPauseExpansion("Notes"));
    }

    [Fact]
    public void ReplaceUser_RemovesOldRules()
    {
        var list = new AppExclusionList();
        list.ReplaceUser(["Vault"]);

        Assert.False(list.ShouldPauseExpansion("1Password"));
        Assert.True(list.ShouldPauseExpansion("MyVault"));
        Assert.True(list.ShouldSkipScrape("Dock"));
    }
}
