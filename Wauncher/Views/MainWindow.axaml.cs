using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.ComponentModel;
using System.Diagnostics;
using SkiaSharp;
using Wauncher.Services;
using Wauncher.ViewModels;
using Wauncher.Utils;

namespace Wauncher.Views
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient Http = HttpClientFactory.Shared;
        private static readonly string CarouselCacheDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "cache",
                "carousel");

        private const int CarouselRotationIntervalSeconds = 5;
        private const int CarouselMaxWidth = 1280;
        private const int CarouselMaxHeight = 720;

        private ICarouselService? _carouselService;
        private readonly List<CancellationTokenSource?> _zoomCts = new();

        private bool _forceClose;
        private bool _isLoaded;
        private int _carouselInitInProgress;
        private Image[] _carouselImages = Array.Empty<Image>();
        private List<string> _carouselImageUrls = new();
        private DispatcherTimer? _carouselTimer;
        private int _currentCarouselIndex;
        private int _currentCarouselSlot;
        private int _carouselRotateInProgress;

        public MainWindow()
        {
            InitializeComponent();
            SettingsWindowViewModel.DisableCarouselChanged += OnDisableCarouselChanged;

            // Initialize services in background to improve startup performance
            _ = Task.Run(() =>
            {
                try
                {
                    ServiceContainer.Initialize();
                    _carouselService = ServiceContainer.GetService<ICarouselService>();

                    var viewModel = new MainWindowViewModel(
                        ServiceContainer.GetService<IDiscordService>(),
                        ServiceContainer.GetService<IGameService>(),
                        _carouselService,
                        ServiceContainer.GetService<IUpdateService>(),
                        ServiceContainer.GetService<IServerService>(),
                        ServiceContainer.GetService<IFriendsService>());

                    Dispatcher.UIThread.Post(() =>
                    {
                        DataContext = viewModel;
                        viewModel.PropertyChanged += ViewModel_PropertyChanged;
                        if (_isLoaded)
                            _ = InitializeCarouselAsync();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ConsoleManager.ShowError($"Failed to initialize services: {ex.Message}");
                    });
                }
            });

            Loaded += (_, _) =>
            {
                _isLoaded = true;
                if (_carouselService != null)
                    _ = InitializeCarouselAsync();
                _ = PatchNotesControl.LoadPatchNotesAsync();

                SettingsPanelControl.CloseRequested += (_, _) =>
                {
                    if (DataContext is MainWindowViewModel vm)
                        vm.IsSettingsPanelOpen = false;
                };

                InfoPanelControl.CloseRequested += (_, _) =>
                {
                    if (DataContext is MainWindowViewModel vm)
                        vm.IsInfoPanelOpen = false;
                };

                var appearancePanelControl = this.FindControl<Controls.AppearancePanel>("AppearancePanelControl");
                if (appearancePanelControl != null)
                {
                    appearancePanelControl.CloseRequested += () =>
                    {
                        if (DataContext is MainWindowViewModel vm)
                            vm.IsAppearancePanelOpen = false;
                    };
                }
            };

            Closing += (_, e) =>
            {
                if (_forceClose)
                    return;

                var settings = SettingsWindowViewModel.LoadGlobal();
                bool shouldHideToTray =
                    settings.MinimizeToTray &&
                    (Game.IsRunning() ||
                     string.Equals((DataContext as MainWindowViewModel)?.GameStatus, "Running", StringComparison.OrdinalIgnoreCase));

                if (shouldHideToTray)
                {
                    e.Cancel = true;
                    Hide();
                    MemoryManager.StartBackgroundCleanup();
                    return;
                }

                MemoryManager.StopBackgroundCleanup();

                try
                {
                    if (Application.Current is App app)
                    {
                        var trayIconField = typeof(App).GetField("_trayIcon",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var trayIcon = trayIconField?.GetValue(app) as Avalonia.Controls.TrayIcon;
                        trayIcon?.Dispose();
                    }
                }
                catch
                {
                }

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    _forceClose = true;
                    Dispatcher.UIThread.Post(() => desktop.Shutdown());
                }
            };

            Closed += (_, _) => _ = CleanupServicesAsync();
        }

        private async Task InitializeCarouselAsync()
        {
            if (Interlocked.Exchange(ref _carouselInitInProgress, 1) == 1)
                return;

            try
            {
                for (int attempt = 0; attempt < 20 && _carouselService == null; attempt++)
                    await Task.Delay(100);

                if (_carouselService == null)
                    return;

                TeardownCarousel();

                var carouselContainer = this.FindControl<Grid>("CarouselContainer");
                var offlinePanel = this.FindControl<Border>("CarouselOfflinePanel");
                var offlineTitle = this.FindControl<TextBlock>("CarouselOfflineTitle");
                var offlineSubText = this.FindControl<TextBlock>("CarouselOfflineSubText");
                if (carouselContainer == null)
                    return;

                var settings = SettingsWindowViewModel.LoadGlobal();
                if (settings.DisableCarousel)
                {
                    if (offlinePanel != null)
                        offlinePanel.IsVisible = true;

                    if (offlineTitle != null)
                        offlineTitle.Text = "Carousel Disabled";

                    if (offlineSubText != null)
                        offlineSubText.Text = "Carousel is turned off in settings.";

                    return;
                }

                bool hasInternet = NetworkInterface.GetIsNetworkAvailable();
                var urls = hasInternet
                    ? await _carouselService.LoadCarouselUrlsFromGitHubAsync()
                    : null;

                if (urls == null || urls.Count == 0)
                {
                    if (offlinePanel != null)
                        offlinePanel.IsVisible = true;

                    if (offlineTitle != null)
                        offlineTitle.Text = "No internet connection";

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
                System.Diagnostics.Debug.WriteLine("Carousel init failed: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _carouselInitInProgress, 0);
            }
        }

        private async Task CleanupServicesAsync()
        {
            SettingsWindowViewModel.DisableCarouselChanged -= OnDisableCarouselChanged;
            TeardownCarousel();

            try
            {
                if (_carouselService != null)
                    await _carouselService.TeardownCarouselAsync();
            }
            catch
            {
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainWindowViewModel vm)
                return;

            if (e.PropertyName != nameof(MainWindowViewModel.GameStatus))
                return;

            var settings = SettingsWindowViewModel.LoadGlobal();

            if (!_forceClose &&
                settings.MinimizeToTray &&
                string.Equals(vm.GameStatus, "Running", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsVisible)
                        Hide();
                });
                MemoryManager.StartBackgroundCleanup();
                return;
            }

            if (!_forceClose &&
                settings.MinimizeToTray &&
                string.Equals(vm.GameStatus, "Not Running", StringComparison.OrdinalIgnoreCase) &&
                !IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                });
                MemoryManager.StopBackgroundCleanup();
                return;
            }

            if (string.Equals(vm.GameStatus, "Not Running", StringComparison.OrdinalIgnoreCase))
                MemoryManager.StopBackgroundCleanup();
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

            for (int i = 0; i < _zoomCts.Count; i++)
                StopZoom(i);

            foreach (var image in _carouselImages)
            {
                if (image.Source is IDisposable disposable)
                    disposable.Dispose();

                image.Source = null;

                if (image.Parent is Panel panel)
                    panel.Children.Remove(image);
            }

            _carouselImageUrls.Clear();
            _carouselImages = Array.Empty<Image>();
            _currentCarouselIndex = 0;
            _currentCarouselSlot = 0;
            Interlocked.Exchange(ref _carouselRotateInProgress, 0);
        }

        private void OnDisableCarouselChanged(bool disabled)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var offlinePanel = this.FindControl<Border>("CarouselOfflinePanel");
                var offlineTitle = this.FindControl<TextBlock>("CarouselOfflineTitle");
                var offlineSubText = this.FindControl<TextBlock>("CarouselOfflineSubText");

                if (disabled)
                {
                    TeardownCarousel();

                    if (offlinePanel != null)
                        offlinePanel.IsVisible = true;

                    if (offlineTitle != null)
                        offlineTitle.Text = "Carousel Disabled";

                    if (offlineSubText != null)
                        offlineSubText.Text = "Carousel is turned off in settings.";

                    return;
                }

                if (offlinePanel != null)
                    offlinePanel.IsVisible = false;

                await InitializeCarouselAsync();
            });
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
                var bytes = cachedBytes ?? await Http.GetByteArrayAsync(url);
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

        private void StartZoomOut(Image image, int slot)
        {
            StopZoom(slot);
            _zoomCts[slot] = new CancellationTokenSource();
            var cts = _zoomCts[slot]!;

            image.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            var scale = new ScaleTransform(1.15, 1.15);
            image.RenderTransform = scale;

            const double startScale = 1.15;
            const double endScale = 1.0;
            const double totalMs = 6000.0;
            var startTime = DateTime.UtcNow;

            var zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            zoomTimer.Tick += (_, _) =>
            {
                if (cts.IsCancellationRequested)
                {
                    zoomTimer.Stop();
                    return;
                }

                var t = Math.Min((DateTime.UtcNow - startTime).TotalMilliseconds / totalMs, 1.0);
                var s = startScale + (endScale - startScale) * t;
                scale.ScaleX = s;
                scale.ScaleY = s;

                if (t >= 1.0)
                    zoomTimer.Stop();
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

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            ForceQuit();
        }

        public void ForceQuit()
        {
            _forceClose = true;

            try
            {
                TeardownCarousel();
            }
            catch
            {
            }

            MemoryManager.StopBackgroundCleanup();

            try
            {
                if (Application.Current is App app)
                {
                    var trayIconField = typeof(App).GetField("_trayIcon",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var trayIcon = trayIconField?.GetValue(app) as Avalonia.Controls.TrayIcon;
                    trayIcon?.Dispose();
                }
            }
            catch
            {
            }

            try
            {
                Close();
            }
            catch
            {
            }

            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            }
            catch
            {
            }

            Environment.Exit(0);

            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
            }
        }
    }
}
