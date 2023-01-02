using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    /// <summary>
    /// Returns an empty string if the input value is zero. 
    /// </summary>
    public class ZeroToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int val = 0;
            if (value != null && value is int)
                val = (int)value;

            return (val > 0) ? val.ToString() : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
