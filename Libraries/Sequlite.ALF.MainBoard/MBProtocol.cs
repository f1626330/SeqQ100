using Hywire.CommLibrary;
using System;
using System.Collections.Generic;
using static Hywire.CommLibrary.SerialCommBase;

namespace Sequlite.ALF.MainBoard
{
    public static class MBProtocol
    {
        internal delegate void ChangeStatusHandle(CommStatus newStatus);
        internal static event ChangeStatusHandle OnChangingStatus;

        #region Protocol Enums
        public enum Addesses : byte
        {
            MainBoard = 1,
        }
        public enum Functions : byte
        {
            Query = 0x03,
            Setting = 0x10,
        }
        public enum Registers : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            GLEDIntensity,
            GLEDStatus,
            RLEDIntensity,
            RLEDStatus,
            OnOffInputs,
            ChemiTemperCtrlSwitch,
            ChemiTemperCtrlRamp,
            ChemiTemperCtrlPower,
            ChemiTemper,
            HeatSinkTemper,
            CoolerTemper,
            AmbientTemper,
            PDValue,
            WLEDIntensity,
            WLEDStatus,
            FilterPosition,
            YMotionSpeed,
            YMotionAccel,
            YMotionPos,
            YMotionEn,
            PMotionPos,
            TemperCtrlPro,
            TemperCtrlInt,
            TemperCtrlDif,
            PMotionEnable,
            PMotionPower,
            TemperCtrlPower,
        }
        public enum FrameBorder : byte
        {
            Head = 0x55,
            End = 0xaa,
        }
        public enum SettingResults
        {
            Finished = 0x00,
            BusyDoing = 0x01,
            Failed = 0x02,
        }
        #endregion Protocol Enums

        #region Properties
        internal static string DeviceType { get; private set; }
        internal static string HWVersion { get; private set; }
        internal static string FWVersion { get; private set; }
        internal static uint GLEDIntensity { get; private set; }
        internal static bool IsGLEDOn { get; private set; }
        internal static uint RLEDIntensity { get; private set; }
        internal static bool IsRLEDOn { get; private set; }
        internal static OnOffInputsType OnOffInputs { get; private set; } = new OnOffInputsType();
        internal static bool IsChemiTemperCtrlOn { get; private set; }
        internal static double ChemiTemperCtrlRamp { get; private set; }
        internal static uint ChemiTemperCtrlPower { get; private set; }
        internal static double ChemiTemper { get; private set; }
        internal static double HeatSinkTemper { get; private set; }
        internal static double CoolerTemper { get; private set; }
        internal static double AmbientTemper { get; private set; }
        internal static uint PDValue { get; private set; }
        internal static uint WLEDIntensity { get; private set; }
        internal static bool IsWLEDOn { get; private set; }
        internal static int FilterPos { get; private set; }
        internal static uint YMotionSpd { get; private set; }
        internal static uint YMotionAcc { get; private set; }
        internal static int YMotionPos { get; private set; }
        internal static bool IsYMotionEnabled { get; private set; }
        internal static int PMotionPos { get; private set; }
        internal static int TemperCtrlPro { get; private set; }
        internal static int TemperCtrlInt { get; private set; }
        internal static int TemperCtrlDif { get; private set; }
        internal static int TemperCtrlPower { get; private set; }
        internal static bool IsPMotionEnabled { get; private set; }
        internal static int PMotionPower { get; private set; }

        private static CommStatus _Status;
        internal static CommStatus Status
        {
            get { return _Status; }
            private set
            {
                _Status = value;
                OnChangingStatus?.Invoke(_Status);
            }
        }
        #endregion Properties

        #region Private Fields
        private static bool _SettingGLEDStatus;
        private static bool _SettingRLEDStatus;
        #endregion Private Fields

        #region Internal Classes
        public class ProtocolStruct
        {
            public FrameBorder ProtocolHead { get; } = FrameBorder.Head;
            public Addesses Address { get; set; }
            public Functions Function { get; set; }
            public Registers StartedModule { get; set; }
            public byte ModuleNums { get; set; }
            public byte[] DataField { get; set; } = new byte[0];
            public ushort CRC16Code { get; set; }
            public FrameBorder ProtocolEnd { get; } = FrameBorder.End;
        }
        #endregion Internal Classes

