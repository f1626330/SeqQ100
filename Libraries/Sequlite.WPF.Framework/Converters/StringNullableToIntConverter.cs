using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class StringNullableToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string retVal = string.Empty;
            if (value != null)
                retVal = value.ToString();
            return retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string strVal = string.Empty;
            if (value != null)
                strVal = value.ToString();

            if (string.IsNullOrEmpty(strVal))
            {
                return 0;
            }
            else
            {
                return int.Parse(strVal);
            }
        }
    }
}


