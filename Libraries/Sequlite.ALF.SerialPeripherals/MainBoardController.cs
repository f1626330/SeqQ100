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
    public class MainBoardController : SerialPeripheralBase
    {
        #region Registers defination
        public enum Registers : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            TemperCtrlP,
            TemperCtrlI,
            TemperCtrlD,
            ChemiTemperSwitch,
            ChemiTemperRamp,
            ChemiTemperPower,
            ChemiTemper,
            HeatSinkTemper,
            PCBTemper,
            OnoffInputs,
            BarCodeReaderCtrl,
            FirmwareReady,
            RelaunchDevice,
            DoorStatus,

            ChemiTemperHeatGain,
            ChemiTemperCoolGain,
        }
        public enum RegistersV2 : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            TemperCtrlP,
            TemperCtrlI,
            TemperCtrlD,
            ChemiTemperSwitch,
            ChemiTemper,
            HeatSinkTemper,
            PCBTemper,
            OnoffInputs,
            FirmwareReady,
            RelaunchDevice,
            ChemiTemperHeatGain,
            ChemiTemperCoolGain,
            FrontPanelLED,
            FluidPreHeatEnable,
            FluidPreHeatTemp,
            FluidPreHeatCtrlP,
            FluidPreHeatCtrlI,
            FluidPreHeatCtrlD,
            FluidPreHeatGain,
            FanSpeed,
        }
        #endregion Registers defination


        private static Object LockInstanceCreation = new Object();
        static MainBoardController _MainboardController = null;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("MainBoard Controller");
        public static MainBoardController GetInstance()
        {
            if (_MainboardController == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_MainboardController == null)
                    {
                        _MainboardController = new MainBoardController();
                    }

                }
            }
            return _MainboardController;
        }

        private MainBoardController():base(AddressTypes.ADDR_MB)
        {
        }

        #region Public Properties
        public double TemperCtrlP { get; private set; }
        public double TemperCtrlI { get; private set; }
        public double TemperCtrlD { get; private set; }
        public bool ChemiTemperCtrlOn { get; private set; }
        public double ChemiTemperRamp { get; private set; }
        public uint ChemiTemperPower { get; private set; }
        public double ChemiTemper { get; private set; }
        public double HeatSinkTemper { get; private set; }
        public double AmbientTemper { get; private set; }
        public bool FCClampStatus { get; private set; }
        public bool IsFCDoorAvailable { get; private set; }
        public bool IsFCDoorEnable { get; set; } = true;
        public bool FCDoorStatus { get; private set; }
        public bool BarCodeReaderReset { get; private set; }
        public bool BarCodeReaderTrigger { get; private set; }
        public bool? DoorIsOpen { get; private set; }
        public double ChemiTemperHeatGain { get; private set; }
        public double ChemiTemperCoolGain { get; private set; }
        public bool IsMachineRev2P4 { get; private set; }     // HW changed in ALF 2.4
        public bool IsProtocolRev2 { get; private set; }        // Protool changed in ALF 2.5
        public byte FrontPanelRLEDIntensity { get; private set; }
        public byte FrontPanelGLEDIntensity { get; private set; }
        public byte FrontPanelBLEDIntensity { get; private set; }
        public bool IsFluidPreheatAvailable { get; private set; }
        public bool FluidPreHeatingEnabled { get; private set; }
        public double FluidPreHeatingTgtTemper { get; private set; }
        public double FluidPreHeatingCrntTemper { get; private set; }
        public double FluidPreHeatCtrlKp { get; private set; }
        public double FluidPreHeatCtrlKi { get; private set; }
        public double FluidPreHeatCtrlKd { get; private set; }
        public double FluidPreHeatGain { get; private set; }
        public int EFanPWMValue { get; private set; }
        public int BFanPWMValue { get; private set; }
        public int FanPWMValue { get; private set; }
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
                if (startReg >= Registers.DeviceType && startReg <= Registers.ChemiTemperSwitch)
                {
                    return ReadRegisters((byte)startReg, regNums);
                }
                switch (startReg)
                {
                    case Registers.ChemiTemper:
                        return ReadRegisters((byte)RegistersV2.ChemiTemper, regNums);
                    case Registers.HeatSinkTemper:
                        return ReadRegisters((byte)RegistersV2.HeatSinkTemper, regNums);
                    case Registers.PCBTemper:
                        return ReadRegisters((byte)RegistersV2.PCBTemper, regNums);
                    case Registers.OnoffInputs:
                        return ReadRegisters((byte)RegistersV2.OnoffInputs, regNums);
                    case Registers.ChemiTemperHeatGain:
                        return ReadRegisters((byte)RegistersV2.ChemiTemperHeatGain, regNums);
                    case Registers.ChemiTemperCoolGain:
                        return ReadRegisters((byte)RegistersV2.ChemiTemperCoolGain, regNums);
                    default:
                        return false;
                }
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
                if (startReg >= Registers.DeviceType && startReg <= Registers.ChemiTemperSwitch)
                {
                    return WriteRegisters((byte)startReg, regNums, values);
                }
                switch (startReg)
                {
                    case Registers.ChemiTemper:
                        return WriteRegisters((byte)RegistersV2.ChemiTemper, regNums, values);
                    case Registers.FirmwareReady:
                        return WriteRegisters((byte)RegistersV2.FirmwareReady, regNums, values);
                    case Registers.RelaunchDevice:
                        return WriteRegisters((byte)RegistersV2.RelaunchDevice, regNums, values);
                    case Registers.ChemiTemperHeatGain:
                        return WriteRegisters((byte)RegistersV2.ChemiTemperHeatGain, regNums, values);
                    case Registers.ChemiTemperCoolGain:
                        return WriteRegisters((byte)RegistersV2.ChemiTemperCoolGain, regNums, values);
                    default:
                        return SettingResults.UnknownRegister;
                }
            }
        }

        public bool GetTemperCtrlParameters()
        {
            if (!IsProtocolRev2)
            {
                return ReadRegisters(Registers.TemperCtrlP, 6);
            }
            if (ReadRegisters((byte)RegistersV2.TemperCtrlP, 3))
            {
                return ReadRegisters((byte)RegistersV2.ChemiTemperHeatGain, 2);
            }
            return false;
        }
        public bool SetTemperCtrlParameters(int temperCtrlKp, int temperCtrlKi, int temperCtrlKd, int temperCtrlMaxCrnt)
        {
            if(WriteRegisters(Registers.TemperCtrlP, 3, new int[] { temperCtrlKp, temperCtrlKi, temperCtrlKd }) == SettingResults.OK)
            {
                return WriteRegisters(Registers.ChemiTemperPower, 1, new int[] { temperCtrlMaxCrnt }) == SettingResults.OK;
            }
            return false;
        }
        /// <summary>
        /// called by machines after alf 2.5.
        /// </summary>
        /// <param name="ctrlKp"></param>
        /// <param name="ctrlKi"></param>
        /// <param name="ctrlKd"></param>
        /// <param name="ctrlHeatGain"></param>
        /// <param name="ctrlCoolGain"></param>
        /// <returns></returns>
        public bool SetTemperCtrlParameters(double ctrlKp, double ctrlKi, double ctrlKd, double ctrlHeatGain, double ctrlCoolGain)
        {
            if (WriteRegisters((byte)RegistersV2.TemperCtrlP, 3, new int[] { (int)(ctrlKp * 1000), (int)(ctrlKi * 1000), (int)(ctrlKd * 1000) }) == SettingResults.OK)
            {
                return WriteRegisters((byte)RegistersV2.ChemiTemperHeatGain, 2, new int[] { (int)(ctrlHeatGain * 100), (int)(ctrlCoolGain * 100) }) == SettingResults.OK;
            }
            return false;
        }
        public bool SetChemiTemperCtrlRamp(double ramp)
        {
            int rampVal = (int)(ramp * 1000);
            return WriteRegisters(Registers.ChemiTemperRamp, 1, new int[] { rampVal }) == SettingResults.OK;
        }
        public bool SetChemiTemperCtrlStatus(bool setOn)
        {
            int onVal = setOn ? 1 : 0;
            return WriteRegisters(Registers.ChemiTemperSwitch, 1, new int[] { onVal }) == SettingResults.OK;
        }
        public bool SetChemiTemper(double temper)
        {
            int temperVal = (int)(temper * 1000);
            return WriteRegisters(Registers.ChemiTemper, 1, new int[] { temperVal }) == SettingResults.OK;
        }
        public bool GetDoorStatus()
        {
            return ReadRegisters(Registers.DoorStatus, 1);
        }
        public bool SetDoorStatus(bool setOpen)
        {
            if (!IsFCDoorEnable) return true;

            while (GetDoorStatus() == false) ;
            if(setOpen == DoorIsOpen)
            {
                return true;
            }
            int openVal = setOpen ? 1 : 0;
            int tryCounts = 0;
            while (WriteRegisters(Registers.DoorStatus, 1, new int[] { openVal }) != SettingResults.Delay)
            {
                tryCounts++;
                if (tryCounts > 3)
                {
                    return false;
                }
                Thread.Sleep(5);
            }
            Thread.Sleep(5000);     // wait for Door action
            GetDoorStatus();
            while (DoorIsOpen != setOpen)
            {
                tryCounts++;
                Thread.Sleep(5);
                if (tryCounts > 100)
                {
                    return false;
                }
                GetDoorStatus();
            }
            return true;
        }
        public bool SetLEDIndicator(byte rLED, byte gLED, byte bLED)
        {
            return WriteRegisters((byte)RegistersV2.FrontPanelLED, 1, new int[] { rLED | (gLED << 8) | (bLED << 24) }) == SettingResults.OK;
        }
        public bool SetFluidPreHeatingEnable(bool enable)
        {
            return WriteRegisters((byte)RegistersV2.FluidPreHeatEnable, 1, new int[] { enable ? 1 : 0 }) == SettingResults.OK;
        }
        public bool GetFluidPreHeatingTemp()
        {
            return ReadRegisters((byte)RegistersV2.FluidPreHeatTemp, 1);
        }
        public bool SetFluidPreHeatingTemp(double temper)
        {
            return WriteRegisters((byte)RegistersV2.FluidPreHeatTemp, 1, new int[] { (int)(temper * 100) }) == SettingResults.OK;
        }
        public bool GetChemiTemper()
        {
            return ReadRegisters(Registers.ChemiTemper, 1);
        }
        /// <summary>
        /// Set the intensity percentages of the front panel LEDs.
        /// </summary>
        /// <param name="r">0 - 100</param>
        /// <param name="g">0 - 100</param>
        /// <param name="b">0 - 100</param>
        /// <returns></returns>
        public bool SetFrontPanelLEDs(int r, int g, int b)
        {
            int rPcnt = (int)(r / 100.0 * 255.0);
            int gPcnt = (int)(g / 100.0 * 255.0);
            int bPcnt = (int)(b / 100.0 * 255.0);
            int val = rPcnt | (gPcnt << 8) | (bPcnt << 16);

            return WriteRegisters((byte)RegistersV2.FrontPanelLED, 1, new int[] { val }) == SettingResults.OK;
        }
        public bool GetChemiTemperCtrlGains()
        {
            return ReadRegisters((byte)RegistersV2.ChemiTemperHeatGain, 2);
        }
        /// <summary>
        /// Read Fluid Preheating Temper Ctrl Parameters: Kp, Ki, Kd, and the gain.
        /// </summary>
        /// <returns></returns>
        public bool GetFluidPreHeatCtrlParameters()
        {
            return ReadRegisters((byte)RegistersV2.FluidPreHeatCtrlP, 4);
        }
        public bool SetFluidPreHeatCtrlParameters(double kp, double ki, double kd, double gain)
        {
            return WriteRegisters((byte)RegistersV2.FluidPreHeatCtrlP, 4, new int[]
            {
                (int)(Math.Round(kp*1000,0)), (int)(Math.Round(ki*1000,0)),(int)(Math.Round(kd*1000,0)),(int)(Math.Round(gain*100,0))
            }) == SettingResults.OK;
        }
        public bool GetFanPWMValues()
        {
            return ReadRegisters((byte)RegistersV2.FanSpeed, 1);
        }
        public bool SetFanPWMValues(int eFan, int bFan, int fan)
        {
            int val = (eFan << 16) | (bFan << 8) | fan;
            return WriteRegisters((byte)RegistersV2.FanSpeed, 1, new int[] { val }) == SettingResults.OK;
        }
        #endregion Public Functions

        #region Private Functions
        #endregion Private Functions

        #region Override Functions
        public override string[] GetRegisterNames()
        {
            if (!IsProtocolRev2)
            {
                var names = Enum.GetNames(typeof(Registers));
                return names;
            }
            var newNames = Enum.GetNames(typeof(RegistersV2));
            return newNames;
        }
        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            detectedFrameIndex = 0;
            if (Status != CommStatus.Hearing) { return; }
            _Answer = ALFCommonProtocol.MapResponseFromBytes(ReadBuf, ReadIndex, out detectedFrameIndex);
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
                    for (int i = 0; i < _Answer.RegNums; i++)
                    {
                        if (!IsProtocolRev2)
                        {
                            switch ((Registers)(_Answer.StartReg + i))
                            {
                                case Registers.DeviceType:
                                    DeviceType = Encoding.ASCII.GetString(_Answer.DataField, dataOffset, 4);
                                    break;
                                case Registers.HWVersion:
                                    HWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    if (HWVersion == "2.0.0.2")
                                    {
                                        IsMachineRev2P4 = true;
                                    }
                                    else
                                    {
                                        if (_Answer.DataField[dataOffset] == 2)
                                        {
                                            if (_Answer.DataField[dataOffset + 1] > 0 || _Answer.DataField[dataOffset + 2] > 0 || _Answer.DataField[dataOffset + 3] > 2)
                                            {
                                                IsProtocolRev2 = true;
                                                IsMachineRev2P4 = true;
                                            }
                                        }
                                    }
                                    // "2.0.0.0": alf2.1, no FC door, no Fluid Preheat
                                    // "2.0.0.1": alf2.2, alf2.3, has FC door, no Fluid Preheat
                                    // "2.0.0.2": alf2.4, no FC door, no Fluid Preheat
                                    // "2.0.0.3": alf2.5, has FC door, has Fluid Preheat
                                    if (HWVersion != "2.0.0.0" && HWVersion != "2.0.0.2")
                                    {
                                        IsFCDoorAvailable = true;
                                    }
                                    else
                                    {
                                        IsFCDoorAvailable = false;
                                    }
                                    if (HWVersion != "2.0.0.0" && HWVersion != "2.0.0.1" && HWVersion != "2.0.0.2")
                                    {
                                        IsFluidPreheatAvailable = true;
                                    }
                                    else
                                    {
                                        IsFluidPreheatAvailable = false;
                                    }
                                    break;
                                case Registers.FWVersion:
                                    FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    break;
                                case Registers.TemperCtrlP:
                                    TemperCtrlP = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.TemperCtrlI:
                                    TemperCtrlI = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.TemperCtrlD:
                                    TemperCtrlD = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.PCBTemper:
                                    AmbientTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case Registers.ChemiTemperSwitch:
                                    ChemiTemperCtrlOn = _Answer.DataField[dataOffset] == 0x01;
                                    break;
                                case Registers.ChemiTemperRamp:
                                    ChemiTemperRamp = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case Registers.ChemiTemperPower:
                                    ChemiTemperPower = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.ChemiTemper:
                                    ChemiTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case Registers.HeatSinkTemper:
                                    HeatSinkTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case Registers.OnoffInputs:
                                    FCClampStatus = (_Answer.DataField[dataOffset] & 0x01) == 0x01;
                                    if(HWVersion != "2.0.0.1") { FCClampStatus = !FCClampStatus; }
                                    FCDoorStatus = (_Answer.DataField[dataOffset] & 0x02) == 0x02;
                                    break;
                                case Registers.BarCodeReaderCtrl:
                                    BarCodeReaderReset = (_Answer.DataField[dataOffset] & 0x80) == 0x80;
                                    BarCodeReaderTrigger = (_Answer.DataField[dataOffset] & 0x40) == 0x40;
                                    break;
                                case Registers.DoorStatus:
                                    if (_Answer.DataField[dataOffset] == 0x00) { DoorIsOpen = false; }
                                    if (_Answer.DataField[dataOffset] == 0x01) { DoorIsOpen = true; }
                                    if (_Answer.DataField[dataOffset] == 0x02) { DoorIsOpen = null; }
                                    break;
                                case Registers.ChemiTemperHeatGain:
                                    ChemiTemperHeatGain = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case Registers.ChemiTemperCoolGain:
                                    ChemiTemperCoolGain = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                            }
                        }
                        else
                        {
                            switch ((RegistersV2)(_Answer.StartReg + i))
                            {
                                case RegistersV2.DeviceType:
                                    DeviceType = Encoding.ASCII.GetString(_Answer.DataField, dataOffset, 4);
                                    break;
                                case RegistersV2.HWVersion:
                                    HWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    if (HWVersion == "2.0.0.2")
                                    {
                                        IsMachineRev2P4 = true;
                                        IsProtocolRev2 = false;
                                    }
                                    else
                                    {
                                        if (_Answer.DataField[dataOffset] == 2)
                                        {
                                            if (_Answer.DataField[dataOffset + 1] > 0 || _Answer.DataField[dataOffset + 2] > 0 || _Answer.DataField[dataOffset + 3] > 2)
                                            {
                                                IsProtocolRev2 = true;
                                                IsMachineRev2P4 = false;
                                                break;
                                            }
                                        }
                                        IsProtocolRev2 = false;
                                        IsMachineRev2P4 = false;
                                    }
                                    // "2.0.0.0": alf2.1, no FC door, no Fluid Preheat
                                    // "2.0.0.1": alf2.2, alf2.3, has FC door, no Fluid Preheat
                                    // "2.0.0.2": alf2.4, no FC door, no Fluid Preheat
                                    // "2.0.0.3": alf2.5, has FC door, has Fluid Preheat
                                    if (HWVersion != "2.0.0.0" && HWVersion != "2.0.0.2")
                                    {
                                        IsFCDoorAvailable = true;
                                    }
                                    else
                                    {
                                        IsFCDoorAvailable = false;
                                    }
                                    if (HWVersion != "2.0.0.0" && HWVersion != "2.0.0.1" && HWVersion != "2.0.0.2")
                                    {
                                        IsFluidPreheatAvailable = true;
                                    }
                                    else
                                    {
                                        IsFluidPreheatAvailable = false;
                                    }
                                    break;
                                case RegistersV2.FWVersion:
                                    FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    break;
                                case RegistersV2.TemperCtrlP:
                                    TemperCtrlP = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.TemperCtrlI:
                                    TemperCtrlI = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.TemperCtrlD:
                                    TemperCtrlD = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.ChemiTemperSwitch:
                                    ChemiTemperCtrlOn = _Answer.DataField[dataOffset] == 0x01;
                                    break;
                                case RegistersV2.ChemiTemper:
                                    ChemiTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.HeatSinkTemper:
                                    HeatSinkTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.PCBTemper:
                                    AmbientTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.OnoffInputs:
                                    FCClampStatus = (_Answer.DataField[dataOffset] & 0x01) == 0x01;
                                    if (HWVersion != "2.0.0.1") { FCClampStatus = !FCClampStatus; }
                                    FCDoorStatus = (_Answer.DataField[dataOffset] & 0x02) == 0x02;
                                    break;
                                case RegistersV2.ChemiTemperHeatGain:
                                    ChemiTemperHeatGain = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.ChemiTemperCoolGain:
                                    ChemiTemperCoolGain = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.FrontPanelLED:
                                    FrontPanelRLEDIntensity = _Answer.DataField[dataOffset];
                                    FrontPanelGLEDIntensity = _Answer.DataField[dataOffset + 1];
                                    FrontPanelBLEDIntensity = _Answer.DataField[dataOffset + 2];
                                    break;
                                case RegistersV2.FluidPreHeatEnable:
                                    FluidPreHeatingEnabled = _Answer.DataField[dataOffset] == 0x01;
                                    break;
                                case RegistersV2.FluidPreHeatTemp:
                                    FluidPreHeatingTgtTemper = BitConverter.ToInt16(_Answer.DataField, dataOffset) * 0.01;
                                    FluidPreHeatingCrntTemper = BitConverter.ToInt16(_Answer.DataField, dataOffset + 2) * 0.01;
                                    break;
                                case RegistersV2.FluidPreHeatCtrlP:
                                    FluidPreHeatCtrlKp = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.FluidPreHeatCtrlI:
                                    FluidPreHeatCtrlKi = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.FluidPreHeatCtrlD:
                                    FluidPreHeatCtrlKd = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.001;
                                    break;
                                case RegistersV2.FluidPreHeatGain:
                                    FluidPreHeatGain = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.FanSpeed:
                                    int tmpVal = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    EFanPWMValue = (tmpVal >> 16) & 0x000000ff;
                                    BFanPWMValue = (tmpVal >> 8) & 0x000000ff;
                                    FanPWMValue = (tmpVal) & 0x000000ff;
                                    break;
                            }
                        }
                        dataOffset += 4;
                    }
                }
                Status = CommStatus.Idle;
            }
        }
        #endregion Override Functions
    }
}
