using Avalonia;
using Avalonia.Threading;
using ContextKey.Core.Engines;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;
using ContextKey.Infrastructure.Ai;
using ContextKey.Infrastructure.Input;
using ContextKey.Infrastructure.macOS;
using ContextKey.Infrastructure.Storage;
using ContextKey.Infrastructure.Windows;
using ContextKey.UI.ViewModels;
using ContextKey.UI.Views;

namespace ContextKey.UI;

internal sealed class ExpansionHost : IDisposable
{
    private readonly StaticExpansionEngine _engine;
    private readonly IKeyboardHookService _hook;
    private readonly ITextInjector _injector;
    private readonly IWindowScraper _scraper;
    private readonly RegexContextEngine _regex;
    private readonly ISearchEngine _search;
    private readonly IDisposable? _embedder;
    private readonly CancellationTokenSource _lifetime = new();
    private FloatingOverlayView? _overlay;
    private bool _overlayOpen;

    public ExpansionHost(
        StaticExpansionEngine engine,
        RegexContextEngine regex,
        ISearchEngine search,
        ISnippetStore store,
        IKeyboardHookService hook,
        ITextInjector injector,
        IWindowScraper scraper,
        IDisposable? embedder = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(injector);
        ArgumentNullException.ThrowIfNull(scraper);

        engine.Load(store.Load());

        var session = new StaticTriggerSession(engine, regex);
        _engine = engine;
        _hook = hook;
        _injector = injector;
        _scraper = scraper;
        _regex = regex;
        _search = search;
        _embedder = embedder;

        _hook.KeyReceived += (_, e) =>
        {
            if (_overlayOpen)
            {
                return;
            }

            var result = session.Handle(e.Keystroke);
            e.Handled = result.Handled;

            if (session.Buffer.StartsWith(';'))
            {
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
                Replace(result.EraseCount, string.Empty);
                _ = ExpandDynamicAsync(key);
                return;
            }

            if (result.ShouldOpenOverlay)
            {
                var hasCaret = _scraper.TryGetCaretScreenPosition(out var x, out var y);
                Replace(result.EraseCount, string.Empty);
                _overlayOpen = true;
                _ = ShowOverlayAsync(result.OverlayQuery, x, y, hasCaret);
            }
        };

        _hook.Start();
    }

    public static ExpansionHost Start()
    {
        var regex = new RegexContextEngine();
        var onnx = OnnxMiniLmEmbedder.TryCreate();
        ITextEmbedder embedder = onnx is null ? NullTextEmbedder.Instance : onnx;
        if (onnx is not null)
        {
            _ = Task.Run(onnx.Warmup);
        }

        var search = new HybridSearchEngine(
            new KeywordSearchEngine(regex),
            new SemanticSearchEngine(embedder));

        return new ExpansionHost(
            new StaticExpansionEngine(),
            regex,
            search,
            new JsonSnippetStore(),
            new SharpHookKeyboardHookService(),
            new SharpHookTextInjector(),
            CreateScraper(),
            onnx);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _hook.Dispose();
        if (_injector is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _embedder?.Dispose();

        Dispatcher.UIThread.Post(() => _overlay?.Close());
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

    private async Task ShowOverlayAsync(string query, int x, int y, bool hasCaret)
    {
        try
        {
            var windows = await _scraper.ScrapeAsync(_lifetime.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => ShowOverlay(query, windows, x, y, hasCaret));
        }
        catch (OperationCanceledException)
        {
            _overlayOpen = false;
        }
        catch (Exception ex)
        {
            _overlayOpen = false;
            Console.Error.WriteLine($"overlay failed: {ex.Message}");
        }
    }

    private void ShowOverlay(
        string query,
        IReadOnlyList<ScrapedWindow> windows,
        int x,
        int y,
        bool hasCaret)
    {
        var vm = new FloatingOverlayViewModel(_search, _engine.GetAll(), windows, query);
        var window = new FloatingOverlayView { DataContext = vm };
        _overlay = window;

        if (hasCaret)
        {
            window.Position = new PixelPoint(x, Math.Max(0, y + 2));
        }

        window.Closed += async (_, _) =>
        {
            _overlayOpen = false;
            _overlay = null;
            var value = window.ChosenValue;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                await Task.Delay(40, _lifetime.Token);
                await _injector.InjectAsync(value, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        };

        window.Show();
        if (!hasCaret)
        {
            var area = window.Screens.Primary?.WorkingArea;
            if (area is { } bounds)
            {
                window.Position = new PixelPoint(bounds.X + 72, bounds.Y + 72);
            }
        }

        window.Activate();
    }

    private static IWindowScraper CreateScraper() =>
        OperatingSystem.IsMacOS() ? new MacWindowScraper() : new WindowsWindowScraper();
}
