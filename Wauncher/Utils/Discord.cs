using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using System;

namespace Wauncher.Utils
{
    public static class Discord
    {
        private static readonly string _appId = "1133457462024994947";
        private static DiscordRpcClient _client = new DiscordRpcClient(_appId);
        private static RichPresence _presence = new RichPresence();
        public static string? CurrentUserId { get; private set; }
        public static string? CurrentUserAvatar { get; private set; }
        public static string? CurrentUserUsername { get; private set; }

        public static void Init()
        {
            _client.OnReady += OnReady;

            _client.Logger = new ConsoleLogger()
            {
                Level = Debug.Enabled() ? LogLevel.Warning : LogLevel.None
            };

            if (!_client.Initialize())
                return;

            SetDetails("In Launcher");
            SetTimestamp(DateTime.UtcNow);
            SetLargeArtwork("icon");

            Update();
        }

        public static void Deinitialize()
        {
            if (!_client.IsDisposed)
            {
                // SetPresence(null) clears the presence from Discord immediately.
                // Do NOT call Deinitialize/Dispose here — that prevents ClearPresence
                // from flushing, so the presence stays visible in Discord.
                _client.SetPresence(null);
            }
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
            CurrentUserId = e.User.ID.ToString();
            CurrentUserAvatar = e.User.GetAvatarURL(User.AvatarFormat.PNG);
            CurrentUserUsername = e.User.Username;
            OnAvatarUpdate?.Invoke(CurrentUserAvatar);
            OnUsernameUpdate?.Invoke(CurrentUserUsername);

            if (Debug.Enabled())
                Terminal.Debug($"Discord RPC: User is ready => @{e.User.Username} ({e.User.ID})");
        }

        public static event Action<string?>? OnAvatarUpdate;
        public static event Action<string?>? OnUsernameUpdate;
    }
}

