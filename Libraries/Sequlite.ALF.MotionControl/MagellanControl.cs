using PMDLibrary;
using Sequlite.ALF.Common;
using System;
using System.IO.Ports;
using System.Threading;
using System.Windows;

namespace Sequlite.ALF.MotionControl
{
    public class MagellanControl : IDisposable
    {
        private bool _isConnected;
        private PMD.PMDPeripheralCOM _Peripheral;
        private PMD.PMDDevice _Device;
        private PMD.PMDAxis _Axis;
        ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Z-stage");
        private double _targetPos;
        private uint _portNumber; //< COM port number (ex: 3 for COM3)

        private const double CycleTime = 51.2e-6; //< The control cycle time of the controller. Units = [seconds]
        private const double CountsPerMicron = 200; //< 200 counts equals 1 micrometer

        public bool IsConnected { get { return _isConnected; } }
        public double VelocityLimitHigh { get; } = (int.MaxValue - 1) / 65536.0 / CountsPerMicron / CycleTime;
        public double VelocityLimitLow { get; } = int.MinValue / 65536.0 / CountsPerMicron / CycleTime;
        public double AccelLimitHigh { get; } = (int.MaxValue - 1) / 65536.0 / CountsPerMicron / CycleTime / CycleTime;
        public double AccelLimitLow { get; } = 0;

        #region Public Functions
        /// <summary>
        /// Overloaded connect function attempts to connect using all available serial ports
        /// </summary>
        /// <param name="baudrate"></param>
        /// <param name="homeAfterConnected"></param>
        /// <returns></returns>
        public bool Connect(in uint baudrate = 57600, bool homeAfterConnected = false)
        {
            if (_isConnected == true)
            {
                return _isConnected;
            }

            try
            {
                var portList = SerialPort.GetPortNames();
                if (portList != null)
                {
                    foreach(string port in portList)
                    {
                        if(Connect(port, baudrate, homeAfterConnected))
                        {
                            return _isConnected;
                        }
                    }
                    return _isConnected;
                }
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                string msg = $"Exception: serial port names could not be queried";
                Logger.LogError(msg + e.ToString());
            }
            catch (Exception e)
            {
                string msg = $"Unhandled exception";
                Logger.LogError(msg + e.ToString());
            }
            return _isConnected;
        }

        public bool Connect(in string portName, in uint baudrate = 57600, bool homeAfterConnected = false)
        {
            if (_isConnected == true)
            {
                return _isConnected;
            }

            // the magellan library is not stable while connecting to the device which is just powered on
            // here we try several times as a workaround
            const int maxTries = 10; //< maximum number of times to attempt connection
            const int retryDelayMs = 200; //< time delay in milliseconds to wait in between connection attempts

            for (int tryCounts = 0; tryCounts < maxTries; tryCounts++)
            {
                try
                {
                    _portNumber = uint.Parse(portName.Substring(3)) - 1;
                    _Peripheral = new PMD.PMDPeripheralCOM(_portNumber, baudrate, PMD.PMDSerialParity.None, PMD.PMDSerialStopBits.SerialStopBits1);
                    _Peripheral.SerialSync();
                    _Device = new PMD.PMDDevice(_Peripheral, PMD.PMDDeviceType.MotionProcessor);
                    _Axis = new PMD.PMDAxis(_Device, PMD.PMDAxisNumber.Axis1);
                    _Axis.OperatingMode = 0x37;     // Set operating mode (enable the loops)
                    _Axis.MotionCompleteMode = PMD.PMDMotionCompleteMode.ActualPosition;
                    _isConnected = true;
                    if (!Home())
                    {
                        return false;
                    }
                    break;
                }
                catch (UnauthorizedAccessException ex)   // failed to open the port, generally it means the port is already opened by others
                {
                    Logger.LogError(ex.ToString());
                }
                catch (Exception ex)   // it throws exception if failed to connect to the board
                {
                    Logger.LogError(ex.ToString());
                    if (ex.Message.Contains("Invalid"))
                    {
                        try
                        {
                            _Axis.Reset();
                            _Axis.OperatingMode = 0x37;
                            _Axis.MotionCompleteMode = PMD.PMDMotionCompleteMode.ActualPosition;
                            _isConnected = true;
                            if (!Home())
                            {
                                return false;
                            }
                        }
                        catch (Exception resetEx)
                        {
                            Logger.LogError(resetEx.ToString());
                        }
                        finally
                        {
                        }
                    }
                    else if (ex.Message.Contains("Timeout") || ex.Message.Contains("Checksum") || ex.Message.Contains("Instruction"))
                    {
                        Dispose();
                        Thread.Sleep(retryDelayMs);
                    }
                }
                if (_isConnected)
                {
                    break;
                }
            }
            if (_isConnected && homeAfterConnected)
            {
                Home();
            }
            return _isConnected;
        }

