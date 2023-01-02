using Hywire.MotionControl;
using Sequlite.ALF.Common;
using System;
using System.Diagnostics;
using System.Threading;

namespace Sequlite.ALF.MotionControl
{
    /// <summary>
    /// The MotionControl class provides high-level access to a collection of motion control devices. 
    /// There are two motion controllers in ALF 2.x:
    /// 1) The Magellan Controller (Dover Motion) controls z stage motion.
    /// 2) The Galileo Controller (Hywire) controls x/y stage, filter slider, and flow cell door motion.
    /// </summary>
    public class MotionController
    {
        // Events
        public delegate void RecordsUpdateHandler();
        public event RecordsUpdateHandler OnRecordsUpdated;

        public delegate bool DoorCtrlHandler(bool open);
        public event DoorCtrlHandler OnDoorCtrlRequested;


        // Private fields
        // The magellan controller is used for z stage motion
        private MagellanControl _magellan = new MagellanControl();

        //unused old Galileo controller code. Contains a data structure with commands that is still used?
        //private GalilControl _GalilController = new GalilControl();

        // v 1.x ALFs use a galileo motion controller for all other motion
        private HywireGalilControl _hywireGalileo = new HywireGalilControl();

        // v2.x ALFs use a custom hywire motion controller for all other motion
        private HywireController _hywireMotion = new HywireController();
        
        private bool _isMotionConnected;

        private System.Timers.Timer _RecordTimer;

        ISeqLog Logger = SeqLogFactory.GetSeqFileLog("MOTION"); //< get a logger for this class

        private static object _instanceCreationLocker = new object(); //< locker used during instance creation


        private int _XCurrentPos;
        private int _YCurrentPos;

        private int _XCurrentPosBeforeMoving;
        private int _XCurrentPosAfterMoving;
        private int _XEncoderPosBeforeMoving;
        private int _XEncoderPosAfterMoving;
        private int _YCurrentPosBeforeMoving;
        private int _YCurrentPosAfterMoving;
        private int _YEncoderPosBeforeMoving;
        private int _YEncoderPosAfterMoving;

        private bool _IsFCDoorInitialized = false;

        // unused fields ///
        private bool _isMoving; //<
        private bool _XFirstTimeToHome = true;
        private bool _YFirstTimeToHome = true;

        // public fields        
        public bool IsFluidicCheckEnabled { get; set; } = true;
        public bool IsReagentDoorEnabled { get; set; } = true;
        public bool IsRFIDReaderEnabled { get; set; } = true;
        public bool IsBarcodeReaderEnabled { get; set; } = true;
        public bool IsFcDoorSensorEnabled { get; set; } = true; //< flag to enable or disable sensor for FC door
        public bool UsingControllerV2 { get; set; } // set to true for 2.x machines
        public bool IsMotionConnected
        {
            get { return _isMotionConnected; }
        }
        public bool IsZStageConnected
        {
            get { return _magellan != null ? _magellan.IsConnected : false; }
        }
        //public bool IsConnected
        //{
        //    get
        //    {
        //        return _IsGalilConnected & _IsMagellanConnected;
        //    }
        //}
        public int FCurrentPos // why is the F, not X??
        {
            get
            {
                if (UsingControllerV2)
                {
                    return _hywireMotion.CurrentPositions[MotorTypes.Motor_X];
                }
                else
                {
                    return _XCurrentPos;
                }
            }
            set
            {
                _XCurrentPos = value;
            }
        }
        public int YCurrentPos
        {
            get
            {
                if (UsingControllerV2)
                {
                    return _hywireMotion.CurrentPositions[MotorTypes.Motor_Y];
                }
                else
                {
                    return _YCurrentPos;
                }
            }
            set
            {
                _YCurrentPos = value;
            }
        }
        public int XEncoderPos
        {
            get { return _hywireMotion.EncoderPositions[MotorTypes.Motor_X]; }
        }
        public int YEncoderPos
        {
            get { return _hywireMotion.EncoderPositions[MotorTypes.Motor_Y]; }
        }
        /// <summary>
        /// Z current position units = [um]
        /// </summary>
        public double ZCurrentPos { get; private set; }
        public int CCurrentPos { get; private set; }
        public bool IsFAtHome { get; private set; }
        public bool IsYAtHome { get; private set; }
        public bool IsZAtHome { get; private set; }
        public bool IsFAtLimit { get; private set; }
        public bool IsYAtLimit { get; private set; }
        public bool IsZAtLimit { get; private set; }

