using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.IO;
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
            var owner = VisualRoot as Window;
            var infoWindow = new InfoWindow();
            if (owner != null)
                infoWindow.Show(owner);
            else
                infoWindow.Show();
        }

        private void Button_Settings(object? sender, RoutedEventArgs e)
        {
            var owner = VisualRoot as Window;
            var settingsWindow = new SettingsWindow();
            if (owner != null)
                settingsWindow.Show(owner);
            else
                settingsWindow.Show();
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
