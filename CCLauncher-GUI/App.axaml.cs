using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia;
using CCLauncher_GUI.ViewModels;
using Launcher.Utils;

namespace CCLauncher_GUI;
public partial class App : Application
{
    public override void Initialize()
    {
        ConsoleManager.InitializeDebugConsole();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };

            if (Debug.Enabled())
            {
                Terminal.Print("Initializing launcher...");
            }

            _ = vm.Initialize(); // start update check

            desktop.ShutdownRequested += (s, e) =>
            {
                ConsoleManager.HideConsole(); // baibai console
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}