using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
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
            // Initialize memory management
            MemoryManager.CleanupMemory();

            // Subscribe to theme changes
            AppearanceWindowViewModel.ColorThemeChanged += OnColorThemeChanged;
        }

        private void OnColorThemeChanged(object? sender, Wauncher.ViewModels.ColorTheme theme)
        {
            // Marshal to the UI thread, then apply to Application.Resources
            Dispatcher.UIThread.Post(() => ApplyThemeToResources(theme));
        }

        /// <summary>
        /// Applies a ColorTheme to the live Application.Resources so all
        /// DynamicResource-bound UI updates immediately. Must run on UI thread.
        /// </summary>
        public void ApplyThemeToResources(Wauncher.ViewModels.ColorTheme theme)
        {
            try
            {
                // Update main background (used by all panels and server selector)
                var brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(theme.BgMain));
                Resources["AppMainBackground"] = brush;

                // Update accent green (launch button, active tab border, etc.)
                var accentBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(theme.AccentGreen));
                Resources["AppAccentGreen"] = accentBrush;

                // Make slider knobs + filled track follow the accent color
                Resources["SliderThumbBackground"] = accentBrush;
                Resources["SliderThumbBackgroundPointerOver"] = accentBrush;
                Resources["SliderThumbBackgroundPressed"] = accentBrush;
                Resources["SliderTrackValueFill"] = accentBrush;
                Resources["SliderTrackValueFillPointerOver"] = accentBrush;
                Resources["SliderTrackValueFillPressed"] = accentBrush;
                Resources["SliderTrackValueFillDisabled"] = accentBrush;

                // Make ToggleSwitch "on" state follow the accent color
                Resources["ToggleSwitchFillOn"] = accentBrush;
                Resources["ToggleSwitchFillOnPointerOver"] = accentBrush;
                Resources["ToggleSwitchFillOnPressed"] = accentBrush;
                Resources["ToggleSwitchStrokeOn"] = accentBrush;
                Resources["ToggleSwitchStrokeOnPointerOver"] = accentBrush;
                Resources["ToggleSwitchStrokeOnPressed"] = accentBrush;

                // Update primary text color (all text derives from this)
                brush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(theme.TextPrimary));
                Resources["AppTextColor"] = brush;
                Resources["AppPrimaryText"] = brush;

                // Update secondary text colors (derived from primary but with transparency)
                var textColor = Avalonia.Media.Color.Parse(theme.TextPrimary);
                Resources["AppMutedText"] = new Avalonia.Media.SolidColorBrush(textColor) { Opacity = 0.53 };
                Resources["AppSectionLabel"] = new Avalonia.Media.SolidColorBrush(textColor) { Opacity = 0.33 };
                Resources["AppBodyText"] = new Avalonia.Media.SolidColorBrush(textColor) { Opacity = 0.8 };
                Resources["AppBulletText"] = new Avalonia.Media.SolidColorBrush(textColor) { Opacity = 0.67 };
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("App.ApplyThemeToResources", ex, "Failed to apply color theme");
            }
        }

        /// <summary>
        /// Loads the saved color theme from wauncher_settings and applies it at startup.
        /// </summary>
        private void ApplySavedThemeAtStartup()
        {
            try
            {
                var settings = SettingsWindowViewModel.LoadGlobal();
                var theme = settings.LoadColorTheme();
                if (theme != null)
                    ApplyThemeToResources(theme);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("App.ApplySavedThemeAtStartup", ex, "Failed to load saved color theme");
            }
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                try
                {
                    if (!Steam.IsInstalled())
                    {
                        ConsoleManager.ShowError(
                            "Steam is required to use Wauncher.\n\nPlease install Steam and relaunch.");
                        desktop.Shutdown();
                        return;
                    }

                    if (!IsSteamRunning())
                    {
                        ConsoleManager.ShowError(
                            "Steam must be open before using Wauncher.\n\nPlease open Steam, then relaunch Wauncher.");
                        desktop.Shutdown();
                        return;
                    }

                    if (Game.IsRunning())
                    {
                        ConsoleManager.ShowError(
                            "ClassicCounter is already running.\n\nPlease close the game before opening Wauncher again.");
                        desktop.Shutdown();
                        return;
                    }

                    bool hasRecentSteamUser = await Steam.GetRecentLoggedInSteamID(false);
                    if (!hasRecentSteamUser)
                    {
                        ConsoleManager.ShowError(
                            "Steam is open, but no logged-in Steam account was detected.\n\nPlease sign in to Steam and relaunch Wauncher.");
                        desktop.Shutdown();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("App.OnFrameworkInitializationCompleted", ex, "Application startup validation failed");
                    ConsoleManager.ShowError($"Startup error: {ex.Message}");
                    desktop.Shutdown();
                    return;
                }

                // Apply the saved color theme before showing the window so
                // custom colors persist across sessions without a flash of defaults.
                ApplySavedThemeAtStartup();

                desktop.MainWindow = new MainWindow();

                // Initialize Discord in background
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (DependencyChecks.IsDiscordInstalled())
                            Discord.Init();
                    }
                    catch
                    {
                        // Discord integration is optional.
                    }
                });

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
            catch (Exception ex)
            {
                ErrorLogger.LogError("App.IsSteamRunning", ex, "Failed to check if Steam is running");
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("App.SetupTrayIcon", ex, "Failed to load tray icon");
                // Tray icon is optional, log but don't fail
                System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
            }

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

