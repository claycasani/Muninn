using System.Globalization;

namespace Muninn.Converters;

/// <summary>Returns TextDecorations.Strikethrough when true, None when false.</summary>
public class BoolToStrikethroughConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TextDecorations.Strikethrough : TextDecorations.None;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
