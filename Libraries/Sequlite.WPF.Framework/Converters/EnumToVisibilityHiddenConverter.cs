using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class EnumToVisibilityHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value != null) ? (value.Equals(parameter) ? Visibility.Hidden : Visibility.Visible) : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
