using System;
using System.Collections.Generic;

namespace Hywire.MotionControl
{
    [Flags]
    public enum MotorTypes : byte
    {
        Motor_X = 1,
        Motor_Y = 2,
        Motor_Z = 4,
        Motor_W = 8,
        CFG = 0xf0,
    }
    public enum CmdTypes:byte
    {
        Read = 1,
        Write = 2,
    }
    public enum MotionRegisters
    {
        Version = 1,

        StartSpeed = 1,
        TopSpeed = 2,
        Acceleration = 3,
        Deceleration = 4,
        DccPositionL = 5,
        TgtPositionL = 6,
        DccPositionR = 7,
        TgtPositionR = 8,
        DelayTime = 9,
        Repeats = 10,
        Start = 11,
        Home = 12,
        Enable = 13,
        Polar = 14,
        CrntPos = 15,
        CrntStates = 16,
        EncoderPos = 17,
        DriveCurrent = 18,
        DriveMode = 19,
    }
    public enum MotionDriveCurrent
    {
        Percent100 = 0,
        Percent75,
        Percent50,
        Percent20,
    }
    public enum MotionDriveMode
    {
        Divide1,
        Divide2,
        Divide16,
        Divide8,
    }

    internal static class Protocol
    {
        public static byte[] GetControllerVersion()
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Read;
            sendFrame.MotionType = MotorTypes.CFG;
            sendFrame.Register = MotionRegisters.Version;
            sendFrame.RegNums = 1;
            sendFrame.Data = null;
            return sendFrame.MapToBytes();
        }

