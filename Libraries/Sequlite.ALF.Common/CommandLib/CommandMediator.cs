using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;


namespace Sequlite.ALF.Common
{

    #region public enum  CommandMediatorState
    /// <summary>
    /// 
    /// </summary>
    public enum CommandMediatorState
    {
        ///<summary>
        ///CommandMediator is idle and not processing commands.
        ///</summary>
        Idle,
        ///<summary>
        ///CommandMediator is busy processing commands.
        ///</summary>
        Busy
    }
    #endregion


    public delegate void ExceptionDelegate(CommandMediator sender, Exception handledException);
    public delegate void StateChangedDelegate(CommandMediator sender, CommandMediatorState state);
    public delegate void StatusUpdateDelegate(CommandMediator sender, string status);


    /// <summary>
    /// Implementation of the CommandMediator class. This class is purposed to be a SPOT 
    /// (Single Point Of Truth) utility that executes and profiles (optionally) commands. 
    /// This class provides a single point for serial/parallel commands, general logging, command logging 
    /// and exception handling/logging. It also provides events to the UI layer that notify it when commands 
    /// are running and completed.
    /// </summary>
    /// <remarks>
    /// <remarks>
    ///   <para>Copyright: Created by Tony Jenniges, 2008-2009, All rights reserved.</para>
    ///   <para>Contact:   tonyjenniges@yahoo.com</para>
    /// </remarks>   ///   <para></para>
    ///   <list type="bullet">
    ///     <listheader>Advantages of CommandMediator</listheader>
    ///     <item><description>SPOT (Single Point Of Truth) utility that can be used on multiple projects (and battle tested and hardened).</description>
    ///     <item><description>Common point for application logging</description></item>
    ///     <item><description>Common point for application exception handling</description></item>
    ///     <item><description>Command performance profiling and reporting</description></item>
    ///     <item><description>Multi-threaded command execution.</description></item>
    ///     <item><description>Less implementation code in GUI (i.e., 'thin' client UI)</description></item>
    /// </list>
    /// </remarks>
    public class CommandMediator
    {
        protected Dictionary<string, object> _SupportedResources = new Dictionary<string, object>();

        protected string _MediatorName = "CommandMediator";
        protected CommandMediatorState _State = CommandMediatorState.Idle;
        protected ISeqLog _Logger = null;
        protected bool _IsSimulationMode = false;
        protected BaseCommand _ExecutingCommand = null;
        protected ExecutionState _ExecutingCommandState = null;
        //
        // Command thread and synchronization objects:
        //
        protected Thread _MediatorThread = null;
        protected AutoResetEvent _ResetEvent;
        protected Object _CommandSyncObject = new Object();
        protected Object _LoggerSyncObject = new Object();
        protected Object _ResourcesSyncObject = new Object();
        protected Object _PerfProfilingSyncObject = new Object();

        protected bool _bProfileCommandPerformance = false;
        protected Dictionary<string, List<long>> _PerformanceTable = new Dictionary<string, List<long>>();
        protected char[] _TrimChars = new char[] { '\r', '\b' };

        /// <summary>
        /// Exception event is raised on command exception.
        /// </summary>
        public event ExceptionDelegate Exception;
        /// <summary>
        /// StateChanged event is raised when the state of the mediator changes.
        /// </summary>
        public event StateChangedDelegate StateChanged;


        #region public CommandMediatorState State
        /// <summary>
        /// Get the current state of the command mediator.
        /// </summary>
        public CommandMediatorState State
        {
            get { return _State; }
        }
        #endregion

        #region public string  Name
        /// <summary>
        /// Get the name of the command mediator. For derived command mediators set the name
        /// of the commamd mediator in the constructor of the derived class.
        /// </summary>
        public string Name
        {
            get { return _MediatorName; }
        }
        #endregion

        #region public bool    IsSimulationMode
        /// <summary>
        /// Get/set whether we are in simulation mode.
        /// </summary>
        public bool IsSimulationMode
        {
            get { return _IsSimulationMode; }
            set
            {
                lock (_CommandSyncObject)
                {
                    _IsSimulationMode = value;
                }
            }
        }
        #endregion

        #region public object  this[string resource]
        /// <summary>
        /// Get a supported resource by name. If the resource does not exist this indexer returns NULL.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public object this[string resource]
        {
            get
            {
                if (_SupportedResources.ContainsKey(resource))
                {
                    return _SupportedResources[resource];
                }

                return null;
            }
        }
        #endregion