        // TODO: this class should not expose private members of the motion controller class!
        public HywireController HywireMotionController
        {
            get { return _hywireMotion; }
        }
        public bool IsFCDoorAvailable { get; set; }
        public bool IsCartridgeAvailable { get; set; }
        public bool IsFCDoorController { get => IsFCDoorAvailable && !IsCartridgeAvailable; }
        public bool? FCDoorIsOpen
        {
            get
            {
                if (_hywireMotion.MotionStates[MotorTypes.Motor_Z].IsAtFwdLimit ||
                    _hywireMotion.CurrentPositions[MotorTypes.Motor_Z] / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor] == SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitHigh)
                {
                    return true;
                }
                else if (_hywireMotion.MotionStates[MotorTypes.Motor_Z].IsAtHome ||
                    _hywireMotion.CurrentPositions[MotorTypes.Motor_Z] / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor] == SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitLow)
                {
                    return false;
                }
                else
                {
                    return null;
                }
            }
        }

        private static MotionController _MotionController = null;
        public static MotionController GetInstance()
        {
            if (_MotionController == null)
            {
                lock (_instanceCreationLocker)
                {
                    if (_MotionController == null)
                    {
                        _MotionController = new MotionController();
                    }

                }
            }
            return _MotionController;
        }


        // singleton constructor
        private MotionController()
        {
            Logger.Log("Creating MotionController object...");
            _RecordTimer = new System.Timers.Timer(1500);
            _RecordTimer.AutoReset = true;
            _RecordTimer.Elapsed += _RecordTimer_Elapsed;
        }


        /// <summary>
        /// Wait movement finish function + refresh current position, fire event to update pos
        /// seperated from control function
        /// </summary>
        /// <param name="motionType">The axis to wait for (X, Y, Z, F, FCDoor)</param>
        /// <param name="targetPosition"></param>
        /// <param name="timeoutMs">The number of milliseconds to wait until an error is detected. Set to -1 to never timeout. 
        /// If retryCount is also set, an exception will not be thrown until timeoutMs * retryCount milliseconds have elapsed</param>
        private void WaitMovement(in MotionTypes motionType, in double targetPosition, in double speed = 1, in double accel = 1, in int pollingIntervalMs = 10, in int retryCount = 3, in int timeoutMs = 10000)
        {
            try
            {
                int tryCount = 0; // TODO: Use to define number of retries (like z stage logic), not time interval (like other axis control logic)
                //time limit will be same, try count different because controller refresh rate lower for ALF1.0
                //Alf 1.0s use Galil Controller
                int trycountlimit = UsingControllerV2 ? 1200 : 120;
                switch (motionType)
                {
                    case MotionTypes.Filter:
                        do
                        {
                            if (tryCount++ > trycountlimit)
                            {
                                Logger.LogError("Failed to Move Filer, waiting expire.");
                                throw new Exception("Filter movement failed");
                            }
                            if (UsingControllerV2)
                            {
                                Thread.Sleep(50);
                                HywireMotionController.GetMotionInfo(MotorTypes.Motor_X);
                                FCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_X];
                            }
                            else
                            {
                                Thread.Sleep(500);
                                var records = _hywireGalileo.Record();
                                if (records != null)
                                {
                                    FCurrentPos = (int)_hywireGalileo.SourceValue(records, "_RPA");
                                }
                            }
                            OnRecordsUpdated?.Invoke();
                        }
                        while (FCurrentPos != targetPosition);
                        break;
                    case MotionTypes.XStage:
                        do
                        {
                            if (tryCount++ > trycountlimit)
                            {
                                Logger.LogError(string.Format("Failed to Move X-stage, waiting expire. Target Pos: {0}; Crnt Pos: {1}; Encoder Pos:{2}",
                                    targetPosition, _hywireMotion.CurrentPositions[MotorTypes.Motor_X], _hywireMotion.EncoderPositions[MotorTypes.Motor_X]));
                                throw new Exception("X-stage movement failed");
                            }
                            if (UsingControllerV2)
                            {
                                Thread.Sleep(50);
                                HywireMotionController.GetMotionInfo(MotorTypes.Motor_X);
                                FCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_X];
                            }
                            else
                            {
                                Thread.Sleep(500);
                                var records = _hywireGalileo.Record();
                                if (records != null)
                                {
                                    FCurrentPos = (int)_hywireGalileo.SourceValue(records, "_RPA");
                                }
                            }
                            OnRecordsUpdated?.Invoke();
                        }
                        while (FCurrentPos != targetPosition || (UsingControllerV2 ? _hywireMotion.MotionStates[MotorTypes.Motor_X].IsBusy : false));
                        if (UsingControllerV2)
                        {
                            _XCurrentPosAfterMoving = HywireMotionController.CurrentPositions[MotorTypes.Motor_X];
                            _XEncoderPosAfterMoving = HywireMotionController.EncoderPositions[MotorTypes.Motor_X];
                            int detaCrntPos = _XCurrentPosAfterMoving - _XCurrentPosBeforeMoving;
                            int detaEncoderPos = _XEncoderPosAfterMoving - _XEncoderPosBeforeMoving;
                            int tryCounts = 0;
                            while (Math.Abs(detaCrntPos - detaEncoderPos) > 100)
                            {
                                if (tryCounts++ > 20)
                                {
                                    throw new Exception(string.Format("{0} moving verification failed", motionType.ToString()));
                                }
                                Thread.Sleep(5);
                                HywireMotionController.GetMotionInfo(MotorTypes.Motor_X);
                                _XCurrentPosAfterMoving = HywireMotionController.CurrentPositions[MotorTypes.Motor_X];
                                _XEncoderPosAfterMoving = HywireMotionController.EncoderPositions[MotorTypes.Motor_X];
                                detaCrntPos = _XCurrentPosAfterMoving - _XCurrentPosBeforeMoving;
                                detaEncoderPos = _XEncoderPosAfterMoving - _XEncoderPosBeforeMoving;
                            }
                        }
                        break;
                    case MotionTypes.YStage:
                        do
                        {
                            if (tryCount++ > trycountlimit)
                            {
                                Logger.LogError(string.Format("Failed to Move Y-stage, waiting expire.Target Pos: {0}; Crnt Pos: {1}; Encoder Pos:{2}",
                                    targetPosition, _hywireMotion.CurrentPositions[MotorTypes.Motor_Y], _hywireMotion.EncoderPositions[MotorTypes.Motor_Y]));
                                throw new Exception("Y-stage movement failed");
                            }
                            if (UsingControllerV2)
                            {
                                Thread.Sleep(50);
                                HywireMotionController.GetMotionInfo(MotorTypes.Motor_Y);
                                YCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_Y];
                            }
                            else
                            {
                                Thread.Sleep(500);
                                var records = _hywireGalileo.Record();
                                if (records != null)
                                {
                                    YCurrentPos = (int)_hywireGalileo.SourceValue(records, "_RPB");
                                }
                            }
                            OnRecordsUpdated?.Invoke();
                        }
                        while (YCurrentPos != targetPosition || (UsingControllerV2 ? _hywireMotion.MotionStates[MotorTypes.Motor_Y].IsBusy : false));
                        if (UsingControllerV2)
                        {
                            _YCurrentPosAfterMoving = HywireMotionController.CurrentPositions[MotorTypes.Motor_Y];
                            _YEncoderPosAfterMoving = HywireMotionController.EncoderPositions[MotorTypes.Motor_Y];
                            int detaCrntPos = _YCurrentPosAfterMoving - _YCurrentPosBeforeMoving;
                            int detaEncoderPos = _YEncoderPosAfterMoving - _YEncoderPosBeforeMoving;
                            if (Math.Abs(detaCrntPos / SettingsManager.ConfigSettings.MotionFactors[motionType] - detaEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[motionType]) > 0.01)
                            {
                                throw new Exception(string.Format("{0} moving verification failed", motionType.ToString()));
                            }

                            if (IsFCDoorController)
                            {
                                if ((IsYAtHome || YCurrentPos == 0) && FCDoorIsOpen != false)
                                {
#if !DisableFCDoor
                                    SetFCDoorStatus(false);
#endif
                                }
                            }
                        }
                        break;
                    case MotionTypes.FCDoor:
                    case MotionTypes.Cartridge:
                        do
                        {
                            if (tryCount++ > trycountlimit)
                            {
                                Logger.LogError("Failed to Move Cartridge, waiting expire.");
                                throw new Exception("Cartridge movement failed");
                            }
                            if (UsingControllerV2)
                            {
                                Thread.Sleep(50);
                                HywireMotionController.GetMotionInfo(MotorTypes.Motor_Z);
                                CCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_Z];
                            }
                            else
                            {
                                Thread.Sleep(500);
                                var records = _hywireGalileo.Record();
                                if (records != null)
                                {
                                    CCurrentPos = (int)_hywireGalileo.SourceValue(records, "_RPC");
                                }
                            }
                            OnRecordsUpdated?.Invoke();
                        }
                        while (CCurrentPos != targetPosition || (UsingControllerV2 ? _hywireMotion.MotionStates[MotorTypes.Motor_Z].IsBusy : false));
                        if (UsingControllerV2 && motionType == MotionTypes.FCDoor)
                        {
                            _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_Z, new MotionDriveCurrent[] { MotionDriveCurrent.Percent50 });
                        }
                        break;
                    case MotionTypes.ZStage:
                        // read in the current position
                        double currentPosition = 0;
                        _magellan.ReadActualPos(ref currentPosition);
                        Stopwatch w = new Stopwatch();
                        w.Start();
                        while (Math.Abs(currentPosition - targetPosition) > 0.1)
                        {
                            if(w.ElapsedMilliseconds < timeoutMs)
                            {
                                Thread.Sleep(pollingIntervalMs);
                                _magellan.ReadActualPos(ref currentPosition);
                            }
                            else if(tryCount < retryCount)
                            {
                                Logger.LogError($"Z-stage movement timed out after {timeoutMs} ms");
                                // try to reconnect 3 times with a delay of 1s and cooldown of 10s
                                // delay and cooldown increases with each reconnect attempt
                                ZStageReconnect(3, tryCount * 1000, tryCount * 10000);
                                ++tryCount;
                                // resent z stage movement command
                                bool result = _magellan.SetNewAcceleration(accel);
                                result &= _magellan.SetNewDeceleration(accel);
                                result &= _magellan.SetNewVelocity(speed);
                                result &= _magellan.SetNewPosition(targetPosition, true);
                                if(result)
                                {
                                    w.Restart();
                                }
                                else
                                {
                                    Thread.Sleep(tryCount * 15000); // ... additional 15 second cooldown
                                }
                            }
                            else
                            {
                                Logger.LogError("Failed to Move Z-stage. Wait Movement timeout.");
                                throw new Exception("Z-stage movement failed");
                            }
                        }

                        ZCurrentPos = currentPosition;
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return;
            }
        }

        /// <summary>
        /// keep refresh Z stage pos no matter Z stage is moving or not
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _RecordTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (IsZStageConnected)
                {
                    double zPos = 0;
                    _magellan.ReadActualPos(ref zPos);
                    ZCurrentPos = zPos;
                }
                OnRecordsUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return;
            }
        }

        /// <summary>
        /// Disconnects Z stage controller and attempts to reconnect.
        /// If reconnection is successful, attempts to set movement parameters and execute the last movement comm
        /// </summary>
        /// <param name="tryCount">The number of tries to attempt reconnection before giving up and throwing an exception</param>
        /// <param name="delayMs">The time to wait in between reconnection attempts</param>
        /// <param name="coolDownMs">The time to wait before attempting to reconnect after disconnecting from the controller</param>
        /// <returns></returns>
        private bool ZStageReconnect(in int tryCount = 3, in int delayMs = 1000, in int coolDownMs = 10000)
        {
            string message = $"Attempting to reconnect to Z stage controller...";
            Logger.Log(message);
            // first disconnect and release resources
            _magellan.Dispose();

            message = $"Waiting {coolDownMs} ms before reconnecting...";
            Thread.Sleep(coolDownMs);

            // try to reconnect to the z stage
            int tries = 0;
            do
            {
                message = $"Attempting Z stage controller reconnection. Attempt number: {tries + 1}...";
                Logger.Log(message);
                ZStageConnect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.ZStage].PortName);
                tries++;
                if (IsZStageConnected)
                {
                    message = $"Z stage controller reconnection succeeded after {tries} attempts.";
                    Logger.Log(message);
                    return true;
                }
                Thread.Sleep(delayMs);
            }
            while (!IsZStageConnected && tries < tryCount);

            message = $"Z stage controller reconnection failed after {tries} attempts.";
            Logger.LogError(message);
            return false;
        }


        #region Public Functions
        public void ZStageConnect()
        {
            _magellan.Connect();
            if (_magellan.IsConnected && _RecordTimer.Enabled == false)
            {
                _RecordTimer.Start();
            }
        }
        public void ZStageConnect(string portName)
        {
            _magellan.Connect(portName);
            if (_magellan.IsConnected && _RecordTimer.Enabled == false)
            {
                _RecordTimer.Start();
            }
        }

        public void OtherStagesConnect()
        {
            if (UsingControllerV2)
            {
                _isMotionConnected = _hywireMotion.Connect();
                if (_isMotionConnected)
                {
                    // 1. set signal polarity
                    MotionSignalPolarity polar = new MotionSignalPolarity()
                    {
                        ClkPolar = true,
                        DirPolar = true,
                        FwdLmtPolar = true,
                        HomePolar = true,
                    };
                    MotionSignalPolarity polarX = new MotionSignalPolarity()
                    {
                        ClkPolar = true,
                        FwdLmtPolar = true,
                        HomePolar = true,
                    };
                    _hywireMotion.SetMotionPolarities(polarX, polar, polar, polar);

                    // 2. set torque & mode
                    _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W,
                        new MotionDriveCurrent[] { MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100 });
                    _hywireMotion.SetMotionDriveMode(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W,
                        new MotionDriveMode[] { MotionDriveMode.Divide16, MotionDriveMode.Divide8, IsFCDoorController ? MotionDriveMode.Divide8 : MotionDriveMode.Divide16, MotionDriveMode.Divide16 });

                    // 3. home Filter(X), Y stage(Y), Cartridge(Z)
                    int[] startSpeeds = new int[] { 10, 10, 10 };
                    int[] topSpeeds = new int[3];
                    int[] accVals = new int[3];
                    //if (!UsingHywireController)
                    //{
                    //    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]);
                    //    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]);
                    //}
                    //else
                    //{
                    //    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    //    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    //}
                    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    topSpeeds[1] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    if (IsFCDoorController)
                    {
                        topSpeeds[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.FCDoor].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]);
                    }
                    else
                    {
                        topSpeeds[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    }
                    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    accVals[1] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    if (IsFCDoorController)
                    {
                        accVals[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.FCDoor].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]);
                    }
                    else
                    {
                        accVals[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    }

                    // since X is allowed to move to negative positions, so we move X forward to positive position before home action
                    _hywireMotion.GetMotionInfo(MotorTypes.Motor_X);
                    // move 2 mm forward
                    var tgtPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_X] + 2 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage];
                    _hywireMotion.AbsoluteMove(MotorTypes.Motor_X, startSpeeds[0], topSpeeds[0], accVals[0], accVals[0], (int)tgtPos, true, true);
                    MotorTypes motorTypes;
                    motorTypes = IsCartridgeAvailable ? MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z : MotorTypes.Motor_X | MotorTypes.Motor_Y;
                    _hywireMotion.HomeMotions(motorTypes, startSpeeds, topSpeeds, accVals, accVals);

                    Thread waitXmove = new Thread(e => WaitMovement(MotionTypes.XStage, 0));
                    Thread waitYmove = new Thread(e => WaitMovement(MotionTypes.YStage, 0));
                    waitXmove.Start();
                    waitYmove.Start();

                    if (IsCartridgeAvailable)
                    {
                        Thread waitZmove = new Thread(e => WaitMovement(MotionTypes.Cartridge, 0));
                        waitZmove.Start();
                    }
                }
            }
            else
            {
                _isMotionConnected = _hywireGalileo.Connect();
                Thread waitYmove = new Thread(e => WaitMovement(MotionTypes.YStage, 0));
                Thread waitFmove = new Thread(e => WaitMovement(MotionTypes.Filter, 0));
                waitYmove.Start();
                waitFmove.Start();
            }

        }

        public void OtherStagesConnect(string portName)
        {
            if (UsingControllerV2)
            {
                _isMotionConnected = _hywireMotion.Connect(portName);
                if (_isMotionConnected)
                {
                    // 1. set signal polarity
                    MotionSignalPolarity polar = new MotionSignalPolarity()
                    {
                        ClkPolar = true,
                        DirPolar = true,
                        FwdLmtPolar = true,
                        HomePolar = true,
                    };
                    MotionSignalPolarity polarX = new MotionSignalPolarity()
                    {
                        ClkPolar = true,
                        FwdLmtPolar = true,
                        HomePolar = true,
                    };
                    _hywireMotion.SetMotionPolarities(polarX, polar, polar, polar);

                    // 2. set torque & mode
                    _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W,
                        new MotionDriveCurrent[] { MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100, MotionDriveCurrent.Percent100 });
                    _hywireMotion.SetMotionDriveMode(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W,
                        new MotionDriveMode[] { MotionDriveMode.Divide16, MotionDriveMode.Divide8, MotionDriveMode.Divide16, MotionDriveMode.Divide16 });

                    // stop all motions
                    _hywireMotion.SetStart(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W, new bool[] { false, false, false, false });
                    bool motionsStopped;
                    do
                    {
                        motionsStopped = !_hywireMotion.MotionStates[MotorTypes.Motor_X].IsBusy;
                        motionsStopped &= !_hywireMotion.MotionStates[MotorTypes.Motor_Y].IsBusy;
                        motionsStopped &= !_hywireMotion.MotionStates[MotorTypes.Motor_Z].IsBusy;
                        motionsStopped &= !_hywireMotion.MotionStates[MotorTypes.Motor_W].IsBusy;
                        if (!motionsStopped)
                        {
                            Thread.Sleep(500);
                        }
                    }
                    while (!motionsStopped);

                    // set enable
                    _hywireMotion.SetEnable(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z, new bool[] { true, true, true });

                    // 3. home Filter(X), Y stage(Y), Cartridge(Z)
                    int[] startSpeeds = new int[] { 10, 10, 10 };
                    int[] topSpeeds = new int[3];
                    int[] accVals = new int[3];
                    //if (!UsingHywireController)
                    //{
                    //    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]);
                    //    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]);
                    //}
                    //else
                    //{
                    //    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    //    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    //}
                    //topSpeeds[1] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    //topSpeeds[2] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    //accVals[1] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    //accVals[2] = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);

                    topSpeeds[0] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    topSpeeds[1] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    topSpeeds[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    accVals[0] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    accVals[1] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    accVals[2] = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);

                    // since X is allowed to move to negative positions, so we move X forward to positive position before home action
                    _hywireMotion.GetMotionInfo(MotorTypes.Motor_X);
                    // move 2 mm forward
                    var tgtPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_X] + 2 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage];
                    _hywireMotion.AbsoluteMove(MotorTypes.Motor_X, startSpeeds[0], topSpeeds[0], accVals[0], accVals[0], (int)tgtPos, true, true);

                    MotorTypes motorTypes;
                    motorTypes = IsCartridgeAvailable ? MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z : MotorTypes.Motor_X | MotorTypes.Motor_Y;
                    _hywireMotion.HomeMotions(motorTypes, startSpeeds, topSpeeds, accVals, accVals);
                    //_HywireMotionController.HomeMotions(MotorTypes.Motor_X | MotorTypes.Motor_Y , startSpeeds, topSpeeds, accVals, accVals);
                    Thread waitXmove = new Thread(e => WaitMovement(MotionTypes.XStage, 0));
                    Thread waitYmove = new Thread(e => WaitMovement(MotionTypes.YStage, 0));
                    waitXmove.Start();
                    waitYmove.Start();

                    if (IsCartridgeAvailable)
                    {
                        Thread waitZmove = new Thread(e => WaitMovement(MotionTypes.Cartridge, 0));
                        waitZmove.Start();
                    }

                    Thread resetXEncoderPos = new Thread(() =>
                    {
                        int waitCnts = 0;
                        while (!_hywireMotion.MotionStates[MotorTypes.Motor_X].IsAtHome)
                        {
                            if (waitCnts++ > 30)
                            {
                                Logger.Log("Reset X encoder position at launching time failed, wait for at home timeout");
                                return;
                            }
                            Thread.Sleep(1000);
                        }
                        Thread.Sleep(5000); // wait for the stage stop (for X stage the waiting time should be set longer than that of Y stage, since X is a closed loop system)
                        _hywireMotion.ResetEncoderPosition(MotorTypes.Motor_X);
                    });
                    resetXEncoderPos.Start();

                    Thread resetYEncoderPos = new Thread(() =>
                    {
                        int waitCnts = 0;
                        while (!_hywireMotion.MotionStates[MotorTypes.Motor_Y].IsAtHome)
                        {
                            if (waitCnts++ > 30)
                            {
                                Logger.Log("Reset Y encoder position at launching time failed, wait for at home timeout");
                                return;
                            }
                            Thread.Sleep(1000);
                        }
                        Thread.Sleep(500); // wait for the stage stop
                        _hywireMotion.ResetEncoderPosition(MotorTypes.Motor_Y);
                    });
                    resetYEncoderPos.Start();
                }
            }
            else
            {
                _isMotionConnected = _hywireGalileo.Connect(portName);
                Thread waitYmove = new Thread(e => WaitMovement(MotionTypes.YStage, 0));
                Thread waitFmove = new Thread(e => WaitMovement(MotionTypes.Filter, 0));
                waitYmove.Start();
                waitFmove.Start();
            }
            //if (IsGalilConnected && _RecordTimer.Enabled == false)
            //{
            //    _RecordTimer.Start();
            //}
        }

        public void Reconnect()
        {
            if (UsingControllerV2) { _hywireMotion.Reconnect(); }
            else { _hywireGalileo.Reconnect(); }
        }


        public bool SendCommand(string cmd)
        {
            return _hywireGalileo.SendCommand(cmd);
        }

        public bool HomeMotion(MotionTypes type, int speed, int accel, bool wait)
        {
            Logger.Log(string.Format("Motion Controller home move {0}, speed:{1}, accel:{2}, wait:{3}", type, speed, accel, wait));
            if (!IsMotionConnected)
            {
                return false;
            }
            try
            {
                _isMoving = true;
                if (type == MotionTypes.None)
                {
                    return false;
                }
                if (UsingControllerV2)
                {
                    MotorTypes motorType = MotorTypes.Motor_X;
                    switch (type)
                    {
                        case MotionTypes.Filter:
                            motorType = MotorTypes.Motor_X;
                            break;
                        case MotionTypes.YStage:
                            motorType = MotorTypes.Motor_Y;
                            break;
                        case MotionTypes.Cartridge:
                            motorType = MotorTypes.Motor_Z;
                            break;
                        case MotionTypes.XStage:
                            motorType = MotorTypes.Motor_X;
                            break;
                        case MotionTypes.FCDoor:
                            motorType = MotorTypes.Motor_Z;
                            break;
                    }
                    bool issuccess = _hywireMotion.HomeMotion(motorType, new int[] { 256 }, new int[] { speed }, new int[] { accel }, new int[] { accel }, wait);
                    if (wait)
                    {
                        WaitMovement(type, 0);
                    }
                    else
                    {
                        Thread waitmove = new Thread(e => WaitMovement(type, 0));
                        waitmove.Start();
                    }
                    return issuccess;
                }
                else
                {
                    string motionName = string.Empty;
                    switch (type)
                    {
                        case MotionTypes.Filter:
                            motionName = "X";
                            break;
                        case MotionTypes.YStage:
                            motionName = "Y";
                            break;
                        case MotionTypes.Cartridge:
                            motionName = "Z";
                            break;
                        case MotionTypes.XStage:
                            motionName = "X";
                            break;
                        default:
                            return false;
                    }
                    SendCommand(GalilControl.Commands[GalilCommandSet.HaltThread]);
                    SendCommand(GalilControl.Commands[GalilCommandSet.Stop] + motionName);
                    SendCommand(GalilControl.Commands[GalilCommandSet.ServoHere] + motionName);
                    SendCommand(GalilControl.Commands[GalilCommandSet.Speed] + string.Format("{0}={1}", motionName, speed));
                    SendCommand(GalilControl.Commands[GalilCommandSet.Acceleration] + string.Format("{0}={1}", motionName, accel));
                    SendCommand(GalilControl.Commands[GalilCommandSet.SwitchDeceleration] + string.Format("{0}={1}", motionName, accel));
                    SendCommand(GalilControl.Commands[GalilCommandSet.Home] + motionName);
                    SendCommand(GalilControl.Commands[GalilCommandSet.Begin] + motionName);
                    if (wait)
                    {
                        WaitMovement(type, 0);
                    }
                    else
                    {
                        Thread waitmove = new Thread(e => WaitMovement(type, 0));
                        waitmove.Start();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
                return false;
            }
            finally
            {
                _isMoving = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="speed">unit: um/sec</param>
        /// <param name="accel">unit: um/sec^2</param>
        /// <param name="wait"></param>
        /// <returns>isSucceed</returns>
        public bool HomeZstage()
        {
            if (!IsZStageConnected)
            {
                return false;
            }
            try
            {
                bool result;
                result = _magellan.Home();
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }
        /// <summary>
        /// Absolute move 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pos"></param>
        /// <param name="speed"></param>
        /// <param name="accel"></param>
        /// <param name="wait"> if don't wait and not inside reciep, start a seperate thread to refresh pos</param>
        /// <param name="isV2Recipe"> if is inside a recipe, do not start a different thread to refresh the position </param>
        /// <returns></returns>
        public bool AbsoluteMoveEx(MotionTypes type, int pos, int speed, int accel, bool wait = false, bool isV2Recipe = false)
        {
            Logger.Log(string.Format("Start Motion Controller abs move {0}, pos:{1}, speed:{2}, accel:{3}, wait:{4}", type, pos, speed, accel, wait));
            if (!IsMotionConnected) { return false; }
            if (type == MotionTypes.None) { return false; }
            try
            {
                _isMoving = true;
                if (UsingControllerV2)
                {
                    MotorTypes motorType = MotorTypes.Motor_X;
                    switch (type)
                    {
                        case MotionTypes.Filter:
                            motorType = MotorTypes.Motor_X;
                            break;
                        case MotionTypes.YStage:
                            motorType = MotorTypes.Motor_Y;
                            _YCurrentPosBeforeMoving = HywireMotionController.CurrentPositions[motorType];
                            _YEncoderPosBeforeMoving = HywireMotionController.EncoderPositions[motorType];
                            // open the door before moving Y stage if target position is larger than 30 mm
                            if (pos > 30 * SettingsManager.ConfigSettings.MotionFactors[type] && IsFCDoorAvailable)
                            {
#if !DisableFCDoor
                                if (OnDoorCtrlRequested?.Invoke(true) == false)
                                {
                                    return false;
                                }
#endif
                            }
                            break;
                        case MotionTypes.Cartridge:
                            motorType = MotorTypes.Motor_Z;
                            break;
                        case MotionTypes.XStage:
                            motorType = MotorTypes.Motor_X;
                            _XCurrentPosBeforeMoving = HywireMotionController.CurrentPositions[motorType];
                            _XEncoderPosBeforeMoving = HywireMotionController.EncoderPositions[motorType];
                            break;
                        case MotionTypes.FCDoor:
                            motorType = MotorTypes.Motor_Z;
                            _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_Z, new MotionDriveCurrent[] { MotionDriveCurrent.Percent100 });
                            break;
                        default:
                            return false;
                    }
                    if (_hywireMotion.AbsoluteMove(motorType, 256, speed, accel, accel, pos, true, wait))
                    {
                        if (wait)
                        {
                            //WaitMovement(type, pos);
                            switch (motorType)
                            {
                                case MotorTypes.Motor_X:
                                    FCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_X];
                                    break;
                                case MotorTypes.Motor_Y:
                                    YCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_Y];
                                    break;
                                case MotorTypes.Motor_Z:
                                    CCurrentPos = _hywireMotion.CurrentPositions[MotorTypes.Motor_Z];
                                    break;
                            }

                            if (motorType == MotorTypes.Motor_X)
                            {
                                _XCurrentPosAfterMoving = HywireMotionController.CurrentPositions[motorType];
                                _XEncoderPosAfterMoving = HywireMotionController.EncoderPositions[motorType];
                                int detaCrntPos = _XCurrentPosAfterMoving - _XCurrentPosBeforeMoving;
                                int detaEncoderPos = _XEncoderPosAfterMoving - _XEncoderPosBeforeMoving;
                                int tryCounts = 0;
                                while (Math.Abs(detaCrntPos - detaEncoderPos) > 100)
                                {
                                    if (tryCounts++ > 20)
                                    {
                                        throw new Exception(string.Format("{0} moving verification failed", type.ToString()));
                                    }
                                    Thread.Sleep(5);
                                    HywireMotionController.GetMotionInfo(MotorTypes.Motor_X);
                                    _XCurrentPosAfterMoving = HywireMotionController.CurrentPositions[MotorTypes.Motor_X];
                                    _XEncoderPosAfterMoving = HywireMotionController.EncoderPositions[MotorTypes.Motor_X];
                                    detaCrntPos = _XCurrentPosAfterMoving - _XCurrentPosBeforeMoving;
                                    detaEncoderPos = _XEncoderPosAfterMoving - _XEncoderPosBeforeMoving;
                                }
                            }
                            else if (motorType == MotorTypes.Motor_Y)
                            {
                                _YCurrentPosAfterMoving = HywireMotionController.CurrentPositions[motorType];
                                _YEncoderPosAfterMoving = HywireMotionController.EncoderPositions[motorType];
                                int detaCrntPos = _YCurrentPosAfterMoving - _YCurrentPosBeforeMoving;
                                int detaEncoderPos = _YEncoderPosAfterMoving - _YEncoderPosBeforeMoving;
                                if (Math.Abs(detaCrntPos / SettingsManager.ConfigSettings.MotionFactors[type] - detaEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[type]) > 0.01)
                                {
                                    throw new Exception(string.Format("{0} moving verification failed", type.ToString()));
                                }

                                if (IsFCDoorController)
                                {
                                    if ((IsYAtHome || YCurrentPos == 0) && FCDoorIsOpen != false)
                                    {
#if !DisableFCDoor
                                        SetFCDoorStatus(false);
#endif
                                    }
                                }
                            }
                            else if (motorType == MotorTypes.Motor_Z)
                            {

                            }
                        }
                        else if (!wait && !isV2Recipe)
                        {
                            Thread waitmove = new Thread(e => WaitMovement(type, pos));
                            waitmove.Start();
                        }
                        Logger.Log(string.Format("Finished Motion Controller abs move {0}, pos:{1}, speed:{2}, accel:{3}, wait:{4}", type, pos, speed, accel, wait));
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    string motionName = string.Empty;
                    switch (type)
                    {
                        case MotionTypes.Filter:
                            motionName = "X";
                            break;
                        case MotionTypes.YStage:
                            motionName = "Y";
                            break;
                        case MotionTypes.Cartridge:
                            motionName = "Z";
                            break;
                        case MotionTypes.XStage:
                            motionName = "X";
                            break;
                        default:
                            return false;
                    }
                    SendCommand(GalilControl.Commands[GalilCommandSet.HaltThread]);
                    SendCommand(GalilControl.Commands[GalilCommandSet.Stop] + motionName);
                    SendCommand(GalilControl.Commands[GalilCommandSet.ServoHere] + motionName);
                    SendCommand(GalilControl.Commands[GalilCommandSet.Speed] + string.Format("{0}={1}", motionName, speed));
                    SendCommand(GalilControl.Commands[GalilCommandSet.Acceleration] + string.Format("{0}={1}", motionName, accel));
                    SendCommand(GalilControl.Commands[GalilCommandSet.Deceleration] + string.Format("{0}={1}", motionName, accel));
                    SendCommand(GalilControl.Commands[GalilCommandSet.PositionAbsolute] + string.Format("{0}={1}", motionName, pos));
                    SendCommand(GalilControl.Commands[GalilCommandSet.Begin] + motionName);
                    if (wait)
                    {
                        WaitMovement(type, pos);
                    }
                    else
                    {
                        Thread waitmove = new Thread(e => WaitMovement(type, pos));
                        waitmove.Start();
                    }
                    Logger.Log(string.Format("Finished Motion Controller abs move {0}, pos:{1}, speed:{2}, accel:{3}, wait:{4}", type, pos, speed, accel, wait));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
            finally
            {
                _isMoving = false;
            }
        }
        /// <summary>
        /// Absolute move with retry
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pos"></param>
        /// <param name="speed"></param>
        /// <param name="accel"></param>
        /// <param name="wait"></param>
        /// <param name="isV2Recipe"></param>
        /// <returns></returns>
        public bool AbsoluteMove(MotionTypes type, int pos, int speed, int accel, bool wait = false, bool isV2Recipe = false)
        {
            int max_tries = 4;
            int sleep_ms = 1000;
            int trycount = 0;
            bool failed = true;
            while (failed)
            {
                trycount++;
                if (trycount > max_tries)
                {
                    Logger.LogError($"Exceed max trycount, Failed to move-{type}");
                    return false;
                }
                try
                {
                    if (trycount > 2)
                    {
                        Reconnect();
                        Logger.LogError("Trycount larger than 2, reconnect");
                    }
                    failed = !AbsoluteMoveEx(type, pos, speed, accel, wait, isV2Recipe);
                    if (failed)
                    {
                        Logger.LogError($"Move {type} stage failed, trycount-{trycount}, retry");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Move {type} stage failed, trycount-{trycount}, Exception:{ex} retry");
                    Thread.Sleep(sleep_ms);
                }
            }
            return !failed;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos">unit: um</param>
        /// <param name="speed">unit: um/sec</param>
        /// <param name="accel">unit: um/sec^2</param>
        /// <param name="wait"></param>
        /// <returns></returns>
        public bool AbsoluteMoveZStage(double pos, double speed, double accel, bool wait = false)
        {
            Logger.Log($"Starting absolute move... Type: Z-stage, pos:{pos}, speed:{speed}, accel:{accel}, wait:{wait}");
            Stopwatch absMoveStopwatch = new Stopwatch();
            absMoveStopwatch.Start();
            if (Math.Abs(ZCurrentPos - pos) < 0.1)
            {
                Logger.Log("Z stage already in position");
                return true;
            }
            if (!IsZStageConnected)
            {
                Logger.Log("Z stage controller is not connected");
                if (ZStageReconnect())
                {
                    return AbsoluteMoveZStage(pos, speed, accel, wait);
                }
                else
                {
                    return false;
                }
            }
            try
            {
                bool result;
                //double zPos = pos;
                //double zSpeed = speed;
                //double zAccel = accel;
                result = _magellan.SetNewAcceleration(accel);
                result &= _magellan.SetNewDeceleration(accel);
                result &= _magellan.SetNewVelocity(speed);
                result &= _magellan.SetNewPosition(pos, wait);
                if (wait && result)      // update the z current pos immediately
                {
                    WaitMovement(MotionTypes.ZStage, pos, speed, accel);
                }
                else if (!wait && result)
                {
                    Thread waitmove = new Thread(e => WaitMovement(MotionTypes.ZStage, pos, speed, accel));
                    waitmove.Start();
                }
                else
                {
                    Logger.LogError("Z stage absolute move failed to set Z stage controller parameters.");
                }
                Logger.Log($"Z stage absolute move elapsed time [ms]|{absMoveStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                Logger.Log($"Finished Z stage abs move pos:{pos}, speed:{speed}, accel:{accel}, wait:{wait}", SeqLogFlagEnum.DEBUG);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        public bool RelativeMove(MotionTypes type, in int positionDelta, in int speed, in int accel, bool wait = false)
        {
            Logger.Log($"Starting relative move {type}, position delta:{positionDelta}, speed:{speed}, accel:{accel}, wait:{wait}",SeqLogFlagEnum.DEBUG);
            Stopwatch relMoveStopwatch = new Stopwatch();
            relMoveStopwatch.Start();
            if (!IsMotionConnected) { return false; }
            if (type == MotionTypes.None) { return false; }
            try
            {
                _isMoving = true;
                string motionName = string.Empty;
                int targetPosition = 0;
                switch (type)
                {
                    case MotionTypes.Filter:
                        motionName = "X";
                        targetPosition = FCurrentPos + positionDelta;
                        break;
                    case MotionTypes.YStage:
                        motionName = "Y";
                        targetPosition = YCurrentPos + positionDelta;
                        break;
                    case MotionTypes.Cartridge:
                        motionName = "Z";
                        targetPosition = CCurrentPos + positionDelta;
                        break;
                    case MotionTypes.XStage:
                        motionName = "X";
                        targetPosition = FCurrentPos + positionDelta;
                        break;
                    default:
                        return false;
                }
                SendCommand(GalilControl.Commands[GalilCommandSet.HaltThread]);
                SendCommand(GalilControl.Commands[GalilCommandSet.Stop] + motionName);
                SendCommand(GalilControl.Commands[GalilCommandSet.ServoHere] + motionName);
                SendCommand(GalilControl.Commands[GalilCommandSet.Speed] + string.Format("{0}={1}", motionName, speed));
                SendCommand(GalilControl.Commands[GalilCommandSet.Acceleration] + string.Format("{0}={1}", motionName, accel));
                SendCommand(GalilControl.Commands[GalilCommandSet.Deceleration] + string.Format("{0}={1}", motionName, accel));
                SendCommand(GalilControl.Commands[GalilCommandSet.PositionRelative] + string.Format("{0}={1}", motionName, positionDelta));
                SendCommand(GalilControl.Commands[GalilCommandSet.Begin] + motionName);
                if (wait)
                {
                    WaitMovement(type, targetPosition);
                }
                else
                {
                    Thread waitmove = new Thread(e => WaitMovement(type, targetPosition));
                    waitmove.Start();
                }
                Logger.Log($"Z stage relative move elapsed time [ms]|{relMoveStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                Logger.Log($"Finished z stage relative move {type}, position delta:{positionDelta}, speed:{speed}, accel:{accel}, wait:{wait}", SeqLogFlagEnum.DEBUG);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
            finally
            {
                _isMoving = false;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="positionDelta">unit: um</param>
        /// <param name="speed">unit: um/sec</param>
        /// <param name="accel">unit: um/sec^2</param>
        /// <param name="wait">if true, blocks this thread until the movement has completed</param>
        /// <returns></returns>
        public bool RelativeMoveZStage(in double positionDelta, in double speed, in double accel, bool wait = false)
        {
            if (!IsZStageConnected)
            {
                Logger.LogWarning($"Z stage relative move error: Z stage controller is not connected");
                if (ZStageReconnect())
                {
                    return AbsoluteMoveZStage(ZCurrentPos + positionDelta, speed, accel, wait);
                }
                else
                {

                    Logger.LogError($"Relative move failed. Could not connect to Z stage");
                    return false;
                }
            }
            else
            {
                try
                {
                    Logger.Log($"Starting relative move... Type: Z stage, pos:{positionDelta}, speed:{speed}, accel:{accel}, wait:{wait}", SeqLogFlagEnum.DEBUG);
                    
                    bool result;

                    // read the current position
                    double startPosition = 0;
                    _magellan.ReadActualPos(ref startPosition);

                    // set movement paramaters then send movement command
                    result = _magellan.SetNewAcceleration(accel);
                    result &= _magellan.SetNewDeceleration(accel);
                    result &= _magellan.SetNewVelocity(speed);
                    result &= _magellan.SetNewRelativePos(positionDelta, wait);

                    if (wait && result)
                    {
                        WaitMovement(MotionTypes.ZStage, startPosition + positionDelta, speed, accel);

                        double crntPos = 0;
                        _magellan.ReadActualPos(ref crntPos);
                        
                        // note: z-stage wait logic moved from here to WaitMovement method
                        /*int trycount = 0;

                        while (Math.Abs(Math.Abs(crntPos - startpos) - Math.Abs(pos)) > 0.1)
                        {
                            double val1 = Math.Abs(Math.Abs(crntPos - startpos) - Math.Abs(pos));
                            double val2 = Math.Abs(crntPos - startpos - pos);
                            Logger.Log($"val1:{val1} val2:{val2}");

                            if (trycount++ > 12 * 1000)
                            {
                                Logger.LogError("Failed to Move Z stage, relative move wait timeout.");
                                if (ZStageReconnect())
                                {
                                    // success
                                    AbsoluteMoveZStage(startpos, speed, accel, true);
                                    trycount = 0;
                                    _ = _MagellanController.SetNewAcceleration(accel);
                                    _ = _MagellanController.SetNewDeceleration(accel);
                                    _ = _MagellanController.SetNewVelocity(speed);
                                    _ = _MagellanController.SetNewRelativePos(pos, wait);
                                    _MagellanController.ReadActualPos(ref crntPos);
                                }
                                else
                                {
                                    throw new Exception("Z stage movement failed");
                                }
                            }
                            Thread.Sleep(5);
                            _MagellanController.ReadActualPos(ref crntPos);
                        }*/
                        ZCurrentPos = crntPos;
                    }
                    Logger.Log($"Finished relative move. Type: Z stage, pos:{positionDelta}, speed:{speed}, accel:{accel}, wait:{wait}", SeqLogFlagEnum.DEBUG);
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
            }
        }

        public bool HaltAllMotions()
        {
            try
            {
                if (IsMotionConnected)
                {
                    if (UsingControllerV2)
                    {
                        return _hywireMotion.SetStart(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W, new bool[] { false, false, false, false });
                    }
                    else
                    {
                        SendCommand("HX");
                        SendCommand("ST");
                    }
                    return true;
                }
                if (IsZStageConnected)
                {
                    return _magellan.TerminateMotion();
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        public bool HaltZStage()
        {
            try
            {
                return _magellan.TerminateMotion();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        public bool SelectFilter(int filter, bool wait = false)
        {
            if (filter == 0) { return false; }

            try
            {
                double factor = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter];
                int pos = (int)Math.Round(SettingsManager.ConfigSettings.FilterPositionSettings[filter] * factor);
                int speed = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Speed * factor);
                int accel = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Accel * factor);
                return AbsoluteMove(MotionTypes.Filter, pos, speed, accel, wait);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Read FC Door's status; Applied in ALF 2.5
        /// </summary>
        /// <returns></returns>
        public bool GetFCDoorStatus()
        {
            if (!IsFcDoorSensorEnabled) return true;

            return _hywireMotion.GetMotionInfo(MotorTypes.Motor_Z);
        }
        /// <summary>
        /// Set FC Door's status; Applied in ALF 2.5
        /// </summary>
        /// <param name="setOpen">true to open the door, otherwise close the door.</param>
        /// <returns></returns>
        public bool SetFCDoorStatus(bool setOpen)
        {
            if (!IsFcDoorSensorEnabled) return true;

            double doorMotionFactor = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor];
            int speed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Speed * doorMotionFactor);
            int acceleration = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Accel * doorMotionFactor);
            bool result = false;

            // set motor current to 100% before movement
            _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_Z, new MotionDriveCurrent[] { MotionDriveCurrent.Percent100 });
            if (setOpen)
            {
                int openPosition = (int)(SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitHigh * doorMotionFactor);
                if (AbsoluteMove(MotionTypes.FCDoor, openPosition, speed, acceleration, true))
                {
                    _hywireMotion.GetMotionInfo(MotorTypes.Motor_Z);
                    OnRecordsUpdated?.Invoke();
                    result = _hywireMotion.MotionStates[MotorTypes.Motor_Z].IsAtFwdLimit;
                }
            }
            else
            {
                int closedPosition = (int)(SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitLow * doorMotionFactor);
                if (!_IsFCDoorInitialized)
                {
                    if (HomeMotion(MotionTypes.FCDoor, speed, acceleration, true))
                    {
                        _IsFCDoorInitialized = true;
                        if (AbsoluteMove(MotionTypes.FCDoor, closedPosition, speed, acceleration, true))
                        {
                            _hywireMotion.GetMotionInfo(MotorTypes.Motor_Z);
                            OnRecordsUpdated?.Invoke();
                            result = true;
                        }
                    }
                }
                else if (AbsoluteMove(MotionTypes.FCDoor, closedPosition, speed, acceleration, true))
                {
                    _hywireMotion.GetMotionInfo(MotorTypes.Motor_Z);
                    OnRecordsUpdated?.Invoke();
                    result = true;
                }
            }
            // set motor current to 50% after moving to hold position but reduce heat
            _hywireMotion.SetMotionDriveCurrent(MotorTypes.Motor_Z, new MotionDriveCurrent[] { MotionDriveCurrent.Percent50 });
            return result;
        }
        #endregion Public Functions
    }

}
