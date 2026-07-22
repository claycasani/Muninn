using System.Globalization;

namespace Muninn.Converters;

/// <summary>Returns true when a string is null or empty (inverse of StringNotEmptyConverter).</summary>
public class StringEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not string s || string.IsNullOrEmpty(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
