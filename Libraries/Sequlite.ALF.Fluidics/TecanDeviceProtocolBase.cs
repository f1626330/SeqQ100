using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.Fluidics
{
    internal class CommandBlock
    {
        public byte STX { get; } = 0x2F;
        public TechDeviceAddresses DeviceAddress { get; set; }
        public string DataBlock { get; set; }
        public byte CR { get; } = 0x0D;
        public byte[] GetBytes()
        {
            List<byte> list = new List<byte>();
            list.Add(STX);
            list.Add((byte)DeviceAddress);
            list.AddRange(Encoding.ASCII.GetBytes(DataBlock));
            list.Add(CR);
            return list.ToArray();
        }
    }
    internal class AnswerBlock
    {
        public byte STX { get; } = 0x2F;
        public TechDeviceAddresses MasterAddress { get; } = TechDeviceAddresses.Master;
        public DeviceStatus StatusAndErrCode { get; set; }
        public string DataBlock { get; set; }
        public byte ETX { get; } = 0x03;
    }
    internal class DeviceStatus
    {
        public bool IsReady { get; private set; }
        public TechDeviceErrorCodes ErrorCode { get; private set; }
        public byte DataByte
        {
            get
            {
                return (byte)(0x40 | (IsReady ? 0x20 : 0x00) | (byte)ErrorCode);
            }
        }
        public DeviceStatus(byte data)
        {
            IsReady = (data & 0x20) == 0x20 ? true : false;
            ErrorCode = (TechDeviceErrorCodes)(data & 0x0f);
        }
    }
    public enum TechDeviceErrorCodes : byte
    {
        ErrorFree = 0,
        ErrInitial = 1,
        InvalidCmd = 2,
        InvalidOperand = 3,
        InvalidCmdSqc = 4,
        E2promFailure = 6,
        NotInitialized = 7,
        PlungerOvld = 9,
        ValveOvld = 10,
        PlgMoveNotAllowed = 11,
        ValveHomeMissing = 12,
        ADCFailure = 14,
        CommandOvfl = 15,
    }
    public enum TechDeviceAddresses : byte
    {
        Master = 0x30,
        Device0 = 0x31,
        Device1 = 0x32,
        Device2 = 0x33,
        Device3 = 0x34,
        Device4 = 0x35,
        Device5 = 0x36,
        Device6 = 0x37,
        Device7 = 0x38,
        Device8 = 0x39,
        Device9 = 0x3A,
        Device10 = 0x3B,
        Device11 = 0x3C,
        Device12 = 0x3D,
        Device13 = 0x3E,
        Device14 = 0x3F,
    }
    public enum TecanBaudrateOptions
    {
        Rate_9600 = 9600,
        Rate_38400 = 38400,
    }


}
