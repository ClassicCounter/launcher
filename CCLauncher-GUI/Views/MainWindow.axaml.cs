using Avalonia.Controls;
using CCLauncher_GUI.ViewModels;

namespace CCLauncher_GUI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }
}