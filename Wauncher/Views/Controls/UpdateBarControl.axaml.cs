using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Wauncher.ViewModels;
using Wauncher.Views;

namespace Wauncher.Views.Controls
{
    public partial class UpdateBarControl : UserControl
    {
        public UpdateBarControl()
        {
            InitializeComponent();
        }

        private void Button_Info(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            vm.IsSettingsPanelOpen = false;
            vm.IsInfoPanelOpen = !vm.IsInfoPanelOpen;
        }

        private async void Button_Settings(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            vm.IsInfoPanelOpen = false;
            vm.IsSettingsPanelOpen = !vm.IsSettingsPanelOpen;

            // Trigger Add to Steam button refresh when opening
            if (vm.IsSettingsPanelOpen && VisualRoot is MainWindow win)
            {
                var panel = win.FindControl<SettingsPanel>("SettingsPanelControl");
                if (panel != null)
                    await panel.OnOpenAsync();
            }
        }

        private void Button_Appearance(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            vm.IsAppearancePanelOpen = !vm.IsAppearancePanelOpen;
        }

        private void OpenGameFolder_Click(object? sender, RoutedEventArgs e)
        {
            var dir = Path.GetDirectoryName(System.Environment.ProcessPath ?? string.Empty) ?? Directory.GetCurrentDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
    }
}
