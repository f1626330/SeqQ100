using Hywire.CommLibrary;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.SerialPeripherals
{
    public class FluidController : SerialPeripheralBase
    {
        #region Registers defination
        public enum Registers : byte
        {
            DeviceType = 1,
            HWVersion,
            FWVersion,
            AmbientTemper,
            Pressure,
            FlowRate,
            BufferLevel,
            WasteLevel,
            Bubble,
            PressureAndFlowArray,
            OnoffInputs,
            TemperOfPressureSnsr,
            TemperOfFlowSnsr,
            FlowSnsrSoftReset,
            FlowMediumSelect,
            FirmwareReady,
            RelaunchDevice,
            WasteStatus,
        }
        #endregion Registers defination

        private static Object LockInstanceCreation = new Object();
        static FluidController _Instance = null;
        public static FluidController GetInstance()
        {
            lock (LockInstanceCreation)
            {
                if (_Instance == null)
                {
                    _Instance = new FluidController();
                }
                return _Instance;
            }
        }
        private FluidController():base(AddressTypes.ADDR_Fluid)
        {
        }

        #region Public Properties
        public double AmbientTemper { get; private set; }
        public double Pressure { get; private set; }
        public double FlowRate { get; private set; }
        public uint BufferLevel { get; private set; }
        public uint WasteLevel { get; private set; }
        public bool BubbleDetected { get; private set; }
        public uint Bubble { get; private set; }
        public bool BufferTrayIn { get; private set; }
        public bool SipperDown { get; private set; }
        public bool WasteIn { get; private set; }
        public double PressureOffset { get; private set; }
        public double LastPressureOffset { get; private set; }
        public double[] PressureArray { get; private set; } = new double[10];
        public double[] FlowArray { get; private set; } = new double[10];
        public bool IsBusyInResettingPressure { get; private set; }
        public bool[] BubbleStatusArray { get; private set; } = new bool[10];
        /// <summary>
        /// Mass of the Waste, unit of kg.
        /// </summary>
        public double MassOfWaste { get; private set; }
        #endregion Public Properties


        #region Public Functions
        public bool ReadRegisters(Registers startReg, byte regNums)
        {
            lock (_ThreadLock)
            {
                byte[] sendBytes = ReadRequest((byte)startReg, regNums);
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
                byte[] sendBytes = WriteRequest((byte)startReg, regNums, values);
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
        public bool ResetBubbleCounts()
        {
            return WriteRegisters(Registers.Bubble, 1, new int[] { 0 }) == SettingResults.OK;
        }
        public bool QueryPressureAndFlowArray()
        {
            return ReadRegisters(Registers.PressureAndFlowArray, 1);
        }
        /// <summary>
        /// take average of pressures within 4 seconds as pressure zero
        /// </summary>
        public void ResetPressure()
        {
            IsBusyInResettingPressure = true;
            LastPressureOffset = PressureOffset;
            PressureOffset = 0;
            double sum = 0;
            for (int i = 0; i < 1; i++)
            {
                QueryPressureAndFlowArray();
                sum += PressureArray.Sum();
                //Thread.Sleep(1000);
            }
            PressureOffset = sum / 10;
            IsBusyInResettingPressure = false;
        }

        public bool ReadMassOfWaste()
        {
            return ReadRegisters(Registers.WasteStatus, 1);
        }
        #endregion Public Functions

        #region Private Functions
        #endregion Private Functions

        #region Override Functions
        public override string[] GetRegisterNames()
        {
            var names = Enum.GetNames(typeof(Registers));
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
                                case Registers.Pressure:
                                    Pressure = BitConverter.ToInt32(_Answer.DataField, dataOffset) * 1000.0 / Math.Pow(2, 22) - PressureOffset;
                                    break;
                                case Registers.FlowRate:
                                    FlowRate = BitConverter.ToInt16(_Answer.DataField, dataOffset) / 500.0;
                                    break;
                                case Registers.BufferLevel:
                                    BufferLevel = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.WasteLevel:
                                    WasteLevel = BitConverter.ToUInt32(_Answer.DataField, dataOffset);
                                    break;
                                case Registers.Bubble:
                                    for (int loop = 0; loop < 10; loop++)
                                    {
                                        int bubbleAccValRawData = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        dataOffset += 4;
                                        Bubble = (uint)bubbleAccValRawData;
                                    }
                                    for (int loop = 0; loop < 10; loop++)
                                    {
                                        int bubbleStatusRawData = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        BubbleStatusArray[loop] = bubbleStatusRawData == 1 ? true : false;
                                        dataOffset += 4;
                                    }
                                    //BubbleDetected = (_Answer.DataField[dataOffset + 3] & 0x80) == 0x80;
                                    //Bubble = BitConverter.ToUInt32(_Answer.DataField, dataOffset) & 0x7fffffff;
                                    dataOffset -= 4;
                                    i += 19;
                                    break;
                                case Registers.PressureAndFlowArray:
                                    // there are 10 pressures and 10 flows in the single frame
                                    for (int loop = 0; loop < 10; loop++)
                                    {
                                        int pressureRawData = BitConverter.ToInt32(_Answer.DataField, dataOffset);
                                        PressureArray[loop] = pressureRawData * 1000.0 / Math.Pow(2, 22) - PressureOffset;
                                        dataOffset += 4;
                                    }
                                    for (int loop = 0; loop < 10; loop++)
                                    {
                                        int flowRawData = BitConverter.ToInt16(_Answer.DataField, dataOffset);
                                        FlowArray[loop] = flowRawData / 500.0;
                                        dataOffset += 4;
                                    }
                                    i += 20;
                                    break;
                                case Registers.OnoffInputs:
                                    SipperDown = (_Answer.DataField[dataOffset] & 0x01) == 0x01;
                                    BufferTrayIn = (_Answer.DataField[dataOffset] & 0x02) == 0x02;
                                    WasteIn = (_Answer.DataField[dataOffset] & 0x04) == 0x04;
                                    BubbleDetected = (_Answer.DataField[dataOffset + 1] & 0x01) == 0x01;
                                    break;
                                case Registers.WasteStatus:
                                    MassOfWaste = BitConverter.ToInt16(_Answer.DataField, dataOffset) * 0.01;
                                    break;
                            }
                            dataOffset += 4;
                            regLoop++;
                        }
                    }
                    Status = CommStatus.Idle;
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        #endregion Override Functions
    }
}
