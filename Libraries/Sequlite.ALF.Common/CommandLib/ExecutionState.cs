using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Sequlite.ALF.Common
{
    /// <summary>
    /// This class encapsulates command execution state.
    /// </summary>
    /// <remarks>
    ///   <para>Copyright: Created by Tony Jenniges, 2008-2009, All rights reserved.</para>
    ///   <para>Contact:   tonyjenniges@yahoo.com</para>
    /// </remarks>
    public class ExecutionState : INotifyPropertyChanged
    {
        /// <summary>
        /// Enumerated command execution status.
        /// </summary>
        public enum ExecutionStatus
        {
            /// <summary>
            /// Command is waiting (queued) to be run.
            /// </summary>
            WaitingToRun,
            /// <summary>
            /// Command is currently executing.
            /// </summary>
            Executing,
            /// <summary>
            /// Command successfully completed its task.
            /// </summary>
            Completed,
            /// <summary>
            /// Command was cancelled by user or cancelled because parent command was cancelled by user.
            /// </summary>
            Cancelled,
            /// <summary>
            /// Command was cancelled because either it or it's child commands exceed a timeout time.
            /// </summary>
            CancelledOnTimeout,
            /// <summary>
            /// Command stopped on exception or if a parent command was stopped do to a child exception.
            /// </summary>
            StoppedOnException,
        }

        private string _Name = "";
        private Guid _Guid = Guid.Empty;
        private ExecutionState _ParentExecutionState = null;

        private ExecutionStatus _Status = ExecutionStatus.WaitingToRun;
        private List<ExecutionState> _Children = new List<ExecutionState>();
        private DateTime _StartTime = DateTime.Now;
        private DateTime _EndTime = DateTime.Now;
        private Exception _Error = null;
        private int _ThreadID = -1;


        private const string _TIME_FORMAT = "{0,-13:hh:mm:ss.ffff tt}";

        /// <summary>
        /// Get the user friendly name of this command.
        /// </summary>
        public string Name
        {
            get { return _Name; }
            internal set { _Name = value; }
        }

        /// <summary>
        /// Get the command's globally unique identifier.
        /// </summary>
        public Guid ID
        {
            get { return _Guid; }
            internal set { _Guid = value; }
        }

        /// <summary>
        /// Get the execution status object of this command's parent (if
        /// this command does not have a parent command owner then this property 
        /// will return null).
        /// </summary>
        public ExecutionState ParentExecutionState
        {
            get { return _ParentExecutionState; }
        }

        /// <summary>
        /// Get the execution state of the child commands for this command.
        /// Note: only <see cref="SerialCommand"/> and <see cref="ParallelCommand"/> 
        /// command objects can have child commands.
        /// </summary>
        public List<ExecutionState> Children
        {
            get { return _Children; }
        }

        /// <summary>
        /// Get the associated command's current status.
        /// </summary>
        public ExecutionStatus Status
        {
            get { return _Status; }
            internal set
            {

                if (_Status != value)
                {
                    _Status = value;
                    FirePropertyChanged("Status");
                }

            }
        }

        /// <summary>
        /// Get the time the command was started.
        /// </summary>
        public DateTime StartTime
        {
            get { return _StartTime; }
            internal set
            {
                if (_StartTime != value)
                {
                    _StartTime = value;
                    FirePropertyChanged("StartTime");
                }
            }
        }

        /// <summary>
        /// Get the time the command was completed. Check the <see cref="ExecutionStatus"/>
        /// property to know if the command completed successfuly with error or cancellation.
        /// </summary>
        public DateTime CompletedTime
        {
            get { return _EndTime; }
            internal set
            {
                if (_EndTime != value)
                {
                    _EndTime = value;
                    FirePropertyChanged("CompletedTime");
                }
            }
        }

        /// <summary>
        /// Get the command's exception. This value will only be non-null if the command's
        /// <see cref="Status"/> is StoppedOnException.
        /// </summary>
        public Exception Error
        {
            get { return _Error; }
            internal set
            {
                if (_Error != value)
                {
                    _Error = value;
                    FirePropertyChanged("Error");
                }
            }
        }

        /// <summary>
        /// Get the time the command was completed. Check the <see cref="ExecutionStatus"/>
        /// property to know if the command completed successfuly with error or cancellation.
        /// </summary>
        public int ThreadID
        {
            get { return _ThreadID; }
            internal set
            {
                if (_ThreadID != value)
                {
                    _ThreadID = value;
                    FirePropertyChanged("ThreadID");
                }
            }
        }

        #region Constructors...
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ExecutionState()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parent"></param>
        public ExecutionState(BaseCommand cmd, BaseCommand parent)
        {
            if (cmd == null)
            {
                return;
            }

            _Name = cmd.Name;
            _Guid = cmd.UniqueID;

            if (parent != null)
            {
                _ParentExecutionState = parent._ExecutionInfo;
            }

            if (cmd is SerialCommand)
            {
                SerialCommand serialCmd = (SerialCommand)cmd;

                foreach (BaseCommand child in serialCmd.ChildCommands)
                {
                    this.Children.Add(child.CreateExecutionState(serialCmd));
                }
            }
            else if (cmd is ParallelCommand)
            {
                ParallelCommand parallelCmd = (ParallelCommand)cmd;

                foreach (BaseCommand child in parallelCmd.ChildCommands)
                {
                    this.Children.Add(child.CreateExecutionState(parallelCmd));
                }
            }
        }

        #endregion


        /// <summary>
        /// 
        /// </summary>
        /// <param name="strBuilder"></param>
        /// <param name="nestLevel"></param>
        internal void LogState(StringBuilder strBuilder, int nestLevel)
        {
            string numTabs = new string('\t', nestLevel);
            string numTabsPlus1 = new string('\t', nestLevel + 1);

            strBuilder.AppendLine(numTabs + "Command: " + this.Name);

            TimeSpan elapsedTime = this.CompletedTime - this.StartTime;

            strBuilder.AppendLine(numTabsPlus1 + "ID:           " + this._Guid.ToString());
            strBuilder.AppendLine(numTabsPlus1 + "Thread ID:    " + this._ThreadID.ToString());
            strBuilder.AppendLine(numTabsPlus1 + "Status:       " + this.Status.ToString());
            strBuilder.AppendLine(numTabsPlus1 + "Start:        " + string.Format(_TIME_FORMAT, this.StartTime));
            strBuilder.AppendLine(numTabsPlus1 + "Completed:    " + string.Format(_TIME_FORMAT, this.CompletedTime));
            strBuilder.AppendLine(numTabsPlus1 + "Elapsed [ms]: " + elapsedTime.TotalMilliseconds.ToString("F4"));

            if (this.Children.Count > 0)
            {
                foreach (ExecutionState execState in this.Children)
                {
                    execState.LogState(strBuilder, nestLevel + 1);
                }
            }
        }

        #region INotifyPropertyChanged Members...

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        private void FirePropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

    }


}
