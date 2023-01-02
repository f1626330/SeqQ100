using System;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BooleanToHiddenNegateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var boolVal = false;
            if (value is bool)
            {
                boolVal = (bool)value;
            }

            return boolVal ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }
}


