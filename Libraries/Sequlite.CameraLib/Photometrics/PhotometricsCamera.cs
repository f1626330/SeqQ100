using Photometrics.Pvcam;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging; //WriteableBitmap

namespace Sequlite.CameraLib
{
    public class PhotometricsCamera : ICamera
    {
        //public override delegate void CameraNotifHandler(Object sender);
        public event CameraNotifHandler CameraNotif;
        public event ExposureChangedHandle OnExposureChanged;
        #region Private data...

        private CameraMake _CameraType = CameraMake.Photometrics;
        //private static PhotometricsCamera _Instance = null;
        private PVCamCamera _PVCamCamera = null;
        //private bool _IsForceShutterOpen = false;

        private int _RoiStartX = 0;
        private int _RoiStartY = 0;
        private int _RoiWidth = 0;
        private int _RoiHeight = 0;
        private bool _IsAbortCapture;
        private bool _IsForceShutterOpen;
        private double _CCDCoolerSetPoint;
        private double _CurrCcdTemperature;
        private int _ReadoutSpeed;
        private int _Gain;

        private bool _IsDynamicDarkCorrection = false;
        private StringBuilder _Cameralog = new StringBuilder();
        private string _FolderStr;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("PVCamera");
        #endregion
        public string Cameralog
        {
            get { return _Cameralog.ToString(); }
        }
        #region Constructors...

