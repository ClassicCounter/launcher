using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using Wauncher.Utils;

namespace Wauncher.ViewModels
{
    public partial class AppearanceWindowViewModel : ViewModelBase
    {
        // Static event for color changes
        public static event EventHandler<ColorTheme>? ColorThemeChanged;

        [ObservableProperty]
        private ColorTheme currentTheme = ColorTheme.Dark();

        [ObservableProperty]
        private ObservableCollection<ColorTheme> predefinedThemes = new();

        [ObservableProperty]
        private int selectedThemeIndex = 0;

        public AppearanceWindowViewModel()
        {
            try
            {
                InitializePredefinedThemes();
                LoadSavedTheme();
            }
            catch
            {
                // Fallback to default theme if loading fails
                PredefinedThemes.Clear();
                foreach (var theme in ColorTheme.GetPredefinedThemes())
                {
                    PredefinedThemes.Add(theme);
                }
                CurrentTheme = new ColorTheme(ColorTheme.Dark());
            }
        }

        private void InitializePredefinedThemes()
        {
            PredefinedThemes.Clear();
            foreach (var theme in ColorTheme.GetPredefinedThemes())
            {
                PredefinedThemes.Add(theme);
            }
        }

        [RelayCommand]
        private void SelectTheme(int index)
        {
            if (index >= 0 && index < PredefinedThemes.Count)
            {
                var selectedTheme = PredefinedThemes[index];
                CurrentTheme = new ColorTheme(selectedTheme);
                SelectedThemeIndex = index;
                ApplyTheme();
            }
        }

        // Note: Color updates are handled through the UI, individual color pickers would go here
        private void UpdateColor(string colorProperty, string newColor)
        {
            var property = CurrentTheme.GetType().GetProperty(colorProperty);
            if (property != null && property.CanWrite)
            {
                property.SetValue(CurrentTheme, newColor);
                CurrentTheme.ThemeName = "Custom";
                ApplyTheme();
            }
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            CurrentTheme = new ColorTheme(ColorTheme.Dark());
            SelectedThemeIndex = 0;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ColorThemeChanged?.Invoke(this, CurrentTheme);
            SaveTheme();
        }

        public void ApplyThemeChanges()
        {
            CurrentTheme.ThemeName = "Custom";
            ApplyTheme();
        }

        [RelayCommand]
        private void ChangeBgColor()
        {
            var colors = new[] { "#2A2A2A", "#3A3A3A", "#4A4A4A", "#1A1A1A" };
            var currentColor = CurrentTheme.BgMain;
            var nextColor = colors.FirstOrDefault(c => c != currentColor) ?? colors[0];
            CurrentTheme.BgMain = nextColor;
            ApplyThemeChanges();
        }

        [RelayCommand]
        private void ChangeGreenColor()
        {
            var colors = new[] { "#2ECC71", "#4CAF50", "#66BB6A", "#81C784" };
            var currentColor = CurrentTheme.AccentGreen;
            var nextColor = colors.FirstOrDefault(c => c != currentColor) ?? colors[0];
            CurrentTheme.AccentGreen = nextColor;
            ApplyThemeChanges();
        }

        [RelayCommand]
        private void ChangeTextColor()
        {
            var colors = new[] { "#FFFFFF", "#E0E0E0", "#F5F5F5", "#D0D0D0" };
            var currentColor = CurrentTheme.TextPrimary;
            var nextColor = colors.FirstOrDefault(c => c != currentColor) ?? colors[0];
            CurrentTheme.TextPrimary = nextColor;
            ApplyThemeChanges();
        }

        private void SaveTheme()
        {
            try
            {
                var settings = SettingsWindowViewModel.LoadGlobal();
                settings.SaveColorTheme(CurrentTheme);
            }
            catch (Exception ex)
            {
                Wauncher.Utils.Terminal.Warning($"Failed to save color theme: {ex.Message}");
            }
        }

        private void LoadSavedTheme()
        {
            try
            {
                var settings = SettingsWindowViewModel.LoadGlobal();
                var savedTheme = settings.LoadColorTheme();
                if (savedTheme != null)
                {
                    CurrentTheme = savedTheme;
                    // Try to match with predefined theme
                    var matchIndex = PredefinedThemes.ToList().FindIndex(t => t.ThemeName == savedTheme.ThemeName);
                    SelectedThemeIndex = matchIndex >= 0 ? matchIndex : 0;
                }
            }
            catch (Exception ex)
            {
                Wauncher.Utils.Terminal.Warning($"Failed to load color theme: {ex.Message}");
            }
        }
    }
}
