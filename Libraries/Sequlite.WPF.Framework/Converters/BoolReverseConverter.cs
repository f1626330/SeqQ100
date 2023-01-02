using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BoolReverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(bool))
            {
                return !((bool)value);
            }
            else return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(bool))
            {
                return !((bool)value);
            }
            else return false;
        }
    }

    public class IsFalseMutipleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var isFalse = values.All(i => (i != null) &&
            (i as bool? == false || i.ToString() == "false"));
            return isFalse;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
