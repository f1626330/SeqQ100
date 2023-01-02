using Microsoft.Win32.SafeHandles;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Sequlite.WPF.Framework
{
    public class ViewModelBase : INotifyPropertyChanged, IDisposable
    {

        //bool disposed = false;
        //SafeHandle handle = new SafeFileHandle(IntPtr.Zero, true);

        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            //if (disposed)
            //    return;

            //if (disposing)
            //{
            //    handle.Dispose();

            //}
           

            //disposed = true;
        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, bool bForeedToRaiseEvent) => InternalSetProperty<T>(ref storage, value, bForeedToRaiseEvent);

         bool InternalSetProperty<T>(ref T storage, T value, bool bForeedToRaiseEvent,
            [CallerMemberName]string propertyName = null, bool raiseCanExecuteChanged = false)
        {
            bool notEqual;
            notEqual = !EqualityComparer<T>.Default.Equals(storage, value);
            if (notEqual)
            {
                storage = value;
            }
            
            if (notEqual || bForeedToRaiseEvent)
            {
                RaisePropertyChanged(propertyName);
            }

            if (raiseCanExecuteChanged && notEqual)
            {
                CommandManager.InvalidateRequerySuggested();
            }
            return notEqual; 
        }

        protected bool SetProperty<T>(ref T storage, T value,
           [CallerMemberName]string propertyName = null, bool raiseCanExecuteChanged = false) => InternalSetProperty<T>(ref storage, value, false, propertyName, raiseCanExecuteChanged);



        protected void Dispatch(Action f)
        {
            try
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished)
                        return;

                    if (Application.Current.Dispatcher.CheckAccess())
                        f();
                    else
                        Application.Current.Dispatcher.Invoke(f);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException ex)
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    if (!(Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished))
                    {
                        LogException("Exception: ", ex);

                    }
                }
                return;
            }
            catch (Exception ex)
            {
                LogException("Exception: ", ex);

                return;
            }
        }

        protected void Dispatch<T>(Action<T> f, T args)
        {
            try
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished)
                        return;

                    if (Application.Current.Dispatcher.CheckAccess())
                        f(args);
                    else
                        Application.Current.Dispatcher.Invoke(f, args);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException ex)
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    if (!(Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished))
                    {
                        LogException("Exception: ", ex);

                    }
                }
                return;
            }
            catch (Exception ex)
            {
                LogException("Exception: ", ex);

                return;
            }
        }

        protected Dispatcher TheDispatcher => Application.Current?.Dispatcher;

        public event PropertyChangedEventHandler PropertyChanged;
        private void LogException(string message, Exception ex)
        {
            if (Console.Out != null)
            {

                string logMsg = message  + ex.ToString();

                Console.Out.WriteLine(logMsg);
            }
        }
    }
}
