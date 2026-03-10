using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Wauncher.ViewModels;

namespace Wauncher.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsWindowViewModel();

            SettingsWindowViewModel.DiscordRpcChanged += OnDiscordRpcChangedExternally;
            this.Closed += (_, _) => SettingsWindowViewModel.DiscordRpcChanged -= OnDiscordRpcChangedExternally;
        }

        private void OnDiscordRpcChangedExternally(bool enabled)
        {
            if (DataContext is SettingsWindowViewModel vm && vm.DiscordRpc != enabled)
                vm.DiscordRpc = enabled;
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
}
