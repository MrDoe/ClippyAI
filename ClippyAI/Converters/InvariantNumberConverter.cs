using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ClippyAI.Converters;

/// <summary>
/// Formats a number with the invariant culture.
/// </summary>
/// <remarks>
/// Technical values such as the similarity distance should read the same in
/// every language: "0.42", not "0,42" in German. Localized prose is
/// translated, machine values are not.
/// </remarks>
public class InvariantNumberConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value is IFormattable formattable
            ? formattable.ToString("0.##", CultureInfo.InvariantCulture)
            : value.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
