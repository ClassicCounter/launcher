using System.IO;
using System.Net.Http;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using Wauncher.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wauncher.ViewModels;
using Wauncher.Views;

namespace Wauncher.Views
{
    public partial class MainWindow : Window
    {
        private InfoWindow?     _infoWindow     = null;
        private SettingsWindow? _settingsWindow = null;
        private SettingsWindowViewModel _settings;
        private int _launchInProgress;
        private int _updateInProgress;
        private int _installInProgress;

        private bool _dropdownOpen = false;

        private const double HeightClosed = 720;
        private const double HeightOpen   = 720;

        // â”€â”€ Image carousel (center content area) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Image[] _carouselImages = Array.Empty<Image>();
        private List<string> _carouselImageUrls = new();
        private DispatcherTimer? _carouselTimer;
        private int _currentCarouselIndex = 0;
        private int _currentCarouselSlot = 0;
        private int _carouselRotateInProgress = 0;
        private const int CarouselRotationIntervalSeconds = 5;
        private const int CarouselMaxWidth = 1280;
        private const int CarouselMaxHeight = 720;
        private readonly List<System.Threading.CancellationTokenSource?> _zoomCts = new();
        private static string WauncherDirectory =>
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? Directory.GetCurrentDirectory();

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsWindowViewModel.LoadGlobal();

            this.Loaded += (_, _) =>
            {
                LaunchUpdateButton.IsEnabled  = true;
            };

