using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class AppearancePanel : UserControl
    {
        public event Action? CloseRequested;
        private AppearanceWindowViewModel? _vm;

        public AppearancePanel()
        {
            InitializeComponent();
            _vm = new AppearanceWindowViewModel();
            DataContext = _vm;

            var closeButton = this.FindControl<Button>("ClosePanelButton");
            if (closeButton != null)
            {
                closeButton.Click += (s, e) => CloseRequested?.Invoke();
            }

            InitializeColorPickers();
        }

        private void InitializeColorPickers()
        {
            if (_vm == null) return;

            if (this.FindControl<SliderColorPicker>("BgColorPicker") is SliderColorPicker bgPicker)
                bgPicker.SetColor(Color.Parse(_vm.CurrentTheme.BgMain));

            if (this.FindControl<SliderColorPicker>("GreenColorPicker") is SliderColorPicker greenPicker)
                greenPicker.SetColor(Color.Parse(_vm.CurrentTheme.AccentGreen));

            if (this.FindControl<SliderColorPicker>("TextColorPicker") is SliderColorPicker textPicker)
                textPicker.SetColor(Color.Parse(_vm.CurrentTheme.TextPrimary));
        }

        private void BgColorPicker_ColorChanged(object? sender, Color color)
        {
            if (_vm != null)
            {
                _vm.CurrentTheme.BgMain = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                _vm.ApplyThemeChanges();
            }
        }

        private void GreenColorPicker_ColorChanged(object? sender, Color color)
        {
            if (_vm != null)
            {
                _vm.CurrentTheme.AccentGreen = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                _vm.ApplyThemeChanges();
            }
        }

        private void TextColorPicker_ColorChanged(object? sender, Color color)
        {
            if (_vm != null)
            {
                _vm.CurrentTheme.TextPrimary = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                _vm.ApplyThemeChanges();
            }
        }

        private void ResetButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            // Reset the theme to defaults (applies + saves)
            _vm.ResetToDefaultCommand.Execute(null);

            // Snap the sliders back to the new default values
            InitializeColorPickers();
        }
    }
}
