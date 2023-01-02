using System;
using System.Diagnostics;
using System.Threading;

namespace Sequlite.ALF.Common
{
    public delegate void CommandCompletedHandler(BaseCommand sender, ExecutionState execState);
    /// <summary>
    /// Abstract base command for all commands.
    /// </summary>
    /// <remarks>
    ///   <para>Copyright: Created by Tony Jenniges, 2008-2009, All rights reserved.</para>
    ///   <para>Contact:   tonyjenniges@yahoo.com</para>
    /// </remarks>
    public abstract class BaseCommand
    {
        protected string _CommandName = null;
        protected int _msTimeoutTime = -1;
        protected CommandMediator _OwningMediator = null;
        protected BaseCommand _Parent = null;
        protected Guid _Guid = Guid.NewGuid();
        protected AutoResetEvent _AutoResetEvent = new AutoResetEvent(false);
        protected Thread _CommandThread = null;

        protected SynchronizationContext _SyncContext = null;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("BaseCommon");

        /// <summary>
        /// The command's execution info.
        /// </summary>
        internal ExecutionState _ExecutionInfo;

        /// <summary>
        /// Command completed event.
        /// </summary>
        public event CommandCompletedHandler Completed;


        #region public string Name
        /// <summary>
        /// Get the unique identifying name of the command.
        /// </summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_CommandName))
                {
                    return this.GetType().Name;
                }
                else
                {
                    return _CommandName;
                }
            }
        }
        #endregion

        #region public int TimeOut
        /// <summary>
        /// Get/set the command timeout time in milliseconds. If the command has not completed
        /// it's task in this time then it will time out. A value of -1 means the timeout time
        /// is infinite.
        /// </summary>
        public int TimeOut
        {
            get { return _msTimeoutTime; }
            set { _msTimeoutTime = Math.Max(-1, value); }
        }
        #endregion

        #region public Guid UniqueID
        /// <summary>
        /// Get the unique identifier for this command. Every object instance of this 
        /// command type have a unique ID (Guid).
        /// </summary>
        public Guid UniqueID
        {
            get { return _Guid; }
        }
        #endregion

        #region public BaseCommand Parent
        /// <summary>
        /// Get the parent command (if null, this command is not a child command).
        /// </summary>
        public BaseCommand Parent
        {
            get { return _Parent; }
            internal set { _Parent = value; }
        }
        #endregion

        #region public CommandMediator OwningMediator
        /// <summary>
        /// Get the owning <see cref="CommandMediator"/> object instance.
        /// </summary>
        public CommandMediator OwningMediator
        {
            get { return _OwningMediator; }
            internal set { _OwningMediator = value; }
        }
        #endregion


        #region internal AutoResetEvent AutoReset
        /// <summary>
        /// Used to notify waiting threads that this command's task has completed.
        /// </summary>
        internal AutoResetEvent AutoReset
        {
            get { return _AutoResetEvent; }
        }
        #endregion


        /// <summary>
        /// Execute the command's task which is implemented by the abstract method <see cref="DoTask"/>.
        /// </summary>
        /// <remarks>
        /// Do not call this method directly from code, the <see cref="CommandMediator"/>
        /// object will invoke this method.
        /// </remarks>
        internal void Execute()
        {
            if (_CommandThread != null && _CommandThread.IsAlive)
            {
                throw new InvalidOperationException("Command instance is already executing asynchronously.");
            }

            _AutoResetEvent.Reset();

            ThreadStart threadStart = new ThreadStart(ExecuteWorker);

            _CommandThread = new Thread(threadStart);
            _CommandThread.Name = "Base cmd worker"; // added name
            _CommandThread.IsBackground = true;
            _CommandThread.Start();
        }

        /// <summary>
        /// stop the thread
        /// </summary>
        public void Stop()
        {
            if (_CommandThread != null && _CommandThread.IsAlive)
            {
                _CommandThread.Abort();
            }
        }
        /// <summary>
        /// Cancel the command. This method calls Thread.Abort on the internal thread
        /// which raises a ThreadAbortException on the command's thread.
        /// </summary>
        public void Cancel()
        {
            if (_CommandThread != null && _CommandThread.IsAlive)
            {
                try
                {
                    CleanUpOnAbort();
                }
                catch (Exception ex)
                {
                    _OwningMediator.LogException("Exception caught in command \"" + this.Name + "\" CleanUpOnAbort method.", ex);
                }

                _CommandThread.Abort();
            }
        }

        /// <summary>
        /// Create the <see cref="ExecutionState"/> tracking object for this command. 
        /// </summary>
        /// <returns></returns>
        internal virtual ExecutionState CreateExecutionState(BaseCommand parent)
        {
            _ExecutionInfo = new ExecutionState(this, parent);

            return _ExecutionInfo;
        }

        /// <summary>
        /// The worker thread method that calls the <see cref="DoTask"/> protected abstract method. This wrapper 
        /// method takes care of profiling commands, logging to the <see cref="CommandInvoker"/>, and
        /// handling ThreadAbortExceptions and other exceptions while determining the <see cref="ExecutionState"/>
        /// of the command.
        /// </summary>
        internal void ExecuteWorker()
        {
            try
            {
                _ExecutionInfo.Status = ExecutionState.ExecutionStatus.Executing;
                _ExecutionInfo.ThreadID = Thread.CurrentThread.ManagedThreadId;
                _ExecutionInfo.StartTime = DateTime.Now;

                if (this._OwningMediator != null && _OwningMediator.ProfileCommandPerformance)
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    if (_OwningMediator.IsSimulationMode)
                    {
                        SimulateDoTask();
                    }
                    else
                    {
                        DoTask();
                    }

                    if (_ExecutionInfo.Status != ExecutionState.ExecutionStatus.StoppedOnException ||
                        _ExecutionInfo.Status != ExecutionState.ExecutionStatus.Cancelled)
                    {
                        _ExecutionInfo.Status = ExecutionState.ExecutionStatus.Completed;
                    }

                    stopWatch.Stop();
                    _OwningMediator.LogCommandPerformance(stopWatch.ElapsedMilliseconds, this.Name);
                }
                else
                {
                    if (_OwningMediator.IsSimulationMode)
                    {
                        SimulateDoTask();
                    }
                    else
                    {
                        DoTask();
                    }

                    if (_ExecutionInfo.Status != ExecutionState.ExecutionStatus.StoppedOnException ||
                        _ExecutionInfo.Status != ExecutionState.ExecutionStatus.Cancelled)
                    {
                        _ExecutionInfo.Status = ExecutionState.ExecutionStatus.Completed;
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                Logger.LogError(ex.ToString());
                if (!CheckForStoppedOnChildException())
                {
                    _ExecutionInfo.Status = ExecutionState.ExecutionStatus.Cancelled;
                    if (this._OwningMediator != null)
                    {
                        _OwningMediator.LogMessage("Command \"" + this.Name + "\" cancelled (on ThreadAbortException).");
                    }
                }

                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _ExecutionInfo.Error = ex;
                _ExecutionInfo.Status = ExecutionState.ExecutionStatus.StoppedOnException;

                if (this._OwningMediator != null)
                {
                    _OwningMediator.LogException("Exception caught in command \"" + this.Name + "\"", ex);
                }

                if (this.Parent != null)
                {
                    this.Parent.Cancel();
                }
            }
            finally
            {
                _ExecutionInfo.CompletedTime = DateTime.Now;

                _AutoResetEvent.Set();

                if (Completed != null)
                {
                    ////Delegate[] Completed.GetInvocationList();
                    //if (Completed.Target is DispatcherObject)
                    //{
                    //   //
                    //   // This is essential what the DispatcherSynchronizationContext implements:
                    //   //
                    //   ((DispatcherObject)Completed.Target).Dispatcher.Invoke(DispatcherPriority.Normal, Completed, state);
                    //}
                    //else
                    //{
                    Completed(this, _ExecutionInfo);
                    //}
                }
            }
        }

        /// <summary>
        /// Override this method to implement the code that implements the command's task.
        /// It is not necessary to add exception handling in this method because there
        /// is a wrapper method that calls this method that handles all exceptions. If you
        /// do add exception handling be sure to rethrow any caught exceptions.
        /// </summary>
        protected abstract void DoTask();

        /// <summary>
        /// This method is called before a command is aborted due to calling "Cancel"
        /// or because of exception.
        /// </summary>
        protected virtual void CleanUpOnAbort()
        {

        }

        /// <summary>
        /// This method is provided to simulate the task instead of actually performing the
        /// task. For commands that talk to hardware this method would be useful while 
        /// "connected" to a simulator of the actual hardware.
        /// </summary>
        protected virtual void SimulateDoTask()
        {
            Thread.Sleep(1000);
        }


        /// <summary>
        /// Check to see if the reason for the ThreadAbortException is due to a child command being
        /// "StoppedOnException". If so, set this command's <see cref="ExecutionState"/> to
        /// "StoppedOnException" and if this command has a parent then abort the parent command.
        /// </summary>
        /// <returns>
        /// True if the reason for the ThreadAbortException was due to a child being
        /// "StoppedOnException", returns false otherwise.
        /// </returns>
        internal bool CheckForStoppedOnChildException()
        {
            bool bChildStoppedOnException = false;

            foreach (ExecutionState childState in this._ExecutionInfo.Children)
            {
                if (childState.Status == ExecutionState.ExecutionStatus.StoppedOnException)
                {
                    this._ExecutionInfo.Status = ExecutionState.ExecutionStatus.StoppedOnException;
                    this._ExecutionInfo.Error = childState.Error;

                    bChildStoppedOnException = true;
                    break;
                }
            }

            if (bChildStoppedOnException)
            {
                if (this.Parent != null)
                {
                    this.Parent.Cancel();
                }
            }

            return bChildStoppedOnException;
        }


        /// <summary>
        /// This method is called by the <see cref="CommandMediator"/> (and by any <see cref="ParallelCommand"/>
        /// for each child) to make sure that each command's thread is available for garbage collection.
        /// </summary>
        internal virtual void NullCommandThread()
        {
            _CommandThread = null;
        }

    }

}



