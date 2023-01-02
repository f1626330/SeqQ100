using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace Sequlite.WPF.Framework
{
    public class ReverseBooleanToVisibilityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Where(x => x is bool).Cast<bool>().Any(x => x))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[0];
        }
    }
}

