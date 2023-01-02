using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BooleanToVisibilityNegateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is Boolean)
            {
                return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
