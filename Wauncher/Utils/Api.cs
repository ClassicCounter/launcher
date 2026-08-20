using Refit;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace Wauncher.Utils
{
    public class FullGameDownload
    {
        public required string File { get; set; }
        public required string Link { get; set; }
        public required string Hash { get; set; }
    }

    public class FullGameDownloadResponse
    {
        public List<FullGameDownload>? Files { get; set; }
        public string? Error { get; set; }
    }

    public interface IGitHub
    {
        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/repos/ClassicCounter/launcher/releases/latest")]
        Task<string> GetLatestRelease();

        [Headers("User-Agent: ClassicCounter Wauncher",
            "Accept: application/vnd.github.raw+json")]
        [Get("/repos/ClassicCounter/launcher/contents/dependencies.json")]
        Task<string> GetDependencies();

        [Headers("User-Agent: ClassicCounter Wauncher",
            "Accept: application/vnd.github.raw+json")]
        [Get("/repos/ClassicCounter/launcher/contents/carousel.json")]
        Task<string> GetCarouselManifest();

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/repos/ClassicCounter/launcher/contents/Wauncher/Assets")]
        Task<string> GetCarouselAssetsWauncher();

        [Headers("User-Agent: ClassicCounter Wauncher",
            "Accept: application/vnd.github.raw+json")]
        [Get("/repos/ClassicCounter/launcher/contents/Wauncher/patchnotes.md")]
        Task<string> GetPatchNotesWauncher();
    }

    public class FriendInfo : INotifyPropertyChanged
    {
        private string _steamId = "";
        private string _username = "";
        private string _avatarUrl = "";
        private string _status = "Offline";
        private string _quickJoinIpPort = "";
        private string _quickJoinServerName = "";

        [JsonProperty("steamid")]
        public string SteamId
        {
            get => _steamId;
            // Only overwrite if new value is non-empty so steamid64 written first isn't erased
            // by a subsequent empty "steamid" field in the same JSON object.
            set { if (!string.IsNullOrWhiteSpace(value)) { _steamId = value; OnPropertyChanged(); } }
        }

        [JsonProperty("steamid2")]
        public string? SteamId2
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SteamId))
                    SteamId = value;
            }
        }

        [JsonProperty("steamid64")]
        public string? SteamId64
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SteamId))
                    SteamId = value;
            }
        }

        [JsonProperty("username")]
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        [JsonProperty("avatar_url")]
        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value; OnPropertyChanged(); }
        }

        [JsonProperty("avatar")]
        public string? Avatar
        {
            set { if (!string.IsNullOrWhiteSpace(value)) AvatarUrl = value; }
        }

        [JsonProperty("custom_username")]
        public string? CustomUsername
        {
            set { if (!string.IsNullOrWhiteSpace(value)) Username = value; }
        }

        [JsonProperty("custom_avatar")]
        public string? CustomAvatar
        {
            set { if (!string.IsNullOrWhiteSpace(value)) AvatarUrl = value; }
        }

        [JsonProperty("status")]
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DotColor));
                OnPropertyChanged(nameof(IsOffline));
                OnPropertyChanged(nameof(AvatarOpacity));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        [JsonIgnore]
        public string QuickJoinIpPort
        {
            get => _quickJoinIpPort;
            set { _quickJoinIpPort = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanQuickJoin)); }
        }

        [JsonIgnore]
        public string QuickJoinServerName
        {
            get => _quickJoinServerName;
            set { _quickJoinServerName = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public bool CanQuickJoin => !string.IsNullOrWhiteSpace(QuickJoinIpPort);

        public string DotColor      => IsOffline ? "#888888" : "#4CAF50";
        public bool   IsOffline     => string.Equals(Status, "Offline", StringComparison.OrdinalIgnoreCase);
        public double AvatarOpacity => IsOffline ? 0.35 : 1.0;
        public string StatusText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Status))
                    return "Offline";

                const string inGamePrefix = "In Game - ";
                return Status.StartsWith(inGamePrefix, StringComparison.OrdinalIgnoreCase)
                    ? Status[inGamePrefix.Length..].Trim()
                    : Status;
            }
        }
        public string StatusColor => IsOffline ? "#666666" : "#999999";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FriendsResponse
    {
        public List<FriendInfo>? Friends { get; set; }
    }

    public class CCFriendsResponse
    {
        [JsonProperty("response")]
        public List<FriendInfo>? Response { get; set; }
    }

    public class CCPlayerResponse
    {
        [JsonProperty("response")]
        public FriendInfo? Response { get; set; }
    }

    public interface IClassicCounterProfiles
    {
        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/friends")]
        Task<string> GetFriends([AliasAs("steamid64")] string steamId64);

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/player")]
        Task<string> GetSelfInfo([AliasAs("steamid64")] string steamId64);
    }

    public interface IClassicCounter
    {
        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/patch/get")]
        Task<string> GetPatches();

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/game/get")]
        Task<string> GetFullGameValidate();

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/game/full")]
        Task<FullGameDownloadResponse> GetFullGameDownload([Query] string steam_id);
    }

    public static class Api
    {
        private static RefitSettings _settings = new RefitSettings(new NewtonsoftJsonContentSerializer());

        // Fail fast when a host is unreachable instead of hanging on the default ~100s timeout.
        private static HttpClient TimedClient(string baseUrl, int timeoutSeconds = 12) =>
            new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        public static IGitHub GitHub = RestService.For<IGitHub>(TimedClient("https://api.github.com", 8), _settings);
        public static IClassicCounter ClassicCounter = RestService.For<IClassicCounter>(TimedClient("https://classiccounter.cc/api"), _settings);
        public static IClassicCounterProfiles Profiles = RestService.For<IClassicCounterProfiles>(TimedClient("https://classiccounter.cc/api/profiles"), _settings);

        public static List<FriendInfo> ParseFriendsPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<FriendInfo>();

            try
            {
                var cc = JsonConvert.DeserializeObject<CCFriendsResponse>(json);
                if (cc?.Response != null && cc.Response.Count > 0)
                    return NormalizeFriends(cc.Response);
            }
            catch { }

            try
            {
                var wrapped = JsonConvert.DeserializeObject<FriendsResponse>(json);
                if (wrapped?.Friends != null && wrapped.Friends.Count > 0)
                    return NormalizeFriends(wrapped.Friends);
            }
            catch { }

            try
            {
                var flat = JsonConvert.DeserializeObject<List<FriendInfo>>(json);
                if (flat != null)
                    return NormalizeFriends(flat);
            }
            catch { }

            return new List<FriendInfo>();
        }

        public static FriendInfo? ParseSelfInfoPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var cc = JsonConvert.DeserializeObject<CCPlayerResponse>(json);
                if (cc?.Response != null)
                    return NormalizeFriends(new[] { cc.Response }).FirstOrDefault();
            }
            catch { }

            try
            {
                var parsed = JsonConvert.DeserializeObject<FriendInfo>(json);
                if (parsed != null)
                    return NormalizeFriends(new[] { parsed }).FirstOrDefault();
            }
            catch { }

            return null;
        }

        private static List<FriendInfo> NormalizeFriends(IEnumerable<FriendInfo> friends)
        {
            var normalized = new List<FriendInfo>();
            var usedKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var f in friends)
            {
                var username = string.IsNullOrWhiteSpace(f.Username) ? "Unknown" : f.Username;
                var status = NormalizeStatus(f.Status);

                // Fall back to username as identity key when the API omits steamid.
                // Append an index suffix to guarantee uniqueness when two friends share a username.
                string key = !string.IsNullOrWhiteSpace(f.SteamId) ? f.SteamId : username;
                if (!usedKeys.Add(key))
                {
                    int n = 1;
                    string unique;
                    while (!usedKeys.Add(unique = $"{key}#{n}")) n++;
                    key = unique;
                }

                normalized.Add(new FriendInfo
                {
                    SteamId = key,
                    Username = username,
                    AvatarUrl = f.AvatarUrl ?? string.Empty,
                    Status = status
                });
            }

            return normalized;
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Offline";

            var trimmed = status.Trim();
            if (string.Equals(trimmed, "Offline", StringComparison.OrdinalIgnoreCase))
                return "Offline";

            if (string.Equals(trimmed, "Online", StringComparison.OrdinalIgnoreCase))
                return "Online";

            return trimmed;
        }
    }
}