        public void Dispose()
        {
            _isConnected = false;
            if (_Axis != null)
            {
                _Axis.Close();
                _Axis = null;
            }
            if (_Device != null)
            {
                _Device.Close();
                _Device = null;
            }
            if (_Peripheral != null)
            {
                _Peripheral.Close();
                _Peripheral = null;
            }
        }

        public bool Home()
        {
            if (!_isConnected)
            {
                return false;
            }

            try
            {
                ushort encoderHomeMask = (ushort)PMD.PMDEventStatus.CaptureReceived;
                ushort motionCompleteMask = (ushort)PMD.PMDEventStatus.MotionComplete;
                //The "capture" sequence is specified as the home switch.  This means it will use the home switch for home.
                _Axis.CaptureSource = PMD.PMDCaptureSource.Home;
                // Sets drive in velocity mode for moving to an undefined distance (i.e. the home switch)
                _Axis.ProfileMode = PMD.PMDProfileMode.Velocity;

                // Set velocity & acceleration for searching home position
                double velocity = 1000;    // 1000 um/s
                SetNewAcceleration(20000, true);    // 20000 um/s^2

                // First check state of HomeSwitch, move away if HomeSwitch is active
                ushort signalStatus = _Axis.SignalStatus;
                if ((signalStatus & encoderHomeMask) == encoderHomeMask)
                {
                    // Home switch is already active so we need to move away.
                    SetNewVelocity(velocity, true);
                    signalStatus = 1;
                    while (signalStatus != 0x0000)     // wait for home switch to go inactive
                    {
                        signalStatus = _Axis.SignalStatus;
                        signalStatus &= encoderHomeMask;
                    }
                }
                _Axis.ResetEventStatus((ushort)~encoderHomeMask);
                _Axis.ResetEventStatus((ushort)(~motionCompleteMask));

                int capturePosition = _Axis.CaptureValue;    // Need to clear out any previous captures to rearm the capture mechanism

                // TO DO:	Update with the appropriate parameter values before executing this code
                SetNewVelocity(-velocity, true);
                _Axis.SetBreakpointValue(0, 0x00080008);    // break when capture occurs
                _Axis.SetBreakpoint(0, PMD.PMDAxisNumber.Axis1, PMD.PMDBreakpointAction.Update, PMD.PMDBreakpointTrigger.EventStatus);
                _Axis.StopMode = PMD.PMDStopMode.Smooth;

                WaitForEvent(PMD.PMDEventStatus.CaptureReceived, 10000);       // Wait 10 seconds for home to be toggled, otherwise time-out
                capturePosition = _Axis.CaptureValue;            // only for re-arming capture, throw away value
                _Axis.AdjustActualPosition(-capturePosition);
                _Axis.ResetEventStatus((ushort)~encoderHomeMask);
                _Axis.ResetEventStatus((ushort)(~motionCompleteMask));

                // default to trapezoidal profile mode
                _Axis.ProfileMode = PMD.PMDProfileMode.Trapezoidal;
                SetNewAcceleration(100000);
                SetNewDeceleration(100000);
                SetNewVelocity(2000);
                SetNewPosition(0, true);
                _targetPos = 0;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }

        }

