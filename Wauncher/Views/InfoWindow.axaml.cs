using Avalonia.Controls;
using Wauncher.ViewModels;

namespace Wauncher;

public partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        DataContext = new InfoWindowViewModel();
    }
}