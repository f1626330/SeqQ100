using System;
using System.Globalization;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public class ExposureTimeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            //double dExpCh1 = (double)values[0];
            //double dExpCh2 = (double)values[1];
            //double dExpCh3 = (double)values[2];

            double dExpCh1 = 0.0;
            double dExpCh2 = 0.0;
            double dExpCh3 = 0.0;
            string captureType = string.Empty;

            if (values != null)
            {
                if (values[0] != null)
                    double.TryParse(values[0].ToString(), out dExpCh1);
                if (values[1] != null)
                    double.TryParse(values[1].ToString(), out dExpCh2);
                if (values[2] != null)
                    double.TryParse(values[2].ToString(), out dExpCh3);
                if (values[3] != null)
                    captureType = values[3].ToString();
            }

            string strExposureTime = string.Empty;

            if (dExpCh2 == 0 && dExpCh3 == 0)
            {
                //
                // Grayscale image
                //
                strExposureTime = FormatExposureTime(dExpCh1);
            }
            else
            {
                //
                // RGB image
                //
                string strExpCh1 = (dExpCh1 > 0) ? FormatExposureTime(dExpCh1) : "--";
                string strExpCh2 = (dExpCh2 > 0) ? " / " + FormatExposureTime(dExpCh2) : " / --";
                string strExpCh3 = string.Empty;
                if (captureType.Equals("RGB", StringComparison.InvariantCultureIgnoreCase))
                {
                    strExpCh3 = (dExpCh3 > 0) ? " / " + FormatExposureTime(dExpCh3) : " / --";
                }
                else
                {
                    strExpCh3 = (dExpCh3 > 0) ? " / " + FormatExposureTime(dExpCh3) : "";
                }

                strExposureTime = string.Format("{0}{1}{2}", strExpCh1, strExpCh2, strExpCh3);
            }

            return strExposureTime;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Convert exposure to minutes, seconds, and milliseconds
        /// </summary>
        /// <param name="dExposureTime">Exposure time in seconds</param>
        /// <returns>Formatted exposure time</returns>
        private string FormatExposureTime(double dExposureTime)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(dExposureTime);

            string strMin = (timeSpan.Minutes > 0) ? string.Format("{0}m", timeSpan.Minutes) : string.Empty;
            string strSec = (timeSpan.Seconds > 0) ? string.Format("{0}s", timeSpan.Seconds) : string.Empty;
            string strMsec = (timeSpan.Milliseconds > 0) ? string.Format("{0}ms", timeSpan.Milliseconds) : string.Empty;
            string strExposureTime = string.Format("{0}{1}{2}", strMin, strSec, strMsec);
            return strExposureTime;
        }

    }

}
