using Refit;
using Newtonsoft.Json;

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

    public class FriendInfo
    {
        [JsonProperty("steamid")]
        public string SteamId { get; set; } = "";

        [JsonProperty("steamid2")]
        public string? SteamId2
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SteamId))
                    SteamId = value;
            }
        }

        [JsonProperty("username")]
        public string Username  { get; set; } = "";

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; } = "";

        [JsonProperty("avatar")]
        public string? Avatar
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    AvatarUrl = value;
            }
        }

        [JsonProperty("custom_username")]
        public string? CustomUsername
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Username = value;
            }
        }

        [JsonProperty("custom_avatar")]
        public string? CustomAvatar
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    AvatarUrl = value;
            }
        }

        [JsonProperty("status")]
        public string Status    { get; set; } = "Offline"; // "Online" | "Offline"

        public string DotColor      => Status == "Online" ? "#4CAF50" : "#888888";
        public bool   IsOffline     => Status == "Offline";
        public double AvatarOpacity => IsOffline ? 0.35 : 1.0;
        public string StatusText    => IsOffline ? "Offline" : "In Game";
        public string StatusColor   => IsOffline ? "#666666" : "#999999";
    }

    public class FriendsResponse
    {
        public List<FriendInfo>? Friends { get; set; }
    }

    public interface IEddies
    {
        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/friendsapi.php")]
        Task<string> GetFriends([AliasAs("steamid64")] string steamId64);

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/friendsapi.php")]
        Task<string> GetFriendsBySteamId2([AliasAs("steamid2")] string steamId2);

        [Headers("User-Agent: ClassicCounter Wauncher")]
        [Get("/selfinfo.php")]
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
        private static HttpClientHandler _httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslErrors) => true
        };
        private static HttpClient ClassicCounterApiHttpClient = new HttpClient(_httpClientHandler)
        {
            BaseAddress = new Uri("https://classiccounter.cc/api")
        };
        private static RefitSettings _settings = new RefitSettings(new NewtonsoftJsonContentSerializer());
        public static IGitHub GitHub = RestService.For<IGitHub>("https://api.github.com", _settings);
        public static IClassicCounter ClassicCounter = Argument.Exists("--ssl-bypass")
            ? RestService.For<IClassicCounter>(ClassicCounterApiHttpClient, _settings)
            : RestService.For<IClassicCounter>("https://classiccounter.cc/api", _settings); // THIS IS NOT IDEAL, CHANGE THE WORLD OF TRUSTED CERTIFICATES
        public static IEddies Eddies = RestService.For<IEddies>("https://eddies.cc/api", _settings);

        public static List<FriendInfo> ParseFriendsPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<FriendInfo>();

            try
            {
                var wrapped = JsonConvert.DeserializeObject<FriendsResponse>(json);
                if (wrapped?.Friends != null && wrapped.Friends.Count > 0)
                    return NormalizeFriends(wrapped.Friends);
            }
            catch
            {
                // Fall through to array parse.
            }

            try
            {
                var flat = JsonConvert.DeserializeObject<List<FriendInfo>>(json);
                if (flat != null)
                    return NormalizeFriends(flat);
            }
            catch
            {
                // Ignore and return empty.
            }

            return new List<FriendInfo>();
        }

        public static FriendInfo? ParseSelfInfoPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var parsed = JsonConvert.DeserializeObject<FriendInfo>(json);
                if (parsed == null)
                    return null;

                return NormalizeFriends(new[] { parsed }).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static List<FriendInfo> NormalizeFriends(IEnumerable<FriendInfo> friends)
        {
            var normalized = new List<FriendInfo>();
            foreach (var f in friends)
            {
                var username = string.IsNullOrWhiteSpace(f.Username) ? "Unknown" : f.Username;
                var status = string.Equals(f.Status, "Online", StringComparison.OrdinalIgnoreCase)
                    ? "Online"
                    : "Offline";

                normalized.Add(new FriendInfo
                {
                    SteamId = f.SteamId ?? string.Empty,
                    Username = username,
                    AvatarUrl = f.AvatarUrl ?? string.Empty,
                    Status = status
                });
            }

            return normalized;
        }
    }
}

