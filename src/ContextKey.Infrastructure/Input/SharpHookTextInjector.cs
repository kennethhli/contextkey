using ContextKey.Core.Interfaces;
using SharpHook.Data;
using SharpHook.Simulation;

namespace ContextKey.Infrastructure.Input;

public sealed class SharpHookTextInjector : ITextInjector, IDisposable
{
    private readonly EventSimulator _simulator = EventSimulator.Create("ContextKey");

    public Task InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _simulator.SimulateTextEntry(text);
        }

        return Task.CompletedTask;
    }

    public Task EraseAsync(int characterCount, CancellationToken cancellationToken = default)
    {
        if (characterCount <= 0)
        {
            return Task.CompletedTask;
        }

        var sequence = _simulator.Sequence();
        for (var i = 0; i < characterCount; i++)
        {
            sequence.AddKeyPress(KeyCode.VcBackspace);
            sequence.AddKeyRelease(KeyCode.VcBackspace);
        }

        sequence.Simulate();
        return Task.CompletedTask;
    }

    public void Dispose() => _simulator.Dispose();
}
