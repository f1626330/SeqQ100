using System;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace Sequlite.ALF.Common
{
    
    public class AutoSetQueue<T> : Queue<T>, IDisposable
    {
        private object _Lock = new object();
        private ManualResetEvent _Event = new ManualResetEvent(false);
        protected bool _ContWait = true;

        #region <CTOR>
        public AutoSetQueue()
            : base()
        {
        }

        public AutoSetQueue(ICollection<T> collection)
            : base(collection)
        {
        }

        public AutoSetQueue(Int32 capacity)
            : base(capacity)
        {
        }

        #endregion </CTOR>

        public new void Clear()
        {
            lock (_Lock)
            {
                base.Clear();
            }
        }

        /// <exception cref="System.ObjectDisposedException"></exception>
        public new void Enqueue(T item)
        {
            lock (_Lock)
            {
                base.Enqueue(item);
            }
            _Event.Set();
        }

        /// <exception cref="System.ObjectDisposedException"></exception>
        public void EnqueueItems(T[] items)
        {
            lock (_Lock)
            {
                foreach (T item in items)
                {
                    base.Enqueue(item);
                }
            }
            _Event.Set();
        }

        /// <exception cref="System.ObjectDisposedException"></exception>
        public T WaitForNextItem()
        {
            return WaitForNextItem(System.Threading.Timeout.Infinite);
        }

        /// <exception cref="System.ObjectDisposedException"></exception>
        public T WaitForNextItem(int timeoutMilliSeconds)
        {
            while (_ContWait)
            {
                //lock (_Lock)
                //{
                    if (this.Count > 0)
                    {
                        T ret = default(T);
                        lock (_Lock)
                        {
                            ret = base.Dequeue();
                        }
                        return ret;
                    }

                    if (this.Count <= 0)
                    {
                         _Event.Reset();
                    }
                //}

                if (!_Event.WaitOne(timeoutMilliSeconds, false))
                {
                    throw new AutoSetQueueTimeOutException(timeoutMilliSeconds);
                }
            }

            return default(T); //For a reference-type, it returns null
        }

        public T[] WaitForNextItems()
        {
            return WaitForNextItems(System.Threading.Timeout.Infinite);
        }

        public T[] WaitForNextItems(int timeoutMilliSeconds)
        {
            T[] items;
            while (_ContWait)
            {
                //lock (_Lock)
                //{
                    if (this.Count > 0)
                    {
                        items = base.ToArray();
                        Clear();
                        _Event.Reset();
                        return items;
                    }
                //}

                if (!_Event.WaitOne(timeoutMilliSeconds, false))
                {
                    throw new AutoSetQueueTimeOutException(timeoutMilliSeconds);
                }
            }
            return default(T[]); //For a reference-type, it returns null
        }

        public bool HasItems
        {
            get
            {
                lock (_Lock)
                {
                    return base.Count > 0;
                }
            }
        }

        public int QueueCount
        {
            get
            {
                lock (_Lock)
                {
                    return base.Count ;
                }
            }
        }

        public void Dispose()
        {
            if (!_ContWait)
                return;

            lock (_Lock)
            {
                if (_ContWait)
                {
                    _ContWait = false;
                    _Event.Set();
                    _Event.Close();
                }
            }
        }
    }

    public class AutoSetQueueTimeOutException : Exception
    {
        public AutoSetQueueTimeOutException(int timeoutMilliSeconds)
            : base("Timeout(millisecs): " + timeoutMilliSeconds.ToString(CultureInfo.InvariantCulture))
        {
        }
    }
}
