using Avalonia.Data.Converters;
using System.Globalization;

namespace Wauncher.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public static readonly PathToImageConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path))
                return null;

            // Get the application directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(appDir, path);

            // Return file:// URI format for the image
            return new Uri(fullPath);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }
}
