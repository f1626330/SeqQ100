using System.Collections.Generic;
using System.Threading;

namespace Sequlite.ALF.Common
{

    /// <summary>
    /// A command class that serially executes all child commands.
    /// </summary>
    /// <remarks>
    ///   <para>Copyright: Created by Tony Jenniges, 2008-2009, All rights reserved.</para>
    ///   <para>Contact:   tonyjenniges@yahoo.com</para>
    /// </remarks>
    public sealed class SerialCommand : BaseCommand
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
        /// <param name="childCommands">All child commands to be executed in serial (in constructor argument order).</param>
        public SerialCommand(params BaseCommand[] childCommands) : base()
        {
            for (int i = 0; i < childCommands.Length; i++)
            {
                _ChildCommands.Add(childCommands[i]);
            }
        }


        /// <summary>
        /// Execute all child commands in serial on the serial command's thread.
        /// </summary>
        protected override void DoTask()
        {
            try
            {
                if (_ChildCommands == null || _ChildCommands.Count == 0)
                {
                    _OwningMediator.LogMessage("   No serial child commands to execute.");
                    return;
                }

                foreach (BaseCommand cmd in _ChildCommands)
                {
                    if (_OwningMediator != null)
                    {
                        _OwningMediator.LogMessage("   Executing serial child command \"" + cmd.Name + "\"...");
                    }

                    cmd.OwningMediator = _OwningMediator;
                    cmd.Parent = this;
                    cmd.ExecuteWorker();

                    if (_OwningMediator != null)
                    {
                        _OwningMediator.LogMessage("   Serial child command \"" + cmd.Name + "\" successfully completed.");
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                //
                // If we get a thread abort exception here
                //
                _ExecutionInfo.Status = ExecutionState.ExecutionStatus.StoppedOnException;

                if (this.Parent != null)
                {
                    this.Parent.Cancel();
                }

                throw ex;
            }
        }

    }

}
