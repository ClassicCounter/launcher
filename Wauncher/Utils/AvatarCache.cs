using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace Wauncher.Utils
{
    public static class AvatarCache
    {
        private static readonly HttpClient _http = new();
        private static readonly ConcurrentDictionary<string, byte> _inFlight = new();
        private static readonly string _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassicCounter",
            "Wauncher",
            "cache",
            "avatars");

        private const int MaxAvatarBytes = 20 * 1024 * 1024; // 20 MB
        private const int MaxSteamAvatarDimension = 128;

        public static string GetDisplaySource(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
                return string.Empty;

            var cachedPath = GetCachePath(avatarUrl);
            if (File.Exists(cachedPath))
                return new Uri(cachedPath).AbsoluteUri;

            QueueWarmCache(avatarUrl);
            return avatarUrl;
        }

        public static void QueueWarmCache(string? avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
                return;

            if (!_inFlight.TryAdd(avatarUrl, 0))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureCachedAsync(avatarUrl);
                }
                catch
                {
                    // Best-effort cache warmup only.
                }
                finally
                {
                    _inFlight.TryRemove(avatarUrl, out _);
                }
            });
        }

        private static async Task EnsureCachedAsync(string avatarUrl)
        {
            var cachePath = GetCachePath(avatarUrl);
            if (File.Exists(cachePath))
                return;

            Directory.CreateDirectory(_cacheDir);

            using var response = await _http.GetAsync(avatarUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var tempPath = cachePath + ".tmp";
            var bytes = await ReadAvatarBytesAsync(response);
            var bytesToWrite = TryDownscaleSteamAvatar(avatarUrl, bytes) ?? bytes;

            await File.WriteAllBytesAsync(tempPath, bytesToWrite);
            File.Move(tempPath, cachePath, overwrite: true);
        }

        private static async Task<byte[]> ReadAvatarBytesAsync(HttpResponseMessage response)
        {
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var bufferStream = new MemoryStream();

            var buffer = new byte[81920];
            int read;
            int total = 0;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                total += read;
                if (total > MaxAvatarBytes)
                    throw new InvalidDataException("Avatar exceeds size limit.");

                await bufferStream.WriteAsync(buffer.AsMemory(0, read));
            }

            return bufferStream.ToArray();
        }

        private static byte[]? TryDownscaleSteamAvatar(string avatarUrl, byte[] bytes)
        {
            if (!ShouldDownscaleAvatar(avatarUrl))
                return null;

            try
            {
                using var sourceBitmap = SKBitmap.Decode(bytes);
                if (sourceBitmap == null)
                    return null;

                if (sourceBitmap.Width <= MaxSteamAvatarDimension &&
                    sourceBitmap.Height <= MaxSteamAvatarDimension)
                {
                    return null;
                }

                var scale = Math.Min(
                    (double)MaxSteamAvatarDimension / sourceBitmap.Width,
                    (double)MaxSteamAvatarDimension / sourceBitmap.Height);

                int targetWidth = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale));
                int targetHeight = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));

                using var resizedBitmap = sourceBitmap.Resize(
                    new SKImageInfo(targetWidth, targetHeight),
                    SKFilterQuality.Medium);

                if (resizedBitmap == null)
                    return null;

                using var image = SKImage.FromBitmap(resizedBitmap);
                var format = GetEncodedFormat(avatarUrl);
                using var data = image.Encode(format, 90);
                return data?.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static bool ShouldDownscaleAvatar(string avatarUrl)
        {
            try
            {
                var host = new Uri(avatarUrl).Host;
                return host.Contains("steamstatic.com", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static SKEncodedImageFormat GetEncodedFormat(string avatarUrl)
        {
            var ext = GetExtensionFromUrl(avatarUrl);
            return ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg,
            };
        }

        private static string GetCachePath(string avatarUrl)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(avatarUrl))).ToLowerInvariant();
            var ext = GetExtensionFromUrl(avatarUrl);
            return Path.Combine(_cacheDir, $"{hash}{ext}");
        }

        private static string GetExtensionFromUrl(string avatarUrl)
        {
            try
            {
                var uri = new Uri(avatarUrl);
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6)
                    return ext.ToLowerInvariant();
            }
            catch
            {
                // ignore and fall back
            }
            return ".img";
        }
    }
}
