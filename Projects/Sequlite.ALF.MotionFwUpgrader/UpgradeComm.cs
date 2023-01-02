using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.MotionFwUpgrader
{
    class UpgradeComm
    {
        public enum BusStatusTypes
        {
            Idle,
            Waiting,
            Timeout,
        }

        #region Private fields
        private SerialPort _CommandPort;
        private object _SendThreadLock;
        private byte[] _ReceiveBuf;
        private int _ReceivedIndex;
        #endregion Private fields

        #region Constructor
        private static UpgradeComm _Instance;
        private static object _InstanceCreateLock = new object();
        public static UpgradeComm GetInstance()
        {
            lock (_InstanceCreateLock)
            {
                if (_Instance == null)
                {
                    _Instance = new UpgradeComm();
                }
                return _Instance;
            }
        }
        protected UpgradeComm()
        {
            _SendThreadLock = new object();
            _ReceiveBuf = new byte[1024000];
        }
        #endregion Constructor

        #region Public Properties
        public BusStatusTypes BusStatus { get; protected set; }
        public bool IsConnected { get; protected set; }
        public string ErrorMessage { get; protected set; }

        public EPCSIdTypes EPCSId { get { return UpgradeProtocol.EPCSId; } }
        public byte ReconfigTrigger { get { return UpgradeProtocol.ReconfigTrigger; } }
        public EPCSFlashContent FlashContent { get { return UpgradeProtocol.FlashContent; } }
        public string LastUpgradeInfo { get; protected set; }
        public bool IsInFactoryMode { get; private set; }
        public bool IsInUserMode { get; private set; }
        public string UserFWVersion { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect()
        {
            if (IsConnected) { return true; }
            var portNames = SerialPort.GetPortNames();
            foreach (var port in portNames)
            {
                try
                {
                    IsConnected = false;
                    _CommandPort = new SerialPort(port);
                    _CommandPort.ReadTimeout = 1000;
                    _CommandPort.BaudRate = 115200;
                    _CommandPort.Parity = Parity.None;
                    _CommandPort.DataBits = 8;
                    _CommandPort.StopBits = StopBits.One;
                    _CommandPort.Open();
                    IsConnected = true;
                    if (UpgraderReadEpcsId())
                    {
                        UpgraderReadLastUpgradeInfo();
                        IsInFactoryMode = true;
                    }
                    else if (GetUserImageVersions())
                    {
                        IsInUserMode = true;
                    }
                    else
                    {
                        IsConnected = false;
                        if (_CommandPort.IsOpen)
                        {
                            _CommandPort.Close();
                        }
                    }
                    if (IsConnected)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (_CommandPort.IsOpen)
                    {
                        _CommandPort.Close();
                    }
                }
            }
            return false;
        }
        public bool Disconnect()
        {
            try
            {
                if (!IsConnected) { return true; }
                _CommandPort.Close();
                IsConnected = false;
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }
        }
        public bool UpgraderReadEpcsId()
        {
            byte[] cmd = UpgradeProtocol.UpgraderReadEpcsId();
            return SendBytes(cmd, 8);
        }
        public bool GetUserImageVersions()
        {
            byte[] cmd = { 0x55, 0x01, 0xf0, 0x01, 0x01, 0xaa };
            if( SendBytes(cmd, 10))
            {
                UserFWVersion = UpgradeProtocol.UserFWVersion;
                return true;
            }
            return false;
        }
        public bool UpgraderReadEpcsContent(int startAddr, ushort length, byte[] rxbuf, int offset)
        {
            byte[] cmd = UpgradeProtocol.UpgraderReadEpcsContent(startAddr, length);
            if (SendBytes(cmd, 10 + length))
            {
                Buffer.BlockCopy(UpgradeProtocol.FlashContent.Content, 0, rxbuf, offset, length);
                return true;
            }
            return false;
        }
        public bool UpgraderWriteEpcsContent(int startAddr, ushort length, byte[] txbuf, int offset)
        {
            byte[] dat = new byte[length];
            Buffer.BlockCopy(txbuf, offset, dat, 0, length);
            byte[] cmd = UpgradeProtocol.UpgraderWriteEpcsContent(startAddr, length, dat);
            return SendBytes(cmd, 10);
        }
        public bool UpgraderEraseSector(int addr)
        {
            byte[] cmd = UpgradeProtocol.UpgraderEraseSector(addr);
            return SendBytes(cmd, 10);
        }
        public bool UpgraderReconfigFPGA(bool toUserMode)
        {
            byte[] cmd = UpgradeProtocol.UpgraderReconfigFPGA(toUserMode);
            return SendBytes(cmd, 10);
        }
        public bool UserImageSwitchToUpgrader()
        {
            byte[] cmd = UpgradeProtocol.UserImageSwitchToUpgrader();
            lock (_SendThreadLock)
            {
                if (!IsConnected) { return false; }
                try
                {
                    _CommandPort.Write(cmd, 0, cmd.Length);   // FPGA should be reconfiged to factory mode now
                    Disconnect();
                    return true;
                }
                catch (Exception ex)
                {
                    IsConnected = false;
                    return false;
                }
            }
        }
        public bool UpgraderReadLastUpgradeInfo()
        {
            byte[] info = new byte[256];
            if (UpgraderReadEpcsContent(0x00200000 - 512, 256, info, 0))
            {
                int infoEnd = 0;
                for (; infoEnd < 256; infoEnd++)
                {
                    if (info[infoEnd] == 0xff)
                    {
                        break;
                    }
                }
                LastUpgradeInfo = Encoding.ASCII.GetString(info, 0, infoEnd);
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion Public Functions

        protected bool SendBytes(byte[] cmd, int responseLen)
        {
            lock (_SendThreadLock)
            {
                if (!IsConnected) { return false; }
                try
                {
                    // clear receive buffer at first
                    _CommandPort.Read(_ReceiveBuf, 0, _CommandPort.BytesToRead);
                    // write command
                    _CommandPort.Write(cmd, 0, cmd.Length);
                    // query response repeately up to 1sec
                    int waitCnt = 0;
                    while(_CommandPort.BytesToRead < responseLen)
                    {
                        if(++waitCnt > 1000)
                        {
                            return false;
                        }
                        Thread.Sleep(1);
                    }
                    _ReceivedIndex = _CommandPort.Read(_ReceiveBuf, 0, responseLen);
                    // decode the response
                    return UpgradeProtocol.ResponseDecoding(_ReceiveBuf, _ReceivedIndex);
                }
                catch(TimeoutException ex)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    IsConnected = false;
                    Connect();
                    return false;
                }

            }
        }
    }
}
