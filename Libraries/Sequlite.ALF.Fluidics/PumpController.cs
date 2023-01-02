using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Sequlite.ALF.Common;
using System.Windows;
using System.Linq;

namespace Sequlite.ALF.Fluidics
{
    internal class PumpController : IPump
    {

        public event UpdateStatusHandler OnStatusChanged;

        #region Private Fields
        protected SerialPort _Port;
        protected byte[] _ReadBuf;
        //protected int _ReadIndex;
        internal AnswerBlock _DTAnswer;
        protected object _ThreadLock;
        private int _FlowRate = 0;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog( "PUMP");
        protected bool[] _CurrentPumpValvesPos;
        #endregion Private Fields

        #region Public Properties
        private ModeOptions _SelectedMode;
        public ModeOptions SelectedMode
        {
            get { return _SelectedMode; }
            set
            {
                _SelectedMode = value;
            }
        }

        private PathOptions _SelectedPath;
        public PathOptions SelectedPath
        {
            get { return _SelectedPath; }
            set
            {
                _SelectedPath = value;
            }
        }

        public bool IsAllowPathChange
        {
            get { return true; }
        }

        public bool IsConnected { get; protected set; }
        //public bool IsIdle { get; protected set; }
        public bool IsPumpingCanceled { get; protected set; }
        public TechDeviceAddresses PumpAddress { get; set; } = TechDeviceAddresses.Device0;
        public bool IsReady { get; protected set; }
        public TechDeviceErrorCodes ErrorCode { get; protected set; }

        public int CurrentValvePos { get; protected set; }
        public int PumpAbsolutePos { get; protected set; }
        public int PumpActualPos { get; protected set; }
        public int StartPos { get; set; }
        public int FinalPos { get; set; }
        public bool IsPathFC { get; set; }
        #endregion Public Properties

        public PumpController()
        {
            Logger.Log("Create A Pump control Object.");
            _Port = new SerialPort();
            _ReadBuf = new byte[100];
            _SelectedMode = ModeOptions.AspirateDispense;
            _SelectedPath = PathOptions.FC;
            _ThreadLock = new object();
        }

       
        #region Public Functions
        public bool Reconnect()
        {
            Logger.LogWarning("Reconnect Pump");
            _Port.Close();
            IsConnected = false;
            _Port = new SerialPort();
            _ReadBuf = new byte[100];
            return Connect();
        }
        public bool Connect(TecanBaudrateOptions baudRate = TecanBaudrateOptions.Rate_9600)
        {
            if (IsConnected) { return true; }
            for (int tryCounts = 0; tryCounts < 4; tryCounts++)
            {
                var portList = SerialPort.GetPortNames();
                if (portList != null)
                {
                    for (int i = 0; i < portList.Length; i++)
                    {
                        try
                        {
                            if (_Port.IsOpen)
                            {
                                continue;
                            }
                            _Port.PortName = portList[i];
                            _Port.BaudRate = (int)baudRate;
                            _Port.Parity = Parity.None;
                            _Port.DataBits = 8;
                            _Port.StopBits = StopBits.One;
                            _Port.ReadTimeout = 500;
                            _Port.Open();
                            if (ReportDeviceStatus())
                            {
                                InitializePump();
                                IsConnected = true;
                                break;
                            }
                            else
                            {
                                _Port.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex.ToString());
                        }
                    }
                }
                if (IsConnected)
                {
                    break;
                }
            }
            return IsConnected;
        }
        public virtual bool Connect(string portName, TecanBaudrateOptions baudrate = TecanBaudrateOptions.Rate_9600)
        {
            return Connect(baudrate);
        }

        public bool InitializePump(bool isCCW = false, int speed = 1, int inputPort = 0, int outputPort = 1)
        {
            if (!_Port.IsOpen) { return false; }
            byte[] sendingData = PumpDTProtocol.InitializePump(PumpAddress, isCCW, speed, inputPort, outputPort);
            _Port.Write(sendingData, 0, sendingData.Length);
            if (ReadDTResponse())
            {
                //SetValvePath(PathOptions.Waste);
                while (true)
                {
                    if (ReportDeviceStatus() == false) { return false; }
                    if (IsReady && ErrorCode == TechDeviceErrorCodes.ErrorFree) { return true; }
                    else if (ErrorCode == TechDeviceErrorCodes.ErrInitial) { Logger.LogError("Error Initial Pump"); return false; }
                }
            }
            else { return false; }
        }

