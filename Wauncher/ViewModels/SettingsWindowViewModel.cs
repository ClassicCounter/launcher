using CommunityToolkit.Mvvm.ComponentModel;

namespace Wauncher.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        // ── Static events — fire whenever these settings change on ANY instance ──
        public static event Action<bool>?   DiscordRpcChanged;
        public static event Action<bool>?   DisableCarouselChanged;

        [ObservableProperty]
        private bool _discordRpc = true;

        [ObservableProperty]
        private bool _skipUpdates = false;

        [ObservableProperty]
        private bool _disableCarousel = false;

        [ObservableProperty]
        private bool _disableHardwareAcceleration = false;

        [ObservableProperty]
        private bool _enableGc = false;

        [ObservableProperty]
        private string _launchOptions = string.Empty;

        public SettingsWindowViewModel()
        {
            Load();
        }

        partial void OnSkipUpdatesChanged(bool value) => Save();
        partial void OnLaunchOptionsChanged(string value) => Save();
        partial void OnDisableCarouselChanged(bool value)
        {
            Save();
            DisableCarouselChanged?.Invoke(value);
        }
        partial void OnDisableHardwareAccelerationChanged(bool value) => Save();
        partial void OnEnableGcChanged(bool value) => Save();

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
                        case "DiscordRpc":     DiscordRpc     = value.Trim() == "true"; break;
                        case "SkipUpdates":    SkipUpdates    = value.Trim() == "true"; break;
                        case "DisableCarousel": DisableCarousel = value.Trim() == "true"; break;
                        case "DisableHardwareAcceleration": DisableHardwareAcceleration = value.Trim() == "true"; break;
                        case "EnableGc":       EnableGc       = value.Trim() == "true"; break;
                        case "LaunchOptions":  LaunchOptions  = value; break;
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                var path = SettingsPath();
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new System.Collections.Generic.List<string>();

                // Remove only the standard setting keys, preserving color_/appearance_ entries
                lines.RemoveAll(l =>
                    l.StartsWith("DiscordRpc=") ||
                    l.StartsWith("SkipUpdates=") ||
                    l.StartsWith("DisableCarousel=") ||
                    l.StartsWith("DisableHardwareAcceleration=") ||
                    l.StartsWith("EnableGc=") ||
                    l.StartsWith("LaunchOptions="));

                lines.Add($"DiscordRpc={DiscordRpc.ToString().ToLower()}");
                lines.Add($"SkipUpdates={SkipUpdates.ToString().ToLower()}");
                lines.Add($"DisableCarousel={DisableCarousel.ToString().ToLower()}");
                lines.Add($"DisableHardwareAcceleration={DisableHardwareAcceleration.ToString().ToLower()}");
                lines.Add($"EnableGc={EnableGc.ToString().ToLower()}");
                lines.Add($"LaunchOptions={LaunchOptions}");

                File.WriteAllLines(path, lines);
            }
            catch { }
        }

        public static SettingsWindowViewModel LoadGlobal() => new();

        public static string SettingsPath()
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassicCounter",
                "Wauncher",
                "config");
            var newPath = Path.Combine(configDir, "wauncher_settings.cfg");

            try
            {
                Directory.CreateDirectory(configDir);
            }
            catch
            {
                // Fall back to returning the new path even if folder creation fails.
            }

            return newPath;
        }

        public void SaveColorTheme(ColorTheme theme)
        {
            try
            {
                var lines = File.ReadAllLines(SettingsPath()).ToList();

                // Remove existing color theme entries
                lines.RemoveAll(l => l.StartsWith("appearance_") || l.StartsWith("color_"));

                // Add new color theme entries
                lines.Add($"appearance_theme={theme.ThemeName}");
                lines.Add($"color_accent_green={theme.AccentGreen}");
                lines.Add($"color_accent_blue={theme.AccentBlue}");
                lines.Add($"color_accent_red={theme.AccentRed}");
                lines.Add($"color_accent_yellow={theme.AccentYellow}");
                lines.Add($"color_accent_purple={theme.AccentPurple}");
                lines.Add($"color_bg_main={theme.BgMain}");
                lines.Add($"color_bg_panel={theme.BgPanel}");
                lines.Add($"color_bg_input={theme.BgInput}");
                lines.Add($"color_bg_rightpanel={theme.BgRightPanel}");
                lines.Add($"color_text_primary={theme.TextPrimary}");
                lines.Add($"color_text_secondary={theme.TextSecondary}");
                lines.Add($"color_text_muted={theme.TextMuted}");
                lines.Add($"color_text_body={theme.TextBody}");
                lines.Add($"color_interactive_hover={theme.InteractiveHover}");
                lines.Add($"color_interactive_pressed={theme.InteractivePressed}");
                lines.Add($"color_interactive_disabled={theme.InteractiveDisabled}");
                lines.Add($"color_divider_light={theme.DividerLight}");
                lines.Add($"color_divider_medium={theme.DividerMedium}");
                lines.Add($"color_link_text={theme.LinkText}");

                File.WriteAllLines(SettingsPath(), lines);
            }
            catch { }
        }

        public ColorTheme? LoadColorTheme()
        {
            try
            {
                string path = SettingsPath();
                if (!File.Exists(path)) return null;

                var theme = new ColorTheme();
                var lines = File.ReadAllLines(path);

                foreach (var line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line[..eq].Trim();
                    var value = line[(eq + 1)..];

                    switch (key)
                    {
                        case "appearance_theme":           theme.ThemeName = value; break;
                        case "color_accent_green":         theme.AccentGreen = value; break;
                        case "color_accent_blue":          theme.AccentBlue = value; break;
                        case "color_accent_red":           theme.AccentRed = value; break;
                        case "color_accent_yellow":        theme.AccentYellow = value; break;
                        case "color_accent_purple":        theme.AccentPurple = value; break;
                        case "color_bg_main":              theme.BgMain = value; break;
                        case "color_bg_panel":             theme.BgPanel = value; break;
                        case "color_bg_input":             theme.BgInput = value; break;
                        case "color_bg_rightpanel":        theme.BgRightPanel = value; break;
                        case "color_text_primary":         theme.TextPrimary = value; break;
                        case "color_text_secondary":       theme.TextSecondary = value; break;
                        case "color_text_muted":           theme.TextMuted = value; break;
                        case "color_text_body":            theme.TextBody = value; break;
                        case "color_interactive_hover":    theme.InteractiveHover = value; break;
                        case "color_interactive_pressed":  theme.InteractivePressed = value; break;
                        case "color_interactive_disabled": theme.InteractiveDisabled = value; break;
                        case "color_divider_light":        theme.DividerLight = value; break;
                        case "color_divider_medium":       theme.DividerMedium = value; break;
                        case "color_link_text":            theme.LinkText = value; break;
                    }
                }

                return theme;
            }
            catch { }
            return null;
        }
    }
}
