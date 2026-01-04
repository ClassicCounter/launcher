using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Utils;

namespace Wauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public string GameStatus { get; private set; } = "Game Status: ";
        
        [ObservableProperty]
        private string _profilePicture = "https://avatars.githubusercontent.com/u/75831703?v=4";

        [ObservableProperty]
        private string _usernameGreeting = "Hello, username";
        
        public string WhitelistStatus { get; set; } = "Gray";
        
        public MainWindowViewModel()
        {
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
