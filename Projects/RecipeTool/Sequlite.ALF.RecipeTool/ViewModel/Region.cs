using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    class Region: IValueConverter
    {
        string _lane = ""; // values 1,2,3,4 or Python list spec
        string _X =""; // values all floating point values and Python list spec, Units MM
        string _Y= "";// values all floating point values and Python list spec, Units MM
        int _regionIndex = -1; //either use region index or lane,x,y. -1 means no regionindex

        public string Lane { get => _lane; set => _lane = value; }
        public string X { get => _X; set => _X = value; }
        public string Y { get => _Y; set => _Y = value; }
        public int RegionIndex { get => _regionIndex; set => _regionIndex = value; }

        public override string ToString()
        {
            return _regionIndex.ToString();
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _regionIndex;
            if ((int)value == _regionIndex)
            {
                return true;
            }
            return false;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _regionIndex;
        }


        /// <summary>
        /// a region is a location on a flow cell
        /// </summary>
        public Region(int index = -1)
        {
            _regionIndex = index;
        }
    }
}
