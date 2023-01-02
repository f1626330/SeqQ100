using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    //OR converter
    public class EnumToVisibilityMultiConverter : IValueConverter
    {
        //The converter will return Visibility.Visible if the parameter argument contains a string equal to value, 
        //Visible.Collapsed otherwise.
        //example: 
        //   <Grid Visibility="{Binding CurrentResponse,
        //     Converter={StaticResource EnumToVisibilityMultiConverter},
        //     ConverterParameter='Invalid, NotFound'}">
        //     <TextBlock Text = "Invalid or NotFound" ></ TextBlock >
        //   </Grid>
        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value == null || parameter == null || !(value is Enum))
                return Visibility.Collapsed;

            var currentState = value.ToString();
            var stateStrings = parameter.ToString();
            var found = false;

            foreach (var state in stateStrings.Split(','))
            {
                found = (currentState == state.Trim());

                if (found)
                    break;
            }

            return found ? Visibility.Visible : Visibility.Collapsed;
        }

       
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
