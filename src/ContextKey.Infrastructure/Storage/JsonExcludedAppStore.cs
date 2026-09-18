using System.Text.Json;
using ContextKey.Core;

namespace ContextKey.Infrastructure.Storage;

public sealed class JsonExcludedAppStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public JsonExcludedAppStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public string Path => _path;

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_path))
        {
            var seeded = AppExclusionList.Defaults.ToArray();
            Save(seeded);
            return seeded;
        }

        var json = File.ReadAllText(_path);
        var names = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        return AppExclusionList.Normalize(names ?? []);
    }

    public void Save(IEnumerable<string> names)
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(AppExclusionList.Normalize(names), JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, "ContextKey", "excluded-apps.json");
    }
}