        #region public bool    ProfileCommandPerformance
        /// <summary>
        /// Get/set the flag to start/stop command performance profiling. IF true,
        /// all executed commands profile their performance (See method
        /// <see cref="GetPerformanceProfileSnapShot"/>).
        /// </summary>
        /// <remarks>
        /// Call method <see cref="ResetCommandPerformanceProfiler"/> to clear
        /// all pervious performance profilings from the internal hash table.
        /// </remarks>
        public bool ProfileCommandPerformance
        {
            get { return _bProfileCommandPerformance; }
            set { _bProfileCommandPerformance = value; }
        }
        #endregion

        #region public ISeqLog Logger
        /// <summary>
        /// Get the logger object.
        /// </summary>
        public ISeqLog Logger
        {
            get { return _Logger; }
        }
        #endregion



        #region Constructors...
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CommandMediator()
        {
            _ResetEvent = new AutoResetEvent(false);

            //
            // Start the command executor thread:
            //
            ThreadStart threadStart = new ThreadStart(CommandExecutorWorkerThread);

            _MediatorThread = new Thread(threadStart);
            _MediatorThread.Name = "Mediator thread"; // added thread name
            _MediatorThread.IsBackground = true;
            _MediatorThread.Start();

        }
        #endregion


        #region public void SetLogger(ILogger logger)
        /// <summary>
        /// Set the logger for this object instance.
        /// </summary>
        /// <param name="logFilePath">Full path for log file.</param>
        public void SetLogger(ISeqLog logger)
        {
            lock (_LoggerSyncObject)
            {
                _Logger = logger;
            }
        }
        #endregion

        #region public void AddSupportedResource(string resourceName, object service)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="service"></param>
        public void AddSupportedResource(string resourceName, object resource)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                throw new Exception("Resource name cannot be null or empty string.");
            }
            if (resource == null)
            {
                throw new Exception("Resource object cannot be null.");
            }

