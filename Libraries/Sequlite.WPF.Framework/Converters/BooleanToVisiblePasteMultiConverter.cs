using System;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BooleanToVisiblePasteMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var retval = Visibility.Hidden;

            // Expected input format: IsCropping, IsImageClipboard, IsCopyAndPasteAllowed
            if (value[0] is Boolean && value[1] is Boolean && value[2] is Boolean)
            {
                // !IsCropping && IsImageClipboard && IsCopyAndPasteAllowed
                if (!(bool)value[0] && (bool)value[1] && (bool)value[2])
                {
                    retval = Visibility.Visible;
                }
                else
                {
                    retval = Visibility.Collapsed;
                }
            }

            return retval;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
