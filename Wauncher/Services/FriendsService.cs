using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Avalonia.Threading;
using Wauncher.Utils;
using Wauncher.ViewModels;

namespace Wauncher.Services
{
    public class FriendsService : IFriendsService, INotifyPropertyChanged
    {
        private const string DefaultAvatarUrl = "https://avatars.githubusercontent.com/u/75831703?v=4";
        private readonly IServerService _serverService;
        private DispatcherTimer? _friendsTimer;
        private int _friendsRefreshInProgress;
        private string _lastRenderedFriendsSignature = string.Empty;
        private string _lastKnownSteamId2 = string.Empty;
        private bool _started;

        public bool IsOfflineMode => !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

        public ObservableCollection<FriendInfo> Friends { get; } = new();

        private string _currentUserAvatar = DefaultAvatarUrl;
        public string CurrentUserAvatar
        {
            get => _currentUserAvatar;
            private set
            {
                _currentUserAvatar = string.IsNullOrWhiteSpace(value) ? DefaultAvatarUrl : value;
                OnPropertyChanged(nameof(CurrentUserAvatar));
            }
        }

        private string _currentUserUsername = "username";
        public string CurrentUserUsername
        {
            get => _currentUserUsername;
            private set
            {
                _currentUserUsername = string.IsNullOrWhiteSpace(value) ? "username" : value;
                OnPropertyChanged(nameof(CurrentUserUsername));
            }
        }

        private string _whitelistText = "Unknown";
        public string WhitelistText
        {
            get => _whitelistText;
            private set
            {
                _whitelistText = string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
                OnPropertyChanged(nameof(WhitelistText));
            }
        }

        private string _whitelistDotColor = "Gray";
        public string WhitelistDotColor
        {
            get => _whitelistDotColor;
            private set
            {
                _whitelistDotColor = string.IsNullOrWhiteSpace(value) ? "Gray" : value;
                OnPropertyChanged(nameof(WhitelistDotColor));
            }
        }

        private bool _friendsShowStatus = true;
        public bool FriendsShowStatus 
        { 
            get => _friendsShowStatus; 
            private set
            {
                _friendsShowStatus = value;
                OnPropertyChanged(nameof(FriendsShowStatus));
                OnPropertyChanged(nameof(ShowGenericFriendsStatus));
            }
        }

        private bool _showNoFriendsState = false;
        public bool ShowNoFriendsState 
        { 
            get => _showNoFriendsState; 
            private set
            {
                _showNoFriendsState = value;
                OnPropertyChanged(nameof(ShowNoFriendsState));
                OnPropertyChanged(nameof(ShowGenericFriendsStatus));
            }
        }

        public bool ShowGenericFriendsStatus => FriendsShowStatus && !ShowNoFriendsState;

        private string _friendsStatus = "Loading...";
        public string FriendsStatus 
        { 
            get => _friendsStatus; 
            private set
            {
                _friendsStatus = value;
                OnPropertyChanged(nameof(FriendsStatus));
            }
        }

        public FriendsService(IServerService serverService)
        {
            _serverService = serverService;
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            _ = RefreshFriendsSafeAsync();
            _friendsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _friendsTimer.Tick += async (_, _) => await RefreshFriendsSafeAsync();
            _friendsTimer.Start();
        }

