using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ContextKey.Core;
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
    private readonly ISnippetStore _store;
    private readonly JsonExcludedAppStore _exclusionStore;
    private readonly AppExclusionList _exclusions;
    private readonly IDisposable? _embedder;
    private readonly CancellationTokenSource _lifetime = new();
    private FloatingOverlayView? _overlay;
    private SettingsWindow? _settings;
    private bool _overlayOpen;
    private bool _settingsOpen;

    public ExpansionHost(
        StaticExpansionEngine engine,
        RegexContextEngine regex,
        ISearchEngine search,
        ISnippetStore store,
        IKeyboardHookService hook,
        ITextInjector injector,
        IWindowScraper scraper,
        JsonExcludedAppStore exclusionStore,
        AppExclusionList exclusions,
        IDisposable? embedder = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(injector);
        ArgumentNullException.ThrowIfNull(scraper);
        ArgumentNullException.ThrowIfNull(exclusionStore);
        ArgumentNullException.ThrowIfNull(exclusions);

        engine.Load(store.Load());
        exclusions.ReplaceUser(exclusionStore.Load());

        var session = new StaticTriggerSession(engine, regex);
        _engine = engine;
        _hook = hook;
        _injector = injector;
        _scraper = scraper;
        _regex = regex;
        _search = search;
        _store = store;
        _exclusionStore = exclusionStore;
        _exclusions = exclusions;
        _embedder = embedder;

        _hook.KeyReceived += (_, e) =>
        {
            if (_overlayOpen || _settingsOpen)
            {
                return;
            }

            if (_exclusions.ShouldPauseExpansion(FrontmostAppName()) || _scraper.IsSecureFocus())
            {
                session.Clear();
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
                _ = ExpandDynamicAsync(key, $";{key}");
                return;
            }

            if (result.ShouldOpenOverlay)
            {
                var hasCaret = _scraper.TryGetCaretScreenPosition(out var x, out var y);
                _overlayOpen = true;
                _ = ShowOverlayAsync(result.OverlayQuery, result.EraseCount, x, y, hasCaret);
            }
        };

        _hook.Start();
    }

    public static ExpansionHost Start()
    {
        if (OperatingSystem.IsMacOS())
        {
            PromptMacAccessibility();
        }

        var regex = new RegexContextEngine();
        var onnx = OnnxMiniLmEmbedder.TryCreate();
        ITextEmbedder embedder = onnx is null ? NullTextEmbedder.Instance : onnx;
        if (onnx is not null)
        {
            _ = Task.Run(onnx.Warmup);
        }

        var exclusions = new AppExclusionList();
        var exclusionStore = new JsonExcludedAppStore();
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
            CreateScraper(exclusions),
            exclusionStore,
            exclusions,
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

        Dispatcher.UIThread.Post(() =>
        {
            _overlay?.Close();
            _settings?.Close();
        });
    }

    public void ShowSettings()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_settings is { IsVisible: true })
            {
                _settings.Activate();
                return;
            }

            _settingsOpen = true;
            _overlay?.Close();
            var vm = new SettingsViewModel(
                _engine.GetAll(),
                PersistSnippets,
                _exclusions.User,
                PersistExcludedApps);
            var window = new SettingsWindow { DataContext = vm };
            _settings = window;
            window.Closed += (_, _) =>
            {
                _settingsOpen = false;
                _settings = null;
            };

            if (OperatingSystem.IsMacOS())
            {
                MacFocus.ActivateThisApp();
            }

            window.Show();
            window.Activate();
        });
    }

    private void PersistSnippets(IReadOnlyList<Snippet> snippets)
    {
        _store.Save(snippets);
        _engine.Load(snippets);
    }

    private void PersistExcludedApps(IReadOnlyList<string> names)
    {
        _exclusionStore.Save(names);
        _exclusions.ReplaceUser(names);
        _scraper.ClearCache();
    }

    private static string? FrontmostAppName() =>
        OperatingSystem.IsMacOS() ? MacFocus.FrontmostProcessName() : null;

    private void Replace(int eraseCount, string text)
    {
        _injector.EraseAsync(eraseCount).GetAwaiter().GetResult();
        if (text.Length > 0)
        {
            _injector.InjectAsync(text).GetAwaiter().GetResult();
        }
    }

    private async Task ExpandDynamicAsync(string key, string restore)
    {
        try
        {
            var windows = await ScrapeWithTimeout().ConfigureAwait(false);
            if (!_regex.TryExtract(key, windows, out var value))
            {
                Console.WriteLine($";{key}: nothing found in other windows");
                await _injector.InjectAsync(restore, _lifetime.Token).ConfigureAwait(false);
                return;
            }

            await _injector.InjectAsync(value, _lifetime.Token).ConfigureAwait(false);
            Console.WriteLine($";{key} → {value}");
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

    private async Task ShowOverlayAsync(string query, int eraseCount, int x, int y, bool hasCaret)
    {
        var shown = false;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => ShowOverlay(query, eraseCount, [], x, y, hasCaret));
            shown = true;

            var windows = await ScrapeWithTimeout().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_overlay?.DataContext is FloatingOverlayViewModel vm)
                {
                    vm.ReplaceWindows(windows);
                }
            });
        }
        catch (OperationCanceledException)
        {
            if (!shown)
            {
                _overlayOpen = false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"overlay failed: {ex.Message}");
            if (!shown)
            {
                _overlayOpen = false;
            }
        }
    }

    private async Task<IReadOnlyList<ScrapedWindow>> ScrapeWithTimeout()
    {
        var scrape = _scraper.ScrapeAsync(_lifetime.Token);
        var winner = await Task.WhenAny(scrape, Task.Delay(600, _lifetime.Token)).ConfigureAwait(false);
        if (winner != scrape)
        {
            Console.WriteLine("scrape: timed out");
            return [];
        }

        return await scrape.ConfigureAwait(false);
    }

    private void ShowOverlay(
        string query,
        int eraseCount,
        IReadOnlyList<ScrapedWindow> windows,
        int x,
        int y,
        bool hasCaret)
    {
        var vm = new FloatingOverlayViewModel(_search, _engine.GetAll(), windows, query);
        var window = new FloatingOverlayView { DataContext = vm };
        _overlay = window;
        window.EraseCount = eraseCount;

        if (OperatingSystem.IsMacOS())
        {
            window.RestorePid = MacFocus.FrontmostPid();
            MacFocus.ActivateThisApp();
        }

        window.Closed += async (_, _) =>
        {
            _overlayOpen = false;
            _overlay = null;
            var value = window.ChosenValue;
            var restorePid = window.RestorePid;
            var eraseCount = window.EraseCount;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    MacFocus.ActivatePid(restorePid);
                }

                await Task.Delay(180, _lifetime.Token);
                await _injector.EraseAsync(eraseCount, _lifetime.Token);
                await _injector.InjectAsync(value, _lifetime.Token);
                Console.WriteLine($"overlay insert: {value}");
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"overlay insert failed: {ex.Message}");
            }
        };

        window.Show();
        window.Position = ClampToScreen(window, hasCaret
            ? new PixelPoint(x, y + 2)
            : FallbackPosition(window));
        window.Activate();
        Console.WriteLine($"overlay: {window.Position.X},{window.Position.Y}");
    }

    private static PixelPoint FallbackPosition(Window window)
    {
        var area = window.Screens.Primary?.WorkingArea;
        return area is { } bounds
            ? new PixelPoint(bounds.X + 72, bounds.Y + 72)
            : new PixelPoint(72, 72);
    }

    private static PixelPoint ClampToScreen(Window window, PixelPoint desired)
    {
        var screen = window.Screens.ScreenFromPoint(desired) ?? window.Screens.Primary;
        if (screen is null)
        {
            return desired;
        }

        var area = screen.WorkingArea;
        var maxX = Math.Max(area.X + 8, area.X + area.Width - 400);
        var maxY = Math.Max(area.Y + 8, area.Y + area.Height - 180);
        return new PixelPoint(
            Math.Clamp(desired.X, area.X + 8, maxX),
            Math.Clamp(desired.Y, area.Y + 8, maxY));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static void PromptMacAccessibility()
    {
        if (MacWindowScraper.EnsureAccessibility())
        {
            Console.WriteLine("accessibility: ok");
            return;
        }

        Console.WriteLine(
            "accessibility: missing — System Settings → Privacy & Security → Accessibility");
        Console.WriteLine("enable Terminal (and dotnet if it shows up), then quit and rerun");
    }

    private static IWindowScraper CreateScraper(AppExclusionList exclusions) =>
        OperatingSystem.IsMacOS() ? new MacWindowScraper(exclusions) : new WindowsWindowScraper();
}
