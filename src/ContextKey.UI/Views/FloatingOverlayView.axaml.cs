using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ContextKey.Infrastructure.macOS;
using ContextKey.UI.ViewModels;

namespace ContextKey.UI.Views;

public partial class FloatingOverlayView : Window
{
    private bool _allowDeactivateClose;

    public FloatingOverlayView()
    {
        InitializeComponent();
        Opened += OnOpened;
        Deactivated += OnDeactivated;
        PointerPressed += (_, _) => FocusOverlay();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        ResultList.DoubleTapped += (_, _) => ConfirmAndClose();
        ResultList.SelectionChanged += (_, _) =>
        {
            if (ResultList.SelectedItem is not null)
            {
                ResultList.ScrollIntoView(ResultList.SelectedItem);
            }
        };
    }

    public string? ChosenValue { get; private set; }

    public int RestorePid { get; set; }

    public int EraseCount { get; set; }

    private void OnOpened(object? sender, EventArgs e)
    {
        FocusOverlay();
        QueryBox.Focus();
        QueryBox.CaretIndex = QueryBox.Text?.Length ?? 0;

        // macOS often fires Deactivated as the window appears; ignore that first blip
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _allowDeactivateClose = true;
        };
        timer.Start();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_allowDeactivateClose)
        {
            CloseIfOpen();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FloatingOverlayViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseIfOpen();
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            ConfirmAndClose();
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            vm.MoveSelection(1);
            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            vm.MoveSelection(-1);
        }
    }

    private void ConfirmAndClose()
    {
        if (DataContext is FloatingOverlayViewModel vm)
        {
            ChosenValue = vm.Confirm();
        }

        CloseIfOpen();
    }

    private void FocusOverlay()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacFocus.ActivateThisApp();
        }

        Activate();
    }

    private void CloseIfOpen()
    {
        if (!IsVisible)
        {
            return;
        }

        Close();
    }
}
