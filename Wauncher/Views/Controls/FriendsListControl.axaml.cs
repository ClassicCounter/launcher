using Avalonia.Controls;
using Avalonia.Interactivity;
using Wauncher.Utils;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class FriendsListControl : UserControl
    {
        public FriendsListControl()
        {
            InitializeComponent();
        }

        private void ViewFriendProfile_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: FriendInfo friend })
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            vm.ViewFriendProfileCommand.Execute(friend);
        }

        private void JoinFriendServer_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: FriendInfo friend })
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            vm.JoinFriendServerCommand.Execute(friend);
        }
    }
}
