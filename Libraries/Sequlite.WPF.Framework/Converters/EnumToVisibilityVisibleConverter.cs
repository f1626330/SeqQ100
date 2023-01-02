using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class EnumToVisibilityVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value != null) ? (value.Equals(parameter) ? Visibility.Visible : Visibility.Hidden) : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
