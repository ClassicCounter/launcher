using Avalonia.Controls;
using Avalonia.Interactivity;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class ServerListControl : UserControl
    {
        public ServerListControl()
        {
            InitializeComponent();
        }

        public void SetDropdownOpen(bool isOpen)
        {
            var panel = this.FindControl<Border>("ServerListPanel");
            if (panel != null)
            {
                panel.MaxHeight = isOpen ? 270 : 0;
            }
        }

        private void ServerItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ServerInfo server })
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            vm.SelectServerCommand.Execute(server);
        }
    }
}
