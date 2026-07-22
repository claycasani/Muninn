using System.Globalization;

namespace Muninn.Converters;

/// <summary>
/// BoolToTertiaryColorConverter  — ColorTextTertiary when true, ColorTextPrimary when false.
/// BoolToAccentSeparatorConverter — ColorAccent stroke when true, ColorSeparator when false.
/// BoolToAccentTransparentConverter — ColorAccent fill when true, Transparent when false.
/// </summary>
public class BoolToTertiaryColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCompleted = value is true;
        return Application.Current!.Resources.TryGetValue(
            isCompleted ? "ColorTextTertiary" : "ColorTextPrimary", out var color)
            ? color
            : Colors.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToAccentSeparatorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCompleted = value is true;
        return Application.Current!.Resources.TryGetValue(
            isCompleted ? "ColorAccent" : "ColorSeparator", out var color)
            ? color
            : Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToAccentTransparentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isCompleted = value is true;
        return Application.Current!.Resources.TryGetValue(
            isCompleted ? "ColorAccent" : "ColorSurface", out var color)
            ? color
            : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
