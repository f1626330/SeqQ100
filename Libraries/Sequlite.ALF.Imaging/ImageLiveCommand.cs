using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    public class ImageLiveCommand : ThreadBase
    {
        // Image received delegate
        public delegate void ImageReceivedHandler(BitmapSource displayBitmap);
        /// <summary>
        /// Live image received event handler. Triggered everytime the live image is received.
        /// </summary>
        public event ImageReceivedHandler LiveImageReceived;

        public delegate void LEDStateChangedHandle(LEDTypes led, bool newState);
        public event LEDStateChangedHandle OnLEDStateChanged;

        public delegate void CaptureLiveHandle(WriteableBitmap capturedImage, Image.Processing.ImageInfo info);
        public event CaptureLiveHandle OnCaptureLiveImage;

        public delegate void BadImageCapturedHandle();
        public event BadImageCapturedHandle OnCaptureBadImage;

        #region private field/data...
        private Dispatcher _CallingDispatcher = null;
        private ICamera _ActiveCamera = null;
        private Mainboard _Mainboard;
        private BitmapSource _DisplayBitmap;

        private ImageChannelSettings _ImageChannel = null;
        private Int32Rect _RoiRect;
        private bool _IsCommandAborted = false;
        //private bool _IsUserVersion = false;
        private bool _IsAutoDisplayRange = false;
        private bool _IsCapturingImage = false;
        private Image.Processing.ImageInfo _ImageInfo;
        private LEDController _LEDController;
        private bool _IsMachineRev2;
        private WriteableBitmap _CapturedLiveImage;
        #endregion

        public bool IsFailedToSetLED { get; set; }

        public ImageLiveCommand(Dispatcher callingDispatcher,
            ICamera camera,
            Mainboard mainboard,
            ImageChannelSettings imageChannel,
            Int32Rect cropRect,
            bool autoDisplayRange,
            LEDController ledController,
            bool isMachineRev2)
        {
            _CallingDispatcher = callingDispatcher;
            _ActiveCamera = camera;
            _Mainboard = mainboard;
            _ImageChannel = imageChannel;
            _RoiRect = cropRect;
            _IsAutoDisplayRange = autoDisplayRange;
            _LEDController = ledController;
            _IsMachineRev2 = isMachineRev2;
        }

        public override void Initialize()
        {
        }

        public override void Finish()
        {
            _ActiveCamera.CameraNotif -= MVCameraLive_CameraNotif;
            _DisplayBitmap = null;
        }

        public override void ThreadFunction()
        {
            //Set binning mode
            _ActiveCamera.HBin = _ImageChannel.BinningMode;
            _ActiveCamera.VBin = _ImageChannel.BinningMode;
            if (!_IsMachineRev2)
            {
                //Set CCD readout speed (0: Normal, 1: Fast)
                _ActiveCamera.ReadoutSpeed = _ImageChannel.ReadoutSpeed;
            }
            else
            {
                // Set ADC Bit Depth & Pixel Format
                _ActiveCamera.ADCBitDepth = _ImageChannel.ADCBitDepth;
                _ActiveCamera.PixelFormatBitDepth = _ImageChannel.PixelFormatBitDepth;
            }
            //Set gain
            _ActiveCamera.Gain = _ImageChannel.AdGain;
            //Set region of interest
            if (_RoiRect.Width > 0 && _RoiRect.Height > 0)
            {
                _ActiveCamera.RoiStartX = (ushort)_RoiRect.X;
                _ActiveCamera.RoiWidth = (ushort)(_RoiRect.Width);
                _ActiveCamera.RoiStartY = (ushort)_RoiRect.Y;
                _ActiveCamera.RoiHeight = (ushort)(_RoiRect.Height);
            }
            else
            {
                _ActiveCamera.RoiStartX = 0;
                _ActiveCamera.RoiStartY = 0;
                _ActiveCamera.RoiWidth = _ActiveCamera.ImagingColumns / _ActiveCamera.HBin;
                _ActiveCamera.RoiHeight = _ActiveCamera.ImagingRows / _ActiveCamera.VBin;
            }

            try
            {
                #region Set LED status, return if failed
                SetLEDIntensity(_ImageChannel.LED, (int)_ImageChannel.LedIntensity);
                if (_IsMachineRev2)
                {
                    _LEDController.SetLEDControlledByCamera(_ImageChannel.LED, true);
                }
                else
                {
                    bool ledStateGet = false;
                    int tryCounts = 0;
                    //do
                    //{
                    //    if (++tryCounts > 5)
                    //    {
                    //        SetLEDStatus(_ImageChannel.LED, false);
                    //        OnLEDStateChanged?.Invoke(_ImageChannel.LED, false);
                    //        ExitStat = ThreadExitStat.Error;
                    //        IsFailedToSetLED = true;
                    //        return;
                    //    }
                    //    SetLEDStatus(_ImageChannel.LED, true);
                    //    ledStateGet = GetLEDStatus(_ImageChannel.LED);
                    //}
                    //while (ledStateGet == false);
                    OnLEDStateChanged?.Invoke(_ImageChannel.LED, true);
                }
                #endregion Set LED status, return if failed

                _ActiveCamera.CameraNotif += MVCameraLive_CameraNotif;

                if (_IsMachineRev2)
                {
                    ((LucidCamera)_ActiveCamera).EnableTriggerMode = false;
                }

                _ActiveCamera.StartContinuousMode(_ImageChannel.Exposure);

                while ((_ActiveCamera.IsAcqRunning && !_IsCommandAborted) || _IsCapturingImage)
                {
                    // See: link why Sleep(1) is better than Sleep(0)
                    // http://joeduffyblog.com/2006/08/22/priorityinduced-starvation-why-sleep1-is-better-than-sleep0-and-the-windows-balance-set-manager/
                    System.Threading.Thread.Sleep(1);
                }
            }
            catch (System.Threading.ThreadAbortException)
            {
                // don't throw exception if the user abort the process.
            }
            catch (System.Runtime.InteropServices.SEHException)
            {
                // The SEHException class handles SEH errors that are thrown from unmanaged code,
                // but have not been mapped to another .NET Framework exception.
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
                    throw cex;
                }
            }
            catch (Exception ex)
            {
                if (!_IsCommandAborted)
                {
                    _ActiveCamera.StopCapture();
                }
                throw new Exception("Live mode error.", ex);
            }
        }

        private void MVCameraLive_CameraNotif(object sender)
        {
            _CallingDispatcher.Invoke(new Action(() =>
            {
                WriteableBitmap srcImg;
                if (!_IsMachineRev2)
                {
                    srcImg = sender as WriteableBitmap;
                }
                else
                {
                    byte[] imagedata;
                    if (_ActiveCamera is ILucidCamera)
                    {
                         imagedata = (sender as CameraNotifArgs).ImageRef;// byte[];
                    }
                    else
                    {
                       imagedata = (sender as byte[]);
                    }
                    srcImg = LucidCamera.ToWriteableBitmap(_ActiveCamera.RoiWidth, _ActiveCamera.RoiHeight, imagedata, _ActiveCamera.PixelFormatBitDepth);
                }

                if (srcImg != null)
                {
                    if (!_IsMachineRev2)
                    {
                        //TransformedBitmap tb = new TransformedBitmap();
                        //tb.BeginInit();
                        //tb.Source = srcImg;
                        //ScaleTransform transform = new System.Windows.Media.ScaleTransform();
                        //transform.ScaleX = -1;
                        //tb.Transform = transform;
                        //tb.EndInit();
                        //srcImg = new WriteableBitmap(tb);
                        srcImg = ImageProcessing.WpfFlip(srcImg, ImageProcessing.FlipAxis.Horizontal);
                    }
                    else
                    {
                        if (_IsCapturingImage)
                        {
                            _CapturedLiveImage = srcImg.Clone();
                            _IsCapturingImage = false;
                        }
                    }

                    if (srcImg.CanFreeze)
                    {
                        srcImg.Freeze();
                    }

                    if (_IsAutoDisplayRange)
                    {
                        int width = srcImg.PixelWidth;
                        int height = srcImg.PixelHeight;
                        BitmapPalette palette = new BitmapPalette(ImageProcessing.GetColorTableIndexed(false));
                        //PixelFormat dstPixelFormat = PixelFormats.Indexed8;
                        PixelFormat dstPixelFormat = srcImg.Format;
                        Sequlite.Image.Processing.ImageInfo sequliteImageInfo = new Sequlite.Image.Processing.ImageInfo();
                        WriteableBitmap targetImg = new WriteableBitmap(width, height, 96, 96, dstPixelFormat, palette);
                        sequliteImageInfo.SelectedChannel = Sequlite.Image.Processing.ImageChannelType.Mix;
                        sequliteImageInfo.MixChannel.IsAutoChecked = true;
                        targetImg.Lock();
                        ImageProcessingHelper.UpdateDisplayImage(
                            ref srcImg, sequliteImageInfo, ref targetImg);
                        targetImg.AddDirtyRect(new Int32Rect(0, 0, width, height));
                        targetImg.Unlock();
                        _DisplayBitmap = targetImg;
                    }
                    else
                    {
                        _DisplayBitmap = srcImg;
                    }

                    LiveImageReceived?.Invoke(_DisplayBitmap);
                }
            }));
        }

        public override void AbortWork()
        {
            _IsCommandAborted = true;
            _ActiveCamera.CameraNotif -= MVCameraLive_CameraNotif;

            if (_IsMachineRev2)
            {
                //_LEDController.SetLEDControlledByCamera(_ImageChannel.LED, false);
            }
            else
            {
                SetLEDStatus(_ImageChannel.LED, false);
                OnLEDStateChanged?.Invoke(_ImageChannel.LED, false);
            }
            try
            {
                _ActiveCamera.StopCapture();
                System.Threading.Thread.Sleep(200);
            }
            catch (Exception)
            {
                //System.Diagnostics.Debug.WriteLine(ex.Message);
                throw;
            }
        }

        public void CaptureLiveImage()
        {
            #region Process for 1.x machine
            if (!_IsMachineRev2)
            {
                _IsCapturingImage = true;
                _ActiveCamera.StopAcquisition();

                while (_ActiveCamera.IsAcqRunning) { Thread.Sleep(1); }

                if (_ImageChannel.IsCaptureFullRoi)
                {
                    _ActiveCamera.RoiStartX = 0;
                    _ActiveCamera.RoiStartY = 0;
                    _ActiveCamera.RoiWidth = _ActiveCamera.ImagingColumns / _ActiveCamera.HBin;
                    _ActiveCamera.RoiHeight = _ActiveCamera.ImagingRows / _ActiveCamera.VBin;
                }

                bool isBadImage = false;
                int tryCounts = 0;
                WriteableBitmap capturedImage = null;
                _ImageInfo = new Image.Processing.ImageInfo();
                do
                {
                    int pdValue = GetPDValue();
                    _ImageInfo.DateTime = System.String.Format("{0:G}", DateTime.Now.ToString());
                    _ImageInfo.InstrumentModel = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName;
                    _ImageInfo.MixChannel.Exposure = _ImageChannel.Exposure;
                    _ImageInfo.MixChannel.PDValue = pdValue;
                    _ImageInfo.BinFactor = _ImageChannel.BinningMode;
                    if (!_IsMachineRev2)
                    {
                        _ImageInfo.ReadoutSpeed = (_ImageChannel.ReadoutSpeed == 0) ? "Normal" : "Fast";
                    }
                    else
                    {
                        // to do: add adc bit depth & pixel format to image info?
                    }
                    _ImageInfo.GainValue = _ImageChannel.AdGain;
                    _ImageInfo.MixChannel.LightSource = _ImageChannel.LED.ToString();
                    _ImageInfo.MixChannel.LightIntensity = (int)_ImageChannel.LedIntensity;

                    _ActiveCamera.GrabImage(_ImageChannel.Exposure, CaptureFrameType.Normal, ref capturedImage);
                    if (capturedImage != null && _ImageChannel.IsEnableBadImageCheck)
                    {
                        isBadImage = Sequlite.Image.Processing.BadImageIdentifier.IsBadImage(capturedImage);
                    }
                    if (isBadImage)
                    {
                        //_ActiveCamera.Close();
                        //Thread.Sleep(100);
                        //_ActiveCamera.Open();
                        //Thread.Sleep(100);
                        OnCaptureBadImage?.Invoke();
                    }
                    tryCounts++;
                }
                while (isBadImage || tryCounts > 5);
                if (capturedImage != null)
                {
                    if (!_IsMachineRev2)
                    {
                        //TransformedBitmap tb = new TransformedBitmap();
                        //tb.BeginInit();
                        //tb.Source = capturedImage;
                        //System.Windows.Media.ScaleTransform transform = new System.Windows.Media.ScaleTransform();
                        //transform.ScaleX = -1;
                        //tb.Transform = transform;
                        //tb.EndInit();
                        //capturedImage = new WriteableBitmap(tb);
                        capturedImage = ImageProcessing.WpfFlip(capturedImage, ImageProcessing.FlipAxis.Horizontal);
                    }

                    if (capturedImage.CanFreeze) { capturedImage.Freeze(); }
                }
                OnCaptureLiveImage?.Invoke(capturedImage, _ImageInfo);
                if (_ImageChannel.IsCaptureFullRoi)
                {
                    _ActiveCamera.RoiStartX = (ushort)_RoiRect.X;
                    _ActiveCamera.RoiWidth = (ushort)(_RoiRect.Width);
                    _ActiveCamera.RoiStartY = (ushort)_RoiRect.Y;
                    _ActiveCamera.RoiHeight = (ushort)(_RoiRect.Height);
                }
                _ActiveCamera.StartContinuousMode(_ImageChannel.Exposure);
                _IsCapturingImage = false;
            }
            #endregion Process for 1.x machine

            #region Process for 2.x machine
            else
            {
                Task.Factory.StartNew(() =>
                {
                    _CapturedLiveImage = null;
                    _IsCapturingImage = true;
                    while (_IsCapturingImage)
                    {
                        Thread.Sleep(1);
                    }
                    if (_CapturedLiveImage == null)
                    {
                        return;
                    }
                    _ImageInfo = new Image.Processing.ImageInfo();
                    int pdValue = GetPDValue();
                    _ImageInfo.DateTime = System.String.Format("{0:G}", DateTime.Now.ToString());
                    _ImageInfo.InstrumentModel = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName;
                    _ImageInfo.MixChannel.Exposure = _ImageChannel.Exposure;
                    _ImageInfo.MixChannel.PDValue = pdValue;
                    _ImageInfo.BinFactor = _ImageChannel.BinningMode;
                    // to do: add adc bit depth & pixel format to image info?
                    _ImageInfo.GainValue = _ImageChannel.AdGain;
                    _ImageInfo.MixChannel.LightSource = _ImageChannel.LED.ToString();
                    _ImageInfo.MixChannel.LightIntensity = (int)_ImageChannel.LedIntensity;
                    _ImageInfo.CameraSerialNumber = ((LucidCamera)_ActiveCamera).SerialNumber;
                    _ImageInfo.ImagingChannel = ((LucidCamera)_ActiveCamera).Channels;

                    _CallingDispatcher.Invoke(() =>
                    {
                        _CapturedLiveImage.Freeze();
                        OnCaptureLiveImage?.Invoke(_CapturedLiveImage, _ImageInfo);
                    });
                });
            }
            #endregion Process for 2.x machine
        }

        private void SetLEDIntensity(LEDTypes led, int intensity)
        {
            if (!_IsMachineRev2)
            {
                _Mainboard.SetLEDIntensity(led, (uint)intensity);
            }
            else
            {
                _LEDController.SetLEDIntensity(led, intensity);
            }
        }
        private void SetLEDStatus(LEDTypes led, bool setOn)
        {
            if (!_IsMachineRev2)
            {
                _Mainboard.SetLEDStatus(led, setOn);
            }
            else
            {
                _LEDController.SetLEDStatus(led, setOn);
            }
        }
        private bool GetLEDStatus(LEDTypes led)
        {
            if (!_IsMachineRev2)
            {
                _Mainboard.GetLEDStatus(led);
                switch (led)
                {
                    case LEDTypes.Green:
                        return _Mainboard.IsGLEDOn;
                    case LEDTypes.Red:
                        return _Mainboard.IsRLEDOn;
                    case LEDTypes.White:
                        return _Mainboard.IsWLEDOn;
                    default:
                        return false;
                }
            }
            else
            {
                _LEDController.GetLEDStatus(led);
                switch (led)
                {
                    case LEDTypes.Green:
                        return _LEDController.GLEDStatus;
                    case LEDTypes.Red:
                        return _LEDController.RLEDStatus;
                    case LEDTypes.White:
                        return _LEDController.WLEDStatus;
                    default:
                        return false;
                }
            }
        }
        private int GetPDValue()
        {
            if (!_IsMachineRev2)
            {
                _Mainboard.GetPDValue();
                return (int)_Mainboard.PDValue;
            }
            else
            {
                _LEDController.GetPDSampledValue();
                return (int)_LEDController.PDSampleValue;
            }
        }
    }
}
