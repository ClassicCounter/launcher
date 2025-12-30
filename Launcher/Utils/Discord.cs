using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;

namespace Launcher.Utils
{
    public static class Discord
    {
        private static readonly string _appId = "1133457462024994947";
        private static DiscordRpcClient _client = new DiscordRpcClient(_appId);
        private static RichPresence _presence = new RichPresence();
        public static string? CurrentUserId { get; private set; } // ! DEPRECATED ! for whitelist check
        public static string? CurrentUserAvatar { get; private set; }

        public static void Init()
        {
            _client.OnReady += OnReady;

            _client.Logger = new ConsoleLogger()
            {
                Level = Debug.Enabled() ? LogLevel.Warning : LogLevel.None
            };

            if (!_client.Initialize())
            {
                return;
            }

            SetDetails("In Launcher");
            SetTimestamp(DateTime.UtcNow);
            SetLargeArtwork("icon");

            Update();
        }

        public static void Update() => _client.SetPresence(_presence);

        public static void SetDetails(string? details) => _presence.Details = details;
        public static void SetState(string? state) => _presence.State = state;

        public static void SetTimestamp(DateTime? time)
        {
            if (_presence.Timestamps == null) _presence.Timestamps = new();
            _presence.Timestamps.Start = time; 
        }

        public static void SetLargeArtwork(string? key)
        {
            if (_presence.Assets == null) _presence.Assets = new();
            _presence.Assets.LargeImageKey = key; 
        }

        public static void SetSmallArtwork(string? key)
        {
            if (_presence.Assets == null) _presence.Assets = new();
            _presence.Assets.SmallImageKey = key;
        }

        private static void OnReady(object sender, ReadyMessage e)
        {
            CurrentUserId = e.User.ID.ToString(); // ! DEPRECATED ! for passing current uid to api
            CurrentUserAvatar = e.User.GetAvatarURL(User.AvatarFormat.PNG);

            if (Debug.Enabled())
                Terminal.Debug($"Discord RPC: User is ready => @{e.User.Username} ({e.User.ID})");
        }

        public static string? GetAvatar()
        {
            return CurrentUserAvatar;
        }
    }
}
