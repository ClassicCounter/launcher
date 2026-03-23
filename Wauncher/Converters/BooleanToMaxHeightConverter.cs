using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wauncher.Converters
{
    public class BooleanToMaxHeightConverter : IValueConverter
    {
        public static readonly BooleanToMaxHeightConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString && int.TryParse(paramString, out var maxHeight))
            {
                return boolValue ? maxHeight : 0;
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
