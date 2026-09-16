namespace ContextKey.Core.Interfaces;

public interface ITextEmbedder
{
    int Dimensions { get; }
    bool IsAvailable { get; }
    float[] Embed(string text);
}
