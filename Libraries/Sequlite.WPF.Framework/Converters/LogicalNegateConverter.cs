using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{

    public class LogicalNegateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool val = false;
            if (value != null && value is bool)
                val = (bool)value;

            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return true;
        }
    }
}