        public async Task LoadSelfProfileAsync()
        {
            try
            {
                bool hasSteam = await Steam.GetRecentLoggedInSteamID(false);
                if (!hasSteam || string.IsNullOrWhiteSpace(Steam.recentSteamID64))
                    return;

                var rawSelfJson = await Api.Eddies.GetSelfInfo(Steam.recentSteamID64);
                var self = Api.ParseSelfInfoPayload(rawSelfJson);
                if (self == null)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    CurrentUserAvatar = AvatarCache.GetDisplaySource(self.AvatarUrl);
                    CurrentUserUsername = self.Username;
                });
            }
            catch
            {
                // Best-effort profile load; keep defaults on failure.
            }
        }

        public async Task RefreshFriendsAsync()
        {
            try
            {
                if (IsOfflineMode)
                {
                    var steamIdForCache = !string.IsNullOrWhiteSpace(_lastKnownSteamId2)
                        ? _lastKnownSteamId2
                        : (Steam.recentSteamID2 ?? string.Empty);

                    if (TryShowCachedFriends(steamIdForCache, forceOfflineStatus: true))
                        return;

                    Dispatcher.UIThread.Post(() =>
                    {
                        Friends.Clear();
                        _lastRenderedFriendsSignature = string.Empty;
                        ShowNoFriendsState = false;
                        FriendsStatus = "Offline mode";
                        FriendsShowStatus = true;
                    });
                    return;
                }

                bool hasSteam = await Steam.GetRecentLoggedInSteamID(false);
                string steamId = Steam.recentSteamID2 ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(steamId))
                    _lastKnownSteamId2 = steamId;

                if (!hasSteam)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ShowNoFriendsState = false;
                        FriendsStatus = "Steam is not installed.";
                        FriendsShowStatus = true;
                    });
                    return;
                }

                if (string.IsNullOrEmpty(Steam.recentSteamID2))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ShowNoFriendsState = false;
                        FriendsStatus = "Sign in to Steam to see friends.";
                        FriendsShowStatus = true;
                    });
                    return;
                }

                string rawFriendsJson;
                try
                {
                    rawFriendsJson = await Api.Eddies.GetFriends(Steam.recentSteamID64 ?? string.Empty);
                }
                catch
                {
                    rawFriendsJson = await Api.Eddies.GetFriendsBySteamId2(Steam.recentSteamID2 ?? string.Empty);
                }
                var apiFriends = Api.ParseFriendsPayload(rawFriendsJson)
                    .OrderBy(f => f.Status == "Offline" ? 1 : 0)
                    .ToList();

                await FriendsCache.SaveAsync(steamId, apiFriends);

                Dispatcher.UIThread.Post(() =>
                {
                    var sorted = apiFriends;

                    ApplyQuickJoinMetadata(sorted);

                    foreach (var f in sorted)
                        f.AvatarUrl = AvatarCache.GetDisplaySource(f.AvatarUrl);

                    var signature = BuildFriendsSignature(sorted);
                    if (!string.Equals(signature, _lastRenderedFriendsSignature, StringComparison.Ordinal))
                    {
                        Friends.Clear();
                        foreach (var f in sorted)
                            Friends.Add(f);
                        _lastRenderedFriendsSignature = signature;
                    }

                    FriendsShowStatus = Friends.Count == 0;
                    ShowNoFriendsState = Friends.Count == 0;
                    FriendsStatus = Friends.Count == 0 ? "No friends found." : "";
                });
            }
            catch
            {
                if (TryShowCachedFriends(Steam.recentSteamID2 ?? string.Empty, forceOfflineStatus: true))
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    Friends.Clear();
                    _lastRenderedFriendsSignature = string.Empty;
                    ShowNoFriendsState = false;
                    FriendsStatus = IsOfflineMode ? "Offline mode" : "Couldn't load friends right now.";
                    FriendsShowStatus = true;
                });
            }
        }

        public async Task RefreshFriendsSafeAsync()
        {
            if (Interlocked.Exchange(ref _friendsRefreshInProgress, 1) == 1)
                return;

            try
            {
                await RefreshFriendsAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _friendsRefreshInProgress, 0);
            }
        }

        private bool TryShowCachedFriends(string steamId, bool forceOfflineStatus)
        {
            var cached = FriendsCache.Load(steamId);
            if (cached.Count == 0)
                return false;

            var sorted = cached
                .OrderBy(f => f.Status == "Offline" ? 1 : 0)
                .ToList();

            if (forceOfflineStatus)
            {
                foreach (var f in sorted)
                    f.Status = "Offline";
            }

            ApplyQuickJoinMetadata(sorted);

            foreach (var f in sorted)
                f.AvatarUrl = AvatarCache.GetDisplaySource(f.AvatarUrl);

            Dispatcher.UIThread.Post(() =>
            {
                var signature = BuildFriendsSignature(sorted);
                if (!string.Equals(signature, _lastRenderedFriendsSignature, StringComparison.Ordinal))
                {
                    Friends.Clear();
                    foreach (var f in sorted)
                        Friends.Add(f);
                    _lastRenderedFriendsSignature = signature;
                }

                FriendsShowStatus = false;
                ShowNoFriendsState = false;
                FriendsStatus = "";
            });

            return true;
        }

        private static string BuildFriendsSignature(IEnumerable<FriendInfo> friends)
        {
            var sb = new StringBuilder();
            foreach (var f in friends)
            {
                sb.Append(f.Username ?? string.Empty)
                  .Append('\u001f')
                  .Append(f.AvatarUrl ?? string.Empty)
                  .Append('\u001f')
                  .Append(f.Status ?? "Offline")
                  .Append('\u001e');
            }
            return sb.ToString();
        }

        private void ApplyQuickJoinMetadata(IEnumerable<FriendInfo> friends)
        {
            foreach (var friend in friends)
            {
                friend.QuickJoinIpPort = string.Empty;
                friend.QuickJoinServerName = string.Empty;

                var serverName = ExtractServerNameFromStatus(friend.Status);
                if (string.IsNullOrWhiteSpace(serverName))
                    continue;

                var matches = _serverService.Servers
                    .Where(s => !s.IsNone)
                    .Where(s => string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 1)
                {
                    friend.QuickJoinIpPort = matches[0].IpPort;
                    friend.QuickJoinServerName = matches[0].Name;
                }
            }
        }

        private static string ExtractServerNameFromStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return string.Empty;

            var match = Regex.Match(
                status,
                @"^In Game - (?<name>.+?) \(\d+/\d+\)$",
                RegexOptions.IgnoreCase);

            return match.Success
                ? match.Groups["name"].Value.Trim()
                : string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
