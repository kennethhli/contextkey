namespace ContextKey.Core.Models;

public sealed class AppConfig
{
    // = snippet, ; scrape, ;; menu
    public const char StaticPrefix = '=';
    public const char DynamicPrefix = ';';
    public const string OverlayPrefix = ";;";

    public string DatabasePath { get; init; } = "contextkey.db";
    public bool LaunchAtLogin { get; init; }
}
