using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.MotionFwUpgrader
{
    public enum EPCSIdTypes
    {
        None = 0x00,
        EPCS1 = 0x10,
        EPCS4 = 0x12,
        EPCS16 = 0x14,
        EPCS64 = 0x16,
    }
    static class UpgradeProtocol
    {
        #region Enumerations
        enum CommandTypes : byte
        {
            Read = 0x01,
            Write = 0x02,
        }
        enum Registers : byte
        {
            EPCS_Id = 0xf1,
            EPCS_Sector = 0xf3,
            EPCS_Content = 0xf4,
            Reconfig_FPGA = 0xf5,
        }

        #endregion Enumerations

        #region Public Properties
        public static EPCSIdTypes EPCSId { get; private set; }
        public static byte ReconfigTrigger { get; private set; }
        public static string UpgradeInfo { get; private set; }
        public static EPCSFlashContent FlashContent { get; private set; }
        public static string UserFWVersion { get; private set; }
        #endregion Public Properties


        #region Public Functions
        public static byte[] UpgraderReadEpcsId()
        {
            return new byte[] { 0x6a, (byte)CommandTypes.Read, (byte)Registers.EPCS_Id, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6e };
        }
        public static byte[] UpgraderReadEpcsContent(int startAddr, ushort length)
        {
            var addrBytes = BitConverter.GetBytes(startAddr);
            var lenBytes = BitConverter.GetBytes(length);
            List<byte> cmd = new List<byte>();
            cmd.Add(0x6a);
            cmd.Add((byte)CommandTypes.Read);
            cmd.Add((byte)Registers.EPCS_Content);
            cmd.AddRange(lenBytes);
            cmd.AddRange(addrBytes);
            cmd.Add(0x6e);
            return cmd.ToArray();
        }
        public static byte[] UpgraderWriteEpcsContent(int startAddr, ushort length, byte[] content)
        {
            var addrBytes = BitConverter.GetBytes(startAddr);
            var lenBytes = BitConverter.GetBytes((ushort)(length + 4));
            List<byte> cmd = new List<byte>();
            cmd.Add(0x6a);
            cmd.Add((byte)CommandTypes.Write);
            cmd.Add((byte)Registers.EPCS_Content);
            cmd.AddRange(lenBytes);
            cmd.AddRange(addrBytes);
            byte[] dat = new byte[length];
            Buffer.BlockCopy(content, 0, dat, 0, length);
            cmd.AddRange(dat);
            cmd.Add(0x6e);
            return cmd.ToArray();
        }
        public static byte[] UpgraderEraseSector(int addr)
        {
            List<byte> cmd = new List<byte>();
            cmd.Add(0x6a);
            cmd.Add((byte)CommandTypes.Write);
            cmd.Add((byte)Registers.EPCS_Sector);
            cmd.AddRange(new byte[] { 0x04, 0x00 });
            cmd.AddRange(BitConverter.GetBytes(addr));
            cmd.Add(0x6e);
            return cmd.ToArray();
        }
        public static byte[] UpgraderReconfigFPGA(bool toUserMode)
        {
            return new byte[] { 0x6a, (byte)CommandTypes.Write, (byte)Registers.Reconfig_FPGA, 0x04, 0x00, (byte)(toUserMode ? 0x01 : 0x00), 0x00, 0x00, 0x00, 0x6e };
        }
        public static byte[] UserImageSwitchToUpgrader()
        {
            return new byte[] { 0x55, 0x02, 0xf0, 0x02, 0x01, 0x01, 0x00, 0x00, 0x00, 0xaa };
        }
        public static bool ResponseDecoding(byte[] rxBuf, int rxLength)
        {
            if (rxLength < 8) { return false; }
            if (rxLength == 10 && rxBuf[0] == 0x56 && rxBuf[9] == 0xab)     // user image response
            {
                UserFWVersion = string.Format("V{0}.{1}.{2}.{3}", rxBuf[8], rxBuf[7], rxBuf[6], rxBuf[5]);
                return true;
            }
            if (rxBuf[0] != 0x6b || rxBuf[rxLength - 1] != 0x6f) { return false; }

            CommandTypes cmd = (CommandTypes)(rxBuf[1] & 0x0f);
            Registers reg = (Registers)rxBuf[2];

            if (cmd == CommandTypes.Read)
            {
                switch (reg)
                {
                    case Registers.EPCS_Id:
                        EPCSId = (EPCSIdTypes)rxBuf[5];
                        ReconfigTrigger = rxBuf[6];
                        return true;
                    case Registers.EPCS_Content:
                        ushort len = BitConverter.ToUInt16(rxBuf, 3);
                        int addr = BitConverter.ToInt32(rxBuf, 5);
                        FlashContent = new EPCSFlashContent()
                        {
                            StartAddress = addr,
                            Length = len,
                        };
                        FlashContent.Content = new byte[len];
                        Buffer.BlockCopy(rxBuf, 9, FlashContent.Content, 0, len);
                        return true;
                    default:
                        return false;
                }
            }
            else if (cmd == CommandTypes.Write)
            {
                switch (reg)
                {
                    case Registers.EPCS_Sector:
                        break;
                    case Registers.Reconfig_FPGA:
                        break;
                    case Registers.EPCS_Content:
                        break;
                    default:
                        return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion Public Functions
    }

    public class EPCSFlashContent
    {
        public int StartAddress { get; set; }
        public ushort Length { get; set; }
        public byte[] Content { get; set; }
    }
}
