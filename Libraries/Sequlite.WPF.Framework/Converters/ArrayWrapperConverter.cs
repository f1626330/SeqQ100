using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Sequlite.WPF.Framework
{
    public sealed class ArrayWrapperConverter : IValueConverter
    {
        private static readonly Type ArrayWrappingHelperType = typeof(ArrayWrappingHelper<>);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            Type valueType = value.GetType();
            if (!valueType.IsArray)
            {
                return DependencyProperty.UnsetValue;
            }

            Type elementType = valueType.GetElementType();
            Type specificType = ArrayWrappingHelperType.MakeGenericType(elementType);

            IEnumerable wrappingHelper = (IEnumerable)Activator.CreateInstance(specificType, value);
            return wrappingHelper;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class ArrayWrappingHelper<TValue> : IEnumerable
    {
        private readonly TValue[] _array;

        public ArrayWrappingHelper(object array)
        {
            _array = (TValue[])array;
        }

        public IEnumerator GetEnumerator()
        {
            return _array.Select((item, index) => new ArrayItemWrapper<TValue>(_array, index)).GetEnumerator();
        }
    }

    public class ArrayItemWrapper<TValue>
    {
        private readonly TValue[] _array;
        private readonly int _index;

        public int Index
        {
            get { return _index; }
        }

        public TValue Value
        {
            get { return _array[_index]; }
            set { _array[_index] = value; }
        }

        public ArrayItemWrapper(TValue[] array, int index)
        {
            _array = array;
            _index = index;
        }
    }
}
