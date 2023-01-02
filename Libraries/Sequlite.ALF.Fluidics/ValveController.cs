using Sequlite.ALF.Common;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.Fluidics
{

    public enum ValveStatusTypes : byte
    {
        Ok = 0x00,
        Busy = 0x2A,
        HomeError = 0x63,
        MemoryError = 0x58,
        ConfigError = 0x4D,
        PositionError = 0x42,
        IntegrityError = 0x37,
        CRCError = 0x2C,
    }
    internal class ValveController : IValve
    {
        public event ValvePosUpdateHandle OnPositionUpdated;
        #region Private Fields
        private SerialPort _Port;
        private byte[] _ReadBuf;
        private bool _IsConnected;
        private bool _IsBusBusy;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Selector Valve");
        #endregion Private Fields

        #region Constructor
        public ValveController()
        {
            _Port = new SerialPort();
            _ReadBuf = new byte[10];
            Logger.Log("Create a Selector Valve Object");
        }
        #endregion Constructor

        #region Public Properties
        public bool IsConnected { get { return _IsConnected; } }
        public int CurrentPos { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect(string portName="", int baudrate = 19200)
        {
            if (_IsConnected == true)
            {
                return _IsConnected;
            }
            for (int tryCounts = 0; tryCounts < 4; tryCounts++)
            {

                string[] portList = null;
                if (!string.IsNullOrEmpty(portName))
                {
                    portList = new string[] { portName };
                }
                else
                {
                    portList = SerialPort.GetPortNames();
                }
                if (portList != null)
                {
                    for (int i = 0; i < portList.Length; i++)
                    {
                        try
                        {
                            if (_Port.IsOpen)
                            {
                                continue;
                            }
                            _Port.PortName = portList[i];
                            _Port.BaudRate = baudrate;
                            _Port.Parity = Parity.None;
                            _Port.DataBits = 8;
                            _Port.StopBits = StopBits.One;
                            _Port.WriteTimeout = 250;
                            _Port.ReadTimeout = 1000;
                            _Port.Open();
                            if (RequestValveStatus())
                            {
                                _IsConnected = true;
                                break;
                            }
                            else
                            {
                                _Port.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            //Logger.LogError(ex.ToString());
                            Logger.LogWarning("Failed to open port " + portList[i] + "with error : " + ex.Message);
                        }
                    }
                }
                if (_IsConnected)
                {
                    break;
                }
                else
                {
                    Logger.LogError("Failed to connect to Valve.");
                }
            }
            return _IsConnected;
        }
        public bool GetCurrentPos()
        {
            return RequestValveStatus();
        }
        #endregion Public Functions

        protected ValveStatusTypes Status { get; private set; }
        protected string PortName
        {
            get
            {
                if (_Port == null || !_Port.IsOpen)
                {
                    return null;
                }
                return _Port.PortName;
            }
        }
        protected bool IsPortBusy { get; set; }
        protected int Baudrate
        {
            get
            {
                if (_Port == null || !_Port.IsOpen)
                {
                    return 0;
                }
                return _Port.BaudRate;
            }
        }

        public int ValvePos => CurrentPos;

        #region Commands definition
        protected bool RequestValveStatus()
        {
            if (!_Port.IsOpen)
            {
                return false;
            }
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    if (_IsBusBusy == false)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                    if (i == 19)
                    {
                        Logger.LogError("Waiting too long");
                        return false;
                    }
                }
                _IsBusBusy = true;
                byte[] cmd = new byte[2];
                cmd[0] = (byte)'S';
                cmd[1] = 0x0D;
                _Port.Write(cmd, 0, cmd.Length);
                //Thread.Sleep(100);
                _Port.Read(_ReadBuf, 0, 1);
                if (_ReadBuf[0] == 0x2A)
                {
                    Status = ValveStatusTypes.Busy;
                    _IsBusBusy = false;
                    return true;
                }

                _Port.Read(_ReadBuf, 1, 2);
                if (_ReadBuf[2] != 0x0D)
                {
                    _IsBusBusy = false;
                    return false;
                }
                byte status = Convert.ToByte(Encoding.ASCII.GetString(_ReadBuf, 0, 2), 16);
                if (status >= 1 && status <= 24)
                {
                    CurrentPos = status;
                    Status = ValveStatusTypes.Ok;
                    _IsBusBusy = false;
                    return true;
                }
                Status = (ValveStatusTypes)status;
                _IsBusBusy = false;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _IsBusBusy = false;
                return false;
            }
        }
        protected bool HomeValve(bool waitForExecution)
        {
            if (!_Port.IsOpen)
            {
                return false;
            }
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < 20; i++)
                {
                    if (_IsBusBusy == false)
                    {
                        break;
                    }
                    Thread.Sleep(50);
                    if (i == 19)
                    {
                        Logger.LogError("Waiting too long");
                        return false;
                    }
                }
                _IsBusBusy = true;
                byte[] cmd = new byte[2];
                cmd[0] = (byte)'M';
                cmd[1] = 0x0d;
                _Port.Write(cmd, 0, cmd.Length);
                Thread.Sleep(100);
                _Port.Read(_ReadBuf, 0, 1);
                _IsBusBusy = false;
                if (_ReadBuf[0] != 0x0D)
                {
                    _IsBusBusy = false;
                    return false;
                }
                if (waitForExecution)
                {
                    do
                    {
                        RequestValveStatus();
                    }
                    while (Status == ValveStatusTypes.Busy);
                }
                Logger.Log($"Valve home elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                sw.Stop();
                return true;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
                _IsBusBusy = false;
                return false;
            }
        }

        public bool SetToNewPos(int pos, bool waitForExecution)
        {
            Stopwatch sw = Stopwatch.StartNew();
            if (!_Port.IsOpen) { return false; }
            if (pos < 1 || pos > 24)
            {
                throw new ArgumentOutOfRangeException("pos", "Position must be in 1 to 24 range");
            }
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    if (_IsBusBusy == false)
                    {
                        break;
                    }
                    Thread.Sleep(50);
                    if (i == 19)
                    {
                        Logger.LogError("Waiting too long");
                        return false;
                    }
                }
                _IsBusBusy = true;
                byte[] cmd = new byte[4];
                cmd[0] = (byte)'P';
                byte[] data = Encoding.ASCII.GetBytes(pos.ToString("X2"));
                cmd[1] = data[0];
                cmd[2] = data[1];
                cmd[3] = 0x0D;
                _Port.Write(cmd, 0, cmd.Length);
                Thread.Sleep(10);
                _Port.Read(_ReadBuf, 0, 1);
                if (_ReadBuf[0] != 0x0D)
                {
                    _IsBusBusy = false;
                    return false;
                }
                _IsBusBusy = false;
                if (waitForExecution)
                {
                    int trycounts = 0;
                    do
                    {
                        Thread.Sleep(10);
                        RequestValveStatus();
                        if (++trycounts > 200)
                        {
                            ResetValve();
                            Logger.LogError("Waiting too long");
                            Thread.Sleep(100);
                            return false;
                        }
                    }
                    while (CurrentPos != pos);
                }
                Logger.Log($"Valve movement to {pos} elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                sw.Stop();
                OnPositionUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _IsBusBusy = false;
                return false;
            }

        }

        public bool ResetValve()
        {
            Logger.LogError("Reset");
            _Port.Close();
            _IsConnected = false;
            Thread.Sleep(100);
            _Port = new SerialPort();
            _ReadBuf = new byte[10];
            return Connect();
        }

        public bool Initialize(bool isCCW, int initialPort)
        {
            //no impl
            return true;
        }

        public bool SetToNewPos(int pos, bool moveCCW, bool waitForExecution)
        {
            return SetToNewPos(pos, waitForExecution);
        }


        #endregion Commands definition

    }
}
