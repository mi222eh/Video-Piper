using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VideoPiper.Converters;

/// <summary>
/// Turns a cached thumbnail path or a remote thumbnail URL into an image source.
/// Returns null for empty values so the row falls back to its glyph.
/// </summary>
public sealed class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(text));
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
