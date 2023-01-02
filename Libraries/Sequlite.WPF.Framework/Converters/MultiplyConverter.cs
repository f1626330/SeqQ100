using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            if (parameter == null)
                parameter = 1;

            double number;
            double coefficient;

            if (double.TryParse(value.ToString(), out number) && double.TryParse(parameter.ToString(), out coefficient))
            {
                return number * coefficient;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
