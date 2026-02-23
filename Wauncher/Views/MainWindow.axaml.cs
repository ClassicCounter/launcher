using Avalonia.Controls;
using Launcher.Utils;

namespace Wauncher.Views
{
    public partial class MainWindow : Window
    {
        private InfoWindow? _infoWindow = null;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await Game.Launch();
            Discord.SetDetails("In Main Menu");
            Discord.Update();
            await Game.Monitor();
        }
        private void Button_Info(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_infoWindow == null)
            {
                _infoWindow = new InfoWindow();
                _infoWindow.Closed += (s, e) => _infoWindow = null;
                _infoWindow.Show(this);
            }
            else
            {
                _infoWindow.Activate();
            }
        }
    }
}