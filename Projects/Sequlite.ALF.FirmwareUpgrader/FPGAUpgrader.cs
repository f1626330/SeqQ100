
using Hywire.CommLibrary;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.FirmwareUpgrader
{
    internal class FPGAUpgrader : SerialCommBase
    {
        public enum MDBSRegAddr
        {
            VersionInfoAddr = 0x0400,
            HoldEPCSAddr = 0x0401,
            ReleaseEPCSAddr = 0x0402,
            ReadEPCSTypeAddr = 0x0403,
            EraseEPCSAddr = 0x0404,
            ReadEPCSStatusAddr = 0x0405,
            EPCSContentAddr = 0x0500,
        }
        public enum FlashIdCode
        {
            EPCS1 = 0x10,
            EPCS4 = 0x12,
            EPCS16 = 0x14,
            EPCS64 = 0x16,
        }

        #region Private Fields
        private object _ThreadLock;
        #endregion Private Fields

        #region Public Properties
        public bool IsConnected { get; private set; }

        public string PreviousUpgradeInfo { get; private set; }
        public string EPCSType { get; private set; }
        public byte EPCSStatus { get; private set; }
        #endregion Public Properties

        public FPGAUpgrader() : base(1500)
        {
            _ThreadLock = new object();
        }

        #region Public Functions
        public bool Connect(int baudRate = 115200)
        {
            if (IsConnected)
            {
                return true;
            }
            string[] _availablePorts = SerialPort.GetPortNames();
            for (int i = 0; i < _availablePorts.Length; i++)
            {
                try
                {
                    Open(_availablePorts[i], baudRate);
                    var cmd = ModbusProtocol.ReadPreviousInfo();
                    if (SendBytes(cmd) == false)
                    {
                        Close();
                    }
                    else
                    {
                        IsConnected = true;
                        break;
                    }
                }
                catch
                {
                    if (IsOpened)
                    {
                        Close();
                    }
                }
            }
            return IsConnected;
        }
        public bool Connect(string portName, int baudRate = 115200)
        {
            if (IsConnected) { return true; }
            try
            {
                Open(portName, baudRate);
                var cmd = ModbusProtocol.ReadPreviousInfo();
                if (SendBytes(cmd) == false)
                {
                    Close();
                }
                else
                {
                    IsConnected = true;
                }
            }
            catch
            {
                if (IsOpened)
                {
                    Close();
                }
            }
            return IsConnected;
        }
        public bool ReadPreviousInfo()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.ReadPreviousInfo();
                return SendBytes(cmd);
            }
        }
        public bool ConnectEPCS()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.ConnectEPCS();
                return SendBytes(cmd);
            }
        }
        public bool ReleaseEPCS()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.ReleaseEPCS();
                return SendBytes(cmd);
            }
        }
        public bool ReadEPCSType()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.ReadEPCSType();
                return SendBytes(cmd);
            }
        }
        public bool ReadEPCSStatus()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.ReadEPCSStatus();
                return SendBytes(cmd);
            }
        }
        public bool EraseEPCS()
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.EraseEPCS();
                return SendBytes(cmd);
            }
        }
        public bool WriteEPCSMemory(int startAddr, byte[] data)
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.WriteEPCSMemory(startAddr, data);
                return SendBytes(cmd);
            }
        }
        public bool WriteUpgradeInfo(byte[] info)
        {
            lock (_ThreadLock)
            {
                var cmd = ModbusProtocol.WriteUpgradeInfo(info);
                return SendBytes(cmd);
            }
        }
        #endregion Public Functions

        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            detectedFrameIndex = 0;
            if (ReadIndex >= 7)
            {
                if (ReadBuf[0] == ModbusProtocol.DeviceAddress)
                {
                    if (ReadBuf[1] == (byte)ModbusProtocol.FunctionCode)
                    {
                        ushort _tempCRC;
                        if (ModbusProtocol.FunctionCode == ModbusProtocol.FunctionCodeTypes.Func_Read)
                        {
                            ushort _tempLength;
                            if (ReadBuf[2] == 0)   // '0' actually represents '256'
                            {
                                _tempLength = 256 + 5;
                            }
                            else
                            {
                                _tempLength = (ushort)(ReadBuf[2] + 5);
                            }
                            if (ReadIndex >= _tempLength)
                            {
                                _tempCRC = CRC16Calculator.CRC_Cal(ReadBuf, 0, _tempLength - 2);
                                if ((ReadBuf[_tempLength - 1] << 8 | ReadBuf[_tempLength - 2]) != _tempCRC)
                                {
                                    Status = CommStatus.CrcFailed;
                                    detectedFrameIndex = _tempLength;
                                    return;
                                }

                                byte[] dataBuffer = new byte[_tempLength-5];
                                for (byte i = 0; i < dataBuffer.Length; i++)
                                {
                                    dataBuffer[i] = ReadBuf[3 + i];
                                }
                                switch (ModbusProtocol.RegAddr)
                                {
                                    case ModbusProtocol.RegisterAddresses.VersionInfoAddr:
                                        char[] charBuffer = new char[dataBuffer.Length];
                                        Decoder ecd = Encoding.UTF8.GetDecoder();
                                        ecd.GetChars(dataBuffer, 0, dataBuffer.Length, charBuffer, 0);
                                        StringBuilder str = new StringBuilder();
                                        for (int i = 0; i < charBuffer.Length; i++)
                                        {
                                            str.Append(charBuffer[i]);
                                        }
                                        PreviousUpgradeInfo = str.ToString();
                                        break;
                                    case ModbusProtocol.RegisterAddresses.ReadEPCSTypeAddr:
                                        FlashIdCode type = (FlashIdCode)dataBuffer[1];
                                        EPCSType = type.ToString();
                                        break;
                                    case ModbusProtocol.RegisterAddresses.ReadEPCSStatusAddr:
                                        EPCSStatus = dataBuffer[1];
                                        break;
                                }
                                Status = CommStatus.Idle;
                                detectedFrameIndex = _tempLength;
                                return;
                            }
                        }
                        else if (ModbusProtocol.FunctionCode == ModbusProtocol.FunctionCodeTypes.Func_Write)
                        {
                            if (ReadIndex >= 8)
                            {
                                Status = CommStatus.Idle;
                                detectedFrameIndex = 8;
                                return;
                            }
                        }
                    }
                }
            }
            return;
        }
    }

    static class ModbusProtocol
    {
        public enum FunctionCodeTypes
        {
            Func_Read = 0x03,
            Func_Write = 0x10,
        }

        public enum RegisterAddresses : ushort
        {
            VersionInfoAddr = 0x0400,
            HoldEPCSAddr = 0x0401,
            ReleaseEPCSAddr = 0x0402,
            ReadEPCSTypeAddr = 0x0403,
            EraseEPCSAddr = 0x0404,
            ReadEPCSStatusAddr = 0x0405,
            EPCSContentAddr = 0x0500,
        }

        #region Public Properties
        public static byte DeviceAddress { get; } = 0x01;
        public static FunctionCodeTypes FunctionCode { get; private set; }
        public static RegisterAddresses RegAddr { get; set; }
        public static ushort RegNums { get; set; }
        #endregion Public Properties

        #region Private Fields
        #endregion Private Fields
        public static byte[] ReadPreviousInfo()
        {
            RegAddr = RegisterAddresses.VersionInfoAddr;
            RegNums = 1;

            return MasterRead();
        }
        public static byte[] ConnectEPCS()
        {
            RegAddr = RegisterAddresses.HoldEPCSAddr;
            RegNums = 1;

            return MasterWrite(new byte[] { 0, 0 }, 0, 2);
        }
        public static byte[] ReleaseEPCS()
        {
            RegAddr = RegisterAddresses.ReleaseEPCSAddr;
            RegNums = 1;

            return MasterWrite(new byte[] { 0, 0 }, 0, 2);
        }
        public static byte[] ReadEPCSType()
        {
            RegAddr = RegisterAddresses.ReadEPCSTypeAddr;
            RegNums = 1;

            return MasterRead();
        }
        public static byte[] ReadEPCSStatus()
        {
            RegAddr = RegisterAddresses.ReadEPCSStatusAddr;
            RegNums = 1;

            return MasterRead();
        }
        public static byte[] EraseEPCS()
        {
            RegAddr = RegisterAddresses.EraseEPCSAddr;
            RegNums = 1;

            return MasterWrite(new byte[] { 0, 0 }, 0, 2);
        }
        public static byte[] WriteEPCSMemory(int startAddr, byte[] data)
        {
            RegAddr = (RegisterAddresses)((ushort)RegisterAddresses.EPCSContentAddr | (ushort)((startAddr >> 16) & 0x00ff));
            RegNums = (ushort)(startAddr & 0x0000ffff);

            return MasterWrite(data, 0, data.Length);
        }
        public static byte[] WriteUpgradeInfo(byte[] info)
        {
            RegAddr = RegisterAddresses.VersionInfoAddr;
            byte[] tmpRealInfo;
            if (info.Length % 2 == 1)
            {
                tmpRealInfo = new byte[info.Length + 1];
                Buffer.BlockCopy(info, 0, tmpRealInfo, 0, info.Length);
                tmpRealInfo[info.Length] = 0xff;
            }
            else
            {
                tmpRealInfo = info;
            }
            int infoLength = tmpRealInfo.Length;
            if (infoLength > 220)
            {
                infoLength = 220;
            }
            RegNums = (ushort)(tmpRealInfo.Length + 1);

            return MasterWrite(tmpRealInfo, 0, infoLength);
        }

        static byte[] MasterRead()
        {
            byte[] txData = new byte[8];
            FunctionCode = FunctionCodeTypes.Func_Read;
            txData[0] = DeviceAddress;
            txData[1] = (byte)FunctionCode;
            txData[2] = (byte)((ushort)RegAddr / 256);
            txData[3] = (byte)((ushort)RegAddr % 256);
            txData[4] = (byte)(RegNums / 256);
            txData[5] = (byte)(RegNums % 256);

            var CRC = CRC16Calculator.CRC_Cal(txData, 0, 6);
            txData[6] = (byte)(CRC % 256);
            txData[7] = (byte)(CRC / 256);
            return txData;
        }
        static byte[] MasterWrite(byte[] data, int offset, int length)
        {
            byte[] txData = new byte[9 + length];
            FunctionCode = FunctionCodeTypes.Func_Write;
            txData[0] = DeviceAddress;
            txData[1] = (byte)FunctionCode;
            txData[2] = (byte)((ushort)RegAddr / 256);
            txData[3] = (byte)((ushort)RegAddr % 256);
            txData[4] = (byte)(RegNums / 256);
            txData[5] = (byte)(RegNums % 256);
            if (length >= 256)
            {
                txData[6] = 0;   // 用0代表256字节
                length = 256;
            }
            else
            {
                txData[6] = (byte)length;
            }
            for (int i = 0; i < length; i++)
            {
                txData[7 + i] = data[i];
            }

            var CRC = CRC16Calculator.CRC_Cal(txData, 0, (int)(7 + length));
            txData[7 + length] = (byte)(CRC % 256);
            txData[8 + length] = (byte)(CRC / 256);

            return txData;
        }
    }
}
