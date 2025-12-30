using Launcher.Utils;

namespace Wauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public string Greeting { get; } = "Welcome to Avalonia!";
        public string GameStatus { get; private set; } = "Game Status: ";
        public string ProfilePicture { get; set; } = "https://avatars.githubusercontent.com/u/75831703?v=4";
        public string WhitelistStatus { get; set; } = "Gray";
        public MainWindowViewModel()
        {
            string? ProfilePicture = Discord.GetAvatar();
            if (ProfilePicture == null)
            {
                ProfilePicture = "https://avatars.githubusercontent.com/u/75831703?v=4";
            }
        }
    }
}
