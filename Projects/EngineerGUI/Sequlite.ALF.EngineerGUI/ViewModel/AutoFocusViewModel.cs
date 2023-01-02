using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class AutoFocusViewModel : ViewModelBase
    {
        #region Private Fields
        private double _ZLimitL = 250;
        private double _ZLimitH = 280;
        private double _ZSpeed = 800;
        private double _ZAccel = 1500;

        private int _RoiLeft = 0;
        private int _RoiTop = 0;
        private int _RoiWidth = 0;
        private int _RoiHeight = 0;
        private double _Exposure = 1;

        private LEDTypes _SelectedLED;
        private LEDTypes _SelectedOffsetLED;
        private uint _Intensity = 100;

        private StringBuilder _Info;
        private bool _IsAutoFocusing;
        private bool _IsScanning;
        private bool _IsCancelScan = false;
        private AutoFocusCommandBase _AutoFocusCmd;
        //private AutofocusOnFluoV1 _AutoFocusFluoCmd;
        //private int _PDValue;
        //private int _LEDFailure;
        //private MainBoard.Mainboard _MainBoard;
        private double _Offset;
        private double _FocusedFiducial;
        //private double _FocusedFluo;
        private int _SelectedOffsetFilter;
        private string _SelectedAFSurface;
        private bool _IsAFSucc;
        private bool _IsScanOnly;
        private double ScanInterval;
        private List<Point> stdPts = new List<Point>();
        private Point[] graPts;
        private int ZposCount;
        private double ScanMaxStd;
        private double ZposMax;
        #endregion Private Fields

        #region Public Properties

        public double Offset
        {
            get { return _Offset; }
            set
            {
                if(_Offset != value)
                {
                    _Offset = value;
                }
            }
        }
        public double ZLimitL
        {
            get { return _ZLimitL; }
            set
            {
                if (_ZLimitL != value)
                {
                    _ZLimitL = value;
                    RaisePropertyChanged(nameof(ZLimitL));
                }
            }
        }
        public double ZLimitH
        {
            get { return _ZLimitH; }
            set
            {
                if (_ZLimitH != value)
                {
                    _ZLimitH = value;
                    RaisePropertyChanged(nameof(ZLimitH));
                }
            }
        }
        public double ZSpeed
        {
            get { return _ZSpeed; }
            set
            {
                if (_ZSpeed != value)
                {
                    _ZSpeed = value;
                    RaisePropertyChanged(nameof(ZSpeed));
                }
            }
        }
        public double ZAccel
        {
            get { return _ZAccel; }
            set
            {
                if (_ZAccel != value)
                {
                    _ZAccel = value;
                    RaisePropertyChanged(nameof(ZAccel));
                }
            }
        }
        public int RoiLeft
        {
            get { return _RoiLeft; }
            set
            {
                if (_RoiLeft != value)
                {
                    _RoiLeft = value;
                    RaisePropertyChanged(nameof(RoiLeft));
                }
            }
        }
        public int RoiTop
        {
            get { return _RoiTop; }
            set
            {
                if (_RoiTop != value)
                {
                    _RoiTop = value;
                    RaisePropertyChanged(nameof(RoiTop));
                }
            }
        }
        public int RoiWidth
        {
            get { return _RoiWidth; }
            set
            {
                if (_RoiWidth != value)
                {
                    _RoiWidth = value;
                    RaisePropertyChanged(nameof(RoiWidth));
                }
            }
        }
        public int RoiHeight
        {
            get { return _RoiHeight; }
            set
            {
                if (_RoiHeight != value)
                {
                    _RoiHeight = value;
                    RaisePropertyChanged(nameof(RoiHeight));
                }
            }
        }
        public double Exposure
        {
            get { return _Exposure; }
            set
            {
                if (_Exposure != value)
                {
                    _Exposure = value;
                    RaisePropertyChanged(nameof(Exposure));
                }
            }
        }
        public LEDTypes SelectedLED
        {
            get { return _SelectedLED; }
            set
            {
                if (_SelectedLED != value)
                {
                    _SelectedLED = value;
                    RaisePropertyChanged(nameof(SelectedLED));
                }
            }
        }
        public uint Intensity
        {
            get { return _Intensity; }
            set
            {
                if (_Intensity != value)
                {
                    _Intensity = value;
                    RaisePropertyChanged(nameof(Intensity));
                }
            }
        }
        public LEDTypes[] LEDOptions { get; }
        public LEDTypes[] OffsetLEDOptions { get; }
        public string[] AFSurfaceOptions { get; }
        public int[] FilterOptions { get; }
        public string SelectedAFSurface
        {
            get { return _SelectedAFSurface; }
            set
            {
                if( _SelectedAFSurface != value)
                {
                    _SelectedAFSurface = value;
                    RaisePropertyChanged(nameof(SelectedAFSurface));
                }
            }
        }

        public LEDTypes SelectedOffsetLED
        {
            get { return _SelectedOffsetLED; }
            set
            {
                if (_SelectedOffsetLED != value)
                {
                    _SelectedOffsetLED = value;
                    RaisePropertyChanged(nameof(SelectedOffsetLED));
                }
            }
        }
        public string Information
        {
            get { return _Info.ToString(); }
        }
        public bool IsAutoFocusing
        {
            get { return _IsAutoFocusing; }
            set
            {
                if (_IsAutoFocusing != value)
                {
                    _IsAutoFocusing = value;
                    RaisePropertyChanged(nameof(IsAutoFocusing));
                }
            }
        }
        public bool IsScanning
        {
            get { return _IsScanning; }
            set
            {
                if (_IsScanning != value)
                {
                    _IsScanning = value;
                    RaisePropertyChanged(nameof(IsScanning));
                }
            }
        }

        public bool IsCancelScan
        {
            get { return _IsCancelScan; }
            set
            {
                if (_IsCancelScan != value)
                {
                    _IsCancelScan = value;
                    RaisePropertyChanged(nameof(IsCancelScan));
                }
            }
        }
        public ObservableDataSource<Point> StdDevLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> GradientLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> StdDevSave { get; } = new ObservableDataSource<Point>();
        public int SelectedOffsetFilter
        {
            get { return _SelectedOffsetFilter; }
            set
            {
                if (_SelectedOffsetFilter != value)
                {
                    _SelectedOffsetFilter = value;
                    RaisePropertyChanged(nameof(SelectedOffsetFilter));
                }
            }
        }
        #endregion Public Properties
        CameraViewModel CameraVM { get; }
        RecipeViewModel RecipeVM { get; }
        MotionViewModel MotionVM { get; }
        MainBoardViewModel MainBoardVM { get; }
        public AutoFocusViewModel(CameraViewModel cameraVM ,
            RecipeViewModel recipeVM,
            MotionViewModel motionVM,
            MainBoardViewModel mainBoardVM)
        {
            CameraVM = cameraVM;
            RecipeVM = recipeVM;
            MotionVM = motionVM;
            MainBoardVM = mainBoardVM;
            LEDOptions = new LEDTypes[3];
            LEDOptions[0] = LEDTypes.Green;
            LEDOptions[1] = LEDTypes.Red;
            LEDOptions[2] = LEDTypes.White;
            SelectedLED = LEDOptions[2];
            _Info = new StringBuilder();
            OffsetLEDOptions = new LEDTypes[2];
            OffsetLEDOptions[0] = LEDTypes.Green;
            OffsetLEDOptions[1] = LEDTypes.Red;
            SelectedOffsetLED = OffsetLEDOptions[0];
            FilterOptions = new int[] { 1, 2, 3, 4 };
            SelectedOffsetFilter = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetFilterIndex;
            AFSurfaceOptions = new string[] { "Button", "Top" };
            SelectedAFSurface = "Button";
        }

        public void Initialize()
        {
            RoiLeft = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.X;
            RoiTop = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Y;
            RoiWidth = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Width;
            RoiHeight = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Height;
            SelectedLED = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDType;
            Intensity = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDIntensity;
            Exposure = SettingsManager.ConfigSettings.AutoFocusingSettings.ExposureTime;
            ZSpeed = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageSpeed;
            ZAccel = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageAccel;
            SelectedOffsetLED = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetLEDType;

        }

        #region Start Command
        private RelayCommand _StartCmd;
        public ICommand StartCmd
        {
            get
            {
                if (_StartCmd == null)
                {
                    _StartCmd = new RelayCommand(ExecuteScannerCheckCmd, CanExecuteStartCmd);
                }
                return _StartCmd;
            }
        }

        private void ExecuteScannerCheckCmd(object obj)
        {
            if (MainBoardVM.IsGLEDOnSet || MainBoardVM.IsRLEDOnSet || MainBoardVM.IsWLEDOnSet || CameraVM.ActiveCamera.IsAcqRunning)
            {
                MessageBox.Show("LED was on or camera is running, please turn off LEDs or stop live mode before start AF");
                return;
            }

            IsAutoFocusing = true;
            _Info.Clear();
            _Info.Append("ImageSystemCheck started:\n");
            RaisePropertyChanged(nameof(Information));
            var checkTask = new Task<bool>(() => {
                ISeqApp seqApp = SeqAppFactory.GetSeqApp();
                return seqApp.CreateSystemCheckInterface().ImageSystemCheck();
            });
            checkTask.Start();
            checkTask.GetAwaiter().OnCompleted(() =>
            {
                _Info.Append(checkTask.Result ? "ImageSystemCheck succeeded:\n" : "ImageSystemCheck failed:\n");
                RaisePropertyChanged(nameof(Information));
                IsAutoFocusing = false;
            });
        }

        private void ExecuteStartCmd(object obj)
        {
            if (MainBoardVM.IsGLEDOnSet || MainBoardVM.IsRLEDOnSet || MainBoardVM.IsWLEDOnSet || CameraVM.ActiveCamera.IsAcqRunning)
            {
                MessageBox.Show("LED was on or camera is running, please turn off LEDs or stop live mode before start AF");
                return;
            }

            ScanMaxStd = 0;
            stdPts.Clear();
            AutoFocusSettings FiducialAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
            //FiducialAFSettings.FiducialVersion = SettingsManager.ConfigSettings.AutoFocusingSettings.FiducialVersion;
            ICamera camera = CameraVM.ActiveCamera;
            FiducialAFSettings.ExposureTime = Exposure;
            FiducialAFSettings.LEDIntensity = Intensity;
            FiducialAFSettings.LEDType = SelectedLED;
            Int32Rect roi = new Int32Rect();
            if (!MainBoardVM.IsMachineRev2) 
            { 
                roi.X = (camera.ImagingColumns - 1) - (RoiLeft + RoiWidth); 
            }
            else { roi.X = RoiLeft; }
            roi.Y = RoiTop;
            roi.Width = RoiWidth;
            roi.Height = RoiHeight;
            FiducialAFSettings.ROI = roi;
            FiducialAFSettings.ZstageLimitH = ZLimitH;
            FiducialAFSettings.ZstageLimitL = ZLimitL;
            FiducialAFSettings.ZstageAccel = ZAccel;
            FiducialAFSettings.ZstageSpeed = ZSpeed;
            FiducialAFSettings.ExtraExposure = SettingsManager.ConfigSettings.CameraDefaultSettings.ExtraExposure;
            //FiducialAFSettings.FilterIndex = SettingsManager.ConfigSettings.AutoFocusingSettings.FilterIndex;
            //FiducialAFSettings.RotationAngle = SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle;
            _IsAFSucc = false;
            //_IsScanOnly = false;
            ScanInterval = 1;
            FiducialAFSettings.ScanInterval = ScanInterval;
            ZposCount = 0;
            //FiducialAFSettings.IsScanonly = _IsScanOnly;
            //FiducialAFSettings.IsRecipe = false;
            //FiducialAFSettings.IsHConly = false;
            if (!MainBoardVM.IsMachineRev2)
            {
                _AutoFocusCmd = new AutoFocusCommand(TheDispatcher, MotionVM.MotionController, CameraVM.ActiveCamera, MainBoardVM.MainBoard, FiducialAFSettings );
            }
            else
            {
                ScanInterval = 3; //V2 Scan step size larger
                _AutoFocusCmd = new AutoFocusCommand2(TheDispatcher, MotionVM.MotionController, CameraVM.ActiveCamera, MainBoardVM.LEDController, FiducialAFSettings);
            }
            _AutoFocusCmd.Completed += _AutoFocusCmd_Completed;
            _AutoFocusCmd.OnImageSampled += _AutoFocusCmd_OnImageSampled;
            _AutoFocusCmd.Start();
            _Info.Clear();
            _Info.Append("Auto focusing starts:\n");
            RaisePropertyChanged(nameof(Information));
            IsAutoFocusing = true;
        }

        private void _AutoFocusCmd_OnImageSampled(double zPos, double sharpness)
        {            
            //if (_IsScanOnly)
            //{
                stdPts.Add(new Point(zPos, sharpness));
                //stdPts[ZposCount] = new Point(zPos, sharpness);
                //ZposCount += 1;
                if (sharpness > ScanMaxStd) { ScanMaxStd = sharpness; ZposMax = zPos; }
            //}
            _Info.AppendFormat("{2}, Z pos:{0:00.00}, Sharpness:{1:00.00}\n", zPos, sharpness, stdPts.Count);
            RaisePropertyChanged(nameof(Information));
        }

        private void _AutoFocusCmd_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                _IsAFSucc = true;
                _Info.Append("Auto focusing succeeded.\n");
                //if (_IsScanOnly)
                //{
                    StdDevSave.AppendMany(stdPts);
                    for (int i = 0; i < stdPts.Count; i++) { StdDevLine.AppendAsync(TheDispatcher, new Point(stdPts[i].X, stdPts[i].Y/ ScanMaxStd)); } //graPts[i].Y /= graMax;
                    //StdDevLine.AppendMany(stdPts);
                    _Info.Append(string.Format("Focus at:{0:00.00}um, Max STDV:{1:00.00} \n", ZposMax, ScanMaxStd));
                //}
            }
            else if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _Info.Append("Auto focusing aborted.\n");
            }
            else
            {
                _Info.Append(string.Format("Auto focusing failed. Exception:{0}, LED Failure:{1}, Failed to Capture Image{2}", _AutoFocusCmd.ExceptionMessage, _AutoFocusCmd.IsFailedToSetLED, _AutoFocusCmd.IsFailedCaptureImage));
            }
            RaisePropertyChanged(nameof(Information));
            _AutoFocusCmd.OnImageSampled -= _AutoFocusCmd_OnImageSampled;
            IsAutoFocusing = false;
            _IsScanOnly = false;
            //CameraVM.ActiveCamera.StopAcquisition();
            _AutoFocusCmd.Completed -= _AutoFocusCmd_Completed;
            _AutoFocusCmd = null;
        }

        private bool CanExecuteStartCmd(object obj)
        {
            return !IsAutoFocusing && !IsScanning;
        }
        #endregion Start Command

        #region Stop Command
        private RelayCommand _StopCmd;
        public ICommand StopCmd
        {
            get
            {
                if (_StopCmd == null)
                {
                    _StopCmd = new RelayCommand(ExecuteStopCmd, CanExecuteStopCmd);
                }
                return _StopCmd;
            }
        }

        private void ExecuteStopCmd(object obj)
        {
            _AutoFocusCmd.Abort();
        }

        private bool CanExecuteStopCmd(object obj)
        {
            return IsAutoFocusing;
        }
        #endregion Stop Command

        #region Start Scan Command
        private RelayCommand _StartScanCmd;
        public ICommand StartScanCmd
        {
            get
            {
                if (_StartScanCmd == null)
                {
                    _StartScanCmd = new RelayCommand(ExecuteStartScanCmd, CanExecuteStartScanCmd);
                }
                return _StartScanCmd;
            }
        }

        private void ExecuteStartScanCmd(object obj)
        {
            if (MainBoardVM.IsGLEDOnSet || MainBoardVM.IsRLEDOnSet || MainBoardVM.IsWLEDOnSet || CameraVM.ActiveCamera.IsAcqRunning)
            {
                MessageBox.Show("LED was on or camera is running, please turn off LEDs or stop live mode before start AF");
                return;
            }
            //Just Scan no HillClimb
            ScanInterval = 0.5;
            _IsScanOnly = false;
            ZposCount = 0;
            ScanMaxStd = 0;
            ZposMax = 0;
            _IsAFSucc = false;
            stdPts.Clear();
            //stdPts = new Point[(int)((ZLimitH - ZLimitL) / ScanInterval + 1)];
            graPts = new Point[(int)((ZLimitH - ZLimitL) / ScanInterval + 1)];
            //Setting of AF Scan
            AutoFocusSettings FiducialAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
            ICamera camera = CameraVM.ActiveCamera;
            //FiducialAFSettings.FiducialVersion = SettingsManager.ConfigSettings.AutoFocusingSettings.FiducialVersion;
            FiducialAFSettings.ExposureTime = Exposure;
            FiducialAFSettings.LEDIntensity = Intensity;
            FiducialAFSettings.LEDType = SelectedLED;
            Int32Rect roi = new Int32Rect();
            if (!MainBoardVM.IsMachineRev2) { roi.X = (camera.ImagingColumns - 1) - (RoiLeft + RoiWidth); }
            else { roi.X = RoiLeft; }
            roi.Y = RoiTop;
            roi.Width = RoiWidth;
            roi.Height = RoiHeight;
            FiducialAFSettings.ROI = roi;
            FiducialAFSettings.ZstageLimitH = ZLimitH;
            FiducialAFSettings.ZstageLimitL = ZLimitL;
            FiducialAFSettings.ZstageAccel = ZAccel;
            FiducialAFSettings.ZstageSpeed = ZSpeed;
            FiducialAFSettings.ExtraExposure = SettingsManager.ConfigSettings.CameraDefaultSettings.ExtraExposure;
            //FiducialAFSettings.FilterIndex = SettingsManager.ConfigSettings.AutoFocusingSettings.FilterIndex;
            //FiducialAFSettings.RotationAngle = SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle;
            _Info.Clear();
            FiducialAFSettings.ScanInterval = ScanInterval;
            FiducialAFSettings.IsScanonly = _IsScanOnly;
            //FiducialAFSettings.IsHConly = false;
            //FiducialAFSettings.IsRecipe = false;
            if (!MainBoardVM.IsMachineRev2)
            {
                _AutoFocusCmd = new AutoFocusCommand(TheDispatcher, MotionVM.MotionController, CameraVM.ActiveCamera, MainBoardVM.MainBoard, FiducialAFSettings);
                _Info.Append(string.Format("Scanning Starts: Temp:{0:F2} ROI:{1} {2} {3} {4}\n", MainBoardVM.MainBoard.ChemiTemper, RoiLeft, RoiTop, RoiWidth, RoiHeight));
            }
            else
            {
                _AutoFocusCmd = new AutoFocusCommand2(TheDispatcher, MotionVM.MotionController, CameraVM.ActiveCamera, MainBoardVM.LEDController, FiducialAFSettings);
                _Info.Append(string.Format("Scanning Starts: Temp:{0:F2} ROI:{1} {2} {3} {4}\n", MainBoardVM.ChemiTemperGet, RoiLeft, RoiTop, RoiWidth, RoiHeight));
            }
            _AutoFocusCmd.Completed += _AutoFocusCmd_Completed;
            _AutoFocusCmd.OnImageSampled += _AutoFocusCmd_OnImageSampled;
            _AutoFocusCmd.Start();
            RaisePropertyChanged(nameof(Information));
            IsAutoFocusing = true;
        }

        private bool CanExecuteStartScanCmd(object obj)
        {
            return !IsScanning && !IsAutoFocusing;
        }
        #endregion Start Scan Command

        #region Cancel Scan Command
        private RelayCommand _CancelScanCmd;

        public ICommand CancelScanCmd
        {
            get
            {
                if (_CancelScanCmd == null)
                {
                    _CancelScanCmd = new RelayCommand(ExecuteCancelScanCmd, CanExecuteCancelScanCmd);
                }
                return _CancelScanCmd;
            }
        }
        private void ExecuteCancelScanCmd(object obj)
        {
            _AutoFocusCmd.Abort();
        }
        private bool CanExecuteCancelScanCmd(object obj)
        {
            return IsAutoFocusing;
        }
        #endregion Cancel Scan Command

        #region Save Data Command
        private RelayCommand _SaveDataCmd;
        public ICommand SaveDataCmd
        {
            get
            {
                if (_SaveDataCmd == null)
                {
                    _SaveDataCmd = new RelayCommand(ExecuteSaveDataCmd, CanExecuteSaveDataCmd);
                }
                return _SaveDataCmd;
            }
        }

        private void ExecuteSaveDataCmd(object obj)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "csv|*.csv";
            if (saveDialog.ShowDialog() == true)
            {
                using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine("Z Pos/um,StdDev,Gradient");
                        for (int i = 0; i < StdDevLine.Collection.Count; i++)
                        {
                            sw.WriteLine(string.Format("{0},{1}", StdDevLine.Collection.ElementAt(i).X, StdDevSave.Collection.ElementAt(i).Y));
                        }
                        sw.WriteLine(String.Format("ROI:,{0},{1},{2},{3}, ,Expo:,{4}", RoiLeft, RoiTop, RoiWidth, RoiHeight, Exposure));
                    }
                }
            }
        }

        private bool CanExecuteSaveDataCmd(object obj)
        {
            return !IsScanning;
        }
        #endregion Save Data Command

        #region Calculate Offset Command
        private RelayCommand _CalculateOffsetCmd;
        public ICommand CalculateOffsetCmd
        {
            get
            {
                if (_CalculateOffsetCmd == null)
                {
                    _CalculateOffsetCmd = new RelayCommand(ExecuteCalculateOffsetCmd, CanExecuteCalculateOffsetCmd);
                }
                return _CalculateOffsetCmd;
            }
        }

        private void ExecuteCalculateOffsetCmd(object obj)
        {
            if (MainBoardVM.IsGLEDOnSet || MainBoardVM.IsRLEDOnSet || MainBoardVM.IsWLEDOnSet ||
                LucidCameraManager.GetCamera(0).IsAcqRunning || LucidCameraManager.GetCamera(1).IsAcqRunning)
            {
                MessageBox.Show("LED was on or camera is running, please turn off LEDs or stop live mode before start AF");
                return;
            }
            TaskFactory tf = new TaskFactory();
            tf.StartNew(() =>
            {
                if(SelectedAFSurface == "Button") { _Offset = RecipeVM.B_Offset; }
                else { _Offset = RecipeVM.T_Offset; }
                ExecuteStartCmd(obj);
                while (_IsAutoFocusing)
                {
                    Thread.Sleep(1);
                }
                if (_IsAFSucc)
                {
                    AutoFocusSettings OffsetAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                    ICamera camera = CameraVM.ActiveCamera;
                    Int32Rect roi = new Int32Rect();
                    roi.X = 1800;
                    roi.Y = 1800;
                    roi.Width = 800;
                    roi.Height = 800;
                    OffsetAFSettings.ROI = roi;
                    _FocusedFiducial = MotionVM.MotionController.ZCurrentPos;
                    OffsetAFSettings.ZstageLimitH = _FocusedFiducial + 5 - _Offset;
                    OffsetAFSettings.ZstageLimitL = _FocusedFiducial - 5 - _Offset;
                    OffsetAFSettings.ZstageAccel = ZAccel;
                    OffsetAFSettings.ZstageSpeed = ZSpeed;
                    OffsetAFSettings.LEDType = SelectedOffsetLED;
                    if (_SelectedOffsetLED == LEDTypes.Green)
                    {
                        OffsetAFSettings.LEDIntensity = 50;
                    }
                    else if (_SelectedOffsetLED == LEDTypes.Red)
                    {
                        OffsetAFSettings.LEDIntensity = 50;
                    }
                    OffsetAFSettings.OffsetFilterIndex = SelectedOffsetFilter;
                    OffsetAFSettings.ExposureTime = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetExposureTime;
                    OffsetAFSettings.Reference0 = _FocusedFiducial - _Offset;
                    if (!MainBoardVM.IsMachineRev2)
                    {
                        _AutoFocusCmd = new AutofocusOnFluoV1(TheDispatcher, MotionVM.MotionController, CameraVM.ActiveCamera, MainBoardVM.MainBoard, OffsetAFSettings, _FocusedFiducial);
                    }
                    else
                    {
                        ICamera corrCamera;
                        if (OffsetAFSettings.OffsetFilterIndex == 1 || OffsetAFSettings.OffsetFilterIndex == 3) 
                        { 
                            corrCamera = LucidCameraManager.GetCamera(0).Channels.Contains("1") ? LucidCameraManager.GetCamera(0) : LucidCameraManager.GetCamera(1);
                        }
                        else { corrCamera = LucidCameraManager.GetCamera(0).Channels.Contains("2") ? LucidCameraManager.GetCamera(0) : LucidCameraManager.GetCamera(1); ; }
                        _AutoFocusCmd = new AutofocusOnFluoV2(TheDispatcher, MotionVM.MotionController, corrCamera, MainBoardVM.LEDController, OffsetAFSettings, _FocusedFiducial);
                    }

                    _AutoFocusCmd.Completed += _AutoFocusFluoCmd_Completed;
                    _AutoFocusCmd.OnImageSampled += _AutoFocusFluoCmd_OnImageSampled;
                    _AutoFocusCmd.Start();
                    _Info.Append("Autofocus for FiducialOffset starts:\n");
                    RaisePropertyChanged(nameof(Information));
                }
                else { _Info.Append("Autofocus for FiducialOffset failed, retry.\n");
                    MessageBox.Show("Autofocus for FiducialOffset failed, retry."); }
            });
        }

        private void _AutoFocusFluoCmd_OnImageSampled(double zPos, double FOGScore)
        {
            _Info.AppendFormat("Z pos:{0:00.00}, IF:{1:00.000}\n", zPos, FOGScore);
            RaisePropertyChanged(nameof(Information));
        }

        private void _AutoFocusFluoCmd_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                _Info.Append("Autofocus for FiducialOffset succeeded.");
                _Offset = _AutoFocusCmd.Offset;
                _Info.Append(string.Format("{0} Fiducial Offset: {1}", SelectedAFSurface, _Offset));
                if (SelectedAFSurface == "Button") { RecipeVM.B_Offset = _Offset; }
                else { RecipeVM.T_Offset = _Offset; }

            }
            else if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _Info.Append("Autofocus for FiducialOffset aborted.");
            }
            else
            {
                _Info.Append(string.Format("Autofocus for FiducialOffset failed. Exception:{0}, LED Failure:{1}, Failed to Capture Image{2}", 
                    _AutoFocusCmd.ExceptionMessage, _AutoFocusCmd.IsFailedToSetLED, _AutoFocusCmd.IsFailedCaptureImage));
            }
            RaisePropertyChanged(nameof(Information));
            _AutoFocusCmd.Completed -= _AutoFocusFluoCmd_Completed;
            _AutoFocusCmd.OnImageSampled -= _AutoFocusFluoCmd_OnImageSampled;
            _AutoFocusCmd = null;
        }

        private bool CanExecuteCalculateOffsetCmd(object obj)
        {
            return !IsAutoFocusing && !IsScanning;
        }
        #endregion Start Command

        #region Calculate Channel Offset Command
        private RelayCommand _CalculateChannelOffsetCmd;
        public ICommand CalculateChannelOffsetCmd
        {
            get
            {
                if (_CalculateChannelOffsetCmd == null)
                {
                    _CalculateChannelOffsetCmd = new RelayCommand(ExecuteCalculateChannelOffsetCmd, CanExecuteCalculateChannelOffsetCmd);
                }
                return _CalculateChannelOffsetCmd;
            }
        }

        private void ExecuteCalculateChannelOffsetCmd(object obj)
        {
            if (MainBoardVM.IsGLEDOnSet || MainBoardVM.IsRLEDOnSet || MainBoardVM.IsWLEDOnSet ||
                LucidCameraManager.GetCamera(0).IsAcqRunning || LucidCameraManager.GetCamera(1).IsAcqRunning)
            {
                MessageBox.Show("LED was on or camera is running, please turn off LEDs or stop live mode before start AF");
                return;
            }
            TaskFactory tf = new TaskFactory();
            tf.StartNew(() =>
            {
                if (SelectedAFSurface == "Button") { _Offset = RecipeVM.B_Offset; }
                else { _Offset = RecipeVM.T_Offset; }
                ExecuteStartCmd(obj);
                while (_IsAutoFocusing)
                {
                    Thread.Sleep(1);
                }
                if (_IsAFSucc)
                {
                    AutoFocusSettings OffsetAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                    ICamera camera = CameraVM.ActiveCamera;
                    Int32Rect roi = new Int32Rect();
                    roi.X = 1800;
                    roi.Y = 1800;
                    roi.Width = 800;
                    roi.Height = 800;
                    OffsetAFSettings.ROI = roi;
                    _FocusedFiducial = MotionVM.MotionController.ZCurrentPos;
                    OffsetAFSettings.ZstageLimitH = _FocusedFiducial + 5 - _Offset;
                    OffsetAFSettings.ZstageLimitL = _FocusedFiducial - 5 - _Offset;
                    OffsetAFSettings.ZstageAccel = ZAccel;
                    OffsetAFSettings.ZstageSpeed = ZSpeed;
                    OffsetAFSettings.LEDType = SelectedOffsetLED;
                    OffsetAFSettings.ExposureTime = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetExposureTime;
                    OffsetAFSettings.Reference0 = _FocusedFiducial - _Offset;
                    if (_SelectedOffsetLED == LEDTypes.Green)
                    {
                        OffsetAFSettings.LEDIntensity = 50;
                    }
                    else if (_SelectedOffsetLED == LEDTypes.Red)
                    {
                        OffsetAFSettings.LEDIntensity = 50;
                    }

                    ICamera corrCamera = LucidCameraManager.GetCamera(0).Channels.Contains("1") ? LucidCameraManager.GetCamera(0) : LucidCameraManager.GetCamera(1);
                    _AutoFocusCmd = new AutoFocusChannelOffset(TheDispatcher, MotionVM.MotionController, corrCamera, MainBoardVM.LEDController, OffsetAFSettings);

                    _AutoFocusCmd.Completed += _AutoFocusChanneloffsetCmd_Completed;
                    _AutoFocusCmd.OnImageSampled += _AutoFocusFluoCmd_OnImageSampled;
                    _AutoFocusCmd.Start();
                    _Info.Append("AutoFocusChannelOffset starts:\n");
                    RaisePropertyChanged(nameof(Information));
                }
                else
                {
                    _Info.Append("AutoFocusChannelOffset failed, retry.\n");
                    MessageBox.Show("AutoFocusChannelOffset failed, retry.");
                }
            });
        }

        private void _AutoFocusChanneloffsetCmd_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                _Info.Append("AutoFocusChannelOffset succeeded.");
                _Offset = _AutoFocusCmd.Offset;
                _Info.Append(string.Format("ChannelOffset: {0}",  _Offset));
                SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset = _Offset;

            }
            else if (_AutoFocusCmd.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _Info.Append("AutoFocusChannelOffset aborted.");
            }
            else
            {
                _Info.Append(string.Format("AutoFocusChannelOffset failed. Exception:{0}, LED Failure:{1}, Failed to Capture Image{2}",
                    _AutoFocusCmd.ExceptionMessage, _AutoFocusCmd.IsFailedToSetLED, _AutoFocusCmd.IsFailedCaptureImage));
            }
            RaisePropertyChanged(nameof(Information));
            _AutoFocusCmd.Completed -= _AutoFocusChanneloffsetCmd_Completed;
            _AutoFocusCmd.OnImageSampled -= _AutoFocusFluoCmd_OnImageSampled;
            _AutoFocusCmd = null;
        }

        private bool CanExecuteCalculateChannelOffsetCmd(object obj)
        {
            return !IsAutoFocusing && !IsScanning;
        }
        #endregion Start Command
    }

}
