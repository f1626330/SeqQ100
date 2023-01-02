using System.Collections.Generic;
using System.Threading;

namespace Sequlite.ALF.Common
{

    /// <summary>
    /// A command class that executes all child commands in parallel.
    /// </summary>
    /// <remarks>
    ///   <para>Copyright: Created by Tony Jenniges, 2008-2009, All rights reserved.</para>
    ///   <para>Contact:   tonyjenniges@yahoo.com</para>
    /// </remarks>
    public sealed class ParallelCommand : BaseCommand
    {
        private List<BaseCommand> _ChildCommands = new List<BaseCommand>();

        /// <summary>
        /// Get the list of all child commands
        /// </summary>
        internal List<BaseCommand> ChildCommands
        {
            get { return _ChildCommands; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="childCommands">All child commands to be executed in parallel.</param>
        public ParallelCommand(params BaseCommand[] childCommands)
           : base()
        {
            for (int i = 0; i < childCommands.Length; i++)
            {
                _ChildCommands.Add(childCommands[i]);
            }
        }


        /// <summary>
        /// Execute all child commands in parallel and wait for completion on the parallel 
        /// command's thread.
        /// </summary>
        protected override void DoTask()
        {
            if (_ChildCommands == null || _ChildCommands.Count == 0)
            {
                _OwningMediator.LogMessage("   No parallel child commands to execute.");
                return;
            }

            WaitHandle[] _WaitHandles = new WaitHandle[_ChildCommands.Count];
            bool bAllWaitsSignaled = false;

            try
            {
                int iCount = 0;

                foreach (BaseCommand cmd in _ChildCommands)
                {
                    _WaitHandles[iCount++] = cmd.AutoReset;
                }

                //
                // Execute each command on it's own thread:
                //
                foreach (BaseCommand cmd in _ChildCommands)
                {
                    cmd.OwningMediator = _OwningMediator;
                    cmd.Parent = this;
                    if (_OwningMediator != null)
                    {
                        _OwningMediator.LogMessage("   Starting parallel child command \"" + cmd.Name + "\"...");
                    }

                    cmd.Execute();
                }

                bAllWaitsSignaled = WaitHandle.WaitAll(_WaitHandles, _msTimeoutTime);

                if (!bAllWaitsSignaled)
                {
                    _OwningMediator.LogMessage("   Parallel command timed-out before completion!");
                    Thread.CurrentThread.Abort();
                }

                CheckForStoppedOnChildException();

                if (_OwningMediator != null)
                {
                    _OwningMediator.LogMessage("   All parallel child commands for \"" + this.Name + "\" successfully completed.");
                }
            }
            catch (ThreadAbortException ex)
            {
                if (bAllWaitsSignaled)
                {
                    _ExecutionInfo.Status = ExecutionState.ExecutionStatus.StoppedOnException;
                }
                else
                {
                    _ExecutionInfo.Status = ExecutionState.ExecutionStatus.CancelledOnTimeout;
                }

                //
                // Cancel (abort) all child commands:
                //
                foreach (BaseCommand cmd in _ChildCommands)
                {
                    cmd.Cancel();
                }

                if (_OwningMediator != null)
                {
                    if (bAllWaitsSignaled)
                    {
                        _OwningMediator.LogMessage("Command \"" + this.Name + "\" cancelled (on ThreadAbortException).");
                        _OwningMediator.LogMessage("   Aborting all parallel child commands for \"" + this.Name + "\" on exception.");
                    }
                    else
                    {
                        _OwningMediator.LogMessage("Command \"" + this.Name + "\" cancelled on time-out.");
                        _OwningMediator.LogMessage("   Aborting all parallel child commands for \"" + this.Name + "\" on time-out.");
                    }
                }


                if (this.Parent != null)
                {
                    this.Parent.Cancel();
                    this.Parent._ExecutionInfo.Status = ExecutionState.ExecutionStatus.StoppedOnException;
                }

                //
                // Continue to wait for the child commands to abort:
                //
                WaitHandle.WaitAll(_WaitHandles, _msTimeoutTime);

                throw ex;
            }
        }

        /// <summary>
        /// Null all child command threads and this object's command thread so that they
        /// are available for garbage collection.
        /// </summary>
        internal override void NullCommandThread()
        {
            //
            // Null all child command thread objects:
            //
            foreach (BaseCommand cmd in _ChildCommands)
            {
                cmd.NullCommandThread();
            }

            base.NullCommandThread();
        }

    }

}


/*
      internal override void DoTask()
      {
         if (_ChildCommands == null || _ChildCommands.Count == 0)
         {
            _OwningMediator.LogMessage("   No parallel child commands to execute.");
            return;
         }

         try
         {
            WaitHandle[] _WaitHandles = new WaitHandle[_ChildCommands.Count];
            _Threads = new Thread[_ChildCommands.Count];
            int iCount = 0;

            foreach (BaseCommand cmd in _ChildCommands)
            {
               cmd.AutoReset.Reset();
               _WaitHandles[iCount] = cmd.AutoReset;
               _Threads[iCount]     = new Thread(new ThreadStart(cmd.ExecuteParallel));
               _Threads[iCount].IsBackground = true;

               ++iCount;
            }

            iCount = 0;
            foreach (BaseCommand cmd in _ChildCommands)
            {
               cmd.OwningMediator = _OwningMediator;
               if (_OwningMediator != null)
               {
                  _OwningMediator.LogMessage("   Starting parallel child command \"" + cmd.Name + "\"...");
               }

               _Threads[iCount].Start();
               ++iCount;
            }

            WaitHandle.WaitAll(_WaitHandles, _msTimeoutTime);

            if (_OwningMediator != null)
            {
               _OwningMediator.LogMessage("   All parallel child commands for \"" + this.Name + "\" successfully completed.");
            }

         }
         catch (Exception ex)
         {
            if (_OwningMediator != null)
            {
               _OwningMediator.LogMessage("   Aborting all parallel child commands for \"" + this.Name + "\" on exception.");
            }

            // Abort any running commands
            foreach (Thread thread in _Threads)
            {
               try
               {
                  thread.Abort();
               }
               catch
               {
               }
            }
            throw ex;
         }
      }
*/
