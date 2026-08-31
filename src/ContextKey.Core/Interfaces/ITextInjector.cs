namespace ContextKey.Core.Interfaces;

public interface ITextInjector
{
    Task InjectAsync(string text, CancellationToken cancellationToken = default);
    // backspace over the trigger before we paste the expansion
    Task EraseAsync(int characterCount, CancellationToken cancellationToken = default);
}
