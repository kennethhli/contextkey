using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using ContextKey.Core;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;

namespace ContextKey.Infrastructure.macOS;

[SupportedOSPlatform("macos")]
public sealed class MacWindowScraper : IWindowScraper
{
    private const int MaxWindows = 12;
    private const int MaxNodes = 400;
    private const int MaxCharsPerWindow = 24_000;
    private const int BudgetMs = 280;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMilliseconds(1500);

    private readonly AppExclusionList _exclusions;
    private readonly object _gate = new();
    private IReadOnlyList<ScrapedWindow> _cache = [];
    private long _cacheTicks;
    private long _secureTicks;
    private bool _secureCached;
    private Task<IReadOnlyList<ScrapedWindow>>? _inflight;

    public MacWindowScraper(AppExclusionList? exclusions = null)
    {
        _exclusions = exclusions ?? new AppExclusionList();
    }

    public static bool EnsureAccessibility(bool prompt = true) =>
        AxNative.EnsureAccessibility(prompt);

    public Task<IReadOnlyList<ScrapedWindow>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cache.Count > 0 && IsFresh())
            {
                return Task.FromResult(_cache);
            }

            if (_inflight is not null)
            {
                return _inflight;
            }

            _inflight = Task.Run(() =>
            {
                IReadOnlyList<ScrapedWindow> result = [];
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(BudgetMs + 120));
                    result = ScrapeCore(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    result = [];
                }
                finally
                {
                    lock (_gate)
                    {
                        _cache = result;
                        _cacheTicks = Stopwatch.GetTimestamp();
                        _inflight = null;
                    }
                }

                return result;
            }, cancellationToken);

            return _inflight;
        }
    }

    public void ClearCache()
    {
        lock (_gate)
        {
            _cache = [];
            _cacheTicks = 0;
        }
    }

    public bool IsSecureFocus()
    {
        if (SecureEventInputOn())
        {
            return true;
        }

        lock (_gate)
        {
            if (_secureTicks != 0 && Stopwatch.GetElapsedTime(_secureTicks) < TimeSpan.FromMilliseconds(160))
            {
                return _secureCached;
            }
        }

        var secure = ReadFocusedSecure();
        lock (_gate)
        {
            _secureCached = secure;
            _secureTicks = Stopwatch.GetTimestamp();
        }

        return secure;
    }

    public bool TryGetCaretScreenPosition(out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!OperatingSystem.IsMacOS() || AxNative.AXIsProcessTrusted() == 0)
        {
            return false;
        }

        var systemWide = AxNative.AXUIElementCreateSystemWide();
        if (systemWide == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (AxNative.AXUIElementCopyAttributeValue(systemWide, AxNative.AxFocusedUiElement, out var focused) != AxNative.AxSuccess
                || focused == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (AxNative.AXUIElementCopyAttributeValue(focused, AxNative.AxSelectedTextRange, out var rangeValue) != AxNative.AxSuccess
                    || rangeValue == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    if (AxNative.AXUIElementCopyParameterizedAttributeValue(
                            focused, AxNative.AxBoundsForRange, rangeValue, out var boundsValue) != AxNative.AxSuccess
                        || boundsValue == IntPtr.Zero)
                    {
                        return false;
                    }

                    try
                    {
                        if (!TryReadRect(boundsValue, out var rect))
                        {
                            return false;
                        }

                        // AX screen coords are top-left of the main display
                        x = (int)Math.Round(rect.X);
                        y = (int)Math.Round(rect.Y + rect.Height);
                        return true;
                    }
                    finally
                    {
                        AxNative.Release(boundsValue);
                    }
                }
                finally
                {
                    AxNative.Release(rangeValue);
                }
            }
            finally
            {
                AxNative.Release(focused);
            }
        }
        finally
        {
            AxNative.Release(systemWide);
        }
    }

    private static bool SecureEventInputOn()
    {
        try
        {
            return AxNative.IsSecureEventInputEnabled() != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool ReadFocusedSecure()
    {
        if (!OperatingSystem.IsMacOS() || AxNative.AXIsProcessTrusted() == 0)
        {
            return false;
        }

        var systemWide = AxNative.AXUIElementCreateSystemWide();
        if (systemWide == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (AxNative.AXUIElementCopyAttributeValue(systemWide, AxNative.AxFocusedUiElement, out var focused) != AxNative.AxSuccess
                || focused == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var role = CopyString(focused, AxNative.AxRole);
                var subrole = CopyString(focused, AxNative.AxSubrole);
                var description = CopyString(focused, AxNative.AxDescription);
                return SecureField.IsSecure(role, subrole, description);
            }
            finally
            {
                AxNative.Release(focused);
            }
        }
        finally
        {
            AxNative.Release(systemWide);
        }
    }

    private static bool TryReadRect(IntPtr axValue, out CgRect rect)
    {
        var size = Marshal.SizeOf<CgRect>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (AxNative.AXValueGetValue(axValue, AxNative.AxValueCgRectType, buffer) == 0)
            {
                rect = default;
                return false;
            }

            rect = Marshal.PtrToStructure<CgRect>(buffer);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private bool IsFresh() =>
        Stopwatch.GetElapsedTime(_cacheTicks) < CacheTtl;

    private IReadOnlyList<ScrapedWindow> ScrapeCore(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS() || AxNative.AXIsProcessTrusted() == 0)
        {
            Console.WriteLine("scrape skipped: accessibility not granted for this process");
            return [];
        }

        var clock = Stopwatch.StartNew();
        var owners = ListOnScreenApps();
        var results = new List<ScrapedWindow>();

        foreach (var (pid, processName) in owners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (clock.ElapsedMilliseconds >= BudgetMs || results.Count >= MaxWindows)
            {
                break;
            }

            if (pid == Environment.ProcessId || _exclusions.ShouldSkipScrape(processName))
            {
                continue;
            }

            CollectAppWindows(pid, processName, results, clock, cancellationToken);
        }

        return results;
    }

    private static List<(int Pid, string Name)> ListOnScreenApps()
    {
        var list = new List<(int, string)>();
        var seen = new HashSet<int>();
        var info = AxNative.CGWindowListCopyWindowInfo(
            AxNative.WindowOnScreenOnly | AxNative.WindowExcludeDesktop, 0);
        if (info == IntPtr.Zero)
        {
            return list;
        }

        try
        {
            var count = AxNative.CFArrayGetCount(info);
            for (nint i = 0; i < count; i++)
            {
                var dict = AxNative.CFArrayGetValueAtIndex(info, i);
                if (dict == IntPtr.Zero)
                {
                    continue;
                }

                var pidPtr = AxNative.CFDictionaryGetValue(dict, AxNative.CgOwnerPid);
                if (pidPtr == IntPtr.Zero || AxNative.CFNumberGetValue(pidPtr, AxNative.CfNumberIntType, out var pid) == 0)
                {
                    continue;
                }

                if (!seen.Add(pid))
                {
                    continue;
                }

                var namePtr = AxNative.CFDictionaryGetValue(dict, AxNative.CgOwnerName);
                var name = AxNative.ToString(namePtr) ?? $"pid-{pid}";
                list.Add((pid, name));
            }
        }
        finally
        {
            AxNative.Release(info);
        }

        return list;
    }

    private static void CollectAppWindows(
        int pid,
        string processName,
        List<ScrapedWindow> results,
        Stopwatch clock,
        CancellationToken cancellationToken)
    {
        var app = AxNative.AXUIElementCreateApplication(pid);
        if (app == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (AxNative.AXUIElementCopyAttributeValue(app, AxNative.AxWindows, out var windows) != AxNative.AxSuccess
                || windows == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var count = AxNative.CFArrayGetCount(windows);
                for (nint i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (clock.ElapsedMilliseconds >= BudgetMs || results.Count >= MaxWindows)
                    {
                        break;
                    }

                    var window = AxNative.CFArrayGetValueAtIndex(windows, i);
                    if (window == IntPtr.Zero)
                    {
                        continue;
                    }

                    var title = CopyString(window, AxNative.AxTitle) ?? processName;
                    var text = ReadTree(window, clock, cancellationToken);
                    if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    results.Add(new ScrapedWindow
                    {
                        ProcessName = processName,
                        Title = title ?? string.Empty,
                        Text = text
                    });
                }
            }
            finally
            {
                AxNative.Release(windows);
            }
        }
        finally
        {
            AxNative.Release(app);
        }
    }

    private static string ReadTree(IntPtr root, Stopwatch clock, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var nodes = 0;
        Walk(root, builder, ref nodes, clock, cancellationToken);
        if (builder.Length > MaxCharsPerWindow)
        {
            builder.Length = MaxCharsPerWindow;
        }

        return builder.ToString();
    }

    private static void Walk(
        IntPtr element,
        StringBuilder builder,
        ref int nodes,
        Stopwatch clock,
        CancellationToken cancellationToken)
    {
        if (element == IntPtr.Zero ||
            nodes >= MaxNodes ||
            clock.ElapsedMilliseconds >= BudgetMs ||
            builder.Length >= MaxCharsPerWindow ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        nodes++;
        Append(builder, CopyString(element, AxNative.AxTitle));
        Append(builder, CopyString(element, AxNative.AxValue));
        Append(builder, CopyString(element, AxNative.AxDescription));

        if (AxNative.AXUIElementCopyAttributeValue(element, AxNative.AxChildren, out var children) != AxNative.AxSuccess
            || children == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var count = AxNative.CFArrayGetCount(children);
            for (nint i = 0; i < count; i++)
            {
                Walk(AxNative.CFArrayGetValueAtIndex(children, i), builder, ref nodes, clock, cancellationToken);
                if (nodes >= MaxNodes || clock.ElapsedMilliseconds >= BudgetMs)
                {
                    break;
                }
            }
        }
        finally
        {
            AxNative.Release(children);
        }
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(value.Trim());
    }

    private static string? CopyString(IntPtr element, IntPtr attribute)
    {
        if (AxNative.AXUIElementCopyAttributeValue(element, attribute, out var value) != AxNative.AxSuccess
            || value == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return AxNative.ToString(value);
        }
        finally
        {
            AxNative.Release(value);
        }
    }
}
