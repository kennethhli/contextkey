using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace ContextKey.Infrastructure.Input;

public sealed class SharpHookKeyboardHookService : IKeyboardHookService
{
    private SimpleGlobalHook? _hook;

    public event EventHandler<KeystrokeEventArgs>? KeyReceived;

    public bool IsRunning => _hook is { IsRunning: true };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        UioHookProvider.Instance.KeyTypedEnabled = true;

        _hook = new SimpleGlobalHook(UioHookProvider.Instance);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyTyped += OnKeyTyped;
        _ = _hook.RunAsync(GlobalHookType.Keyboard, true);
    }

    public void Stop()
    {
        _hook?.Stop();
    }

    public void Dispose()
    {
        if (_hook is null)
        {
            return;
        }

        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyTyped -= OnKeyTyped;
        _hook.Dispose();
        _hook = null;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        Dispatch(MapPressed(e), e);
    }

    private void OnKeyTyped(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        // KeyTyped can't be suppressed; we only use it for the character
        Dispatch(MapTyped(e), suppressible: false, e);
    }

    private void Dispatch(Keystroke keystroke, KeyboardHookEventArgs e) =>
        Dispatch(keystroke, suppressible: true, e);

    private void Dispatch(Keystroke keystroke, bool suppressible, KeyboardHookEventArgs e)
    {
        var args = new KeystrokeEventArgs { Keystroke = keystroke };
        KeyReceived?.Invoke(this, args);
        if (suppressible && args.Handled)
        {
            e.SuppressEvent = true;
        }
    }

    private static Keystroke MapPressed(KeyboardHookEventArgs e)
    {
        var code = e.Data.KeyCode;
        var mask = e.RawEvent.Mask;
        var isSpace = code == KeyCode.VcSpace;

        return new Keystroke(
            Character: isSpace ? ' ' : null,
            KeyName: code.ToString(),
            IsBackspace: code == KeyCode.VcBackspace,
            IsEscape: code == KeyCode.VcEscape,
            IsEnter: code is KeyCode.VcEnter or KeyCode.VcNumPadEnter,
            IsTab: code == KeyCode.VcTab,
            Alt: mask.HasAlt(),
            Control: mask.HasCtrl(),
            Shift: mask.HasShift(),
            Meta: mask.HasMeta());
    }

    private static Keystroke MapTyped(KeyboardHookEventArgs e)
    {
        var ch = e.Data.KeyChar;
        var mask = e.RawEvent.Mask;

        return new Keystroke(
            Character: ch == '\0' ? null : ch,
            KeyName: e.Data.KeyCode.ToString(),
            IsBackspace: false,
            IsEscape: false,
            IsEnter: false,
            IsTab: false,
            Alt: mask.HasAlt(),
            Control: mask.HasCtrl(),
            Shift: mask.HasShift(),
            Meta: mask.HasMeta());
    }
}
