using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            //Solution 1:
            return (value != null) ? value.Equals(parameter) : false;

            //Solution 2:
            /*string parameterString = parameter.ToString();
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value);*/

            //Solution 3:
            /*if (value != null && value.GetType().IsEnum)
                return (Enum.Equals(value, parameter));
            else
                return DependencyProperty.UnsetValue;*/
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return Binding.DoNothing;
            }
            else
            {
                //Solution 1:
                return value.Equals(true) ? parameter : Binding.DoNothing;
            }

            //Solution 2:
            /*string parameterString = parameter.ToString();
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            return Enum.Parse(targetType, parameterString);*/

            //Solution 3:
            /*if (value is bool && (bool)value)
                return parameter;
            else
                return DependencyProperty.UnsetValue;*/
        }
    }
}
