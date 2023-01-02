using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public abstract class BaseModel : INotifyPropertyChanged, IDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T storage, T value,
           [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }


        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        protected T Get<T>([CallerMemberName] string name = null)
        {
            Debug.Assert(name != null, "name != null");
            object value = null;
            if (_properties.TryGetValue(name, out value))
                return value == null ? default(T) : (T)value;
            return default(T);
        }

        /// <summary>
        /// Sets the value of a property
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="name"></param>
        /// <remarks>Use this overload when implicitly naming the property</remarks>
        protected void Set<T>(T value, [CallerMemberName] string name = null)
        {
            Debug.Assert(name != null, "name != null");
            if (Equals(value, Get<T>(name)))
                return;
            _properties[name] = value;
            OnPropertyChanged(name);
        }

        bool _HasError;
        public bool HasError
        {
            get => _HasError;
            set
            {
                SetProperty(ref _HasError, value);
            }
        }

        public string Error
        {
            get { return null; }
        }

        public virtual string this[string columnName] => string.Empty;

        protected Dictionary<string, bool> _ErrorBits = new Dictionary<string, bool>();
        public void UpdateErrorBits(string name, bool hasError)
        {
            if (hasError)
            {
                if (_ErrorBits.ContainsKey(name))
                {
                    _ErrorBits[name] = true;
                }
                else
                {
                    _ErrorBits.Add(name, true);
                }
            }
            else
            {
                if (_ErrorBits.ContainsKey(name))
                {
                    _ErrorBits.Remove(name);
                }
            }

            HasError = (_ErrorBits.Count > 0);
        }
    }
}
