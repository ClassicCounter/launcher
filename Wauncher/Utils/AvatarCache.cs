using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

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

            await using var input = await response.Content.ReadAsStreamAsync();
            var tempPath = cachePath + ".tmp";
            await using var output = File.Create(tempPath);

            var buffer = new byte[81920];
            int read;
            int total = 0;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                total += read;
                if (total > MaxAvatarBytes)
                    throw new InvalidDataException("Avatar exceeds size limit.");

                await output.WriteAsync(buffer.AsMemory(0, read));
            }

            output.Close();
            File.Move(tempPath, cachePath, overwrite: true);
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
