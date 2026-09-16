using ContextKey.Core.Engines;
using ContextKey.Infrastructure.Ai;

namespace ContextKey.Tests;

public sealed class OnnxMiniLmEmbedderTests
{
    [Fact]
    public void Refund_IsCloserToReturnsThanEmail_WhenModelPresent()
    {
        var path = Path.Combine(OnnxMiniLmEmbedder.ModelDirectory(), OnnxMiniLmEmbedder.ModelFileName);
        if (!File.Exists(path))
        {
            return;
        }

        using var embedder = OnnxMiniLmEmbedder.TryCreate();
        Assert.NotNull(embedder);

        var refund = embedder!.Embed("refund");
        var returns = embedder.Embed("returns policy reverse the charge restore payment");
        var email = embedder.Embed("work email my.email@domain.com");

        var toReturns = SemanticSearchEngine.Cosine(refund, returns);
        var toEmail = SemanticSearchEngine.Cosine(refund, email);
        Assert.True(toReturns > toEmail, $"refund~returns {toReturns:F3} vs refund~email {toEmail:F3}");
    }
}
