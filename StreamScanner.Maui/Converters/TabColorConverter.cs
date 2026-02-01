using System.Globalization;

namespace StreamScanner.Maui.Converters;

public class BoolToTabColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return Application.Current?.Resources.TryGetValue("AccentRed", out var color) == true
                ? color : Colors.Red;
        }
        return Application.Current?.Resources.TryGetValue("BgTertiary", out var bgColor) == true
            ? bgColor : Colors.DarkGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToTabTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return Colors.White;
        }
        return Application.Current?.Resources.TryGetValue("TextSecondary", out var color) == true
            ? color : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return percent / 100.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
