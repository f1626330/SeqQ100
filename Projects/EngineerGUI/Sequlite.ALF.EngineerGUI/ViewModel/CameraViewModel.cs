using Sequlite.ALF.Common;
using Sequlite.ALF.Imaging;
using Sequlite.CameraLib;
using Sequlite.WPF.Framework;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Sequlite.Image.Processing;
using System.Collections.Generic;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public enum CameraStatusEnums
    {
        Disconnected,
        Idle,
        Capture,
        Continuous,
        Live,
    }
    public class CameraViewModel : ViewModelBase
    {
        public delegate void RegionAdornerDelegate(bool bIsVisible);
        public event RegionAdornerDelegate RegionAdornerChanged;
        //public delegate void ImageResizedDelegate(double percent);
        //public event ImageResizedDelegate ImageSizeChanged;
        #region Private Fields
        private PhotometricsCamera _ActiveCamera;
        private bool _IsConnected;
        private double _ExposureTime = 0.5;
        private BinningFactorType _SelectedBinning;
        private GainType _SelectedGain;
        private ReadoutType _SelectedReadout;
        private int _RoiLeft;
        private int _RoiTop;
        private int _RoiWidth;
        private int _RoiHeight;
        private double _CCDTemperSet;
        private double _CCDTemperGet;
        private CameraStatusEnums _WorkingStatus;
        private ImageCaptureCommand _CaptureProcess;
        private ImageLiveCommand _LiveProcess;
        private BitmapSource _LiveImage;
        private Rect _SelectedRegion;
        private int _BadImageCounts;
        private bool _IsCaptureFullROI = true;
        private bool _IsRestarting;
        private bool _IsCaptureAverage;
        private int _LEDFailureCount;

        private static ISeqLog Logger { get; } = SeqLogFactory.GetSeqFileLog("Camera Model");

        private ILucidCamera _EthernetCameraA;
        private ILucidCamera _EthernetCameraB;
        #endregion Private Fields

        MainBoardViewModel MainBoardVM { get; }
        ImageGalleryViewModel ImageGalleryVM { get; }
        MotionViewModel MotionVM { get;  }
        ICameraStatus CameraStatus { get; }
        public bool IsMachineRev2 { get; }
        string ProductVersion { get; }
        #region Constructor
        public CameraViewModel(bool isMachineRev2, string productVersion,
            ICameraStatus cameraStatus, MainBoardViewModel mainBoardVM, ImageGalleryViewModel imageGalleryVM, MotionViewModel motionVM)
        {
            BitDepthOptions = new List<int>
            {
                8,
                10,
                12
            };
            SelectedBitDepth = BitDepthOptions[2];
            PixelFormatOptions = new List<int>
            {
                8,
                16
            };
            SelectedPixelFormat = PixelFormatOptions[1];
            IsMachineRev2 = isMachineRev2;
            ProductVersion = productVersion;
            CameraStatus = cameraStatus;
            MainBoardVM = mainBoardVM;
            ImageGalleryVM = imageGalleryVM;
            MotionVM = motionVM;
        }

        public void Initialize()
        {
            BinningOptions = SettingsManager.ConfigSettings.BinFactors;
            GainOptions = SettingsManager.ConfigSettings.Gains;
            ReadoutOptions = new ObservableCollection<ReadoutType>();

            SelectedBinning = BinningOptions[0];
            SelectedGain = GainOptions[4];

            //_ActiveCamera = new PhotometricsCamera();
            //_ActiveCamera.CCDCoolerSetPoint = -20;
        }
        #endregion Constructor

        #region Public Properties
        public ICamera ActiveCamera
        {
            get
            {
                if (!IsMachineRev2)
                {
                    return _ActiveCamera;
                }
                else
                {
                    return SelectedCamera;
                }
            }
        }
        public ILucidCamera EthernetCameraA
        {
            get { return _EthernetCameraA; }
        }
        public ILucidCamera EthernetCameraB
        {
            get { return _EthernetCameraB; }
        }
        public bool IsConnected
        {
            get { return _IsConnected; }
            set
            {
                if (_IsConnected != value)
                {
                    _IsConnected = value;
                    RaisePropertyChanged("IsConnected");

                }
            }
        }
        public double ExposureTime
        {
            get { return _ExposureTime; }
            set
            {
                if (_ExposureTime != value)
                {
                    _ExposureTime = value;
                    RaisePropertyChanged("ExposureTime");
                }
            }
        }
        public ObservableCollection<BinningFactorType> BinningOptions { get; private set; }
        public BinningFactorType SelectedBinning
        {
            get { return _SelectedBinning; }
            set
            {
                if (_SelectedBinning != value)
                {
                    _SelectedBinning = value;
                    RaisePropertyChanged("SelectedBinning");

                    if (_SelectedBinning != null)
                    {
                        if (_IsConnected)
                        {
                            if (!IsMachineRev2 && _ActiveCamera != null)
                            {
                                _ActiveCamera.HBin = _SelectedBinning.VerticalBins;
                                RoiLeft = 0;
                                RoiTop = 0;
                                RoiWidth = (_ActiveCamera.ImagingColumns / _SelectedBinning.VerticalBins) - 1;
                                if (RoiWidth < 0)
                                {
                                    RoiWidth = 0;
                                }
                                RoiHeight = (_ActiveCamera.ImagingRows / _SelectedBinning.VerticalBins) - 1;
                                if (RoiHeight < 0)
                                {
                                    RoiHeight = 0;
                                }
                            }
                            else if (SelectedCamera != null)
                            {
                                SelectedCamera.HBin = _SelectedBinning.VerticalBins;
                                RoiLeft = 0;
                                RoiTop = 0;
                                RoiWidth = (SelectedCamera.ImagingColumns / _SelectedBinning.VerticalBins) - 1;
                                if (RoiWidth < 0)
                                {
                                    RoiWidth = 0;
                                }
                                RoiHeight = (SelectedCamera.ImagingRows / _SelectedBinning.VerticalBins) - 1;
                                if (RoiHeight < 0)
                                {
                                    RoiHeight = 0;
                                }
                            }
                        }
                    }
                }
            }
        }
        public ObservableCollection<GainType> GainOptions { get; private set; }
        public GainType SelectedGain
        {
            get { return _SelectedGain; }
            set
            {
                if (_SelectedGain != value)
                {
                    _SelectedGain = value;
                    RaisePropertyChanged("SelectedGain");

                    if (_SelectedGain != null)
                    {
                        if (!IsMachineRev2 && _ActiveCamera != null)
                        {
                            _ActiveCamera.Gain = _SelectedGain.Value;
                        }
                        else if (SelectedCamera != null)
                        {
                            SelectedCamera.Gain = _SelectedGain.Value;
                        }
                    }
                }
            }
        }
        public ObservableCollection<ReadoutType> ReadoutOptions { get; private set; }
        public ReadoutType SelectedReadout
        {
            get { return _SelectedReadout; }
            set
            {
                if (_SelectedReadout != value)
                {
                    _SelectedReadout = value;
                    RaisePropertyChanged("SelectedReadout");
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

                    if (!IsMachineRev2 && ActiveCamera != null)
                    {
                        RoiWidth = RoiWidth - ((RoiWidth + RoiLeft) - (ActiveCamera.ImagingColumns - 1));
                    }
                    else if (SelectedCamera != null)
                    {
                        RoiWidth = RoiWidth - ((RoiWidth + RoiLeft) - (SelectedCamera.ImagingColumns - 1));
                    }
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

                    if (!IsMachineRev2 && ActiveCamera != null)
                    {
                        RoiHeight = RoiHeight - ((RoiHeight + RoiTop) - (ActiveCamera.ImagingRows - 1));
                    }
                    else if (SelectedCamera != null)
                    {
                        RoiHeight = RoiHeight - ((RoiHeight + RoiTop) - (SelectedCamera.ImagingRows - 1));
                    }
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
        public double CCDTemperSet
        {
            get { return _CCDTemperSet; }
            set
            {
                if (_CCDTemperSet != value)
                {
                    _CCDTemperSet = value;
                    RaisePropertyChanged(nameof(CCDTemperSet));
                }
            }
        }
        public double CCDTemperGet
        {
            get { return _CCDTemperGet; }
            set
            {
                if (_CCDTemperGet != value)
                {
                    _CCDTemperGet = value;
                    RaisePropertyChanged(nameof(CCDTemperGet));
                }
            }
        }
        public CameraStatusEnums WorkingStatus
        {
            get { return _WorkingStatus; }
            set
            {
                if (_WorkingStatus != value)
                {
                    _WorkingStatus = value;
                    RaisePropertyChanged("WorkingStatus");
                }
            }
        }
        public BitmapSource LiveImage
        {
            get { return _LiveImage; }
            set
            {
                if (_LiveImage != value)
                {
                    _LiveImage = value;
                    RaisePropertyChanged(nameof(LiveImage));
                }
            }
        }
        public Rect SelectedRegion
        {
            get { return _SelectedRegion; }
            set
            {
                if (_SelectedRegion != value)
                {
                    _SelectedRegion = value;
                    RaisePropertyChanged(nameof(SelectedRegion));

                    if (_SelectedRegion != null)
                    {
                        if (_SelectedRegion.Width > 0 && _SelectedRegion.Height > 0)
                        {
                            RoiLeft = (int)_SelectedRegion.X;
                            RoiTop = (int)_SelectedRegion.Y;
                            RoiWidth = (int)_SelectedRegion.Width;
                            RoiHeight = (int)_SelectedRegion.Height;
                        }
                    }
                }
            }
        }

        public int BadImageCounts
        {
            get { return _BadImageCounts; }
            set
            {
                if (_BadImageCounts != value)
                {
                    _BadImageCounts = value;
                    RaisePropertyChanged(nameof(BadImageCounts));
                }
            }
        }

        public int LEDFailureCount
        {
            get { return _LEDFailureCount; }
            set
            {
                if (_LEDFailureCount != value)
                {
                    _LEDFailureCount = value;
                    RaisePropertyChanged(nameof(LEDFailureCount));
                }
            }
        }
        public bool IsCaptureFullROI
        {
            get { return _IsCaptureFullROI; }
            set
            {
                if (_IsCaptureFullROI != value)
                {
                    _IsCaptureFullROI = value;
                    RaisePropertyChanged(nameof(IsCaptureFullROI));
                }
            }
        }

        public bool IsRestarting
        {
            get { return _IsRestarting; }
            set
            {
                if (_IsRestarting != value)
                {
                    _IsRestarting = value;
                    RaisePropertyChanged(nameof(IsRestarting));
                }
            }
        }

        public bool IsCaptureAverage
        {
            get { return _IsCaptureAverage; }
            set
            {
                if (_IsCaptureAverage != value)
                {
                    _IsCaptureAverage = value;
                    RaisePropertyChanged(nameof(IsCaptureAverage));
                }
            }
        }
        public ObservableCollection<ICamera> EthernetCameraOptions { get; } = new ObservableCollection<ICamera>();
        private ICamera _SelectedCamera;
        public ICamera SelectedCamera
        {
            get { return _SelectedCamera; }
            set
            {
                if (_SelectedCamera != value)
                {
                    _SelectedCamera = value;
                    RaisePropertyChanged(nameof(SelectedCamera));
                }
            }
        }

        public List<int> BitDepthOptions { get; }
        private int _SelectedBitDepth;
        public int SelectedBitDepth
        {
            get { return _SelectedBitDepth; }
            set
            {
                if (_SelectedBitDepth != value)
                {
                    _SelectedBitDepth = value;
                    RaisePropertyChanged(nameof(SelectedBitDepth));
                }
            }
        }
        public List<int> PixelFormatOptions { get; }
        private int _SelectedPixelFormat;
        public int SelectedPixelFormat
        {
            get { return _SelectedPixelFormat; }
            set
            {
                if (_SelectedPixelFormat != value)
                {
                    _SelectedPixelFormat = value;
                    RaisePropertyChanged(nameof(SelectedPixelFormat));
                }
            }
        }
        #endregion Public Properties

        #region Set CCD Temperature Command
        private RelayCommand _SetCCDTemperCmd;
        public ICommand SetCCDTemperCmd
        {
            get
            {
                if (_SetCCDTemperCmd == null)
                {
                    _SetCCDTemperCmd = new RelayCommand(ExecuteSetCCDTemperCmd, CanExecuteSetCCDTemperCmd);
                }
                return _SetCCDTemperCmd;
            }
        }

        private void ExecuteSetCCDTemperCmd(object obj)
        {
            if (_ActiveCamera != null)
            {
                _ActiveCamera.CCDCoolerSetPoint = CCDTemperSet;
            }
            //else if (SelectedCamera != null)
            //{
            //    SelectedCamera.CCDCoolerSetPoint = CCDTemperSet;
            //}
        }

        private bool CanExecuteSetCCDTemperCmd(object obj)
        {
            return IsConnected && !IsMachineRev2;
        }
        #endregion Set CCD Temperature Command

        #region Read CCD Temperature Command
        private RelayCommand _ReadCCDTemperCmd;
        public ICommand ReadCCDTemperCmd
        {
            get
            {
                if (_ReadCCDTemperCmd == null)
                {
                    _ReadCCDTemperCmd = new RelayCommand(ExecuteReadCCDTemperCmd, CanExecuteReadCCDTemperCmd);
                }
                return _ReadCCDTemperCmd;
            }
        }

        private void ExecuteReadCCDTemperCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                CCDTemperGet = _ActiveCamera.CCDTemperature;
            }
            else if (SelectedCamera != null)
            {
                CCDTemperGet = SelectedCamera.CCDTemperature;
            }
        }

        private bool CanExecuteReadCCDTemperCmd(object obj)
        {
            return IsConnected;
        }
        #endregion Read CCD Temperature Command

        #region Reset ROI Command
        private RelayCommand _ResetRoiCmd;
        public ICommand ResetRoiCmd
        {
            get
            {
                if (_ResetRoiCmd == null)
                {
                    _ResetRoiCmd = new RelayCommand(ExecuteResetRoiCmd, CanExecuteResetRoiCmd);
                }
                return _ResetRoiCmd;
            }
        }

        private void ExecuteResetRoiCmd(object obj)
        {
            if (_ActiveCamera != null && !IsMachineRev2)
            {
                RoiLeft = 0;
                RoiTop = 0;
                RoiWidth = ActiveCamera.ImagingColumns - 1;
                RoiHeight = ActiveCamera.ImagingRows - 1;
                ActiveCamera.RoiStartX = 0;
                ActiveCamera.RoiWidth = (ushort)(ActiveCamera.ImagingColumns - 1);
                ActiveCamera.RoiStartY = 0;
                ActiveCamera.RoiHeight = (ushort)(ActiveCamera.ImagingRows - 1);
            }
            else if(SelectedCamera!=null)
            {

                RoiLeft = 0;
                RoiTop = 0;
                RoiWidth = SelectedCamera.ImagingColumns / SelectedBinning.VerticalBins;
                RoiHeight = SelectedCamera.ImagingRows / SelectedBinning.VerticalBins;
            }
        }

        private bool CanExecuteResetRoiCmd(object obj)
        {
            return IsConnected;
        }
        #endregion Reset ROI Command

        #region Capture Command
        private RelayCommand _CaptureCmd;
        public ICommand CaptureCmd
        {
            get
            {
                if (_CaptureCmd == null)
                {
                    _CaptureCmd = new RelayCommand(ExecuteCaptureCmd, CanExecuteCaptureCmd);
                }
                return _CaptureCmd;
            }
        }

        private void ExecuteCaptureCmd(object obj)
        {
            BadImageCounts = 0;
            WorkingStatus = CameraStatusEnums.Capture;
            ImageChannelSettings _ImageSetting = new ImageChannelSettings();

            //Set Image Channel Info
            if (MainBoardVM.IsGLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.Green;
                _ImageSetting.LedIntensity = MainBoardVM.GLEDIntensitySet;
            }
            else if (MainBoardVM.IsRLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.Red;
                _ImageSetting.LedIntensity = MainBoardVM.RLEDIntensitySet;
            }
            else if (MainBoardVM.IsWLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.White;
                _ImageSetting.LedIntensity = MainBoardVM.WLEDIntensitySet;
            }

            //Get ROI
            ICamera _Camera;
            Int32Rect roiRect = new Int32Rect(RoiLeft, RoiTop, RoiWidth, RoiHeight);
            if (!IsMachineRev2)
            {
                _Camera = ActiveCamera;
                if ((RoiWidth + RoiLeft) > _Camera.ImagingColumns - 1)
                {
                    RoiWidth = RoiWidth - ((RoiWidth + RoiLeft) - (_Camera.ImagingColumns - 1));
                }
                if ((RoiHeight + RoiTop) > _Camera.ImagingRows - 1)
                {
                    RoiHeight = RoiHeight - ((RoiHeight + RoiTop) - (_Camera.ImagingRows - 1));
                }
                roiRect.X = _Camera.ImagingColumns - 1 - (roiRect.Width + roiRect.X);
            }
            else
            {
                _Camera = SelectedCamera;
            }

            _ImageSetting.Exposure = ExposureTime; // exposure time in seconds
            _ImageSetting.BinningMode = SelectedBinning.VerticalBins;
            _ImageSetting.AdGain = SelectedGain.Value;
            if (!IsMachineRev2)
            {
                int readoutValue = (_SelectedReadout.Value == 0) ? 1 : 0;
                _ImageSetting.ReadoutSpeed = readoutValue;
            }
            else
            {
                _ImageSetting.ADCBitDepth = SelectedBitDepth;
                _ImageSetting.PixelFormatBitDepth = SelectedPixelFormat;
            }
            _ImageSetting.IsCaptureFullRoi = IsCaptureFullROI;
            _ImageSetting.IsCaptureAvg = IsCaptureAverage;
            _ImageSetting.IsEnableBadImageCheck = true;
            _ImageSetting.ExtraExposure = SettingsManager.ConfigSettings.CameraDefaultSettings.ExtraExposure;

            _CaptureProcess = new ImageCaptureCommand(TheDispatcher, MainBoardVM.MainBoard, _Camera, _ImageSetting, roiRect,
                                                    MainBoardVM.LEDController, IsMachineRev2);
            _CaptureProcess.Completed += _CaptureProcess_Completed;
            _CaptureProcess.CommandStatus += _CaptureProcess_CommandStatus;
            _CaptureProcess.CompletionEstimate += _CaptureProcess_CompletionEstimate;
            _CaptureProcess.OnCaptureBadImage += _CaptureProcess_OnCaptureBadImage;
            _CaptureProcess.OnLEDStateChanged += Imaging_OnLEDStateChanged;
            _CaptureProcess.OnLEDFailure += _CaptureProcess_OnLEDFailure;
            _CaptureProcess.Start();
        }

        public void Imaging_OnLEDStateChanged(LEDTypes led, bool newState)
        {
            switch (led)
            {
                case LEDTypes.Green:
                    MainBoardVM.IsGLEDOnSet = newState;
                    break;
                case LEDTypes.Red:
                    MainBoardVM.IsRLEDOnSet = newState;
                    break;
                case LEDTypes.White:
                    MainBoardVM.IsWLEDOnSet = newState;
                    break;
            }
        }

        private void _CaptureProcess_OnCaptureBadImage()
        {
            BadImageCounts++;
            Dispatch(() =>
            {
                var badImage = _CaptureProcess.CapturedImage.Clone();
                ImageGalleryVM.NewDocument(badImage, new Image.Processing.ImageInfo(), "Bad Image " + BadImageCounts, true);
            });
        }
        private void _CaptureProcess_OnLEDFailure()
        {
            LEDFailureCount++;
        }
        private void _CaptureProcess_CompletionEstimate(ThreadBase sender, DateTime dateTime, double estTime)
        {
            //TheDispatcher.Invoke((Action)delegate
            //{
            //    _Workspace.This.CaptureCountdownTimer.Start();
            //});

            CameraStatus.CameraCaptureStartTime = dateTime;
            CameraStatus.EstimatedCaptureTime = estTime;
        }

        private void _CaptureProcess_CommandStatus(object sender, string status)
        {
            TheDispatcher.BeginInvoke((Action)delegate
            {
                CameraStatus.CameraCapturingStatus = status;
            });
        }

        private void _CaptureProcess_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            TheDispatcher.BeginInvoke((Action)delegate
            {
                //_Workspace.This.CaptureCountdownTimer.Stop();
                WorkingStatus = CameraStatusEnums.Idle;

                if (!IsMachineRev2)
                {
                    MainBoardVM.IsRLEDOnSet = false;
                    MainBoardVM.IsGLEDOnSet = false;
                    MainBoardVM.IsWLEDOnSet = false;
                }

                ImageCaptureCommand imageCaptureThread = (sender as ImageCaptureCommand);

                if (exitState == ThreadBase.ThreadExitStat.None)
                {
                    // Capture successful

                    WriteableBitmap capturedImage = imageCaptureThread.CapturedImage;
                    Image.Processing.ImageInfo imageInfo = imageCaptureThread.ImageInfo;
                    if (imageInfo != null)
                    {
                        imageInfo.InstrumentModel = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName;
                        imageInfo.SoftwareVersion = ProductVersion;
                        imageInfo.MixChannel.FocusPosition = MotionVM.ZMotionCurrentPos;
                        imageInfo.MixChannel.YPosition = MotionVM.YMotionCurrentPos;
                        imageInfo.MixChannel.FilterPosition = MotionVM.FMotionCurrentPos;
                        if (IsMachineRev2) 
                        { 
                            imageInfo.MixChannel.XPosition = MotionVM.XMotionCurrentPos; 
                        }
                      
                    }

                    if (capturedImage != null)
                    {
                        string newTitle = String.Format("Image{0}", ++ImageGalleryVM.FileNameCount);
                        ImageGalleryVM.NewDocument(capturedImage, imageInfo, newTitle);
                        //_Workspace.This.SelectedTabIndex = (int)ApplicationTabType.Gallery;   // Switch to gallery tab
                    }
                }
                else if (exitState == ThreadBase.ThreadExitStat.Error)
                {
                    // Oh oh something went wrong - handle the error
                    if (imageCaptureThread != null && imageCaptureThread.Error != null)
                    {
                        string strCaption = "Image acquisition error...";
                        string strMessage = string.Empty;

                        if (imageCaptureThread.IsOutOfMemory)
                        {
                            strMessage = "System low on memory.\n" +
                                         "Please close some images before acquiring another image.\n" +
                                         "If this error persists, please restart the application.";
                            MessageBox.Show(strMessage, strCaption);
                        }
                        else if (imageCaptureThread.IsFailedToSetLED)
                        {
                            MessageBox.Show("Failed to set LED status");
                        }
                        else
                        {
                            strMessage = "Image acquisition error: \n" + imageCaptureThread.Error.Message;
                            MessageBox.Show(strMessage, strCaption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }
                    }

                }

                _CaptureProcess.CompletionEstimate -= _CaptureProcess_CompletionEstimate;
                _CaptureProcess.CommandStatus -= _CaptureProcess_CommandStatus;
                _CaptureProcess.OnCaptureBadImage -= _CaptureProcess_OnCaptureBadImage;
                _CaptureProcess.OnLEDStateChanged -= Imaging_OnLEDStateChanged;
                _CaptureProcess.OnLEDFailure -= _CaptureProcess_OnLEDFailure;
                _CaptureProcess.Completed -= _CaptureProcess_Completed;
                _CaptureProcess = null;
            });
        }

        private bool CanExecuteCaptureCmd(object obj)
        {
            return IsConnected && WorkingStatus == CameraStatusEnums.Idle;
        }
        #endregion Capture Command

        #region Continuous Command
        private RelayCommand _ContinuousCmd;
        public ICommand ContinuousCmd
        {
            get
            {
                if (_ContinuousCmd == null)
                {
                    _ContinuousCmd = new RelayCommand(ExecuteContinuousCmd, CanExecuteContinuousCmd);
                }
                return _ContinuousCmd;
            }
        }

        private void ExecuteContinuousCmd(object obj)
        {
            WorkingStatus = CameraStatusEnums.Continuous;
            MessageBox.Show("To be implemented");
        }

        private bool CanExecuteContinuousCmd(object obj)
        {
            return IsConnected;
        }
        #endregion Continuous Command

        #region Live Mode Command
        private RelayCommand _LiveModeCmd;
        public ICommand LiveModeCmd
        {
            get
            {
                if (_LiveModeCmd == null)
                {
                    _LiveModeCmd = new RelayCommand(ExecuteLiveModeCmd, CanExecuteLiveModeCmd);
                }
                return _LiveModeCmd;
            }
        }

        private void ExecuteLiveModeCmd(object obj)
        {
            BadImageCounts = 0;
            WorkingStatus = CameraStatusEnums.Live;
            if (!IsMachineRev2 && _ActiveCamera.IsAcqRunning) { return; }
            ImageChannelSettings _ImageSetting = new ImageChannelSettings();
            _ImageSetting.Exposure = ExposureTime; // exposure time in seconds
            _ImageSetting.BinningMode = SelectedBinning.VerticalBins;
            _ImageSetting.AdGain = SelectedGain.Value;
            if (!IsMachineRev2)
            {
                int readoutValue = (_SelectedReadout.Value == 0) ? 1 : 0;
                _ImageSetting.ReadoutSpeed = readoutValue;
            }
            else
            {
                _ImageSetting.ADCBitDepth = SelectedBitDepth;
                _ImageSetting.PixelFormatBitDepth = SelectedPixelFormat;
            }
            _ImageSetting.IsCaptureFullRoi = IsCaptureFullROI;

            //Set Image Channel Info
            if (MainBoardVM.IsGLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.Green;
                _ImageSetting.LedIntensity = MainBoardVM.GLEDIntensitySet;
            }
            else if (MainBoardVM.IsRLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.Red;
                _ImageSetting.LedIntensity = MainBoardVM.RLEDIntensitySet;
            }
            else if (MainBoardVM.IsWLEDSelected)
            {
                _ImageSetting.LED = LEDTypes.White;
                _ImageSetting.LedIntensity = MainBoardVM.WLEDIntensitySet;
            }


            //Get ROI
            ICamera camera;
            Int32Rect roiRect = new Int32Rect(RoiLeft, RoiTop, RoiWidth, RoiHeight);
            if (!IsMachineRev2)
            {
                camera = ActiveCamera;
                if ((RoiWidth + RoiLeft) > camera.ImagingColumns - 1)
                {
                    RoiWidth = RoiWidth - ((RoiWidth + RoiLeft) - (camera.ImagingColumns - 1));
                }
                if ((RoiHeight + RoiTop) > camera.ImagingRows - 1)
                {
                    RoiHeight = RoiHeight - ((RoiHeight + RoiTop) - (camera.ImagingRows - 1));
                }
                // transform the roi since the display live image is mirrored by y axis
                roiRect.X = camera.ImagingColumns - 1 - (roiRect.Width + roiRect.X);
            }
            else
            {
                camera = SelectedCamera;
            }

            _LiveProcess = new ImageLiveCommand(TheDispatcher, camera, MainBoardVM.MainBoard, _ImageSetting, roiRect, true,
                                                MainBoardVM.LEDController, IsMachineRev2);
            _LiveProcess.Completed += _LiveProcess_Completed;
            _LiveProcess.LiveImageReceived += _LiveProcess_LiveImageReceived;
            _LiveProcess.OnLEDStateChanged += Imaging_OnLEDStateChanged;
            _LiveProcess.OnCaptureBadImage += _CaptureProcess_OnCaptureBadImage;
            _LiveProcess.OnCaptureLiveImage += _LiveProcess_OnCaptureLiveImage;
            _LiveProcess.Start();

            RegionAdornerChanged?.Invoke(true);
        }

        private void _LiveProcess_OnCaptureLiveImage(WriteableBitmap capturedImage, Image.Processing.ImageInfo imageInfo)
        {
            if (capturedImage != null && imageInfo != null)
            {
                imageInfo.SoftwareVersion = ProductVersion;
                imageInfo.MixChannel.FocusPosition = MotionVM.ZMotionCurrentPos;
                imageInfo.MixChannel.YPosition = MotionVM.YMotionCurrentPos;
                imageInfo.MixChannel.FilterPosition = MotionVM.FMotionCurrentPos;   // for 1.x machine
                imageInfo.MixChannel.XPosition = MotionVM.XMotionCurrentPos;        // for 2.x machine

                string newTitle = String.Format("Live Image{0}", ++ImageGalleryVM.FileNameCount);
                ImageGalleryVM.NewDocument(capturedImage, imageInfo, newTitle);
            }
        }

        private void _LiveProcess_LiveImageReceived(BitmapSource displayBitmap)
        {
            try
            {
                TheDispatcher.BeginInvoke(new Action(() =>
                {
                    LiveImage = displayBitmap;
                }));
            }
            catch (Exception ex)
            {
                ExecuteCancelCmd(null);
                throw new Exception("Live mode error.", ex);
            }
        }

        private void _LiveProcess_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            MainBoardVM.IsRLEDOnSet = false;
            MainBoardVM.IsGLEDOnSet = false;
            MainBoardVM.IsWLEDOnSet = false;
            var imageLiveThread = sender as ImageLiveCommand;
            if (exitState == ThreadBase.ThreadExitStat.Error)
            {
                // Oh oh something went wrong - handle the error
                if (imageLiveThread != null && imageLiveThread.Error != null)
                {
                    string strCaption = "Image acquisition error...";
                    string strMessage = string.Empty;

                    if (imageLiveThread.IsOutOfMemory)
                    {
                        strMessage = "System low on memory.\n" +
                                     "Please close some images before acquiring another image.\n" +
                                     "If this error persists, please restart the application.";
                        MessageBox.Show(strMessage, strCaption);
                    }
                    else if (imageLiveThread.IsFailedToSetLED)
                    {
                        MessageBox.Show("Failed to set LED status");
                    }
                    else
                    {
                        strMessage = "Image acquisition error: \n" + imageLiveThread.Error.Message;
                        MessageBox.Show(strMessage, strCaption, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }

            }

            TheDispatcher.BeginInvoke(new Action(() =>
            {
                LiveImage = null;
            }));
            WorkingStatus = CameraStatusEnums.Idle;
            _LiveProcess.OnLEDStateChanged -= Imaging_OnLEDStateChanged;
            _LiveProcess.OnCaptureBadImage -= _CaptureProcess_OnCaptureBadImage;
            _LiveProcess.OnCaptureLiveImage -= _LiveProcess_OnCaptureLiveImage;
            _LiveProcess.Completed -= _LiveProcess_Completed;
            _LiveProcess.LiveImageReceived -= _LiveProcess_LiveImageReceived;
            _LiveProcess = null;
        }

        private bool CanExecuteLiveModeCmd(object obj)
        {
            return IsConnected && WorkingStatus == CameraStatusEnums.Idle;
        }
        #endregion Live Mode Command

        #region Cancel Command
        private RelayCommand _CancelCmd;
        public ICommand CancelCmd
        {
            get
            {
                if (_CancelCmd == null)
                {
                    _CancelCmd = new RelayCommand(ExecuteCancelCmd, CanExecuteCancelCmd);
                }
                return _CancelCmd;
            }
        }

        private void ExecuteCancelCmd(object obj)
        {
            if (WorkingStatus == CameraStatusEnums.Capture)
            {
                _CaptureProcess?.Abort();
            }
            else if (WorkingStatus == CameraStatusEnums.Live)
            {
                _LiveProcess?.Abort();
                RegionAdornerChanged?.Invoke(false);
            }

            WorkingStatus = CameraStatusEnums.Idle;
        }

        private bool CanExecuteCancelCmd(object obj)
        {
            return true;
        }
        #endregion Cancel Command

        #region Capture Live Image Command
        private RelayCommand _CaptureLiveImageCmd;
        public ICommand CaptureLiveImageCmd
        {
            get
            {
                if (_CaptureLiveImageCmd == null)
                {
                    _CaptureLiveImageCmd = new RelayCommand(ExecuteCaptureLiveImageCmd, CanExecuteCaptureLiveImageCmd);
                }
                return _CaptureLiveImageCmd;
            }
        }

        private void ExecuteCaptureLiveImageCmd(object obj)
        {
            if (_LiveProcess == null) { return; }
            _LiveProcess.CaptureLiveImage();
        }

        private bool CanExecuteCaptureLiveImageCmd(object obj)
        {
            return true;
        }
        #endregion Capture Live Image Command

        #region Reset Bad Image Counts Command
        private RelayCommand _ResetBadImageCountsCmd;
        public ICommand ResetBadImageCountsCmd
        {
            get
            {
                if (_ResetBadImageCountsCmd == null)
                {
                    _ResetBadImageCountsCmd = new RelayCommand(ExecuteResetBadImageCountsCmd, CanExecuteResetBadImageCountsCmd);
                }
                return _ResetBadImageCountsCmd;
            }
        }

        private void ExecuteResetBadImageCountsCmd(object obj)
        {
            BadImageCounts = 0;
        }

        private bool CanExecuteResetBadImageCountsCmd(object obj)
        {
            return true;
        }
        #endregion Reset Bad Image Counts Command

        #region Restart Camera Command
        private RelayCommand _RestartCameraCmd;
        public ICommand RestartCameraCmd
        {
            get
            {
                if (_RestartCameraCmd == null)
                {
                    _RestartCameraCmd = new RelayCommand(ExecuteRestartCameraCmd, CanExecuteRestartCameraCmd);
                }
                return _RestartCameraCmd;
            }
        }

        private void ExecuteRestartCameraCmd(object obj)
        {
            IsRestarting = true;
            WorkingStatus = CameraStatusEnums.Idle;
            _ActiveCamera.Close();
            Thread.Sleep(1000);
            _ActiveCamera.Open();
            Thread.Sleep(500);
            IsRestarting = false;
            //MessageBox.Show("re");
        }

        private bool CanExecuteRestartCameraCmd(object obj)
        {
            return !IsRestarting && !IsMachineRev2;
        }
        #endregion Reset Bad Image Counts Command

        #region Public Functions
        public bool InitializeFromCamera(PhotometricsCamera activeCamera)
        {
            _ActiveCamera = activeCamera;
            //_ActiveCamera.CCDCoolerSetPoint = -20;
            try
            {
                //if (_ActiveCamera.Open())
                if (_ActiveCamera.IsConnected)
                {
                    IsConnected = true;
                    IsRestarting = false;
                    WorkingStatus = CameraStatusEnums.Idle;

                    ReadoutOptions.Clear();
                    for (int i = 0; i < _ActiveCamera.ReadoutOption.Count; i++)
                    {
                        string portDesc = string.Empty;
                        if (_ActiveCamera.ReadoutOption[i].Speed == 0)
                        {
                            portDesc = string.Format("Fast (Speed: {0}, Bit Depth: {1})",
                                _ActiveCamera.ReadoutOption[i].Speed,
                                _ActiveCamera.ReadoutOption[i].BitDepth);
                        }
                        else if (_ActiveCamera.ReadoutOption[i].Speed == 1)
                        {
                            portDesc = portDesc = string.Format("Normal (Speed: {0}, Bit Depth: {1})",
                                _ActiveCamera.ReadoutOption[i].Speed,
                                _ActiveCamera.ReadoutOption[i].BitDepth);
                        }
                        ReadoutType readout = new ReadoutType(i, _ActiveCamera.ReadoutOption[i].Speed, portDesc);
                        ReadoutOptions.Add(readout);
                    }
                    if (ReadoutOptions != null && ReadoutOptions.Count > 0)
                    {
                        SelectedReadout = ReadoutOptions[0];
                    }
                }
                else
                {
                    IsConnected = false;
                }
            }
            catch
            {
                IsConnected = false;
            }
            return IsConnected;
        }
        public void CloseCamera()
        {
            if (!IsMachineRev2)
            {
                _ActiveCamera.Close();
            }
            else
            {
                LucidCameraManager.CloseCameras();
            }
        }
        public bool InitializeFromEthernetCameras(ILucidCamera ethernetCameraA, ILucidCamera ethernetCameraB)
        {
            LucidCameraManager.OnCameraUpdated += LucidCameraManager_OnCameraUpdated;
            if (LucidCameraManager.OpenCameras())
            {
                _EthernetCameraA = ethernetCameraA;// LucidCameraManager.GetCamera(0);
                _EthernetCameraB = ethernetCameraB;// LucidCameraManager.GetCamera(1);
                TheDispatcher.Invoke(new Action(() =>
                {
                    EthernetCameraOptions.Add(_EthernetCameraA);
                    EthernetCameraOptions.Add(_EthernetCameraB);
                    SelectedCamera = EthernetCameraOptions[0];
                }));
                if(SelectedCamera != null)
                {
                    RoiWidth = SelectedCamera.ImagingColumns;
                    RoiHeight = SelectedCamera.ImagingRows;
                    IsConnected = true;
                    WorkingStatus = CameraStatusEnums.Idle;
                    if (_EthernetCameraA != null)
                    {
                        ((LucidCamera)_EthernetCameraA).OnTriggerStartRequested += _EthernetCamera_OnTriggerStartRequested;
                    }
                    if (_EthernetCameraB != null)
                    {
                        ((LucidCamera)_EthernetCameraB).OnTriggerStartRequested += _EthernetCamera_OnTriggerStartRequested;
                    }
                }
                else
                {
                    string errorMessage = "Failed to select Ethernet Camera";
                    Logger.LogError(errorMessage);
                }
            }
            else
            {
                string errorMessage = "Failed to connect to Ethernet Cameras";
                Logger.LogError(errorMessage);
                IsConnected = false;
            }
            return IsConnected;
        }

        private void LucidCameraManager_OnCameraUpdated()
        {
            _EthernetCameraA = LucidCameraManager.GetCamera(0);
            _EthernetCameraB = LucidCameraManager.GetCamera(1);
            if (_EthernetCameraA != null)
            {
                ((LucidCamera)_EthernetCameraA).OnTriggerStartRequested += _EthernetCamera_OnTriggerStartRequested;
            }
            if (_EthernetCameraB != null)
            {
                ((LucidCamera)_EthernetCameraB).OnTriggerStartRequested += _EthernetCamera_OnTriggerStartRequested;
            }
        }

        private void _EthernetCamera_OnTriggerStartRequested()
        {
            while (MainBoardVM.LEDController.SendCameraTrigger() == false) ;
        }
        #endregion Public Functions
    }
}
