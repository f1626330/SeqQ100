using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.SerialPeripherals
{
    public class BarCodeReader
    {
        private static Object LockInstanceCreation = new Object();
        static BarCodeReader _Controller = null;
        public static BarCodeReader GetInstance()
        {
            if (_Controller == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_Controller == null)
                    {
                        _Controller = new BarCodeReader();
                    }

                }
            }
            return _Controller;
        }
        public enum ScanTriggers
        {
            Level = 0,
            Pulse = 2,
            Continious = 4,
            Host = 8,
            AutoSense = 9,
            LevelContinious = 10,
        }
        #region Private Fields
        private SerialPort _Port;
        private object _ThreadLock = new object();
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("BARCODEREADER");
        #endregion Private Fields

        private BarCodeReader()
        {

        }

        #region Public Properties
        public bool IsConnected { get; private set; }
        #endregion Public Properties
        #region Public Functions
        public bool Connect(string portName, int baudrate = 9600)
        {
            try
            {
                _Port = new SerialPort();
                _Port.PortName = portName;
                _Port.BaudRate = baudrate;
                _Port.Parity = Parity.None;
                _Port.DataBits = 8;
                _Port.StopBits = StopBits.One;
                _Port.NewLine = "\r\n";
                _Port.Open();
                if (SetPowerManagement(false))  // to simplify scanning process
                {
                    if (SetTriggerMode(ScanTriggers.Host))
                    {
                        if (SetCodeSuffix(1))   // suffix: "\r\n"
                        {
                            IsConnected = true;
                            return true;
                        }
                    }
                }
                IsConnected = false;
                _Port.Close();
                return false;
            }
            catch (Exception ex)
            {
                if (_Port.IsOpen)
                {
                    _Port.Close();
                }
                IsConnected = false;
                Logger.LogError("Connection failed: " + ex.ToString());
                return IsConnected;
            }
        }

        public bool Connect(int baudRate = 9600)
        {
            var portList = SerialPort.GetPortNames();
            foreach (var port in portList)
            {
                if (Connect(port, baudRate))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// sync read bar code. it will not return until read the code or timeout occured.
        /// </summary>
        /// <param name="readTimeout">unit of millisecond</param>
        /// <returns></returns>
        public string ScanBarCode(int readTimeout = 4000)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return null; }
                byte[] cmd = BarCodeReaderProtocol.Pack_StartScan();
                _Port.Write(cmd, 0, cmd.Length);
                if (WaitAck() == false) { return null; }
                _Port.ReadTimeout = readTimeout;
                try
                {
                    return _Port.ReadLine();
                }
                catch (TimeoutException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// set the scanning duration.
        /// </summary>
        /// <param name="duration">unit of second.</param>
        /// <returns></returns>
        public bool SetScanDuration(byte duration)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetScanDuration(duration);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        /// <summary>
        /// set power management of the reader.
        /// </summary>
        /// <param name="powerSave"></param>
        /// <returns></returns>
        public bool SetPowerManagement(bool powerSave)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetPowerManager(powerSave);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        /// <summary>
        /// set read code suffix.
        /// </summary>
        /// <param name="suffixType">0: none; 1: "\r\n"; 2: "\n"</param>
        /// <returns></returns>
        public bool SetCodeSuffix(byte suffixType)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetSuffix(suffixType);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        /// <summary>
        /// enable/disable the beeper when scanned code.
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetBeeper(bool enable)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetBeeper(enable);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        public bool SetTriggerMode(ScanTriggers mode)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetTrigger(mode);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control">0: on while scanning code; 1: on; 2: off;</param>
        /// <returns></returns>
        public bool SetFloodLight(byte control)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] cmd = BarCodeReaderProtocol.Pack_SetFloodLight(control);
                _Port.Write(cmd, 0, cmd.Length);
                return WaitAck();
            }
        }

        #endregion Public Functions

        #region Private Functions
        private bool WaitAck(int timeoutMsec=500)
        {
            int timeoutCnt = 0;
            int rxlen = 0;
            byte[] rxbuf = new byte[100];
            while(_Port.BytesToRead < 6)
            {
                timeoutCnt++;
                if (timeoutCnt > timeoutMsec)
                {
                    return false;
                }
                Thread.Sleep(1);
            }
            rxlen = _Port.BytesToRead;
            _Port.Read(rxbuf, 0, rxlen);
            if(rxbuf[0]+2 > rxlen)
            {
                _Port.Read(rxbuf, rxlen, rxbuf[0] + 2 - rxlen);
            }
            var ackPack = BarCodeReaderProtocol.Pack_AckFromDevice();
            for(int i = 0; i < 6; i++)
            {
                if (rxbuf[i] != ackPack[i])
                {
                    return false;
                }
            }
            return true;
        }
        #endregion Private Functions
    }

    internal static class BarCodeReaderProtocol
    {
        public static byte Length { get; private set; }
        public static byte Operation { get; private set; }
        /// <summary>
        ///0: from reader; 4: from PC
        /// </summary>
        public static byte MsgSource { get; private set; }
        /// <summary>
        /// bit0: 0: first try; 1: retry
        /// bit1-2: reserved
        /// bit3: 0: setting valid temporarily; setting valid permanently;
        /// bit4-7: reserved
        /// </summary>
        public static byte Status { get; private set; }
        public static byte[] Data { get; private set; }

        private static byte[] GetBytes()
        {
            List<byte> result = new List<byte>();
            result.Add(Length);
            result.Add(Operation);
            result.Add(MsgSource);
            result.Add(Status);
            ushort checksum = Length;
            checksum += Operation;
            checksum += MsgSource;
            checksum += Status;
            if (Data != null && Data.Length > 0)
            {
                result.AddRange(Data);
                foreach(var val in Data)
                {
                    checksum += val;
                }
            }
            checksum = (ushort)(~checksum + 1);
            result.Add((byte)(checksum >> 8));
            result.Add((byte)(checksum & 0x00ff));
            return result.ToArray();
        }

        public static byte[] Pack_AckFromHost()
        {
            Length = 4;
            Operation = 0xd0;
            MsgSource = 0x04;
            Status = 0x00;
            Data = null;
            return GetBytes();
        }

        public static byte[] Pack_AckFromDevice()
        {
            Length = 4;
            Operation = 0xd0;
            MsgSource = 0x00;
            Status = 0x00;
            Data = null;
            return GetBytes();
        }

        public static byte[] Pack_StartScan()
        {
            Length = 4;
            Operation = 0xe4;
            MsgSource = 0x04;
            Status = 0x00;
            Data = null;
            return GetBytes();
        }

        public static byte[] Pack_SetScanDuration(byte duration)
        {
            Length = 7;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[3];
            Data[0] = 0xff;
            Data[1] = 0x88;
            Data[2] = (byte)(duration * 10);        //  step: 0.1 sec
            return GetBytes();
        }

        public static byte[] Pack_SetPowerManager(bool powerSave)
        {
            Length = 7;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[3];
            Data[0] = 0xff;
            Data[1] = 0x80;
            Data[2] = (byte)(powerSave ? 0x01 : 0x00);
            return GetBytes();
        }

        /// <summary>
        /// set read code suffix.
        /// </summary>
        /// <param name="suffix">0: none; 1: "\r\n"; 2: "\n"</param>
        /// <returns></returns>
        public static byte[] Pack_SetSuffix(byte suffix)
        {
            Length = 8;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[4];
            Data[0] = 0xff;
            Data[1] = 0xf2;
            Data[2] = 0x05;
            Data[3] = suffix;
            return GetBytes();
        }

        /// <summary>
        /// enable/disable the beeper when scanned code.
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public static byte[] Pack_SetBeeper(bool enable)
        {
            Length = 7;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[3];
            Data[0] = 0xff;
            Data[1] = 0x38;
            Data[2] = (byte)(enable ? 0x01 : 0x00);
            return GetBytes();
        }

        /// <summary>
        /// Set scan trigger mode.
        /// </summary>
        /// <param name="trigger">0:level;2:pulse;4:continious;8:host;9:auto;10:level continue;</param>
        /// <returns></returns>
        public static byte[] Pack_SetTrigger(BarCodeReader.ScanTriggers trigger)
        {
            Length = 7;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[3];
            Data[0] = 0xff;
            Data[1] = 0x8a;
            Data[2] = (byte)trigger;
            return GetBytes();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control">0: on while scanning; 1: on; 2: off</param>
        /// <returns></returns>
        public static byte[] Pack_SetFloodLight(byte control)
        {
            Length = 8;
            Operation = 0xc6;
            MsgSource = 0x04;
            Status = 0x00;
            Data = new byte[4];
            Data[0] = 0xff;
            Data[1] = 0xf2;
            Data[2] = 0x02;
            Data[3] = control;
            return GetBytes();
        }
    }
}
