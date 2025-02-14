using Avalonia.Controls;
using Avalonia.Interactivity;
using CCLauncher_GUI.ViewModels;

namespace CCLauncher_GUI.Views
{
    public partial class MainContent : UserControl
    {
        public MainContent()
        {
            InitializeComponent();
        }

        // The ! operator tells the compiler this will not be null
        private void OnNewsButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl!.SelectedIndex = 0;
        }

        private void OnPlayButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl!.SelectedIndex = 1;
        }

        private void OnServersButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl!.SelectedIndex = 2;
        }

        private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl!.SelectedIndex = 3;
        }
    }
}