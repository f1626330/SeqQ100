using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sequlite.ALF.Imaging
{
    public abstract class AutoFocusCommandBase : ThreadBase
    {
        public delegate void UpdateInfoHandler(double zPos, double sharpness);
        public event UpdateInfoHandler OnImageSampled;

        public AutoSetQueue<Tuple<string, WriteableBitmap, Image.Processing.ImageInfo>> scanedImageQ { get; private set; }
        #region protected fields

        protected double ScanInterval; //Zpos step size for scanning
        protected bool IsHCOnly; //Cancel scan through, HillClimb only if focus is stable, to save time
        protected bool IsRecipe; //If camera was already initialized outside of AF thread, isrecipe is true. Not in use.

        protected MotionController _Motion;
        protected ICamera _Camera;
        protected Mainboard _MainBoard;
        protected AutoFocusSettings _Settings;
        protected LEDController _LEDController;

        //Varibles for Hillclimb
        protected double _LeftSharpness;
        protected double _CurrentSharpness;
        protected double _RightSharpness;
        protected bool _IsLeftClimbing = true;
        protected bool _IsFirstSample = true;

        protected bool _IsAutoFocusCompleted;
        //protected WriteableBitmap _Image;
        protected double _FocusedSharpness;
        protected int _TryCounts = 0;
        protected bool _IsFailedCaptureImage;
        protected bool _IsBadImage = false;
        protected bool _ledStateGet = false;
        //private int _PDValue;
        //private int _LEDFailure;
        protected double sharpness = 0;
        protected string _FolderStr;
        protected Image.Processing.ImageInfo Info = new Image.Processing.ImageInfo();
        protected ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Autofocus Thread");
        protected bool _IsAbort;
        protected WriteableBitmap _ScanImage = null;
        protected static int InstanceNumber = 0;
        protected static int GetNextInstanceNumber()
        {
            return Interlocked.Increment(ref InstanceNumber);
        }
        protected bool _GotNewImage = false;
        protected Task _ContinuousTask;
        protected bool _SimCameraContinuousModeRunning;
        protected string _Imagefiledir;
        protected bool _IsUsingTiltFiducial = false;
        #endregion protected fields

        #region public properties
        public int Filterfail { get; set; } //ALF1.0
        public bool IsFailedToSetLED { get; set; }
        public bool IsScanOnly { get; protected set; } //only scan through, no HillClimb, used in "Scan" botton at AF Tab
        public bool IsFailedCaptureImage
        {
            get { return _IsFailedCaptureImage; }
        }

        public double FoucsedSharpness
        {
            get { return _FocusedSharpness; }

        }
        public string ExceptionMessage { get; set; }
        public double Offset { get; protected set; } //Fluo af
        public int TryCounts
        {
            get { return _TryCounts; }
        }
        #endregion public properties

        protected void OnImageSampledInvoke(double zPos, double sharpness) //Pass info to AF tab
        {
            Logger.Log($"Z:{zPos}, Sharpness:{sharpness}");
            OnImageSampled?.Invoke(zPos, sharpness);
        }
        public override void ThreadFunction()
        {
            #region AF image saving directory
            StringBuilder folderBuilder = new StringBuilder();
            folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
            folderBuilder.Append($"\\Sequlite\\ALF\\{this.GetType().Name}\\");
            folderBuilder.Append(DateTime.Now.ToString("yyyyMMdd"));
            folderBuilder.Append("\\");
            folderBuilder.Append(DateTime.Now.ToString("HHmmss"));
            folderBuilder.Append(string.Format("_X{0:00.00}mm_Y{1:00.00}mm\\", Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2), Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2)));
            _FolderStr = folderBuilder.ToString();
            #endregion AF image saving directory
            int threadNumber = GetNextInstanceNumber();
            Logger.Log($"Begin AutoFocusCommand thread function: {threadNumber}");
            Info.MixChannel.YPosition = Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);
            Info.MixChannel.Exposure = _Settings.ExposureTime;
            Info.MixChannel.LightIntensity = (int)_Settings.LEDIntensity;
            Info.MixChannel.LightSource = _Settings.LEDType.ToString();
            Info.MixChannel.FilterPosition = _Settings.FilterIndex;
            Info.MixChannel.ROI = _Settings.ROI;
            scanedImageQ = new AutoSetQueue<Tuple<string, WriteableBitmap, Image.Processing.ImageInfo>>();
            ProcessAutoFocus();
            if (ExitStat != ThreadExitStat.None && SettingsManager.ConfigSettings.SystemConfig.EnableSaveAFImage)
            {
                CreateImageFolder();
                while (scanedImageQ.HasItems)
                {
                    var obj = scanedImageQ.Dequeue();
                    if (obj != null && !File.Exists(obj.Item1))
                        ImageProcessing.Save(obj.Item1, obj.Item2, obj.Item3, false);
                }
            }
            scanedImageQ.Clear();
            Logger.Log($"End AutoFocusCommand thread function: {threadNumber}");
        }

        protected void CreateImageFolder()
        {
            if (!Directory.Exists(_FolderStr))
                Directory.CreateDirectory(_FolderStr);
        }

        #region Sharpness calculation and algorigthm
        /// <summary>
        /// Sharpness calculation, depend on the size of ROI(only Rev2), use Horizontal Averaged STDV or Vertical Averaged STDV
        /// Hardware intallation difference
        /// </summary>
        /// <param name="isMachineRev2"></param>
        protected virtual void SharpnessCalculation(bool isMachineRev2)
        {
            Rect rect = new Rect(0, 0, 1, 1);
            if (IsRecipe) { rect = new Rect((double)_Settings.ROI.X / _ScanImage.PixelWidth, (double)_Settings.ROI.Y / _ScanImage.PixelHeight, (double)_Settings.ROI.Width / _ScanImage.PixelWidth, (double)_Settings.ROI.Height / _ScanImage.PixelHeight); }
            if (isMachineRev2 && _Settings.ROI.Width > _Settings.ROI.Height) { sharpness = SharpnessEvaluation.VerticalAveragedStdDev(ref _ScanImage, rect); }
            else { sharpness = SharpnessEvaluation.HorizontalAveragedStdDev(ref _ScanImage, rect); }
            OnImageSampledInvoke(_Motion.ZCurrentPos, sharpness);
            //Logger.Log($"Z:{_Motion.ZCurrentPos}, Sharpness:{sharpness}");
            if (_IsFirstSample)
            {
                _CurrentSharpness = sharpness;
            }
            else if (_IsLeftClimbing)
            {
                _LeftSharpness = sharpness;
            }
            else
            {
                _RightSharpness = sharpness;
            }
        }

        /// <summary>
        /// Hill climb, start with moving to left (lower Z stage position)
        /// compare sharpness of image at two different Zpos, then move left(down) or right(up).
        /// </summary>
        protected virtual void HillClimbingMethod()
        {
            double step = 0.5; //finest Resolution of camera/lens
            if (_IsFirstSample)
            {
                _IsFirstSample = false;
                if (_Motion.ZCurrentPos - step >= _Settings.ZstageLimitL)
                {
                    _IsLeftClimbing = true;
                    RelativeMoveZStage(-step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                }
                else
                {
                    _IsLeftClimbing = false;
                    RelativeMoveZStage(step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                }
            }
            else if (_IsLeftClimbing)
            {
                if (_LeftSharpness < _CurrentSharpness)
                {
                    if (_RightSharpness == 0)
                    {
                        _IsLeftClimbing = false;
                        if (_Motion.ZCurrentPos + 2 * step > _Settings.ZstageLimitH)
                        {
                            _IsAutoFocusCompleted = true;
                            _FocusedSharpness = _CurrentSharpness;
                            ExceptionMessage = "Out of Range" + $"(NextClimbingPos:{_Motion.ZCurrentPos + 2 * step} > ZstageLimitH:{_Settings.ZstageLimitH})";
                            ExitStat = ThreadExitStat.Error;
                            return;
                        }
                        RelativeMoveZStage(2 * step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                    }
                    else if (_RightSharpness < _CurrentSharpness)
                    {
                        _IsAutoFocusCompleted = true;
                        RelativeMoveZStage(step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                        ExitStat = ThreadExitStat.None;
                        _FocusedSharpness = _CurrentSharpness;
                        return;
                    }
                }
                else
                {
                    _RightSharpness = _CurrentSharpness;
                    _CurrentSharpness = _LeftSharpness;
                    if (_Motion.ZCurrentPos - step < _Settings.ZstageLimitL)
                    {
                        _IsAutoFocusCompleted = true;
                        ExitStat = ThreadExitStat.Error;
                        _FocusedSharpness = _CurrentSharpness;
                        ExceptionMessage = "Out of Range" + $"(NextClimbingPos:{_Motion.ZCurrentPos - step} > ZstageLimitL:{_Settings.ZstageLimitL})";
                        return;
                    }
                    RelativeMoveZStage(-step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                }
            }
            else
            {
                if (_RightSharpness < _CurrentSharpness)
                {
                    if (_LeftSharpness == 0)
                    {
                        _IsAutoFocusCompleted = true;
                        ExitStat = ThreadExitStat.Error;
                        _FocusedSharpness = _CurrentSharpness;
                        return;
                    }
                    else if (_LeftSharpness < _CurrentSharpness)
                    {
                        _IsAutoFocusCompleted = true;
                        RelativeMoveZStage(-step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                        ExitStat = ThreadExitStat.None;
                        _FocusedSharpness = _CurrentSharpness;
                        return;
                    }
                }
                else
                {
                    _LeftSharpness = _CurrentSharpness;
                    _CurrentSharpness = _RightSharpness;
                    if (_Motion.ZCurrentPos + step > _Settings.ZstageLimitH)
                    {
                        _IsAutoFocusCompleted = true;
                        ExitStat = ThreadExitStat.Error;
                        _FocusedSharpness = _CurrentSharpness;
                        ExceptionMessage = "Out of Range" + $"(NextClimbingPos:{_Motion.ZCurrentPos + step} > ZstageLimitH:{_Settings.ZstageLimitH})";
                        return;
                    }
                    RelativeMoveZStage(step, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                }
            }
        }
        #endregion Sharpness calculation and algorigthm

        #region Lucid Camera helper function
        public void StartSteamLucidCamera()
        {
            double exposureSec = _Settings.ExposureTime;
            if (IsSimulationMode)
            {
                _ContinuousTask = Task.Factory.StartNew(() =>
                {
                    //bool ContinuousModeStarted = false;
                    Logger.Log("Camera Continuous Task Simulation Starts");
                    Thread.Sleep(100);
                    _SimCameraContinuousModeRunning = true;
                    while (_SimCameraContinuousModeRunning)
                    {
                        if (_IsAbort)
                        {
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    Logger.Log("Camera Continuous Task Simulation Ends");
                });

                while (!_SimCameraContinuousModeRunning)
                {
                    if (_IsAbort)
                    {
                        break;
                    }
                    Thread.Sleep(1);
                }
            }
            else
            {
                SetupLucidCamera();
                _ContinuousTask = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        _Camera.CameraNotif += _Camera_CameraNotif;
                        _Camera.StartContinuousMode(exposureSec);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        try
                        {
                            Logger.LogError("ReOpen Camera");
                            ReOpenCamera();
                            SetupLucidCamera();
                            _Camera.CameraNotif += _Camera_CameraNotif;
                            _Camera.StartContinuousMode(exposureSec);
                        }
                        catch (Exception ex1)
                        {
                            Logger.LogError(ex1.ToString());
                            throw ex1;
                        }
                    }
                });
                while (!((ILucidCamera)_Camera).ContinuousModeStarted)
                {
                    if (_IsAbort)
                    {
                        break;
                    }
                    Thread.Sleep(1);
                }

            }
        }
        public void StopLucidCameraTask()
        {
            ((ILucidCamera)_Camera).CameraNotif -= _Camera_CameraNotif;
            _Camera.StopAcquisition();
            if (_ContinuousTask != null)
            {
                _ContinuousTask.Wait();
                _ContinuousTask.Dispose();
                _ContinuousTask = null;
                Logger.Log(" Camera Continuous Task Ends");
            }

        }
        private void SetupLucidCamera()
        {
            //Set binning mode
            _Camera.HBin = 1;
            _Camera.VBin = 1;
            // Set ADC Bit Depth & Pixel Format
            _Camera.ADCBitDepth = 12;
            _Camera.PixelFormatBitDepth = 16;
            //Set gain
            _Camera.Gain = 1;
            //Set region of interest
            if (_Settings.ROI.Width > 0 && _Settings.ROI.Height > 0)
            {
                _Camera.RoiStartX = _Settings.ROI.X;
                _Camera.RoiStartY = _Settings.ROI.Y;
                _Camera.RoiWidth = _Settings.ROI.Width;
                _Camera.RoiHeight = _Settings.ROI.Height;
                if (_IsUsingTiltFiducial)
                {
                    _Camera.RoiStartY = 0;
                    _Camera.RoiHeight = _Camera.ImagingRows;
                }
            }
            else
            {
                _Camera.RoiStartX = 0;
                _Camera.RoiStartY = 0;
                _Camera.RoiWidth = _Camera.ImagingColumns;
                _Camera.RoiHeight = _Camera.ImagingRows;
            }
            ((ILucidCamera)_Camera).EnableTriggerMode = true;
            ((ILucidCamera)_Camera).IsTriggerFromOutside = true;
        }
        private void ReOpenCamera()
        {
            _Camera.CameraNotif -= _Camera_CameraNotif;
            string serialNumber = ((ILucidCamera)_Camera).SerialNumber;
            LucidCameraManager.ReConnectCamera(serialNumber);
            for (int i = 0; i < LucidCameraManager.GetAllCameras().Count; i++)
            {
                if (LucidCameraManager.GetCamera(i).SerialNumber == serialNumber)
                {
                    _Camera = LucidCameraManager.GetCamera(i);
                    if (serialNumber == _LEDController.G1R3CameraSN.ToString())
                    {
                        LucidCameraManager.GetCamera(i).Channels = "G1/R3";
                    }
                    else if (serialNumber == _LEDController.G2R4CameraSN.ToString())
                    {
                        LucidCameraManager.GetCamera(i).Channels = "G2/R4";
                    }
                }

            }
        }
        public void CaptureImageV2(ref int trycounts, ref bool hasError)
        {
            do
            {
                _GotNewImage = false;

                if (!WaitTriggerArmed())
                {
                    _IsAutoFocusCompleted = true;
                    ExitStat = ThreadExitStat.Error;
                    ExceptionMessage = "Failed to arm trigger";
                    hasError = true;
                    break;
                }
                if (SendCameraTrigger())
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    while (!_GotNewImage)
                    {
                        if (_IsAbort)
                        {
                            break;
                        }
                        Thread.Sleep(1);
                        if (stopwatch.ElapsedMilliseconds > 1000 * 20)
                        {
                            Logger.LogError("Wating expire, Failed to receive image");
                            ExitStat = ThreadExitStat.Error;
                            _IsAutoFocusCompleted = true;
                            _IsFailedCaptureImage = true;
                            stopwatch.Stop();
                            hasError = true;
                            break;
                        }
                    }
                    //Check bad image                    
                    _IsBadImage = BadImage(_ScanImage);
                    if (_ScanImage != null)
                    {
                        _Imagefiledir = _FolderStr + "\\" + string.Format("{0}{1}_Z{2:F2}_X{3:00.00}_Y{4:00.00}.tif",
                                            _Settings.LEDType.ToString().Substring(0, 1),
                                            _Settings.OffsetFilterIndex,
                                            _Motion.ZCurrentPos,
                                            Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2),
                                            Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2));   
                        scanedImageQ.Enqueue(Tuple.Create(_Imagefiledir, _ScanImage.Clone(), Info.Clone() as Image.Processing.ImageInfo));
                    }

                    if (_IsBadImage)
                    {
                        trycounts += 1;
                        #region Restart Camera after camera failed multiple times
                        if (trycounts == 2 || trycounts == 5)
                        {
                            if (IsSimulationMode)
                            {
                                Thread.Sleep(1500);
                            }
                            else
                            {
                                StopLucidCameraTask();
                                Thread.Sleep(100);
                                StartSteamLucidCamera();
                            }

                        }
                        #endregion Restart Camera
                        if (trycounts > 10)
                        {
                            ExitStat = ThreadExitStat.Error;
                            _IsAutoFocusCompleted = true;
                            _IsFailedCaptureImage = true;
                            hasError = true;
                            break;
                        }
                    }
                }
                else
                {
                    hasError = true;
                    break;
                }

            }
            while (_IsBadImage); //do loop
        }
        /// <summary>
        /// event receiving data from camera continous mode thread
        /// </summary>
        /// <param name="sender"></param>
        public void _Camera_CameraNotif(object sender)
        {
            byte[] _ImageDataArray;
            int RoiWidth;
            int RoiHeight;
            int PixelFormatBitDepth;
            if (IsSimulationMode)
            {
                RoiWidth = _Settings.ROI.Width;
                RoiHeight = _Settings.ROI.Height;
                PixelFormatBitDepth = 16;
                _ImageDataArray = (sender as byte[]);
            }
            else
            {
                RoiWidth = _Camera.RoiWidth;
                RoiHeight = _Camera.RoiHeight;
                PixelFormatBitDepth = _Camera.PixelFormatBitDepth;
                if (_Camera is ILucidCamera)
                {
                    _ImageDataArray = (sender as CameraNotifArgs).ImageRef;// byte[];
                }
                else
                {
                    _ImageDataArray = (sender as byte[]);
                }
            }

            if (_ImageDataArray?.Length > 0)
            {
                //_Imagefiledir = _FolderStr + "\\" + string.Format("Z{0:F2}.tif", _Motion.ZCurrentPos);
 
                Info.MixChannel.BitDepth = 16;
                //ImageProcessing.Save(_Imagefiledir, _ImageDataArray, Info, false);
                _ScanImage = LucidCamera.ToWriteableBitmap(RoiWidth, RoiHeight, _ImageDataArray, PixelFormatBitDepth);
                _ImageDataArray = null;
                sender = null;
                _GotNewImage = true;
                Logger.Log("AF thread receive image");
            }
            else
            {
                _ScanImage = null;
                _GotNewImage = true;
                Logger.Log("Failed to readout image");
            }
        }
        public bool SendCameraTrigger()
        {
            bool triggerSent = false;
            if (IsSimulationMode)
            {
                Thread.Sleep(50);
                SimulateWaitExposure(_Settings.ExposureTime);
                triggerSent = true;
                //send a simulation image
                byte v = (byte)(0x20);
                int imageSize = _Settings.ROI.Width * _Settings.ROI.Height * 2 - 1;
                byte[] image = Enumerable.Repeat<byte>(v, imageSize).ToArray();
                _Camera_CameraNotif(image);
            }
            else
            {
                int trycount = 0;
                while ((triggerSent = _LEDController.SendCameraTrigger()) == false)
                {
                    Thread.Sleep(1);
                    if (trycount > 1000)
                    {
                        Logger.LogError("Failed to send trigger to camera");
                        break;
                    }
                    if (_IsAbort)
                    {
                        break;
                    }
                    trycount++;
                }
                if (triggerSent)
                {
                    ((LucidCamera)_Camera).TriggeredByOutside = true;
                }
            }
            return triggerSent;
        }

        public bool WaitTriggerArmed()
        {
            bool ret = false;
            if (IsSimulationMode)
            {
                Thread.Sleep(50);
                ret = true;
            }
            else
            {
                ret = ((ILucidCamera)_Camera).WaitTriggerArmed();
            }
            return ret;
        }
        private void SimulateWaitExposure(double exposureSec)
        {
            const int exposureWaitMS = 100;
            var sw = new Stopwatch();
            sw.Start();
            while (sw.ElapsedMilliseconds < exposureSec * 1000)
            {
                if (_IsAbort)
                {
                    break;
                }
                Thread.Sleep(exposureWaitMS);
            }
            sw.Stop(); //simulation exposure ends
        }
        public bool BadImage(WriteableBitmap img)
        {
            bool b = false;
            if (img == null)
            {
                b = true;
            }
            else
            {
                b = BadImageIdentifier.IsBadImage(img);
                if (IsSimulationMode)
                {
                    b = false;
                }
            }
            return b;
        }
        #endregion Lucid Camera helper function

        #region Photometric Camera helper function
        public void SetupCamera()
        {
            _Camera.OnExposureChanged += _Camera_OnExposureChanged;
            //Set binning mode
            _Camera.HBin = 1;
            _Camera.VBin = 1;
            //Set CCD readout speed (0: Normal, 1: Fast)
            _Camera.ReadoutSpeed = 1;
            //Set gain
            _Camera.Gain = 1;
            //Set region of interest
            if (_Settings.ROI.Width > 0 && _Settings.ROI.Height > 0)
            {
                _Camera.RoiStartX = _Settings.ROI.X;
                _Camera.RoiStartY = _Settings.ROI.Y;
                _Camera.RoiWidth = _Settings.ROI.Width;
                _Camera.RoiHeight = _Settings.ROI.Height;
            }
            else
            {
                _Camera.RoiStartX = 0;
                _Camera.RoiStartY = 0;
                _Camera.RoiWidth = _Camera.ImagingColumns;
                _Camera.RoiHeight = _Camera.ImagingRows;
            }
        }
        public void RestartCamera()
        {
            _Camera.OnExposureChanged -= _Camera_OnExposureChanged;
            _Camera.Close();
            Thread.Sleep(1500);
            _Camera.Open();
            Thread.Sleep(100);
            SetupCamera();
        }
        public void _Camera_OnExposureChanged(bool starts)
        {
            _ledStateGet = false;
            if (starts && !_IsBadImage)
            {

                int tryLEDCounts = 0;
                //_MainBoard.SetLEDStatus(_Settings.LEDType, true);
                //_ledStateGet = true;
                do
                {
                    if (++tryLEDCounts > 3)
                    {
                        _MainBoard.SetLEDStatus(_Settings.LEDType, false);
                        ExitStat = ThreadExitStat.Error;
                        IsFailedToSetLED = true;
                        throw new Exception("LED Failure");
                    }
                    if (_MainBoard.SetLEDStatus(_Settings.LEDType, true))
                    {
                        Thread.Sleep(5);
                        _MainBoard.GetLEDStatus(_Settings.LEDType);
                        Thread.Sleep(5);
                        switch (_Settings.LEDType)
                        {
                            case LEDTypes.Green:
                                _ledStateGet = _MainBoard.IsGLEDOn;
                                break;
                            case LEDTypes.Red:
                                _ledStateGet = _MainBoard.IsRLEDOn;
                                break;
                            case LEDTypes.White:
                                _ledStateGet = _MainBoard.IsWLEDOn;
                                break;
                        }
                    }
                }
                while (_ledStateGet == false);
            }
        }
        #endregion Photometric Camera helper function
        protected void FireOnImageSampledEvent(double zpos, double sharpness)
        {
            Logger.Log($"Z:{zpos}, Sharpness:{sharpness}");
            OnImageSampled?.Invoke(zpos, sharpness);
        }
        protected void RelativeMoveZStage(double pos, double speed, double accel)
        {
            if (IsSimulationMode)
            {
                Thread.Sleep(50);
            }
            else
            {
                _Motion.RelativeMoveZStage(pos, speed, accel, true);
            }
        }

        protected void AbsoluteMoveZStage(double pos, double speed, double accel)
        {
            if (IsSimulationMode)
            {
                Thread.Sleep(50);
            }
            else
            {
                _Motion.AbsoluteMoveZStage(pos, speed, accel, true);
            }
        }
        
        public override void AbortWork()
        {
            _IsAbort = true;
        }
        protected abstract void ProcessAutoFocus();

    }
}
