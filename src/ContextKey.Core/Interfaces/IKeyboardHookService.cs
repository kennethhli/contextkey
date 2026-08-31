using ContextKey.Core.Models;

namespace ContextKey.Core.Interfaces;

public interface IKeyboardHookService : IDisposable
{
    event EventHandler<KeystrokeEventArgs>? KeyReceived;
    bool IsRunning { get; }
    void Start();
    void Stop();
}
