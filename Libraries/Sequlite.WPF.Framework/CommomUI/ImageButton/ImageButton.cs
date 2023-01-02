using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Sequlite.WPF.Framework
{
    public class ImageButton : Button
    {
        public static readonly DependencyProperty ImageContentProperty = DependencyProperty.Register("ImageContent", typeof(string), typeof(ImageButton));
        public string ImageContent
        {
            get
            {
                return (string)GetValue(ImageContentProperty);
            }
            set
            {
                SetValue(ImageContentProperty, value);
            }
        }
    }
}
