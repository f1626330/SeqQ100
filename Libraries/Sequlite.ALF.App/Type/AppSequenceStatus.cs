
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public class AppSequenceStatusProgress : AppStatus
    {
        public ProgressTypeEnum SequenceProgressStatus { get; set; }
        public SequenceDataTypeEnum SequenceDataType { get; set; }
        public AppSequenceStatusProgress()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusProgress;
            SequenceDataType = SequenceDataTypeEnum.None;
        }
    }

    public class AppSequenceStatusCycle : AppStatus
    {
        public int Cycle { get; set; }
        public SequenceDataTypeEnum StepSequenceRead { get; set; }

        public AppSequenceStatusCycle()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusCycle;
        }
    }

   

    public class AppSequenceStatusStep : AppStatus
    {
        public string Step { get; set; }
       
        public ProgressTypeEnum StepMessageType { get; set; }
        public AppSequenceStatusStep()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusStep;
            StepMessageType = ProgressTypeEnum.InProgress;
        }
    }

    public class AppSequenceStatusDataBackup : AppStatus
    {
        public string Message { get; set; }
        public ProgressTypeEnum DataBackupStatus { get; set; }
        public bool IsError { get; set; }
        public AppSequenceStatusDataBackup()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusDataBackup;
            DataBackupStatus = ProgressTypeEnum.None;
        }

    }

    
    public class AppSequenceStatusOLA : AppStatus
    {
        public string Message { get; set; }
        public ProgressTypeEnum OLAStatus { get; set; }
        public bool IsOLAResultsUpdated { get; set; }
        public SequenceDataTypeEnum SequenceDataType { get; set; }
        public AppSequenceStatusOLA()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusOLA;
            OLAStatus = ProgressTypeEnum.None;
        }

    }

    public class AppSequenceStatusTime : AppStatus
    {
        public TimeSpan TimeElapsed { get; set; }
        public AppSequenceStatusTime()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusTime;
        }
    }

    public class AppSequenceStatusTemperature : AppStatus
    {
        public float Temperature { get; set; }
        public AppSequenceStatusTemperature()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusTemperature;
        }
    }

    public class AppSequenceStatusImage : AppStatus
    {
        public string ImageSaved { get; set; }
        public AppSequenceStatusImage()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusImage;
        }
    }

    public class AppSequenceStatusSequenceReport : AppStatus
    {
        public string ReportSaved { get; set; }
        public AppSequenceStatusSequenceReport()
        {
            AppStatusType = AppStatusTypeEnum.AppSequenceStatusSeqenceReport;
        }
    }
}
