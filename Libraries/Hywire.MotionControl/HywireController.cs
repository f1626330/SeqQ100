using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hywire.MotionControl
{
    public class HywireController
    {
        #region Events
        public delegate void UpdateQueryHandle();
        public event UpdateQueryHandle OnQueryUpdated;
        #endregion Events

        #region Private Fields
        private ControllerComm _port;
        private Thread _QueryThread;
        //private Thread _HearingThread;
        //private static object _MotionLock = new object();
        private Dictionary<MotorTypes, int> _StartSpeeds;
        private Dictionary<MotorTypes, int> _TopSpeeds;
        private Dictionary<MotorTypes, int> _Accelerations;
        private Dictionary<MotorTypes, int> _Decelerations;

        private bool _IsNewLog = true;
        private string _FolderStr;
        private string _Filename;
        private static object _LogLock = new object();
        private List<string> _LogList;
        private Thread _LogThread;
        private static object _Lock = new object();
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("HywireController");
        #endregion Private Fields

        #region Constructor
        public HywireController()
        {
            _port = new ControllerComm();
            _port.TimeOutInMilliSec = 1000;

            MotionStates = new Dictionary<MotorTypes, MotionState>();
            MotionStates.Add(MotorTypes.Motor_X, new MotionState());
            MotionStates.Add(MotorTypes.Motor_Y, new MotionState());
            MotionStates.Add(MotorTypes.Motor_Z, new MotionState());
            MotionStates.Add(MotorTypes.Motor_W, new MotionState());

            CurrentPositions = new Dictionary<MotorTypes, int>();
            CurrentPositions.Add(MotorTypes.Motor_X, 0);
            CurrentPositions.Add(MotorTypes.Motor_Y, 0);
            CurrentPositions.Add(MotorTypes.Motor_Z, 0);
            CurrentPositions.Add(MotorTypes.Motor_W, 0);

            EncoderPositions = new Dictionary<MotorTypes, int>();
            EncoderPositions.Add(MotorTypes.Motor_X, 0);
            EncoderPositions.Add(MotorTypes.Motor_Y, 0);
            EncoderPositions.Add(MotorTypes.Motor_Z, 0);
            EncoderPositions.Add(MotorTypes.Motor_W, 0);

            _StartSpeeds = new Dictionary<MotorTypes, int>();
            _StartSpeeds.Add(MotorTypes.Motor_X, 0);
            _StartSpeeds.Add(MotorTypes.Motor_Y, 0);
            _StartSpeeds.Add(MotorTypes.Motor_Z, 0);
            _StartSpeeds.Add(MotorTypes.Motor_W, 0);

            _TopSpeeds = new Dictionary<MotorTypes, int>();
            _TopSpeeds.Add(MotorTypes.Motor_X, 0);
            _TopSpeeds.Add(MotorTypes.Motor_Y, 0);
            _TopSpeeds.Add(MotorTypes.Motor_Z, 0);
            _TopSpeeds.Add(MotorTypes.Motor_W, 0);

            _Accelerations = new Dictionary<MotorTypes, int>();
            _Accelerations.Add(MotorTypes.Motor_X, 0);
            _Accelerations.Add(MotorTypes.Motor_Y, 0);
            _Accelerations.Add(MotorTypes.Motor_Z, 0);
            _Accelerations.Add(MotorTypes.Motor_W, 0);

            _Decelerations = new Dictionary<MotorTypes, int>();
            _Decelerations.Add(MotorTypes.Motor_X, 0);
            _Decelerations.Add(MotorTypes.Motor_Y, 0);
            _Decelerations.Add(MotorTypes.Motor_Z, 0);
            _Decelerations.Add(MotorTypes.Motor_W, 0);

            _LogList = new List<string>();
            _LogThread = new Thread(() =>
            {
                int timecount = 0;
                while (true)
                {
                    lock (_LogLock)
                    {
                        if (_LogList.Count > 300 || timecount % 120 == 0)
                        {
                            while (_LogList.Count > 0)
                            {
                                FileMode mode = _IsNewLog ? FileMode.Create : FileMode.Append;
                                _IsNewLog = false;
                                try
                                {
                                    if (_LogList[0] != null)
                                    {
                                        using (FileStream fs = new FileStream(_FolderStr, mode))
                                        {
                                            byte[] logs = Encoding.Default.GetBytes(_LogList[0]);
                                            fs.Write(logs, 0, logs.Length);
                                            fs.Flush();
                                            _LogList.RemoveAt(0);
                                        }
                                    }
                                    else { Logger.LogError("LogList[0] == null"); }

                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError(ex.ToString());
                                }
                            }
                        }
                    }
                    timecount++;
                    Thread.Sleep(500);
                }
            });
            _LogThread.IsBackground = true;
            _LogThread.Start();
        }
        #endregion Constructor

        #region Public Properties
        public bool IsConnected { get; private set; }
        public string ControllerVersion { get; private set; }
        public Dictionary<MotorTypes, int> CurrentPositions { get; }
        public Dictionary<MotorTypes, MotionState> MotionStates { get; }
        public Dictionary<MotorTypes, int> EncoderPositions { get; }
        public bool HomeYFailed { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect(int baudrate = 115200)
        {
            if (IsConnected) { return true; }

            _Filename = string.Format("{0}-HywireMotionLog.txt", DateTime.Now.ToString("yyyyMMddHHmmss"));
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _FolderStr = Path.Combine(commonAppData, "Sequlite\\Log\\MotionLog");
            if (!Directory.Exists(_FolderStr))
            {
                DirectoryInfo di = Directory.CreateDirectory(_FolderStr);
            }
            _FolderStr = Path.Combine(_FolderStr, _Filename);
            _IsNewLog = !File.Exists(_FolderStr);
            foreach (var portName in SerialPort.GetPortNames())
            {
                try
                {
                    if (_port.Open(portName, baudrate))
                    {
                        if (GetControllerVersion())
                        {
                            IsConnected = true;
                            _QueryThread = new Thread(_QueryTimer_Elapsed);
                            _QueryThread.IsBackground = true;
                            _QueryThread.Start();

                            return true;
                        }
                        else
                        {
                            _port.Close();
                            continue;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    _port.Close();
                }
                catch (UnauthorizedAccessException)         // port is already opened by other applications
                {

                }
                catch (Exception)
                {
                    _port.Close();
                }
            }
            return false;
        }

        public bool Connect(string portName, int baudrate = 115200)
        {
            if (IsConnected) { return true; }

            _Filename = string.Format("{0}-HywireMotionLog.txt", DateTime.Now.ToString("yyyyMMddHHmmss"));
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _FolderStr = Path.Combine(commonAppData, "Sequlite\\Log\\MotionLog");
            if (!Directory.Exists(_FolderStr))
            {
                DirectoryInfo di = Directory.CreateDirectory(_FolderStr);
            }
            _FolderStr = Path.Combine(_FolderStr, _Filename);
            _IsNewLog = !File.Exists(_FolderStr);
            try
            {
                if (_port.Open(portName, baudrate))
                {
                    if (GetControllerVersion())
                    {
                        IsConnected = true;
                        _QueryThread = new Thread(_QueryTimer_Elapsed);
                        _QueryThread.IsBackground = true;
                        _QueryThread.Start();

                        return true;
                    }
                    else
                    {
                        _port.Close();
                    }
                }
            }
            catch (TimeoutException)
            {
                _port.Close();
            }
            catch (UnauthorizedAccessException)         // port is already opened by other applications
            {

            }
            catch (Exception)
            {
                _port.Close();
            }
            return false;
        }

        public bool Reconnect()
        {
            bool suc = _port.Reconnect();
            LogToFile("Reconnect controller");
            return suc;

        }
        /// <summary>
        /// enable and home specified motions. typically used in initialization
        /// </summary>
        /// <param name="startSpeeds"></param>
        /// <param name="topSpeeds"></param>
        /// <param name="accVals"></param>
        /// <param name="dccVals"></param>
        /// <returns></returns>
        public bool HomeMotions(MotorTypes motions, int[] startSpeeds, int[] topSpeeds, int[] accVals, int[] dccVals)
        {
            LogToFile(string.Format("Homing {0}, speeds={1}, accelerations={2}\r\n", motions, topSpeeds, accVals));
            if (SetEnable(motions, new bool[] { true, true, true, true }) == false)
            {
                LogToFile("Set Enable return false, homing failed\r\n");
                return false;
            }
            if (SetMotionSpeedsAndAccs(motions, startSpeeds, topSpeeds, accVals, dccVals) == false)
            {
                LogToFile("Set speeds & accelerations return false, homing failed\r\n");
                return false;
            }
            if (SetHome(motions) == false)
            {
                LogToFile("Start Homing return false, homing failed\r\n");
                return false;
            }
            if (GetMotionInfo(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W) == false)
            {
                LogToFile("Get Motion info return false, homing failed\r\n");
                return false;
            }
            LogToFile("Homing suceeded.\r\n");
            return true;
        }

        public bool GetControllerVersion()
        {
            // this function is used for connection, so it ignores the IsConnected property
            //if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.GetControllerVersion();
                    if (_port.SendBytes(frame) == false)
                    {
                        return false;
                    }
                    //_Port.SetStatusIdle();
                    Frame response = _port.Response;
                    if (response == null)
                    {
                        return false;
                    }
                    ControllerVersion = string.Format("{0}.{1}.{2}.{3}", (byte)(response.Data[0] >> 24), (byte)(response.Data[0] >> 16),
                                                                        (byte)(response.Data[0] >> 8), (byte)(response.Data[0]));
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool GetMotionInfo(MotorTypes motions)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.GetMotionInfo(motions);
                    if (_port.SendBytes(frame) == false)
                    {
                        return false;
                    }
                    Frame response = _port.Response;
                    if (response == null)
                    {
                        return false;
                    }
                    int dataIndex = 0;
                    bool containsX = false;
                    bool containsY = false;
                    bool containsZ = false;
                    bool containsW = false;
                    if ((response.MotionType & MotorTypes.Motor_X) == MotorTypes.Motor_X) { containsX = true; }
                    if ((response.MotionType & MotorTypes.Motor_Y) == MotorTypes.Motor_Y) { containsY = true; }
                    if ((response.MotionType & MotorTypes.Motor_Z) == MotorTypes.Motor_Z) { containsZ = true; }
                    if ((response.MotionType & MotorTypes.Motor_W) == MotorTypes.Motor_W) { containsW = true; }
                    if (containsX)
                    {
                        CurrentPositions[MotorTypes.Motor_X] = response.Data[dataIndex++];
                    }
                    if (containsY)
                    {
                        CurrentPositions[MotorTypes.Motor_Y] = response.Data[dataIndex++];
                    }
                    if (containsZ)
                    {
                        CurrentPositions[MotorTypes.Motor_Z] = response.Data[dataIndex++];
                    }
                    if (containsW)
                    {
                        CurrentPositions[MotorTypes.Motor_W] = response.Data[dataIndex++];
                    }
                    if (containsX)
                    {
                        MotionStates[MotorTypes.Motor_X].MapFromData((byte)response.Data[dataIndex++]);
                    }
                    if (containsY)
                    {
                        MotionStates[MotorTypes.Motor_Y].MapFromData((byte)response.Data[dataIndex++]);
                    }
                    if (containsZ)
                    {
                        MotionStates[MotorTypes.Motor_Z].MapFromData((byte)response.Data[dataIndex++]);
                    }
                    if (containsW)
                    {
                        MotionStates[MotorTypes.Motor_W].MapFromData((byte)response.Data[dataIndex++]);
                    }
                    if (containsX)
                    {
                        EncoderPositions[MotorTypes.Motor_X] = response.Data[dataIndex++];
                    }
                    if (containsY)
                    {
                        EncoderPositions[MotorTypes.Motor_Y] = response.Data[dataIndex++];
                    }
                    if (containsZ)
                    {
                        EncoderPositions[MotorTypes.Motor_Z] = response.Data[dataIndex++];
                    }
                    if (containsW)
                    {
                        EncoderPositions[MotorTypes.Motor_W] = response.Data[dataIndex++];
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetMotionPolarities(MotionSignalPolarity x, MotionSignalPolarity y, MotionSignalPolarity z, MotionSignalPolarity w)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetMotionPolarities(x, y, z, w);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetMotionDriveCurrent(MotorTypes motions, MotionDriveCurrent[] currents)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetDriveCurrent(motions, currents);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetMotionDriveMode(MotorTypes motions, MotionDriveMode[] driveModes)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetDriveMode(motions, driveModes);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool SetMotionSpeedsAndAccs(MotorTypes motions, int[] startSpeeds, int[] topSpeeds, int[] accVals, int[] dccVals)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetMotionSpeedAndAcc(motions, startSpeeds, topSpeeds, accVals, dccVals);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetEnable(MotorTypes motions, bool[] enables)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetMotionEnable(motions, enables);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetStart(MotorTypes motions, bool[] starts)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetMotionStart(motions, starts);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool SetHome(MotorTypes motions)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.SetMotionHome(motions);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool SetStart(MotorTypes motions, int[] startSpeeds, int[] topSpeeds, int[] accVals, int[] dccVals,
                                                        int[] dccPosL, int[] tgtPosL, int[] dccPosR, int[] tgtPosR, int[] delayTimes, int[] repeats, bool[] startNow)
        {
            if (!IsConnected) { return false; }
            try
            {
                lock (_Lock)
                {
                    //if (_Port.WaitForStatus(_Port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                    //{
                    //    return false;
                    //}
                    byte[] frame = Protocol.StartMotion(motions, startSpeeds, topSpeeds, accVals, dccVals,
                                                        dccPosL, tgtPosL, dccPosR, tgtPosR, delayTimes, repeats, startNow);
                    if (_port.SendBytes(frame) == false) { return false; }
                    if (_port.Response == null) { return false; }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool HomeMotion(MotorTypes motions, int[] startSpeeds, int[] topSpeeds, int[] accVals, int[] dccVals, bool waitForcomplete)
        {
            if (!IsConnected) { return false; }
            LogToFile(string.Format("Homing {0}, speeds={1}, accelerations={2}, waitForComplete={3}", motions, topSpeeds, accVals, waitForcomplete));
            bool containsX = (motions & MotorTypes.Motor_X) == MotorTypes.Motor_X;
            bool containsY = (motions & MotorTypes.Motor_Y) == MotorTypes.Motor_Y;
            bool containsZ = (motions & MotorTypes.Motor_Z) == MotorTypes.Motor_Z;
            bool containsW = (motions & MotorTypes.Motor_W) == MotorTypes.Motor_W;
            SetEnable(motions, new bool[] { true, true, true, true });
            // 1. stop the motion if possible
            SetStart(motions, new bool[] { false, false, false, false });
            if (containsX)
            {
                while (MotionStates[MotorTypes.Motor_X].IsBusy) { Thread.Sleep(1); }
            }
            if (containsY)
            {
                while (MotionStates[MotorTypes.Motor_Y].IsBusy) { Thread.Sleep(1); }
            }
            if (containsZ)
            {
                while (MotionStates[MotorTypes.Motor_Z].IsBusy) { Thread.Sleep(1); }
            }
            if (containsW)
            {
                while (MotionStates[MotorTypes.Motor_W].IsBusy) { Thread.Sleep(1); }
            }

            // 2. set motion parameters
            int retry = 0;
            while (SetMotionSpeedsAndAccs(motions, startSpeeds, topSpeeds, accVals, dccVals) == false)
            {
                if (++retry > 3)
                {
                    LogToFile("Set speeds & accelerations return false, homing failed\r\n");
                    return false;
                }
                LogToFile("Set speeds & accelerations return false, retry\r\n");
            }
            // 3. set home
            retry = 0;
            while (SetHome(motions) == false)
            {
                if (++retry > 3)
                {
                    LogToFile("Start homing return false, homing failed\r\n");
                    return false;
                }
                LogToFile("Start homing return false, retry\r\n");
            }
            retry = 0;
            while (GetMotionInfo(motions) == false)
            {
                if (++retry > 3)
                {
                    LogToFile("Get motion info return false, homing failed\r\n");
                    return false;
                }
                LogToFile("Get motion info return false, retry\r\n");
            }
            // 4. wait for motion complete if needed
            if (waitForcomplete)
            {
                int count = 0;
                retry = 0;
                while (GetMotionInfo(motions) == false)
                {
                    if (++retry > 3)
                    {
                        LogToFile("Get motion info return false, homing failed\r\n");
                        return false;
                    }
                    LogToFile("Get motion info return false, retry\r\n");
                }
                bool completed = false;
                do
                {
                    if (count++ > 30000)        // wait 30 seconds before throwing exceptions
                    {
                        if (containsY)
                        {
                            HomeYFailed = true;
                        }
                        LogToFile("Waiting for completed too long, homing failed\r\n");
                        throw new Exception(string.Format("{0} waiting too long", motions.ToString()));
                    }

                    completed = true;
                    if (containsX && MotionStates[MotorTypes.Motor_X].IsBusy) { completed = false; }
                    if (containsY && MotionStates[MotorTypes.Motor_Y].IsBusy) { completed = false; }
                    if (containsZ && MotionStates[MotorTypes.Motor_Z].IsBusy) { completed = false; }
                    if (containsW && MotionStates[MotorTypes.Motor_W].IsBusy) { completed = false; }
                    Thread.Sleep(1);
                }
                while (completed == false);
            }
            if (containsY)
            {
                HomeYFailed = false;
            }
            LogToFile("Homing succeeded.\r\n");
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="motion"></param>
        /// <param name="startSpeed">unit: pulse/sec</param>
        /// <param name="topSpeed">unit: pulse/sec</param>
        /// <param name="accVal">unit: pulse/sec^2</param>
        /// <param name="dccVal">unit: pulse/sec^2</param>
        /// <param name="tgtPos">unit: pulse</param>
        /// <param name="waitForcomplete"></param>
        /// <returns></returns>
        public bool AbsoluteMove(MotorTypes motion, int startSpeed, int topSpeed, int accVal, int dccVal, int tgtPos, bool startNow, bool waitForcomplete)
        {
            if (!IsConnected) { return false; }
            LogToFile(string.Format("Absolute move {0}, speed={1}, acceleration={2}, target pos={3}, startNow={4}, waitForComplete={5}\r\n",
                motion, topSpeed, accVal, tgtPos, startNow, waitForcomplete));
            //SetEnable(motion, new bool[] { true });
            // 1. stop the motion if possible
            if (MotionStates[motion].IsBusy)
            {
                LogToFile("Motion is busy, stop it at first.\r\n");
                SetStart(motion, new bool[] { false });
                do
                {
                    Thread.Sleep(1);
                    GetMotionInfo(motion);
                }
                while (MotionStates[motion].IsBusy);
                Thread.Sleep(5);    // wait for the motion stopped, 5msec is an estimated time
            }

            // 2. calculate and set dcc position
            int retry = 0;
            while (GetMotionInfo(motion) == false)
            {
                if (++retry > 3)
                {
                    LogToFile("Get motion info return false, Absolute move failed\r\n");
                    return false;
                }
                LogToFile("Get motion info return false, retry\r\n");
            }
            double crntPos = CurrentPositions[motion];
            LogToFile(string.Format("Current position of {0} is: {1}\r\n", motion, crntPos));
            if (crntPos == tgtPos)
            {
                LogToFile("Current Position is already at target position, Absolute move succeeded.\r\n");
                return true;
            }
            double accPos, dccPos;
            double speedSqureDiff = 1.0 * topSpeed * topSpeed - 1.0 * startSpeed * startSpeed;
            double dir = tgtPos > crntPos ? 1.0 : -1.0;
            accPos = crntPos + dir * speedSqureDiff / 2 / accVal;
            dccPos = tgtPos - dir * speedSqureDiff / 2 / dccVal;
            bool noTopSpeed = false;
            if ((tgtPos > crntPos) && (accPos > dccPos))
            {
                noTopSpeed = true;
            }
            else if ((tgtPos < crntPos) && (accPos < dccPos))
            {
                noTopSpeed = true;
            }
            if (noTopSpeed)
            {
                double acc_dcc = accVal + dccVal;
                dccPos = tgtPos * (dccVal / acc_dcc) + crntPos * (accVal / acc_dcc);
            }
            LogToFile(string.Format("Calculated dcc position of {0} is: {1}\r\n", motion, dccPos));

            // 3. set parameters and start motion if startNow is true
            retry = 0;
            while (SetStart(motion, new int[] { startSpeed }, new int[] { topSpeed }, new int[] { accVal }, new int[] { dccVal },
                new int[] { (int)dccPos }, new int[] { tgtPos }, new int[] { 0 }, new int[] { 0 }, new int[] { 0 }, new int[] { 0 }, new bool[] { startNow }) == false)
            {
                if (++retry > 3)
                {
                    LogToFile("Set motion parameters return false, absolute move failed\r\n");
                    return false;
                }
                LogToFile("Set motion parameters return false, retry\r\n");
            }
            if (startNow)
            {
                //if (SetStart(motion, new bool[] { true }) == false) { return false; }
                if (waitForcomplete)
                {
                    int count = 0;
                    bool completed = false;
                    do
                    {
                        if (GetMotionInfo(motion) == false) { continue; }
                        if (count++ > 30000)        // wait 30 seconds before throwing exceptions
                        {
                            LogToFile("Absolute move waiting too long, failed\r\n");
                            throw new Exception(string.Format("{0} waiting too long", motion.ToString()));
                        }

                        completed = true;
                        if (MotionStates[motion].IsBusy) { completed = false; }
                        Thread.Sleep(1);
                    }
                    while (completed == false);
                }
            }
            LogToFile("Absolute move succeeded.\r\n");
            return true;
        }

        public bool ResetEncoderPosition(MotorTypes motion)
        {
            if (!IsConnected) { return false; }
            try
            {
                LogToFile(string.Format("Resetting Encoder position of {0}\r\n", motion));
                if (_port.WaitForStatus(_port.TimeOutInMilliSec, CommLibrary.SerialCommBase.CommStatus.Idle) == false)
                {
                    LogToFile("Waiting for serial comm idle timeout, resetting encoder position failed\r\n");
                    return false;
                }
                byte[] frame = Protocol.ResetEncoderPosition(motion);
                if (_port.SendBytes(frame) == false)
                {
                    LogToFile("Sending bytes return false, resetting encoder position failed\r\n");
                    return false;
                }
                if (_port.Response == null)
                {
                    LogToFile("Device response is invalid, resetting encoder position failed\r\n");
                    return false;
                }
                return true;
            }
            catch
            {
                LogToFile("Exception catched, resetting encoder position failed\r\n");
                return false;
            }
        }
        #endregion Public Functions

        #region Private Functions
        private void _QueryTimer_Elapsed()
        {
            while (IsConnected)
            {
                if (_port.Status == CommLibrary.SerialCommBase.CommStatus.Idle)
                {
                    if (GetMotionInfo(MotorTypes.Motor_X | MotorTypes.Motor_Y | MotorTypes.Motor_Z | MotorTypes.Motor_W))
                    {
                        OnQueryUpdated?.Invoke();
                    }
                }
                Thread.Sleep(500);
            }
        }

        private void LogToFile(string newLog)
        {
            string logPack = string.Format("{0}: {1}", DateTime.Now.ToString("HH:mm:ss:fff"), newLog);
            lock (_LogLock)
            {
                _LogList.Add(logPack);
            }
        }
        #endregion Private Functions

    }

    public class MotionState
    {
        public bool IsEnabled { get; private set; }
        public bool IsAtFwdLimit { get; private set; }
        public bool IsAtBwdLimit { get; private set; }
        public bool IsAtHome { get; private set; }
        public bool IsBusy { get; private set; }

        public void MapFromData(byte state)
        {
            IsAtHome = (state & 0x10) == 0x10;
            IsAtFwdLimit = (state & 0x08) == 0x08;
            IsAtBwdLimit = (state & 0x04) == 0x04;
            IsBusy = (state & 0x02) == 0x02;
            IsEnabled = (state & 0x01) == 0x01;
        }
    }

    public class MotionSignalPolarity
    {
        public bool ClkPolar { get; set; }
        public bool DirPolar { get; set; }
        public bool EnaPolar { get; set; }
        public bool FwdLmtPolar { get; set; }
        public bool BwdLmtPolar { get; set; }
        public bool HomePolar { get; set; }

        public byte MapToByte()
        {
            byte result = 0;
            result = (byte)(result | (ClkPolar ? 0x20 : 0x00));
            result = (byte)(result | (DirPolar ? 0x10 : 0x00));
            result = (byte)(result | (EnaPolar ? 0x08 : 0x00));
            result = (byte)(result | (FwdLmtPolar ? 0x04 : 0x00));
            result = (byte)(result | (BwdLmtPolar ? 0x02 : 0x00));
            result = (byte)(result | (HomePolar ? 0x01 : 0x00));
            return result;
        }

        public void MapFromByte(byte polar)
        {
            ClkPolar = (polar & 0x20) == 0x20 ? true : false;
            DirPolar = (polar & 0x10) == 0x10 ? true : false;
            EnaPolar = (polar & 0x08) == 0x08 ? true : false;
            FwdLmtPolar = (polar & 0x04) == 0x04 ? true : false;
            BwdLmtPolar = (polar & 0x02) == 0x02 ? true : false;
            HomePolar = (polar & 0x01) == 0x01 ? true : false;
        }
    }
}
