using Sequlite.ALF.Common;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class HardwareVerifyViewModel : ViewModelBase
    {
        #region Private Fields
        private MotionController _MotionCtroller;
        private LEDController _LEDController;
        private MainBoardController _MBController;
        private FluidController _FluidController;
        private Chiller _ChillerController;
        private RFIDController _RFIDReader;
        private BarCodeReader _BarCodeReader;

        private HardwareTestViewModel _CurrentTestItem;
        private Thread _SelftTestThread;
        private Thread _SensorsTestThread;
        private Thread _WorkingTestThread;
        private DateTime _WorkingTestStartTime;
        private List<double> _FCLogTimes = new List<double>();
        private List<double> _FCTemperLog = new List<double>();
        private List<double> _FCHeatSinkTemperLog = new List<double>();

        private CameraViewModel _CameraVm;
        private ImageGalleryViewModel _GalleryVm;
        #endregion Private Fields

        #region Public Properties
        public ObservableCollection<HardwareTestViewModel> SelfTestItems { get; }
        public ObservableCollection<HardwareTestViewModel> SensorsTestItems { get; }
        public ObservableCollection<HardwareTestViewModel> WorkingTestItems { get; }
        #endregion Public Properties

        #region Constructor
        public HardwareVerifyViewModel(CameraViewModel cameraVm, ImageGalleryViewModel galleryVm)
        {
            _CameraVm = cameraVm;
            _GalleryVm = galleryVm;
            _MotionCtroller = MotionController.GetInstance();
            _LEDController = LEDController.GetInstance();
            _MBController = MainBoardController.GetInstance();
            _FluidController = FluidController.GetInstance();
            _ChillerController = Chiller.GetInstance();
            _RFIDReader = RFIDController.GetInstance();
            _BarCodeReader = BarCodeReader.GetInstance();

            SelfTestItems = new ObservableCollection<HardwareTestViewModel>();
            SensorsTestItems = new ObservableCollection<HardwareTestViewModel>();
            WorkingTestItems = new ObservableCollection<HardwareTestViewModel>();
            InitSelfTestItems();
            InitSensorTestItems();
            InitWorkingTestItems();
        }

        public void InitSelfTestItems()
        {
            SelfTestItems.Clear();
            SelfTestItems.Add(new HardwareTestViewModel("LED/PD", "Turn on LED and read PD value"));
            SelfTestItems.Add(new HardwareTestViewModel("Camera", "Grab images from both cameras"));
            SelfTestItems.Add(new HardwareTestViewModel("FC Temper Ctrl", "Control FC Temperature"));
            SelfTestItems.Add(new HardwareTestViewModel("Chiller Temper", "Get Chiller Temper"));
            SelfTestItems.Add(new HardwareTestViewModel("Motions", "check all Motions"));
        }

        public void InitSensorTestItems()
        {
            SensorsTestItems.Clear();
            SensorsTestItems.Add(new HardwareTestViewModel("Buffer Sensors", "Sipper Down sensor & Buffer In sensor"));
            SensorsTestItems.Add(new HardwareTestViewModel("Cartridge Sensors", "Cartridge Presented & Chiller Door Status"));
            SensorsTestItems.Add(new HardwareTestViewModel("RFID Reader", "Read RFID"));
            SensorsTestItems.Add(new HardwareTestViewModel("Load Cartridge", "Move Cartridge to the Lowest position"));
            SensorsTestItems.Add(new HardwareTestViewModel("Prepare FC", "Move X & Y to specific positions for clamping FC"));
            SensorsTestItems.Add(new HardwareTestViewModel("Clamp FC", "Check FC Clamp sensor"));
            SensorsTestItems.Add(new HardwareTestViewModel("2D Code Reader", "Read FC's 2D Code"));
        }

        public void InitWorkingTestItems()
        {
            WorkingTestItems.Clear();
            WorkingTestItems.Add(new HardwareTestViewModel("XY Home", "Home XY to home for next test"));
            WorkingTestItems.Add(new HardwareTestViewModel("Temperature Loop", "Repeats 100 times from 20 to 95, then to 65, then back to 20"));
            WorkingTestItems.Add(new HardwareTestViewModel("Capture Image", "Repeats 200 times to capture images at different XY positions"));
        }
        #endregion Constructor

        #region Public Properties
        public HardwareTestViewModel CurrentTestItem
        {
            get { return _CurrentTestItem; }
            set
            {
                if (_CurrentTestItem != value)
                {
                    _CurrentTestItem = value;
                    if (_CurrentTestItem != null)
                    {
                        _CurrentTestItem.TestResult = TestResults.Testing;
                    }
                    RaisePropertyChanged(nameof(CurrentTestItem));
                }
            }
        }
        #endregion Public Properties

        #region Set Command
        private RelayCommand _SetCmd;
        public RelayCommand SetCmd
        {
            get
            {
                if (_SetCmd == null)
                {
                    _SetCmd = new RelayCommand(ExecuteSetCmd, CanExecuteSetCmd);
                }
                return _SetCmd;
            }
        }

        private void ExecuteSetCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "GoSelftTest":
                    InitSelfTestItems();
                    _SelftTestThread = new Thread(SelfTestProcessing);
                    _SelftTestThread.IsBackground = true;
                    _SelftTestThread.Start();
                    break;
                case "StopSelftTest":
                    if (_SelftTestThread != null)
                    {
                        _SelftTestThread.Abort();
                        _SelftTestThread.Join();
                        _SelftTestThread = null;
                    }
                    break;
                case "GoSensorsTest":
                    InitSensorTestItems();
                    _SensorsTestThread = new Thread(SensorsTestProcessing);
                    _SensorsTestThread.IsBackground = true;
                    _SensorsTestThread.Start();
                    break;
                case "StopSensorsTest":
                    if (_SensorsTestThread != null)
                    {
                        _SensorsTestThread.Abort();
                        _SensorsTestThread.Join();
                        _SensorsTestThread = null;
                    }
                    break;
                case "GoWorkingTest":
                    InitWorkingTestItems();
                    _WorkingTestThread = new Thread(WorkingTestProcessing);
                    _WorkingTestThread.IsBackground = true;
                    _WorkingTestThread.Start();
                    break;
                case "StopWorkingTest":
                    if (_WorkingTestThread != null)
                    {
                        _WorkingTestThread.Abort();
                        _WorkingTestThread.Join();
                        _WorkingTestThread = null;
                    }
                    break;
            }
        }

        private bool CanExecuteSetCmd(object obj)
        {
            return true;
        }
        #endregion Set Command

        #region Self Test Processing
        private void SelfTestProcessing()
        {
            try
            {
                #region LED/PD
                CurrentTestItem = SelfTestItems[0];
                if (!_LEDController.IsConnected)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "LED Controller is not connected yet.";
                }
                else
                {
                    int gPDValue = 0, rPDValue = 0; //wPDValue = 0;
                    _LEDController.SetLEDIntensity(Common.LEDTypes.Green, 50);
                    _LEDController.SetLEDStatus(Common.LEDTypes.Green, true);
                    if (_LEDController.GetPDValue())
                    {
                        gPDValue = _LEDController.PDValue;
                    }
                    _LEDController.SetLEDStatus(Common.LEDTypes.Green, false);

                    _LEDController.SetLEDIntensity(Common.LEDTypes.Red, 50);
                    _LEDController.SetLEDStatus(Common.LEDTypes.Red, true);
                    if (_LEDController.GetPDValue())
                    {
                        rPDValue = _LEDController.PDValue;
                    }
                    _LEDController.SetLEDStatus(Common.LEDTypes.Red, false);

                    //_LEDController.SetLEDIntensity(Common.LEDTypes.White, 50);
                    //_LEDController.SetLEDStatus(Common.LEDTypes.White, true);
                    //if (_LEDController.GetPDValue())
                    //{
                    //    wPDValue = _LEDController.PDValue;
                    //}
                    //_LEDController.SetLEDStatus(Common.LEDTypes.White, false);

                    //if (gPDValue > 1000 && rPDValue > 1000 && wPDValue > 1000)
                    double greenMin = SettingsManager.ConfigSettings.CameraCalibSettings.GreenPDMinCount;
                    double redMin = SettingsManager.ConfigSettings.CameraCalibSettings.GreenPDMinCount;
                    if (gPDValue > SettingsManager.ConfigSettings.CameraCalibSettings.GreenPDMinCount && 
                        rPDValue > SettingsManager.ConfigSettings.CameraCalibSettings.RedPDMinCount)
                    {
                        CurrentTestItem.TestResult = TestResults.Ok;
                    }
                    else
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                    }
                    //CurrentTestItem.Comments = string.Format("PD Value when Green, Red, White LED on:{0},{1},{2}", gPDValue, rPDValue, wPDValue);
                    CurrentTestItem.Comments = $"Hardware self test failed to pass LED intensity check. Green value:{gPDValue} (min value: {greenMin}) Red value{rPDValue} (min value: {redMin})";
                }
                #endregion LED/PD

                #region Capture Image
                CurrentTestItem = SelfTestItems[1];
                var cameraA = LucidCameraManager.GetCamera(0);
                var cameraB = LucidCameraManager.GetCamera(1);
                if (cameraA == null)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "None camera found.";
                }
                else if(cameraB == null)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Only 1 camera connected.";
                }
                else
                {
                    try
                    {
                        WriteableBitmap imageA = null;
                        cameraA.ADCBitDepth = 8;
                        cameraA.GrabImage(0.1, CaptureFrameType.Normal, ref imageA);
                        if (imageA == null)
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = string.Format("Camera {0} failed to grab image.", cameraA.SerialNumber);
                        }
                    }
                    catch(Exception ex)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = string.Format("Camera {0} failed to grab image due to: {1}", cameraA.SerialNumber, ex.Message);
                    }
                    finally
                    {
                    }

                    try
                    {
                        WriteableBitmap imageB = null;
                        cameraB.ADCBitDepth = 8;
                        cameraB.GrabImage(0.1, CaptureFrameType.Normal, ref imageB);
                        if (imageB == null)
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = string.Format("Camera {0} failed to grab image.", cameraB.SerialNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = string.Format("Camera {0} failed to grab image due to: {1}", cameraB.SerialNumber, ex.Message);
                    }
                    finally
                    {
                    }
                }
                if (CurrentTestItem.TestResult != TestResults.Failed)
                {
                    CurrentTestItem.TestResult = TestResults.Ok;
                }
                #endregion Capture Image

                #region FC Temper Control
                CurrentTestItem = SelfTestItems[2];
                if (!_MBController.IsConnected)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Mainboard Controller is not connected.";
                }
                else
                {
                    // home X, Y before FC Temper control
                    int xSpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int xAcc = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int ySpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int yAcc = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    _MotionCtroller.HomeMotion(Common.MotionTypes.XStage, xSpeed, xAcc, false);
                    _MotionCtroller.HomeMotion(MotionTypes.YStage, ySpeed, yAcc, false);
                    bool xyAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome && _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome;
                    int waitHomeTime = 0;
                    while (xyAtHome == false)
                    {
                        waitHomeTime++;
                        if (waitHomeTime > 60)  // wait for 60 sec
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = "Failed to Home X & Y";
                            break;
                        }
                        xyAtHome = xyAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome && _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome;
                        Thread.Sleep(1000);
                    }
                    if (CurrentTestItem.TestResult != TestResults.Failed)
                    {
                        var controller = TemperatureController.GetInstance();
                        controller.GetTemper();
                        double crntFCTemper = controller.CurrentTemper;
                        double detaTemper = 10;
                        if (crntFCTemper > 50)
                        {
                            detaTemper = -10;
                        }
                        double tgtFCTemper = crntFCTemper + detaTemper;
                        controller.SetTemperature(tgtFCTemper, 0);
                        Thread.Sleep(10000);
                        controller.GetTemper();
                        if (detaTemper > 0)
                        {
                            if (controller.CurrentTemper < crntFCTemper + detaTemper / 2)
                            {
                                CurrentTestItem.TestResult = TestResults.Failed;
                                CurrentTestItem.Comments = "Heating failed.";
                            }
                        }
                        else
                        {
                            if (controller.CurrentTemper > crntFCTemper + detaTemper * 0.9)
                            {
                                CurrentTestItem.TestResult = TestResults.Failed;
                                CurrentTestItem.Comments = "Cooling failed.";
                            }
                        }

                        if (CurrentTestItem.TestResult != TestResults.Failed)
                        {
                            controller.SetTemperature(crntFCTemper, 0);
                            Thread.Sleep(10000);
                            controller.GetTemper();
                            if (detaTemper > 0)
                            {
                                if (controller.CurrentTemper > crntFCTemper + detaTemper * 0.9)
                                {
                                    CurrentTestItem.TestResult = TestResults.Failed;
                                    CurrentTestItem.Comments = "Cooling failed.";
                                }
                            }
                            else
                            {
                                if (controller.CurrentTemper < crntFCTemper + detaTemper / 2)
                                {
                                    CurrentTestItem.TestResult = TestResults.Failed;
                                    CurrentTestItem.Comments = "Heating failed.";
                                }
                            }
                        }
                        controller.SetControlSwitch(false);
                    }
                }

                if (CurrentTestItem.TestResult != TestResults.Failed)
                {
                    CurrentTestItem.TestResult = TestResults.Ok;
                }
                #endregion FC Temper Control

                #region Get Chiller Temper
                CurrentTestItem = SelfTestItems[3];
                if (_ChillerController.ReadRegisters(Chiller.Registers.ChillerTemper, 1))
                {
                    if(_ChillerController.ChillerTemper > 15)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                    }
                    else
                    {
                        CurrentTestItem.TestResult = TestResults.Ok;
                    }
                    CurrentTestItem.Comments = "Chiller Current Temper: " + _ChillerController.ChillerTemper;
                }
                else
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Failed to get chiller temperature.";
                }
                #endregion Get Chiller Temper

                #region Motion Verify
                CurrentTestItem = SelfTestItems[4];
                if (!_MotionCtroller.IsMotionConnected)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Motion Controller is not connected.";
                }
                else
                {
                    bool xAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome;
                    bool yAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome;
                    int xSpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int xAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int ySpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int yAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int xHomeSpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int xHomeAcc = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int yHomeSpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int yHomeAcc = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    if (!xAtHome)
                    {
                        _MotionCtroller.HomeMotion(MotionTypes.XStage, xHomeSpeed, xHomeAcc, false);
                    }
                    if (!yAtHome)
                    {
                        _MotionCtroller.HomeMotion(MotionTypes.YStage, yHomeSpeed, yHomeAcc, false);
                    }
                    if (!xAtHome || !yAtHome)
                    {
                        int waitTime = 0;
                        do
                        {
                            if (++waitTime > 20)
                            {
                                CurrentTestItem.TestResult = TestResults.Failed;
                                CurrentTestItem.Comments = "Failed to home motion.";
                                break;
                            }
                            Thread.Sleep(1000);
                            xAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome;
                            yAtHome = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome;
                        }
                        while (!xAtHome || !yAtHome);
                    }
                    if (CurrentTestItem.TestResult != TestResults.Failed)
                    {
                        _MotionCtroller.AbsoluteMove(MotionTypes.XStage, (int)(20 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), xSpeed, xAcc);
                        _MotionCtroller.AbsoluteMove(MotionTypes.YStage, (int)(10 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), ySpeed, yAcc);

                        bool xBusy = false;
                        bool yBusy = false;
                        int waitTime = 0;
                        do
                        {
                            if (++waitTime > 20)
                            {
                                CurrentTestItem.TestResult = TestResults.Failed;
                                CurrentTestItem.Comments = "Failed to move motion.";
                                break;
                            }
                            Thread.Sleep(1000);
                            xBusy = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy;
                            yBusy = _MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy;
                        }
                        while (xBusy || yBusy);

                        if (CurrentTestItem.TestResult != TestResults.Failed)
                        {
                            _MotionCtroller.HomeMotion(MotionTypes.XStage, xSpeed, xAcc, false);
                            _MotionCtroller.HomeMotion(MotionTypes.YStage, ySpeed, yAcc, false);
                            CurrentTestItem.TestResult = TestResults.Ok;
                        }
                    }
                }
                #endregion Motion Verify
            }
            catch (ThreadAbortException ex)
            {
                CurrentTestItem.TestResult = TestResults.Aborted;
                CurrentTestItem.Comments = ex.Message;
            }
            catch (Exception ex)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = ex.Message;
            }
            finally
            {
                CurrentTestItem = null;
                TemperatureController.GetInstance().SetControlSwitch(false);
            }
        }

        private void Camera_OnTriggerStartRequested()
        {
            _LEDController.SendCameraTrigger();
        }
        #endregion Self Test Processing

        #region Sensors Test Processing
        private bool HomeXY()
        {
            int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed);
            int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel);
            int xPos = (int)(20 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
            int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed);
            int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel);
            int yPos = (int)(150 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
            _MotionCtroller.HomeMotion(MotionTypes.XStage, xSpeed, xAccel, false);
            _MotionCtroller.HomeMotion(MotionTypes.YStage, ySpeed, yAccel, false);
            int tryCnts = 0;
            while (true)
            {
                Thread.Sleep(1000);
                _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);
                if (_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome)
                {
                    if (_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome)
                    {
                        return true;
                    }
                }
                if (++tryCnts > 60)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Home XY timeout.";
                    return false;
                }
            }
        }
        private void SensorsTestProcessing()
        {
            try
            {
                BufferSensorTest();
                CartridgeSensorTest();

                #region RFID Reader
                CurrentTestItem = SensorsTestItems[2];
                int tryCnts = 0;
                do
                {
                    _RFIDReader.ReadId();
                    Thread.Sleep(100);
                    if (_RFIDReader.ReadIDs != null && _RFIDReader.ReadIDs.Count > 0)
                    {
                        CurrentTestItem.TestResult = TestResults.Ok;
                        CurrentTestItem.Comments = "RFID: " + _RFIDReader.ReadIDs[0].EPC;
                        break;
                    }

                    tryCnts++;
                    if (tryCnts > 10)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        break;
                    }
                } while (_RFIDReader.ReadIDs.Count == 0);
                #endregion RFID Reader

                #region Load Cartridge
                CurrentTestItem = SensorsTestItems[3];
                int pos = (int)Math.Round((SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Cartridge].MotionRange.LimitHigh * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                if (_MotionCtroller.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Unload cartridge failed.";
                    return;
                }
                else
                {
                    CurrentTestItem.TestResult = TestResults.Ok;
                }
                #endregion Load Cartridge

                #region Prepare FC
                CurrentTestItem = SensorsTestItems[4];
                if (!_MotionCtroller.IsMotionConnected)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Motion Controller is not connected.";
                }
                else
                {
                    int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed);
                    int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel);
                    int xPos = (int)(20 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed);
                    int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel);
                    int yPos = (int)(150 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    _MotionCtroller.HomeMotion(MotionTypes.XStage, xSpeed, xAccel, false);
                    _MotionCtroller.HomeMotion(MotionTypes.YStage, ySpeed, yAccel, false);
                    tryCnts = 0;
                    while (true)
                    {
                        Thread.Sleep(1000);
                        _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);
                        if (_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsAtHome)
                        {
                            if (_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsAtHome)
                            {
                                break;
                            }
                        }
                        if (++tryCnts > 40)
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = "Home XY timeout.";
                            break;
                        }
                    }
                    xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed);
                    xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel);
                    ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed);
                    yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel);
                    if (CurrentTestItem.TestResult != TestResults.Failed)
                    {
                        _MotionCtroller.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAccel, false);
                        _MotionCtroller.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAccel, false);
                        tryCnts = 0;
                        bool isXReady = false;
                        bool isYReady = false;
                        while (!isXReady || !isYReady)
                        {
                            Thread.Sleep(1000);
                            _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);

                            int xError = _MotionCtroller.HywireMotionController.EncoderPositions[Hywire.MotionControl.MotorTypes.Motor_X] - xPos;
                            int yError = _MotionCtroller.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_Y] - yPos;
                            if (!_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy)
                            {
                                if (Math.Abs(xError) > 25)  // 2.5 um tolerance, X servo controller has a delayed control
                                {
                                    CurrentTestItem.TestResult = TestResults.Failed;
                                    CurrentTestItem.Comments = "Move X failed.";
                                    break;
                                }
                                else
                                {
                                    isXReady = true;
                                }
                            }
                            if (!_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy)
                            {
                                if(yError != 0)
                                {
                                    CurrentTestItem.TestResult = TestResults.Failed;
                                    CurrentTestItem.Comments = "Move Y failed.";
                                    break;
                                }
                                else
                                {
                                    isYReady = true;
                                }
                            }
                            if (++tryCnts > 60)
                            {
                                CurrentTestItem.TestResult = TestResults.Failed;
                                CurrentTestItem.Comments = "move XY timeout.";
                                break;
                            }
                        }
                    }
                    if(CurrentTestItem.TestResult != TestResults.Failed)
                    {
                        CurrentTestItem.TestResult = TestResults.Ok;
                    }
                }
                #endregion Prepare FC

                #region Clamp FC
                FCClampTest();
                #endregion Clamp FC

                #region Read 2D code
                CurrentTestItem = SensorsTestItems[6];
                if (!_BarCodeReader.IsConnected)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Bar code reader is not connected.";
                }
                else
                {
                    int xPos = (int)(25 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int xSpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int xAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                    int yPos = (int)(95 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int ySpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    int yAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                    _MotionCtroller.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAcc, false);
                    _MotionCtroller.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAcc, true);

                    int waitCnts = 0;
                    while (_MotionCtroller.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy)
                    {
                        if(waitCnts > 100)
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = "Move XY failed.";
                            break;
                        }
                        Thread.Sleep(100);
                    }

                    if(CurrentTestItem.TestResult!= TestResults.Failed)
                    {
                        string barCode = _BarCodeReader.ScanBarCode();
                        if (string.IsNullOrEmpty(barCode))
                        {
                            CurrentTestItem.TestResult = TestResults.Failed;
                            CurrentTestItem.Comments = "Read code failed.";
                        }
                        else
                        {
                            CurrentTestItem.TestResult = TestResults.Ok;
                            CurrentTestItem.Comments = barCode;
                        }
                    }
                }

                #endregion Read 2D code
            }
            catch (ThreadAbortException ex)
            {
                CurrentTestItem.TestResult = TestResults.Aborted;
                CurrentTestItem.Comments = ex.Message;
            }
            catch (Exception ex)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = ex.Message;
            }
            finally
            {
                CurrentTestItem = null;
            }
        }
        private void UpdateBufferSensorStatus(ref bool bufferin, ref bool sipperdown)
        {
            _FluidController.ReadRegisters(FluidController.Registers.OnoffInputs, 1);
            bufferin = !_FluidController.BufferTrayIn;
            sipperdown = !_FluidController.SipperDown;
        }
        private void BufferSensorTest()
        {
            MessageBoxResult msgResult;
            CurrentTestItem = SensorsTestItems[0];
            if (!_FluidController.IsConnected)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = "Fluid Controller is not connected.";
                return;
            }

            bool bufferIn = false;
            bool sipperDown = false;
            UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
            if (sipperDown)
            {
                msgResult = MessageBox.Show("Please move the sipper up, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                if (sipperDown)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Sipper down sensor failed.";
                    return;
                }

                if (bufferIn)
                {
                    msgResult = MessageBox.Show("Please move the Buffer out, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if(msgResult!= MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the Buffer in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }

                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the sipper down, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if(msgResult!= MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!sipperDown)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Sipper down sensor failed.";
                    }
                }
                else
                {
                    msgResult = MessageBox.Show("Please move the buffer in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if(msgResult!= MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the sipper down, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if(msgResult!= MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!sipperDown)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Sipper down sensor failed.";
                    }
                }
            }
            else
            {
                if (bufferIn)
                {
                    msgResult = MessageBox.Show("Please move the Buffer out, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the Buffer in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }

                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the sipper down, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!sipperDown)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Sipper down sensor failed.";
                    }
                }
                else
                {
                    msgResult = MessageBox.Show("Please move the buffer in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!bufferIn)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Buffer in sensor failed.";
                        return;
                    }
                    msgResult = MessageBox.Show("Please move the sipper down, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateBufferSensorStatus(ref bufferIn, ref sipperDown);
                    if (!sipperDown)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Sipper down sensor failed.";
                    }
                }
            }
            if(CurrentTestItem.TestResult != TestResults.Failed)
            {
                CurrentTestItem.TestResult = TestResults.Ok;
            }
        }

        private void UpdateChillerSensorStatus(ref bool cartridgein, ref bool chillerdoor)
        {
            _ChillerController.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
            cartridgein = _ChillerController.CartridgePresent;
            chillerdoor = _ChillerController.CartridgeDoor;
        }
        private void CartridgeSensorTest()
        {
            MessageBoxResult msgResult;
            CurrentTestItem = SensorsTestItems[1];
            if (!_ChillerController.IsConnected)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = "Chiller Controller is not connected.";
                return;
            }

            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            if (_MotionCtroller.AbsoluteMove(MotionTypes.Cartridge, 0, speed, accel, true) == false)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = "Unload cartridge failed.";
                return;
            }

            bool cartridgeIn = false;
            bool chillerDoorClosed = false;
            UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
            if (cartridgeIn)
            {
                if (chillerDoorClosed)
                {
                    msgResult = MessageBox.Show("Please Open the chiller door, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if(msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                    if (chillerDoorClosed)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Chiller door sensor failed.";
                        return;
                    }
                }
                msgResult = MessageBox.Show("Please move the cartridge out, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                if (cartridgeIn)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Cartridge present sensor failed.";
                    return;
                }
                msgResult = MessageBox.Show("Please move the cartridge in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if(msgResult!= MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                if (!cartridgeIn)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Cartridge present sensor failed.";
                    return;
                }
                msgResult = MessageBox.Show("Please close the chiller door, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if(msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                if (!chillerDoorClosed)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Chiller door sensor failed.";
                    return;
                }
            }
            else
            {
                if (chillerDoorClosed)
                {
                    msgResult = MessageBox.Show("Please Open the chiller door, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                    UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                    if (chillerDoorClosed)
                    {
                        CurrentTestItem.TestResult = TestResults.Failed;
                        CurrentTestItem.Comments = "Chiller door sensor failed.";
                        return;
                    }
                }
                msgResult = MessageBox.Show("Please move the cartridge in, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                if (!cartridgeIn)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Cartridge present sensor failed.";
                    return;
                }
                msgResult = MessageBox.Show("Please close the chiller door, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                UpdateChillerSensorStatus(ref cartridgeIn, ref chillerDoorClosed);
                if (!chillerDoorClosed)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Chiller door sensor failed.";
                    return;
                }
            }

            if(CurrentTestItem.TestResult != TestResults.Failed)
            {
                CurrentTestItem.TestResult = TestResults.Ok;
            }
        }

        private void FCClampTest()
        {
            CurrentTestItem = SensorsTestItems[5];
            if (!_MBController.IsConnected)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = "Mainboard controller is not connected.";
            }
            else
            {
                bool fcClamped;
                MessageBoxResult msgResult;
                _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                fcClamped = _MBController.FCClampStatus;
                if (fcClamped)
                {
                    msgResult = MessageBox.Show("Please release FC clamp, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                    if (msgResult != MessageBoxResult.OK)
                    {
                        CurrentTestItem.TestResult = TestResults.Aborted;
                        return;
                    }
                }
                _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                fcClamped = _MBController.FCClampStatus;
                if (fcClamped)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "FC clamp sensor failed.";
                    return;
                }
                msgResult = MessageBox.Show("Please put on FC and clamp it, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    CurrentTestItem.TestResult = TestResults.Aborted;
                    return;
                }
                _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                fcClamped = _MBController.FCClampStatus;
                if (!fcClamped)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "FC clamp sensor failed.";
                    return;
                }
                CurrentTestItem.TestResult = TestResults.Ok;
            }
        }
        #endregion Sensors Test Processing

        #region Working Test Processing
        private void WorkingTestProcessing()
        {
            try
            {
                #region Home XY
                CurrentTestItem = WorkingTestItems[0];
                if (CheckFCClamped() == false)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "FC is not clamped.";
                    return;
                }
                if (HomeXY() == false)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "Home XY failed.";
                }
                CurrentTestItem.TestResult = TestResults.Ok;
                #endregion Home XY

                #region Temperature Loop
                CurrentTestItem = WorkingTestItems[1];
                FCTemperLoopTest();
                #endregion Temperature Loop
            }
            catch (ThreadAbortException ex)
            {
                CurrentTestItem.TestResult = TestResults.Aborted;
                CurrentTestItem.Comments = ex.Message;
            }
            catch (Exception ex)
            {
                CurrentTestItem.TestResult = TestResults.Failed;
                CurrentTestItem.Comments = ex.Message;
            }
            finally
            {
                CurrentTestItem = null;
            }
        }

        private bool CheckFCClamped()
        {
            _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
            bool fcClamped = _MBController.FCClampStatus;
            if (!fcClamped)
            {
                var msgResult = MessageBox.Show("Please put on FC and clamp it, then click OK button", "", MessageBoxButton.OKCancel, MessageBoxImage.None);
                if (msgResult != MessageBoxResult.OK)
                {
                    return false;
                }
                _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                fcClamped = _MBController.FCClampStatus;
                if (!fcClamped)
                {
                    return false;
                }
            }
            return true;
        }
        private bool CheckFCTemper(double tgtTemper, int timeOut, double tolerance)
        {
            var controller = TemperatureController.GetInstance();
            int waitTime = 0;
            double temperDiff = tgtTemper - controller.CurrentTemper;
            controller.SetTemperature(tgtTemper, 0);
            do
            {
                if (waitTime > timeOut)
                {
                    CurrentTestItem.TestResult = TestResults.Failed;
                    CurrentTestItem.Comments = "FC temperature set to 95 C failed.";
                    return false;
                }
                Thread.Sleep(1000);
                temperDiff = tgtTemper - controller.CurrentTemper;
            }
            while (Math.Abs(temperDiff) > tolerance);
            return true;
        }

        private void FCTemperLoopTest()
        {
            var controller = TemperatureController.GetInstance();

            #region Step1: Set to 20 C to check if it can work to low temper
            if(CheckFCTemper(20, 90, 0.2) == false)
            {
                return;
            }
            #endregion Step1: Set to 20 C to check if it can work to low temper

            #region Temper Loop
            System.Timers.Timer logTimer = new System.Timers.Timer(500);
            logTimer.Elapsed += LogTimer_Elapsed;
            logTimer.Start();
            _WorkingTestStartTime = DateTime.Now;
            try
            {
                for (int loop = 0; loop < 40; loop++)
                {
                    CurrentTestItem.Comments = "Temper loop test, current loop:" + loop;
                    Thread.Sleep(30000);
                    if(CheckFCTemper(95, 50, 0.2) == false)
                    {
                        return;
                    }
                    Thread.Sleep(30000);
                    if(CheckFCTemper(60, 60, 0.2) == false)
                    {
                        return;
                    }
                    Thread.Sleep(30000);
                    if(CheckFCTemper(20, 120, 0.2) == false)
                    {
                        return;
                    }
                }
            }
            catch
            {

            }
            finally
            {
                TemperatureController.GetInstance().SetControlSwitch(false);
                if(CurrentTestItem.TestResult != TestResults.Failed)
                {
                    CurrentTestItem.TestResult = TestResults.Ok;
                }
                logTimer.Stop();
                using (FileStream fs = new FileStream(string.Format("{0}TemperatureLoopLog.csv",DateTime.Now.ToString("yyMMddhhmmss")), FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.WriteLine("Time(s),FC Temper,HeatSink Temper");
                        for(int i = 0; i < _FCTemperLog.Count; i++)
                        {
                            writer.WriteLine(string.Format("{0},{1},{2}", _FCLogTimes[i], _FCTemperLog[i], _FCHeatSinkTemperLog[i]));
                        }
                        writer.Flush();
                    }
                }
            }
            #endregion Temper Loop

        }

        private void LogTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            double time = (DateTime.Now - _WorkingTestStartTime).TotalSeconds;
            _FCLogTimes.Add(time);
            _FCTemperLog.Add(TemperatureController.GetInstance().CurrentTemper);
            _FCHeatSinkTemperLog.Add(_MBController.HeatSinkTemper);
        }
        #endregion Working Test Processing

        #region LoopMoveStageCaptureImageCmd
        private RelayCommand _LoopMoveCaptureCmd;
        public RelayCommand LoopMoveCaptureCmd
        {
            get
            {
                if(_LoopMoveCaptureCmd==null)
                {
                    _LoopMoveCaptureCmd = new RelayCommand(ExecuteLoopMoveCaptureCmd, CanExecuteLoopMoveCaptureCmd);
                }
                return _LoopMoveCaptureCmd;
            }
        }

        private void ExecuteLoopMoveCaptureCmd(object obj)
        {
            Task.Factory.StartNew(() =>
            {
                string logFileName = "Move Y stage loop test log.csv";
                LogToFile(logFileName, FileMode.Create, "Start Move Y stage test.\n");

                double imagingPos = _MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage];
                double stepPos = 0.85;
                double tgtPos = imagingPos;
                #region Home Y, Clear Y encoder position
                int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed);
                int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel);
                int yHomeSpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                int yHomeAccel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                _MotionCtroller.HomeMotion(MotionTypes.YStage, yHomeSpeed, yHomeAccel, true);
                Thread.Sleep(300);  // wait for Y stage stable
                _MotionCtroller.HywireMotionController.ResetEncoderPosition(Hywire.MotionControl.MotorTypes.Motor_Y);
                _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                double yCurrentPos = Math.Round(_MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                double yEncoderPos = Math.Round(_MotionCtroller.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                LogToFile(logFileName, FileMode.Append, string.Format("Y Homed, Current: {0}, Encoder: {1}\r\n", yCurrentPos, yEncoderPos));
                #endregion Home Y, Clear Y encoder position

                #region Loops
                LogToFile(logFileName, FileMode.Append, string.Format("Current Pos,Encoder Pos,Pos Error\n"));
                string pdAverageStoreFileName = "PD Average value VS Image Pixel Average Value.csv";
                File.WriteAllText(pdAverageStoreFileName, "PD Sample Val, PD Aver Val, Image Pixel Aver\r\n");
                for (int loop = 0; loop < 100; loop++)
                {
                    if (loop == 0)
                    {
                        _CameraVm.SelectedCamera = _CameraVm.EthernetCameraOptions[0];
                    }
                    else if (loop == 50)
                    {
                        _CameraVm.SelectedCamera = _CameraVm.EthernetCameraOptions[1];
                    }
                    #region move to imaging position
                    tgtPos = imagingPos;
                    _MotionCtroller.AbsoluteMove(MotionTypes.YStage, (int)(tgtPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), ySpeed, yAccel, true);
                    Thread.Sleep(300);  // wait for Y stage stable
                    _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                    yCurrentPos = Math.Round(_MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                    yEncoderPos = Math.Round(_MotionCtroller.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                    LogToFile(logFileName, FileMode.Append, string.Format("{0},{1},{2}\r\n", yCurrentPos, yEncoderPos, yCurrentPos - yEncoderPos));
                    #endregion move to imaging position
                    #region take & save image
                    for(int wc = 0; wc < 100; wc++)
                    {
                        _CameraVm.CaptureCmd.Execute(null);
                        while (_CameraVm.WorkingStatus != CameraStatusEnums.Idle)
                        {
                            Thread.Sleep(1);
                        }
                        while (_GalleryVm.ActiveFile == null)
                        {
                            Thread.Sleep(1);
                        }
                        string fileName = string.Format("Loop{3}-Capture{4}-PD{5}-Current{0}-Encoder{1}-{2}.tif", yCurrentPos, yEncoderPos, DateTime.Now.ToString("yyMMdd-hhmmss"), loop, wc, _GalleryVm.ActiveFile.ImageInfo.MixChannel.PDValue);
                        TheDispatcher.BeginInvoke((Action)delegate
                        {
                            ImageProcessing.Save(fileName, _GalleryVm.ActiveFile.SourceImage, _GalleryVm.ActiveFile.ImageInfo, false);
                            #region Get image pixel average & PD average
                            ImageStatistics imageStatistics = new ImageStatistics();
                            var pixelAverValue = imageStatistics.GetAverage(_GalleryVm.ActiveFile.SourceImage.Clone());
                            _LEDController.ReadRegisters(LEDController.Registers.PDSampleData, 1);
                            int pdAverageCount = (int)(_CameraVm.ExposureTime / 0.005) - 1;
                            uint pdTotal = 0;
                            for (int i = 1; i < pdAverageCount; i++)
                            {
                                pdTotal += _LEDController.PDCurve[i];
                            }
                            var pdAver = pdTotal / (pdAverageCount - 1);
                            File.AppendAllText(pdAverageStoreFileName, string.Format("{0},{1},{2}\r\n", _GalleryVm.ActiveFile.ImageInfo.MixChannel.PDValue, pdAver, pixelAverValue));
                            #endregion Get image pixel average & PD average

                            _GalleryVm.ActiveFile.IsDirty = false;
                            _GalleryVm.CloseFile(_GalleryVm.ActiveFile);
                        });
                        while (_GalleryVm.ActiveFile != null)
                        {
                            Thread.Sleep(1);
                        }
                    }
                    #endregion take & save image

                    for (int step = 0; step < 50; step++)
                    {
                        tgtPos = imagingPos + step * stepPos;
                        _MotionCtroller.AbsoluteMove(MotionTypes.YStage, (int)(tgtPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), ySpeed, yAccel, true);
                        Thread.Sleep(300);  // wait for Y stage stable
                        _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                        yCurrentPos = Math.Round(_MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                        yEncoderPos = Math.Round(_MotionCtroller.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                        LogToFile(logFileName, FileMode.Append, string.Format("{0},{1},{2}\r\n", yCurrentPos, yEncoderPos, yCurrentPos - yEncoderPos));
                    }

                    // go back to 0
                    //_MotionCtroller.AbsoluteMove(MotionTypes.YStage, 0, ySpeed, yAccel, true);
                    //Thread.Sleep(300);  // wait for Y stage stable
                    //_MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                    //yCurrentPos = Math.Round(_MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                    //yEncoderPos = Math.Round(_MotionCtroller.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                    //LogToFile(logFileName, FileMode.Append, string.Format("{0},{1},{2}\r\n", yCurrentPos, yEncoderPos, yCurrentPos - yEncoderPos));

                    // home
                    _MotionCtroller.HomeMotion(MotionTypes.YStage, yHomeSpeed, yHomeAccel, true);
                    Thread.Sleep(1000);
                    _MotionCtroller.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                    yCurrentPos = Math.Round(_MotionCtroller.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                    yEncoderPos = Math.Round(_MotionCtroller.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                    LogToFile(logFileName, FileMode.Append, string.Format("{0},{1},{2}\r\n", yCurrentPos, yEncoderPos, yCurrentPos - yEncoderPos));
                }
                #endregion Loops
            });
        }

        private bool CanExecuteLoopMoveCaptureCmd(object obj)
        {
            return true;
        }

        private void LogToFile(string filePath, FileMode mode, string log)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, mode))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.Write(log);
                        writer.Flush();
                    }
                    fs.Flush();
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        #endregion LoopMoveStageCaptureImageCmd
    }

    public enum TestResults
    {
        NA,
        Testing,
        Ok,
        Failed,
        Aborted,
    }
    public class HardwareTestViewModel : ViewModelBase
    {
        #region Private Fields
        private string _TestName;
        private string _TestDescription;
        private TestResults _TestResult;
        private string _Comments;
        #endregion Private Fields

        public HardwareTestViewModel(string name, string desc)
        {
            _TestName = name;
            _TestDescription = desc;
            _TestResult = TestResults.NA;
        }

        #region Public Properties
        public string TestName
        {
            get { return _TestName; }
            set
            {
                if (_TestName != value)
                {
                    _TestName = value;
                    RaisePropertyChanged(nameof(TestName));
                }
            }
        }
        public string TestDescription
        {
            get { return _TestDescription; }
            set
            {
                if (_TestDescription != value)
                {
                    _TestDescription = value;
                    RaisePropertyChanged(nameof(TestDescription));
                }
            }
        }
        public TestResults TestResult
        {
            get { return _TestResult; }
            set
            {
                if (_TestResult != value)
                {
                    _TestResult = value;
                    RaisePropertyChanged(nameof(TestResult));
                }
            }
        }
        public string Comments
        {
            get { return _Comments; }
            set
            {
                if (_Comments != value)
                {
                    _Comments = value;
                    RaisePropertyChanged(nameof(Comments));
                }
            }
        }
        #endregion Public Properties
    }
}