            lock (_ResourcesSyncObject)
            {
                if (_SupportedResources.ContainsKey(resourceName))
                {
                    _SupportedResources[resourceName] = resource;
                    this.LogMessage("Resource \"" + resourceName + "\" in supported resources was replaced with a new value.");
                }
                else
                {
                    _SupportedResources.Add(resourceName, resource);
                    this.LogMessage("Added new resource \"" + resourceName + "\".");
                }
            }
        }
        #endregion

        #region public void RemoveSupportedResource(string resourceName)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceName"></param>
        public void RemoveSupportedResource(string resourceName)
        {
            lock (_ResourcesSyncObject)
            {
                if (_SupportedResources.ContainsKey(resourceName))
                {
                    _SupportedResources.Remove(resourceName);
                }
            }
        }
        #endregion


        #region public ExecutionState ExecuteCommand(BaseCommand command)
        /// <summary>
        /// Execute the command as non-blocking (asynchronous). Command exceptions are swallowed 
        /// but logged when running asynchonously; However, the user can subscribe to the <see cref="ExceptionOcurred"/>
        /// event to be notified when an exception occurs.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public ExecutionState ExecuteCommand(BaseCommand command)
        {
            if (_State == CommandMediatorState.Busy)
            {
                throw new Exception(_MediatorName + " is busy executing a command.");
            }

            if (command == null)
            {
                throw new ArgumentNullException("Cannot execute a null command object (command = null).");
            }

            lock (_CommandSyncObject)
            {
                _ExecutingCommand = command;
                _ExecutingCommandState = command.CreateExecutionState(null);
                _ResetEvent.Set();
            }

            return _ExecutingCommandState;
        }
        #endregion

        #region public ExecutionState ExecuteCommandBlocking(BaseCommand command)
        /// <summary>
        /// Execute the command as blocking (synchronous). All command 
        /// exceptions are logged and then re-thrown when executed blocking
        /// (however, they are consumed by CommandMediator when executed 
        /// asynchronously by <see cref="ExecuteCommand"/> method).
        /// </summary>
        /// <remarks>Blocking commands will not raise the CommandMediator's 
        /// <see cref="OnStateChanged"/> event. 
        /// <param name="command"></param>
        /// <returns></returns>
        public ExecutionState ExecuteCommandBlocking(BaseCommand command)
        {
            if (_State == CommandMediatorState.Busy)
            {
                throw new Exception(_MediatorName + " is busy executing a command.");
            }

            lock (_CommandSyncObject)
            {
                if (command == null)
                {
                    throw new ArgumentNullException("Cannot execute a null command object (command = null).");
                }

                _ExecutingCommand = command;
                _ExecutingCommand.OwningMediator = this;
                _ExecutingCommandState = command.CreateExecutionState(null);

                command.ExecuteWorker();
            }

            return _ExecutingCommandState;
        }
        #endregion

        #region public void Cancel()
        /// <summary>
        /// Cancels (aborts) the executing command (if there is one). If the command is a
        /// <see cref="SerialCommand"/> or a <see cref="ParallelCommand"/> all child 
        /// commands of the parent command will be canceled (aborted).
        /// </summary>
        public void Cancel()
        {
            if (_ExecutingCommand != null)
            {
                _ExecutingCommand.Cancel();
            }
        }
        #endregion

        #region public void ResetCommandPerformanceProfiler()
        /// <summary>
        /// Reset or clear all previously accrued command performance timings.
        /// </summary>
        public void ResetCommandPerformanceProfiler()
        {
            lock (_PerfProfilingSyncObject)
            {
                _PerformanceTable.Clear();
            }
        }
        #endregion

        #region public Dictionary<string, List<long>> GetPerformanceProfileSnapShot()
        /// <summary>
        /// Get a dictionary of all executed commands and their execution times. The
        /// dictionary key is the command name and the value is a generic list of type long
        /// where this value is the list of all command execution times in milliseconds.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, List<long>> GetPerformanceProfileSnapShot()
        {
            Dictionary<string, List<long>> tempTable = new Dictionary<string, List<long>>();

            lock (_PerfProfilingSyncObject)
            {
                foreach (string key in _PerformanceTable.Keys)
                {
                    List<long> tempList = _PerformanceTable[key] as List<long>;
                    tempTable.Add(key, new List<long>(tempList));
                }
            }

            return tempTable;
        }
        #endregion


        //
        // Worker Methods:
        //
        #region protected void CommandExecutorWorkerThread()
        /// <summary>
        /// The command processing worker thread.
        /// </summary>
        protected void CommandExecutorWorkerThread()
        {
            while (true)
            {
                try
                {
                    _ResetEvent.WaitOne();
                    CommandExecutorWorker();
                }
                catch (ThreadAbortException ex)
                {
                    // Swallow thread abort exception ... thread is aborted when app closes.
                    // We do not want to log this because this is how .NET aborts threads.
                    // .NET will automatically raise this exception again at the end of the 
                    // catch block (see documentation on ThreadAbortException).
                    LogException("ThreadAbortException caught in CommandMediator command executor worker thread ... exiting thread.", ex);
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogException("Exception caught in CommandMediator command executor worker thread", ex);
                        RaiseOnExceptionEvent(ex);
                    }
                    catch
                    {
                        //Swallow any logging exceptions ... we don't want any failure due to error 
                        //logging to cripple this thread.
                    }
                }
                finally
                {
                    _ResetEvent.Reset();
                }
            }//end while

        }
        #endregion

        #region protected virtual void CommandExecutorWorker()
        /// <summary>
        /// The command executor worker method.
        /// </summary>
        protected virtual void CommandExecutorWorker()
        {
            lock (_CommandSyncObject)
            {
                RaiseStateChangedEvent(CommandMediatorState.Busy);

                _ExecutingCommand.OwningMediator = this;

                if (_ExecutingCommand is ParallelCommand)
                {
                    LogMessage("Executing parallel command \"" + _ExecutingCommand.Name + "\"...");
                }
                else if (_ExecutingCommand is SerialCommand)
                {
                    LogMessage("Executing serial command \"" + _ExecutingCommand.Name + "\"...");
                }
                else
                {
                    LogMessage("Executing command \"" + _ExecutingCommand.Name + "\"...");
                }

                _ExecutingCommand.Execute();

                //
                // Wait until command has completed or timed out:
                //
                bool receivedSignal = _ExecutingCommand.AutoReset.WaitOne(_ExecutingCommand.TimeOut, false);

                if (!receivedSignal)
                {
                    LogMessage("Aborting command \"" + _ExecutingCommand.Name + "\" on timeout.");
                    _ExecutingCommand.Cancel();
                }

                if (_ExecutingCommand is ParallelCommand)
                {
                    LogMessage("Parallel command \"" + _ExecutingCommand.Name + "\" successfully completed.");
                }
                else if (_ExecutingCommand is SerialCommand)
                {
                    LogMessage("Serial command \"" + _ExecutingCommand.Name + "\" successfully completed.");
                }
                else
                {
                    LogMessage("Command \"" + _ExecutingCommand.Name + "\" successfully completed.");
                }

                if (_ExecutingCommandState != null)
                {
                    LogCommandExecutionState(_ExecutingCommandState);
                }

                //
                // Null the command thread so that it is available for garbage collection:
                //
                _ExecutingCommand.NullCommandThread();

                _ExecutingCommand = null;
                RaiseStateChangedEvent(CommandMediatorState.Idle);
            }//end lock
        }
        #endregion


        #region protected void RaiseStateChangedEvent(UIMediatorState state)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        protected void RaiseStateChangedEvent(CommandMediatorState state)
        {
            if (_State == state) return;

            _State = state;

            if (StateChanged != null)
            {
                StateChanged(this, _State);
            }
        }
        #endregion

        #region protected void RaiseOnExceptionEvent(Exception ex)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        protected void RaiseOnExceptionEvent(Exception ex)
        {
            if (Exception != null)
            {
                Exception(this, ex);
            }
        }
        #endregion


        #region internal void LogCommandPerformance(Stopwatch stopWatch, string name)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_ms"></param>
        /// <param name="name"></param>
        internal void LogCommandPerformance(long elapsedTime_ms, string name)
        {
            try
            {
                lock (_PerfProfilingSyncObject)
                {
                    if (_PerformanceTable.ContainsKey(name))
                    {
                        List<long> timingList = _PerformanceTable[name] as List<long>;
                        timingList.Add(elapsedTime_ms);
                    }
                    else
                    {
                        List<long> timingList = new List<long>();
                        timingList.Add(elapsedTime_ms);
                        _PerformanceTable.Add(name, timingList);
                    }
                }
            }
            catch
            {
            }
        }
        #endregion

        #region internal void LogMessage(string msg)
        /// <summary>
        /// Log a message to the attached logger. See method <see cref="SetLogger"/>
        /// to attach a logger.
        /// </summary>
        /// <param name="msg"></param>
        internal void LogMessage(string msg)
        {
            if (_Logger == null) return;

            _Logger.Log(msg);
        }
        #endregion

        #region internal void LogException(string header, Exception ex)
        /// <summary>
        /// Log an exception to the attached logger. See method <see cref="SetLogger"/>
        /// to attach a logger.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="ex"></param>
        internal void LogException(string header, Exception ex)
        {
            if (_Logger == null) return;

            //_Logger.LogException(header, ex);
            _Logger.LogError(header + " Exception: " + ex.ToString());
        }
        #endregion


        #region private void LogCommandExecutionState(ExecutionState execState)
        /// <summary>
        /// Log the command execution state to the attached logger.S ee method <see cref="SetLogger"/>
        /// to attach a logger.
        /// </summary>
        /// <param name="execState"></param>
        private void LogCommandExecutionState(ExecutionState execState)
        {
            StringBuilder strBuilder = new StringBuilder();

            strBuilder.AppendLine();
            execState.LogState(strBuilder, 0);
            strBuilder.AppendLine();

            LogMessage(strBuilder.ToString());
        }
        #endregion


    }

}











