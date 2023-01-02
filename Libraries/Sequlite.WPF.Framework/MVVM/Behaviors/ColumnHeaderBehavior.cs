using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace Sequlite.WPF.Framework
{
    public class ColumnHeaderBehavior : Behavior<DataGrid>
    {
        protected override void OnAttached()
        {
            AssociatedObject.AutoGeneratingColumn +=
                new EventHandler<DataGridAutoGeneratingColumnEventArgs>(OnAutoGeneratingColumn);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.AutoGeneratingColumn -=
                new EventHandler<DataGridAutoGeneratingColumnEventArgs>(OnAutoGeneratingColumn);
        }

        protected void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string displayName = GetPropertyDisplayName(e.PropertyDescriptor);
            if (!string.IsNullOrEmpty(displayName))
            {
                e.Column.Header = displayName;
            }
            else
            {
                e.Column.Header = e.PropertyName;
                //e.Cancel = true;
            }
        }

        protected static string GetPropertyDisplayName(object descriptor)
        {
            PropertyDescriptor pd = descriptor as PropertyDescriptor;
            if (pd != null)
            {
                DisplayNameAttribute attr = pd.Attributes[typeof(DisplayNameAttribute)] as DisplayNameAttribute;
                if ((attr != null) && (attr != DisplayNameAttribute.Default))
                {
                    return attr.DisplayName;
                }
            }
            else
            {
                PropertyInfo pi = descriptor as PropertyInfo;
                if (pi != null)
                {
                    Object[] attrs = pi.GetCustomAttributes(typeof(DisplayNameAttribute), true);
                    foreach (var att in attrs)
                    {
                        DisplayNameAttribute attribute = att as DisplayNameAttribute;
                        if ((attribute != null) && (attribute != DisplayNameAttribute.Default))
                        {
                            return attribute.DisplayName;
                        }
                    }
                }
            }
            return null;
        }
    }
}
