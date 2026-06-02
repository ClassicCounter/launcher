using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;

namespace Wauncher.Views.Controls
{
    public partial class SliderColorPicker : UserControl
    {
        private double _hue = 0;
        private double _saturation = 1.0;
        private double _brightness = 1.0;
        private bool _isUpdating = false;

        public event EventHandler<Color>? ColorChanged;

        public Color SelectedColor => HsvToRgb(_hue, _saturation, _brightness);

        public SliderColorPicker()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        private void Hue_Changed(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            _hue = e.NewValue;
            UpdateDisplay();
        }

        private void Saturation_Changed(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            _saturation = e.NewValue / 100.0;
            UpdateDisplay();
        }

        private void Brightness_Changed(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating) return;
            _brightness = e.NewValue / 100.0;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            UpdatePreview();
            UpdateHexDisplay();
            UpdateSliderLabels();
            ColorChanged?.Invoke(this, SelectedColor);
        }

        private void UpdatePreview()
        {
            if (this.FindControl<Rectangle>("ColorPreview") is Rectangle preview)
            {
                preview.Fill = new SolidColorBrush(SelectedColor);
            }
        }

        private void UpdateHexDisplay()
        {
            if (this.FindControl<TextBlock>("HexDisplay") is TextBlock hexDisplay)
            {
                var color = SelectedColor;
                hexDisplay.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }

        private void UpdateSliderLabels()
        {
            if (this.FindControl<TextBlock>("HueValue") is TextBlock hueValue)
                hueValue.Text = $"{_hue:F0}°";

            if (this.FindControl<TextBlock>("SatValue") is TextBlock satValue)
                satValue.Text = $"{_saturation * 100:F0}%";

            if (this.FindControl<TextBlock>("BrightValue") is TextBlock brightValue)
                brightValue.Text = $"{_brightness * 100:F0}%";
        }

        public void SetColor(Color color)
        {
            _isUpdating = true;
            RgbToHsv(color, out var h, out var s, out var v);
            _hue = h;
            _saturation = s;
            _brightness = v;

            if (this.FindControl<Slider>("HueSlider") is Slider hueSlider)
                hueSlider.Value = _hue;
            if (this.FindControl<Slider>("SaturationSlider") is Slider satSlider)
                satSlider.Value = _saturation * 100;
            if (this.FindControl<Slider>("BrightnessSlider") is Slider brightSlider)
                brightSlider.Value = _brightness * 100;

            _isUpdating = false;
            UpdatePreview();
            UpdateHexDisplay();
            UpdateSliderLabels();
        }

        private static Color HsvToRgb(double hue, double saturation, double brightness)
        {
            hue = hue % 360;
            var c = brightness * saturation;
            var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            var m = brightness - c;

            double r, g, b;

            if (hue < 60)
                (r, g, b) = (c, x, 0);
            else if (hue < 120)
                (r, g, b) = (x, c, 0);
            else if (hue < 180)
                (r, g, b) = (0, c, x);
            else if (hue < 240)
                (r, g, b) = (0, x, c);
            else if (hue < 300)
                (r, g, b) = (x, 0, c);
            else
                (r, g, b) = (c, 0, x);

            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        private static void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            h = 0;
            if (delta != 0)
            {
                if (max == r)
                    h = ((g - b) / delta % 6) * 60;
                else if (max == g)
                    h = ((b - r) / delta + 2) * 60;
                else
                    h = ((r - g) / delta + 4) * 60;

                if (h < 0) h += 360;
            }

            s = max == 0 ? 0 : delta / max;
            v = max;
        }
    }
}
