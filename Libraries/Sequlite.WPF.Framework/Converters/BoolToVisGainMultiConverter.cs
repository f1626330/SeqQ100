using System;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BoolToVisGainMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var retval = Visibility.Collapsed;
            if (value[0] is Boolean && value[1] is Boolean)
            {
                if ((bool)value[0] && !(bool)value[1])
                {
                    retval = Visibility.Visible;
                }
                else if ((bool)value[0] && (bool)value[1])
                {
                    retval = Visibility.Collapsed;
                }
            }

            return retval;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
