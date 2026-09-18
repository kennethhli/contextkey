using Avalonia.Controls;
using Avalonia.Interactivity;
using ContextKey.UI.ViewModels;

namespace ContextKey.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnNewClicked(object? sender, RoutedEventArgs e) => Vm?.NewSnippet();

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => Vm?.DeleteSelected();

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Vm?.Save();

    private void OnSaveExcludedClicked(object? sender, RoutedEventArgs e) => Vm?.SaveExcluded();
}
