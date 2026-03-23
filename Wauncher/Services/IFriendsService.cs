using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Wauncher.Utils;

namespace Wauncher.Services
{
    public interface IFriendsService
    {
        ObservableCollection<FriendInfo> Friends { get; }
        string CurrentUserAvatar { get; }
        string CurrentUserUsername { get; }
        string WhitelistText { get; }
        string WhitelistDotColor { get; }
        bool FriendsShowStatus { get; }
        bool ShowNoFriendsState { get; }
        bool ShowGenericFriendsStatus { get; }
        string FriendsStatus { get; }
        void Start();
        Task RefreshFriendsAsync();
        Task RefreshFriendsSafeAsync();
        Task LoadSelfProfileAsync();
        bool IsOfflineMode { get; }
    }
}
