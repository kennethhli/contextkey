using ContextKey.Core.Interfaces;

namespace ContextKey.Core.Engines;

public sealed class NullTextEmbedder : ITextEmbedder
{
    public static NullTextEmbedder Instance { get; } = new();

    public int Dimensions => 0;
    public bool IsAvailable => false;

    public float[] Embed(string text) => [];
}