/*
 * 
       #region protected void CommandExecutorWorkerThread()
      /// <summary>
      /// The command processing worker thread.
      /// </summary>
      protected void CommandExecutorWorkerThread()
      {
         RaiseStateChangedEvent(CommandMediatorState.Busy);

         Stopwatch stopWatch = new Stopwatch();

         if (_ExecutingCommand is ParallelCommandBlock)
         {
            LogMessage("Executing parallel command \"" + _ExecutingCommand.Name + "\"...");
         }
         else if (_ExecutingCommand is SerialCommandBlock)
         {
            LogMessage("Executing serial command \"" + _ExecutingCommand.Name + "\"...");
         }
         else
         {
            LogMessage("Executing command \"" + _ExecutingCommand.Name + "\"...");
         }

         if (_bProfileCommandPerformance)
         {
            stopWatch.Reset();
            stopWatch.Start();

            _ExecutingCommand.Execute();
            _ExecutingCommand.AutoReset.WaitOne(_ExecutingCommand.TimeOut, false);

            stopWatch.Stop();
            LogCommandPerformance(stopWatch.ElapsedMilliseconds, _ExecutingCommand.Name);
            RaiseStateChangedEvent(CommandMediatorState.Idle);
         }
         else
         {
            _ExecutingCommand.Execute();
            _ExecutingCommand.AutoReset.WaitOne(_ExecutingCommand.TimeOut, false);
            RaiseStateChangedEvent(CommandMediatorState.Idle);
         }

         if (cmd is ParallelCommandBlock)
         {
            LogMessage("Parallel command \"" + _ExecutingCommand.Name + "\" successfully completed.");
         }
         else if (cmd is SerialCommandBlock)
         {
            LogMessage("Serial command \"" + _ExecutingCommand.Name + "\" successfully completed.");
         }
         else
         {
            LogMessage("Command \"" + _ExecutingCommand.Name + "\" successfully completed.");
         }

         while (true)
         {
            BaseCommand cmd = null;

            try
            {
               if (_CommandQueue.Count > 0)
               {
                  RaiseStateChangedEvent(CommandMediatorState.Busy);

                  //
                  // Execute the top-most commmand in the queue:
                  //
                  cmd = null;

                  lock (_QueueSyncObject)
                  {
                     cmd = _CommandQueue.Dequeue();
                  }

                  RaiseStatusUpdateEvent("Executing command \"" + cmd.Name + "\"...");
                  CommandExecutorWorker(stopWatch, cmd);
                  cmd = null;
               }
               else
               {
                  //
                  // Wait until there is at least one service command in the queue:
                  //
                  RaiseStateChangedEvent(CommandMediatorState.Idle);
                  _ResetEvent.WaitOne();
               }
            }
            catch (ThreadAbortException ex)
            {
               // Swallow thread abort exception ... thread is aborted when app closes.
               // We do not want to log this because this is how .NET aborts threads.
               // .NET will automatically raise this exception again at the end of the 
               // catch block (see documentation on ThreadAbortException).
               LogException("Exception caught in CommandMediator command executor worker thread", ex);

               lock (_QueueSyncObject)
               {
                  _CommandQueue.Clear();
               }

            }
            catch (Exception ex)
            {
               try
               {
                  LogException("Exception caught in CommandMediator command executor worker thread", ex);
                  RaiseOnExceptionEvent(ex);
               }
               catch
               {
                  //Swallow any logging exceptions ... we don't want any failure due to error 
                  //logging to cripple this thread.
               }
            }
         }//end while
      }
      #endregion

 *       #region protected void CommandExecutorWorker(Stopwatch stopWatch, BaseCommand cmd)
      /// <summary>
      /// 
      /// </summary>
      /// <param name="stopWatch"></param>
      /// <param name="cmd"></param>
      protected void CommandExecutorWorker(Stopwatch stopWatch, BaseCommand cmd)
      {
         if (cmd is ParallelCommandBlock)
         {
            LogMessage("Executing parallel command \"" + cmd.Name + "\"...");
         }
         else if (cmd is SerialCommandBlock)
         {
            LogMessage("Executing serial command \"" + cmd.Name + "\"...");
         }
         else
         {
            LogMessage("Executing command \"" + cmd.Name + "\"...");
         }

         if (_bProfileCommandPerformance)
         {
            stopWatch.Reset();
            stopWatch.Start();

            cmd.Execute();

            stopWatch.Stop();
            LogCommandPerformance(stopWatch.ElapsedMilliseconds, cmd.Name);
         }
         else
         {
            cmd.Execute();
         }

         if (cmd is ParallelCommandBlock)
         {
            LogMessage("Parallel command \"" + cmd.Name + "\" successfully completed.");
         }
         else if (cmd is SerialCommandBlock)
         {
            LogMessage("Serial command \"" + cmd.Name + "\" successfully completed.");
         }
      }
      #endregion

      #region public void ExecuteCommand(List<BaseCommand> commands)
      /// <summary>
      /// Execute the commands as non-blocking (asynchronous). Command exceptions are swallowed 
      /// but logged when running asynchonously; However, the user can subscribe to the <see cref="ExceptionOcurred"/>
      /// event to be notified when an exception occurs.
      /// </summary>
      /// <param name="commands">A list of commands to enqueue in the command queue</param>
      public void ExecuteCommand(List<BaseCommand> commands)
      {
         try
         {
            lock (_QueueSyncObject)
            {
               foreach (BaseCommand cmd in commands)
               {
                  cmd.OwningMediator = this;
                  _CommandQueue.Enqueue(cmd);
               }
            }

            _ResetEvent.Set();
         }
         catch (Exception ex)
         {
            LogException("Exception caught while executing Command list", ex);
            RaiseOnExceptionEvent(ex);
         }
      }
      #endregion
*/
