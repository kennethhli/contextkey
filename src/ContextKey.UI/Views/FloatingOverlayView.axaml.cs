using Avalonia.Controls;
using Avalonia.Input;
using ContextKey.UI.ViewModels;

namespace ContextKey.UI.Views;

public partial class FloatingOverlayView : Window
{
    public FloatingOverlayView()
    {
        InitializeComponent();
        Opened += OnOpened;
        Deactivated += (_, _) => CloseIfOpen();
        KeyDown += OnKeyDown;
    }

    public string? ChosenValue { get; private set; }

    private void OnOpened(object? sender, EventArgs e)
    {
        QueryBox.Focus();
        QueryBox.CaretIndex = QueryBox.Text?.Length ?? 0;
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

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ChosenValue = vm.Confirm();
            CloseIfOpen();
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

    private void CloseIfOpen()
    {
        if (!IsVisible)
        {
            return;
        }

        Close();
    }
}
