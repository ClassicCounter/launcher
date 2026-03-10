using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using Wauncher.Utils;
using System.Diagnostics;
using Wauncher.ViewModels;
using Wauncher.Views;

namespace Wauncher
{
    public partial class App : Application
    {
        private TrayIcon? _trayIcon = null;
        private NativeMenuItem? _discordRpcMenuItem = null;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            ProtocolManager.RegisterURIHandler();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                if (!Steam.IsInstalled())
                {
                    Wauncher.Utils.ConsoleManager.ShowError(
                        "Steam is required to use Wauncher.\n\nPlease install Steam and relaunch.");
                    desktop.Shutdown();
                    return;
                }

                if (!IsSteamRunning())
                {
                    Wauncher.Utils.ConsoleManager.ShowError(
                        "Steam must be open before using Wauncher.\n\nPlease open Steam, then relaunch Wauncher.");
                    desktop.Shutdown();
                    return;
                }

                bool hasRecentSteamUser = Steam.GetRecentLoggedInSteamID(false).GetAwaiter().GetResult();
                if (!hasRecentSteamUser)
                {
                    Wauncher.Utils.ConsoleManager.ShowError(
                        "Steam is open, but no logged-in Steam account was detected.\n\nPlease sign in to Steam and relaunch Wauncher.");
                    desktop.Shutdown();
                    return;
                }

                // Always init so Discord username/avatar callbacks fire for the greeting.
                // Presence is only pushed via Update() when RPC is enabled.
                try
                {
                    if (DependencyChecks.IsDiscordInstalled())
                        Discord.Init();
                }
                catch
                {
                    // Discord integration is optional.
                }

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                desktop.Exit += (_, _) => _trayIcon?.Dispose();
            }

            SetupTrayIcon();
            base.OnFrameworkInitializationCompleted();
        }

        private static bool IsSteamRunning()
        {
            try
            {
                return Process.GetProcessesByName("steam").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void SetupTrayIcon()
        {
            var settings = SettingsWindowViewModel.LoadGlobal();

            _discordRpcMenuItem = new NativeMenuItem
            {
                Header = settings.DiscordRpc ? "Discord RPC ON" : "Discord RPC OFF"
            };
            _discordRpcMenuItem.Click += DiscordRpc_Click;

            var openItem = new NativeMenuItem { Header = "Open" };
            openItem.Click += (_, _) => ShowMainWindow();

            var exitItem = new NativeMenuItem { Header = "Exit" };
            exitItem.Click += (_, _) =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                {
                    if (d.MainWindow is Views.MainWindow mw)
                        mw.ForceQuit();
                    d.TryShutdown();
                }
            };

            var menu = new NativeMenu();
            menu.Items.Add(openItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(_discordRpcMenuItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exitItem);

            _trayIcon = new TrayIcon
            {
                ToolTipText = "ClassicCounter",
                Menu = menu,
            };

            try
            {
                var uri = new Uri("avares://Wauncher/Assets/Wauncher.ico");
                using var stream = AssetLoader.Open(uri);
                _trayIcon.Icon = new WindowIcon(stream);
            }
            catch { }

            _trayIcon.Clicked += (_, _) => ShowMainWindow();

            // Live sync
            SettingsWindowViewModel.DiscordRpcChanged += enabled => ApplyDiscordRpc(enabled);
        }

        private void ShowMainWindow()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null)
            {
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                desktop.MainWindow.Activate();
            }
        }

        public void DiscordRpc_Click(object? sender, EventArgs e)
        {
            var settings = SettingsWindowViewModel.LoadGlobal();
            settings.DiscordRpc = !settings.DiscordRpc; // auto-saves via OnDiscordRpcChanged
            ApplyDiscordRpc(settings.DiscordRpc);
        }

        private void ApplyDiscordRpc(bool enabled)
        {
            if (!DependencyChecks.IsDiscordInstalled())
            {
                if (_discordRpcMenuItem != null)
                    _discordRpcMenuItem.Header = "Discord RPC (Discord not installed)";
                return;
            }

            if (enabled)
            {
                Discord.SetDetails("In Main Menu");
                Discord.SetState(null);
                Discord.Update();
            }
            else
            {
                Discord.Deinitialize();
            }

            if (_discordRpcMenuItem != null)
                _discordRpcMenuItem.Header = enabled ? "Discord RPC ON" : "Discord RPC OFF";
        }

        [RelayCommand]
        public void TrayIconClicked() => ShowMainWindow();

        public void ExitApplication_Click(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.TryShutdown();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var toRemove = BindingPlugins.DataValidators
                .OfType<DataAnnotationsValidationPlugin>().ToArray();
            foreach (var plugin in toRemove)
                BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}

