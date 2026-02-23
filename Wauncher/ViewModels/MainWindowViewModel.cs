using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Utils;

namespace Wauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public string GameStatus { get; private set; } = "Game Status: ";
        
        public string ProtocolManager { get; private set; } = "Selected server: ";
        
        [ObservableProperty]
        private string _profilePicture = "https://avatars.githubusercontent.com/u/75831703?v=4";

        [ObservableProperty]
        private string _usernameGreeting = "Hello, username";
        
        public string WhitelistStatus { get; set; } = "Gray";
        
        public MainWindowViewModel()
        {
            if (Argument.Exists("--protocol-command"))
            {
                ProtocolManager = ProtocolManager + "Ready to Launch!";
            }

            Discord.OnAvatarUpdate += (avatarUrl) =>
            {
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    Dispatcher.UIThread.Post(() => ProfilePicture = avatarUrl);
                }
            };

            Discord.OnUsernameUpdate += (username) =>
            {
                if (!string.IsNullOrEmpty(username))
                {
                    Dispatcher.UIThread.Post(() => UsernameGreeting = $"Hello, {username}");
                }
            };
        }
    }
}
