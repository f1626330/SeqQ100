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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    /// <summary>
    /// Autofocus algorithm for Fiducial Version 1.0, with ALF1.0s hardwares
    /// </summary>
    public class AutoFocusCommand : AutoFocusCommandBase
    {
        #region Constructor
        public AutoFocusCommand(Dispatcher callingDisptcher, MotionController motion, ICamera camera, Mainboard mainboard, AutoFocusSettings settings)
        {
            _Motion = motion;
            _Camera = camera;
            _MainBoard = mainboard;
            _Settings = settings;
            //_IsMachineRev2 = false;
            IsScanOnly = settings.IsScanonly;
            ScanInterval = settings.ScanInterval;
        }

        #endregion Constructor

       
        protected override void ProcessAutoFocus()
        {
            try
            {
                //StringBuilder folderBuilder = new StringBuilder();
                //folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
                //folderBuilder.Append("\\Sequlite\\ALF\\AutofocusImages\\");
                //folderBuilder.Append(DateTime.Now.ToString("yyyyMMdd"));
                //folderBuilder.Append("\\");
                //folderBuilder.Append(DateTime.Now.ToString("HHmmss"));
                //folderBuilder.Append(string.Format("_{0:00.00}m\\", Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2)));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                // 1. select the filter if filter index is not zero
                if (_Settings.FilterIndex >= 0) //ALF 1.1 compatiblity
                {
                    _Motion.SelectFilter(_Settings.FilterIndex, true);
                }
                // 3. set light source
                _MainBoard.SetLEDIntensity(_Settings.LEDType, _Settings.LEDIntensity);
                
                if (!IsSimulationMode)
                {
                    SetupCamera();
                    if (ScanRange() && !IsScanOnly)
                    {
                        while (!_IsAutoFocusCompleted)
                        {
                            if (_IsAbort)
                            {
                                ExitStat = ThreadExitStat.Abort;
                                return;
                            }
                            _ScanImage = null;
                            int trycounts = 0;
                            do
                            {
                                _Camera.GrabImage(_Settings.ExposureTime, CaptureFrameType.Normal, ref _ScanImage);
                                _MainBoard.SetLEDStatus(_Settings.LEDType, false);
                                if (_ScanImage != null)
                                {
                                    _IsBadImage = BadImageIdentifier.IsBadImage(_ScanImage);
                                }
                                if (_ScanImage == null || _IsBadImage || !_ledStateGet)
                                {
                                    trycounts += 1;
                                    if (trycounts == 2 || trycounts == 5)
                                    {
                                        RestartCamera();
                                    }
                                    if (trycounts > 10)
                                    {
                                        ExitStat = ThreadExitStat.Error;
                                        _IsAutoFocusCompleted = true;
                                        _IsFailedCaptureImage = true;
                                        return;
                                    }
                                }

                            }
                            while (_IsBadImage || _ScanImage == null || !_ledStateGet);
                            if (_ScanImage != null && _Settings.RotationAngle < 0)
                            {
                                //TransformedBitmap tb = new TransformedBitmap();
                                //tb.BeginInit();
                                //tb.Source = _ScanImage;
                                //System.Windows.Media.ScaleTransform transform = new System.Windows.Media.ScaleTransform();
                                //transform.ScaleX = -1;
                                //tb.Transform = transform;
                                //tb.EndInit();
                                //_ScanImage = new WriteableBitmap(tb);
                                //tb = null;
                                _ScanImage = ImageProcessing.WpfFlip(_ScanImage, ImageProcessing.FlipAxis.Horizontal);
                            }

                            if (SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle != 0)
                            {
                                WriteableBitmap tmp = _ScanImage;
                                _ScanImage = ImageProcessing.Rotate(tmp, Math.Abs(SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle));
                                tmp = null;
                            }
                            _TryCounts += trycounts;
                            SharpnessCalculation(false);
                            Info.MixChannel.FocusPosition = _Motion.ZCurrentPos;
                            //string imagefilename = _FolderStr + "\\" + string.Format("Z{0:F2}__Y{1:00.00}_Std{2:F3}.tif", _Motion.ZCurrentPos, _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], sharpness);
                            //ImageProcessing.Save(imagefilename, _ScanImage, Info, false);
                            _ScanImage = null;
                            HillClimbingMethod();
                        }
                    }
                }

            }
            catch (ThreadAbortException)
            {
                ExitStat = ThreadExitStat.Abort;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                if (ex.Message == "LED Failure")
                {
                    ExceptionMessage = ex.ToString();
                }
                else
                {
                    ExitStat = ThreadExitStat.Error;
                    ExceptionMessage = ex.ToString();
                }
            }
            finally
            {
                _MainBoard.SetLEDStatus(_Settings.LEDType, false);
                _Camera.OnExposureChanged -= _Camera_OnExposureChanged;
            }
        }

        private bool ScanRange()
        {
            double stdMax = 0;
            double zMax = 0;
            double ZLimitL = _Settings.ZstageLimitL;
            double ZLimitH = _Settings.ZstageLimitH;
            int filterfail = 0;
            try
            {
                for (double zPos = ZLimitL; zPos <= ZLimitH; zPos += ScanInterval)
                {
                    if (_IsAbort)
                    {
                        ExitStat = ThreadExitStat.Abort;
                        return false;
                    }
                    _Motion.AbsoluteMoveZStage(zPos, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                    Info.MixChannel.FocusPosition = zPos;
                    _ScanImage = null;
                    int trycounts = 0;
                    do
                    {
                        _Camera.GrabImage(_Settings.ExposureTime / 2, CaptureFrameType.Normal, ref _ScanImage);
                        #region Image check
                        if (_ScanImage != null)
                        {
                            _IsBadImage = BadImageIdentifier.IsBadImage(_ScanImage);
                        }
                        //if (!_IsMachineRev2 &&
                        if(_ScanImage == null || _IsBadImage || (!_ledStateGet))
                        {
                            trycounts += 1;
                            if (trycounts == 2 || trycounts == 5)
                            {
                                RestartCamera();
                            }
                            if (trycounts > 10)
                            {
                                ExitStat = ThreadExitStat.Error;
                                _IsAutoFocusCompleted = true;
                                _IsFailedCaptureImage = true;
                                return false;
                            }
                        }
                        #endregion Image check
                        Thread.Sleep(10);
                    }

                while (_IsBadImage || _ScanImage == null ||   (!_ledStateGet)) ;
                if (_ScanImage != null && _Settings.RotationAngle < 0)
                    {
                        //TransformedBitmap tb = new TransformedBitmap();
                        //tb.BeginInit();
                        //tb.Source = _ScanImage;
                        //System.Windows.Media.ScaleTransform transform = new System.Windows.Media.ScaleTransform();
                        //transform.ScaleX = -1;
                        //tb.Transform = transform;
                        //tb.EndInit();
                        //_ScanImage = new WriteableBitmap(tb);
                        _ScanImage = ImageProcessing.WpfFlip(_ScanImage, ImageProcessing.FlipAxis.Horizontal);
                    }

                    if (_Settings.RotationAngle != 0)
                    {
                        _ScanImage = ImageProcessing.Rotate(_ScanImage, Math.Abs(_Settings.RotationAngle));
                    }

                    Rect rect = new Rect(0, 0, 1, 1);
                    double stdDev = SharpnessEvaluation.HorizontalAveragedStdDev(ref _ScanImage, rect);
                    //string imagefilename = _FolderStr + "\\" + string.Format("scan_Z{0:F2}__Y{1:00.00}_Std{2:F3}.tif", _Motion.ZCurrentPos, Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2), stdDev);
                    //ImageProcessing.Save(imagefilename, _ScanImage, Info, false);
                    //if (!_IsMachineRev2 && 
                    if(stdDev < 10)
                    {
                        //MessageBox.Show((_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactorSettings[MotionTypes.Filter]).ToString());
                        filterfail++;
                        _Motion.SelectFilter(_Settings.FilterIndex, true);
                    }
                    if (stdDev > stdMax) { stdMax = stdDev; zMax = zPos; }
                    OnImageSampledInvoke(_Motion.ZCurrentPos, stdDev);
                }
                Filterfail = filterfail;
                _FocusedSharpness = stdMax;
                _ScanImage = null;
                if (zMax == ZLimitH || zMax == ZLimitL || _FocusedSharpness < 20)
                {
                    _IsAutoFocusCompleted = true;
                    _Motion.AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                    ExitStat = ThreadExitStat.Error;
                    ExceptionMessage = "Out of Range";
                    return false;
                }
                else
                {
                    _Motion.AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                    return true;
                }
            }
            catch(Exception ex)
            {
                ExceptionMessage = ex.ToString();
                Logger.LogError(ExceptionMessage);
                return false;
            }
            
        }

    }
}

