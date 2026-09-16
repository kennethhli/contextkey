using System.Text.Json;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Infrastructure.Storage;

public sealed class JsonSnippetStore : ISnippetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;

    public JsonSnippetStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public string Path => _path;

    public IReadOnlyList<Snippet> Load()
    {
        if (!File.Exists(_path))
        {
            var seeded = Seed();
            Save(seeded);
            return seeded;
        }

        var json = File.ReadAllText(_path);
        var snippets = JsonSerializer.Deserialize<List<Snippet>>(json, JsonOptions);
        return snippets ?? [];
    }

    public void Save(IEnumerable<Snippet> snippets)
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(snippets.ToList(), JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, "ContextKey", "snippets.json");
    }

    private static List<Snippet> Seed() =>
    [
        new Snippet { Trigger = "email", Expansion = "my.email@domain.com", Description = "work email" },
        new Snippet { Trigger = "br", Expansion = "Best regards,\nAlex", Description = "sign-off" },
        new Snippet
        {
            Trigger = "ret",
            Expansion = "We can reverse the charge and restore the original payment.",
            Description = "returns policy"
        }
    ];
}
