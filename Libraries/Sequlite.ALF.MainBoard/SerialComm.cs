using Hywire.CommLibrary;
using Sequlite.ALF.Common;
using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.MainBoard
{
    class SerialComm : SerialCommBase
    {
        #region Events
        public delegate void EventDelegate();
        public event EventDelegate OnResponseArrived;
        #endregion Events

        #region Private Fields
        private int _LogIndex;
        private bool _IsNewLog = true;
        private string _FolderStr;
        private string _Filename;
        private static object _LogLock = new object();
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("MainBoardComm");
        #endregion Private Fields
        public SerialComm(uint readBufSize) : base(readBufSize)
        {
            TimeOutInMilliSec = 1000;
            MBProtocol.OnChangingStatus += ALFProtocol_OnChangeStatus;
            _Filename = string.Format("{0}MainboardCommLog.txt", DateTime.Now.ToString("yyyyMMdd"));
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _FolderStr = Path.Combine(commonAppData, "Sequlite\\Log\\MBLog");
            if (!Directory.Exists(_FolderStr))
            {
                DirectoryInfo di = Directory.CreateDirectory(_FolderStr);
            }
            _FolderStr = Path.Combine(_FolderStr, _Filename);
            _IsNewLog = !File.Exists(_FolderStr);
            Logger.Log("Mainboard Serial Communiciation Created");
        }

        private void ALFProtocol_OnChangeStatus(CommStatus newStatus)
        {
            Status = newStatus;
        }

        private void LogToFile(string newLog)
        {
            Task.Run(() =>
            {
                lock (_LogLock)
                {
                    FileMode mode = _IsNewLog ? FileMode.Create : FileMode.Append;
                    _IsNewLog = false;
                    try
                    {
                        using (FileStream fs = new FileStream(_FolderStr, mode))
                        {
                            byte[] logs = Encoding.Default.GetBytes(newLog);
                            fs.Write(logs, 0, logs.Length);
                            fs.Flush();
                        }
                        Logger.Log(newLog, SeqLogFlagEnum.DEBUG);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.ToString());
                    }
                }
            });
        }
        #region Public Properties
        public bool IsConnected { get; private set; }
        public MBProtocol.Registers ResponseStartReg { get; private set; }
        public int ResponseRegNumbers { get; private set; }
        #endregion Public Properties

        #region Override Functions
        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            if (ReadIndex > 0)
            {
                StringBuilder logStr = new StringBuilder();
                logStr.Append(DateTime.Now.ToString("HH:mm:ss:fff"));
                logStr.Append("IN: ");
                for (; _LogIndex < ReadIndex; _LogIndex++)
                {
                    logStr.AppendFormat("{0:X2} ", ReadBuf[_LogIndex]);
                }
                logStr.Append("\r\n");
                LogToFile(logStr.ToString());
            }
            else { _LogIndex = 0; }

            MBProtocol.Registers responseStartReg;
            int responseRegNumbers;
            MBProtocol.ResponseDecoding(ReadBuf, ReadIndex, out detectedFrameIndex, out responseStartReg, out responseRegNumbers);
            if (detectedFrameIndex == 0)
            {
                return;
            }
            ResponseStartReg = responseStartReg;
            ResponseRegNumbers = responseRegNumbers;

            if (Status == CommStatus.Idle)
            {
                OnResponseArrived?.Invoke();
            }
            else if (Status == CommStatus.CrcFailed)
            {
                Status = CommStatus.Idle;
            }
            else if (Status == CommStatus.UnknownResponse)
            {
                Status = CommStatus.Idle;
            }
        }
        public override bool SendBytes(byte[] data)
        {
            if (SendBytes(data, 0, data.Length))
            {
                MBProtocol.Presetting(data);
                StringBuilder logStr = new StringBuilder();
                logStr.Append(DateTime.Now.ToString("HH:mm:ss:fff"));
                logStr.Append("OUT: ");
                foreach (byte b in data)
                {
                    logStr.AppendFormat("{0:X2} ", b);
                }
                logStr.Append("\r\n");
                LogToFile(logStr.ToString());
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion Override Functions

        #region Public Functions
        public void Connect(int baudRate)
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                if (Open(port, baudRate))
                {
                    byte[] sendBytes = MBProtocol.Query(MBProtocol.Registers.DeviceType, 3);
                    if (SendBytes(sendBytes))
                    {
                        IsConnected = true;
                        break;
                    }
                    Close();
                    Thread.Sleep(100);
                }
            }
        }
        public void Connect(string portName, int baudRate)
        {
            if (Open(portName, baudRate))
            {
                byte[] sendBytes = MBProtocol.Query(MBProtocol.Registers.DeviceType, 3);
                if (SendBytes(sendBytes))
                {
                    IsConnected = true;
                }
            }
        }
        public void Disconnect()
        {
            if (IsConnected)
            {
                base.Close();
                IsConnected = false;
            }
        }
        #endregion Public Functions

    }
}
