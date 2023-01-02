using Sequlite.ALF.Common;
using System;
using System.IO.Ports;
using System.Threading;
using System.Management;
using System.Collections.Generic;

namespace Hywire.CommLibrary
{
    public abstract class SerialCommBase
    {
        public delegate void CommTimeOutHandler(string errorInfo);
        public event CommTimeOutHandler OnCommTimeOut;
        public enum CommStatus
        {
            Idle,
            Hearing,
            TimeOut,
            GotAck,
            GotResponse,
            InvalidRequest,
            UnknownResponse,
            CrcFailed,
        }

        #region Private Fields
        private SerialPort _Port;
        private Thread _ReadThread;
        private CommStatus _Status;
        protected System.Timers.Timer _Timer;
        protected int _TimeOutInMilliSec = 1000;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Serial Comm Base");

        private object _ThreadLock = new object();
        #endregion Private Fields

        #region Public Properties
        public byte[] ReadBuf { get; protected set; }
        public int ReadIndex { get; protected set; }
        public CommStatus Status
        {
            get { return _Status; }
            protected set
            {
                if (_Status != value)
                {
                    _Status = value;
                    if (_Status == CommStatus.Hearing)
                    {
                        _Timer.Start();
                    }
                    else
                    {
                        if (_Status == CommStatus.TimeOut)
                        {
                            if (OnCommTimeOut != null)
                            {
                                OnCommTimeOut.Invoke(string.Format("Communication time out for {0} ms!", TimeOutInMilliSec));
                            }
                        }
                        _Timer.Stop();
                    }
                }
            }
        }
        public int TimeOutInMilliSec
        {
            get { return _TimeOutInMilliSec; }
            set
            {
                _TimeOutInMilliSec = value;
                _Timer.Interval = _TimeOutInMilliSec;
            }
        }
        public bool IsOpened
        {
            get
            {
                return _Port.IsOpen;
            }
        }

        public string PortName { get; private set; }
        #endregion Public Properties

        #region Constructor
        public SerialCommBase(uint readBufSize)
        {
            _Port = new SerialPort();
            ReadBuf = new byte[readBufSize];
            ReadIndex = 0;
            Status = CommStatus.Idle;
            _Timer = new System.Timers.Timer();
            _Timer.AutoReset = false;
            _Timer.Interval = _TimeOutInMilliSec;
            _Timer.Enabled = false;
            _Timer.Elapsed += _Timer_Elapsed;
        }

        private void _Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _Timer.Enabled = false;
            Status = CommStatus.TimeOut;
        }
        #endregion Constructor

