using Hywire.CommLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.SerialPeripherals
{
    public enum AddressTypes : byte
    {
        ADDR_MB = 0x01,
        ADDR_Fluid = 0x02,
        ADDR_LED = 0x03,
        ADDR_Chiller = 0x04,
    }
    public enum FunctionTypes : byte
    {
        Read = 0x03,
        Write = 0x10,
    }
    public enum SettingResults : byte
    {
        OK = 0x00,
        Delay = 0x01,
        Error = 0x02,
        SettingTimeout = 0xff,
        CrcCheckFailed = 0xfe,
        UnknownRegister = 0x03,
    }
    public class ALFCommonProtocol
    {
        public byte ProtocolHead { get; } = 0x55;
        public AddressTypes Address { get; set; }
        public FunctionTypes Function { get; set; }
        public byte StartReg { get; set; }
        public byte RegNums { get; set; }
        public byte[] DataField { get; set; }
        public ushort CRC16Code { get; set; }
        public byte ProtocolEnd { get; } = 0xaa;

        public byte[] GetBytes()
        {
            List<byte> result = new List<byte>();
            result.Add(ProtocolHead);
            result.Add((byte)Address);
            result.Add((byte)Function);
            result.Add(StartReg);
            result.Add(RegNums);
            if (DataField != null)
            {
                result.AddRange(DataField);
            }
            result.AddRange(BitConverter.GetBytes(CRC16Calculator.CRC_Cal(result.ToArray(), 0, result.Count)));
            result.Add(ProtocolEnd);
            return result.ToArray();
        }

        public bool IsCrcCorrect
        {
            get
            {
                byte[] data = GetBytes();
                if(CRC16Code == CRC16Calculator.CRC_Cal(data, 0, data.Length - 3))
                {
                    return true;
                }
                else { return false; }
            }
        }

        public static ALFCommonProtocol MapResponseFromBytes(byte[] buf, int length, out int frameIndex)
        {
            frameIndex = 0;
            if(length < 12) { return null; }
            for(int offset = 0; offset <= length - 12; offset++)
            {
                if (buf[offset] != 0x55) { continue; }
                byte addr = buf[offset + 1];
                byte cmd = buf[offset + 2];
                byte reg = buf[offset + 3];
                byte regNums = buf[offset + 4];
                int frameLen = 12;
                if (cmd == (byte)FunctionTypes.Read)
                {
                    frameLen = 8 + 4 * regNums;
                }
                if (offset + frameLen > length) { return null; }
                if (buf[offset + frameLen - 1] != 0xaa) { return null; }
                ushort crc = BitConverter.ToUInt16(buf, offset + frameLen - 3);
                ALFCommonProtocol result = new ALFCommonProtocol();
                result.Address = (AddressTypes)addr;
                result.Function = (FunctionTypes)cmd;
                result.StartReg = reg;
                result.RegNums = regNums;
                result.DataField = new byte[frameLen - 8];
                Array.Copy(buf, offset + 5, result.DataField, 0, result.DataField.Length);
                result.CRC16Code = crc;

                frameIndex = offset + frameLen;
                return result;
            }
            return null;
        }

    }
}
