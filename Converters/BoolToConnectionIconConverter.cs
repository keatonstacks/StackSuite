using System.Globalization;
using System.Windows.Data;
namespace StackSuite.Converters;
public class BoolToConnectionIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? "LanConnect" : "LanDisconnect";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}