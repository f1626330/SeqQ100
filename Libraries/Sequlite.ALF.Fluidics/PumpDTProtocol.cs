using System;
using System.Collections.Generic;
using System.Text;

namespace Sequlite.ALF.Fluidics
{
    internal static class PumpDTProtocol
    {
        #region Internal Classes
        #endregion Internal Classes

        #region Master Commands
        /// <summary>
        /// This is a generic function for master command creation.
        /// </summary>
        /// <param name="pump">pump address.</param>
        /// <param name="cmd">It is the caller's responsibility to fill the Data Block with this parameter.</param>
        /// <returns></returns>
        public static byte[] SendCommand(TechDeviceAddresses pump, string cmd)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = cmd;
            return command.GetBytes();
        }

        public static byte[] InitializePump(TechDeviceAddresses pump, bool isCCW, int speed, int inputPort, int outputPort)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            if (isCCW)
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.InitPlungerValveCCW];
            }
            else
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.InitPlungerValveCW];
            }
            command.DataBlock += string.Format("{0},{1},{2}", speed, inputPort, outputPort);
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }

        public static byte[] MicroSteppingCtrl(TechDeviceAddresses pump, bool finePositioningOn)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = "N" + (finePositioningOn ? "1" : "0") + "R";
            return command.GetBytes();
        }

        public static byte[] SetPumpAbsPos(TechDeviceAddresses pump, int position)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.AbsPosMove];
            command.DataBlock += position.ToString();
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }

        public static byte[] SetPumpRelPos(TechDeviceAddresses pump, int position)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            if (position > 0)
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.RelPickupMove];
            }
            else
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.RelDispnsMove];
            }
            command.DataBlock += Math.Abs(position).ToString();
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }

        public static byte[] SetValvePos(TechDeviceAddresses pump, int position, bool isCCW)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            if (isCCW)
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ValveToOutput];
            }
            else
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ValveToInput];
            }
            command.DataBlock += position.ToString();
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }

        public static byte[] SetTopSpeed(TechDeviceAddresses pump, int topSpeed)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.SetTopSpeed];
            command.DataBlock += topSpeed.ToString();
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }

        public static byte[] TerminateAction(TechDeviceAddresses pump)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.TerminateCmd];
            return command.GetBytes();
        }

        public static byte[] RunCommand(TechDeviceAddresses pump, string cmd)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            command.DataBlock = cmd;
            return command.GetBytes();
        }

        public static byte[] RunCmdLoop(TechDeviceAddresses pump, string previousCmd, string loopCmd, int cycles)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = pump;
            if (!string.IsNullOrEmpty(previousCmd))
            {
                command.DataBlock = previousCmd;
                command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.MarkRepeatStart];
            }
            else
            {
                command.DataBlock = PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.MarkRepeatStart];
            }
            command.DataBlock += loopCmd;
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.RepeatCmdSq] + cycles.ToString();
            command.DataBlock += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ExecuteCmd];
            return command.GetBytes();
        }
        #endregion Master Commands
    }
}
