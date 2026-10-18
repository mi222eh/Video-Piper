using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using VideoPiper.Models;

namespace VideoPiper.Converters;

/// <summary>
/// Maps <see cref="ItemStatus"/> to a state brush using the Vp* design tokens from App.xaml
/// so queue rows stay coherent in both themes.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var key = value switch
        {
            ItemStatus.Downloading => "VpWarningBrush",
            ItemStatus.Complete => "VpSuccessBrush",
            ItemStatus.Failed => "VpErrorBrush",
            _ => "TextFillColorSecondaryBrush",
        };
        return (Application.Current.Resources[key] as Brush) ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
