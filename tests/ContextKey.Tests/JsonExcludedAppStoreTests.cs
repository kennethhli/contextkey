using ContextKey.Infrastructure.Storage;

namespace ContextKey.Tests;

public sealed class JsonExcludedAppStoreTests
{
    [Fact]
    public void Load_MissingFile_WritesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ck-ex-{Guid.NewGuid():N}.json");

        try
        {
            var store = new JsonExcludedAppStore(path);
            var names = store.Load();

            Assert.True(File.Exists(path));
            Assert.Contains(names, n => n.Equals("1Password", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(names, n => n.Equals("Bitwarden", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAndDedupes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ck-ex-{Guid.NewGuid():N}.json");

        try
        {
            var store = new JsonExcludedAppStore(path);
            store.Save(["  Vault ", "vault", "KeePassXC"]);

            var loaded = store.Load();

            Assert.Equal(2, loaded.Count);
            Assert.Equal(["KeePassXC", "Vault"], loaded.ToArray());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