        #region Public Functions
        public static byte[] MasterRequest(ProtocolStruct sendingFrame)
        {
            List<byte> bytesList = new List<byte>();
            bytesList.Add((byte)sendingFrame.ProtocolHead);
            bytesList.Add((byte)sendingFrame.Address);
            bytesList.Add((byte)sendingFrame.Function);
            bytesList.Add((byte)sendingFrame.StartedModule);
            bytesList.Add(sendingFrame.ModuleNums);
            bytesList.AddRange(sendingFrame.DataField);
            bytesList.AddRange(BitConverter.GetBytes(sendingFrame.CRC16Code));
            bytesList.Add((byte)sendingFrame.ProtocolEnd);
            return bytesList.ToArray();
        }

        public static byte[] Query(Registers startedModule, byte numbers)
        {
            if (numbers <= 0)
            {
                throw new ArgumentOutOfRangeException("Register Numbers", "Setting register numbers out of range.");
            }
            List<byte> bytesList = new List<byte>();
            bytesList.Add((byte)FrameBorder.Head);
            bytesList.Add((byte)Addesses.MainBoard);
            bytesList.Add((byte)Functions.Query);
            bytesList.Add((byte)startedModule);
            bytesList.Add(numbers);
            bytesList.AddRange(BitConverter.GetBytes(CRC16Calculator.CRC_Cal(bytesList.ToArray(), 0, bytesList.Count)));
            bytesList.Add((byte)FrameBorder.End);
            return bytesList.ToArray();
        }

        public static byte[] Setting(Registers startedModule, byte numbers, byte[] dataField)
        {
            if (numbers <= 0 || numbers > 10)
            {
                throw new ArgumentOutOfRangeException("Register Numbers", "Setting register numbers out of range.");
            }
            if (dataField == null || dataField.Length != numbers * 4)
            {
                throw new ArgumentException("DataField is invalid");
            }
            List<byte> bytesList = new List<byte>();
            bytesList.Add((byte)FrameBorder.Head);
            bytesList.Add((byte)Addesses.MainBoard);
            bytesList.Add((byte)Functions.Setting);
            bytesList.Add((byte)startedModule);
            bytesList.Add(numbers);
            bytesList.AddRange(dataField);
            bytesList.AddRange(BitConverter.GetBytes(CRC16Calculator.CRC_Cal(bytesList.ToArray(), 0, bytesList.Count)));
            bytesList.Add((byte)FrameBorder.End);
            return bytesList.ToArray();
        }

