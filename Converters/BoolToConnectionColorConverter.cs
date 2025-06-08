using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StackSuite.Converters;
public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Brushes.LimeGreen : Brushes.OrangeRed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}