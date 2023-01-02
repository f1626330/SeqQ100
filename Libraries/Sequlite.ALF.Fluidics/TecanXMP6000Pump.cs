using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.Fluidics
{
    internal class TecanXMP6000Pump : PumpController
    {
        [Flags]
        public enum ValveNumbers
        {
            Valve1 = 0x08,
            Valve2 = 0x04,
            Valve3 = 0x02,
            Valve4 = 0x01,
        }
        #region Public Functions
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("TECAN PUMP");
        public override bool Connect(string portName, TecanBaudrateOptions baudrate = TecanBaudrateOptions.Rate_9600)
        {
            try
            {
                _Port = new System.IO.Ports.SerialPort();
                _Port.PortName = portName;
                _Port.BaudRate = (int)baudrate;
                _Port.Parity = System.IO.Ports.Parity.None;
                _Port.DataBits = 8;
                _Port.StopBits = System.IO.Ports.StopBits.One;
                _Port.ReadTimeout = 500;
                _Port.Open();
                if (ReportDeviceConfiguration())
                {
                    if (InitializePump(50, true) == false)
                    {
                        return false;
                    }
                    IsConnected = true;
                    return true;
                }
                else
                {
                    if (_Port.IsOpen)
                    {
                        _Port.Close();
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                if (_Port.IsOpen)
                {
                    _Port.Close();
                }
                return false;
            }
        }


        /// <summary>
        /// 0 = input / left / waste / Z
        /// 1 = output / right / FC / Y
        /// </summary>
        /// <param name="forcePercent">range from 25 to 100</param>
        /// <param name="SetValveToInput"></param>
        /// <param name="speed">0 or 10-40</param>
        /// <returns></returns>
        public bool InitializePump(int forcePercent = 50, bool SetValveToInput = true, int speed = 0)
        {
            if (!_Port.IsOpen) { return false; }
            string cmd = string.Empty;
            cmd = string.Format("x{0}", forcePercent);
            if (SetValveToInput)
            {
                cmd += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.InitPlungerValveCW];       // 'Z'
                _CurrentPumpValvesPos = new bool[4] { false, false, false, false };
            }
            else
            {
                cmd += PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.InitPlungerValveCCW];      // 'Y'
                _CurrentPumpValvesPos = new bool[4] { true, true, true, true };
            }
            cmd += speed;
            byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, cmd + "R");
            _Port.Write(sendingData, 0, sendingData.Length);
            if (ReadDTResponse())
            {
                while (true)
                {
                    if (ReportDeviceStatus() == false) { return false; }
                    else if (ErrorCode != TechDeviceErrorCodes.ErrorFree) { Logger.LogError(ErrorCode.ToString()); return false; }
                    else if (IsReady) { return true; }
                    Thread.Sleep(10);
                }
            }
            else { return false; }
        }

        /// <summary>
        /// 0 = input / left / waste
        /// 1 = output / right / FC
        /// </summary>
        /// <param name="setToOutput"></param>
        /// <returns></returns>
        public override bool SetValvePositions(bool[] setToOutput)
        {
            if (_CurrentPumpValvesPos.SequenceEqual(setToOutput))
            {
                Logger.Log(string.Format("Pump valve already set to{0}{1}{2}{3}", setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0));
                return true;
            }
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                string settingStr = string.Format("B{0}{1}{2}{3}R", setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0);
                byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, settingStr);
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    while (true)
                    {
                        if (ReportDeviceStatus() == false) { return false; }
                        else if (ErrorCode != TechDeviceErrorCodes.ErrorFree) { Logger.LogError(ErrorCode.ToString()); return false; }
                        else if (IsReady) { break; }
                        Thread.Sleep(10);
                    }
                    Logger.Log($"Set pump valve position elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    sw.Stop();
                    _CurrentPumpValvesPos = setToOutput;
                    return true;
                }
                else { return false; }
            }
        }
        #endregion Public Functions

        #region Private Functions
        protected bool ReportDeviceConfiguration()
        {
            if (!_Port.IsOpen) { return false; }
            try
            {
                byte[] sendingData = SmartValveProtocol.SendCommand(PumpAddress, "?76");
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (_DTAnswer.DataBlock.Contains("XMP"))
                    {
                        return true;
                    }
                    else { Logger.LogError(string.Format("_DTAnswer no XMP:{0}", _DTAnswer.DataBlock)); return false; }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }
        #endregion Private Functions
    }
}
