using System.Globalization;
using StreamScanner.Maui.ViewModels;

namespace StreamScanner.Maui.Converters;

public class RefreshOptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int seconds)
        {
            return new RefreshOption(seconds, seconds == 0 ? "Off" : $"{seconds}s");
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RefreshOption option)
        {
            return option.Seconds;
        }
        return 0;
    }
}
