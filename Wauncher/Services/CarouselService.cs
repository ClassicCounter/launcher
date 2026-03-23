using System;
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
            catch 
            { 
                return null; 
            }
        }

        public async Task TeardownCarouselAsync()
        {
            await Task.CompletedTask;
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
