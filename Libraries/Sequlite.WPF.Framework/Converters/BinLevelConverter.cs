using System;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class BinLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int iBinFactor = 0;
            string strBinningFactor = string.Empty;

            if (value != null && value is int)
            {
                iBinFactor = (int)value;

                strBinningFactor = iBinFactor.ToString() + "x" + iBinFactor.ToString();
            }
            return (iBinFactor > 0) ? strBinningFactor : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
