using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public enum AppMessageTypeEnum
    {
        Normal, //information
        Completed, 
        Warning,
        Error,
        ErrorNotification,
        Status,
    }

    public class AppMessage
    {
        public AppMessage()
        {
        }

        public AppMessageTypeEnum MessageType { get; set; }
        public string Message { get => MessageObject.ToString(); }
        public object MessageObject { get; set; }
    }

    public enum ProgressTypeEnum
    {
        None,
        Started,
        InProgress,
        InProgressWithWarning,
        Completed,
        Aborted,
        Failed,
    }

    public enum AppStatusTypeEnum
    {
        Unknown,
        AppSequenceStatusCycle,
        AppSequenceStatusTime,
        AppSequenceStatusStep,
        AppSequenceStatusTemperature,
        AppSequenceStatusImage,
        AppSequenceStatusOLA,
        AppSequenceStatusDataBackup,
        AppSequenceStatusProgress,

        AppRunWashStatus,
        AppSequenceStatusSeqenceReport,
    }

    public abstract class AppStatus
    {
        //public virtual string LogMessage { get { return String.Empty; } }
        public AppStatusTypeEnum AppStatusType { get; protected set; }

    }
}