        public static void ResponseDecoding(byte[] readBuf, int readIndex, out int detectedFrameIndex, out Registers startReg, out int regNumbers)
        {
            detectedFrameIndex = 0;
            startReg = 0;
            regNumbers = 0;

            int miniFrameLength = 12;
            if (readIndex < miniFrameLength) { return; }

            FrameBorder head;
            Addesses addr;
            Functions func = 0;
            //byte nums = 0;
            byte[] dataField;
            ushort receivedCRC = 0;
            int frameLength = 0;
            // search for valid response: head(Byte i+0) + addr(Byte i+1) + function(Byte i+2) + numbers(Byte i+4)
            int startIndex = 0;
            for (startIndex = 0; startIndex + miniFrameLength <= readIndex; startIndex++)
            {
                head = (FrameBorder)readBuf[startIndex];
                addr = (Addesses)readBuf[startIndex + 1];
                func = (Functions)readBuf[startIndex + 2];
                regNumbers = readBuf[startIndex + 4];
                if ((head == FrameBorder.Head) &&
                    (addr == Addesses.MainBoard) &&
                    ((func == Functions.Query) || (func == Functions.Setting)) &&
                    (regNumbers > 0))
                {
                    frameLength = regNumbers * 4 + 8;
                    if (startIndex + frameLength > readIndex)
                    {
                        return;
                    }
                    detectedFrameIndex = startIndex + frameLength;
                    if (readBuf[detectedFrameIndex - 1] != (byte)FrameBorder.End)
                    {
                        Status = CommStatus.UnknownResponse;
                        return;
                    }
                    receivedCRC = BitConverter.ToUInt16(readBuf, detectedFrameIndex - 3);
                    Status = CommStatus.GotResponse;
                    break;
                }
            }

            if (Status != CommStatus.GotResponse)
            {
                return;
            }

            // CRC verification
            ushort verifyCRC = CRC16Calculator.CRC_Cal(readBuf, startIndex, frameLength - 3);
            if (verifyCRC != receivedCRC)
            {
                Status = CommStatus.CrcFailed;
                return;
            }

            #region Query response
            if (func == Functions.Query)
            {
                dataField = new byte[regNumbers * 4];
                Buffer.BlockCopy(readBuf, startIndex + 5, dataField, 0, dataField.Length);
                int register = readBuf[startIndex + 3];
                startReg = (Registers)register;
                for (int i = 0; i < regNumbers; i++)
                {
                    register = (int)startReg + i;
                    int offset = i * 4;
                    switch ((Registers)register)
                    {
                        case Registers.DeviceType:
                            DeviceType = string.Format("{0}.{1}.{2}.{3}", dataField[offset + 0],
                                dataField[offset + 1], dataField[offset + 2], dataField[offset + 3]);
                            break;
                        case Registers.HWVersion:
                            HWVersion = string.Format("{0}.{1}.{2}.{3}", dataField[offset + 0],
                                dataField[offset + 1], dataField[offset + 2], dataField[offset + 3]);
                            break;
                        case Registers.FWVersion:
                            FWVersion = string.Format("{0}.{1}.{2}.{3}", dataField[i * 4 + 0],
                                dataField[offset + 1], dataField[offset + 2], dataField[offset + 3]);
                            break;
                        case Registers.GLEDIntensity:
                            GLEDIntensity = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.GLEDStatus:
                            IsGLEDOn = dataField[offset] == 0x01;
                            break;
                        case Registers.RLEDIntensity:
                            RLEDIntensity = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.RLEDStatus:
                            IsRLEDOn = dataField[offset] == 0x01;
                            break;
                        case Registers.OnOffInputs:
                            OnOffInputs.MapFromUint32((uint)MsbFirstBytesToInt(dataField, offset));
                            break;
                        case Registers.ChemiTemperCtrlSwitch:
                            IsChemiTemperCtrlOn = dataField[offset] == 0x01;
                            break;
                        case Registers.ChemiTemperCtrlRamp:
                            ChemiTemperCtrlRamp = BitConverter.ToInt32(dataField, offset) * 0.001;
                            break;
                        case Registers.ChemiTemperCtrlPower:
                            ChemiTemperCtrlPower = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.ChemiTemper:
                            ChemiTemper = BitConverter.ToInt32(dataField, offset) * 0.001;
                            break;
                        case Registers.HeatSinkTemper:
                            HeatSinkTemper = BitConverter.ToInt32(dataField, offset) * 0.01;
                            break;
                        case Registers.CoolerTemper:
                            CoolerTemper = BitConverter.ToInt32(dataField, offset) * 0.01;
                            break;
                        case Registers.AmbientTemper:
                            AmbientTemper = BitConverter.ToInt32(dataField, offset) * 0.01;
                            break;
                        case Registers.PDValue:
                            PDValue = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.WLEDIntensity:
                            WLEDIntensity = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.WLEDStatus:
                            IsWLEDOn = dataField[offset] == 0x01;
                            break;
                        case Registers.FilterPosition:
                            FilterPos = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.YMotionSpeed:
                            YMotionSpd = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.YMotionAccel:
                            YMotionAcc = BitConverter.ToUInt32(dataField, offset);
                            break;
                        case Registers.YMotionPos:
                            YMotionPos = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.YMotionEn:
                            IsYMotionEnabled = dataField[offset] == 0x01;
                            break;
                        case Registers.PMotionPos:
                            PMotionPos = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.TemperCtrlPro:
                            TemperCtrlPro = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.TemperCtrlInt:
                            TemperCtrlInt = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.TemperCtrlDif:
                            TemperCtrlDif = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.TemperCtrlPower:
                            TemperCtrlPower = BitConverter.ToInt32(dataField, offset);
                            break;
                        case Registers.PMotionEnable:
                            IsPMotionEnabled = dataField[offset] == 0x01;
                            break;
                        case Registers.PMotionPower:
                            PMotionPower = BitConverter.ToInt32(dataField, offset);
                            break;
                        default:
                            Status = CommStatus.UnknownResponse;
                            return;
                    }
                }
            }
            #endregion Query response

            #region Setting response
            else if (func == Functions.Setting)
            {
                SettingResults result = (SettingResults)readBuf[startIndex + 5];
                switch (result)
                {
                    case SettingResults.Finished:
                        dataField = new byte[regNumbers * 4];
                        Buffer.BlockCopy(readBuf, startIndex + 5, dataField, 0, dataField.Length);
                        int register = readBuf[startIndex + 3];
                        startReg = (Registers)register;
                        for (int i = 0; i < regNumbers; i++)
                        {
                            register = (int)startReg + i;
                            switch ((Registers)register)
                            {
                                case Registers.GLEDStatus:
                                    IsGLEDOn = _SettingGLEDStatus;
                                    break;
                                case Registers.RLEDStatus:
                                    IsRLEDOn = _SettingRLEDStatus;
                                    break;
                            }
                        }
                        break;
                    case SettingResults.BusyDoing:
                        break;
                    case SettingResults.Failed:
                        break;
                    default:
                        Status = CommStatus.UnknownResponse;
                        break;
                }
            }
            #endregion Setting response

            Status = CommStatus.Idle;
        }

