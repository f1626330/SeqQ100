using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class SequenceStatusModel : ModelBase
    {

        public SequenceStatusModel() { }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string RunName{ get; set; }
        public string RunDescription { get; set; }
        public string UserName { get; set; }
        
        public string TotalYieldString { get; set; }
        
        TimeSpan _TimeElapsed = new TimeSpan(0);
        public TimeSpan TimeElapsed {get => _TimeElapsed; set => SetProperty(ref _TimeElapsed, value);}
       
        string _Step = "Step";
        public string Step { get => _Step; set => SetProperty(ref _Step, value); }

        ProgressTypeEnum _StepMessageType = ProgressTypeEnum.InProgress;
        public ProgressTypeEnum StepMessageType { get => _StepMessageType; set => SetProperty(ref _StepMessageType, value); }

        string _OLAMessage = "OLA OFF";
        public string OLAMessage { get => _OLAMessage; set => SetProperty(ref _OLAMessage, value); }

        ProgressTypeEnum _OLAMessageType = ProgressTypeEnum.InProgress;
        public ProgressTypeEnum OLAMessageType { get => _OLAMessageType; set => SetProperty(ref _OLAMessageType, value); }

        string _DataBackupMessage;
        public string DataBackupMessage { get => _DataBackupMessage; set => SetProperty(ref _DataBackupMessage, value); }

        bool _IsDataBackupRunning;
        public bool IsDataBackupRunning { get => _IsDataBackupRunning; set => SetProperty(ref _IsDataBackupRunning, value); }

        bool _IsOLARunning;
        public bool IsOLARunning { get => _IsOLARunning; set => SetProperty(ref _IsOLARunning, value); }

        string _ImageSaved;
        public string ImageSaved { get => _ImageSaved; set => SetProperty(ref _ImageSaved, value); }

        int _Cyle; //current cycle
        public int Cyle { get => _Cyle; set => SetProperty(ref _Cyle, value); }

        public SequenceInfo SequenceInformation { get; set; }

        double _Temperature;
        public double Temperature { get => _Temperature; set => SetProperty(ref _Temperature, value); }

        DateTime _TempTime = DateTime.Now;
        public DateTime TempTime { get => _TempTime; set => SetProperty(ref _TempTime, value); }
        bool _IsOLAEnabled;
        public bool IsOLAEnabled { get => _IsOLAEnabled; set => SetProperty(ref _IsOLAEnabled, value); }
        Dictionary<SequenceDataTypeEnum, ISequenceDataFeeder> _SequenceDataFeederList = new Dictionary<SequenceDataTypeEnum, ISequenceDataFeeder>();
        public ISequenceDataFeeder SequenceDataFeeder(SequenceDataTypeEnum sequenceDataType)
        {
            if (_SequenceDataFeederList.ContainsKey(sequenceDataType))
            {
                return _SequenceDataFeederList[sequenceDataType];
            }
            else
            {
                return null;
            }
        }

        public ISequenceDataFeeder CreateSequenceDataFeeder(SequenceDataTypeEnum sequenceDataType, ISeqLog logger)
        {
            if (!_SequenceDataFeederList.ContainsKey(sequenceDataType))
            {
                ISequenceDataFeeder sequenceDataFeeder = new SequenceOLADataProcess(logger, sequenceDataType);
                _SequenceDataFeederList[sequenceDataType] = sequenceDataFeeder;
                return sequenceDataFeeder;
            }
            else
            {
                return _SequenceDataFeederList[sequenceDataType];
            }
        }

        bool _IsSequenceDone;
        public bool IsSequenceDone { get => _IsSequenceDone; set => SetProperty(ref _IsSequenceDone, value); }

        SequenceDataTypeEnum _ImagingSequenceRead;
        public SequenceDataTypeEnum ImagingSequenceRead { get=> _ImagingSequenceRead; set=>SetProperty(ref _ImagingSequenceRead, value); }


        public void UpdateOLAStatus(AppSequenceStatusOLA st, bool updateMessage = true)
        {
            lock (_commonLock)
            {
                if (updateMessage)
                {
                    OLAMessage = st.Message;
                }
                
                ProgressTypeEnum oLAStatus = st.OLAStatus;
                OLAMessageType = oLAStatus;
                //switch (oLAStatus)
                //{
                //    case ProgressTypeEnum.InProgress:
                //        IsOLARunning = true;
                //        break;

                //    case ProgressTypeEnum.Aborted:
                //    case ProgressTypeEnum.Failed:
                //    case ProgressTypeEnum.Completed:
                //        IsOLARunning = false;

                //        break;
                //}
            }
        }

        public void UpdateDataBackupStatus(AppSequenceStatusDataBackup st)
        {
            DataBackupMessage = st.Message;
            ProgressTypeEnum status = st.DataBackupStatus;
            switch (status)
            {
                case ProgressTypeEnum.InProgress:
                    IsDataBackupRunning = true;
                    break;

                case ProgressTypeEnum.Aborted:
                case ProgressTypeEnum.Failed:
                case ProgressTypeEnum.Completed:
                    IsDataBackupRunning = false;
                    
                    break;
            }
        }
        public void Clear()
        {
            IsDataBackupRunning = false;
            IsOLARunning = false;
            TimeElapsed = new TimeSpan(0);
            IsSequenceDone = false;
        }
    }
}
