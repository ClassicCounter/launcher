using System.Text.Json;
using Wauncher.Utils;

namespace Wauncher.Utils
{
    public static class FriendsCache
    {
        private static readonly string _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassicCounter",
            "Wauncher",
            "cache");

        private static readonly string _cacheFile = Path.Combine(_cacheDir, "friends_cache.json");

        private sealed class CachedFriend
        {
            public string Username { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string Status { get; set; } = "Offline";
        }

        private sealed class CacheEnvelope
        {
            public Dictionary<string, List<CachedFriend>> BySteamId { get; set; } = new();
        }

        public static async Task SaveAsync(string steamId, IEnumerable<FriendInfo> friends)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            var envelope = LoadEnvelope();
            envelope.BySteamId[steamId] = friends.Select(f => new CachedFriend
            {
                Username = f.Username ?? string.Empty,
                AvatarUrl = f.AvatarUrl ?? string.Empty,
                Status = string.IsNullOrWhiteSpace(f.Status) ? "Offline" : f.Status
            }).ToList();

            Directory.CreateDirectory(_cacheDir);
            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_cacheFile, json);
        }

        public static List<FriendInfo> Load(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return new List<FriendInfo>();

            var envelope = LoadEnvelope();
            if (!envelope.BySteamId.TryGetValue(steamId, out var cached) || cached == null)
                return new List<FriendInfo>();

            return cached.Select(c => new FriendInfo
            {
                Username = c.Username,
                AvatarUrl = c.AvatarUrl,
                Status = string.IsNullOrWhiteSpace(c.Status) ? "Offline" : c.Status
            }).ToList();
        }

        private static CacheEnvelope LoadEnvelope()
        {
            try
            {
                if (!File.Exists(_cacheFile))
                    return new CacheEnvelope();

                var json = File.ReadAllText(_cacheFile);
                if (string.IsNullOrWhiteSpace(json))
                    return new CacheEnvelope();

                return JsonSerializer.Deserialize<CacheEnvelope>(json) ?? new CacheEnvelope();
            }
            catch
            {
                return new CacheEnvelope();
            }
        }
    }
}

