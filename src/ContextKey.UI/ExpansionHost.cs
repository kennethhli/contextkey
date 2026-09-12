using ContextKey.Core.Engines;
using ContextKey.Core.Interfaces;
using ContextKey.Infrastructure.Input;
using ContextKey.Infrastructure.macOS;
using ContextKey.Infrastructure.Storage;
using ContextKey.Infrastructure.Windows;

namespace ContextKey.UI;

internal sealed class ExpansionHost : IDisposable
{
    private readonly IKeyboardHookService _hook;
    private readonly ITextInjector _injector;
    private readonly IWindowScraper _scraper;
    private readonly RegexContextEngine _regex;
    private readonly CancellationTokenSource _lifetime = new();

    public ExpansionHost(
        StaticExpansionEngine engine,
        RegexContextEngine regex,
        ISnippetStore store,
        IKeyboardHookService hook,
        ITextInjector injector,
        IWindowScraper scraper)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(injector);
        ArgumentNullException.ThrowIfNull(scraper);

        engine.Load(store.Load());

        var session = new StaticTriggerSession(engine, regex);
        _hook = hook;
        _injector = injector;
        _scraper = scraper;
        _regex = regex;

        _hook.KeyReceived += (_, e) =>
        {
            var result = session.Handle(e.Keystroke);
            e.Handled = result.Handled;

            if (session.Buffer == ";")
            {
                // start walking AX while they finish typing ;email
                _ = _scraper.ScrapeAsync(_lifetime.Token);
            }

            if (result.ShouldExpand && result.Expansion is not null)
            {
                Replace(result.EraseCount, result.Expansion);
                return;
            }

            if (result.ShouldScrape && result.DynamicKey is not null)
            {
                var key = result.DynamicKey;
                var erase = result.EraseCount;
                Replace(erase, string.Empty);
                _ = ExpandDynamicAsync(key);
            }
        };

        _hook.Start();
    }

    public static ExpansionHost Start() =>
        new(
            new StaticExpansionEngine(),
            new RegexContextEngine(),
            new JsonSnippetStore(),
            new SharpHookKeyboardHookService(),
            new SharpHookTextInjector(),
            CreateScraper());

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _hook.Dispose();
        if (_injector is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void Replace(int eraseCount, string text)
    {
        _injector.EraseAsync(eraseCount).GetAwaiter().GetResult();
        if (text.Length > 0)
        {
            _injector.InjectAsync(text).GetAwaiter().GetResult();
        }
    }

    private async Task ExpandDynamicAsync(string key)
    {
        try
        {
            var windows = await _scraper.ScrapeAsync(_lifetime.Token).ConfigureAwait(false);
            if (_regex.TryExtract(key, windows, out var value))
            {
                await _injector.InjectAsync(value).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"scrape failed: {ex.Message}");
        }
    }

    private static IWindowScraper CreateScraper() =>
        OperatingSystem.IsMacOS() ? new MacWindowScraper() : new WindowsWindowScraper();
}