        /// <summary>
        /// get the current positions, states and encoder positions of the specified motors
        /// </summary>
        /// <param name="motors"></param>
        /// <returns></returns>
        public static byte[] GetMotionInfo(MotorTypes motions)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Read;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.CrntPos;
            sendFrame.RegNums = 3;
            sendFrame.Data = null;
            return sendFrame.MapToBytes();
        }
        public static byte[] SetMotionStart(MotorTypes motions, bool[] start)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.Start;
            sendFrame.RegNums = 1;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums];
            for (int i = 0; i < motionNums; i++)
            {
                sendFrame.Data[i] = start[i] ? 1 : 0;
            }
            return sendFrame.MapToBytes();
        }
        public static byte[] SetMotionEnable(MotorTypes motions, bool[] enable)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.Enable;
            sendFrame.RegNums = 1;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums];
            for (int i = 0; i < motionNums; i++)
            {
                sendFrame.Data[i] = enable[i] ? 1 : 0;
            }
            return sendFrame.MapToBytes();
        }
        public static byte[] SetMotionHome(MotorTypes motions)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.Home;
            sendFrame.RegNums = 1;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums];
            for (int i = 0; i < motionNums; i++)
            {
                sendFrame.Data[i] = 1;
            }
            return sendFrame.MapToBytes();
        }
        public static byte[] SetMotionPolarities(MotionSignalPolarity polar_x, MotionSignalPolarity polar_y, MotionSignalPolarity polar_z, MotionSignalPolarity polar_w)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W;
            sendFrame.Register = MotionRegisters.Polar;
            sendFrame.RegNums = 1;
            sendFrame.Data = new int[4];
            sendFrame.Data[0] = polar_x.MapToByte();
            sendFrame.Data[1] = polar_y.MapToByte();
            sendFrame.Data[2] = polar_z.MapToByte();
            sendFrame.Data[3] = polar_w.MapToByte();
            return sendFrame.MapToBytes();
        }

        /// <summary>
        /// prepare motion parameters. the parameter array should match the motion types.
        /// if X, Y is claimed, then each array should contain 2 elements, the first is for X, and the second is for Y.
        /// </summary>
        /// <param name="motions"></param>
        /// <param name="startSpeed"></param>
        /// <param name="topSpeed"></param>
        /// <param name="accVal"></param>
        /// <param name="dccVal"></param>
        /// <param name="dccPosL"></param>
        /// <param name="tgtPosL"></param>
        /// <param name="dccPosR"></param>
        /// <param name="tgtPosR"></param>
        /// <param name="delayTime"></param>
        /// <param name="repeats"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static byte[] StartMotion(MotorTypes motions, int[] startSpeed, int[] topSpeed, int[] accVal, int[] dccVal, 
                                            int[] dccPosL, int[] tgtPosL, int[] dccPosR, int[] tgtPosR,
                                            int[] delayTime, int[] repeats, bool[] start)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.StartSpeed;
            sendFrame.RegNums = 11;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums * sendFrame.RegNums];
            for(int j = 0; j < sendFrame.RegNums; j++)
            {
                for (int i = 0; i < motionNums; i++)
                {
                    switch (j)
                    {
                        case 0:
                            sendFrame.Data[j * motionNums + i] = startSpeed[i];
                            break;
                        case 1:
                            sendFrame.Data[j * motionNums + i] = topSpeed[i];
                            break;
                        case 2:
                            sendFrame.Data[j * motionNums + i] = accVal[i];
                            break;
                        case 3:
                            sendFrame.Data[j * motionNums + i] = dccVal[i];
                            break;
                        case 4:
                            sendFrame.Data[j * motionNums + i] = dccPosL[i];
                            break;
                        case 5:
                            sendFrame.Data[j * motionNums + i] = tgtPosL[i];
                            break;
                        case 6:
                            sendFrame.Data[j * motionNums + i] = dccPosR[i];
                            break;
                        case 7:
                            sendFrame.Data[j * motionNums + i] = tgtPosR[i];
                            break;
                        case 8:
                            sendFrame.Data[j * motionNums + i] = delayTime[i];
                            break;
                        case 9:
                            sendFrame.Data[j * motionNums + i] = repeats[i];
                            break;
                        case 10:
                            sendFrame.Data[j * motionNums + i] = start[i] ? 1 : 0;
                            break;
                    }
                }
            }
            return sendFrame.MapToBytes();
        }

        /// <summary>
        /// write start speed, top speed, acc value and dcc value to specified motions
        /// </summary>
        /// <param name="motions"></param>
        /// <param name="startSpeed"></param>
        /// <param name="topSpeed"></param>
        /// <param name="accVal"></param>
        /// <param name="dccVal"></param>
        /// <returns></returns>
        public static byte[] SetMotionSpeedAndAcc(MotorTypes motions, int[] startSpeed, int[] topSpeed, int[] accVal, int[] dccVal)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.StartSpeed;
            sendFrame.RegNums = 4;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums * sendFrame.RegNums];
            for (int j = 0; j < sendFrame.RegNums; j++)
            {
                for (int i = 0; i < motionNums; i++)
                {
                    switch (j)
                    {
                        case 0:
                            sendFrame.Data[j * motionNums + i] = startSpeed[i];
                            break;
                        case 1:
                            sendFrame.Data[j * motionNums + i] = topSpeed[i];
                            break;
                        case 2:
                            sendFrame.Data[j * motionNums + i] = accVal[i];
                            break;
                        case 3:
                            sendFrame.Data[j * motionNums + i] = dccVal[i];
                            break;
                    }
                }
            }
            return sendFrame.MapToBytes();
        }

        public static byte[] GetMotionSpeedAndAcc(MotorTypes motions)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Read;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.StartSpeed;
            sendFrame.RegNums = 4;
            sendFrame.Data = null;
            return sendFrame.MapToBytes();
        }

        public static byte[] SetDriveCurrent(MotorTypes motions, MotionDriveCurrent[] current)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.DriveCurrent;
            sendFrame.RegNums = 1;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums * sendFrame.RegNums];
            for (int i = 0; i < motionNums; i++)
            {
                sendFrame.Data[i] = (int)current[i];
            }
            return sendFrame.MapToBytes();
        }

        public static byte[] SetDriveMode(MotorTypes motions, MotionDriveMode[] mode)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motions;
            sendFrame.Register = MotionRegisters.DriveMode;
            sendFrame.RegNums = 1;
            byte motionNums = 0;
            if ((sendFrame.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z)
            {
                motionNums++;
            }
            if ((sendFrame.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W)
            {
                motionNums++;
            }
            sendFrame.Data = new int[motionNums * sendFrame.RegNums];
            for (int i = 0; i < motionNums; i++)
            {
                sendFrame.Data[i] = (int)mode[i];
            }
            return sendFrame.MapToBytes();
        }

        public static byte[] ResetEncoderPosition(MotorTypes motion)
        {
            Frame sendFrame = new Frame();
            sendFrame.CommandType = CmdTypes.Write;
            sendFrame.MotionType = motion;
            sendFrame.Register = MotionRegisters.EncoderPos;
            sendFrame.RegNums = 1;
            sendFrame.Data = new int[] { 0 };
            return sendFrame.MapToBytes();
        }
    }
    internal class Frame
    {
        public byte Head { get; } = 0x55;
        public CmdTypes CommandType { get; set; }
        public MotorTypes MotionType { get; set; }
        public MotionRegisters Register { get; set; }
        public byte RegNums { get; set; }
        public int[] Data { get; set; }
        public byte End { get; } = 0xaa;

        public byte SettingResultH
        {
            get
            {
                if(Data!=null && Data.Length > 0)
                {
                    return (byte)((Data[0] >> 8) & 0x00ff);
                }
                return 0xff;
            }
        }
        public byte SettingResultL
        {
            get
            {
                if (Data != null && Data.Length > 0)
                {
                    return (byte)(Data[0] & 0x00ff);
                }
                return 0xff;
            }
        }

        public byte[] MapToBytes()
        {
            List<byte> result = new List<byte>();
            result.Add(Head);
            result.Add((byte)CommandType);
            result.Add((byte)MotionType);
            result.Add((byte)Register);
            result.Add(RegNums);
            byte motionNums = 0;
            if((MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X) { motionNums++; }
            if ((MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y) { motionNums++; }
            if ((MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z) { motionNums++; }
            if ((MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W) { motionNums++; }
            //result.Add((byte)(motionNums * RegNums * 4));   // data field length
            if (Data!=null && Data.Length > 0)
            {
                foreach(var val in Data)
                {
                    result.AddRange(BitConverter.GetBytes(val));
                }
            }
            result.Add(End);
            return result.ToArray();
        }

        public static Frame ResonseDecode(byte[] bytes, int offset, int endIndex, out int frameLength)
        {
            frameLength = 0;
            Frame result = new Frame();
            if (endIndex - offset < 8)  // write response is 8 bytes
            {
                return null;
            }
            result.CommandType = (CmdTypes)bytes[offset + 1];
            result.MotionType = (MotorTypes)bytes[offset + 2];
            result.Register = (MotionRegisters)bytes[offset + 3];
            result.RegNums = bytes[offset + 4];
            if (result.RegNums == 0) { return null; }
            byte motionNums = 0;
            if ((result.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X) { motionNums++; }
            if ((result.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y) { motionNums++; }
            if ((result.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z) { motionNums++; }
            if ((result.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W) { motionNums++; }
            if (result.MotionType == MotorTypes.CFG) { motionNums = 1; }
            if (motionNums == 0) { return null; }
            if (bytes[offset] != 0x56 || (result.CommandType != CmdTypes.Read && result.CommandType != CmdTypes.Write))
            {
                return null;
            }
            if (result.CommandType == CmdTypes.Read)
            {
                int dataLen = motionNums * result.RegNums * 4;
                if (endIndex < offset + 6 + dataLen) { return null; }
                if (bytes[offset + 5 + dataLen] != 0xab) { return null; }
                result.Data = new int[result.RegNums * motionNums];
                int dataOffset = offset + 5;
                for (int i = 0; i < result.RegNums; i++)
                {
                    for(int j = 0; j < motionNums; j++)
                    {
                        result.Data[i * motionNums + j] = BitConverter.ToInt32(bytes, dataOffset);
                        dataOffset += 4;
                    }
                }
                frameLength = 6 + dataLen;
            }
            else if (result.CommandType == CmdTypes.Write)
            {
                if (bytes[offset + 7] != 0xab) { return null; }
                result.Data = new int[1];   // for write response, the data field is writing result (2 bytes)
                result.Data[0] = (bytes[offset + 5] << 8) | bytes[offset + 6];
                frameLength = 8;
            }
            return result;
        }
    }
}