        public PhotometricsCamera()
        {
            _PVCamCamera = new PVCamCamera();
            Logger.Log("Create a PVCamera Object");
            //_PVCamCamera.CamNotif += new PVCamCamera.CameraNotificationsHandler(_PVCamCamera_CamNotif);
            _PVCamCamera.ReportMsg += PVCamCamera_RportMsg;
            _PVCamCamera.StartExposure += PVCamCamera_StartExposure;
            StringBuilder folderBuilder = new StringBuilder();
            folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            folderBuilder.Append("\\Sequlite\\ALF\\Cameralogs\\");
            _FolderStr = folderBuilder.ToString();
            Directory.CreateDirectory(_FolderStr);
            _FolderStr = _FolderStr + DateTime.Now.ToString("yyyyMMddHHmmss");
            FileStream fs = new FileStream(_FolderStr + " CameraLogs.txt", FileMode.Create, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(string.Format("Starting time: {0}", DateTime.Now));
            sw.Flush();
            sw.Close();
            fs.Close();

        }

        public PhotometricsCamera(double ccdCoolerSetPoint)
        {
            _PVCamCamera = new PVCamCamera();
            _CCDCoolerSetPoint = ccdCoolerSetPoint;
        }

        #endregion

        #region Public properties...

        public  CameraMake CameraType
        {
            get { return _CameraType; }
        }

        public List<ReadoutOption> ReadoutOption
        {
            get { return _PVCamCamera.SpeedTable.ReadoutOption; }
        }

        public List<PP_Feature> PP_FeatureList
        {
            get { return _PVCamCamera.PP_FeatureList; }
        }

        public int RoiStartX
        {
            get
            {
                //return _PVCamCamera.Region[0].s1;
                return _RoiStartX;
            }
            set
            {
                //if (_PVCamCamera.Region[0].s1 != value)
                //{
                //    _PVCamCamera.Region[0].s1 = (ushort)value;
                //}
                _RoiStartX = value;
            }
        }

        public int RoiStartY
        {
            get
            {
                //return _PVCamCamera.Region[0].p1;
                return _RoiStartY;
            }
            set
            {
                //if (_PVCamCamera.Region[0].p1 != value)
                //{
                //    _PVCamCamera.Region[0].p1 = (ushort)value;
                //}
                _RoiStartY = value;
            }
        }

        public int RoiWidth
        {
            get
            {
                //return _PVCamCamera.Region[0].s2;
                return _RoiWidth;
            }
            set
            {
                //if (_PVCamCamera.Region[0].s2 != value)
                //{
                //    //_PVCamCamera.Region[0].s2 = (ushort)make_even(value);
                //    _PVCamCamera.Region[0].s2 = (ushort)value;
                //}
                //if (_PVCamCamera.Region[0].s2 != _PVCamCamera.XSize - 1)
                //{
                //    _PVCamCamera.Region[0].s2 = (ushort)(_PVCamCamera.XSize - 1);
                //}
                _RoiWidth = value;
            }
        }

        public int RoiHeight
        {
            get
            {
                //return _PVCamCamera.Region[0].p2;
                return _RoiHeight;
            }
            set
            {
                //if (_PVCamCamera.Region[0].p2 != value)
                //{
                //    //_PVCamCamera.Region[0].p2 = (ushort)make_even(value);
                //    _PVCamCamera.Region[0].p2 = (ushort)value;
                //}
                //if (_PVCamCamera.Region[0].p2 != _PVCamCamera.YSize - 1)
                //{
                //    //_PVCamCamera.Region[0].p2 = (ushort)make_even(value);
                //    _PVCamCamera.Region[0].p2 = (ushort)(_PVCamCamera.YSize - 1);
                //}
                _RoiHeight = value;
            }
        }

        public int ImagingColumns
        {
            get { return _PVCamCamera.XSize; }
        }

        public int ImagingRows
        {
            get { return _PVCamCamera.YSize; }
        }

        /// <summary>
        /// Horizontal binning
        /// </summary>
        public int HBin
        {
            get
            {
                return _PVCamCamera.Region[0].pbin;
            }
            set
            {
                if (_PVCamCamera != null && _PVCamCamera.Region != null)
                    _PVCamCamera.Region[0].pbin = (ushort)value;
            }
        }

        /// <summary>
        /// Vertical binning
        /// </summary>
        public int VBin
        {
            get
            {
                return _PVCamCamera.Region[0].sbin;
            }
            set
            {
                if (_PVCamCamera != null && _PVCamCamera.Region != null)
                    _PVCamCamera.Region[0].sbin = (ushort)value;
            }
        }

        public double MinExposure
        {
            get
            {
                double minExposure = (double)_PVCamCamera.MinExposureTime / 1000.0;
                return minExposure;
            }
        }

        public double MaxExposure
        {
            get
            {
                double maxExposure = (double)_PVCamCamera.MaxExposureTime / 1000.0;
                return maxExposure;
            }
        }

        public double CCDCoolerSetPoint
        {
            get
            {
                return _PVCamCamera.CurrentSetpoint / 100.0;
            }
            set
            {
                _CCDCoolerSetPoint = value;
                Int16 newSetpoint = Convert.ToInt16(_CCDCoolerSetPoint * 100.0);
                if (_PVCamCamera.CurrentSetpoint != newSetpoint)
                {
                    if (newSetpoint > _PVCamCamera.MaxSetpoint || newSetpoint < _PVCamCamera.MinSetpoint)
                    {
                        _CCDCoolerSetPoint = -20.0;   //Out of range: default to -20
                        newSetpoint = Convert.ToInt16(_CCDCoolerSetPoint * 100.0);
                    }
                    _PVCamCamera.SetTemperatureSetpoint(newSetpoint);
                }
            }
        }

        public double CCDTemperature
        {
            get
            {
                //previous read ccd temperature
                _CurrCcdTemperature = _PVCamCamera.CurrentTemperature / 100.0;

                if (!_PVCamCamera.IsAcqRunning)
                {
                    if (_PVCamCamera.GetCurrentTemprature())
                    {
                        //current ccd temperature
                        _CurrCcdTemperature = _PVCamCamera.CurrentTemperature / 100.0;
                    }
                }
                return _CurrCcdTemperature;
            }
        }

        public string FirmwareVersion
        {
            get
            {
                string fwVersion = string.Empty;
                if (_PVCamCamera != null)
                {
                    fwVersion = _PVCamCamera.PvCamFirmwareVersion;
                }
                return fwVersion;
            }
        }

        //public override string CameraModel
        //{
        //    get { return _AltaCamera.CameraModel; }
        //}

        //public override bool IsExposing
        //{
        //    get
        //    {
        //        return _IsExposing;
        //    }
        //    set
        //    {
        //        if (_IsExposing != value)
        //        {
        //            _IsExposing = value;
        //        }
        //    }
        //}

        /// <summary>
        /// Opens/close the camera shutter
        /// TRUE forces shutter to open;
        /// FALSE allows normal shutter operation
        /// </summary>
        public bool ForceShutterOpen
        {
            get
            {
                return _IsForceShutterOpen;
            }
            set
            {
                _IsForceShutterOpen = value;
                //Current Photometrics camera doesn't have a shutter
                //if (_IsForceShutterOpen)
                //{
                //    _PVCamCamera.SetShutterMode(Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_NO_CHANGE);
                //}
                //else
                //{
                //    _PVCamCamera.SetShutterMode(Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_PRE_EXPOSURE);
                //}
            }
        }

        public int Gain
        {
            get { return _Gain; }
            set
            {
                _Gain = value;
                _PVCamCamera.SetGainState((Int16)_Gain);
            }
        }

        /// <summary>
        /// Returns/sets the camera’s acquisition speed (Photometrics 1 : Normal, 0 : Fast)
        /// </summary>
        public int ReadoutSpeed
        {
            get { return _ReadoutSpeed; }
            set
            {
                //Photometrics camera: 0 = Fast, 1 = Normal
                //Apogee camera: 0 = Normal, 1 = Fast
                _ReadoutSpeed = value;
                int roSpeed = (value == 0) ? 1 : 0;
                _PVCamCamera.SetReadoutSpeed((Int16)roSpeed);
            }
        }

        public bool IsAbortCapture
        {
            get { return _IsAbortCapture; }
            set { _IsAbortCapture = value; }
        }

        public bool IsAcqRunning
        {
            get { return _PVCamCamera.IsAcqRunning; }
            //set { _PVCamCamera.IsAcqRunning = value; }
        }
        public StringBuilder CameraMessage = new StringBuilder();
        /*public override bool IsDynamicDarkCorrection
        {
            get { return _IsDynamicDarkCorrection; }
            set
            {
                _IsDynamicDarkCorrection = value;

                //Dark Frame Correction : feat=1, funct=0, corrEnabled=1 (enable)
                Int32 feat = 1;         //dark frame correction
                Int32 funct = 0;
                Int32 corrEnabled = 0;  //function value
                corrEnabled = (_IsDynamicDarkCorrection) ? 1 : 0;  // 1 = enable, 0 = disable
                _PVCamCamera.WritePostProcessingFeature(feat, funct, corrEnabled);
            }
        }*/
        public int ADCBitDepth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int PixelFormatBitDepth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Channels { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        #endregion

        #region Public methods...

        //public static PhotometricsCamera CreateInstance()
        //{
        //    if (_Instance == null)
        //    {
        //        _Instance = new PhotometricsCamera();
        //    }
        //    return _Instance;
        //}

        public bool Open()
        {
            bool bResult = false;
            try
            {
                PVCamCamera.RefreshCameras(_PVCamCamera);
                if (_PVCamCamera != null && PVCamCamera.CameraList.Count > 0)
                {
                    if (PVCamCamera.OpenCamera(PVCamCamera.CameraList[0], _PVCamCamera))
                    {
                        bResult = true;
                    }
                }
                else
                {
                    bResult = false;
                }
            }
            catch
            {
                bResult = false;
            }
            IsConnected = bResult;
            return bResult;
        }

        public bool IsConnected { get; private set; }
        public void Close()
        {
            if (_PVCamCamera != null && IsConnected)
            {
                _PVCamCamera.StopAcquisition();
                _PVCamCamera.WaitForFullAcquisitionStop();
                _PVCamCamera.CloseCamera();
                IsConnected = false;
            }
        }

        public void GrabImage(double exposureTime, CaptureFrameType frameType, ref WriteableBitmap capturedImage)
        {
            #region === Camera setup ===
            Logger.Log("Start SetExposureTime");
            // Exposure time:
            _PVCamCamera.SetExposureTime((uint)(exposureTime * 1000.0));  //seconds to milliseconds

            // ROI setup
            int iStartX = _RoiStartX * _PVCamCamera.Region[0].pbin;
            int iStartY = _RoiStartY * _PVCamCamera.Region[0].sbin;
            int iWidth = _RoiWidth * _PVCamCamera.Region[0].pbin;
            int iHeight = _RoiHeight * _PVCamCamera.Region[0].sbin;
            if ((iWidth + iStartX) > _PVCamCamera.XSize - 1)
            {
                iWidth = iWidth - ((iWidth + iStartX) - (_PVCamCamera.XSize - 1));
            }
            if ((iHeight + iStartY) > _PVCamCamera.YSize - 1)
            {
                iHeight = iHeight - ((iHeight + iStartY) - (_PVCamCamera.YSize - 1));
            }

            _PVCamCamera.Region[0].s1 = (ushort)iStartX;
            _PVCamCamera.Region[0].s2 = (ushort)(iWidth + iStartX);
            _PVCamCamera.Region[0].p1 = (ushort)iStartY;
            _PVCamCamera.Region[0].p2 = (ushort)(iHeight + iStartY);
            _PVCamCamera.SetMultiROI(new ushort[] { _PVCamCamera.Region[0].s1 },
                                    new ushort[] { _PVCamCamera.Region[0].s2 },
                                    new ushort[] { _PVCamCamera.Region[0].p1 },
                                    new ushort[] { _PVCamCamera.Region[0].p2 });

            // Set shutter mode
            //Photometrics.Pvcam.PvTypes.ShutterModes shutterMode;
            //shutterMode = (frameType == CaptureFrameType.Dark) ? Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_NEVER : Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_PRE_EXPOSURE;
            //_PVCamCamera.SetShutterMode(shutterMode);

            #endregion

            _IsAbortCapture = false;
            Logger.Log("Start AcqSetup");
            // Start exposure
            if (!_PVCamCamera.AcqSetup(PVCamCamera.AcqTypes.ACQ_TYPE_SINGLE))
            {
                throw new Exception("Image acquisition setup error: AcqSetup(PVCamCamera.ACQ_TYPE_SINGLE)");
            }

            // trigger LED turn on
            Logger.Log("Start SyncSeqAcq");
            // Grab image from camera
            if (!_PVCamCamera.StartSyncSeqAcq())
            {
                throw new Exception("Image acquisition error: StartSyncSeqAcq()");
            }

            if (!_IsAbortCapture)
            {
                int width = (_PVCamCamera.Region[0].s2 - _PVCamCamera.Region[0].s1 + 1) / _PVCamCamera.Region[0].sbin;
                int height = (_PVCamCamera.Region[0].p2 - _PVCamCamera.Region[0].p1 + 1) / _PVCamCamera.Region[0].pbin;
                //int width = _PVCamCamera.XSize / _PVCamCamera.Region[0].sbin;
                //int height = _PVCamCamera.YSize / _PVCamCamera.Region[0].pbin;

                _PVCamCamera.FrameToBitmap(_PVCamCamera.FrameDataShorts, width, height);

                capturedImage = _PVCamCamera.LastBMP;

                //
                // The way the camera is mounted, we have to rotate the image 180 degree
                //
                //if (_PVCamCamera.LastBMP == null)
                //    capturedImage = null;
                //else
                //{
                //    TransformedBitmap tb = new TransformedBitmap();
                //    tb.BeginInit();
                //    tb.Source = _PVCamCamera.LastBMP;
                //    System.Windows.Media.RotateTransform transform = new System.Windows.Media.RotateTransform(180);
                //    tb.Transform = transform;
                //    tb.EndInit();
                //    capturedImage = new WriteableBitmap((BitmapSource)tb);
                //}
            }
        }

        public void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage,
                                       int lightType)
        {
            #region === Camera setup ===

            // Exposure time:
            _PVCamCamera.SetExposureTime((UInt32)(exposureTime * 1000.0));  //seconds to milliseconds

            // ROI setup
            int iStartX = _RoiStartX * _PVCamCamera.Region[0].pbin;
            int iStartY = _RoiStartY * _PVCamCamera.Region[0].sbin;
            int iWidth = _RoiWidth * _PVCamCamera.Region[0].pbin;
            int iHeight = _RoiHeight * _PVCamCamera.Region[0].sbin;
            if ((iWidth + iStartX) > _PVCamCamera.XSize - 1)
            {
                iWidth = iWidth - ((iWidth + iStartX) - (_PVCamCamera.XSize - 1));
            }
            if ((iHeight + iStartY) > _PVCamCamera.YSize - 1)
            {
                iHeight = iHeight - ((iHeight + iStartY) - (_PVCamCamera.YSize - 1));
            }

            _PVCamCamera.Region[0].s1 = (ushort)iStartX;
            _PVCamCamera.Region[0].s2 = (ushort)(iWidth + iStartX);
            _PVCamCamera.Region[0].p1 = (ushort)iStartY;
            _PVCamCamera.Region[0].p2 = (ushort)(iHeight + iStartY);

            // Set shutter mode
            //Photometrics.Pvcam.PvTypes.ShutterModes shutterMode;
            //shutterMode = (frameType == CaptureFrameType.Dark) ? Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_NEVER : Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_PRE_EXPOSURE;
            //_PVCamCamera.SetShutterMode(shutterMode);

            #endregion

            _IsAbortCapture = false;
            Logger.Log("Start SyncSeq");
            // Start exposure
            if (!_PVCamCamera.AcqSetup(PVCamCamera.AcqTypes.ACQ_TYPE_SINGLE))
            {
                Logger.LogError("Image acquisition setup error: AcqSetup(PVCamCamera.ACQ_TYPE_SINGLE)");
                throw new Exception("Image acquisition setup error: AcqSetup(PVCamCamera.ACQ_TYPE_SINGLE)");
            }
            // Grab image from camera
            if (!_PVCamCamera.StartSyncSeqAcq())
            {
                Logger.LogError("Image acquisition error: StartSyncSeqAcq()");
                throw new Exception("Image acquisition error: StartSyncSeqAcq()");
            }

            if (!_IsAbortCapture)
            {
                //OnExposureChanged?.Invoke(false);

                int width = (_PVCamCamera.Region[0].s2 - _PVCamCamera.Region[0].s1 + 1) / _PVCamCamera.Region[0].sbin;
                int height = (_PVCamCamera.Region[0].p2 - _PVCamCamera.Region[0].p1 + 1) / _PVCamCamera.Region[0].pbin;
                //int width = _PVCamCamera.XSize / _PVCamCamera.Region[0].sbin;
                //int height = _PVCamCamera.YSize / _PVCamCamera.Region[0].pbin;

                _PVCamCamera.FrameToBitmap(_PVCamCamera.FrameDataShorts, width, height);

                //capturedImage = _PVCamCamera.LastBMP;

                //
                // The way the camera is mounted, we have to rotate the image 180 degree
                //
                if (_PVCamCamera.LastBMP == null)
                    capturedImage = null;
                else
                {
                    TransformedBitmap tb = new TransformedBitmap();
                    tb.BeginInit();
                    tb.Source = _PVCamCamera.LastBMP;
                    System.Windows.Media.RotateTransform transform = new System.Windows.Media.RotateTransform(180);
                    tb.Transform = transform;
                    tb.EndInit();
                    capturedImage = new WriteableBitmap((BitmapSource)tb);
                }
            }
        }

        /// <summary>
        /// Focus calibration image grab.
        /// [See the other GrabImage method for normal image grab]
        /// </summary>
        /// <param name="capturedImage"></param>
        /// <param name="dExposureTime"></param>
        /// <param name="iLightDelay"></param>
        /// <param name="mvController"></param>
        /*public override void GrabImage(ref WriteableBitmap capturedImage, double exposureTime, int lightingDelayTime, SerialControl controller, LightCode lightSource)
        {
            #region === Camera setup ===

            // ROI setup
            int iStartX = _RoiStartX * _PVCamCamera.Region[0].pbin;
            int iStartY = _RoiStartY * _PVCamCamera.Region[0].sbin;
            int iWidth = _RoiWidth * _PVCamCamera.Region[0].pbin;
            int iHeight = _RoiHeight * _PVCamCamera.Region[0].sbin;
            if ((iWidth + iStartX) > _PVCamCamera.XSize - 1)
            {
                iWidth = iWidth - ((iWidth + iStartX) - (_PVCamCamera.XSize - 1));
            }
            if ((iHeight + iStartY) > _PVCamCamera.YSize - 1)
            {
                iHeight = iHeight - ((iHeight + iStartY) - (_PVCamCamera.YSize - 1));
            }

            _PVCamCamera.Region[0].s1 = (ushort)iStartX;
            _PVCamCamera.Region[0].s2 = (ushort)(iWidth + iStartX);
            _PVCamCamera.Region[0].p1 = (ushort)iStartY;
            _PVCamCamera.Region[0].p2 = (ushort)(iHeight + iStartY);

            //Set shutter mode
            //Photometrics.Pvcam.PvTypes.ShutterModes shutterMode = Photometrics.Pvcam.PvTypes.ShutterModes.OPEN_PRE_EXPOSURE;
            //_PVCamCamera.SetShutterMode(shutterMode);

            #endregion

            _IsAbortCapture = false;

            //capturedImage = new WriteableBitmap(_AltaCamera.RoiPixelsH, _AltaCamera.RoiPixelsV, 96, 96, PixelFormats.Gray16, null);

            controller.SetLightOn(lightSource);
            //mvController.SetLightContinueOn((uint)lightSource, (uint)(lightingDelayTime));

            // exposure time is actually the led on time
            //_PVCamCamera.Expose(dExposureTime + 0.5, true);
            _PVCamCamera.SetExposureTime((uint)(exposureTime * 1000.0));
            if (!_PVCamCamera.AcqSetup(PVCamCamera.AcqTypes.ACQ_TYPE_SINGLE))
            {
                throw new Exception("Image acquisition setup error: AcqSetup(PVCamCamera.ACQ_TYPE_SINGLE)");
            }

            // wait for the shutter to open - Photometrics has no shutter
            //System.Threading.Thread.Sleep(200);

            // turn white light on (time in milliseconds)
            //mvController.SetLightContinueOn((uint)LightCode.White, (uint)(lightingDelayTime));

            // Apogee is working on the fix: the camera sometimes report ImageReady prematurely.
            // So, we're sleeping the duration of the exposure time before checking the camera status
            //System.Threading.Thread.Sleep((int)(dExposureTime + 0.5));
            // check camera status to make sure image data is ready
            //while (!IsAbortCapture && _AltaCamera.ImagingStatus != Apn_Status.Apn_Status_ImageReady) ;

            // Grab the image
            //_AltaCamera.GetImage((int)capturedImage.BackBuffer);

            //Grab image from camera
            if (!_PVCamCamera.StartSyncSeqAcq())
            {
                throw new Exception("Image acquisition error: StartSyncSeqAcq()");
            }

            // Turn the light off
            controller.SetLightOff(lightSource);

            if (!_IsAbortCapture)
            {
                int width = (_PVCamCamera.Region[0].s2 - _PVCamCamera.Region[0].s1 + 1) / _PVCamCamera.Region[0].sbin;
                int height = (_PVCamCamera.Region[0].p2 - _PVCamCamera.Region[0].p1 + 1) / _PVCamCamera.Region[0].pbin;

                _PVCamCamera.FrameToBitmap(_PVCamCamera.FrameDataShorts, width, height);

                capturedImage = _PVCamCamera.LastBMP;
            }
        }*/

        public void StopCapture()
        {
            if (_PVCamCamera != null)
            {
                try
                {
                    _IsAbortCapture = true;
                    _PVCamCamera.StopAcquisition();
                    _PVCamCamera.CamNotif -= new PVCamCamera.CameraNotificationsHandler(_PVCamCamera_CamNotif);
                }
                catch
                {
                }
            }
        }

        public void StartContinuousMode(double exposureTime)
        {
            if (_PVCamCamera == null || _PVCamCamera.IsAcqRunning)
            {
                return;
            }

            #region === Camera setup ===

            //Set number of frames to get in circular buffer (continuous) mode
            _PVCamCamera.FramesToGet = PVCamCamera.RUN_UNTIL_STOPPED;
            //_PVCamCamera.FramesToGet = 1;
            //exposure time:
            _PVCamCamera.SetExposureTime((uint)(exposureTime * 1000.0));  //seconds to milliseconds

            // ROI setup
            int iStartX = _RoiStartX * _PVCamCamera.Region[0].pbin;
            int iStartY = _RoiStartY * _PVCamCamera.Region[0].sbin;
            int iWidth = _RoiWidth * _PVCamCamera.Region[0].pbin;
            int iHeight = _RoiHeight * _PVCamCamera.Region[0].sbin;
            if ((iWidth + iStartX) > _PVCamCamera.XSize - 1)
            {
                iWidth = iWidth - ((iWidth + iStartX) - (_PVCamCamera.XSize - 1));
            }
            if ((iHeight + iStartY) > _PVCamCamera.YSize - 1)
            {
                iHeight = iHeight - ((iHeight + iStartY) - (_PVCamCamera.YSize - 1));
            }

            _PVCamCamera.Region[0].s1 = (ushort)iStartX;
            _PVCamCamera.Region[0].s2 = (ushort)(iWidth + iStartX);
            _PVCamCamera.Region[0].p1 = (ushort)iStartY;
            _PVCamCamera.Region[0].p2 = (ushort)(iHeight + iStartY);
            _PVCamCamera.SetMultiROI(new ushort[] { _PVCamCamera.Region[0].s1 },
                                    new ushort[] { _PVCamCamera.Region[0].s2 },
                                    new ushort[] { _PVCamCamera.Region[0].p1 },
                                    new ushort[] { _PVCamCamera.Region[0].p2 });
            #endregion

            _PVCamCamera.CamNotif += new PVCamCamera.CameraNotificationsHandler(_PVCamCamera_CamNotif);
            Logger.Log("Start ContinuousMode");
            if (!_PVCamCamera.AcqSetup(PVCamCamera.AcqTypes.ACQ_TYPE_CONTINUOUS))
            {
                return;
            }

            //if acqusition setup succeeded, start the acquisition
            if (!_PVCamCamera.StartContinuousAcquisition())
            {
                return;
            }
        }

        private void PVCamCamera_RportMsg(PVCamCamera pvcc, ReportMessage rm)
        {
            if(rm.Type == 1){Logger.LogError(rm.Message, SeqLogFlagEnum.DEBUG);}
            else { Logger.Log(rm.Message, SeqLogFlagEnum.DEBUG); }
            lock (this)
            {
                _Cameralog.Append(string.Format("{0}: {1}, Error:{2} \r\n", DateTime.Now.ToString("HH:mm:ss"), rm.Message, rm.Type));
                if (rm.Message == "Closing camera..." || rm.Type == 1)
                {
                    FileStream fs = new FileStream(_FolderStr + " CameraLogs.txt", FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(_Cameralog);
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                    _Cameralog.Clear();
                }
            }
        }

        private void PVCamCamera_StartExposure()
        {
            Logger.Log("Turn On LED");
            OnExposureChanged?.Invoke(true);
        }
        private void _PVCamCamera_CamNotif(PVCamCamera pvcc, ReportEvent e)
        {
            if (e.NotifEvent == CameraNotifications.ACQ_NEW_FRAME_RECEIVED)
            {
                try
                {
                    _PVCamCamera.FrameToBitmap(_PVCamCamera.FrameDataShorts,
                                               (_PVCamCamera.Region[0].s2 - _PVCamCamera.Region[0].s1 + 1) / _PVCamCamera.Region[0].sbin,
                                               (_PVCamCamera.Region[0].p2 - _PVCamCamera.Region[0].p1 + 1) / _PVCamCamera.Region[0].pbin);

                    WriteableBitmap wbBitmapFrame = _PVCamCamera.LastBMP;

                    _PVCamCamera.FrameNumber++;

                    if (wbBitmapFrame != null)
                    {
                        if (wbBitmapFrame.CanFreeze)
                        {
                            wbBitmapFrame.Freeze();
                        }
                    }

                    if (CameraNotif != null)
                    {
                        CameraNotif(wbBitmapFrame);
                    }
                }
                catch (Exception ex)
                {
                    //_PVCamCamera.CamNotif -= new PVCamCamera.CameraNotificationsHandler(_PVCamCamera_CamNotif);
                    Logger.LogError(ex.ToString());
                    StopCapture();
                    throw new Exception("ERROR: Live mode error.", ex);
                }
                finally
                {
                    // Forces a garbage collection
                    //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    //GC.WaitForPendingFinalizers();
                    //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    // Force garbage collection.
                    //GC.Collect();
                    // Wait for all finalizers to complete before continuing.
                    //GC.WaitForPendingFinalizers();
                }
            }
            else if (e.NotifEvent == CameraNotifications.ACQ_CONT_FINISHED)
            {
                StopCapture();
            }
        }

        public void StopAcquisition()
        {
            if (_PVCamCamera != null)
            {
                _PVCamCamera.StopAcquisition();
            }
        }

        //public void ReadPostProcessingFeatures()
        //{
        //    if (_PVCamCamera != null)
        //        _PVCamCamera.ReadPostProcessingFeatures();
        //}

        //public List<PP_Feature> PostProcessingFeatures
        //{
        //    get
        //    {
        //        return _PVCamCamera.PP_FeatureList;
        //    }
        //}

        private bool _IsDefectivePixelCorrection = false;
        public bool IsDefectivePixelCorrection
        {
            get { return _IsDefectivePixelCorrection; }
            set
            {
                _IsDefectivePixelCorrection = value;
                PvTypes.PP_FEATURE_IDS targetfeatID = PvTypes.PP_FEATURE_IDS.PP_FEATURE_DEFECTIVE_PIXEL_CORRECTION;
                PvTypes.PP_PARAMETER_IDS targetfuncID = PvTypes.PP_PARAMETER_IDS.PP_FEATURE_DEFECTIVE_PIXEL_CORRECTION_ENABLED;
                _PVCamCamera.ConfigPostProcessingID(targetfeatID, targetfuncID, _IsDefectivePixelCorrection);
            }
        }

        public bool IsDynamicDarkCorrection
        {
            get { return _IsDynamicDarkCorrection; }
            set
            {
                _IsDynamicDarkCorrection = value;
                PvTypes.PP_FEATURE_IDS targetfeatID = PvTypes.PP_FEATURE_IDS.PP_FEATURE_DYNAMIC_DARK_FRAME_CORRECTION;
                PvTypes.PP_PARAMETER_IDS targetfuncID = PvTypes.PP_PARAMETER_IDS.PP_FEATURE_DYNAMIC_DARK_FRAME_CORRECTION_ENABLED;
                _PVCamCamera.ConfigPostProcessingID(targetfeatID, targetfuncID, _IsDynamicDarkCorrection);
            }
        }

        private bool _IsEnhancedDynamicRange = false;
        public bool IsEnhancedDynamicRange
        {
            get { return _IsEnhancedDynamicRange; }
            set
            {
                _IsEnhancedDynamicRange = value;
                PvTypes.PP_FEATURE_IDS targetfeatID = PvTypes.PP_FEATURE_IDS.PP_FEATURE_ENHANCED_DYNAMIC_RANGE;
                PvTypes.PP_PARAMETER_IDS targetfuncID = PvTypes.PP_PARAMETER_IDS.PP_FEATURE_ENHANCED_DYNAMIC_RANGE_ENABLED;
                _PVCamCamera.ConfigPostProcessingID(targetfeatID, targetfuncID, _IsEnhancedDynamicRange);
            }
        }

        #endregion

        private int make_even(int n)
        {
            return n - (n % 2);
        }
    }
}
