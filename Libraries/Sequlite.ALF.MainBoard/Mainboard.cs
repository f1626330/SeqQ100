using Hywire.CommLibrary;
using Sequlite.ALF.Common;
using System;

namespace Sequlite.ALF.MainBoard
{
    public class Mainboard
    {
        #region Events
        public delegate void EventHandler(MBProtocol.Registers startReg, int numbers);
        public event EventHandler OnUpdateStatus;

        public delegate void OnLEDStatusChangedHandle(LEDTypes led, bool status);
        public event OnLEDStatusChangedHandle OnLEDStatusChanged;
        #endregion Events

        #region Private Fields
        private SerialComm _Comm;

        private bool _IsGLEDSetOn;
        private bool _IsRLEDSetOn;
        private bool _IsWLEDSetOn;
        private int _GLEDOnTimerCounts;
        private int _RLEDOnTimerCounts;
        private int _WLEDOnTimerCounts;
        private System.Timers.Timer _LEDOnTimer;
        private static object _LEDLock = new object();
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("MainBoard");
        #endregion Private Fields
        private static Object LockInstanceCreation = new Object();
        static Mainboard _Mainboard = null;
        public static Mainboard GetInstance()
        {
            if (_Mainboard == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_Mainboard == null)
                    {
                        _Mainboard = new Mainboard();
                    }

                }
            }
            return _Mainboard;
        }
        #region Constructor
        private Mainboard()
        {
            _Comm = new SerialComm(1000);
            _Comm.OnResponseArrived += _Comm_OnResponseArrived;

            _LEDOnTimer = new System.Timers.Timer(1000);
            _LEDOnTimer.AutoReset = true;
            _LEDOnTimer.Elapsed += _LEDOnTimer_Elapsed;
            _LEDOnTimer.Start();

            GLEDMaxOnTime = 100;
            RLEDMaxOnTime = 100;
            WLEDMaxOnTime = 100;
            Logger.Log("Create a Mainboard Object");
        }

        private void _LEDOnTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_IsGLEDSetOn)
            {
                if (++_GLEDOnTimerCounts > GLEDMaxOnTime)
                {
                    SetLEDStatus(LEDTypes.Green, false);
                    OnLEDStatusChanged?.Invoke(LEDTypes.Green, false);
                }
            }
            else { _GLEDOnTimerCounts = 0; }

            if (_IsRLEDSetOn)
            {
                if (++_RLEDOnTimerCounts > RLEDMaxOnTime)
                {
                    SetLEDStatus(LEDTypes.Red, false);
                    OnLEDStatusChanged?.Invoke(LEDTypes.Red, false);
                }
            }
            else { _RLEDOnTimerCounts = 0; }

            if (_IsWLEDSetOn)
            {
                if (++_WLEDOnTimerCounts > WLEDMaxOnTime)
                {
                    SetLEDStatus(LEDTypes.White, false);
                    OnLEDStatusChanged?.Invoke(LEDTypes.White, false);
                }
            }
            else { _WLEDOnTimerCounts = 0; }
        }

        private void _Comm_OnResponseArrived()
        {
            OnUpdateStatus?.Invoke(_Comm.ResponseStartReg, _Comm.ResponseRegNumbers);
        }
        #endregion Constructor

        #region Public Properties
        public bool IsConnected { get { return _Comm.IsConnected; } }
        public int BaudRate { get; set; } = 115200;

        public string DeviceType { get { return MBProtocol.DeviceType; } }
        public string HWVersion { get { return MBProtocol.HWVersion; } }
        public string FWVersion { get { return MBProtocol.FWVersion; } }
        public uint GLEDIntensity { get { return MBProtocol.GLEDIntensity; } }
        public bool IsGLEDOn { get { return MBProtocol.IsGLEDOn; } }
        public uint RLEDIntensity { get { return MBProtocol.RLEDIntensity; } }
        public bool IsRLEDOn { get { return MBProtocol.IsRLEDOn; } }
        public OnOffInputsType OnOffInputs { get { return MBProtocol.OnOffInputs; } }
        public bool IsChemiTemperCtrlOn { get { return MBProtocol.IsChemiTemperCtrlOn; } }
        public double ChemiTemperCtrlRamp { get { return MBProtocol.ChemiTemperCtrlRamp; } }
        public double ChemiTemperCtrlPower { get { return MBProtocol.ChemiTemperCtrlPower; } }
        public double ChemiTemper { get { return MBProtocol.ChemiTemper; } }
        public double HeatSinkTemper { get { return MBProtocol.HeatSinkTemper; } }
        public double CoolerTemper { get { return MBProtocol.CoolerTemper; } }
        public double AmbientTemper { get { return MBProtocol.AmbientTemper; } }
        public uint PDValue { get { return MBProtocol.PDValue; } }
        public uint WLEDIntensity { get { return MBProtocol.WLEDIntensity; } }
        public bool IsWLEDOn { get { return MBProtocol.IsWLEDOn; } }
        public int FilterPos { get { return MBProtocol.FilterPos; } }
        public uint YMotionSpd { get { return MBProtocol.YMotionSpd; } }
        public uint YMotionAcc { get { return MBProtocol.YMotionAcc; } }
        public int YMotionPos { get { return MBProtocol.YMotionPos; } }
        public bool IsYMotionEnabled { get { return MBProtocol.IsYMotionEnabled; } }
        public int PMotionPos { get { return MBProtocol.PMotionPos; } }
        public int TemperCtrlPro { get { return MBProtocol.TemperCtrlPro; } }
        public int TemperCtrlInt { get { return MBProtocol.TemperCtrlInt; } }
        public int TemperCtrlDif { get { return MBProtocol.TemperCtrlDif; } }
        public int TemperCtrlPower { get { return MBProtocol.TemperCtrlPower; } }
        public bool IsCartridgeEnabled { get { return MBProtocol.IsPMotionEnabled; } }
        public int CartridgePower { get { return MBProtocol.PMotionPower; } }

        public int GLEDMaxOnTime { get; set; }
        public int RLEDMaxOnTime { get; set; }
        public int WLEDMaxOnTime { get; set; }
        #endregion Public Properties

        #region Public Functions
        public void Connect()
        {
            if (IsConnected)
            {
                return;
            }
            _Comm.Connect(BaudRate);
        }
        public bool Connect(string portName)
        {
            if (IsConnected) { return true; }
            _Comm.Connect(portName, BaudRate);
            return IsConnected;
        }
        public void Disconnect()
        {
            if (IsConnected)
            {
                _Comm.Disconnect();
            }
        }

        /// <summary>
        /// Get device information: device type, hardware version, firmware version
        /// </summary>
        /// <returns>truth if reading is successful.</returns>
        public bool GetDeviceInfo()
        {
            return Query(MBProtocol.Registers.DeviceType, 3, true);
        }

        /// <summary>
        /// Get the intensity of the specified LED.
        /// </summary>
        /// <param name="ledType">0: Green LED; 1: Red LED; 2: White LED;</param>
        /// <returns>truth if reading is successful.</returns>
        public bool GetLEDIntensity(LEDTypes ledType)
        {
            MBProtocol.Registers target;
            switch (ledType)
            {
                case LEDTypes.Green:
                    target = MBProtocol.Registers.GLEDIntensity;
                    break;
                case LEDTypes.Red:
                    target = MBProtocol.Registers.RLEDIntensity;
                    break;
                case LEDTypes.White:
                    target = MBProtocol.Registers.WLEDIntensity;
                    break;
                default:
                    return false;
            }
            return Query(target, true);
        }

        public bool SetLEDIntensity(LEDTypes ledType, uint intensity)
        {
            MBProtocol.Registers target;
            switch (ledType)
            {
                case LEDTypes.Green:
                    target = MBProtocol.Registers.GLEDIntensity;
                    break;
                case LEDTypes.Red:
                    target = MBProtocol.Registers.RLEDIntensity;
                    break;
                case LEDTypes.White:
                    target = MBProtocol.Registers.WLEDIntensity;
                    break;
                default:
                    return false;
            }
            return Setting(target, (int)intensity, true);
        }

        public bool GetLEDStatus(LEDTypes ledType)
        {
            lock (_LEDLock)
            {
                MBProtocol.Registers target;
                switch (ledType)
                {
                    case LEDTypes.Green:
                        target = MBProtocol.Registers.GLEDStatus;
                        break;
                    case LEDTypes.Red:
                        target = MBProtocol.Registers.RLEDStatus;
                        break;
                    case LEDTypes.White:
                        target = MBProtocol.Registers.WLEDStatus;
                        break;
                    default:
                        return false;
                }
                return Query(target, true);
            }

        }

        public bool SetLEDStatus(LEDTypes ledType, bool isOn)
        {
            MBProtocol.Registers target;
            if (isOn)
            {
                switch (ledType)
                {
                    case LEDTypes.Green:
                        if (_IsRLEDSetOn || _IsWLEDSetOn)
                        {
                            SetLEDStatus(LEDTypes.Red, false);
                            SetLEDStatus(LEDTypes.White, false);
                            _IsRLEDSetOn = false;
                            _IsWLEDSetOn = false;
                        }
                        else if (_IsGLEDSetOn)
                        {
                            return true;
                        }
                        break;
                    case LEDTypes.Red:
                        if (_IsGLEDSetOn || _IsWLEDSetOn)
                        {
                            SetLEDStatus(LEDTypes.Green, false);
                            SetLEDStatus(LEDTypes.White, false);
                            _IsGLEDSetOn = false;
                            _IsWLEDSetOn = false;
                        }
                        else if (_IsRLEDSetOn)
                        {
                            return true;
                        }
                        break;
                    case LEDTypes.White:
                        if (_IsRLEDSetOn || _IsGLEDSetOn)
                        {
                            SetLEDStatus(LEDTypes.Red, false);
                            SetLEDStatus(LEDTypes.Green, false);
                            _IsRLEDSetOn = false;
                            _IsGLEDSetOn = false;
                        }
                        else if (_IsWLEDSetOn)
                        {
                            return true;
                        }
                        break;
                }
            }
            lock (_LEDLock)
            {
                switch (ledType)
                {
                    case LEDTypes.Green:
                        target = MBProtocol.Registers.GLEDStatus;
                        _IsGLEDSetOn = isOn;
                        break;
                    case LEDTypes.Red:
                        target = MBProtocol.Registers.RLEDStatus;
                        _IsRLEDSetOn = isOn;
                        break;
                    case LEDTypes.White:
                        target = MBProtocol.Registers.WLEDStatus;
                        _IsWLEDSetOn = isOn;
                        break;
                    default:
                        return false;
                }
                return Setting(target, isOn ? 1 : 0, true);
            }
        }

        public bool GetOnOffStatus()
        {
            return Query(MBProtocol.Registers.OnOffInputs, true);
        }

        public bool GetChemiTemperCtrlStatus()
        {
            return Query(MBProtocol.Registers.ChemiTemperCtrlSwitch, true);
        }

        public bool SetChemiTemperCtrlStatus(bool isCtrlOn)
        {
            return Setting(MBProtocol.Registers.ChemiTemperCtrlSwitch, isCtrlOn ? 1 : 0, true);
        }

        public bool GetChemiTemperCtrlRamp()
        {
            return Query(MBProtocol.Registers.ChemiTemperCtrlRamp, true);
        }

        public bool SetChemiTemperCtrlRamp(double newRamp)
        {
            return Setting(MBProtocol.Registers.ChemiTemperCtrlRamp, (int)(newRamp * 1000.0), true);
        }

        public bool GetChemiTemperCtrlPower()
        {
            return Query(MBProtocol.Registers.ChemiTemperCtrlPower, true);
        }

        public bool GetChemiTemper()
        {
            return Query(MBProtocol.Registers.ChemiTemper, true);
        }

        public bool SetChemiTemper(double newTemper)
        {
            return Setting(MBProtocol.Registers.ChemiTemper, (int)(newTemper * 1000.0), true);
        }

        public bool GetCoolerTemper()
        {
            return Query(MBProtocol.Registers.CoolerTemper, true);
        }

        public bool SetCoolerTemper(double newTemper)
        {
            return Setting(MBProtocol.Registers.CoolerTemper, (int)(newTemper * 100.0), true);
        }

        public bool GetAmbientTemper()
        {
            return Query(MBProtocol.Registers.AmbientTemper, true);
        }

        public bool GetPDValue()
        {
            return Query(MBProtocol.Registers.PDValue, true);
        }
        public bool GetTemperCtrlParameters()
        {
            return Query(MBProtocol.Registers.TemperCtrlPro, 6, true);
        }
        public bool SetTemperCtrlParameters(int kp, int ki, int kd, int maxCrntPcnt)
        {
            int[] values = { kp, ki, kd };
            Setting(MBProtocol.Registers.TemperCtrlPower, maxCrntPcnt, true);
            return Setting(MBProtocol.Registers.TemperCtrlPro, values, true);
        }

        public bool SetCartridgeMotorStatus(bool enable)
        {
            return Setting(MBProtocol.Registers.PMotionEnable, enable ? 1 : 0, true);
        }
        public bool GetCartridgeMotorStatus()
        {
            return Query(MBProtocol.Registers.PMotionEnable, true);
        }
        public bool SetCartridgeMotorPower(int powerPercent)
        {
            return Setting(MBProtocol.Registers.PMotionPower, powerPercent, true);
        }


        public bool Query(MBProtocol.Registers module, bool waitForResponse)
        {
            if (!IsConnected) { return false; }
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
            {
                Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                return false;
            }
            byte[] request = MBProtocol.Query(module, 1);
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle))
            {
                if (_Comm.SendBytes(request))
                {
                    if (waitForResponse)
                    {
                        if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
                        {
                            Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                            return false;
                        }
                        else { return true; }
                    }
                    return true;
                }
                else { return false; }
            }
            else
            {
                Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                return false;
            }
        }
        public bool Query(MBProtocol.Registers module, byte numbers, bool waitForResponse)
        {
            if (!IsConnected) { return false; }
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
            {
                Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                return false;
            }

            byte[] request = MBProtocol.Query(module, numbers);
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle))
            {
                if (_Comm.SendBytes(request))
                {
                    if (waitForResponse)
                    {
                        if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
                        {
                            Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                            return false;
                        }
                        else { return true; }
                    }
                    return true;
                }
                else { return false; }
            }
            else
            {
                Logger.LogError(string.Format("Query waitForStatus expire, module:{0}", module));
                return false;
            }
        }
        public bool Setting(MBProtocol.Registers module, int targetValue, bool waitForResponse)
        {
            if (!IsConnected) { return false; }
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
            {
                Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                return false;
            }

            byte[] valBytes = new byte[4];
            byte[] request = MBProtocol.Setting(module, 1, BitConverter.GetBytes(targetValue));
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle))
            {
                if (_Comm.SendBytes(request))
                {
                    if (waitForResponse)
                    {
                        if( _Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
                        {
                            Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                            return false;
                        }
                        else { return true; }
                    }
                    return true;
                }
                else { return false; }
            }
            else
            {
                Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                return false;
            }
        }
        public bool Setting(MBProtocol.Registers module, int[] targetValues, bool waitForResponse)
        {
            if (!IsConnected) { return false; }
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
            {
                Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                return false;
            }
            byte[] valBytes = new byte[targetValues.Length * 4];
            for (int i = 0; i < targetValues.Length; i++)
            {
                var tmpBytes = BitConverter.GetBytes(targetValues[i]);
                for (int j = 0; j < 4; j++)
                {
                    valBytes[i * 4 + j] = tmpBytes[j];
                }
            }
            byte[] request = MBProtocol.Setting(module, (byte)targetValues.Length, valBytes);
            if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle))
            {
                if (_Comm.SendBytes(request))
                {
                    if (waitForResponse)
                    {
                        if (_Comm.WaitForStatus(500, SerialCommBase.CommStatus.Idle) == false)
                        {
                            Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                            return false;
                        }
                        else { return true; }
                    }
                    return true;
                }
                else { return false; }
            }
            else
            {
                Logger.LogError(string.Format("Setting waitForStatus expire, module:{0}", module));
                return false;
            }
        }
        #endregion Public Functions
    }
}
