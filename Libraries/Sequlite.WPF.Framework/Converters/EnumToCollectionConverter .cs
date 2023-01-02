
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;

namespace Sequlite.WPF.Framework
{
    public static class EnumHelper
    {
        public static string Description(this Enum e)
        {
            return (e.GetType()
                     .GetField(e.ToString())
                     .GetCustomAttributes(typeof(DescriptionAttribute), false)
                     .FirstOrDefault() as DescriptionAttribute)?.Description ?? e.ToString();
        }

        public static DisplayAttribute GetDisplayAttributesFrom(this Enum enumValue)
        {
            return enumValue.GetType().GetMember(enumValue.ToString())
                           .First()
                           .GetCustomAttribute<DisplayAttribute>();
        }
    }

    [ValueConversion(typeof(Enum), typeof(IEnumerable<ValueDescription>))]
    public class EnumToCollectionConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Enum.GetValues(value.GetType())
                       .Cast<Enum>()
                       .Select(e => new ValueDescription() { Value = e, Description = e.Description() })
                       .ToList();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

       
    }


    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumToNameConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //return Enum.GetValues(value.GetType())
            //           .Cast<Enum>()
            //           .Select(e => new ValueDescription() { Value = e, Description = e.Description() })
            //           .ToList();
            if (value != null)
            {
                return ((Enum)value).Description();
            }
            else
            {
                return "";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }


    }


    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumToDisplayNameConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            if (value != null)
            {
              return ((Enum)value).GetDisplayAttributesFrom().Name;
            }
            else
            {
                return "";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }


    }

    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumToDisplayDescriptionConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value != null)
            {
                return ((Enum)value).GetDisplayAttributesFrom().Description;
            }
            else
            {
                return "";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }


    }
}
