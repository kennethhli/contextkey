using ContextKey.Core.Models;
using ContextKey.Infrastructure.Storage;

namespace ContextKey.Tests;

public sealed class JsonSnippetStoreTests
{
    [Fact]
    public void Load_MissingFile_WritesSeedSnippets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ck-{Guid.NewGuid():N}.json");

        try
        {
            var store = new JsonSnippetStore(path);
            var snippets = store.Load();

            Assert.True(File.Exists(path));
            Assert.Contains(snippets, s => s.Trigger == "email");
            Assert.Contains(snippets, s => s.Trigger == "br");
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
    public void SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ck-{Guid.NewGuid():N}.json");

        try
        {
            var store = new JsonSnippetStore(path);
            store.Save([new Snippet { Trigger = "sig", Expansion = "Alex" }]);

            var loaded = store.Load();

            Assert.Single(loaded);
            Assert.Equal("sig", loaded[0].Trigger);
            Assert.Equal("Alex", loaded[0].Expansion);
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
