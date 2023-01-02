using Sequlite.ALF.Common;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    public class ImageCaptureCommand : ThreadBase
    {
        // Image capture status delegate
        public delegate void CommandStatusHandler(object sender, string status);
        // Image capture status event
        public event CommandStatusHandler CommandStatus;
        // Image capture completion time estimate delegate
        public delegate void CommandCompletionEstHandler(ThreadBase sender, DateTime dateTime, double estTime);
        // Image capture completion time estimate event
        public event CommandCompletionEstHandler CompletionEstimate;

        public delegate void BadImageCapturedHandle();
        public event BadImageCapturedHandle OnCaptureBadImage;

        public delegate void OnLEDFailureHandle();
        public event OnLEDFailureHandle OnLEDFailure;

        public delegate void LEDStateChangedHandle(LEDTypes led, bool newState);
        public event LEDStateChangedHandle OnLEDStateChanged;

        private Dispatcher _CallingDispatcher = null;
        private MainBoard.Mainboard _MainBoard = null;
        private ICamera _ActiveCamera = null;
        private ImageChannelSettings _ImageSetting = null;
        private WriteableBitmap _CapturedImage = null;
        private Image.Processing.ImageInfo _ImageInfo = null;
        private Int32Rect _RoiRect;
        private bool _IsCommandAborted = false;
        private int _PDValue;
        private bool _IsBadImage = false;
        private bool _ledStateGet = false;
        //private int _LEDFailure;
        private System.Timers.Timer _PDTimer;
        private LEDController _LEDController;
        private bool _IsMachineRev2;

        public string ErrorMessage { get; set; }
        public ImageCaptureCommand(Dispatcher callingDispatcher,
            MainBoard.Mainboard mainboard,
            ICamera camera,
            ImageChannelSettings imagechannel,
            Int32Rect roiRect,
            LEDController ledController,
            bool isMachineRev2)
        {
            _CallingDispatcher = callingDispatcher;
            _MainBoard = mainboard;
            _ActiveCamera = camera;
            _ImageSetting = imagechannel;
            _RoiRect = roiRect;
            _LEDController = ledController;
            _IsMachineRev2 = isMachineRev2;
        }

        public WriteableBitmap CapturedImage
        {
            get { return _CapturedImage; }
        }

        public Image.Processing.ImageInfo ImageInfo
        {
            get { return _ImageInfo; }
        }

        public bool IsFailedToSetLED { get; set; }
        public bool IsFailedToCaptureImage { get; set; }

        public override void ThreadFunction()
        {
            CommandStatus?.Invoke(this, "Preparing to capture...");


            #region Estimated times setup
            double cameraDownloadTime = (_ImageSetting.ReadoutSpeed == 0) ? 10.0 : 1.5;
            int iBinningFactor = _ImageSetting.BinningMode;
            double estCaptureTime = _ImageSetting.Exposure + (cameraDownloadTime / (iBinningFactor * iBinningFactor));
            DateTime dateTime = DateTime.Now;
            _ImageInfo = new Image.Processing.ImageInfo();
            _ImageInfo.DateTime = System.String.Format("{0:G}", dateTime.ToString());
            _ImageInfo.MixChannel.Exposure = _ImageSetting.Exposure;
            _ImageInfo.BinFactor = _ImageSetting.BinningMode;
            _ImageInfo.ReadoutSpeed = (_ImageSetting.ReadoutSpeed == 0) ? "Normal" : "Fast";
            _ImageInfo.GainValue = _ImageSetting.AdGain;
            _ImageInfo.MixChannel.LightSource = _ImageSetting.LED.ToString();
            _ImageInfo.MixChannel.LightIntensity = (int)_ImageSetting.LedIntensity;
            CompletionEstimate?.Invoke(this, dateTime, estCaptureTime);
            #endregion Estimated times setup

            CommandStatus?.Invoke(this, "Capturing image...");

            try
            {
                #region Camera setup
                //Set binning mode
                _ActiveCamera.HBin = _ImageSetting.BinningMode;
                _ActiveCamera.VBin = _ImageSetting.BinningMode;
                if (!_IsMachineRev2)
                {
                    //Set CCD readout speed (0: Normal, 1: Fast)
                    _ActiveCamera.ReadoutSpeed = _ImageSetting.ReadoutSpeed;
                }
                else
                {
                    // Set ADC Bit Depth & Pixel Format
                    _ActiveCamera.ADCBitDepth = _ImageSetting.ADCBitDepth;
                    _ActiveCamera.PixelFormatBitDepth = _ImageSetting.PixelFormatBitDepth;
                }
                //Set gain
                _ActiveCamera.Gain = _ImageSetting.AdGain;
                //Set region of interest
                if (_RoiRect.Width > 0 && _RoiRect.Height > 0 && !_ImageSetting.IsCaptureFullRoi)
                {
                    _ActiveCamera.RoiStartX = (ushort)_RoiRect.X;
                    _ActiveCamera.RoiWidth = (ushort)(_RoiRect.Width);
                    _ActiveCamera.RoiStartY = (ushort)_RoiRect.Y;
                    _ActiveCamera.RoiHeight = (ushort)(_RoiRect.Height);
                }
                else if (_ImageSetting.IsCaptureFullRoi)
                {
                    _ActiveCamera.RoiStartX = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiLeft;
                    _ActiveCamera.RoiStartY = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiTop;
                    _ActiveCamera.RoiWidth = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth;
                    _ActiveCamera.RoiHeight = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight;
                    if(SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth == 0 && SettingsManager.ConfigSettings.CameraDefaultSettings.RoiLeft == 0) {_ActiveCamera.RoiWidth = (ushort)(_ActiveCamera.ImagingColumns);}
                    if(SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight == 0 && SettingsManager.ConfigSettings.CameraDefaultSettings.RoiTop == 0) { _ActiveCamera.RoiHeight = (ushort)(_ActiveCamera.ImagingRows); }
                    if (!_IsMachineRev2) { _ActiveCamera.RoiStartX = _ActiveCamera.ImagingColumns - 1 - (_ActiveCamera.RoiWidth + _ActiveCamera.RoiStartX); }

                }
                else
                {
                    _ActiveCamera.RoiStartX = 0;
                    _ActiveCamera.RoiStartY = 0;
                    _ActiveCamera.RoiWidth = _ActiveCamera.ImagingColumns / _ActiveCamera.HBin - 1;
                    _ActiveCamera.RoiHeight = _ActiveCamera.ImagingRows / _ActiveCamera.VBin - 1;
                }
                #endregion Camera setup
                Int32Rect roiRect = new Int32Rect(_ActiveCamera.RoiStartX, _ActiveCamera.RoiStartY, _ActiveCamera.RoiWidth, _ActiveCamera.RoiHeight);
                if (!_IsMachineRev2) {roiRect = new Int32Rect((_ActiveCamera.ImagingColumns - 1 - (_ActiveCamera.RoiWidth + _ActiveCamera.RoiStartX)), _ActiveCamera.RoiStartY, _ActiveCamera.RoiWidth, _ActiveCamera.RoiHeight); }

                _ImageInfo.MixChannel.ROI = roiRect;
                int imagenum = 1;
                WriteableBitmap _Image = null;
                ImageArithmetic ImageProcessor = new ImageArithmetic();
                if (!_IsMachineRev2)
                {
                    _MainBoard.SetLEDIntensity(_ImageSetting.LED, _ImageSetting.LedIntensity);
                }
                else
                {
                    while(_LEDController.SetLEDIntensity(_ImageSetting.LED, (int)_ImageSetting.LedIntensity) == false)
                    {
                        OnLEDFailure?.Invoke();
                    }
                    while(_LEDController.SetLEDControlledByCamera(_ImageSetting.LED, true) == false)
                    {
                        OnLEDFailure?.Invoke();
                    }
                    ((LucidCamera)_ActiveCamera).EnableTriggerMode = false;     // disable trigger mode by default
                    _ledStateGet = true;
                }

                // for ALF2.0 Lucid Camera, it will not send OnExposureChanged event, actually it will turn on LED by hardware signal
                _ActiveCamera.OnExposureChanged += _ActiveCamera_OnExposureChanged;
                if (_ImageSetting.IsCaptureAvg)
                {
                    imagenum = 5;
                }
                for (int i = 0; i < imagenum; i++)
                {

                    int tryCounts = 0;
                    double ccdExposure;
                    do
                    {
                        //double ccdExposure = _ImageSetting.Exposure + _ImageSetting.ExtraExposure;
                        ccdExposure = _ImageSetting.Exposure;
                        if (_IsBadImage)
                        {
                            ccdExposure = 0.2;
                        }
                        _ActiveCamera.GrabImage(ccdExposure, CaptureFrameType.Normal, ref _CapturedImage);
                        if (_CapturedImage != null)
                        {
                            _IsBadImage = Sequlite.Image.Processing.BadImageIdentifier.IsBadImage(_CapturedImage);
                        }

                        if (_IsBadImage || _CapturedImage == null || !_ledStateGet)
                        {
                            if (_IsBadImage)
                            {
                                _CapturedImage.Freeze();
                                OnCaptureBadImage?.Invoke();
                            }

                            else if(_ledStateGet)
                            {
                                OnLEDFailure?.Invoke();
                            }

                            tryCounts += 1;
                            if (tryCounts == 2 || tryCounts == 10)
                            {
                                _ActiveCamera.Close();
                                Thread.Sleep(1500);
                                _ActiveCamera.Open();
                                Thread.Sleep(100);

                                #region Camera setup
                                //Set binning mode
                                _ActiveCamera.HBin = _ImageSetting.BinningMode;
                                _ActiveCamera.VBin = _ImageSetting.BinningMode;
                                if (!_IsMachineRev2)
                                {
                                    //Set CCD readout speed (0: Normal, 1: Fast)
                                    _ActiveCamera.ReadoutSpeed = _ImageSetting.ReadoutSpeed;
                                }
                                else
                                {
                                    // Set ADC Bit Depth & Pixel Format
                                    _ActiveCamera.ADCBitDepth = _ImageSetting.ADCBitDepth;
                                    _ActiveCamera.PixelFormatBitDepth = _ImageSetting.PixelFormatBitDepth;
                                }
                                //Set gain
                                _ActiveCamera.Gain = _ImageSetting.AdGain;
                                //Set region of interest
                                if (_RoiRect.Width > 0 && _RoiRect.Height > 0 && !_ImageSetting.IsCaptureFullRoi)
                                {
                                    _ActiveCamera.RoiStartX = (ushort)_RoiRect.X;
                                    _ActiveCamera.RoiWidth = (ushort)(_RoiRect.Width);
                                    _ActiveCamera.RoiStartY = (ushort)_RoiRect.Y;
                                    _ActiveCamera.RoiHeight = (ushort)(_RoiRect.Height);
                                }
                                else if (_ImageSetting.IsCaptureFullRoi)
                                {
                                    _ActiveCamera.RoiStartX = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiLeft;
                                    _ActiveCamera.RoiStartY = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiTop;
                                    _ActiveCamera.RoiWidth = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth;
                                    _ActiveCamera.RoiHeight = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight;
                                    _ActiveCamera.RoiStartX = _ActiveCamera.ImagingColumns - 1 - (_ActiveCamera.RoiWidth + _ActiveCamera.RoiStartX);
                                }
                                else
                                {
                                    _ActiveCamera.RoiStartX = 0;
                                    _ActiveCamera.RoiStartY = 0;
                                    _ActiveCamera.RoiWidth = _ActiveCamera.ImagingColumns / _ActiveCamera.HBin - 1;
                                    _ActiveCamera.RoiHeight = _ActiveCamera.ImagingRows / _ActiveCamera.VBin - 1;
                                }
                                #endregion Camera setup
                            }
                            if (tryCounts > 20)
                            {
                                ExitStat = ThreadExitStat.Error;
                                IsFailedToCaptureImage = true;
                                ErrorMessage = "Capture Failure";
                                return;
                            }
                        }
                    }
                    while (_IsBadImage || _CapturedImage == null || !_ledStateGet || ccdExposure != _ImageSetting.Exposure);

                    if (_ImageSetting.IsCaptureAvg)
                    {
                        _CapturedImage = ImageProcessor.Divide(_CapturedImage, 5);
                        if (i == 0)
                        {
                            _Image = _CapturedImage.Clone();

                        }
                        else
                        {
                            _Image = ImageProcessor.AddImage(ref _Image, ref _CapturedImage);
                        }


                    }

                    if (_IsMachineRev2)
                    {
                        _LEDController.GetPDSampledValue();
                        _PDValue = (int)_LEDController.PDSampleValue;
                        _ImageInfo.MixChannel.PDValue = _PDValue;

                        _ImageInfo.CameraSerialNumber = ((LucidCamera)_ActiveCamera).SerialNumber;
                        _ImageInfo.ImagingChannel = ((LucidCamera)_ActiveCamera).Channels;
                    }

                }
                if (_ImageSetting.IsCaptureAvg)
                {
                    _CapturedImage = _Image;
                }
                if (_CapturedImage != null && !_IsMachineRev2)
                {
                    //TransformedBitmap tb = new TransformedBitmap();
                    //tb.BeginInit();
                    //tb.Source = _CapturedImage;
                    //System.Windows.Media.ScaleTransform transform = new System.Windows.Media.ScaleTransform();
                    //transform.ScaleX = -1;
                    //tb.Transform = transform;
                    //tb.EndInit();
                    //_CapturedImage = new WriteableBitmap(tb);
                    _CapturedImage = ImageProcessing.WpfFlip(_CapturedImage, ImageProcessing.FlipAxis.Horizontal);
                }

                if (_CapturedImage.CanFreeze) { _CapturedImage.Freeze(); }
                CommandStatus?.Invoke(this, string.Empty);
            }
            catch (System.Threading.ThreadAbortException)
            {
                // don't throw exception if the user abort the process.
            }
            catch (System.Runtime.InteropServices.SEHException)
            {
                // The SEHException class handles SEH errors that are thrown from unmanaged code,
                // but have not been mapped to another .NET Framework exception.
                ErrorMessage = "Memory issue";
                throw new OutOfMemoryException();
            }
            catch (System.Runtime.InteropServices.COMException cex)
            {
                if (cex.ErrorCode == unchecked((int)0x88980003))
                {
                    throw new OutOfMemoryException();
                }
                else
                {
                    ErrorMessage = "Image capture error. COM";
                    throw new Exception("Image capture error. COM", cex);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "LED Failure")
                {
                    _ActiveCamera.StopCapture();
                    throw new Exception(ex.ToString());
                }
                else
                {
                    if (!_IsCommandAborted)
                    {
                        _ActiveCamera.StopCapture();
                    }
                    ErrorMessage = string.Format("Image capture error. {0}", ex.ToString());
                    throw new Exception(string.Format("Image capture error. {0}", ex.ToString()), ex);
                }
            }
            finally
            {
                if (!_IsMachineRev2)
                {
                    _MainBoard.SetLEDStatus(_ImageSetting.LED, false);
                }
                else
                {
                    //_LEDController.SetLEDControlledByCamera(_ImageSetting.LED, false);
                }
                _PDTimer?.Dispose();
                _ActiveCamera.OnExposureChanged -= _ActiveCamera_OnExposureChanged;
            }
        }

        private void _ActiveCamera_OnExposureChanged(bool starts)
        {
            _ledStateGet = false;
            if (starts && !_IsBadImage)
            {
                //int tryLEDCounts = 0;
                //do
                //{
                //    if (++tryLEDCounts > 2)
                //    {
                //        _MainBoard.SetLEDStatus(_ImageSetting.LED, false);
                //        OnLEDStateChanged?.Invoke(_ImageSetting.LED, false);
                //        ExitStat = ThreadExitStat.Error;
                //        ErrorMessage = "LED fail to turn on";
                //        IsFailedToSetLED = true;
                //        throw new Exception(ErrorMessage);
                //    }
                //    _MainBoard.SetLEDStatus(_ImageSetting.LED, true);
                //    Thread.Sleep(5);
                //    _MainBoard.GetLEDStatus(_ImageSetting.LED);
                //    Thread.Sleep(5);
                //    switch (_ImageSetting.LED)
                //    {
                //        case LEDTypes.Green:
                //            _ledStateGet = _MainBoard.IsGLEDOn;
                //            break;
                //        case LEDTypes.Red:
                //            _ledStateGet = _MainBoard.IsRLEDOn;
                //            break;
                //        case LEDTypes.White:
                //            _ledStateGet = _MainBoard.IsWLEDOn;
                //            break;
                //    }

                //}
                //while (_ledStateGet == false);
                _ledStateGet = true;
                _PDTimer = new System.Timers.Timer();
                _PDTimer.Interval = 200;
                _PDTimer.AutoReset = false;
                _PDTimer.Elapsed += _PDTimer_Elapsed;
                OnLEDStateChanged?.Invoke(_ImageSetting.LED, true);
                _PDTimer.Start();


                //Stopwatch sw = Stopwatch.StartNew();

                //MessageBox.Show(String.Format("set command used:{0}", sw.ElapsedMilliseconds));
                //Thread.Sleep((int)(_ImageSetting.Exposure * 1000 - 30));
                //_MainBoard.GetPDValue();
                //_PDValue = (int)_MainBoard.PDValue;
                //_ImageInfo.MixChannel.PDValue = _PDValue;
                //int trycount = 0;
                //bool _isLEDon = true;
                //do
                //{
                //    _MainBoard.SetLEDStatus(_ImageSetting.LED, false);
                //    if (trycount++ > 5)
                //    {
                //        ExitStat = ThreadExitStat.Error;
                //        ErrorMessage = "Failed to turn off LED";
                //        throw new Exception(ErrorMessage);
                //    }

                //    Thread.Sleep(1);
                //    _MainBoard.GetLEDStatus(_ImageSetting.LED);
                //    Thread.Sleep(1);
                //    switch (_ImageSetting.LED)
                //    {
                //        case LEDTypes.Green:
                //            _isLEDon = _MainBoard.IsGLEDOn;
                //            break;
                //        case LEDTypes.Red:
                //            _isLEDon = _MainBoard.IsRLEDOn;
                //            break;
                //        case LEDTypes.White:
                //            _isLEDon = _MainBoard.IsWLEDOn;
                //            break;
                //    }
                //}
                //while (_isLEDon);
                //OnLEDStateChanged?.Invoke(_ImageSetting.LED, false);
                //MessageBox.Show(_MainBoard.HWVersion);
                //if (_MainBoard.HWVersion == "1.0.0.0" && _ImageSetting.LED != LEDTypes.White)
                //{
                //    if (_PDValue < 10)
                //    {
                //        if (_LEDFailure++ > 3)
                //        {
                //            ExitStat = ThreadExitStat.Error;
                //            ErrorMessage = "LED Failure";
                //            throw new Exception(ErrorMessage);
                //        }
                //        _ledStateGet = false;
                //    }
                //}
            }
        }
        private void _PDTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_IsMachineRev2)
            {
                _MainBoard.GetPDValue();
                _PDValue = (int)_MainBoard.PDValue;
            }
            else
            {
                _LEDController.GetPDValue();
                _PDValue = _LEDController.PDValue;
            }
            _ImageInfo.MixChannel.PDValue = _PDValue;
        }
        public override void Finish()
        {
            CommandStatus?.Invoke(this, string.Empty);
        }

        public override void AbortWork()
        {
            _IsCommandAborted = true;
            try
            {
                _ActiveCamera.StopCapture();
                System.Threading.Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
