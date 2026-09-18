namespace ContextKey.Core;

public sealed class AppExclusionList
{
    public static readonly string[] Defaults =
    [
        "1Password",
        "Bitwarden",
        "LastPass",
        "Keychain Access"
    ];

    public static readonly string[] SystemApps =
    [
        "WindowServer", "Dock", "Control Center", "Notification Center",
        "SystemUIServer", "Spotlight", "ContextKey"
    ];

    private string[] _user = Defaults.ToArray();

    public IReadOnlyList<string> User => _user;

    public void ReplaceUser(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _user = Normalize(names);
    }

    public bool ShouldSkipScrape(string? processName) =>
        Matches(processName, SystemApps) || Matches(processName, _user);

    public bool ShouldPauseExpansion(string? processName) =>
        Matches(processName, _user);

    public static bool Matches(string? processName, IEnumerable<string> rules)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                continue;
            }

            if (processName.Contains(rule.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string[] Normalize(IEnumerable<string> names) =>
        names
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