        public bool SetMicroStepMode(bool enable)
        {
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] sendingData = PumpDTProtocol.MicroSteppingCtrl(PumpAddress, enable);
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (_DTAnswer.StatusAndErrCode.ErrorCode == TechDeviceErrorCodes.ErrorFree) { return true; }
                    else { return false; }
                }
                else { return false; }
            }
        }

        public bool GetPumpPos()
        {
            lock (_ThreadLock)
            {
                if (ReportPumpAbsolutePos())
                {
                    if (ReportPumpActualPos())
                    {
                        OnStatusChanged?.Invoke(IsPathFC);
                        return true;
                    }
                }
                return false;
            }
        }
        public bool SetPumpAbsPos(int pos, bool waitEnded = false)
        {
            lock(_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] sendingData = PumpDTProtocol.SetPumpAbsPos(PumpAddress, pos);
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (waitEnded)
                    {
                        do
                        {
                            if (ReportDeviceStatus())
                            {
                                if (IsReady)
                                {
                                    ReportPumpAbsolutePos();
                                    ReportPumpActualPos();
                                    if (IsPumpingCanceled)
                                    {
                                        IsPumpingCanceled = false;
                                        return false;
                                    }
                                    else if (ErrorCode != TechDeviceErrorCodes.ErrorFree)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        while (IsReady == false);
                    }
                    return true;
                }
                else { return false; }
            }
        }

        public bool SetPumpRelPos(int pos, bool waitEnded = false)
        {
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                try
                {
                    if (!_Port.IsOpen) { return false; }
                    StartPos = PumpAbsolutePos;
                    if (CurrentValvePos == 2)
                    {
                        IsPathFC = true;
                    }
                    byte[] sendingData = PumpDTProtocol.SetPumpRelPos(PumpAddress, pos);
                    _Port.Write(sendingData, 0, sendingData.Length);
                    Stopwatch stopwatch = new Stopwatch();
                    bool isError = false;
                    if (ReadDTResponse())
                    {
                        if (waitEnded)
                        {
                            stopwatch.Start();
                            Thread.Sleep((int)((double)(Math.Abs(pos) / _FlowRate) * 1000 * 0.95)); //wait 95% of calculated pump time before refresh status
                            do
                            {
                                if (IsPumpingCanceled)
                                {
                                    IsPumpingCanceled = false;
                                    return false;
                                }
                                else if (ReportDeviceStatus() == false)
                                {
                                    return false;
                                }
                                else if (ErrorCode != TechDeviceErrorCodes.ErrorFree)
                                {
                                    Logger.LogError(ErrorCode.ToString());
                                    return false;
                                }
                                if (stopwatch.ElapsedMilliseconds > (Math.Abs(pos / _FlowRate) + 3) * 1000 * 2)
                                {
                                    Logger.LogError(string.Format("RelPump step error, waited:{0}ms, pos:{1}, flowrate:{2}", stopwatch.ElapsedMilliseconds, pos, _FlowRate));
                                    isError = true;
                                    break;
                                }
                                Thread.Sleep(5);
                            }
                            while (!IsReady);
                        }
                        if (!ReportPumpAbsolutePos())
                        {
                            Logger.LogError("Update position failed");
                        }
                        FinalPos = PumpAbsolutePos;
                        Logger.Log(string.Format("SetPumpRelPos, Start Pos:{0}, End Pos:{1}", StartPos, FinalPos));
                        OnStatusChanged?.Invoke(IsPathFC);
                        Logger.Log($"Set pump relative position elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                        sw.Stop();
                        stopwatch.Stop();
                        return !isError;
                    }
                    else { return false; }
                }
                catch(Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
                finally
                {
                    IsPathFC = false;
                }
            }
        }

        protected bool SetValvePos(int pos, bool waitEnded = false)
        {
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                bool isCCW = false;
                if ((CurrentValvePos == 2 && pos == 1) || (CurrentValvePos == 3))
                {
                    isCCW = true;
                }
                byte[] sendingData = PumpDTProtocol.SetValvePos(PumpAddress, pos, isCCW);
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (waitEnded)
                    {
                        do
                        {
                            Thread.Sleep(10);
                            if (IsPumpingCanceled)
                            {
                                IsPumpingCanceled = false;
                                return false;
                            }
                            else if (ReportDeviceStatus() == false)
                            {
                                return false;
                            }
                            else if (ErrorCode != TechDeviceErrorCodes.ErrorFree)
                            {
                                Logger.LogError(ErrorCode.ToString());
                                //if(ErrorCode == TechDeviceErrorCodes.ValveOvld) { Reconnect(); }
                                return false;
                            }
                        }
                        while (IsReady == false);
                    }
                    CurrentValvePos = pos;
                    Logger.Log($"Set pump value position elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    sw.Stop();
                    return true;
                }
                else { return false; }
            }
        }

        public bool SetValvePath(PathOptions path, bool waitEnded = false)
        {
            int pos = 0;
            switch (path)
            {
                case PathOptions.FC:
                    pos = 2;
                    break;
                case PathOptions.Waste:
                    pos = 1;
                    break;
                case PathOptions.Bypass:
                    pos = 3;
                    break;
            }
            return SetValvePos(pos, waitEnded);
        }

        /// <summary>
        /// Set the top speed when pump moves
        /// </summary>
        /// <param name="speed">unit of pulse/s</param>
        /// <param name="waitEnded"></param>
        /// <returns></returns>
        public bool SetTopFlowRate(int speed, bool waitEnded = false)
        {
            if(_FlowRate == speed )
            {
                Logger.Log($"Already set to flow rate {_FlowRate}");
                return true;
            }
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                if (!_Port.IsOpen) { return false; }
                byte[] sendingData = PumpDTProtocol.SetTopSpeed(PumpAddress, speed);
                _Port.Write(sendingData, 0, sendingData.Length);
                if (ReadDTResponse())
                {
                    if (waitEnded)
                    {
                        do
                        {
                            Thread.Sleep(10);
                            if (IsPumpingCanceled)
                            {
                                IsPumpingCanceled = false;
                                return false;
                            }
                            else if (ReportDeviceStatus() == false)
                            {
                                return false;
                            }
                            else if (ErrorCode != TechDeviceErrorCodes.ErrorFree)
                            {
                                Logger.LogError(ErrorCode.ToString());
                                return false;
                            }
                        }
                        while (IsReady == false);
                    }
                    _FlowRate = speed;
                    Logger.Log($"Set pump flow rate {_FlowRate} elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    sw.Stop();
                    return true;
                }
                else { return false; }
            }
        }

        //public bool TerminateAction()
        //{
        //    if (!_Port.IsOpen) { return false; }
        //    byte[] sendingData = PumpDTProtocol.TerminateAction(PumpAddress);
        //    _Port.Write(sendingData, 0, sendingData.Length);
        //    Thread.Sleep(100);
        //    if (ReadDTResponse())
        //    {
        //        do
        //        {
        //            Thread.Sleep(10);
        //            if (ReportDeviceStatus() == false)
        //            {
        //                return false;
        //            }
        //        }
        //        while (IsReady == false);
        //        return true;
        //    }
        //    else { return false; }
        //}

        public void CancelPumping()
        {
            IsPumpingCanceled = true;
            for (int i = 0; i < 50; i++)
            {
                if (IsPumpingCanceled == false)
                {
                    Logger.Log("Pump thread cancelled");
                    break;
                }
                Thread.Sleep(10);
            }
            IsPumpingCanceled = false;
        }


        public virtual bool SetValvePositions(bool[] setToOutput)
        {
            return false; //no impl
        }
        public bool TecanPumpMove(bool[] setToOutput, int speed, int pos, bool waitEnded = false)
        {
            Stopwatch sw = Stopwatch.StartNew();
            lock (_ThreadLock)
            {
                try
                {
                    if (!_Port.IsOpen) { return false; }
                    string settingStr = null;
                    //Add valve pos command if need change
                    if (_CurrentPumpValvesPos.SequenceEqual(setToOutput))
                    {
                        Logger.Log(string.Format("Pump valve already set to{0}{1}{2}{3}", setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0));
                    }
                    else 
                    {
                        Logger.Log(string.Format("Set pump valve: {0}{1}{2}{3}", setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0));
                        settingStr += string.Format("B{0}{1}{2}{3}", setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0);
                    }
                    //Add Speed command if need change
                    if (_FlowRate == speed)
                    {
                        Logger.Log($"Flow rate already set to {_FlowRate}");
                    }
                    else 
                    {
                        Logger.Log($"Set Flow rate to {speed}");
                        settingStr += string.Format("V{0}", speed); 
                    }
                    //Add position command and excute command "R"
                    settingStr += string.Format("{0}{1}R", pos > 0 ? PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.RelPickupMove] : PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.RelDispnsMove], Math.Abs(pos));
                    
                    StartPos = PumpAbsolutePos;
                    byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, settingStr);
                    _Port.Write(sendingData, 0, sendingData.Length);
                    Stopwatch stopwatch = new Stopwatch();
                    bool isError = false;
                    if (ReadDTResponse())
                    {
                        if (waitEnded)
                        {
                            stopwatch.Start();
                            Thread.Sleep((int)((double)(Math.Abs(pos) / speed) * 1000 * 0.95)); //wait 95% of calculated pump time before refresh status
                            do
                            {
                                if (IsPumpingCanceled)
                                {
                                    IsPumpingCanceled = false;
                                    return false;
                                }
                                else if (ReportDeviceStatus() == false)
                                {
                                    return false;
                                }
                                else if (ErrorCode != TechDeviceErrorCodes.ErrorFree)
                                {
                                    Logger.LogError(ErrorCode.ToString());
                                    return false;
                                }
                                if (stopwatch.ElapsedMilliseconds > (Math.Abs(pos / speed) + 3) * 1000 * 2)
                                {
                                    Logger.LogError(string.Format("RelPump step error, waited:{0}ms, pos:{1}, flowrate:{2}", stopwatch.ElapsedMilliseconds, pos, speed));
                                    return false;
                                }
                                Thread.Sleep(5);
                            }
                            while (!IsReady);
                        }
                        if (!ReportPumpAbsolutePos())
                        {
                            Logger.LogError("Update position failed");
                        }
                        _FlowRate = speed;
                        _CurrentPumpValvesPos = setToOutput;
                        FinalPos = PumpAbsolutePos;
                        Logger.Log(string.Format("Tecan Pump Movement, Start Pos:{0}, End Pos:{1}, Current Valve{2}{3}{4}{5}, Flowrate{6}", StartPos, FinalPos,
                            setToOutput[0] ? 1 : 0, setToOutput[1] ? 1 : 0, setToOutput[2] ? 1 : 0, setToOutput[3] ? 1 : 0, _FlowRate));
                        OnStatusChanged?.Invoke(IsPathFC);
                        Logger.Log($"Set Tecan pump valve, speed, relative position elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                        sw.Stop();
                        stopwatch.Stop();
                        return !isError;
                    }
                    else { return false; }
                }
                catch(Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
                finally
                {
                    IsPathFC = false;
                }
            }
            
        }
        #endregion Public Functions

        #region Private Functions
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
                        if (strResponse.Length > 5)
                        {
                            _DTAnswer.DataBlock = strResponse.Substring(3, strResponse.Length - 5);
                        }
                        if(ErrorCode!= TechDeviceErrorCodes.ErrorFree)
                        {
                            Logger.LogError("Error code: " + ErrorCode);
                            return false;
                        }
                        return true;
                    }
                    else { Logger.LogError("startIndex < 0"); return false; }
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

        protected bool ReportPumpAbsolutePos()
        {
            if (!_Port.IsOpen) { return false; }
            byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ReportAbsPos]);
            _Port.Write(sendingData, 0, sendingData.Length);
            //Thread.Sleep(100);
            if (ReadDTResponse())
            {
                int temp;
                if (int.TryParse(_DTAnswer.DataBlock, out temp))
                {
                    PumpAbsolutePos = temp;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else { return false; }
        }
        protected bool ReportPumpActualPos()
        {
            if (!_Port.IsOpen) { return false; }
            byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ReportActPos]);
            _Port.Write(sendingData, 0, sendingData.Length);
            //Thread.Sleep(100);
            if (ReadDTResponse())
            {
                int temp;
                if (int.TryParse(_DTAnswer.DataBlock, out temp))
                {
                    PumpActualPos = temp;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else { return false; }
        }

        protected bool ReportDeviceStatus()
        {
            if (!_Port.IsOpen) { return false; }
            try
            {
                byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, "Q");
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

        public bool ReportValvePos()
        {
            if (!_Port.IsOpen) { return false; }
            byte[] sendingData = PumpDTProtocol.SendCommand(PumpAddress, PumpProtocol.CommandsDic[PumpProtocol.CommandsEnum.ReportValvePos]);
            _Port.Write(sendingData, 0, sendingData.Length);
            if (ReadDTResponse())
            {
                int temp;
                if (int.TryParse(_DTAnswer.DataBlock, out temp))
                {
                    CurrentValvePos = temp;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else { return false; }
        }

        #endregion Private Functions
    }

   
}