        /// <summary>
        /// Read the encoder position, unit of um, returns false if failed to read
        /// </summary>
        /// <param name="encoderPos">unit of um.</param>
        /// <returns></returns>
        public bool ReadActualPos(ref double encoderPos)
        {
            if (!_isConnected) { return false; }
            try
            {
                encoderPos = _Axis.ActualPosition / CountsPerMicron;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading the actual position:" + ex.ToString());
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Set to new position. returns false if failed to do so.
        /// Make sure that the controller is in trapeziodal/S-curve profile mode with acceleration and velocity set beforehand.
        /// </summary>
        /// <param name="newPos">unit of um.</param>
        /// <returns></returns>
        public bool SetNewPosition(in double newPos, bool waitForComplete = false)
        {
            if (!IsConnected) { return false; }
            try
            {
                _targetPos = newPos;
                _Axis.Position = (int)Math.Round((_targetPos * CountsPerMicron));
                _Axis.Update();
                if (waitForComplete)
                {
                    return WaitForEvent(PMD.PMDEventStatus.MotionComplete, 10000);
                }
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        public bool SetNewRelativePos(in double newRelativePos, bool waitForComplete = false)
        {
            if (!IsConnected) { return false; }
            try
            {
                _targetPos += newRelativePos;
                _Axis.Position = (int)Math.Round((_targetPos * CountsPerMicron));
                _Axis.Update();
                if (waitForComplete)
                {
                    return WaitForEvent(PMD.PMDEventStatus.MotionComplete, 10000);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
        }
        /// <summary>
        /// Set new velocity.
        /// </summary>
        /// <param name="newVelocity">unit of um/s</param>
        /// <param name="updateImmediately">the new value would be updated if set to true.</param>
        /// <returns></returns>
        public bool SetNewVelocity(in double newVelocity, bool updateImmediately = false)
        {
            if (!IsConnected) { return false; }
            try
            {
                int newValue = (int)Math.Round((newVelocity * CountsPerMicron * CycleTime * 65536));     // convert um/s to counts/cycle
                _Axis.Velocity = newValue;
                if (updateImmediately)
                {
                    _Axis.Update();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Set new acceleration.
        /// </summary>
        /// <param name="newAccel">unit of um/s^2</param>
        /// <param name="updateImmediately">the new value would be updated if set to true.</param>
        /// <returns></returns>
        public bool SetNewAcceleration(in double newAccel, bool updateImmediately = false)
        {
            if (!IsConnected) { return false; }
            try
            {
                uint newValue = (uint)Math.Round((newAccel * CountsPerMicron * CycleTime * CycleTime * 65536));
                _Axis.Acceleration = newValue;
                if (updateImmediately)
                {
                    _Axis.Update();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
        }

        public bool SetNewDeceleration(in double newDecel, bool updateImmediately = false)
        {
            if (!IsConnected) { return false; }
            try
            {
                uint newValue = (uint)Math.Round((newDecel * CountsPerMicron * CycleTime * CycleTime * 65536));
                _Axis.Deceleration = newValue;
                if (updateImmediately)
                {
                    _Axis.Update();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
        }

        public bool TerminateMotion()
        {
            if (!IsConnected) { return false; }
            try
            {
                _Axis.ClearPositionError();
                _Axis.Update();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }
        public bool WaitForEvent(PMD.PMDEventStatus waitingEvent, int timeout)
        {
            if (!IsConnected) { return false; }
            ushort mask = 0;
            ushort eventStatus = 0;
            mask = (ushort)waitingEvent;
            try
            {
                for (int i = 0; i < timeout / 10; i++)
                {
                    eventStatus = _Axis.EventStatus;
                    if ((ushort)(eventStatus & mask) == mask)
                    {
                        _Axis.ResetEventStatus((ushort)~mask);
                        return true;
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
            return false;

        }

        public bool SetNewBaudrate(uint baudrate)
        {
            if (!IsConnected) { return false; }
            try
            {
                PMD.PMDSerialBaud baud;
                switch (baudrate)
                {
                    case 1200:
                        baud = PMD.PMDSerialBaud.SerialBaud1200;
                        break;
                    case 2400:
                        baud = PMD.PMDSerialBaud.SerialBaud2400;
                        break;
                    case 9600:
                        baud = PMD.PMDSerialBaud.SerialBaud9600;
                        break;
                    case 19200:
                        baud = PMD.PMDSerialBaud.SerialBaud19200;
                        break;
                    case 57600:
                        baud = PMD.PMDSerialBaud.SerialBaud57600;
                        break;
                    case 115200:
                        baud = PMD.PMDSerialBaud.SerialBaud115200;
                        break;
                    case 230400:
                        baud = PMD.PMDSerialBaud.SerialBaud230400;
                        break;
                    case 460800:
                        baud = PMD.PMDSerialBaud.SerialBaud460800;
                        break;
                    default:
                        return false;
                }
                PMD.PMDSerialBaud olderBaud = PMD.PMDSerialBaud.SerialBaud1200;
                PMD.PMDSerialParity olderParity = PMD.PMDSerialParity.None;
                PMD.PMDSerialStopBits olderStopBits = PMD.PMDSerialStopBits.SerialStopBits1;
                PMD.PMDSerialProtocol olderProtocol = PMD.PMDSerialProtocol.MultiDropUsingIdleLineDetection;
                byte olderId = 0;
                _Axis.GetSerialPortMode(ref olderBaud, ref olderParity, ref olderStopBits, ref olderProtocol, ref olderId);
                _Axis.SetSerialPortMode(baud, olderParity, olderStopBits, olderProtocol, olderId);

                Dispose();

                _Peripheral = new PMD.PMDPeripheralCOM(_portNumber, baudrate, PMD.PMDSerialParity.None, PMD.PMDSerialStopBits.SerialStopBits1);
                _Peripheral.SerialSync();
                _Device = new PMD.PMDDevice(_Peripheral, PMD.PMDDeviceType.MotionProcessor);
                _Axis = new PMD.PMDAxis(_Device, PMD.PMDAxisNumber.Axis1);
                _Axis.OperatingMode = 0x37;     // Set operating mode (enable the loops)
                return true;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
                _isConnected = false;
                return false;
            }
        }
        #endregion Public Functions

    }
}
