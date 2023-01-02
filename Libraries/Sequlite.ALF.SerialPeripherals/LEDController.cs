using Hywire.CommLibrary;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.SerialPeripherals
{
    public class LEDController : SerialPeripheralBase
    {
        private static Object LockInstanceCreation = new Object();
        static LEDController _Controller = null;
        private ISeqLog Logger { get; } = SeqLogFactory.GetSeqFileLog("LED Controller");
        public static LEDController GetInstance()
        {
            if (_Controller == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_Controller == null)
                    {
                        _Controller = new LEDController();
                    }

                }
            }
            return _Controller;
        }
        #region Registers defination
        public enum Registers : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            LEDIntensity,
            LEDStatus,
            CameraSelect,
            AmbientTemper,
            PDValue,
            PDSampleValue,
            CameraTrigger,
            FirmwareReady,
            RelaunchDevice,
            GLEDCurrentAtPer1,
            GLEDCurrentAtPer10,
            GLEDCurrentAtPer20,
            GLEDCurrentAtPer30,
            GLEDCurrentAtPer40,
            GLEDCurrentAtPer50,
            GLEDCurrentAtPer60,
            GLEDCurrentAtPer70,
            GLEDCurrentAtPer80,
            GLEDCurrentAtPer90,
            GLEDCurrentAtPer100,
            RLEDCurrentAtPer1,
            RLEDCurrentAtPer10,
            RLEDCurrentAtPer20,
            RLEDCurrentAtPer30,
            RLEDCurrentAtPer40,
            RLEDCurrentAtPer50,
            RLEDCurrentAtPer60,
            RLEDCurrentAtPer70,
            RLEDCurrentAtPer80,
            RLEDCurrentAtPer90,
            RLEDCurrentAtPer100,
            WLEDCurrentAtPer1,
            WLEDCurrentAtPer10,
            WLEDCurrentAtPer20,
            WLEDCurrentAtPer30,
            WLEDCurrentAtPer40,
            WLEDCurrentAtPer50,
            WLEDCurrentAtPer60,
            WLEDCurrentAtPer70,
            WLEDCurrentAtPer80,
            WLEDCurrentAtPer90,
            WLEDCurrentAtPer100,
            PDSamplePoints,
            PDSampleData,
            GLEDDriveCurrent,
            RLEDDriveCurrent,
            WLEDDriveCurrent,
            G1R3CameraSN,
            G2R4CameraSN,
            BarCodeReaderTrigger,
            ZStagePowerCtrl,
        }
        public enum RegistersV2 : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            LEDIntensity,
            LEDStatus,
            CameraSelect,
            AmbientTemper,
            PDValue,
            PDSampleValue,
            CameraTrigger,
            FirmwareReady,
            RelaunchDevice,
            GLEDCurrentAtPer1,
            GLEDCurrentAtPer2,
            GLEDCurrentAtPer3,
            GLEDCurrentAtPer4,
            GLEDCurrentAtPer5,
            GLEDCurrentAtPer6,
            GLEDCurrentAtPer7,
            GLEDCurrentAtPer8,
            GLEDCurrentAtPer9,
            GLEDCurrentAtPer10,
            GLEDCurrentAtPer20,
            GLEDCurrentAtPer30,
            GLEDCurrentAtPer40,
            GLEDCurrentAtPer50,
            GLEDCurrentAtPer60,
            GLEDCurrentAtPer70,
            GLEDCurrentAtPer80,
            GLEDCurrentAtPer90,
            GLEDCurrentAtPer100,
            RLEDCurrentAtPer1,
            RLEDCurrentAtPer2,
            RLEDCurrentAtPer3,
            RLEDCurrentAtPer4,
            RLEDCurrentAtPer5,
            RLEDCurrentAtPer6,
            RLEDCurrentAtPer7,
            RLEDCurrentAtPer8,
            RLEDCurrentAtPer9,
            RLEDCurrentAtPer10,
            RLEDCurrentAtPer20,
            RLEDCurrentAtPer30,
            RLEDCurrentAtPer40,
            RLEDCurrentAtPer50,
            RLEDCurrentAtPer60,
            RLEDCurrentAtPer70,
            RLEDCurrentAtPer80,
            RLEDCurrentAtPer90,
            RLEDCurrentAtPer100,
            WLEDCurrentAtPer1,
            WLEDCurrentAtPer2,
            WLEDCurrentAtPer3,
            WLEDCurrentAtPer4,
            WLEDCurrentAtPer5,
            WLEDCurrentAtPer6,
            WLEDCurrentAtPer7,
            WLEDCurrentAtPer8,
            WLEDCurrentAtPer9,
            WLEDCurrentAtPer10,
            WLEDCurrentAtPer20,
            WLEDCurrentAtPer30,
            WLEDCurrentAtPer40,
            WLEDCurrentAtPer50,
            WLEDCurrentAtPer60,
            WLEDCurrentAtPer70,
            WLEDCurrentAtPer80,
            WLEDCurrentAtPer90,
            WLEDCurrentAtPer100,
            PDSamplePoints,
            PDSampleData,
            GLEDDriveCurrent,
            RLEDDriveCurrent,
            WLEDDriveCurrent,
            GLEDDriveVoltage,
            RLEDDriveVoltage,
            WLEDDriveVoltage,
            G1R3CameraSN,
            G2R4CameraSN,
            BarCodeReaderTrigger,
            ZStagePowerCtrl,
        }
        #endregion Registers defination

        #region Private Fields
        #endregion Private Fields

        private LEDController() : base(AddressTypes.ADDR_LED)
        {
            GLEDCalibrateCurrents = new Dictionary<int, int>();
            GLEDCalibrateCurrents.Add(1, 0);
            GLEDCalibrateCurrents.Add(2, 0);
            GLEDCalibrateCurrents.Add(3, 0);
            GLEDCalibrateCurrents.Add(4, 0);
            GLEDCalibrateCurrents.Add(5, 0);
            GLEDCalibrateCurrents.Add(6, 0);
            GLEDCalibrateCurrents.Add(7, 0);
            GLEDCalibrateCurrents.Add(8, 0);
            GLEDCalibrateCurrents.Add(9, 0);
            GLEDCalibrateCurrents.Add(10, 0);
            GLEDCalibrateCurrents.Add(20, 0);
            GLEDCalibrateCurrents.Add(30, 0);
            GLEDCalibrateCurrents.Add(40, 0);
            GLEDCalibrateCurrents.Add(50, 0);
            GLEDCalibrateCurrents.Add(60, 0);
            GLEDCalibrateCurrents.Add(70, 0);
            GLEDCalibrateCurrents.Add(80, 0);
            GLEDCalibrateCurrents.Add(90, 0);
            GLEDCalibrateCurrents.Add(100, 0);
            RLEDCalibrateCurrents = new Dictionary<int, int>();
            RLEDCalibrateCurrents.Add(1, 0);
            RLEDCalibrateCurrents.Add(2, 0);
            RLEDCalibrateCurrents.Add(3, 0);
            RLEDCalibrateCurrents.Add(4, 0);
            RLEDCalibrateCurrents.Add(5, 0);
            RLEDCalibrateCurrents.Add(6, 0);
            RLEDCalibrateCurrents.Add(7, 0);
            RLEDCalibrateCurrents.Add(8, 0);
            RLEDCalibrateCurrents.Add(9, 0);
            RLEDCalibrateCurrents.Add(10, 0);
            RLEDCalibrateCurrents.Add(20, 0);
            RLEDCalibrateCurrents.Add(30, 0);
            RLEDCalibrateCurrents.Add(40, 0);
            RLEDCalibrateCurrents.Add(50, 0);
            RLEDCalibrateCurrents.Add(60, 0);
            RLEDCalibrateCurrents.Add(70, 0);
            RLEDCalibrateCurrents.Add(80, 0);
            RLEDCalibrateCurrents.Add(90, 0);
            RLEDCalibrateCurrents.Add(100, 0);
            WLEDCalibrateCurrents = new Dictionary<int, int>();
            WLEDCalibrateCurrents.Add(1, 0);
            WLEDCalibrateCurrents.Add(2, 0);
            WLEDCalibrateCurrents.Add(3, 0);
            WLEDCalibrateCurrents.Add(4, 0);
            WLEDCalibrateCurrents.Add(5, 0);
            WLEDCalibrateCurrents.Add(6, 0);
            WLEDCalibrateCurrents.Add(7, 0);
            WLEDCalibrateCurrents.Add(8, 0);
            WLEDCalibrateCurrents.Add(9, 0);
            WLEDCalibrateCurrents.Add(10, 0);
            WLEDCalibrateCurrents.Add(20, 0);
            WLEDCalibrateCurrents.Add(30, 0);
            WLEDCalibrateCurrents.Add(40, 0);
            WLEDCalibrateCurrents.Add(50, 0);
            WLEDCalibrateCurrents.Add(60, 0);
            WLEDCalibrateCurrents.Add(70, 0);
            WLEDCalibrateCurrents.Add(80, 0);
            WLEDCalibrateCurrents.Add(90, 0);
            WLEDCalibrateCurrents.Add(100, 0);
        }

        #region Public Properties
        public uint GLEDIntensity { get; private set; }
        public bool GLEDStatus { get; private set; }
        public bool GCameraSelect { get; private set; }
        public uint RLEDIntensity { get; private set; }
        public bool RLEDStatus { get; private set; }
        public bool RCameraSelect { get; private set; }
        public uint WLEDIntensity { get; private set; }
        public bool WLEDStatus { get; private set; }
        public bool WCameraSelect { get; private set; }
        public double AmbientTemper { get; private set; }
        public int PDValue { get; private set; }
        public uint PDSampleValue { get; private set; }
        public uint[] PDCurve { get; private set; }
        public Dictionary<int, int> GLEDCalibrateCurrents { get; private set; }
        public Dictionary<int, int> RLEDCalibrateCurrents { get; private set; }
        public Dictionary<int, int> WLEDCalibrateCurrents { get; private set; }
        public uint G1R3CameraSN { get;private set; }
        public uint G2R4CameraSN { get; private set; }
        public bool IsProtocolRev2 { get; set; }
        #endregion Public Properties


        #region Public Functions
        public bool ReadRegisters(Registers startReg, byte regNums)
        {
            if (!IsProtocolRev2)
            {
                return ReadRegisters((byte)startReg, regNums);
            }
            else
            {
                if (startReg >= Registers.DeviceType && startReg <= Registers.PDSampleValue)
                {
                    return ReadRegisters((byte)startReg, regNums);
                }
                switch (startReg)
                {
                    case Registers.PDSampleData:
                        return ReadRegisters((byte)RegistersV2.PDSampleData, regNums);
                    case Registers.PDSamplePoints:
                        return ReadRegisters((byte)RegistersV2.PDSamplePoints, regNums);
                    case Registers.GLEDDriveCurrent:
                        return ReadRegisters((byte)RegistersV2.GLEDDriveCurrent, regNums);
                    case Registers.RLEDDriveCurrent:
                        return ReadRegisters((byte)RegistersV2.RLEDDriveCurrent, regNums);
                    case Registers.WLEDDriveCurrent:
                        return ReadRegisters((byte)RegistersV2.WLEDDriveCurrent, regNums);
                    case Registers.G1R3CameraSN:
                        return ReadRegisters((byte)RegistersV2.G1R3CameraSN, regNums);
                    case Registers.G2R4CameraSN:
                        return ReadRegisters((byte)RegistersV2.G2R4CameraSN, regNums);
                    case Registers.BarCodeReaderTrigger:
                        return ReadRegisters((byte)RegistersV2.BarCodeReaderTrigger, regNums);
                    case Registers.ZStagePowerCtrl:
                        return ReadRegisters((byte)RegistersV2.ZStagePowerCtrl, regNums);
                    default:
                        return false;
                }
            }
        }
        public bool ReadRegisters(RegistersV2 startReg, byte regNums)
        {
            if (IsProtocolRev2)
            {
                return ReadRegisters((byte)startReg, regNums);
            }
            else
            {
                return false;
            }
        }
        public SettingResults WriteRegisters(Registers startReg, byte regNums, int[] values)
        {
            if (!IsProtocolRev2)
            {
                return WriteRegisters((byte)startReg, regNums, values);
            }
            else
            {
                if (startReg <= Registers.RelaunchDevice)
                {
                    return WriteRegisters((byte)startReg, regNums, values);
                }
                switch (startReg)
                {
                    case Registers.PDSamplePoints:
                        return WriteRegisters((byte)RegistersV2.PDSamplePoints, regNums, values);
                    case Registers.GLEDDriveCurrent:
                        return WriteRegisters((byte)RegistersV2.GLEDDriveCurrent, regNums, values);
                    case Registers.RLEDDriveCurrent:
                        return WriteRegisters((byte)RegistersV2.RLEDDriveCurrent, regNums, values);
                    case Registers.WLEDDriveCurrent:
                        return WriteRegisters((byte)RegistersV2.WLEDDriveCurrent, regNums, values);
                    case Registers.G1R3CameraSN:
                        return WriteRegisters((byte)RegistersV2.G1R3CameraSN, regNums, values);
                    case Registers.G2R4CameraSN:
                        return WriteRegisters((byte)RegistersV2.G2R4CameraSN, regNums, values);
                    case Registers.BarCodeReaderTrigger:
                        return WriteRegisters((byte)RegistersV2.BarCodeReaderTrigger, regNums, values);
                    case Registers.ZStagePowerCtrl:
                        return WriteRegisters((byte)RegistersV2.ZStagePowerCtrl, regNums, values);
                    default:
                        return SettingResults.UnknownRegister;
                }
            }
        }
        public SettingResults WriteRegisters(RegistersV2 startReg, byte regNums, int[] values)
        {
            if (!IsProtocolRev2)
            {
                return SettingResults.Error;
            }
            return WriteRegisters((byte)startReg, regNums, values);
        }

        public bool SetLEDIntensity(LEDTypes ledType, int intensity)
        {
            Logger.Log($"SetLEDIntensity:{ledType},{intensity}");
            Registers reg = LEDController.Registers.LEDIntensity;
            byte ledMask = 0x00;
            int writeData = 0;
            switch (ledType)
            {
                case LEDTypes.Green:
                    ledMask |= 0x04;
                    writeData |= (intensity & 0xff) << 16;
                    break;
                case LEDTypes.Red:
                    ledMask |= 0x02;
                    writeData |= (intensity & 0xff) << 8;
                    break;
                case LEDTypes.White:
                    ledMask |= 0x01;
                    writeData |= intensity & 0xff;
                    break;
            }
            writeData |= ledMask << 24;
            return WriteRegisters(reg, 1, new int[] { writeData }) == SettingResults.OK;
        }

        public bool SetLEDStatus(LEDTypes led, bool SetOn)
        {
            Registers reg = LEDController.Registers.LEDStatus;
            byte ledMask = 0x00;
            int writeData = 0;
            switch (led)
            {
                case LEDTypes.Green:
                    ledMask |= 0x04;
                    writeData |= (SetOn ? 1 : 0) << 16;
                    break;
                case LEDTypes.Red:
                    ledMask |= 0x02;
                    writeData |= (SetOn ? 1 : 0) << 8;
                    break;
                case LEDTypes.White:
                    ledMask |= 0x01;
                    writeData |= SetOn ? 1 : 0;
                    break;
            }
            writeData |= ledMask << 24;
            return WriteRegisters(reg, 1, new int[] { writeData}) == SettingResults.OK;
        }

        public bool TurnOnLEDWhileTurnOffOthers(LEDTypes led)
        {
            Registers reg = LEDController.Registers.LEDStatus;
            byte ledMask = 0x07;
            int writeData = 0;
            switch (led)
            {
                case LEDTypes.Green:
                    writeData = 1 << 16;
                    break;
                case LEDTypes.Red:
                    writeData = 1 << 8;
                    break;
                case LEDTypes.White:
                    writeData = 1;
                    break;
            }
            writeData |= ledMask << 24;
            return WriteRegisters(reg, 1, new int[] { writeData }) == SettingResults.OK;
        }
        public bool GetLEDStatus(LEDTypes led)
        {
            Registers reg = LEDController.Registers.LEDStatus;
            return ReadRegisters(reg, 1);
        }

        public bool GetPDValue()
        {
            return ReadRegisters(Registers.PDValue, 1);
        }
        public bool GetPDSampledValue()
        {
            return ReadRegisters(Registers.PDSampleValue, 1);
        }

        public bool SetLEDCalibratingCurrent(LEDTypes led, int percent, int calibratingCurrent)
        {
            if (!IsProtocolRev2)
            {
                Registers reg = LEDController.Registers.GLEDCurrentAtPer1;
                switch (led)
                {
                    case LEDTypes.Green:
                        reg = LEDController.Registers.GLEDCurrentAtPer1;
                        break;
                    case LEDTypes.Red:
                        reg = LEDController.Registers.RLEDCurrentAtPer1;
                        break;
                    case LEDTypes.White:
                        reg = LEDController.Registers.WLEDCurrentAtPer1;
                        break;
                }
                if (percent > 1)
                {
                    percent = percent / 10;
                    reg = (Registers)((int)(reg) + percent);
                }
                return WriteRegisters(reg, 1, new int[] { calibratingCurrent }) == SettingResults.OK;
            }
            else
            {
                RegistersV2 reg = RegistersV2.GLEDCurrentAtPer1;
                switch (led)
                {
                    case LEDTypes.Green:
                        reg = RegistersV2.GLEDCurrentAtPer1;
                        break;
                    case LEDTypes.Red:
                        reg = RegistersV2.RLEDCurrentAtPer1;
                        break;
                    case LEDTypes.White:
                        reg = RegistersV2.WLEDCurrentAtPer1;
                        break;
                }
                if (percent > 1 && percent < 10)
                {
                    reg = (RegistersV2)((int)(reg) + percent - 1);
                }
                else if (percent >= 10)
                {
                    reg = (RegistersV2)((int)reg + 8 + percent / 10);
                }
                return WriteRegisters(reg, 1, new int[] { calibratingCurrent }) == SettingResults.OK;
            }
        }

        public bool GetLEDCalibratingCurrent(LEDTypes led, int percent)
        {
            if (!IsProtocolRev2)
            {
                Registers reg = LEDController.Registers.GLEDCurrentAtPer1;
                switch (led)
                {
                    case LEDTypes.Green:
                        reg = LEDController.Registers.GLEDCurrentAtPer1;
                        break;
                    case LEDTypes.Red:
                        reg = LEDController.Registers.RLEDCurrentAtPer1;
                        break;
                    case LEDTypes.White:
                        reg = LEDController.Registers.WLEDCurrentAtPer1;
                        break;
                }
                if (percent > 1)
                {
                    percent = percent / 10;
                    reg = (Registers)((int)(reg) + percent);
                }
                return ReadRegisters(reg, 1);
            }
            else
            {
                RegistersV2 reg = RegistersV2.GLEDCurrentAtPer1;
                switch (led)
                {
                    case LEDTypes.Green:
                        reg = RegistersV2.GLEDCurrentAtPer1;
                        break;
                    case LEDTypes.Red:
                        reg = RegistersV2.RLEDCurrentAtPer1;
                        break;
                    case LEDTypes.White:
                        reg = RegistersV2.WLEDCurrentAtPer1;
                        break;
                }
                if (percent > 1 && percent < 10)
                {
                    reg = (RegistersV2)((int)reg + percent - 1);
                }
                else if (percent >= 10)
                {
                    reg = (RegistersV2)((int)reg + 8 + percent / 10);
                }
                return ReadRegisters((byte)reg, 1);
            }
        }

        public bool SetLEDDriveCurrent(LEDTypes led, int current)
        {
            Registers reg = Registers.GLEDDriveCurrent;
            switch (led)
            {
                case LEDTypes.Green:
                    reg = Registers.GLEDDriveCurrent;
                    break;
                case LEDTypes.Red:
                    reg = Registers.RLEDDriveCurrent;
                    break;
                case LEDTypes.White:
                    reg = Registers.WLEDDriveCurrent;
                    break;
            }
            return WriteRegisters(reg, 1, new int[] { current }) == SettingResults.OK;
        }
        /// <summary>
        /// set camera exposure selection, note only one led can be enabled at the same time
        /// </summary>
        /// <param name="led"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetLEDControlledByCamera(LEDTypes led, bool enable)
        {
            Logger.Log(string.Format("SetLEDControlledByCamera:{0},{1}", led.ToString(), (enable ? "enable" : "disable")));
            Registers reg = Registers.CameraSelect;
            byte ledMask = 0x07;
            int writeData = 0;
            switch (led)
            {
                case LEDTypes.Green:
                    //ledMask |= 0x04;
                    writeData |= (enable ? 1 : 0) << 16;
                    break;
                case LEDTypes.Red:
                    //ledMask |= 0x02;
                    writeData |= (enable ? 1 : 0) << 8;
                    break;
                case LEDTypes.White:
                    //ledMask |= 0x01;
                    writeData |= enable ? 1 : 0;
                    break;
            }
            writeData |= ledMask << 24;
            return WriteRegisters(reg, 1, new int[] { writeData }) == SettingResults.OK;
        }
        public bool SendCameraTrigger()
        {
            Logger.Log("Send Trigger");
            return WriteRegisters(Registers.CameraTrigger, 1, new int[] { 1 }) == SettingResults.OK;
        }
        /// <summary>
        /// Read Camera Serial Numbers for each channel (G1/R3 and G2/R4)
        /// </summary>
        /// <returns></returns>
        public bool GetCameraMap()
        {
            return ReadRegisters(Registers.G1R3CameraSN, 2);
        }
        public bool SetZstagePowerStatus(bool powerOn)
        {
            return WriteRegisters(Registers.ZStagePowerCtrl, 1, new int[] { powerOn ? 1 : 0 }) == SettingResults.OK;
        }
        public bool SetLEDDriveVoltage(LEDTypes led, int percent)
        {
            RegistersV2 reg = RegistersV2.GLEDDriveVoltage;
            switch (led)
            {
                case LEDTypes.Green:
                    reg = RegistersV2.GLEDDriveVoltage;
                    break;
                case LEDTypes.Red:
                    reg = RegistersV2.RLEDDriveVoltage;
                    break;
                case LEDTypes.White:
                    reg = RegistersV2.WLEDDriveVoltage;
                    break;
            }
            return WriteRegisters(reg, 1, new int[] { percent }) == SettingResults.OK;
        }
        #endregion Public Functions

        #region Private Functions
        #endregion Private Functions

        #region Override Functions
        public override string[] GetRegisterNames()
        {
            if (!IsProtocolRev2)
            {
                var oldNames = Enum.GetNames(typeof(Registers));
                return oldNames;
            }
            var names = Enum.GetNames(typeof(RegistersV2));
            return names;
        }
        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            detectedFrameIndex = 0;
            if (Status != CommStatus.Hearing) { return; }
            _Answer = ALFCommonProtocol.MapResponseFromBytes(ReadBuf, ReadIndex, out detectedFrameIndex);
            try
            {
                if (_Answer != null)
                {
                    if (!_Answer.IsCrcCorrect)
                    {
                        Status = CommStatus.Idle;
                        return;
                    }
                    if (_Answer.Function == FunctionTypes.Read)
                    {
                        int dataOffset = 0;
                        int regLoop = 0;
                        for (int i = 0; i < _Answer.RegNums; i++)
                        {
                            if (!IsProtocolRev2)
                            {
                                switch ((Registers)(_Answer.StartReg + regLoop))
                                {
                                    case Registers.DeviceType:
                                        DeviceType = Encoding.ASCII.GetString(_Answer.DataField, dataOffset, 4);
                                        break;
                                    case Registers.HWVersion:
                                        HWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                    _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                        break;
                                    case Registers.FWVersion:
                                        FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                    _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                        break;
                                    case Registers.AmbientTemper:
                                        AmbientTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                        break;
                                    case Registers.LEDIntensity:
                                        WLEDIntensity = _Answer.DataField[dataOffset];
                                        RLEDIntensity = _Answer.DataField[dataOffset + 1];
                                        GLEDIntensity = _Answer.DataField[dataOffset + 2];
                                        break;
                                    case Registers.LEDStatus:
                                        WLEDStatus = _Answer.DataField[dataOffset] == 0x01;
                                        RLEDStatus = _Answer.DataField[dataOffset + 1] == 0x01;
                                        GLEDStatus = _Answer.DataField[dataOffset + 2] == 0x01;
                                        break;
                                    case Registers.CameraSelect:
                                        WCameraSelect = _Answer.DataField[dataOffset] == 0x01;
                                        RCameraSelect = _Answer.DataField[dataOffset + 1] == 0x01;
                                        GCameraSelect = _Answer.DataField[dataOffset + 2] == 0x01;
                                        break;
                                    case Registers.PDValue:
                                        PDValue = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.PDSampleValue:
                                        PDSampleValue = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.PDSampleData:
                                        PDCurve = new uint[(_Answer.DataField.Length - dataOffset) / 4];
                                        for (int loop = 0; loop < PDCurve.Length; loop++)
                                        {
                                            PDCurve[loop] = BitConverter.ToUInt32(_Answer.DataField, dataOffset + loop * 4);
                                        }
                                        dataOffset += 4 * (PDCurve.Length - 1);
                                        i += PDCurve.Length - 1;
                                        break;
                                    case Registers.GLEDCurrentAtPer1:
                                        GLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer10:
                                        GLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer20:
                                        GLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer30:
                                        GLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer40:
                                        GLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer50:
                                        GLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer60:
                                        GLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer70:
                                        GLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer80:
                                        GLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer90:
                                        GLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.GLEDCurrentAtPer100:
                                        GLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer1:
                                        RLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer10:
                                        RLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer20:
                                        RLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer30:
                                        RLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer40:
                                        RLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer50:
                                        RLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer60:
                                        RLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer70:
                                        RLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer80:
                                        RLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer90:
                                        RLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.RLEDCurrentAtPer100:
                                        RLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer1:
                                        WLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer10:
                                        WLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer20:
                                        WLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer30:
                                        WLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer40:
                                        WLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer50:
                                        WLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer60:
                                        WLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer70:
                                        WLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer80:
                                        WLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer90:
                                        WLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.WLEDCurrentAtPer100:
                                        WLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.G1R3CameraSN:
                                        G1R3CameraSN = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case Registers.G2R4CameraSN:
                                        G2R4CameraSN = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                }
                            }
                            else
                            {
                                switch ((RegistersV2)(_Answer.StartReg + regLoop))
                                {
                                    case RegistersV2.DeviceType:
                                        DeviceType = Encoding.ASCII.GetString(_Answer.DataField, dataOffset, 4);
                                        break;
                                    case RegistersV2.HWVersion:
                                        HWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                    _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                        break;
                                    case RegistersV2.FWVersion:
                                        FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                    _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                        break;
                                    case RegistersV2.AmbientTemper:
                                        AmbientTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                        break;
                                    case RegistersV2.LEDIntensity:
                                        WLEDIntensity = _Answer.DataField[dataOffset];
                                        RLEDIntensity = _Answer.DataField[dataOffset + 1];
                                        GLEDIntensity = _Answer.DataField[dataOffset + 2];
                                        break;
                                    case RegistersV2.LEDStatus:
                                        WLEDStatus = _Answer.DataField[dataOffset] == 0x01;
                                        RLEDStatus = _Answer.DataField[dataOffset + 1] == 0x01;
                                        GLEDStatus = _Answer.DataField[dataOffset + 2] == 0x01;
                                        break;
                                    case RegistersV2.CameraSelect:
                                        WCameraSelect = _Answer.DataField[dataOffset] == 0x01;
                                        RCameraSelect = _Answer.DataField[dataOffset + 1] == 0x01;
                                        GCameraSelect = _Answer.DataField[dataOffset + 2] == 0x01;
                                        break;
                                    case RegistersV2.PDValue:
                                        PDValue = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.PDSampleValue:
                                        PDSampleValue = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.PDSampleData:
                                        PDCurve = new uint[(_Answer.DataField.Length - dataOffset) / 4];
                                        for (int loop = 0; loop < PDCurve.Length; loop++)
                                        {
                                            PDCurve[loop] = BitConverter.ToUInt32(_Answer.DataField, dataOffset + loop * 4);
                                        }
                                        dataOffset += 4 * (PDCurve.Length - 1);
                                        i += PDCurve.Length - 1;
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer1:
                                        GLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer2:
                                        GLEDCalibrateCurrents[2] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer3:
                                        GLEDCalibrateCurrents[3] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer4:
                                        GLEDCalibrateCurrents[4] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer5:
                                        GLEDCalibrateCurrents[5] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer6:
                                        GLEDCalibrateCurrents[6] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer7:
                                        GLEDCalibrateCurrents[7] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer8:
                                        GLEDCalibrateCurrents[8] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer9:
                                        GLEDCalibrateCurrents[9] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer10:
                                        GLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer20:
                                        GLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer30:
                                        GLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer40:
                                        GLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer50:
                                        GLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer60:
                                        GLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer70:
                                        GLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer80:
                                        GLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer90:
                                        GLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.GLEDCurrentAtPer100:
                                        GLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer1:
                                        RLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer2:
                                        RLEDCalibrateCurrents[2] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer3:
                                        RLEDCalibrateCurrents[3] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer4:
                                        RLEDCalibrateCurrents[4] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer5:
                                        RLEDCalibrateCurrents[5] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer6:
                                        RLEDCalibrateCurrents[6] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer7:
                                        RLEDCalibrateCurrents[7] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer8:
                                        RLEDCalibrateCurrents[8] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer9:
                                        RLEDCalibrateCurrents[9] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer10:
                                        RLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer20:
                                        RLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer30:
                                        RLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer40:
                                        RLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer50:
                                        RLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer60:
                                        RLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer70:
                                        RLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer80:
                                        RLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer90:
                                        RLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.RLEDCurrentAtPer100:
                                        RLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer1:
                                        WLEDCalibrateCurrents[1] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer2:
                                        WLEDCalibrateCurrents[2] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer3:
                                        WLEDCalibrateCurrents[3] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer4:
                                        WLEDCalibrateCurrents[4] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer5:
                                        WLEDCalibrateCurrents[5] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer6:
                                        WLEDCalibrateCurrents[6] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer7:
                                        WLEDCalibrateCurrents[7] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer8:
                                        WLEDCalibrateCurrents[8] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer9:
                                        WLEDCalibrateCurrents[9] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer10:
                                        WLEDCalibrateCurrents[10] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer20:
                                        WLEDCalibrateCurrents[20] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer30:
                                        WLEDCalibrateCurrents[30] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer40:
                                        WLEDCalibrateCurrents[40] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer50:
                                        WLEDCalibrateCurrents[50] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer60:
                                        WLEDCalibrateCurrents[60] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer70:
                                        WLEDCalibrateCurrents[70] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer80:
                                        WLEDCalibrateCurrents[80] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer90:
                                        WLEDCalibrateCurrents[90] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.WLEDCurrentAtPer100:
                                        WLEDCalibrateCurrents[100] = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.G1R3CameraSN:
                                        G1R3CameraSN = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                    case RegistersV2.G2R4CameraSN:
                                        G2R4CameraSN = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                        break;
                                }
                            }
                            dataOffset += 4;
                            regLoop++;
                        }
                    }
                    Status = CommStatus.Idle;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion Override Functions
    }
}
