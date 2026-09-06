using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ContextKey.UI;

public partial class App : Application
{
    private ExpansionHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // no main window, tray only
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _host?.Dispose();

            try
            {
                _host = ExpansionHost.Start();
            }
            catch (Exception ex)
            {
                // usually accessibility / input monitoring isn't granted yet
                Console.Error.WriteLine($"hook failed to start: {ex.Message}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _host?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