            this.Opened += (_, _) =>
            {
                _ = SetupCarouselAsync();
                _ = StartupAsync();
                _ = LoadPatchNotesAsync();

                if (DataContext is MainWindowViewModel vm2)
                    vm2.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.IsUpdating) ||
                            e.PropertyName == nameof(MainWindowViewModel.IsInstalling))
                            SetLaunchGlow(vm2.IsUpdating || vm2.IsInstalling);
                    };
            };

            // Window minimize always goes to taskbar; tray hide only happens on game launch.

            this.Closing += (s, e) =>
            {
                if (_forceClose) return;
                _settings = SettingsWindowViewModel.LoadGlobal();
                if (_settings.MinimizeToTray && IsGameRunning())
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            // Ensure carousel timer is stopped whenever the window closes,
            // regardless of which code path triggered it.
            this.Closed += (_, _) => TeardownCarousel();
        }

        // â”€â”€ Image carousel (center content area) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly HttpClient _http = new();
        private static string PatchNotesCachePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "cache",
                "patchnotes.md");
        private static string CarouselCacheDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "cache",
                "carousel");

        private async Task SetupCarouselAsync()
        {
            try
            {
                TeardownCarousel();

                var carouselContainer = this.FindControl<Grid>("CarouselContainer");
                var offlinePanel = this.FindControl<Border>("CarouselOfflinePanel");
                var offlineSubText = this.FindControl<TextBlock>("CarouselOfflineSubText");
                if (carouselContainer == null)
                    return;

                bool hasInternet = NetworkInterface.GetIsNetworkAvailable();
                var urls = hasInternet
                    ? await LoadCarouselUrlsFromGitHubAsync()
                    : null;

                if (urls == null || urls.Count == 0)
                {
                    if (offlinePanel != null)
                        offlinePanel.IsVisible = true;
                    if (offlineSubText != null)
                    {
                        offlineSubText.Text = hasInternet
                            ? "Carousel is temporarily unavailable."
                            : "Connect to Wi-Fi or Ethernet to load the carousel.";
                    }
                    return;
                }

                if (offlinePanel != null)
                    offlinePanel.IsVisible = false;

                _carouselImageUrls = urls;
                _carouselImages = CreateCarouselImages(2);
                EnsureZoomSlots(_carouselImages.Length);

                foreach (var existingImage in carouselContainer.Children.OfType<Image>().ToList())
                    carouselContainer.Children.Remove(existingImage);

                int overlayIndex = offlinePanel != null ? carouselContainer.Children.IndexOf(offlinePanel) : -1;
                for (int i = 0; i < _carouselImages.Length; i++)
                {
                    if (overlayIndex >= 0)
                    {
                        carouselContainer.Children.Insert(overlayIndex, _carouselImages[i]);
                        overlayIndex++;
                    }
                    else
                    {
                        carouselContainer.Children.Add(_carouselImages[i]);
                    }
                }

                _currentCarouselIndex = 0;
                _currentCarouselSlot = 0;
                await SetCarouselImageAsync(_carouselImages[_currentCarouselSlot], _carouselImageUrls[_currentCarouselIndex]);
                _carouselImages[_currentCarouselSlot].Opacity = 1.0;
                StartZoomOut(_carouselImages[_currentCarouselSlot], _currentCarouselSlot);

                _carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(CarouselRotationIntervalSeconds) };
                _carouselTimer.Tick += async (_, _) => await RotateCarouselAsync();
                _carouselTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Carousel: " + ex.Message);
            }
        }

        private async Task<List<string>?> LoadCarouselUrlsFromGitHubAsync()
        {
            try
            {
                var json = await Api.GitHub.GetCarouselAssetsWauncher();
                var assets = JsonConvert.DeserializeObject<List<GitHubAssetEntry>>(json);
                if (assets == null || assets.Count == 0)
                    return null;

                var urls = assets
                    .Where(a => string.Equals(a.Type, "file", StringComparison.OrdinalIgnoreCase))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name) && a.Name.StartsWith("carousel_", StringComparison.OrdinalIgnoreCase))
                    .Where(a => a.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                a.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                a.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                a.Name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    .Where(a => !string.IsNullOrWhiteSpace(a.DownloadUrl))
                    .OrderBy(a => GetCarouselSortIndex(a.Name))
                    .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(a => a.DownloadUrl!)
                    .ToList();

                if (urls.Count == 0)
                    return null;

                return urls;
            }
            catch { return null; }
        }

        private static int GetCarouselSortIndex(string name)
        {
            var match = Regex.Match(name, @"^carousel_(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
                return index;
            return int.MaxValue;
        }

        private sealed class GitHubAssetEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("type")]
            public string Type { get; set; } = string.Empty;

            [JsonProperty("download_url")]
            public string? DownloadUrl { get; set; }
        }

        private static Image[] CreateCarouselImages(int count)
        {
            var images = new Image[count];
            for (int i = 0; i < count; i++)
            {
                images[i] = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0.0,
                    Transitions = new Transitions
                    {
                        new DoubleTransition
                        {
                            Property = Visual.OpacityProperty,
                            Duration = TimeSpan.FromSeconds(1.5),
                            Easing = new CubicEaseInOut()
                        }
                    }
                };
            }

            return images;
        }

        private void EnsureZoomSlots(int count)
        {
            while (_zoomCts.Count < count)
                _zoomCts.Add(null);
        }

        private async Task RotateCarouselAsync()
        {
            if (_carouselImages.Length < 2 || _carouselImageUrls.Count < 2)
                return;

            if (Interlocked.Exchange(ref _carouselRotateInProgress, 1) == 1)
                return;

            try
            {
                int nextIndex = (_currentCarouselIndex + 1) % _carouselImageUrls.Count;
                int nextSlot = (_currentCarouselSlot + 1) % _carouselImages.Length;
                int currentSlot = _currentCarouselSlot;

                await SetCarouselImageAsync(_carouselImages[nextSlot], _carouselImageUrls[nextIndex]);

                _carouselImages[currentSlot].Opacity = 0.0;
                StartZoomOut(_carouselImages[nextSlot], nextSlot);
                _carouselImages[nextSlot].Opacity = 1.0;

                _currentCarouselIndex = nextIndex;
                _currentCarouselSlot = nextSlot;
            }
            finally
            {
                Interlocked.Exchange(ref _carouselRotateInProgress, 0);
            }
        }

        private void TeardownCarousel()
        {
            _carouselTimer?.Stop();
            _carouselTimer = null;
            for (int i = 0; i < _zoomCts.Count; i++) StopZoom(i);
            foreach (var image in _carouselImages)
            {
                if (image.Source is IDisposable disposable)
                    disposable.Dispose();

                image.Source = null;
            }
            _carouselImageUrls.Clear();
            _carouselImages = Array.Empty<Image>();
            _currentCarouselIndex = 0;
            _currentCarouselSlot = 0;
            Interlocked.Exchange(ref _carouselRotateInProgress, 0);
        }

        private async Task SetCarouselImageAsync(Image image, string url)
        {
            var nextBitmap = await LoadCarouselBitmapAsync(url);
            if (nextBitmap == null)
                return;

            if (image.Source is IDisposable disposable)
                disposable.Dispose();

            image.Source = nextBitmap;
        }

        private static async Task<Bitmap?> LoadCarouselBitmapAsync(string url)
        {
            try
            {
                var cachedBytes = await TryGetCachedCarouselBytesAsync(url);
                var bytes = cachedBytes ?? await _http.GetByteArrayAsync(url);
                var resized = cachedBytes ?? TryResizeCarouselBytes(bytes) ?? bytes;

                if (cachedBytes == null)
                    await TryWriteCarouselCacheAsync(url, resized);

                using var ms = new MemoryStream(resized);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<byte[]?> TryGetCachedCarouselBytesAsync(string url)
        {
            try
            {
                var path = GetCarouselCachePath(url);
                if (!File.Exists(path))
                    return null;

                return await File.ReadAllBytesAsync(path);
            }
            catch
            {
                return null;
            }
        }

        private static async Task TryWriteCarouselCacheAsync(string url, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(CarouselCacheDir);
                var path = GetCarouselCachePath(url);
                var tempPath = path + ".tmp";
                await File.WriteAllBytesAsync(tempPath, bytes);
                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                // Best-effort cache only.
            }
        }

        private static string GetCarouselCachePath(string url)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
            return Path.Combine(CarouselCacheDir, $"{hash}.jpg");
        }

        private static byte[]? TryResizeCarouselBytes(byte[] bytes)
        {
            try
            {
                using var sourceBitmap = SKBitmap.Decode(bytes);
                if (sourceBitmap == null)
                    return null;

                if (sourceBitmap.Width <= CarouselMaxWidth &&
                    sourceBitmap.Height <= CarouselMaxHeight)
                {
                    return null;
                }

                var scale = Math.Min(
                    (double)CarouselMaxWidth / sourceBitmap.Width,
                    (double)CarouselMaxHeight / sourceBitmap.Height);

                int targetWidth = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale));
                int targetHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));

                using var resizedBitmap = sourceBitmap.Resize(
                    new SKImageInfo(targetWidth, targetHeight),
                    SKFilterQuality.Medium);

                if (resizedBitmap == null)
                    return null;

                using var image = SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
                return data?.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private void StartZoomOut(Image img, int slot)
        {
            StopZoom(slot);
            _zoomCts[slot] = new System.Threading.CancellationTokenSource();
            var cts = _zoomCts[slot]!;

            img.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            var scale = new ScaleTransform(1.15, 1.15);
            img.RenderTransform = scale;

            const double startScale = 1.15;
            const double endScale   = 1.0;
            var totalMs = 6000.0;
            var startTime = DateTime.UtcNow;

            var zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            zoomTimer.Tick += (_, _) =>
            {
                if (cts.IsCancellationRequested) { zoomTimer.Stop(); return; }
                var t = Math.Min((DateTime.UtcNow - startTime).TotalMilliseconds / totalMs, 1.0);
                var s = startScale + (endScale - startScale) * t;
                scale.ScaleX = s;
                scale.ScaleY = s;
                if (t >= 1.0) zoomTimer.Stop();
            };
            zoomTimer.Start();
        }

        private void StopZoom(int slot)
        {
            if (slot < 0 || slot >= _zoomCts.Count)
                return;

            _zoomCts[slot]?.Cancel();
            _zoomCts[slot] = null;
        }

        // â”€â”€ Server dropdown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void ToggleServerDropdown(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vmOffline && vmOffline.IsOfflineMode)
            {
                CloseDropdown();
                return;
            }

            _dropdownOpen = !_dropdownOpen;

            if (DataContext is MainWindowViewModel vm)
                vm.IsDropdownOpen = _dropdownOpen;

            if (_dropdownOpen)
            {
                ServerListPanel.MaxHeight = 270;
            }
            else
            {
                ServerListPanel.MaxHeight = 0;
            }
        }

        private void ServerItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerInfo server &&
                DataContext is MainWindowViewModel vm)
            {
                vm.SelectedServer = server.IsNone ? null : server;
            }
            CloseDropdown();
        }

        private void CloseDropdown()
        {
            _dropdownOpen = false;
            if (DataContext is MainWindowViewModel vm)
                vm.IsDropdownOpen = false;

            ServerListPanel.MaxHeight = 0;
        }

        // â”€â”€ Game launch â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void LaunchUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;
            _settings = SettingsWindowViewModel.LoadGlobal();

            if (vm.IsCheckingUpdates || vm.IsUpdating || vm.IsInstalling || Volatile.Read(ref _launchInProgress) == 1)
                return;
            else if (vm.IsNeedingInstall)
                _ = InstallGameFromCdnAsync();
            else if (_settings.SkipUpdates)
                _ = LaunchGameAsync();
            else if (vm.UpdateAvailable)
            {
                if (_selfUpdateAvailable)
                    _ = Button_SelfUpdateAsync();
                else
                    Button_Update(sender, e);
            }
            else
                _ = LaunchGameAsync();
        }

        private async Task LaunchGameAsync()
        {
            if (Interlocked.Exchange(ref _launchInProgress, 1) == 1)
                return;

            var vm = DataContext as MainWindowViewModel;
            try
            {
                _settings = SettingsWindowViewModel.LoadGlobal();

                if (vm != null) vm.GameStatus = "Running";

                // Clear any arguments left over from a previous launch before adding new ones.
                Argument.ClearAdditionalArguments();

                Argument.AddArgument("-novid");
                if (!string.IsNullOrWhiteSpace(_settings.LaunchOptions))
                {
                    foreach (var arg in ParseLaunchOptions(_settings.LaunchOptions))
                        Argument.AddArgument(arg);
                }

                var selected = vm?.SelectedServer;
                if (selected != null && !selected.IsNone && !string.IsNullOrEmpty(selected.IpPort))
                {
                    Argument.AddArgument("+connect");
                    Argument.AddArgument(selected.IpPort);
                }

                await Game.Launch();

                if (_settings.MinimizeToTray) Hide();

                if (_settings.DiscordRpc)
                {
                    Discord.SetDetails((selected != null && !selected.IsNone)
                        ? $"Playing on {selected.Name}" : "In Main Menu");
                    Discord.Update();
                }

                await Game.Monitor();
            }
            catch (Exception ex)
            {
                Wauncher.Utils.ConsoleManager.ShowError($"Failed to launch game:\n{ex.Message}");
            }
            finally
            {
                if (vm != null) vm.GameStatus = "Not Running";

                if (!_forceClose && _settings.MinimizeToTray && !IsVisible)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    });
                }

                Interlocked.Exchange(ref _launchInProgress, 0);
            }
        }

        // â”€â”€ Window chrome â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private bool _forceClose = false;

        private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _forceClose = true;
            TeardownCarousel();
            Close();
        }

        public void ForceQuit()
        {
            _forceClose = true;
            TeardownCarousel();
            Close();
        }

        private bool IsGameRunning()
        {
            return DataContext is MainWindowViewModel vm &&
                   string.Equals(vm.GameStatus, "Running", StringComparison.Ordinal);
        }

        private void OpenGameFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dir = WauncherDirectory;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }

        private void VerifyGameFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            if (vm.IsCheckingUpdates || vm.IsUpdating || vm.IsInstalling || vm.IsNeedingInstall)
                return;

            _forceValidateAllOnce = true;
            _cachedPatches = null;
            Button_Update(sender, e);
        }

        // â”€â”€ Update â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private CancellationTokenSource? _updateCts;
        private Patches? _cachedPatches;
        private bool _selfUpdateAvailable;
        private string _selfUpdateDownloadUrl = string.Empty;
        private string _selfUpdateVersion = string.Empty;
        private int _autoSelfUpdateTriggered;
        private int _protocolLaunchTriggered;
        private bool _forceValidateAllOnce;

        /// <summary>
        /// Called on window open. If csgo.exe is missing, triggers a full CDN install.
        /// Otherwise runs the normal patch update check.
        /// </summary>
        private async Task StartupAsync()
        {
            // Yield to let Avalonia finish its initial layout/styling pass
            // (Loaded sets the button disabled/gray; we need that to settle before overriding)
            await Task.Delay(50);

            LaunchUpdateButton.IsEnabled = true;

            string csgoExe = Path.Combine(WauncherDirectory, "csgo.exe");
            if (DataContext is not MainWindowViewModel vm)
                return;

            if (!File.Exists(csgoExe))
            {
                vm.IsNeedingInstall           = true;
                var blue                      = new SolidColorBrush(Color.Parse("#2196F3"));
                LaunchUpdateButton.Background = blue;
                ArrowButton.Background        = blue;
                LaunchButtonGlow.BoxShadow    = BoxShadows.Parse("0 0 18 2 #552196F3");
                LaunchUpdateButton.IsEnabled  = true;
                return;
            }

            if (await TryHandleProtocolLaunchAsync(vm))
                return;

            if (vm?.IsOfflineMode == true)
            {
                vm.IsNeedingInstall           = false;
                vm.UpdateAvailable            = false;
                var green                     = new SolidColorBrush(Color.Parse("#4CAF50"));
                LaunchUpdateButton.Background = green;
                ArrowButton.Background        = green;
                LaunchButtonGlow.BoxShadow    = BoxShadows.Parse("0 0 18 2 #554CAF50");
                LaunchUpdateButton.IsEnabled  = true;
                return;
            }

            _settings = SettingsWindowViewModel.LoadGlobal();
            if (_settings.SkipUpdates)
            {
                vm!.IsNeedingInstall          = false;
                vm.UpdateAvailable            = false;
                vm.IsCheckingUpdates          = false;
                var green                     = new SolidColorBrush(Color.Parse("#4CAF50"));
                LaunchUpdateButton.Background = green;
                ArrowButton.Background        = green;
                LaunchButtonGlow.BoxShadow    = BoxShadows.Parse("0 0 18 2 #554CAF50");
                LaunchUpdateButton.IsEnabled  = true;
                return;
            }

            await CheckForUpdatesAsync();
        }

        private async Task<bool> TryHandleProtocolLaunchAsync(MainWindowViewModel vm)
        {
            var protocolTarget = Argument.GetProtocolConnectTarget();
            if (string.IsNullOrWhiteSpace(protocolTarget))
                return false;

            if (Interlocked.Exchange(ref _protocolLaunchTriggered, 1) == 1)
                return true;

            try
            {
                var matchedServer = vm.Servers.FirstOrDefault(s =>
                    !s.IsNone &&
                    string.Equals(s.IpPort, protocolTarget, StringComparison.OrdinalIgnoreCase));

                if (matchedServer != null)
                {
                    vm.SelectedServer = matchedServer;
                    Argument.ConsumeProtocolConnectTarget();
                }

                await LaunchGameAsync();
                return true;
            }
            catch
            {
                Interlocked.Exchange(ref _protocolLaunchTriggered, 0);
                throw;
            }
        }

        private async Task InstallGameFromCdnAsync()
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (Interlocked.Exchange(ref _installInProgress, 1) == 1)
                return;

            bool installSucceeded = false;
            vm.IsNeedingInstall    = false;
            vm.IsInstalling        = true;
            vm.UpdateProgress      = 0;
            vm.UpdateIndeterminate = true;
            vm.UpdateStatusFile    = "Connecting...";
            vm.UpdateStatusSpeed   = "";

            try
            {
                await DownloadManager.InstallFullGame(
                    onProgress: (file, speed, percent) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            vm.UpdateStatusFile  = $"Installing {ShortFileName(file)}  {percent:F0}%";
                            vm.UpdateStatusSpeed = string.IsNullOrWhiteSpace(speed) ? "" : speed;
                            vm.UpdateProgress    = percent;
                            vm.UpdateIndeterminate = false;
                        });
                    },
                    onStatus: status =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            vm.UpdateStatusFile    = status;
                            vm.UpdateStatusSpeed   = "";
                            vm.UpdateIndeterminate = !status.Contains("Extracting", StringComparison.OrdinalIgnoreCase);
                            if (!vm.UpdateIndeterminate)
                                vm.UpdateProgress = 0;
                        });
                    },
                    onExtractProgress: extractPercent =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            vm.UpdateIndeterminate = false;
                            vm.UpdateStatusFile    = $"Extracting game files... {extractPercent:F0}%";
                            vm.UpdateStatusSpeed   = "";
                            vm.UpdateProgress      = extractPercent;
                        });
                    });

                // Immediately apply any post-install patches so first-time installs
                // end in a launch-ready state without requiring a second manual update.
                Dispatcher.UIThread.Post(() =>
                {
                    vm.UpdateProgress      = 0;
                    vm.UpdateIndeterminate = true;
                    vm.UpdateStatusFile    = "Checking post-install patches...";
                    vm.UpdateStatusSpeed   = "";
                });

                var patches = await Task.Run(() => PatchManager.ValidatePatches());
                var allPatches = patches.Missing.Concat(patches.Outdated).ToList();
                if (allPatches.Count > 0)
                {
                    int totalFiles = allPatches.Count;
                    int completed  = 0;

                    foreach (var patch in allPatches)
                    {
                        var extractWatch = new System.Diagnostics.Stopwatch();
                        await DownloadManager.DownloadPatch(
                            patch,
                            onProgress: (p) =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    vm.UpdateIndeterminate = false;
                                    vm.UpdateStatusFile  = $"Installing {ShortFileName(patch.File)}  {p.ProgressPercentage:F0}%";
                                    vm.UpdateStatusSpeed = FormatDownloadSpeedAndEta(p);
                                    vm.UpdateProgress    = ((completed + p.ProgressPercentage / 100.0) / totalFiles) * 100.0;
                                });
                            },
                            onExtract: () =>
                            {
                                extractWatch.Restart();
                                Dispatcher.UIThread.Post(() =>
                                {
                                    vm.UpdateIndeterminate = false;
                                    vm.UpdateStatusFile  = $"Extracting {ShortFileName(patch.File)}... 0%";
                                    vm.UpdateStatusSpeed = "";
                                    vm.UpdateProgress    = ((double)completed / totalFiles) * 100.0;
                                });
                            },
                            onExtractProgress: extractPercent =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    vm.UpdateIndeterminate = false;
                                    vm.UpdateStatusFile  = $"Extracting {ShortFileName(patch.File)}... {extractPercent:F0}%";
                                    vm.UpdateStatusSpeed = FormatExtractEta(extractWatch, extractPercent);
                                    vm.UpdateProgress    = ((completed + extractPercent / 100.0) / totalFiles) * 100.0;
                                });
                            });

                        completed++;
                        vm.UpdateProgress = (double)completed / totalFiles * 100.0;
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    vm.UpdateStatusFile    = "Game installed and fully updated!";
                    vm.UpdateStatusSpeed   = "";
                    vm.UpdateIndeterminate = false;
                    vm.UpdateProgress      = 100;
                });
                installSucceeded = true;
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                vm.UpdateStatusFile  = $"Install error: {ex.Message}";
                vm.UpdateStatusSpeed = "";
                await Task.Delay(4000);
            }
            finally
            {
                vm.IsInstalling        = false;
                vm.UpdateProgress      = 0;
                vm.UpdateIndeterminate = false;
                vm.UpdateStatusFile    = "";
                vm.UpdateStatusSpeed   = "";

                _cachedPatches = null;

                if (!installSucceeded && !File.Exists(Path.Combine(WauncherDirectory, "csgo.exe")))
                {
                    vm.IsNeedingInstall           = true;
                    var blue                      = new SolidColorBrush(Color.Parse("#2196F3"));
                    LaunchUpdateButton.Background = blue;
                    ArrowButton.Background        = blue;
                    LaunchButtonGlow.BoxShadow    = BoxShadows.Parse("0 0 18 2 #552196F3");
                }
                else
                {
                    try
                    {
                        await CheckForUpdatesAsync();
                    }
                    catch { }
                }

                LaunchUpdateButton.IsEnabled  = true;
                Interlocked.Exchange(ref _installInProgress, 0);
            }
        }

        private async Task LoadPatchNotesAsync()
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    PatchNotesVersion.IsVisible = false;
                });

                if (DataContext is MainWindowViewModel vm && vm.IsOfflineMode)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var cachedItems = LoadCachedPatchNotes();
                        if (cachedItems.Count > 0)
                        {
                            PatchNotesList.ItemsSource = cachedItems;
                        }
                        else
                        {
                            PatchNotesList.ItemsSource = new List<ViewModels.PatchNoteItem>();
                        }

                        PatchNotesVersion.IsVisible = false;
                        PatchNotesScroll.Offset = new Vector(0, 0);
                    });
                    return;
                }

                var md = await Api.GitHub.GetPatchNotesWauncher();
                var items = ParsePatchNotes(md);
                SavePatchNotesCache(md);
                Dispatcher.UIThread.Post(() =>
                {
                    PatchNotesVersion.IsVisible = false;
                    PatchNotesList.ItemsSource = items;
                    PatchNotesScroll.Offset = new Vector(0, 0);
                });
            }
            catch
            {
                var items = LoadCachedPatchNotes();
                if (items.Count == 0)
                {
                    items = BuildFallbackPatchNotes();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    PatchNotesVersion.IsVisible = false;
                    PatchNotesList.ItemsSource = items;
                    PatchNotesScroll.Offset = new Vector(0, 0);
                });
            }
        }

        private static void SavePatchNotesCache(string markdown)
        {
            try
            {
                var directory = Path.GetDirectoryName(PatchNotesCachePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(PatchNotesCachePath, markdown);
            }
            catch
            {
                // Caching is best-effort; keep patch notes functional if disk write fails.
            }
        }

        private static List<ViewModels.PatchNoteItem> LoadCachedPatchNotes()
        {
            try
            {
                if (!File.Exists(PatchNotesCachePath))
                {
                    return new List<ViewModels.PatchNoteItem>();
                }

                var markdown = File.ReadAllText(PatchNotesCachePath);
                return ParsePatchNotes(markdown);
            }
            catch
            {
                return new List<ViewModels.PatchNoteItem>();
            }
        }

        private static List<ViewModels.PatchNoteItem> BuildFallbackPatchNotes()
        {
            return new List<ViewModels.PatchNoteItem>
            {
                new() { Text = "Anniversary Update", IsMajorHeader = true },
                new() { Text = "March 12, 2026", IsDateHeader = true },
                new() { Text = "What''s Changed", IsHeader = true },
                new() { Text = "Donors now permanently get an extra drop at the end of each match.", IsBullet = true },
                new() { Text = "NOVAGANG Collection drops have been reverted back to normal rates.", IsBullet = true },
                new() { Text = "Bug fixes and security improvements.", IsBullet = true },
            };
        }

        private static List<ViewModels.PatchNoteItem> ParsePatchNotes(string markdown)
        {
            var items = new List<ViewModels.PatchNoteItem>();
            bool lastWasMajorHeader = false;

            foreach (var raw in markdown.Split('\n'))
            {
                var line = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) continue;

                line = line.Trim();
                line = line.Replace("**", "").Replace("__", "");
                line = Regex.Replace(line, @"\[(.*?)\]\((.*?)\)", "$1");
                line = Regex.Replace(line, @"`([^`]*)`", "$1");

                if (line.StartsWith("# "))
                {
                    var headerText = line.TrimStart('#', ' ');
                    var (title, dateText) = SplitPatchTitleAndDate(headerText);

                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = title,
                        IsMajorHeader = true
                    });

                    if (!string.IsNullOrWhiteSpace(dateText))
                    {
                        items.Add(new ViewModels.PatchNoteItem
                        {
                            Text = dateText,
                            IsDateHeader = true
                        });
                    }

                    lastWasMajorHeader = true;
                }
                else if (lastWasMajorHeader && TryParsePatchDateLine(line, out var parsedDate))
                {
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = parsedDate,
                        IsDateHeader = true
                    });
                    lastWasMajorHeader = false;
                }
                else if (line.StartsWith("## ") || line.StartsWith("### "))
                {
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = line.TrimStart('#', ' '),
                        IsHeader = true
                    });
                    lastWasMajorHeader = false;
                }
                else if (line.StartsWith("* ") || line.StartsWith("- ") || line.StartsWith("• "))
                {
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = line.Substring(2).Trim(),
                        IsBullet = true
                    });
                    lastWasMajorHeader = false;
                }
                else if (Regex.IsMatch(line, @"^\d+\.\s+"))
                {
                    var bulletText = Regex.Replace(line, @"^\d+\.\s+", string.Empty).Trim();
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = bulletText,
                        IsBullet = true
                    });
                    lastWasMajorHeader = false;
                }
                else if (line.StartsWith("**") && line.EndsWith("**"))
                {
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = line.Trim('*', ' '),
                        IsHeader = true
                    });
                    lastWasMajorHeader = false;
                }
                else
                {
                    items.Add(new ViewModels.PatchNoteItem
                    {
                        Text = line.TrimStart('#', '*', '-', ' '),
                        IsBullet = true
                    });
                    lastWasMajorHeader = false;
                }
            }

            return items;
        }

        private static (string Title, string DateText) SplitPatchTitleAndDate(string headerText)
        {
            var match = Regex.Match(
                headerText,
                @"^(?<title>.+?)\s+(?:-|–|—)\s+(?<date>(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},\s+\d{4})$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return (headerText, string.Empty);

            return (match.Groups["title"].Value.Trim(), match.Groups["date"].Value.Trim());
        }

        private static bool TryParsePatchDateLine(string line, out string dateText)
        {
            dateText = string.Empty;
            var trimmed = line.Trim();

            if (trimmed.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                dateText = trimmed[5..].Trim();
                return !string.IsNullOrWhiteSpace(dateText);
            }

            if (Regex.IsMatch(trimmed, @"^(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},\s+\d{4}$", RegexOptions.IgnoreCase))
            {
                dateText = trimmed;
                return true;
            }

            if (Regex.IsMatch(trimmed, @"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.IgnoreCase))
            {
                dateText = trimmed;
                return true;
            }

            return false;
        }

        private sealed class GitHubRelease
        {
            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("assets")]
            public List<GitHubReleaseAsset>? Assets { get; set; }
        }

        private sealed class GitHubReleaseAsset
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("browser_download_url")]
            public string DownloadUrl { get; set; } = string.Empty;
        }

        private static string NormalizeVersionToken(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "0.0.0";

            var cleaned = version.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[1..];

            cleaned = Regex.Replace(cleaned, @"[^0-9\.]", string.Empty);
            return string.IsNullOrWhiteSpace(cleaned) ? "0.0.0" : cleaned;
        }

        private static bool TryParseVersion(string value, out global::System.Version parsed)
        {
            if (global::System.Version.TryParse(value, out parsed!))
                return true;

            var tokens = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                parsed = new global::System.Version(0, 0, 0);
                return false;
            }

            while (tokens.Length < 3)
                tokens = tokens.Append("0").ToArray();

            return global::System.Version.TryParse(string.Join('.', tokens.Take(4)), out parsed!);
        }

        private async Task<bool> CheckForSelfUpdateAsync()
        {
            _selfUpdateAvailable = false;
            _selfUpdateDownloadUrl = string.Empty;
            _selfUpdateVersion = string.Empty;

            try
            {
                var latestReleaseJson = await Api.GitHub.GetLatestRelease();
                var release = JsonConvert.DeserializeObject<GitHubRelease>(latestReleaseJson);
                if (release == null)
                    return false;

                var currentVersion = NormalizeVersionToken(Wauncher.Utils.Version.Current);
                var latestVersion = NormalizeVersionToken(release.TagName);
                if (!TryParseVersion(currentVersion, out var current) || !TryParseVersion(latestVersion, out var latest))
                    return false;

                if (latest <= current)
                    return false;

                var assets = release.Assets ?? new List<GitHubReleaseAsset>();
                var preferred = assets.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.DownloadUrl) &&
                    string.Equals(a.Name, "wauncher.exe", StringComparison.OrdinalIgnoreCase));

                preferred ??= assets.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.DownloadUrl) &&
                    a.Name.Contains("wauncher", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (preferred == null)
                    return false;

                _selfUpdateAvailable = true;
                _selfUpdateDownloadUrl = preferred.DownloadUrl;
                _selfUpdateVersion = latestVersion;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadFileWithProgressAsync(string url, string destination, Action<double>? onProgress, CancellationToken token)
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0)
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read), token);
                received += read;
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    onProgress?.Invoke((double)received / totalBytes.Value * 100.0);
                }
            }

            onProgress?.Invoke(100.0);
        }

        private static string BuildSelfUpdateScript(string stagedExePath, string currentExePath)
        {
            return
$@"@echo off
setlocal
set ""SRC={stagedExePath}""
set ""DST={currentExePath}""

for /L %%i in (1,1,60) do (
  copy /Y ""%SRC%"" ""%DST%"" >nul 2>nul && goto copied
  timeout /t 1 /nobreak >nul
)

exit /b 1

:copied
start """" ""%DST%""
del /Q ""%SRC%"" >nul 2>nul
del /Q ""%~f0"" >nul 2>nul
exit /b 0
";
        }

        private async Task Button_SelfUpdateAsync()
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null)
                return;

            if (Interlocked.Exchange(ref _updateInProgress, 1) == 1)
                return;

            _updateCts?.Dispose();
            _updateCts = new CancellationTokenSource();
            var token = _updateCts.Token;

            vm.IsUpdating = true;
            vm.UpdateAvailable = false;
            vm.UpdateProgress = 0;
            vm.UpdateIndeterminate = true;
            vm.UpdateStatus = "";
            vm.UpdateStatusFile = "Downloading Wauncher update...";
            vm.UpdateStatusSpeed = "";

            try
            {
                if (string.IsNullOrWhiteSpace(_selfUpdateDownloadUrl))
                    throw new Exception("No self-update package URL found.");

                var updatesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClassicCounter",
                    "Wauncher",
                    "self-update");
                Directory.CreateDirectory(updatesDir);

                var safeVersion = Regex.Replace(_selfUpdateVersion, @"[^0-9A-Za-z\.\-_]", string.Empty);
                if (string.IsNullOrWhiteSpace(safeVersion))
                    safeVersion = "latest";

                var stagedExePath = Path.Combine(updatesDir, $"wauncher_{safeVersion}.exe");
                await DownloadFileWithProgressAsync(_selfUpdateDownloadUrl, stagedExePath, percent =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        vm.UpdateIndeterminate = false;
                        vm.UpdateProgress = percent;
                        vm.UpdateStatusFile = $"Downloading Wauncher update... {percent:F0}%";
                    });
                }, token);

                var currentExePath = Services.GetExePath();
                if (string.IsNullOrWhiteSpace(currentExePath))
                    throw new Exception("Could not locate current Wauncher executable.");

                var scriptPath = Path.Combine(updatesDir, "apply_wauncher_update.cmd");
                var script = BuildSelfUpdateScript(stagedExePath, currentExePath);
                File.WriteAllText(scriptPath, script, Encoding.ASCII);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    WorkingDirectory = updatesDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });

                vm.UpdateStatusFile = "Restarting Wauncher to apply update...";
                vm.UpdateStatusSpeed = "";
                vm.UpdateProgress = 100;
                await Task.Delay(500, token);
                ForceQuit();
            }
            catch (OperationCanceledException)
            {
                vm.UpdateStatusFile = "Update cancelled.";
                vm.UpdateStatusSpeed = "";
                await Task.Delay(800);
            }
            catch (Exception ex)
            {
                vm.UpdateStatusFile = $"Self-update failed: {ex.Message}";
                vm.UpdateStatusSpeed = "";
                vm.UpdateIndeterminate = false;
                await Task.Delay(2500);
            }
            finally
            {
                if (!_forceClose)
                {
                    vm.IsUpdating = false;
                    vm.UpdateProgress = 0;
                    vm.UpdateIndeterminate = false;
                    vm.UpdateStatus = "";
                    vm.UpdateStatusFile = "";
                    vm.UpdateStatusSpeed = "";
                    _updateCts?.Dispose();
                    _updateCts = null;

                    try
                    {
                        await CheckForUpdatesAsync();
                    }
                    catch
                    {
                        // keep UI responsive even if refresh fails
                    }
                }

                Interlocked.Exchange(ref _updateInProgress, 0);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            if (DataContext is not MainWindowViewModel vm) return;
            _settings = SettingsWindowViewModel.LoadGlobal();

            if (vm.IsOfflineMode)
            {
                _selfUpdateAvailable = false;
                _selfUpdateDownloadUrl = string.Empty;
                _selfUpdateVersion = string.Empty;
                _cachedPatches = null;
                vm.UpdateAvailable = false;
                vm.IsCheckingUpdates = false;
                var launchColor = new SolidColorBrush(Color.Parse("#4CAF50"));
                LaunchUpdateButton.Background = launchColor;
                ArrowButton.Background        = launchColor;
                LaunchUpdateButton.IsEnabled  = true;
                return;
            }

            vm.IsCheckingUpdates          = true;
            LaunchUpdateButton.Background = new SolidColorBrush(Color.Parse("#555555"));
            ArrowButton.Background        = new SolidColorBrush(Color.Parse("#555555"));
            LaunchUpdateButton.IsEnabled  = false;
            try
            {
                bool hasSelfUpdate = await CheckForSelfUpdateAsync();
                if (hasSelfUpdate)
                {
                    _cachedPatches = null;
                    vm.UpdateAvailable = true;
                    var selfUpdateColor = new SolidColorBrush(Color.Parse("#FFC107"));
                    LaunchUpdateButton.Background = selfUpdateColor;
                    ArrowButton.Background        = selfUpdateColor;

                    if (Interlocked.Exchange(ref _autoSelfUpdateTriggered, 1) == 0)
                    {
                        _ = Button_SelfUpdateAsync();
                    }

                    return;
                }

                if (_settings.SkipUpdates)
                {
                    _cachedPatches = null;
                    vm.UpdateAvailable = false;
                    var launchColor = new SolidColorBrush(Color.Parse("#4CAF50"));
                    LaunchUpdateButton.Background = launchColor;
                    ArrowButton.Background        = launchColor;
                    return;
                }

                var patches     = await Task.Run(() => PatchManager.ValidatePatches(deleteOutdatedFiles: false));
                bool hasUpdates = patches.Missing.Count > 0 || patches.Outdated.Count > 0;

                // Cache the result so Button_Update can consume it without re-validating.
                _cachedPatches             = patches;
                _selfUpdateAvailable       = false;
                _selfUpdateDownloadUrl     = string.Empty;
                _selfUpdateVersion         = string.Empty;
                vm.UpdateAvailable         = hasUpdates;
                var buttonColor = new SolidColorBrush(
                    Color.Parse(hasUpdates ? "#FFC107" : "#4CAF50"));
                LaunchUpdateButton.Background = buttonColor;
                ArrowButton.Background        = buttonColor;
            }
            catch
            {
                var defaultColor = new SolidColorBrush(Color.Parse("#4CAF50"));
                LaunchUpdateButton.Background = defaultColor;
                ArrowButton.Background        = defaultColor;
            }
            finally
            {
                vm.IsCheckingUpdates         = false;
                LaunchUpdateButton.IsEnabled = true;
            }
        }

        private async void Button_Update(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            if (Interlocked.Exchange(ref _updateInProgress, 1) == 1)
                return;

            _updateCts?.Dispose();
            _updateCts             = new CancellationTokenSource();
            var token              = _updateCts.Token;
            vm.IsUpdating          = true;
            vm.UpdateAvailable     = false;
            vm.UpdateProgress      = 0;
            vm.UpdateIndeterminate = false;
            vm.UpdateStatus        = "";

            try
            {
                // Use the result already computed by CheckForUpdatesAsync when available,
                // to avoid a redundant full validation on every update click.
                bool validateAll = _forceValidateAllOnce;
                _forceValidateAllOnce = false;
                bool usingCachedPatches = _cachedPatches != null;

                if (!usingCachedPatches)
                {
                    vm.UpdateIndeterminate = true;
                    vm.UpdateStatusFile = validateAll
                        ? "Verifying all game files..."
                        : "Checking game files...";
                    vm.UpdateStatusSpeed = "";
                    vm.UpdateProgress = 0;
                }

                var patches = _cachedPatches ?? await Task.Run(() => PatchManager.ValidatePatches(validateAll: validateAll), token);
                _cachedPatches = null; // consumed â€” force fresh check next time
                if (token.IsCancellationRequested) return;

                bool hasPatches = patches.Missing.Count > 0 || patches.Outdated.Count > 0;
                if (!hasPatches)
                {
                    vm.UpdateStatusFile  = "Game is up to date!";
                    vm.UpdateStatusSpeed = "";
                    vm.UpdateProgress    = 100;
                    await Task.Delay(2000);
                    return;
                }

                var allPatches = patches.Missing.Concat(patches.Outdated).ToList();
                int totalFiles = allPatches.Count;
                int completed  = 0;

                foreach (var patch in allPatches)
                {
                    if (token.IsCancellationRequested) break;

                    var extractWatch = new System.Diagnostics.Stopwatch();
                    await DownloadManager.DownloadPatch(
                        patch,
                        onProgress: (p) =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                vm.UpdateIndeterminate = false;
                                vm.UpdateStatusFile  = $"Installing {ShortFileName(patch.File)}  {p.ProgressPercentage:F0}%";
                                vm.UpdateStatusSpeed = FormatDownloadSpeedAndEta(p);
                                vm.UpdateProgress    = ((completed + p.ProgressPercentage / 100.0) / totalFiles) * 100.0;
                            });
                        },
                        onExtract: () =>
                        {
                            extractWatch.Restart();
                            Dispatcher.UIThread.Post(() =>
                            {
                                vm.UpdateIndeterminate = false;
                                vm.UpdateStatusFile  = $"Extracting {ShortFileName(patch.File)}... 0%";
                                vm.UpdateStatusSpeed = "";
                                vm.UpdateProgress    = ((double)completed / totalFiles) * 100.0;
                            });
                        },
                        onExtractProgress: extractPercent =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                vm.UpdateIndeterminate = false;
                                vm.UpdateStatusFile  = $"Extracting {ShortFileName(patch.File)}... {extractPercent:F0}%";
                                vm.UpdateStatusSpeed = FormatExtractEta(extractWatch, extractPercent);
                                vm.UpdateProgress    = ((completed + extractPercent / 100.0) / totalFiles) * 100.0;
                            });
                        });

                    completed++;
                    vm.UpdateProgress = (double)completed / totalFiles * 100.0;
                }

                if (!token.IsCancellationRequested)
                {
                    vm.UpdateStatusFile  = "Update complete!";
                    vm.UpdateStatusSpeed = "";
                    vm.UpdateProgress    = 100;
                    await Task.Delay(2000);
                }
            }
            catch (OperationCanceledException)
            {
                vm.UpdateStatusFile  = "Update cancelled.";
                vm.UpdateStatusSpeed = "";
                await Task.Delay(800);
            }
            catch (Exception ex)
            {
                vm.UpdateStatusFile    = $"Error: {ex.Message}";
                vm.UpdateStatusSpeed   = "";
                vm.UpdateIndeterminate = false;
                await Task.Delay(3000);
            }
            finally
            {
                vm.IsUpdating          = false;
                vm.UpdateProgress      = 0;
                vm.UpdateIndeterminate = false;
                vm.UpdateStatus        = "";
                vm.UpdateStatusFile    = "";
                vm.UpdateStatusSpeed   = "";
                _cachedPatches         = null;
                _updateCts?.Dispose();
                _updateCts             = null;

                try
                {
                    await CheckForUpdatesAsync();
                }
                catch { }

                Interlocked.Exchange(ref _updateInProgress, 0);
            }
        }

        private void Button_CancelUpdate(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _updateCts?.Cancel();
        }

        // â”€â”€ Settings / Info windows â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void FriendsTab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) vm.ActiveRightTab = "Friends";
        }

        private void PatchNotesTab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) vm.ActiveRightTab = "PatchNotes";
            PatchNotesScroll.Offset = new Vector(0, 0);
        }

        private void ViewFriendProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: FriendInfo friend })
                return;

            var profileId = ResolveProfileSteamId(friend.SteamId);
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://eddies.cc/profiles/{profileId}",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Best-effort open.
            }
        }

        private async void JoinFriendServer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: FriendInfo friend })
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            if (string.IsNullOrWhiteSpace(friend.QuickJoinIpPort))
                return;

            var matchedServer = vm.Servers.FirstOrDefault(s =>
                !s.IsNone &&
                string.Equals(s.IpPort, friend.QuickJoinIpPort, StringComparison.OrdinalIgnoreCase));

            if (matchedServer == null)
            {
                Wauncher.Utils.ConsoleManager.ShowError("Couldn't match that friend to a server in your server list.");
                return;
            }

            vm.SelectedServer = matchedServer;
            await LaunchGameAsync();
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

        private void Button_Settings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                var skipUpdatesBeforeOpen = _settings.SkipUpdates;
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, e) =>
                {
                    _settingsWindow = null;
                    _settings = SettingsWindowViewModel.LoadGlobal();
                    if (skipUpdatesBeforeOpen != _settings.SkipUpdates)
                        _ = CheckForUpdatesAsync();
                };
                _settingsWindow.Show(this);
            }
            else _settingsWindow.Activate();
        }

        private void Button_Info(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_infoWindow == null)
            {
                _infoWindow = new InfoWindow();
                _infoWindow.Closed += (s, e) => _infoWindow = null;
                _infoWindow.Show(this);
            }
            else _infoWindow.Activate();
        }

        // â”€â”€ Launch button glow + color â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private void SetLaunchGlow(bool updating)
        {
            var brush = new SolidColorBrush(Color.Parse(updating ? "#FFC107" : "#4CAF50"));
            LaunchUpdateButton.Background = brush;
            ArrowButton.Background        = brush;
            LaunchButtonGlow.BoxShadow    = updating
                ? BoxShadows.Parse("0 0 18 2 #55FFC107")
                : BoxShadows.Parse("0 0 18 2 #554CAF50");
        }

        private static string ShortFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var normalized = path.Replace('\\', '/');
            if (normalized.Length <= 42)
                return normalized;

            var fileName = Path.GetFileName(normalized);
            if (fileName.Length <= 30)
                return fileName;

            return fileName[..27] + "...";
        }

        private static string FormatDownloadSpeedAndEta(object progressArgs)
        {
            double speedBytes = 0;
            if (TryGetDoubleProperty(progressArgs, "AverageBytesPerSecondSpeed", out var avg) && avg > 0)
                speedBytes = avg;
            else if (TryGetDoubleProperty(progressArgs, "BytesPerSecondSpeed", out var cur) && cur > 0)
                speedBytes = cur;

            var speedText = speedBytes > 0
                ? $"{speedBytes / 1024.0 / 1024.0:F1} MB/s"
                : "";

            if (speedBytes <= 0 ||
                !TryGetLongProperty(progressArgs, "TotalBytesToReceive", out var totalBytes) ||
                !TryGetLongProperty(progressArgs, "ReceivedBytesSize", out var receivedBytes) ||
                totalBytes <= 0 || receivedBytes < 0 || receivedBytes >= totalBytes)
            {
                return speedText;
            }

            var remainingBytes = totalBytes - receivedBytes;
            var eta = TimeSpan.FromSeconds(remainingBytes / speedBytes);
            var etaText = $"ETA {FormatEta(eta)}";

            return string.IsNullOrEmpty(speedText) ? etaText : $"{speedText} â€¢ {etaText}";
        }

        private static string FormatExtractEta(System.Diagnostics.Stopwatch watch, double percent)
        {
            if (watch == null || !watch.IsRunning || percent <= 1.0)
                return "";

            var elapsed = watch.Elapsed.TotalSeconds;
            var total = elapsed / (percent / 100.0);
            var remaining = Math.Max(0, total - elapsed);
            return $"ETA {FormatEta(TimeSpan.FromSeconds(remaining))}";
        }

        private static string FormatEta(TimeSpan eta)
        {
            if (eta.TotalHours >= 1)
                return eta.ToString(@"hh\:mm\:ss");
            return eta.ToString(@"mm\:ss");
        }

        private static bool TryGetDoubleProperty(object obj, string propertyName, out double value)
        {
            value = 0;
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null) return false;
            var raw = prop.GetValue(obj);
            if (raw == null) return false;
            try
            {
                value = Convert.ToDouble(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetLongProperty(object obj, string propertyName, out long value)
        {
            value = 0;
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null) return false;
            var raw = prop.GetValue(obj);
            if (raw == null) return false;
            try
            {
                value = Convert.ToInt64(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Minimal parser for launch options that supports quoted values.
        private static IEnumerable<string> ParseLaunchOptions(string options)
        {
            if (string.IsNullOrWhiteSpace(options))
                yield break;

            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (var ch in options)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                yield return current.ToString();
        }

    }
}



