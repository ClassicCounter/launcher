using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Wauncher.ViewModels;

namespace Wauncher;

public partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        DataContext = new InfoWindowViewModel();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
