using ContextKey.Core.Engines;
using ContextKey.Core.Interfaces;
using ContextKey.Infrastructure.Input;
using ContextKey.Infrastructure.Storage;

namespace ContextKey.UI;

internal sealed class ExpansionHost : IDisposable
{
    private readonly IKeyboardHookService _hook;
    private readonly ITextInjector _injector;

    public ExpansionHost(
        StaticExpansionEngine engine,
        ISnippetStore store,
        IKeyboardHookService hook,
        ITextInjector injector)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(injector);

        engine.Load(store.Load());

        var session = new StaticTriggerSession(engine);
        _hook = hook;
        _injector = injector;

        _hook.KeyReceived += (_, e) =>
        {
            var result = session.Handle(e.Keystroke);
            e.Handled = result.Handled;
            if (!result.ShouldExpand || result.Expansion is null)
            {
                return;
            }

            _injector.EraseAsync(result.EraseCount).GetAwaiter().GetResult();
            _injector.InjectAsync(result.Expansion).GetAwaiter().GetResult();
        };

        _hook.Start();
    }

    public static ExpansionHost Start() =>
        new(
            new StaticExpansionEngine(),
            new JsonSnippetStore(),
            new SharpHookKeyboardHookService(),
            new SharpHookTextInjector());

    public void Dispose()
    {
        _hook.Dispose();
        if (_injector is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
