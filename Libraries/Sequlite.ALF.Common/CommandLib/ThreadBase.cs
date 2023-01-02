using System;
using System.Threading;

namespace Sequlite.ALF.Common
{
    public class ThreadBase
    {
        /// <summary>
        /// Command completed delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="exitState"></param>
        public delegate void CommandCompletedHandler(ThreadBase sender, ThreadExitStat exitState);

        /// <summary>
        /// Command completed event.
        /// </summary>
        public event CommandCompletedHandler Completed;
        public event EventHandler<EventArgs> InProgress;
        public enum ThreadExitStat { Abort, Error, None };

        #region Protected field/data...

        protected Thread _ThreadHandle;
        protected ThreadExitStat _ExitStat = ThreadExitStat.None;
        protected AutoResetEvent _AutoResetEvent = new AutoResetEvent(false);
        protected Object _CommandSyncObject = new Object();
        protected bool _IsOutOfMemory = false;
        protected Exception _Error = null;
        protected bool _simulation = false; // change to true if you want to simulate
        protected bool _postprocessing = true; // change to true if you want to process the data after the recipe is run
        #endregion

        #region Public properties...

        public ThreadExitStat ExitStat
        {
            get { return _ExitStat; }
            set { _ExitStat = value; }
        }
        public bool IsAlive
        {
            get { return _ThreadHandle.IsAlive; }
        }
        public Thread ThreadHandle
        {
            get { return _ThreadHandle; }
        }
        public bool IsOutOfMemory
        {
            get { return _IsOutOfMemory; }
            set { _IsOutOfMemory = value; }
        }
        public Exception Error
        {
            get { return _Error; }
            set { _Error = value; }
        }
        public bool IsSimulationMode
        {
            get { return _simulation; }
            set { _simulation = value; }
        }
        
        public string Name { get; set; }

        #endregion

        #region Virtual functions...

        public virtual void ThreadFunction()
        {
        }
        public virtual void Initialize()
        {
        }
        public virtual void Finish()
        {
        }
        public virtual void AbortWork()
        {
        }

        /// <summary>
        /// This method is provided to simulate the task instead of actually performing the  task.
        /// </summary>
        protected virtual void SimulateThreadFunction()
        {
        }

        #endregion

        public ThreadBase()
        {
        }

        void ThreadProc()
        {
            try
            {

                ThreadFunction();
            }
            catch (ThreadAbortException ex)
            {
                ExitStat = ThreadExitStat.Abort;
                Error = ex;
            }
            catch (OutOfMemoryException ex)
            {
                _IsOutOfMemory = true;
                Error = ex;
                ExitStat = ThreadExitStat.Error;
            }
            catch (Exception ex)
            {
                ExitStat = ThreadExitStat.Error;
                Error = ex;
            }
            finally
            {
                _AutoResetEvent.Set();

                // Do some clean up
                Finish();

                if (Completed != null)
                {
                    Completed(this, ExitStat);
                }
            }
        }

        public void Abort()
        {
            _ExitStat = ThreadExitStat.Abort;
            if (_ThreadHandle != null && _ThreadHandle.IsAlive)
            {
                AbortWork();
                //_ThreadHandle.Abort();
            }
        }

        public void Start()
        {
            if (_ThreadHandle != null && _ThreadHandle.IsAlive)
            {
                throw new InvalidOperationException("Command instance is already executing asynchronously.");
            }

            _AutoResetEvent.Reset();

            Initialize();

            _ThreadHandle = new System.Threading.Thread(new System.Threading.ThreadStart(ThreadProc));
            _ThreadHandle.Name = Name;
            _ThreadHandle.SetApartmentState(System.Threading.ApartmentState.STA);
            _ThreadHandle.Priority = ThreadPriority.Highest;
            _ThreadHandle.IsBackground = true;
            _ThreadHandle.Start();
        }

        public void Join()
        {
            if (_ThreadHandle != null && _ThreadHandle.IsAlive)
            {
                _ThreadHandle.Join();
            }
        }
        public void Stop()
        {
            if (_ThreadHandle != null && _ThreadHandle.IsAlive)
            {
                AbortWork();
                //_ThreadHandle.Abort();
            }
        }

        public  void WaitForCompleted(int waitMs = 100)
        {

            while (_ThreadHandle != null && _ThreadHandle.IsAlive)
            {
                if (InProgress != null)
                {
                    InProgress(this, null);
                }
                Thread.Sleep(waitMs);
            }
        }
    }
}
