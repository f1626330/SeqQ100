using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class RealGammaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double val = 0;
            if (value != null)
                val = System.Convert.ToDouble(value);
            val = Math.Round(Math.Pow(10, val), 3);

            return val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
