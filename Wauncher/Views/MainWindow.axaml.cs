using Avalonia.Controls;
using Launcher.Utils;

namespace Wauncher.Views
{
    public partial class MainWindow : Window
    {
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
    }
}