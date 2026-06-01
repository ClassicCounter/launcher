using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Wauncher.Utils;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class SettingsPanel : UserControl
    {
        private bool _allowHardwareAccelerationRestart;
        private bool _isRefreshingSteamButton;

        public event EventHandler? CloseRequested;

        public SettingsPanel()
        {
            InitializeComponent();
            DataContext = new SettingsWindowViewModel();

            SettingsWindowViewModel.DiscordRpcChanged += OnDiscordRpcChangedExternally;

            ClosePanelButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnDiscordRpcChangedExternally(bool enabled)
        {
            if (DataContext is SettingsWindowViewModel vm && vm.DiscordRpc != enabled)
                vm.DiscordRpc = enabled;
        }

        public async Task OnOpenAsync()
        {
            _allowHardwareAccelerationRestart = true;
            await RefreshSteamButtonStateAsync();
        }

        private void DisableHardwareAccelerationToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (!_allowHardwareAccelerationRestart)
                return;

            if (DataContext is SettingsWindowViewModel vm && sender is ToggleSwitch toggle)
            {
                vm.DisableHardwareAcceleration = toggle.IsChecked ?? false;
                vm.Save();
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = "rebootas",
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                        });
                    }
                }
                catch { }

                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ForceQuit();
                    return;
                }

                Environment.Exit(0);
            }, DispatcherPriority.Background);
        }

        private async void AddToSteamButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isRefreshingSteamButton)
                return;

            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    ConsoleManager.ShowError("Couldn't find the running Wauncher executable path.");
                    return;
                }

                await SteamShortcutManager.AddClassicCounterToSteamAsync(exePath);
                await RefreshSteamButtonStateAsync();
            }
            catch (Exception ex)
            {
                ConsoleManager.ShowError($"Failed to add ClassicCounter to Steam:\n{ex.Message}");
            }
        }

        private async Task RefreshSteamButtonStateAsync()
        {
            if (AddToSteamButton == null)
                return;

            try
            {
                _isRefreshingSteamButton = true;
                AddToSteamButton.IsEnabled = false;
                AddToSteamButton.Content = "Checking...";

                var exePath = Environment.ProcessPath;
                bool isAdded = !string.IsNullOrWhiteSpace(exePath) &&
                               await SteamShortcutManager.IsClassicCounterAddedToSteamAsync(exePath);

                AddToSteamButton.Content = isAdded ? "✓ Added" : "Add to Steam";
                AddToSteamButton.IsEnabled = !isAdded;
            }
            catch
            {
                AddToSteamButton.Content = "Add to Steam";
                AddToSteamButton.IsEnabled = true;
            }
            finally
            {
                _isRefreshingSteamButton = false;
            }
        }
    }
}
