using System;
using System.Globalization;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    /// <summary>
    /// Format filter, light, and focus converter
    /// </summary>
    public class FilterLightFocusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            //int iSrcCh1 = (int)values[0];
            //int iSrcCh2 = (int)values[1];
            //int iSrcCh3 = (int)values[2];

            int iSrcCh1 = 0;
            int iSrcCh2 = 0;
            int iSrcCh3 = 0;
            string captureType = string.Empty;
            string formattedSrc = string.Empty;

            if (values != null)
            {
                if (values[0] != null)
                    Int32.TryParse(values[0].ToString(), out iSrcCh1);
                if (values[1] != null)
                    Int32.TryParse(values[1].ToString(), out iSrcCh2);
                if (values[2] != null)
                    Int32.TryParse(values[2].ToString(), out iSrcCh3);
                if (values[3] != null)
                    captureType = values[3].ToString();
            }

            if (iSrcCh2 == 0 && iSrcCh3 == 0)
            {
                // grayscale image

                if (iSrcCh1 > 0)
                {
                    formattedSrc = iSrcCh1.ToString();
                }
            }
            else
            {
                // rgb image

                string strSrcCh1 = (iSrcCh1 > 0) ? string.Format("{0}", iSrcCh1) : "--";
                string strSrcCh2 = (iSrcCh2 > 0) ? string.Format(" / {0}", iSrcCh2) : " / --";
                string strSrcCh3 = string.Empty;

                if (captureType.Equals("RGB", StringComparison.InvariantCultureIgnoreCase))
                {
                    strSrcCh3 = (iSrcCh3 > 0) ? string.Format(" / {0}", iSrcCh3) : " / --";
                }
                else
                {
                    strSrcCh3 = (iSrcCh3 > 0) ? string.Format(" / {0}", iSrcCh3) : "";
                }

                formattedSrc = string.Format("{0}{1}{2}", strSrcCh1, strSrcCh2, strSrcCh3);
            }

            return formattedSrc;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }

    }

}
