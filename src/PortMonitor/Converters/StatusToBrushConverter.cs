using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PortMonitor.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Listen" => new SolidColorBrush(Color.FromRgb(220, 255, 220)),
            "Established" => new SolidColorBrush(Color.FromRgb(220, 235, 255)),
            "TimeWait" => new SolidColorBrush(Color.FromRgb(255, 255, 220)),
            "CloseWait" => new SolidColorBrush(Color.FromRgb(255, 230, 220)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