        internal static void Presetting(byte[] command)
        {
            if (command[2] == 0x10)    // it's setting command
            {
                Registers startReg = (Registers)(command[3]);
                byte regNums = command[4];
                for (int i = 0; i < regNums; i++)
                {
                    Registers register = (Registers)((int)startReg + i);
                    int offset = i * 4;
                    switch (register)
                    {
                        case Registers.GLEDStatus:
                            _SettingGLEDStatus = command[offset + 5] == 0x01;
                            break;
                        case Registers.RLEDStatus:
                            _SettingRLEDStatus = command[offset + 5] == 0x01;
                            break;
                    }
                }
            }
        }
        #endregion Public Functions


        #region Private Functions
        static internal byte[] IntToMsbFirstBytes(int number)
        {
            byte[] result = new byte[4];
            result[0] = (byte)((number >> 24) & 0xff);
            result[1] = (byte)((number >> 16) & 0xff);
            result[2] = (byte)((number >> 8) & 0xff);
            result[3] = (byte)((number) & 0xff);
            return result;
        }
        static internal int MsbFirstBytesToInt(byte[] source, int offset)
        {
            byte[] reverse = { source[offset + 3], source[offset + 2], source[offset + 1], source[offset + 0] };
            return BitConverter.ToInt32(reverse, 0);
        }

        #endregion Private Functions
    }

    public class OnOffInputsType
    {
        public bool IsDoorOpen { get; set; }
        public bool IsOvflowSnsrOn { get; set; }
        public bool IsCartridgeSnsrOn { get; set; }
        public bool IsFCSensorOn { get; set; }
        public bool IsFCClampSnsrOn { get; set; }

        public uint ConvertToUint32()
        {
            uint result = 0;
            result |= (IsDoorOpen ? 0x80000000u : 0);
            result |= (IsOvflowSnsrOn ? 0x40000000u : 0);
            result |= (IsCartridgeSnsrOn ? 0x20000000u : 0);
            result |= (IsFCSensorOn ? 0x10000000u : 0);
            result |= (IsFCClampSnsrOn ? 0x08000000u : 0);

            return result;
        }

        public void MapFromUint32(uint input)
        {
            IsDoorOpen = (input & 0x80000000u) == 0x80000000u;
            IsOvflowSnsrOn = (input & 0x40000000u) == 0x40000000u;
            IsCartridgeSnsrOn = (input & 0x20000000u) == 0x20000000u;
            IsFCSensorOn = (input & 0x10000000u) == 0x10000000u;
            IsFCClampSnsrOn = (input & 0x08000000u) == 0x08000000u;
        }
    }

}
