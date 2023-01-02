using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class LogicalOrToVisibilityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool bResult = false;
            foreach (var val in values)
            {
                if (val is Boolean)
                {
                    if ((bool)val == true)
                    {
                        bResult = true;
                        break;
                    }
                }
            }

            return (bResult) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

}
