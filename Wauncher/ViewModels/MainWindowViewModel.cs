using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using Wauncher.Utils;
using Wauncher.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System;
using System.Threading;
using System.Text.RegularExpressions;
using FriendInfo = Wauncher.Utils.FriendInfo;

namespace Wauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // Services
        private readonly IDiscordService _discordService;
        private readonly IGameService _gameService;
        private readonly ICarouselService _carouselService;
        private readonly IUpdateService _updateService;
        private readonly IServerService _serverService;
        private readonly IFriendsService _friendsService;

        // Observable Properties
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

        [ObservableProperty]
        private bool _isOfflineMode = false;

        [ObservableProperty]
        private ServerInfo? _selectedServer;

        private bool _forceClose = false;

        // Computed Properties
        public bool IsFriendsTabActive => ActiveRightTab == "Friends";
        public bool IsPatchNotesTabActive => ActiveRightTab == "PatchNotes";
        public bool IsOnlineMode => !IsOfflineMode;
        public bool IsCheckingOrUpdating => _updateService.IsCheckingUpdates || _updateService.IsUpdating || _updateService.IsInstalling;
        public bool IsUpdatingOrInstalling => _updateService.IsUpdating || _updateService.IsInstalling;
        public bool ShowUpdateStatus =>
            IsCheckingOrUpdating ||
            _updateService.UpdateStatusFile.StartsWith("Install error:", StringComparison.OrdinalIgnoreCase) ||
            _updateService.UpdateStatusFile.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        public bool IsInstallPending => _updateService.IsNeedingInstall;
        public bool IsUpdatePending => _updateService.IsUpdateAvailable && !_updateService.IsUpdating && !_updateService.IsInstalling;

        public string LaunchButtonText =>
            _updateService.IsInstalling ? "Installing Game..." :
            _updateService.IsUpdating ? "Updating..." :
            _updateService.IsNeedingInstall ? "Install Game" :
            _updateService.IsUpdateAvailable ? "Update" :
            "Launch Game";

        public string SelectedLabel => SelectedServer?.IsNone == false
            ? SelectedServer.Name
            : "Server not selected...";

        public bool IsNoServerSelected => SelectedServer == null || SelectedServer.IsNone;
        public bool IsServerSelected => SelectedServer != null && !SelectedServer.IsNone;

        // Service Properties (expose to UI)
        public ObservableCollection<ServerInfo> Servers => _serverService.Servers;
        public ObservableCollection<FriendInfo> Friends => _friendsService.Friends;
        public bool FriendsShowStatus => _friendsService.FriendsShowStatus;
        public bool ShowNoFriendsState => _friendsService.ShowNoFriendsState;
        public bool ShowGenericFriendsStatus => _friendsService.ShowGenericFriendsStatus;
        public string FriendsStatus => _friendsService.FriendsStatus;
        public IUpdateService UpdateService => _updateService;

        // Commands
        [RelayCommand]
        private async Task LaunchGameAsync()
        {
            if (_updateService.IsInstalling || _updateService.IsUpdating || _updateService.IsCheckingUpdates)
            {
                string errorMsg = "Launch blocked: Update operation in progress";
                Terminal.Warning(errorMsg);
                ErrorLogger.LogError("MainWindowViewModel.LaunchGameAsync", errorMsg, "User attempted to launch during update");
                return;
            }

            if (_updateService.IsNeedingInstall)
            {
                await InstallGameAsync();
                return;
            }

            if (_updateService.IsUpdateAvailable)
            {
                await ValidateFilesAsync();
                return;
            }

            if (_gameService.IsRunning())
            {
                string errorMsg = "ClassicCounter is already running.\n\nPlease close the game before joining a server from Wauncher.";
                ConsoleManager.ShowError(errorMsg);
                ErrorLogger.LogError("MainWindowViewModel.LaunchGameAsync", "Game already running", "User attempted to launch while game is running");
                return;
            }

            // Validate game executable exists before attempting launch
            string gameExePath = Path.Combine(Directory.GetCurrentDirectory(), "csgo.exe");
            if (!File.Exists(gameExePath))
            {
                string errorMsg = "Game executable not found!\n\ncsgo.exe is missing from the launcher directory.\nPlease verify your game files or reinstall.";
                ConsoleManager.ShowError(errorMsg);
                ErrorLogger.LogError("MainWindowViewModel.LaunchGameAsync", "Game executable not found", $"Path: {gameExePath}");
                return;
            }

            GameStatus = "Running";

            try
            {
                var settings = SettingsWindowViewModel.LoadGlobal();
                var selected = SelectedServer;

                // Clear any arguments left over from a previous launch before adding new ones.
                _gameService.ClearAdditionalArguments();

                string? connectTarget = null;
                if (selected != null && !selected.IsNone && !string.IsNullOrEmpty(selected.IpPort))
                {
                    connectTarget = selected.IpPort;
                    Terminal.Print($"Connecting to server: {selected.Name} ({selected.IpPort})");
                }
                else
                {
                    Terminal.Print("Launching game without server connection");
                }

                bool launchSuccess = await _gameService.LaunchAsync(connectTarget, settings.LaunchOptions);
                
                if (!launchSuccess)
                {
                    ConsoleManager.ShowError("Failed to launch game. Please check the logs for details.");
                    ErrorLogger.LogError("MainWindowViewModel.LaunchGameAsync", "Game launch returned false", $"Connect target: {connectTarget}, Launch options: {settings.LaunchOptions}");
                    return;
                }

                if (settings.DiscordRpc)
                {
                    await _discordService.SetDetailsAsync((selected != null && !selected.IsNone)
                        ? $"Playing on {selected.Name}" : "In Main Menu");
                    await _discordService.UpdateAsync();
                }

                await _gameService.MonitorAsync();
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to launch game:\n{ex.Message}";
                ConsoleManager.ShowError(errorMsg);
                ErrorLogger.LogError("MainWindowViewModel.LaunchGameAsync", ex, $"Connect target: {SelectedServer?.IpPort}, Launch options: {SettingsWindowViewModel.LoadGlobal().LaunchOptions}");
            }
            finally
            {
                GameStatus = "Not Running";
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            await _updateService.CheckForUpdatesAsync();
        }

        [RelayCommand]
        private async Task InstallGameAsync()
        {
            bool installed = await _updateService.InstallGameFromCdnAsync();
            if (!installed)
                return;

            try
            {
                bool needsUpdate = await _updateService.CheckForUpdatesAsync();
                if (needsUpdate || _updateService.IsUpdateAvailable)
                    await _updateService.ValidateGameFilesAsync();
            }
            catch
            {
            }
        }

        [RelayCommand]
        private async Task ValidateFilesAsync()
        {
            await _updateService.ValidateGameFilesAsync();
        }

        [RelayCommand]
        private void ToggleServerDropdown()
        {
            if (_serverService.IsOfflineMode)
            {
                IsDropdownOpen = false;
                return;
            }

            IsDropdownOpen = !IsDropdownOpen;
        }

        [RelayCommand]
        private void SelectServer(ServerInfo? server)
        {
            SelectedServer = server?.IsNone == true ? null : server;
            ProtocolManager = (server == null || server.IsNone) ? "None" : server.Name;
            IsDropdownOpen = false;
        }

        [RelayCommand]
        private void SwitchToFriendsTab()
        {
            ActiveRightTab = "Friends";
        }

        [RelayCommand]
        private void SwitchToPatchNotesTab()
        {
            ActiveRightTab = "PatchNotes";
        }

        [RelayCommand]
        private void ViewFriendProfile(FriendInfo friend)
        {
            if (friend == null) return;

            var profileId = ResolveProfileSteamId(friend.SteamId);
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"https://eddies.cc/profiles/{profileId}",
                UseShellExecute = true
            });
        }

        private static string ResolveProfileSteamId(string? steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return string.Empty;

            var value = steamId.Trim();
            if (ulong.TryParse(value, out _))
                return value;

            if (TryConvertSteamId2To64(value, out var steamId64))
                return steamId64.ToString();

            return string.Empty;
        }

        private static bool TryConvertSteamId2To64(string steamId2, out ulong steamId64)
        {
            steamId64 = 0;
            var match = Regex.Match(steamId2, @"^STEAM_[0-5]:([0-1]):(\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            if (!ulong.TryParse(match.Groups[1].Value, out var y))
                return false;
            if (!ulong.TryParse(match.Groups[2].Value, out var z))
                return false;

            steamId64 = 76561197960265728UL + (z * 2UL) + y;
            return true;
        }

        [RelayCommand]
        private async Task JoinFriendServerAsync(FriendInfo friend)
        {
            if (friend == null || string.IsNullOrEmpty(friend.QuickJoinIpPort)) return;

            // Find matching server and select it
            var matchingServer = Servers.FirstOrDefault(s => 
                !s.IsNone && string.Equals(s.IpPort, friend.QuickJoinIpPort, StringComparison.OrdinalIgnoreCase));
            
            if (matchingServer != null)
            {
                SelectedServer = matchingServer;
                await LaunchGameAsync();
            }
        }

        // Constructor with dependency injection
        public MainWindowViewModel(
            IDiscordService discordService,
            IGameService gameService,
            ICarouselService carouselService,
            IUpdateService updateService,
            IServerService serverService,
            IFriendsService friendsService)
        {
            _discordService = discordService;
            _gameService = gameService;
            _carouselService = carouselService;
            _updateService = updateService;
            _serverService = serverService;
            _friendsService = friendsService;

            if (Argument.HasProtocolCommand())
                ProtocolManager = "Ready to Launch!";

            // Subscribe to service property changes
            SubscribeToServiceChanges();

            // Setup network monitoring
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            UpdateOfflineMode();

            // Initialize services after subscriptions are ready so early results reach the UI.
            _ = InitializeServicesAsync();
        }

        private async Task InitializeServicesAsync()
        {
            _serverService.Start();
            _friendsService.Start();

            try
            {
                await _discordService.InitializeAsync();
            }
            catch (Exception ex)
            {
                Terminal.Warning($"Failed to initialize Discord service: {ex.Message}");
            }

            try
            {
                await _carouselService.SetupCarouselAsync();
            }
            catch (Exception ex)
            {
                Terminal.Warning($"Failed to setup carousel: {ex.Message}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _friendsService.LoadSelfProfileAsync();
                    Dispatcher.UIThread.Post(SyncSelfProfile);
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to load self profile: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckWhitelistStatusAsync();
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to check whitelist status: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _serverService.RefreshServersSafeAsync();
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to refresh servers: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _friendsService.RefreshFriendsSafeAsync();
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to refresh friends: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _updateService.CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    Terminal.Warning($"Failed to check for updates: {ex.Message}");
                }
            });
        }

        private void SubscribeToServiceChanges()
        {
            // Subscribe to property changes from services to update UI
            if (_updateService is INotifyPropertyChanged updateNotifier)
            {
                updateNotifier.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(LaunchButtonText));
                    OnPropertyChanged(nameof(IsCheckingOrUpdating));
                    OnPropertyChanged(nameof(IsUpdatingOrInstalling));
                    OnPropertyChanged(nameof(ShowUpdateStatus));
                    OnPropertyChanged(nameof(IsInstallPending));
                    OnPropertyChanged(nameof(IsUpdatePending));
                };
            }

            if (_friendsService is INotifyPropertyChanged friendsNotifier)
            {
                friendsNotifier.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IFriendsService.FriendsStatus))
                        OnPropertyChanged(nameof(FriendsStatus));
                    if (e.PropertyName == nameof(IFriendsService.FriendsShowStatus))
                    {
                        OnPropertyChanged(nameof(FriendsShowStatus));
                        OnPropertyChanged(nameof(ShowGenericFriendsStatus));
                    }
                    if (e.PropertyName == nameof(IFriendsService.ShowNoFriendsState))
                    {
                        OnPropertyChanged(nameof(ShowNoFriendsState));
                        OnPropertyChanged(nameof(ShowGenericFriendsStatus));
                    }
                    if (e.PropertyName == nameof(IFriendsService.CurrentUserAvatar))
                        ProfilePicture = _friendsService.CurrentUserAvatar;
                    if (e.PropertyName == nameof(IFriendsService.CurrentUserUsername))
                        UsernameGreeting = $"Hello, {_friendsService.CurrentUserUsername}";
                };
            }
        }

        private void SyncSelfProfile()
        {
            ProfilePicture = _friendsService.CurrentUserAvatar;
            UsernameGreeting = $"Hello, {_friendsService.CurrentUserUsername}";
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            Dispatcher.UIThread.Post(UpdateOfflineMode);
        }

        private void UpdateOfflineMode()
        {
            IsOfflineMode = !NetworkInterface.GetIsNetworkAvailable();
        }

        private async Task CheckWhitelistStatusAsync()
        {
            try
            {
                bool hasSteam = await Steam.GetRecentLoggedInSteamID(false);
                if (!hasSteam || string.IsNullOrEmpty(Steam.recentSteamID2))
                {
                    WhitelistDotColor = "Gray";
                    WhitelistText = "Unknown";
                    return;
                }

                var response = await Api.ClassicCounter.GetFullGameDownload(Steam.recentSteamID2);
                bool whitelisted = response?.Files != null && response.Files.Count > 0;
                WhitelistDotColor = whitelisted ? "#4CAF50" : "#F44336";
                WhitelistText = whitelisted ? "Whitelisted" : "Not Whitelisted";
            }
            catch
            {
                WhitelistDotColor = "Gray";
                WhitelistText = "Unknown";
            }
        }

        partial void OnActiveRightTabChanged(string value)
        {
            OnPropertyChanged(nameof(IsFriendsTabActive));
            OnPropertyChanged(nameof(IsPatchNotesTabActive));
        }

        partial void OnSelectedServerChanged(ServerInfo? value)
        {
            OnPropertyChanged(nameof(SelectedLabel));
            OnPropertyChanged(nameof(IsNoServerSelected));
            OnPropertyChanged(nameof(IsServerSelected));
        }

        partial void OnIsOfflineModeChanged(bool value) => OnPropertyChanged(nameof(IsOnlineMode));
    }
}
