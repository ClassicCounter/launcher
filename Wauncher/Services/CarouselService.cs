using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Newtonsoft.Json;
using SkiaSharp;
using Wauncher.Utils;

namespace Wauncher.Services
{
    public class CarouselService : ICarouselService
    {
        private static readonly HttpClient _http = HttpClientFactory.Shared;
        private static string CarouselCacheDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "cache",
                "carousel");

        private const int CarouselMaxWidth = 1280;
        private const int CarouselMaxHeight = 720;
        private const int MaxCachedImages = 3; // Keep current + adjacent images
        private const int PreloadDistance = 1; // Preload 1 image before/after current
        
        // Lazy loading cache
        private readonly ConcurrentDictionary<string, Bitmap> _imageCache = new();
        private readonly ConcurrentDictionary<string, Task<Bitmap?>> _loadingTasks = new();
        private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);
        
        public bool IsOfflineMode => !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

        public async Task SetupCarouselAsync()
        {
            // This would be implemented with the actual carousel setup logic
            // For now, it's a placeholder
            await Task.CompletedTask;
        }

        public async Task<List<string>?> LoadCarouselUrlsFromGitHubAsync()
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

                return urls.Count == 0 ? null : urls;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CarouselService.LoadCarouselUrlsFromGitHubAsync", ex, "Failed to load carousel URLs from GitHub");
                return null;
            }
        }

        public async Task TeardownCarouselAsync()
        {
            await ClearImageCacheAsync();
            await Task.CompletedTask;
        }
        
        // Lazy loading implementation
        public async Task<Bitmap?> LoadImageAsync(int index, string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
                
            // Check cache first
            if (_imageCache.TryGetValue(url, out var cachedBitmap))
                return cachedBitmap;
                
            // Check if already loading
            var loadingTask = _loadingTasks.GetOrAdd(url, async (key) => await LoadImageInternalAsync(key));
            
            try
            {
                var bitmap = await loadingTask;
                if (bitmap != null)
                {
                    _imageCache.TryAdd(url, bitmap);
                }
                return bitmap;
            }
            finally
            {
                _loadingTasks.TryRemove(url, out _);
            }
        }
        
        public async Task PreloadAdjacentImagesAsync(int currentIndex, List<string> urls)
        {
            if (urls == null || urls.Count == 0)
                return;
                
            var preloadTasks = new List<Task>();
            
            // Preload previous image
            var prevIndex = (currentIndex - 1 + urls.Count) % urls.Count;
            if (prevIndex != currentIndex)
            {
                preloadTasks.Add(LoadImageAsync(prevIndex, urls[prevIndex]));
            }
            
            // Preload next image
            var nextIndex = (currentIndex + 1) % urls.Count;
            if (nextIndex != currentIndex && nextIndex != prevIndex)
            {
                preloadTasks.Add(LoadImageAsync(nextIndex, urls[nextIndex]));
            }
            
            // Execute preloads in parallel
            await Task.WhenAll(preloadTasks);
        }
        
        public async Task UnloadDistantImagesAsync(int currentIndex, List<string> urls, int maxCachedCount = MaxCachedImages)
        {
            if (urls == null || urls.Count == 0)
                return;
                
            await _cacheSemaphore.WaitAsync();
            try
            {
                // Determine which URLs to keep (current + adjacent)
                var urlsToKeep = new HashSet<string>();
                
                // Keep current
                if (currentIndex >= 0 && currentIndex < urls.Count)
                    urlsToKeep.Add(urls[currentIndex]);
                    
                // Keep adjacent
                for (int i = -PreloadDistance; i <= PreloadDistance; i++)
                {
                    var index = (currentIndex + i + urls.Count) % urls.Count;
                    if (index >= 0 && index < urls.Count)
                        urlsToKeep.Add(urls[index]);
                }
                
                // Remove distant images from cache
                var keysToRemove = _imageCache.Keys
                    .Where(url => !urlsToKeep.Contains(url))
                    .ToList();
                    
                foreach (var key in keysToRemove)
                {
                    if (_imageCache.TryRemove(key, out var bitmap))
                    {
                        bitmap?.Dispose();
                    }
                }
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }
        
        public void ClearImageCache()
        {
            _ = ClearImageCacheAsync();
        }
        
        private async Task ClearImageCacheAsync()
        {
            await _cacheSemaphore.WaitAsync();
            try
            {
                foreach (var kvp in _imageCache)
                {
                    kvp.Value?.Dispose();
                }
                _imageCache.Clear();
                _loadingTasks.Clear();
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }
        
        private async Task<Bitmap?> LoadImageInternalAsync(string url)
        {
            try
            {
                var cachedBytes = await TryGetCachedCarouselBytesAsync(url);
                var bytes = cachedBytes ?? await _http.GetByteArrayAsync(url);
                var resized = cachedBytes ?? TryResizeCarouselBytes(bytes) ?? bytes;

                if (cachedBytes == null)
                    await TryWriteCarouselCacheAsync(url, resized);

                await using var ms = new MemoryStream(resized);
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CarouselService.LoadImageInternalAsync", ex, $"Failed to load image from URL: {url}");
                return null;
            }
        }

        private static int GetCarouselSortIndex(string name)
        {
            var match = Regex.Match(name, @"^carousel_(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
                return index;
            return int.MaxValue;
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("CarouselService.TryGetCachedCarouselBytesAsync", ex, $"Failed to get cached carousel bytes for URL: {url}");
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("CarouselService.TryWriteCarouselCacheAsync", ex, $"Failed to write carousel cache for URL: {url}");
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

        private sealed class GitHubAssetEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("type")]
            public string Type { get; set; } = string.Empty;

            [JsonProperty("download_url")]
            public string? DownloadUrl { get; set; }
        }
    }
}
