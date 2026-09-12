using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using ContextKey.Core.Models;

namespace ContextKey.Core.Engines;

public sealed class RegexContextEngine
{
    public const string EmailKey = "email";
    public const string DateKey = "date";

    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        EmailKey,
        DateKey
    };

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IsoDateRegex = new(
        @"\b(20\d{2}|19\d{2})-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b",
        RegexOptions.Compiled);

    private static readonly Regex SlashDateRegex = new(
        @"\b(0?[1-9]|1[0-2])[/.-](0?[1-9]|[12]\d|3[01])[/.-]((?:20|19)\d{2})\b",
        RegexOptions.Compiled);

    private static readonly string[] SkipEmailLocalParts =
    [
        "noreply", "no-reply", "no_reply", "donotreply", "notifications", "mailer-daemon"
    ];

    public bool IsKnownKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && Keys.Contains(key.Trim());

    public bool TryExtract(string key, IReadOnlyList<ScrapedWindow> windows, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (!IsKnownKey(key) || windows.Count == 0)
        {
            return false;
        }

        var blob = Flatten(windows);
        if (blob.Length == 0)
        {
            return false;
        }

        value = key.Equals(DateKey, StringComparison.OrdinalIgnoreCase)
            ? FirstDate(blob)
            : FirstEmail(blob);

        return value is not null;
    }

    private static string Flatten(IReadOnlyList<ScrapedWindow> windows)
    {
        var parts = new List<string>(windows.Count * 2);
        foreach (var window in windows)
        {
            if (!string.IsNullOrWhiteSpace(window.Title))
            {
                parts.Add(window.Title);
            }

            if (!string.IsNullOrWhiteSpace(window.Text))
            {
                parts.Add(window.Text);
            }
        }

        return string.Join('\n', parts);
    }

    private static string? FirstEmail(string text)
    {
        foreach (Match match in EmailRegex.Matches(text))
        {
            var email = match.Value;
            var local = email.AsSpan(0, email.IndexOf('@'));
            var skip = false;
            foreach (var prefix in SkipEmailLocalParts)
            {
                if (local.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
            {
                return email;
            }
        }

        return null;
    }

    private static string? FirstDate(string text)
    {
        var iso = IsoDateRegex.Match(text);
        if (iso.Success)
        {
            return iso.Value;
        }

        var slash = SlashDateRegex.Match(text);
        if (!slash.Success)
        {
            return null;
        }

        if (!int.TryParse(slash.Groups[1].Value, out var month) ||
            !int.TryParse(slash.Groups[2].Value, out var day) ||
            !int.TryParse(slash.Groups[3].Value, out var year))
        {
            return null;
        }

        try
        {
            return new DateTime(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
