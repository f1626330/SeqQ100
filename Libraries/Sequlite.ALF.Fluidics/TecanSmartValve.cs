using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.Fluidics
{
    internal class TecanSmartValve : IValve
    {
        public event ValvePosUpdateHandle OnPositionUpdated;
        #region Private Fields
        protected SerialPort _Port;
        internal AnswerBlock _DTAnswer;
        private object _ThreadLock;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("TecanValve");
        #endregion Private Fields
        string ValveName { get; }
        public TecanSmartValve(string valveName, int timeout = 500)
        {
            _Port = new SerialPort();
            TimeoutMsec = timeout;
            _ThreadLock = new object();
            ValveName = valveName;
            Logger.Log("Create a TecanValve Object: " + valveName);
        }

        #region Public Properties
        public bool IsConnected { get; protected set; }
        public int ValvePos { get; protected set; }
        #endregion Public Properties

        private int TimeoutMsec { get; set; }
        //private bool Connect(TecanBaudrateOptions baudRate = TecanBaudrateOptions.Rate_9600)
        //{
        //    if (IsConnected) { return true; }
        //    var portList = SerialPort.GetPortNames();
        //    if (portList != null)
        //    {
        //        for (int i = 0; i < portList.Length; i++)
        //        {
        //            try
        //            {
        //                if (_Port.IsOpen)
        //                {
        //                    continue;
        //                }
        //                _Port.PortName = portList[i];
        //                _Port.BaudRate = (int)baudRate;
        //                _Port.Parity = Parity.None;
        //                _Port.DataBits = 8;
        //                _Port.StopBits = StopBits.One;
        //                _Port.ReadTimeout = TimeoutMsec;
        //                _Port.NewLine = "\r\n";
        //                _Port.Open();
        //                if (ReportDeviceConfiguration())
        //                {
        //                    if (Initialize() == false)
        //                    {
        //                        return false;
        //                    }
        //                    IsConnected = true;
        //                    break;
        //                }
        //                else
        //                {
        //                    _Port.Close();
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.LogError(ex.ToString());
        //            }
        //        }
        //    }
        //    return IsConnected;
        //}
        private TechDeviceAddresses Address { get; set; } = TechDeviceAddresses.Device0;
        private bool IsReady { get; set; }
        private TechDeviceErrorCodes ErrorCode { get; set; }

        public int CurrentPos => ValvePos;

        #region Public Functions
        public bool Connect(string portName, int baudRate = (int)TecanBaudrateOptions.Rate_9600)
        {
            try
            {
                _Port.PortName = portName;
                _Port.BaudRate = (int)baudRate;
                _Port.Parity = Parity.None;
                _Port.DataBits = 8;
                _Port.StopBits = StopBits.One;
                _Port.ReadTimeout = TimeoutMsec;
                _Port.NewLine = "\r\n";
                _Port.Open();
                if (ReportDeviceConfiguration())
                {
                    if (Initialize() == false)
                    {
                        return false;
                    }
                    IsConnected = true;
                }
                else
                {
                    _Port.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to connect Valve(" + ValveName + ") to port(" + portName + ") with error: " + ex.Message);
            }
            return IsConnected;
        }
        public bool Initialize(bool isCCW = false, int initialPort = 1)
        {
            if (!_Port.IsOpen) { return false; }
            string cmd = string.Format("w{0},{1}R", initialPort, isCCW ? 1 : 0);
            byte[] sendingData = SmartValveProtocol.SendCommand(Address, cmd);
            _Port.Write(sendingData, 0, sendingData.Length);
            if (ReadDTResponse())
            {
                if (ErrorCode != TechDeviceErrorCodes.ErrorFree) { Logger.LogError(ErrorCode.ToString()); return false; }
                // wait until the valve is initialized to the position 1
                do
                {
                    if (ReportDeviceStatus() == false)
                    {
                        return false;
                    }
                    Thread.Sleep(100);
                } while (!IsReady);
                return true;
                //return SetToNewPos(1, true);
            }
            else { return false; }
        }
        public bool SetToNewPos(int pos, bool moveCCW, bool waitForExecution)
        {
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                try
                {
                    string cmd = string.Format("{0}{1}R", moveCCW ? "O" : "I", pos);
                    byte[] sendingData = SmartValveProtocol.SendCommand(Address, cmd);
                    _Port.Write(sendingData, 0, sendingData.Length);
                    if (ReadDTResponse())
                    {
                        if (ErrorCode != TechDeviceErrorCodes.ErrorFree) { Logger.LogError(ErrorCode.ToString()); return false; }
                    }
                    if (waitForExecution)
                    {
                        int trycounts = 0;
                        do
                        {
                            if (++trycounts > 30)
                            {
                                Logger.LogError("Tecan Valve waiting too long");
                                return false;
                            }
                            if (ReportDeviceStatus())
                            {
                                if (IsReady)
                                {
                                    if (ReportValvePos())
                                    {
                                        if (ValvePos != pos)
                                        {
                                            if (trycounts > 25)
                                            {
                                                Logger.LogError(string.Format("Tecan Valve wrong pos, Target Pos:{0}, CurrentPos:{1}", pos, ValvePos));
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            OnPositionUpdated?.Invoke();
                                            return true;
                                        }
                                    }
                                }
                            }
                            Thread.Sleep(100);
                        }
                        while (ValvePos != pos);
                    }
                    Logger.Log($"Set smart valve position elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    sw.Stop();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
            }
        }
        public bool GetCurrentPos()
        {
            lock (_ThreadLock)
            {
                return ReportValvePos();
            }
        }
        #endregion Public Functions

        #region Private Functions
        protected bool ReportDeviceConfiguration()
        {
            if (!_Port.IsOpen) { return false; }
            try
            {
                byte[] sendingData = SmartValveProtocol.SendCommand(Address, "?76");
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (_DTAnswer.DataBlock.Contains("SV"))
                    {
                        return true;
                    }
                    else { Logger.LogError(string.Format("_DTAnswer no SV:{0}", _DTAnswer.DataBlock)); return false; }
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
        protected bool ReportDeviceStatus()
        {
            if (!_Port.IsOpen) { return false; }
            try
            {
                byte[] sendingData = SmartValveProtocol.SendCommand(Address, "Q");
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    return true;
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
        protected bool ReportValvePos()
        {
            if (!_Port.IsOpen) { return false; }
            try
            {
                byte[] sendingData = SmartValveProtocol.SendCommand(Address, "?0");
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (ErrorCode != TechDeviceErrorCodes.ErrorFree) { Logger.LogError(ErrorCode.ToString()); return false; }
                    ValvePos = int.Parse(_DTAnswer.DataBlock);
                    return true;
                }
                else { return false; }
            }
            catch(Exception ex)
            {
                Logger.Log(ex.ToString());
                return false;
            }
        }
        protected bool ReadDTResponse()
        {
            try
            {
                string strResponse = _Port.ReadLine();
                if (strResponse.Length > 0)
                {
                    int startIndex = strResponse.IndexOf("/0");
                    if (startIndex >= 0)
                    {
                        strResponse = strResponse.Substring(startIndex);
                        _DTAnswer = new AnswerBlock();
                        byte[] test = Encoding.ASCII.GetBytes(strResponse);
                        _DTAnswer.StatusAndErrCode = new DeviceStatus(Encoding.ASCII.GetBytes(strResponse.Substring(2, 1))[0]);
                        IsReady = _DTAnswer.StatusAndErrCode.IsReady;
                        ErrorCode = _DTAnswer.StatusAndErrCode.ErrorCode;
                        int len = strResponse.Length;
                        if (strResponse.Length > 4)
                        {
                            _DTAnswer.DataBlock = strResponse.Substring(3, strResponse.Length - 4);
                        }
                        return true;
                    }
                    else { return false; }
                }
                else { Logger.LogError(string.Format("strResponse.Length < 0, strResponse:{0}", strResponse)); return false; }
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        public bool SetToNewPos(int pos, bool waitForExecution)
        {
            return SetToNewPos(pos, false, waitForExecution);
        }

        public bool ResetValve()
        {
            //do nothing
            return true;
        }
        #endregion Private Functions
    }

    internal static class SmartValveProtocol
    {
        #region Public Functions
        public static byte[] SendCommand(TechDeviceAddresses address, string cmd)
        {
            CommandBlock command = new CommandBlock();
            command.DeviceAddress = address;
            command.DataBlock = cmd;
            return command.GetBytes();
        }
        #endregion Public Functions
    }
}
