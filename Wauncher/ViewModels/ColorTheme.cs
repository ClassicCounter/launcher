namespace Wauncher.ViewModels
{
    public class ColorTheme
    {
        // Theme name
        public string ThemeName { get; set; } = "Custom";

        // Accent Colors
        public string AccentGreen { get; set; } = "#4CAF50";
        public string AccentBlue { get; set; } = "#1B6EA8";
        public string AccentRed { get; set; } = "#F44336";
        public string AccentYellow { get; set; } = "#FFC107";
        public string AccentPurple { get; set; } = "#9B59B6";

        // Background Colors
        public string BgMain { get; set; } = "#3A3A3A";
        public string BgPanel { get; set; } = "#3A3A3A";
        public string BgInput { get; set; } = "#33FFFFFF";
        public string BgRightPanel { get; set; } = "#223A3A3A";

        // Text Colors
        public string TextPrimary { get; set; } = "#FFFFFF";
        public string TextSecondary { get; set; } = "#88FFFFFF";
        public string TextMuted { get; set; } = "#55FFFFFF";
        public string TextBody { get; set; } = "#CCFFFFFF";

        // Interactive States
        public string InteractiveHover { get; set; } = "#22FFFFFF";
        public string InteractivePressed { get; set; } = "#11FFFFFF";
        public string InteractiveDisabled { get; set; } = "#686868";

        // UI Elements
        public string DividerLight { get; set; } = "#22FFFFFF";
        public string DividerMedium { get; set; } = "#33FFFFFF";
        public string LinkText { get; set; } = "#6CB5F5";

        public ColorTheme() { }

        public ColorTheme(ColorTheme other)
        {
            ThemeName = other.ThemeName;
            AccentGreen = other.AccentGreen;
            AccentBlue = other.AccentBlue;
            AccentRed = other.AccentRed;
            AccentYellow = other.AccentYellow;
            AccentPurple = other.AccentPurple;
            BgMain = other.BgMain;
            BgPanel = other.BgPanel;
            BgInput = other.BgInput;
            BgRightPanel = other.BgRightPanel;
            TextPrimary = other.TextPrimary;
            TextSecondary = other.TextSecondary;
            TextMuted = other.TextMuted;
            TextBody = other.TextBody;
            InteractiveHover = other.InteractiveHover;
            InteractivePressed = other.InteractivePressed;
            InteractiveDisabled = other.InteractiveDisabled;
            DividerLight = other.DividerLight;
            DividerMedium = other.DividerMedium;
            LinkText = other.LinkText;
        }

        // Predefined themes
        public static ColorTheme Dark() => new()
        {
            ThemeName = "Dark",
            AccentGreen = "#4CAF50",
            AccentBlue = "#1B6EA8",
            AccentRed = "#F44336",
            AccentYellow = "#FFC107",
            AccentPurple = "#9B59B6",
            BgMain = "#3A3A3A",
            BgPanel = "#3A3A3A",
            BgInput = "#33FFFFFF",
            BgRightPanel = "#223A3A3A",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#88FFFFFF",
            TextMuted = "#55FFFFFF",
            TextBody = "#CCFFFFFF",
            InteractiveHover = "#22FFFFFF",
            InteractivePressed = "#11FFFFFF",
            InteractiveDisabled = "#686868",
            DividerLight = "#22FFFFFF",
            DividerMedium = "#33FFFFFF",
            LinkText = "#6CB5F5"
        };

        public static ColorTheme DarkBlue() => new()
        {
            ThemeName = "Dark Blue",
            AccentGreen = "#2196F3",
            AccentBlue = "#1976D2",
            AccentRed = "#F44336",
            AccentYellow = "#FFC107",
            AccentPurple = "#2196F3",
            BgMain = "#1A237E",
            BgPanel = "#1A237E",
            BgInput = "#33FFFFFF",
            BgRightPanel = "#0D47A1",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#88FFFFFF",
            TextMuted = "#55FFFFFF",
            TextBody = "#CCFFFFFF",
            InteractiveHover = "#22FFFFFF",
            InteractivePressed = "#11FFFFFF",
            InteractiveDisabled = "#686868",
            DividerLight = "#22FFFFFF",
            DividerMedium = "#33FFFFFF",
            LinkText = "#64B5F6"
        };

        public static ColorTheme DarkPurple() => new()
        {
            ThemeName = "Dark Purple",
            AccentGreen = "#9B59B6",
            AccentBlue = "#7B1FA2",
            AccentRed = "#F44336",
            AccentYellow = "#FFC107",
            AccentPurple = "#9B59B6",
            BgMain = "#4A148C",
            BgPanel = "#4A148C",
            BgInput = "#33FFFFFF",
            BgRightPanel = "#311B92",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#88FFFFFF",
            TextMuted = "#55FFFFFF",
            TextBody = "#CCFFFFFF",
            InteractiveHover = "#22FFFFFF",
            InteractivePressed = "#11FFFFFF",
            InteractiveDisabled = "#686868",
            DividerLight = "#22FFFFFF",
            DividerMedium = "#33FFFFFF",
            LinkText = "#CE93D8"
        };

        public static ColorTheme DarkGreen() => new()
        {
            ThemeName = "Dark Green",
            AccentGreen = "#2E7D32",
            AccentBlue = "#1B5E20",
            AccentRed = "#F44336",
            AccentYellow = "#FFC107",
            AccentPurple = "#9B59B6",
            BgMain = "#1B5E20",
            BgPanel = "#1B5E20",
            BgInput = "#33FFFFFF",
            BgRightPanel = "#003D00",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#88FFFFFF",
            TextMuted = "#55FFFFFF",
            TextBody = "#CCFFFFFF",
            InteractiveHover = "#22FFFFFF",
            InteractivePressed = "#11FFFFFF",
            InteractiveDisabled = "#686868",
            DividerLight = "#22FFFFFF",
            DividerMedium = "#33FFFFFF",
            LinkText = "#81C784"
        };

        public static List<ColorTheme> GetPredefinedThemes() => new()
        {
            Dark(),
            DarkBlue(),
            DarkPurple(),
            DarkGreen()
        };
    }
}