        #region Public Functions
        public bool Open(string portName, int baudRate = 9600)
        {
            if (_Port.IsOpen)
            {
                return true;
            }
            try
            {
                Logger = SeqLogFactory.GetSeqFileLog("SerialCommBase", portName);
                _Port.BaudRate = baudRate;
                _Port.Parity = Parity.None;
                _Port.DataBits = 8;
                _Port.StopBits = StopBits.One;
                _Port.PortName = portName;
                _Port.WriteTimeout = 1000;
                _Port.Open();
                PortName = portName;
                if (_ReadThread == null || _ReadThread.ThreadState == ThreadState.Stopped)
                {
                    _ReadThread = new Thread(ReadProcess);
                    _ReadThread.Name = "Serial Read"; // added name
                    _ReadThread.IsBackground = true;
                    _ReadThread.Priority = ThreadPriority.Highest;
                    _ReadThread.Start();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }
        public virtual bool Close()
        {
            if (_Port.IsOpen)
            {
                _ReadThread.Abort();
                _ReadThread.Join();
                _ReadThread = null;
                _Port.Close();
            }
            return true;
        }
        public bool Reconnect()
        {
            if (SearchForPort() == false)
            {
                return false;
            }
            try
            {
                var baudRate = _Port.BaudRate;
                var portName = _Port.PortName;
                if (Close() == false)
                {
                    return false;
                }
                _Port.BaudRate = baudRate;
                _Port.Parity = Parity.None;
                _Port.DataBits = 8;
                _Port.StopBits = StopBits.One;
                _Port.PortName = portName;
                _Port.WriteTimeout = 1000;
                _Port.Open();
                _ReadThread = new Thread(ReadProcess);
                _ReadThread.Name = "Serial Read"; // added name
                _ReadThread.IsBackground = true;
                _ReadThread.Priority = ThreadPriority.Highest;
                _ReadThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Reconnection failed, " + ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Repeatedly attempts to connect to the serial port device
        /// </summary>
        /// <param name="maxRetryCount">THe number of times to try connecting before giving up</param>
        /// <param name="retryDelay">The time delay between connection attempts (units = [ms] )</param>
        /// <returns></returns>
        protected bool SearchForPort(in int maxRetryCount = 10, in int retryDelay = 500)
        {
            bool portAvailable = false;
            int retryCount = 0;
            while (portAvailable == false)
            {
                try
                {
                    Logger.Log($"Trying to connect to serial port (name): {PortName}");
                    SelectQuery deviceQuery = new SelectQuery("Win32_PnPEntity");
                    ManagementObjectSearcher deviceSearch = new ManagementObjectSearcher(deviceQuery);
                    var infos = deviceSearch.Get();
                    foreach (ManagementObject deviceInfo in infos)
                    {
                        if (deviceInfo != null)
                        {
                            // device name
                            var dev_name_info = deviceInfo["Name"];
                            if (dev_name_info != null)
                            {
                                var dev_name = dev_name_info.ToString().Trim();
                                if (!string.IsNullOrEmpty(dev_name) && dev_name.Contains(PortName))
                                {
                                    portAvailable = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (portAvailable)
                    {
                        Logger.Log($"Serial port {PortName} was found");
                        return true;
                    }
                    else if (retryCount < maxRetryCount)
                    {
                        Logger.Log($"Serial port {PortName} was not found. Retrying in {retryDelay} ms");
                        retryCount++;
                        Thread.Sleep(retryDelay);
                    }
                    else
                    {
                        Logger.LogError($"Reconnection to the serial port {PortName} failed.");
                        return false;
                    }
                }
                catch (System.ArgumentNullException e)
                {
                    Logger.LogError($"Serial port name is NULL");
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(string.Format("Searching for serial port {0} failed, {1}", PortName, ex.ToString()));
                }
            }
            return false;
        }

        /// <summary>
        /// Inheritance Class must override this function.
        /// In this function, Status would be set to "GotResponse" when response is arrived to avoid time out event.
        /// </summary>
        /// <param name="detectedFrameIndex">Should be set to 0 or last index (start index + frame length) of the response bytes.</param>
        protected abstract void ResponseDetect(out int detectedFrameIndex);

        /// <summary>
        /// wait for Status changes to the given status, return true;
        /// return false if wait timeout or Status changes TimeOut or InvalidRequest
        /// </summary>
        /// <param name="milliSec">wait time out</param>
        /// <param name="status">the Status to be wait for</param>
        /// <returns></returns>
        public bool WaitForStatus(int milliSec, CommStatus status)
        {
            return WaitForStatus(milliSec, status, status);
        }
        public bool WaitForStatus(int milliSec, CommStatus status1, CommStatus status2)
        {
            while (milliSec > 0)
            {
                if (Status == status1 || Status == status2)
                {
                    return true;
                }
                else if (Status == CommStatus.TimeOut || Status == CommStatus.InvalidRequest || Status == CommStatus.CrcFailed)
                {
                    Status = CommStatus.Idle;
                    return false;
                }
                milliSec--;
                Thread.Sleep(1);
            }
            return false;
        }

        public bool SendBytes(byte[] data, int offset, int length)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen)
                {
                    return false;
                }
                if (WaitForStatus(TimeOutInMilliSec, CommStatus.Idle) == false)
                {
                    Logger.LogError("SendBytes wait expire");
                    return false;
                }
                try
                {
                    Logger.LogDebug($"SendBytes: {BitConverter.ToString(data, offset, length).Replace('-',' ')}");
                    _Port.Write(data, offset, length);
                    Status = CommStatus.Hearing;
                    return WaitForStatus(TimeOutInMilliSec, CommStatus.Idle);
                    //return true;
                }
                catch (System.TimeoutException ex)
                {
                    Logger.LogError($"SerialPort {_Port.PortName} writebytes timeout, will try to reconnect");
                    if (Reconnect())
                    {
                        try
                        {
                            _Port.Write(data, offset, length);
                            Status = CommStatus.Hearing;
                            return WaitForStatus(TimeOutInMilliSec, CommStatus.Idle);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    //if (_Port.IsOpen == false)
                    //{
                    //    Logger.LogError("Port is Closed, try reopen");
                    //    try
                    //    {
                    //        _Port.Open();
                    //    }
                    //    catch (Exception openEx)
                    //    {
                    //        Logger.LogError(openEx.ToString());
                    //    }
                    //}
                    return false;
                }
            }
        }

        public virtual bool SendBytes(byte[] data)
        {
            return SendBytes(data, 0, data.Length);
        }
        #endregion Public Functions

        #region Private Functions
        private void ReadProcess()
        {
            int bytesToRead;
            while (_Port.IsOpen)
            {
                try
                {
                    bytesToRead = _Port.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        _Port.Read(ReadBuf, ReadIndex, bytesToRead);
                        ReadIndex += bytesToRead;
                        if (ReadIndex >= ReadBuf.Length)
                        {
                            ReadIndex = 0;
                            return;
                        }                        
                    }                    
                    int length = 0;
                    ResponseDetect(out length);
                    if (length > 0)
                    {
                        for (int i = 0; i < ReadIndex - length; i++)
                        {
                            ReadBuf[i] = ReadBuf[length + i];
                        }
                        ReadIndex -= length;
                        Logger.LogDebug($"ReadBuf: {BitConverter.ToString(ReadBuf, 0, length).Replace('-', ' ')}");
                    }
                }
                catch (ArgumentException ex)
                {
                    Logger.LogError(ex.ToString());
                    //_Port.Close();
                    ReadIndex = 0;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.LogError(ex.ToString());
                    _Port.Close();
                }
                catch (System.IO.IOException ex)
                {
                    Logger.LogError(string.Format("SerialPort {0} read failed, {1}", _Port.PortName, ex.ToString()));
                    Logger.Log("Closing and reopen the SerialPort: " + _Port.PortName);
                    try
                    {
                        var portName = _Port.PortName;
                        var baudRate = _Port.BaudRate;
                        var readTimeout = _Port.ReadTimeout;
                        var writeTimeout = _Port.WriteTimeout;
                        //_Port.Close();
                        if (SearchForPort() == false)
                        {
                            Logger.LogError(portName + " reopen failed.");
                            return;
                        }
                        _Port = null;
                        _Port = new SerialPort();
                        _Port.BaudRate = baudRate;
                        _Port.Parity = Parity.None;
                        _Port.DataBits = 8;
                        _Port.StopBits = StopBits.One;
                        _Port.PortName = portName;
                        _Port.WriteTimeout = 1000;
                        _Port.Open();
                        Logger.Log(string.Format("SerialPort {0} reopened.", _Port.PortName));
                    }
                    catch (Exception ex1)
                    {
                        Logger.LogError(string.Format("SerialPort {0} reopen failed, {1}", _Port.PortName, ex1.ToString()));
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(string.Format("SerialPort {0} read failed, {1}", _Port.PortName, ex.ToString()));
                    //_Port.Close();
                    ReadIndex = 0;
                }
                Thread.Sleep(1);
            }
        }
        #endregion Private Functions
    }
}
