using CommunityToolkit.Mvvm.ComponentModel;

namespace Wauncher.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        // ── Static events — fire whenever these settings change on ANY instance ──
        public static event Action<bool>?   DiscordRpcChanged;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _discordRpc = true;

        [ObservableProperty]
        private bool _skipUpdates = false;

        public SettingsWindowViewModel()
        {
            Load();
        }

        partial void OnMinimizeToTrayChanged(bool value) => Save();
        partial void OnSkipUpdatesChanged(bool value) => Save();

        partial void OnDiscordRpcChanged(bool value)
        {
            Save();
            DiscordRpcChanged?.Invoke(value);
        }

        private void Load()
        {
            try
            {
                string path = SettingsPath();
                if (!File.Exists(path)) { Save(); return; }

                foreach (var line in File.ReadAllLines(path))
                {
                    // Split only on the first "=" so values like "+set key=value" are preserved.
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..];

                    switch (key)
                    {
                        case "MinimizeToTray": MinimizeToTray = value.Trim() == "true"; break;
                        case "DiscordRpc":     DiscordRpc     = value.Trim() == "true"; break;
                        case "SkipUpdates":    SkipUpdates    = value.Trim() == "true"; break;
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(SettingsPath(), new[]
                {
                    $"MinimizeToTray={MinimizeToTray.ToString().ToLower()}",
                    $"DiscordRpc={DiscordRpc.ToString().ToLower()}",
                    $"SkipUpdates={SkipUpdates.ToString().ToLower()}",
                });
            }
            catch { }
        }

        public static SettingsWindowViewModel LoadGlobal() => new();

        public static string SettingsPath() =>
            Path.Combine(new FileInfo(System.Environment.ProcessPath ?? "").Directory?.FullName ?? Directory.GetCurrentDirectory(), "wauncher_settings.cfg");
    }
}
