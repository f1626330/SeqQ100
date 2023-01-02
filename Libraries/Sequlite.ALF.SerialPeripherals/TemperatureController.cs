using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.SerialPeripherals
{
    public class TemperatureController
    {
        enum ControllerCommands
        {
            GetTemper = 0x01,
            GetDrivePower = 0x04,
            SetControlInput = 0x29,
            SetSensorType = 0x2a,
            SetControlType = 0x2b,
            GetOutputDir = 0x45,
            SetOutputDir = 0x2c,
            SetPowerOn = 0x2d,
            SetShutDownIfAlarm = 0x2e,
            SetFixedTemper = 0x1c,
            SetCtrlP = 0x1d,
            GetCtrlP = 0x51,
            SetCtrlI = 0x1e,
            GetCtrlI = 0x52,
            SetCtrlD = 0x1f,
            GetCtrlD = 0x53,
            SetSensorOffset = 0x26,
            SetHeatGain = 0x0c,
            SetCoolGain = 0x0d,
            GetHeatGain = 0x5c,
            GetCoolGain = 0x5d,
        }

        public enum ControlInputOptions
        {
            PCSet = 0,
            Potentiometer = 1,
            VoltageInput = 2,
            CurrentInput = 3,
            Differential = 4,
        }

        public enum SensorTypes
        {
            TS141_5k,
            TS67_TS136_15k,
            TS91_10k,
            TS165_230k,
            TS104_50k,
            YSI_H_TP53_10k,
        }

        public enum ControlTypes
        {
            DeadbandCtrl,
            PIDCtrl,
            PCCtrl,
        }

        public enum OutputDirections
        {
            WP1P_WP2N,
            WP2P_WP1N,
        }

        private SerialPort _Commport;
        private object _ThreadLock;
        private int _ResponseData;

        private System.Timers.Timer _rampTimer;
        private double _targetTemperature; //< the current setpoint for the chemistry (FC) temperature controller
        private double _rampStartTemperature; //< the temperature at the start of a ramp operation
        private double _rampTargetTemperature; //< the final target temperature at the end of a ramp operation
        DateTime _rampStartTime; //< the time a ramp was started
        private double _rampTimeMs; //< the time length of a temperature ramp (units = [ms])
        private bool _isRamping; //< used to stop ramping if the control switch is turned off. (Consider switching to timer.Enabled instead)
        private bool _isControllerOn = false;
        public bool IsRamping => _isRamping;

        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Temperature Controller");
        private bool _IsNewLog;
        private string _FileName;
        private string _FolderStr;
        private List<string> _LogList;
        private Thread _LogThread;
        private static object _Lock = new object();
        private static object _instanceCreationLocker = new object();  //< used for locking during instantiation
        private MainBoardController _MainboardController;

        #region Public Constructor
        static TemperatureController _Instance;
        public static TemperatureController GetInstance()
        {
            if (_Instance == null)
            {
                lock (_instanceCreationLocker)
                {
                    if (_Instance == null)
                    {
                        _Instance = new TemperatureController();
                    }
                }
            }
            return _Instance;
        }
        private TemperatureController()
        {
            _Commport = new SerialPort();
            _ThreadLock = new object();

            // construct a timer for temperature ramping.
            // by default, auto-reset is enabled and interval is 100 ms
            _rampTimer = new System.Timers.Timer();
            _rampTimer.Elapsed += _RampTimer_Elapsed;

            _FileName = string.Format("{0}-FCTemperCommLog.txt", DateTime.Now.ToString("yyyyMMddHHmmss"));
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _FolderStr = Path.Combine(commonAppData, "Sequlite\\Log\\FCTempLog");
            if (!Directory.Exists(_FolderStr))
            {
                DirectoryInfo di = Directory.CreateDirectory(_FolderStr);
            }
            _FolderStr = Path.Combine(_FolderStr, _FileName);
            _LogList = new List<string>();
            _LogThread = new Thread(() =>
            {
                while (true)
                {
                    while (_LogList.Count > 0)
                    {
                        FileMode mode = _IsNewLog ? FileMode.Create : FileMode.Append;
                        _IsNewLog = false;
                        try
                        {
                            using (FileStream fs = new FileStream(_FolderStr, mode))
                            {
                                byte[] logs = Encoding.Default.GetBytes(_LogList[0]);
                                fs.Write(logs, 0, logs.Length);
                                fs.Flush();
                                lock (_Lock)
                                {
                                    _LogList.RemoveAt(0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex.ToString());
                        }
                    }
                    Thread.Sleep(100);
                }
            });
            _LogThread.IsBackground = true;
            _LogThread.Start();

            _MainboardController = MainBoardController.GetInstance();
        }

        /// <summary>
        /// Callback function for _rampTimer. This method estimates the current 
        /// temperature setpoint using the time elapsed since the start of the ramp and
        /// interpolating between the start and end setpoints.
        /// </summary>
        /// <param name="sender">unused</param>
        /// <param name="e">Signal time is used to measure the amount of time 
        /// that has elapsed since the start of the ramp</param>
        private void _RampTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isRamping)
            {
                double deltaTemperature = _rampTargetTemperature - _rampStartTemperature;
                double elapsedTime = (e.SignalTime - _rampStartTime).TotalMilliseconds;
                double nextTimeMs = elapsedTime + _rampTimer.Interval;
                double nextTemperature;

                // the next timer event is expected to occur before the ramp ends
                if (nextTimeMs.CompareTo(_rampTimeMs) < 0)
                {
                    // interpolate next temperature setpoint
                    double rampFraction = nextTimeMs / _rampTimeMs;
                    nextTemperature = _rampStartTemperature + (deltaTemperature * rampFraction);
                }
                else
                {
                    // the next timer elapsed event will be equal to or after the ramp duration
                    // set to the final target temperature
                     nextTemperature = _rampStartTemperature + deltaTemperature;
                    
                }

                // do not end the ramp until the total ramp time has elapsed
                if(elapsedTime >= _rampTimeMs)
                {
                    SetTargetTemperature(_rampTargetTemperature);
                    _isRamping = false;
                    _rampTimer.Stop();
                }
                else
                {
                    SetTargetTemperature(nextTemperature);
                }
            }
            else
            {
                // timer should not fire, ramping is done
            }
        }
        #endregion Public Constructor

        #region Public Properties
        public bool IsConnected { get; private set; }
        public double CurrentTemper { get; private set; }
        public double CtrlP { get; private set; }
        public double CtrlI { get; private set; }
        public double CtrlD { get; private set; }
        public double HeatGain { get; private set; }
        public double CoolGain { get; private set; }
        public double DrivingPower { get; private set; }
        public bool IsOutputFwdDir { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect()
        {
            if (IsConnected == true) { return true; }
            if (_MainboardController.IsProtocolRev2)
            {
                IsConnected = _MainboardController.IsConnected;
                return IsConnected;
            }
            var portNames = SerialPort.GetPortNames();
            foreach (var port in portNames)
            {
                try
                {
                    _Commport.PortName = port;
                    _Commport.BaudRate = 9600;
                    _Commport.Parity = Parity.None;
                    _Commport.DataBits = 8;
                    _Commport.StopBits = StopBits.One;
                    _Commport.ReadTimeout = 1000;
                    _Commport.Open();
                    IsConnected = SendCommand(Protocol.WriteCmdtoController(ControllerCommands.GetTemper, 0));
                    if (IsConnected)
                    {
                        SetSensorType(SensorTypes.TS104_50k);
                        return true;
                    }
                    else
                    {
                        _Commport.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    if (_Commport.IsOpen)
                    {
                        _Commport.Close();
                    }
                }
            }
            return false;
        }
        public void Disconnect()
        {
            if (!IsConnected) { return; }
            try
            {
                _Commport.Close();
                IsConnected = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
        public bool GetTemper() //todo: improve method name
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetChemiTemper();
                CurrentTemper = _MainboardController.ChemiTemper;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetTemper, 0);
            if (SendCommand(cmd) == false) { return false; }
            CurrentTemper = _ResponseData / 100.0;
            return true;
        }
        public bool GetDrivingPower()
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetDrivePower, 0);
            if (SendCommand(cmd) == false) { return false; }
            DrivingPower = Math.Round(_ResponseData / -5.11, 2);    // to percent, 2 decimals
            return true;
        }
        public bool SetControlInput(ControlInputOptions input)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetControlInput, (int)input);
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetSensorType(SensorTypes sensor)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetSensorType, (int)sensor);
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetControlType(ControlTypes control)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetControlType, (int)control);
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool GetOutputDir(OutputDirections dir)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetOutputDir, 0);
            if (SendCommand(cmd) == false) { return false; }
            IsOutputFwdDir = _ResponseData == 0 ? true : false;
            return true;
        }
        public bool SetOutputDir(OutputDirections dir)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetOutputDir, (int)dir);
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetControlSwitch(bool setOn)
        {
            if (IsConnected == false) { return false; }
            if (_isControllerOn == setOn) { return true; }
            if (_MainboardController.IsProtocolRev2)
            {
                if (_MainboardController.SetChemiTemperCtrlStatus(setOn) == false)
                {
                    return false;
                }
                else { _isControllerOn = setOn; }
                if (!setOn)
                {
                    _isRamping = false;
                }
                return true;
            }

            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetPowerOn, setOn ? 1 : 0);
            if (SendCommand(cmd) == false) { return false; }
            else { _isControllerOn = setOn; }
            if (!setOn)
            {
                _isRamping = false;
            }
            return true;
        }
        
        /// <summary>
        /// Sets the chemistry (flow cell) temperature controller set point.
        /// This method writes to the temperature set point register of the controller.
        /// </summary>
        /// <param name="targetTemperature">The temperature set point (units == [°C])</param>
        /// <returns>True if the temperature set point was set successfully.</returns>
        private bool SetTargetTemperature(in double targetTemperature)
        {
            _targetTemperature = targetTemperature;
            if (_MainboardController.IsProtocolRev2)
            {
                if (_MainboardController.SetChemiTemper(targetTemperature) == false)
                {
                    return false;
                }
                return true;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetFixedTemper, (int)(targetTemperature * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool GetCtrlP()
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetTemperCtrlParameters();
                CtrlP = _MainboardController.TemperCtrlP;
                CtrlI = _MainboardController.TemperCtrlI;
                CtrlD = _MainboardController.TemperCtrlD;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetCtrlP, 0);
            if (SendCommand(cmd) == false) { return false; }
            CtrlP = _ResponseData / 100.0;
            return true;
        }
        public bool GetCtrlI()
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetTemperCtrlParameters();
                CtrlP = _MainboardController.TemperCtrlP;
                CtrlI = _MainboardController.TemperCtrlI;
                CtrlD = _MainboardController.TemperCtrlD;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetCtrlI, 0);
            if (SendCommand(cmd) == false) { return false; }
            CtrlI = _ResponseData / 100.0;
            return true;
        }
        public bool GetCtrlD()
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetTemperCtrlParameters();
                CtrlP = _MainboardController.TemperCtrlP;
                CtrlI = _MainboardController.TemperCtrlI;
                CtrlD = _MainboardController.TemperCtrlD;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetCtrlD, 0);
            if (SendCommand(cmd) == false) { return false; }
            CtrlD = _ResponseData / 100.0;
            return true;
        }
        public bool SetCtrlP(double ctrlP)
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.SetTemperCtrlParameters(ctrlP, CtrlI, CtrlD, HeatGain, CoolGain);
                if (result)
                {
                    CtrlP = ctrlP;
                }
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetCtrlP, (int)(ctrlP * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetCtrlI(double ctrlI)
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.SetTemperCtrlParameters(CtrlP, ctrlI, CtrlD, HeatGain, CoolGain);
                if (result)
                {
                    CtrlI = ctrlI;
                }
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetCtrlI, (int)(ctrlI * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetCtrlD(double ctrlD)
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.SetTemperCtrlParameters(CtrlP, CtrlI, ctrlD, HeatGain, CoolGain);
                if (result)
                {
                    CtrlD = ctrlD;
                }
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetCtrlD, (int)(ctrlD * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetSensorOffset(double offset)
        {
            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetSensorOffset, (int)(offset * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetHeaterGain(double gain)
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.SetTemperCtrlParameters(CtrlP, CtrlI, CtrlD, gain, CoolGain);
                if (result)
                {
                    HeatGain = gain;
                }
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetHeatGain, (int)(gain * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool SetCoolerGain(double gain)
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.SetTemperCtrlParameters(CtrlP, CtrlI, CtrlD, HeatGain, gain);
                if (result)
                {
                    CoolGain = gain;
                }
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.SetCoolGain, (int)(gain * 100));
            if (SendCommand(cmd) == false) { return false; }
            return true;
        }
        public bool GetHeaterGain()
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetTemperCtrlParameters();
                HeatGain = _MainboardController.ChemiTemperHeatGain;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetHeatGain, 0);
            if (SendCommand(cmd) == false) { return false; }
            HeatGain = _ResponseData / 100.0;
            return true;
        }
        public bool GetCoolerGain()
        {
            if (_MainboardController.IsProtocolRev2)
            {
                bool result = _MainboardController.GetTemperCtrlParameters();
                CoolGain = _MainboardController.ChemiTemperCoolGain;
                return result;
            }

            if (IsConnected == false) { return false; }
            byte[] cmd = Protocol.WriteCmdtoController(ControllerCommands.GetCoolGain, 0);
            if (SendCommand(cmd) == false) { return false; }
            CoolGain = _ResponseData / 100.0;
            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns the estimated amount of time left to complete a temperature
        /// ramp (units = [ms]). Returns 0 if the temperature controller is not ramping.</returns>
        public double GetRampTimeRemaining()
        {
            return _isRamping ? _rampTimeMs - (DateTime.Now - _rampStartTime).TotalMilliseconds : 0;
        }

        /// <summary>
        /// Calculates the current process error (set point - process value).
        /// The process error is referred to as the process difference 
        /// to avoid confustion with error messages.
        /// This method updates the most recent temperature value using GetTemper
        /// prior to calcualating process error.
        /// </summary>
        /// <returns>The process error (SP - PV)</returns>
        public double GetProcessDifference()
        {
            GetTemper();
            return _targetTemperature - CurrentTemper;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetTemperature">The target temperature (units == [°C])</param>
        /// <param name="rampTime">Ramp time. Must be a positive number > 5 seconds. (units == [seconds])</param>
        /// <param name="tolerance">Temperature tolerance. Must be a positive number > 0.5  (units == [°C])</param>
        /// <returns></returns>
        public bool SetTemperature(in double targetTemperature, in double rampTime, in double tolerance = 0.5)
        {
            const int rampStepInterval = 2000; //< the amount of time to wait between ramp steps (units == [ms])
            const double minRampTime = 5.0; //< minimum ramp time [s]. If less, temperature is set directly
            const double minTolerance = 0.5; //< minimum tolerance [°C]. If less, temperature is set directly

            try
            {
                // update temperature value in CurrentTemper
                if (GetTemper() == false)
                {
                    Logger.Log("Error: Set Target Temperature Failed", SeqLogFlagEnum.DEBUG);
                    return false;
                }

                // check that ramping timers are turned off
                if (_isRamping)
                {
                    _rampTimer.Stop();
                    _isRamping = false;
                }

                // check if a ramping process is needed
                if (rampTime < minRampTime || Math.Abs(targetTemperature - _targetTemperature) < minTolerance)
                {
                    // no ramping process
                    if(SetTargetTemperature(targetTemperature) == false)
                    {
                        Logger.Log("Error: Set Control Switch Failed", SeqLogFlagEnum.DEBUG);
                        return false;
                    }
                }
                else
                {
                    // setup timer to ramp temperature
                    // set the time in ms between timer elapsed events
                    _rampTimer.Interval = rampStepInterval;

                    // calculate the next temperature
                    _rampStartTemperature = CurrentTemper; // could also use current target temperature
                    _rampTargetTemperature = targetTemperature;
                    _rampStartTime = DateTime.Now;
                    _rampTimeMs = rampTime * 1000;
                    double deltaTemperature = _rampTargetTemperature - _rampStartTemperature; // process error = SP - PV
                    double rampFraction = rampStepInterval / _rampTimeMs;
                    double nextTemperature = _rampStartTemperature + (deltaTemperature * rampFraction);
                    SetTargetTemperature(nextTemperature);

                    _isRamping = true;
                    _rampTimer.Start();
                }

                // turn on chemi temperature control
                if (!_isControllerOn) { Thread.Sleep(100); } //avoid continuously sending signal
                if (SetControlSwitch(true) == false)
                {
                    Logger.Log("Error: Set Control Switch Failed", SeqLogFlagEnum.DEBUG);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }
        #endregion Public Functions

        private bool SendCommand(byte[] cmd)
        {
            lock (_ThreadLock)
            {
                try
                {
                    return SendProcess(cmd);
                }
                catch (TimeoutException)
                {
                    Logger.Log("Temper controller timeout, retry");
                    try
                    {
                        var port = _Commport.PortName;
                        _Commport.Close();
                        _Commport = null;
                        _Commport = new SerialPort();
                        _Commport.PortName = port;
                        _Commport.BaudRate = 9600;
                        _Commport.Parity = Parity.None;
                        _Commport.DataBits = 8;
                        _Commport.StopBits = StopBits.One;
                        _Commport.ReadTimeout = 1000;
                        _Commport.Open();
                        return SendProcess(cmd);
                    }
                    catch (Exception)
                    {
                        Logger.Log("Temper controller resend command failed.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
            }
        }

        private bool SendProcess(byte[] cmd)
        {
            LogToFile("Sending: " + Encoding.ASCII.GetString(cmd));
            _Commport.Write(cmd, 0, cmd.Length);
            string response = _Commport.ReadTo("^");
            LogToFile("Received: " + response);
            int offsetIndex = response.IndexOf("*");
            LogToFile(" Offset of STX: " + offsetIndex);
            if (offsetIndex < 0) { return false; }
            if (Protocol.IsResponseCheckSumCorrect(response.Substring(offsetIndex)) == false)
            {
                Logger.LogError("Controller response check sum error");
                return false;
            }
            string responseData = response.Substring(offsetIndex + 1, 8);
            if (responseData == "XXXXXXXX")
            {
                Logger.LogError("Controller complains check sum error");
                return false;
            }
            _ResponseData = int.Parse(responseData, System.Globalization.NumberStyles.HexNumber);
            LogToFile(" Data is: " + _ResponseData + "\r\n");
            return true;
        }
        private void LogToFile(string newLog)
        {
            string logPack = string.Format("{0}: {1}", DateTime.Now.ToString("HH:mm:ss:fff"), newLog);
            lock (_Lock)
            {
                _LogList.Add(logPack);
            }
        }

        static class Protocol
        {
            const byte stx = 0x2a;
            const byte etx = 0x0d;
            const byte ack = 0x5e;
            const string address = "00";

            public static byte[] WriteCmdtoController(ControllerCommands cmd, int value)
            {
                List<byte> bytes = new List<byte>();
                bytes.Add(stx);
                bytes.AddRange(Encoding.ASCII.GetBytes(address));
                byte cmdVal = (byte)cmd;
                bytes.AddRange(Encoding.ASCII.GetBytes(cmdVal.ToString("x2")));
                bytes.AddRange(Encoding.ASCII.GetBytes(value.ToString("x8")));
                byte checksum = 0;
                for (int i = 1; i < bytes.Count; i++)
                {
                    checksum += bytes[i];
                }
                bytes.AddRange(Encoding.ASCII.GetBytes(checksum.ToString("x2")));
                bytes.Add(etx);
                return bytes.ToArray();
            }

            /// <summary>
            /// return true if the check sum is correct, otherwise return false.
            /// the response's structure is: (stx: "*")+DDDDDDDD+SS+(ack: "^")
            /// </summary>
            /// <param name="response">response without the last character "^"</param>
            /// <returns></returns>
            public static bool IsResponseCheckSumCorrect(string response)
            {
                var resBytes = Encoding.ASCII.GetBytes(response.Substring(1));  // skip the stx field
                if (resBytes.Length != 10) { return false; }
                byte sum = 0;
                for (int i = 0; i < 8; i++)
                {
                    sum += resBytes[i];
                }
                var sumBytes = Encoding.ASCII.GetBytes(sum.ToString("x2"));
                if (sumBytes[0] != resBytes[8] || sumBytes[1] != resBytes[9])
                {
                    return false;
                }
                return true;
            }
        }
    }
}
