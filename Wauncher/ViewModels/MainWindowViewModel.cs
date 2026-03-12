using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Wauncher.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using FriendInfo = Wauncher.Utils.FriendInfo;

namespace Wauncher.ViewModels
{
    public class ServerInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name   { get; set; } = "";
        public string IpPort { get; set; } = "";

        private int    _players;
        private int    _maxPlayers;
        private bool   _isOnline;
        private string _map = "";

        public int Players
        {
            get => _players;
            set
            {
                if (_players == value) return;
                _players = value;
                Notify(nameof(Players), nameof(PlayerCount));
            }
        }

        public int MaxPlayers
        {
            get => _maxPlayers;
            set
            {
                if (_maxPlayers == value) return;
                _maxPlayers = value;
                Notify(nameof(MaxPlayers), nameof(PlayerCount));
            }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline == value) return;
                _isOnline = value;
                Notify(nameof(IsOnline), nameof(DotColor));
            }
        }

        public string Map
        {
            get => _map;
            set
            {
                if (_map == value) return;
                _map = value;
                Notify(nameof(Map), nameof(MapDisplay));
            }
        }

        public bool IsNone => string.IsNullOrEmpty(IpPort);

        public string PlayerCount => IsNone ? "" : $"{Players}/{MaxPlayers}";
        public string DotColor    => IsNone ? "Transparent" : (IsOnline ? "#4CAF50" : "#F44336");
        public string NameColor   => IsNone ? "#66FFFFFF" : "White";
        public string MapDisplay  => (!IsNone && !string.IsNullOrEmpty(Map)) ? Map : "";

        private void Notify(params string[] names)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var name in names)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            });
        }
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _gameStatus = "Not Running";

        [ObservableProperty]
        private string _protocolManager = "None";

        [ObservableProperty]
        private string _profilePicture = "https://avatars.githubusercontent.com/u/75831703?v=4";

        [ObservableProperty]
        private string _usernameGreeting = "Hello, username";

        [ObservableProperty]
        private string _whitelistDotColor = "Gray";

        [ObservableProperty]
        private string _whitelistText = "Unknown";

        [ObservableProperty]
        private bool _isDropdownOpen = false;

        [ObservableProperty]
        private string _activeRightTab = "Friends";

        public bool IsFriendsTabActive    => ActiveRightTab == "Friends";
        public bool IsPatchNotesTabActive => ActiveRightTab == "PatchNotes";

        partial void OnActiveRightTabChanged(string value)
        {
            OnPropertyChanged(nameof(IsFriendsTabActive));
            OnPropertyChanged(nameof(IsPatchNotesTabActive));
        }

        [ObservableProperty]
        private bool _isOfflineMode = false;

        public bool IsOnlineMode => !IsOfflineMode;
        partial void OnIsOfflineModeChanged(bool value) => OnPropertyChanged(nameof(IsOnlineMode));

        [ObservableProperty]
        private bool _isUpdating = false;

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private bool _isNeedingInstall = false;

        [ObservableProperty]
        private bool _isCheckingUpdates = false;

        public bool IsCheckingOrUpdating  => IsCheckingUpdates || IsUpdating || IsInstalling;
        public bool IsUpdatingOrInstalling => IsUpdating || IsInstalling;

        partial void OnIsCheckingUpdatesChanged(bool value) => OnPropertyChanged(nameof(IsCheckingOrUpdating));
        partial void OnIsInstallingChanged(bool value)
        {
            OnPropertyChanged(nameof(LaunchButtonText));
            OnPropertyChanged(nameof(IsCheckingOrUpdating));
            OnPropertyChanged(nameof(IsUpdatingOrInstalling));
        }
        partial void OnIsNeedingInstallChanged(bool value) => OnPropertyChanged(nameof(LaunchButtonText));

        [ObservableProperty]
        private string _updateStatus = "";

        [ObservableProperty]
        private string _updateStatusFile = "";

        [ObservableProperty]
        private string _updateStatusSpeed = "";

        [ObservableProperty]
        private double _updateProgress = 0;

        [ObservableProperty]
        private bool _updateIndeterminate = false;

        [ObservableProperty]
        private bool _updateAvailable = false;

        public string LaunchButtonText =>
            IsInstalling    ? "Installing Game..." :
            IsUpdating      ? "Updating..."        :
            IsNeedingInstall ? "Install Game"      :
            UpdateAvailable ? "Update"             :
            "Launch Game";

        partial void OnIsUpdatingChanged(bool value)
        {
            OnPropertyChanged(nameof(LaunchButtonText));
            OnPropertyChanged(nameof(IsCheckingOrUpdating));
            OnPropertyChanged(nameof(IsUpdatingOrInstalling));
        }
        partial void OnUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(LaunchButtonText));

        [ObservableProperty]
        private ServerInfo? _selectedServer;

        // What the SELECTED SERVER label shows
        public string SelectedLabel => SelectedServer?.IsNone == false
            ? SelectedServer.Name
            : "Server not selected...";

        public bool IsNoServerSelected => SelectedServer == null || SelectedServer.IsNone;
        public bool IsServerSelected   => SelectedServer != null && !SelectedServer.IsNone;

        public ObservableCollection<ServerInfo> Servers { get; } = new()
        {
            // ── None (clears selection) ──────────────────────────────────────
            new ServerInfo { Name = "None",                  IpPort = "",                            IsOnline = false },

            // ── Real servers ─────────────────────────────────────────────────
            new ServerInfo { Name = "NA | PUG | 64 Tick",   IpPort = "na.classiccounter.cc:27015",  Players = 0, MaxPlayers = 10, IsOnline = true },
            new ServerInfo { Name = "NA | PUG-2 | 64 Tick", IpPort = "na.classiccounter.cc:27016",  Players = 0, MaxPlayers = 10, IsOnline = true },
            new ServerInfo { Name = "EU | PUG | 64 Tick",   IpPort = "eu.classiccounter.cc:27016",  Players = 0, MaxPlayers = 10, IsOnline = true },
            new ServerInfo { Name = "EU | PUG | 128 Tick",  IpPort = "eu.classiccounter.cc:27015",  Players = 0, MaxPlayers = 10, IsOnline = true },
            new ServerInfo { Name = "EU | PUG-2 | 128 Tick",IpPort = "eu.classiccounter.cc:27022",  Players = 0, MaxPlayers = 10, IsOnline = true },
        };

        partial void OnSelectedServerChanged(ServerInfo? value)
        {
            // Update the label shown in the trigger button
            ProtocolManager = (value == null || value.IsNone) ? "None" : value.Name;
            OnPropertyChanged(nameof(SelectedLabel));
            OnPropertyChanged(nameof(IsNoServerSelected));
            OnPropertyChanged(nameof(IsServerSelected));
        }

        public MainWindowViewModel()
        {
            if (Argument.HasProtocolCommand())
                ProtocolManager = "Ready to Launch!";

            _ = LoadSelfProfileAsync();

            CheckWhitelistStatus();
            UpdateOfflineMode();
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            // Query servers immediately, then refresh every 30 seconds
            _ = RefreshServersSafeAsync();
            _serverRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _serverRefreshTimer.Tick += async (_, _) => await RefreshServersSafeAsync();
            _serverRefreshTimer.Start();

            // Query friends immediately, then refresh every 30 seconds
            _ = RefreshFriendsSafeAsync();
            _friendsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _friendsTimer.Tick += async (_, _) => await RefreshFriendsSafeAsync();
            _friendsTimer.Start();
        }

        private DispatcherTimer? _serverRefreshTimer;
        private int _serverRefreshInProgress;

        // ── Friends ───────────────────────────────────────────────────────────────
        public ObservableCollection<FriendInfo> Friends { get; } = new();

        [ObservableProperty] private bool   _friendsShowStatus = true;
        [ObservableProperty] private string _friendsStatus     = "Loading...";
        [ObservableProperty] private bool   _showNoFriendsState = false;
        public bool ShowGenericFriendsStatus => FriendsShowStatus && !ShowNoFriendsState;

        partial void OnFriendsShowStatusChanged(bool value) => OnPropertyChanged(nameof(ShowGenericFriendsStatus));
        partial void OnShowNoFriendsStateChanged(bool value) => OnPropertyChanged(nameof(ShowGenericFriendsStatus));

        private DispatcherTimer? _friendsTimer;
        private int _friendsRefreshInProgress;
        private string _lastRenderedFriendsSignature = string.Empty;
        private string _lastKnownSteamId2 = string.Empty;

        private async Task LoadSelfProfileAsync()
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
                    if (!string.IsNullOrWhiteSpace(self.AvatarUrl))
                        ProfilePicture = AvatarCache.GetDisplaySource(self.AvatarUrl);

                    if (!string.IsNullOrWhiteSpace(self.Username))
                        UsernameGreeting = $"Hello, {self.Username}";
                });
            }
            catch
            {
                // Best-effort profile load; keep defaults on failure.
            }
        }

        private async Task RefreshServersAsync()
        {
            if (IsOfflineMode)
            {
                foreach (var s in Servers.Where(s => !s.IsNone))
                {
                    s.IsOnline = false;
                    s.Players = 0;
                    s.MaxPlayers = 0;
                    s.Map = "";
                }
                return;
            }

            await ServerQuery.RefreshServers(Servers.Where(s => !s.IsNone));

            // Re-order by player count descending; None always stays at index 0
            var sorted = Servers.Where(s => !s.IsNone)
                                .OrderByDescending(s => s.Players)
                                .ToList();
            int insertAt = 1;
            foreach (var server in sorted)
            {
                int from = Servers.IndexOf(server);
                if (from != insertAt)
                    Servers.Move(from, insertAt);
                insertAt++;
            }
        }

        private async Task RefreshServersSafeAsync()
        {
            if (Interlocked.Exchange(ref _serverRefreshInProgress, 1) == 1)
                return;

            try
            {
                await RefreshServersAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _serverRefreshInProgress, 0);
            }
        }

        private async Task RefreshFriendsAsync()
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
                        FriendsStatus     = "Steam is not installed.";
                        FriendsShowStatus = true;
                    });
                    return;
                }

                if (string.IsNullOrEmpty(Steam.recentSteamID2))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ShowNoFriendsState = false;
                        FriendsStatus     = "Sign in to Steam to see friends.";
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
                    FriendsStatus      = Friends.Count == 0 ? "No friends found." : "";
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
                    FriendsStatus      = IsOfflineMode ? "Offline mode" : "Couldn't load friends right now.";
                    FriendsShowStatus  = true;
                });
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

                var matches = Servers
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

        private async Task RefreshFriendsSafeAsync()
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





        private void CheckWhitelistStatus()
        {
            Task.Run(async () =>
            {
                try
                {
                    bool hasSteam = await Steam.GetRecentLoggedInSteamID(false);
                    if (!hasSteam)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            WhitelistDotColor = "Gray";
                            WhitelistText     = "Unknown";
                        });
                        return;
                    }

                    if (string.IsNullOrEmpty(Steam.recentSteamID2))
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            WhitelistDotColor = "Gray";
                            WhitelistText     = "Unknown";
                        });
                        return;
                    }

                    var response = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID2);
                    bool whitelisted = response?.Files != null && response.Files.Count > 0;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        WhitelistDotColor = whitelisted ? "#4CAF50" : "#F44336";
                        WhitelistText     = whitelisted ? "Whitelisted" : "Not Whitelisted";
                    });
                }
                catch
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        WhitelistDotColor = "Gray";
                        WhitelistText     = "Unknown";
                    });
                }
            });
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            Dispatcher.UIThread.Post(UpdateOfflineMode);
        }

        private void UpdateOfflineMode()
        {
            IsOfflineMode = !NetworkInterface.GetIsNetworkAvailable();
        }
    }
}


