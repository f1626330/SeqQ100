using Hywire.CommLibrary;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.SerialPeripherals
{
    public class Chiller : SerialPeripheralBase
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
            ChillerMaxPower,
            ChillerTemper,
            HeatSinkTemper,
            PCBTemper,
            OnoffInputs,
            RFIDCtrl,
            FirmwareReady,
            RelaunchDevice,

            // new registers introduced in alf2.4
            ChillerTgtTemper,
            ChillerMotorCtrl,
            IndicatorLED,
            FluidHeatingEnable,
            FluidHeatingTemper,
            ChillerMotorRelMove,
            ChillerMotorAbsMove,
        }
        public enum RegistersV2 : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            TemperCtrlP,
            TemperCtrlI,
            TemperCtrlD,
            ChillerMaxPower,
            ChillerTemper,
            HeatSinkTemper,
            PCBTemper,
            OnoffInputs,
            RFIDCtrl,
            FirmwareReady,
            RelaunchDevice,
            ChillerTgtTemper,
            ChillerMotorCtrl,
            ChillerMotorRelMove,
            ChillerMotorAbsMove,
            ChillerMotorSpeed,
            CoolingPumpVoltage,
            CoolingPumpSpeed,
            ChillerDoorStatus,
            CoolingLiquidLevel,
        }
        #endregion Registers defination

        #region Chiller Motor Status
        public enum CartridgeStatusTypes
        {
            Default = 0xff,
            Unloaded = 0x00,
            Loading = 0x01,
            Unloading = 0x02,
            Loaded = 0x03,
            UnloadError = 0x04,
            LoadError = 0x05,
            MovingUp = 0x06,
            MovingDown = 0x07,
            MovingUpOk = 0x08,
            MovingDownOk = 0x09,
            MovingUpError = 0x0a,
            MovingDownError = 0x0b,
        }
        #endregion Chiller Motor Status

        #region Public Events
        public delegate void MotorStatusChangedHandle(CartridgeStatusTypes status);
        public event MotorStatusChangedHandle OnMotorStatusChanged;
        #endregion Public Events

        #region Private Fields
        private System.Timers.Timer _MotorStatusQueryTimer;
        private CartridgeStatusTypes _MotorStatus;
        private double _MotorPos;
        ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Chiller");
        private static Object LockInstanceCreation = new Object();
        #endregion Private Fields
        static Chiller _Controller = null;
        public static Chiller GetInstance()
        {
            if (_Controller == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_Controller == null)
                    {
                        _Controller = new Chiller();
                    }

                }
            }
            return _Controller;
        }

        private Chiller():base(AddressTypes.ADDR_Chiller)
        {
            _MotorStatusQueryTimer = new System.Timers.Timer(1000);
            _MotorStatusQueryTimer.AutoReset = true;
            _MotorStatusQueryTimer.Elapsed += _MotorStatusQueryTimer_Elapsed;
        }

        private void _MotorStatusQueryTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            GetChillerMotorStatus();
            GetChillerMotorPos();
            if (CartridgeMotorStatus != CartridgeStatusTypes.Loading &&
                CartridgeMotorStatus != CartridgeStatusTypes.Unloading &&
                CartridgeMotorStatus != CartridgeStatusTypes.MovingDown &&
                CartridgeMotorStatus != CartridgeStatusTypes.MovingUp)
            {
                _MotorStatusQueryTimer.Enabled = false;
            }
        }

        #region Public Properties
        public double ChillerTemper { get; private set; } //< the current measured chiller temperature. Update by reading the ChillerTemper register. Units = [C]
        public double ChillerTargetTemperature { get; private set; } //< the chiller temperature setpoint. Updated by reading the ChillerTgtTemper register. Units = [C]
        public double HeatSinkTemper { get; private set; }
        public double PCBTemper { get; private set; }
        public int TemperCtrlP { get; private set; }
        public int TemperCtrlI { get; private set; }
        public int TemperCtrlD { get; private set; }
        public int ChillerMaxPower { get; private set; }
        public bool CartridgePresent { get; private set; }
        public bool CartridgeDoor { get; private set; }
        public bool RFIDEnable { get; private set; }
        public bool IsFluidHeatingEnabled { get; private set; }
        public double FluidHeatingTemper { get; private set; }
        public CartridgeStatusTypes CartridgeMotorStatus
        {
            get { return _MotorStatus; }
            private set
            {
                if (_MotorStatus != value)
                {
                    _MotorStatus = value;
                    OnMotorStatusChanged?.Invoke(_MotorStatus);
                }
            }
        }
        public double CartridgeMotorPos
        {
            get { return _MotorPos; }
            set
            {
                if (_MotorPos != value)
                {
                    _MotorPos = value;
                    OnMotorStatusChanged?.Invoke(_MotorStatus);
                }
            }
        }
        public bool IsProtocolRev2 { get; set; }
        public int CartridgeMotorSpeed { get; private set; }
        public int CoolingPumpVoltage { get; private set; }
        public int CoolingPumpSpeed { get; private set; }
        public bool CoolingLiquidIsFull { get; private set; }
        public bool CartridgeDoorLocked { get; private set; }
        #endregion Public Properties


        #region Public Functions
        /// <summary>
        /// Checks if the current position of the Reagent Sipper Motor is 
        /// equal to the Reagent Cartridge Position set in the calibration file.
        /// </summary>
        /// <returns>True if the sippers are all the way down, false otherwise</returns>
        public bool CheckCartridgeSippersReagentPos()
        {
            if (Math.Abs(CartridgeMotorPos - SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos) <= 0.03)
            {
                return true;
            }
            else { return false; }
            //return CartridgeMotorPos.Equals(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos);
        }
        /// <summary>
        /// Checks if the current position of the Reagent Sipper Motor is 
        /// equal to the Wash Cartridge Position set in the calibration file.
        /// </summary>
        /// <returns>True if the sippers are all the way down, false otherwise</returns>
        public bool CheckCartridgeSippersWashPos()
        {
            if (Math.Abs(CartridgeMotorPos - SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos) <= 0.03)
            {
                return true;
            }
            else { return false; }
            //return CartridgeMotorPos.Equals(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos);
        }
        public bool ReadRegisters(Registers startReg, byte regNums)
        {
            lock (_ThreadLock)
            {
                byte[] sendBytes = null;
                if (!IsProtocolRev2)
                {
                    sendBytes = ReadRequest((byte)startReg, regNums);
                }
                else
                {
                    if (startReg <= Registers.ChillerMotorCtrl)
                    {
                        sendBytes = ReadRequest((byte)startReg, regNums);
                    }
                    else
                    {
                        switch (startReg)
                        {
                            case Registers.ChillerMotorRelMove:
                                sendBytes = ReadRequest((byte)RegistersV2.ChillerMotorRelMove, regNums);
                                break;
                            case Registers.ChillerMotorAbsMove:
                                sendBytes = ReadRequest((byte)RegistersV2.ChillerMotorAbsMove, regNums);
                                break;
                            default:
                                return false;
                        }
                    }
                }
                if (SendBytes(sendBytes))
                {
                    if (!_Answer.IsCrcCorrect) { return false; }
                    return true;
                }
                else { return false; }
            }
        }
        public SettingResults WriteRegisters(Registers startReg, byte regNums, int[] values)
        {
            lock (_ThreadLock)
            {
                byte[] sendBytes;
                if (!IsProtocolRev2)
                {
                    sendBytes = WriteRequest((byte)startReg, regNums, values);
                }
                else
                {
                    if (startReg <= Registers.ChillerMotorCtrl)
                    {
                        sendBytes = WriteRequest((byte)startReg, regNums, values);
                    }
                    else
                    {
                        switch (startReg)
                        {
                            case Registers.ChillerMotorRelMove:
                                sendBytes = WriteRequest((byte)RegistersV2.ChillerMotorRelMove, regNums, values);
                                break;
                            case Registers.ChillerMotorAbsMove:
                                sendBytes = WriteRequest((byte)RegistersV2.ChillerMotorAbsMove, regNums, values);
                                break;
                            default:
                                return SettingResults.UnknownRegister;
                        }
                    }
                }

                if (SendBytes(sendBytes))
                {
                    if (!_Answer.IsCrcCorrect) { return SettingResults.CrcCheckFailed; }
                    return (SettingResults)_Answer.DataField[0];
                }
                else
                {
                    return SettingResults.SettingTimeout;
                }
            }
        }

        // why doesn't this set the ChillerTgtTemper register?
        /// <summary>
        /// Writes a value to the ChillerTemper register. 
        /// The ChillerTemper and ChillerTgtTemper registers behave strangely:
        /// To read the temperature setpoint, you must read ChillerTgtTemper. 
        /// Reading ChillerTemper will report the current measured temperature.
        /// However, ChillerTgTemper cannot be used to set the setpoint.
        /// (The setpoint is set by writing to the ChillerTemper register)
        /// </summary>
        /// <param name="chillerTemper">The temperature setpoint to write to the chiller (units = [C])</param>
        /// <returns>True if the temperature setpoint was sucussfully written</returns>
        public bool SetCoolerTargetTemperature(double chillerTemper)
        {
            int temperVal = (int)(chillerTemper * 100.0);
            SettingResults result = WriteRegisters(Registers.ChillerTemper, 1, new int[] { temperVal });
            string message = $"Writing temperature setpoint to ChillerTemper register. (temp: {chillerTemper} value:{temperVal})... Result: {result}";
            Logger.Log(message, SeqLogFlagEnum.DEBUG);
            // after setting, read back the temperature to update the UI with the correct value.
            // (otherwise the value displayed in the UI will be overwritten)
            GetCoolerTargetTemperature();
            return result == SettingResults.OK;
        }

        /// <summary>
        /// Requets an updated value from the ChillerTgtTemper register
        /// </summary>
        /// <returns></returns>
        public bool GetCoolerTargetTemperature()
        {
            Debug.WriteLine("");
            bool ok = ReadRegisters(Registers.ChillerTgtTemper, 1);
            string message = $"Reading chiller target temper register... Result: {ok}";
            Logger.Log(message, SeqLogFlagEnum.DEBUG);
            return ok;
        }
        public bool GetTemperCtrlParameters()
        {
            return ReadRegisters(Registers.TemperCtrlP, 4);
        }
        public bool SetTemperCtrlParameters(int temperCtrlKp, int temperCtrlKi, int temperCtrlKd, int temperCtrlMaxCrnt)
        {
            return WriteRegisters(Registers.TemperCtrlP, 4, new int[] { temperCtrlKp, temperCtrlKi, temperCtrlKd, temperCtrlMaxCrnt }) == SettingResults.OK;
        }

        /// <summary>
        /// Control Chiller motor to load/unload the cartridge. this is introduced in ALF2.4 machine
        /// </summary>
        /// <param name="load">Move Cartridge down if true, otherwise move cartridge up to unload.</param>
        /// <returns></returns>
        public bool ChillerMotorControl(bool load)
        {
            int loadVal = load ? 1 : 0;
            var result = WriteRegisters(Registers.ChillerMotorCtrl, 1, new int[] { loadVal });
            if(result == SettingResults.OK || result == SettingResults.Delay)
            {
                _MotorStatusQueryTimer.Start();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Move the Cartridge up/down by given distances.
        /// </summary>
        /// <param name="isUpward">Set true to move the Cartridge up, otherwise move the Cartridge down.</param>
        /// <param name="distanceInMM">Distance in MilliMeter.</param>
        /// <returns></returns>
        public bool SetChillerMotorRelMove(bool isUpward, double distanceInMM)
        {
            int tempVal = isUpward ? 0x00000000 : int.MinValue;
            tempVal |= (int)(distanceInMM * Common.SettingsManager.ConfigSettings.MotionFactors[Common.MotionTypes.Cartridge]);
            var result = WriteRegisters(Registers.ChillerMotorRelMove, 1, new int[] { tempVal });
            if(result == SettingResults.OK || result == SettingResults.Delay)
            {
                _MotorStatusQueryTimer.Start();
                return true;
            }
            return false;
        }
        public bool SetChillerMotorAbsMove(double posInMM)
        {
            if (posInMM < Common.SettingsManager.ConfigSettings.MotionSettings[Common.MotionTypes.Cartridge].MotionRange.LimitLow)
            {
                return false;
            }
            if(posInMM > Common.SettingsManager.ConfigSettings.MotionSettings[Common.MotionTypes.Cartridge].MotionRange.LimitHigh)
            {
                return false;
            }
            int tempVal = (int)(posInMM * Common.SettingsManager.ConfigSettings.MotionFactors[Common.MotionTypes.Cartridge]);
            var result = WriteRegisters(Registers.ChillerMotorAbsMove, 1, new int[] { tempVal });
            if(result== SettingResults.OK || result== SettingResults.Delay)
            {
                _MotorStatusQueryTimer.Start();
                return true;
            }
            return false;
        }
        /// <summary>
        /// Set Chiller Motor Speed, range from 1 to 10.
        /// 1 = 0.4 rev/second = 4 rev/second (according to Paul 2022 01 24)
        /// </summary>
        /// <param name="speedLevel">1 - 10</param>
        /// <returns></returns>
        public bool SetChillerMotorSpeed(int speedLevel)
        {
            if (!IsProtocolRev2)
            {
                return false;
            }
            if (speedLevel < 1 || speedLevel > 10)
            {
                return false;
            }
            return WriteRegisters((byte)RegistersV2.ChillerMotorSpeed, 1, new int[] { speedLevel }) == SettingResults.OK;
        }
        public bool GetChillerMotorStatus()
        {
            return ReadRegisters(Registers.ChillerMotorCtrl, 1);
        }
        public bool GetChillerMotorPos()
        {
            return ReadRegisters(Registers.ChillerMotorAbsMove, 1);
        }

        public bool SetFluidHeatingEnable(bool enable)
        {
            int enableVal = enable ? 1 : 0;
            return WriteRegisters(Registers.FluidHeatingEnable, 1, new int[] { enableVal }) == SettingResults.OK;
        }
        public bool GetFluidHeatingEnable()
        {
            return ReadRegisters(Registers.FluidHeatingEnable, 1);
        }
        public bool SetFluidHeatingTemper(double temper)
        {
            int temperVal = (int)(temper * 100);
            return WriteRegisters(Registers.FluidHeatingTemper, 1, new int[] { temperVal }) == SettingResults.OK;
        }
        public bool GetFluidHeatingTemper()
        {
            return ReadRegisters(Registers.FluidHeatingTemper, 1);
        }
        /// <summary>
        /// Set Cooling Pump's Control Voltage, ranges from 1 to 10
        /// </summary>
        /// <param name="voltLevel"></param>
        /// <returns></returns>
        public bool SetCoolingPumpVoltage(int voltLevel)
        {
            if (!IsProtocolRev2)
            {
                return false;
            }
            if (voltLevel < 1 || voltLevel > 10)
            {
                return false;
            }
            return WriteRegisters((byte)RegistersV2.CoolingPumpVoltage, 1, new int[] { voltLevel }) == SettingResults.OK;
        }
        public bool GetCoolingPumpSpeed()
        {
            if (!IsProtocolRev2)
            {
                return false;
            }
            return ReadRegisters((byte)RegistersV2.CoolingPumpSpeed, 1);
        }
        /// <summary>
        /// Lock/Unlock the door of the chiller box
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public bool ChillerDoorControl(bool locked)
        {
            if (!IsProtocolRev2)
            {
                return false;
            }
            return WriteRegisters((byte)RegistersV2.ChillerDoorStatus, 1, new int[] { (locked ? 1 : 0) }) == SettingResults.OK;
        }
        public bool GetCoolingLiquidLevel()
        {
            if (!IsProtocolRev2)
            {
                return false;
            }
            return ReadRegisters((byte)RegistersV2.CoolingLiquidLevel, 1);
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
            else
            {
                var names = Enum.GetNames(typeof(RegistersV2));
                return names;
            }
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
                                    break;
                                case Registers.FWVersion:
                                    FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    break;
                                case Registers.ChillerTemper:
                                    ChillerTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case Registers.HeatSinkTemper:
                                    HeatSinkTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case Registers.PCBTemper:
                                    PCBTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
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
                                case Registers.ChillerMaxPower:
                                    ChillerMaxPower = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.OnoffInputs:
                                    CartridgePresent = (_Answer.DataField[dataOffset] & 0x01) == 0x01;
                                    CartridgeDoor = (_Answer.DataField[dataOffset] & 0x02) == 0x02;
                                    break;
                                case Registers.RFIDCtrl:
                                    RFIDEnable = (_Answer.DataField[dataOffset] & 0x80) == 0x80;
                                    break;
                                case Registers.ChillerMotorCtrl:
                                    CartridgeMotorStatus = (CartridgeStatusTypes)_Answer.DataField[dataOffset];
                                    break;
                                case Registers.FluidHeatingEnable:
                                    IsFluidHeatingEnabled = (_Answer.DataField[dataOffset] == 0x01);
                                    break;
                                case Registers.FluidHeatingTemper:
                                    FluidHeatingTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) / 100.0;
                                    break;
                                case Registers.ChillerMotorAbsMove:
                                    var tempVal = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    CartridgeMotorPos = Math.Round(tempVal / Common.SettingsManager.ConfigSettings.MotionFactors[Common.MotionTypes.Cartridge], 2);
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
                                    break;
                                case RegistersV2.FWVersion:
                                    FWVersion = string.Format("{0}.{1}.{2}.{3}", _Answer.DataField[dataOffset], _Answer.DataField[dataOffset + 1],
                                                                                _Answer.DataField[dataOffset + 2], _Answer.DataField[dataOffset + 3]);
                                    break;
                                case RegistersV2.ChillerTemper:
                                    //Debug.WriteLine($"Chiller current temperature: {BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01}");
                                    ChillerTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.ChillerTgtTemper:
                                    ChillerTargetTemperature = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.1; // NOTE: target temp is returned x10 instead of x100 ??
                                    break;
                                case RegistersV2.HeatSinkTemper:
                                    HeatSinkTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.PCBTemper:
                                    PCBTemper = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                                case RegistersV2.TemperCtrlP:
                                    TemperCtrlP = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case RegistersV2.TemperCtrlI:
                                    TemperCtrlI = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case RegistersV2.TemperCtrlD:
                                    TemperCtrlD = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case RegistersV2.ChillerMaxPower:
                                    ChillerMaxPower = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case RegistersV2.OnoffInputs:
                                    CartridgePresent = (_Answer.DataField[dataOffset] & 0x01) == 0x01;
                                    CartridgeDoor = (_Answer.DataField[dataOffset] & 0x02) == 0x02;
                                    break;
                                case RegistersV2.RFIDCtrl:
                                    RFIDEnable = (_Answer.DataField[dataOffset] & 0x80) == 0x80;
                                    break;
                                case RegistersV2.ChillerMotorCtrl:
                                    CartridgeMotorStatus = (CartridgeStatusTypes)_Answer.DataField[dataOffset];
                                    break;
                                case RegistersV2.ChillerMotorAbsMove:
                                    var tempVal = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    CartridgeMotorPos = Math.Round(tempVal / Common.SettingsManager.ConfigSettings.MotionFactors[Common.MotionTypes.Cartridge], 2);
                                    break;
                                case RegistersV2.CoolingPumpSpeed:
                                    CoolingPumpSpeed = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                    break;
                                case RegistersV2.ChillerDoorStatus:
                                    CartridgeDoorLocked = _Answer.DataField[dataOffset] == 1;
                                    break;
                                case RegistersV2.CoolingLiquidLevel:
                                    CoolingLiquidIsFull = _Answer.DataField[dataOffset] == 1;
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
