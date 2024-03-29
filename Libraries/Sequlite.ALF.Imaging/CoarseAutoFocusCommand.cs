﻿using Sequlite.ALF.Common;
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
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{    /// <summary>
     /// Coarse Autofocus for Fiducial Version 2.0, with ALF2.0s hardwares
     /// </summary>
    public class CoarseAutoFocusCommand : AutoFocusCommandBase
    {
        #region private fields

        #endregion private fields

        #region constructor 
        public CoarseAutoFocusCommand(Dispatcher callingDispatcher, MotionController motion, ICamera camera, LEDController ledController, AutoFocusSettings settings)
        {
            _Motion = motion;
            _Camera = camera;
            _LEDController = ledController;
            _Settings = settings;
            //_IsMachineRev2 = true;
            IsScanOnly = settings.IsScanonly;
            ScanInterval = settings.ScanInterval;
            IsRecipe = settings.IsRecipe;
            IsHCOnly = settings.IsHConly;
            if (IsScanOnly) { IsHCOnly = false; }
        }
        #endregion constructor
        protected override void ProcessAutoFocus()
        {
            try
            {
                #region AF image saving directory
                //StringBuilder folderBuilder = new StringBuilder();
                //folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
                //folderBuilder.Append("\\Sequlite\\ALF\\AutofocusImages\\");
                //folderBuilder.Append(DateTime.Now.ToString("yyyyMMdd"));
                //folderBuilder.Append("\\");
                //folderBuilder.Append(DateTime.Now.ToString("HHmmss"));
                //folderBuilder.Append(string.Format("_X{0:00.00}mm_Y{1:00.00}mm\\", Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2), Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2)));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                #endregion AF image saving directory

                // Set light source & camera
                _LEDController.SetLEDIntensity(_Settings.LEDType, (int)_Settings.LEDIntensity);
                _LEDController.SetLEDControlledByCamera(_Settings.LEDType, true);
                if ((_Settings.ZstageLimitH - _Settings.ZstageLimitL) % ScanInterval != 0)
                {
                    _Settings.ZstageLimitH += ScanInterval - (_Settings.ZstageLimitH - _Settings.ZstageLimitL) % ScanInterval;
                }

                StartSteamLucidCamera();

                #region scan through
                if (!_IsAbort)
                {
                    bool _IspassScan = true;
                    if (!IsHCOnly)
                    {
                        Logger.Log("Scan Range");
                        _IspassScan = CoarseScanRangeForRev2();
                    }
                    else
                    {
                        AbsoluteMoveZStage(_Settings.Reference0, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                        if (!IsSimulationMode)
                        {
                            //Logger.Log($"Set Camera Exposure to {_Settings.ExposureTime}(sec)");
                            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime * 1000);
                        }
                    }

                    if (_IspassScan && !IsScanOnly)
                    {

                        int simulationAutoFocusTotalLoopCount = 0;// rand.Next(1,5);// 5;
                        int simulationAutoFocusLoopCount = 0;
                        if (IsSimulationMode)
                        {
                            var rand = new Random();
                            simulationAutoFocusTotalLoopCount = rand.Next(1, 5);
                        }

                        while (!_IsAutoFocusCompleted)
                        {
                            if (_IsAbort)
                            {
                                ExitStat = ThreadExitStat.Abort;
                                //return;
                                break;
                            }
                            Info.MixChannel.FocusPosition = _Motion.ZCurrentPos;
                            int trycounts = 0;
                            _ScanImage = null;
                            bool hasError = false;
                            //Capture image
                            CaptureImageV2(ref trycounts, ref hasError);

                            if (!_IsAbort && !hasError)
                            {
                                if (_Settings.RotationAngle != 0) //Rotate and crop image if fiducial is not horizontal
                                {
                                    _ScanImage = ImageProcessing.Rotate(_ScanImage, _Settings.RotationAngle);
                                }
                                _TryCounts += trycounts;
                                SharpnessCalculation(true);
                                //_Imagefiledir = _FolderStr + "\\" + string.Format("Z{0:F2}_X{1:00.00}_Y{2:00.00}_Std{3:F3}.tif", _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                                //    _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], sharpness);
                                //ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                                _ScanImage = null;
                                if (IsSimulationMode)
                                {
                                    if (simulationAutoFocusLoopCount < simulationAutoFocusTotalLoopCount)
                                    {
                                        Logger.Log("Hill Climbing Simulation");
                                        Thread.Sleep(1000);
                                        simulationAutoFocusLoopCount++;
                                    }
                                    else
                                    {
                                        _IsAutoFocusCompleted = true;
                                        ExitStat = ThreadExitStat.Error;
                                        _FocusedSharpness = _CurrentSharpness;
                                    }
                                }
                                else
                                {
                                    Logger.Log("Hill Climbing");
                                    HillClimbingMethod();
                                }
                            }
                            else
                            {

                                break;
                            }
                        } //while auto focus not completed
                    }
                }
                //}
                #endregion scan through

            }
            catch (ThreadAbortException)
            {
                ExitStat = ThreadExitStat.Abort;
                Logger.Log("AF Thread Abort");
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
                _LEDController.SetLEDControlledByCamera(_Settings.LEDType, false);
                if (IsSimulationMode)
                {
                    _SimCameraContinuousModeRunning = false;
                }
                else
                {
                    if (!IsRecipe)
                    {
                        StopLucidCameraTask();
                    }
                }
                if (_IsAbort)
                {
                    ExitStat = ThreadExitStat.Abort;
                }
                _IsAutoFocusCompleted = true;
                _ScanImage = null;

            }
        }
        /// <summary>
        /// Scan through with certain steps size and range. find top, innner_top, inner_bottom surfaces, set z to inner_top as new reference0(or around, depends on step size)  
        /// </summary>
        /// <returns>isSuccess</returns>
        private bool CoarseScanRangeForRev2()
        {
            double stdMax = 0;
            double zMax = 0;
            double ZLimitL = _Settings.ZstageLimitL; //um
            double ZLimitH = _Settings.ZstageLimitH;
            bool ret = true;
            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime / 2 * 1000);

            List<double> zPos_list = new List<double>();
            List<double> stdDev_list = new List<double>();
            var coarseScans = new List<Tuple<double, double>>();



            try
            {
                int num = (int)((ZLimitH - ZLimitL) / ScanInterval) + 1;
                double[,] scanArray = new double[num, 2];

                //Move stage from lower limit to top in defined step size
                for (double zPos = ZLimitL, i = 0; zPos <= ZLimitH; zPos += ScanInterval, i++)
                {
                    if (_IsAbort)
                    {
                        ExitStat = ThreadExitStat.Abort;
                        ret = false;
                        break;
                    }
                    AbsoluteMoveZStage(zPos, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                    Info.MixChannel.FocusPosition = zPos;
                    _ScanImage = null;
                    int trycounts = 0;
                    //Capture image, end loop until image pass tests.
                    CaptureImageV2(ref trycounts, ref ret);

                    if (ret)
                    {
                        if (_Settings.RotationAngle != 0)
                        {
                            WriteableBitmap tmp = _ScanImage;
                            _ScanImage = ImageProcessing.Rotate(tmp, _Settings.RotationAngle);
                            tmp = null;
                        }
                        Rect rect = new Rect(0, 0, 1, 1);
                        double stdDev = 0;
                        if (_Settings.ROI.Width > _Settings.ROI.Height) { stdDev = SharpnessEvaluation.VerticalAveragedStdDev(ref _ScanImage, rect); }
                        else { stdDev = SharpnessEvaluation.HorizontalAveragedStdDev(ref _ScanImage, rect); }
                        //_Imagefiledir = _FolderStr + "\\" + string.Format("Scan_Z{0:F2}_X{1:00.00}_Y{2:00.00}_Std{3:F3}.tif", _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                        //                _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], stdDev);
                        //ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                        _ScanImage = null;

                        //save (zPos, stdDev) to a list for peakfindings

                        zPos_list.Add(zPos);
                        stdDev_list.Add(stdDev);

                        coarseScans.Add(Tuple.Create(zPos, stdDev));

                        //Find Maximum and Zpos
                        if (stdDev > stdMax) { stdMax = stdDev; zMax = zPos; }
                        OnImageSampledInvoke(_Motion.ZCurrentPos, stdDev);
                    }
                }
                ////Normalization list;
                double[] norm_stdDev = NormalizeData((IEnumerable<double>)stdDev_list, 0, 1);

                //peaks finding on the list;
                IList<int> peaks = FindPeaks(norm_stdDev.ToList(), 10, 0.01); //check 5 step (10um) on each side 

                double zRef0 = zPos_list[peaks[1]];
                double stdRef0 = stdDev_list[peaks[1]];

                SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight = zPos_list[peaks[1]] - zPos_list[peaks[0]];
                SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness = zPos_list[peaks[2]] - zPos_list[peaks[1]];
                //move z stage to new reference0;

                if (ret)
                {
                    _FocusedSharpness = stdRef0; // stdMax; //
                    if (IsSimulationMode)
                    {
                        AbsoluteMoveZStage(zRef0, _Settings.ZstageSpeed, _Settings.ZstageAccel);     //zMax
                        ret = true;
                    }
                    else
                    {
                        if (!IsScanOnly)
                        {
                            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime * 1000);
                        }
                        if (zMax == ZLimitH || zMax == ZLimitL || _FocusedSharpness < 10) //Sharpness of noise is lower than 10 
                        {
                            _IsAutoFocusCompleted = true;
                            AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                            ExitStat = ThreadExitStat.Error;
                            ExceptionMessage = "Out of Range";
                            ret = false;
                        }
                        else
                        {
                            AbsoluteMoveZStage(zRef0, _Settings.ZstageSpeed, _Settings.ZstageAccel);//zMax;
                            ret = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                ExceptionMessage = ex.ToString();
                ret = false;
            }

            return ret;

        }

        #region ScanRangeForRev2

        /// <summary>
        /// Scan through with certain steps size and range. find global maximum(or around, depends on step size)
        /// </summary>
        /// <returns>isSuccess</returns>
        private bool ScanRangeForRev2()
        {
            double stdMax = 0;
            double zMax = 0;
            double ZLimitL = _Settings.ZstageLimitL; //um
            double ZLimitH = _Settings.ZstageLimitH;
            bool ret = true;
            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime / 2 * 1000);
            try
            {
                int num = (int)((ZLimitH - ZLimitL) / ScanInterval) + 1;
                //Move stage from lower limit to top in defined step size
                for (double zPos = ZLimitL; zPos <= ZLimitH; zPos += ScanInterval)
                {
                    if (_IsAbort)
                    {
                        ExitStat = ThreadExitStat.Abort;
                        ret = false;
                        break;
                    }
                    AbsoluteMoveZStage(zPos, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                    Info.MixChannel.FocusPosition = zPos;
                    _ScanImage = null;
                    int trycounts = 0;
                    //Capture image, end loop until image pass tests.
                    CaptureImageV2(ref trycounts, ref ret);

                    if (ret)
                    {
                        if (_Settings.RotationAngle != 0)
                        {
                            WriteableBitmap tmp = _ScanImage;
                            _ScanImage = ImageProcessing.Rotate(tmp, _Settings.RotationAngle);
                            tmp = null;
                        }
                        Rect rect = new Rect(0, 0, 1, 1);
                        double stdDev = 0;
                        if (_Settings.ROI.Width > _Settings.ROI.Height) { stdDev = SharpnessEvaluation.VerticalAveragedStdDev(ref _ScanImage, rect); }
                        else { stdDev = SharpnessEvaluation.HorizontalAveragedStdDev(ref _ScanImage, rect); }
                        //_Imagefiledir = _FolderStr + "\\" + string.Format("Scan_Z{0:F2}_X{1:00.00}_Y{2:00.00}_Std{3:F3}.tif", _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                        //                _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], stdDev);
                        //ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                        _ScanImage = null;
                        //Find Maximum and Zpos
                        if (stdDev > stdMax) { stdMax = stdDev; zMax = zPos; }
                        OnImageSampledInvoke(_Motion.ZCurrentPos, stdDev);
                    }
                }

                if (ret)
                {
                    _FocusedSharpness = stdMax;
                    if (IsSimulationMode)
                    {
                        AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                        ret = true;
                    }
                    else
                    {
                        if (!IsScanOnly)
                        {
                            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime * 1000);
                        }
                        if (zMax == ZLimitH || zMax == ZLimitL || _FocusedSharpness < 10) //Sharpness of noise is lower than 10 
                        {
                            _IsAutoFocusCompleted = true;
                            AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                            ExitStat = ThreadExitStat.Error;
                            ExceptionMessage = "Out of Range";
                            ret = false;
                        }
                        else
                        {
                            AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                            ret = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                ExceptionMessage = ex.ToString();
                ret = false;
            }

            return ret;

        }
        #endregion

        private static double[] NormalizeData(IEnumerable<double> data, int min, int max)
        {
            double dataMax = data.Max();
            double dataMin = data.Min();
            double range = dataMax - dataMin;

            return data
                .Select(d => (d - dataMin) / range)
                .Select(n => (double)((1 - n) * min + n * max))
                .ToArray();
        }
        private static IList<int> FindPeaks(IList<double> values, int rangeOfPeaks, double threshold)
        {
            List<int> peaks = new List<int>();
            double current;
            IEnumerable<double> range;

            int checksOnEachSide = rangeOfPeaks / 2;
            for (int i = 0; i < values.Count; i++)
            {
                current = values[i];
                range = values;

                if (i > checksOnEachSide)
                {
                    range = range.Skip(i - checksOnEachSide);
                }

                range = range.Take(rangeOfPeaks);
                if ((range.Count() > 0) && (current == range.Max()) && (i != 0) && (i != values.Count - 1) && ((range.Max() - range.Min()) > threshold))
                {
                    peaks.Add(i);
                }
            }

            return peaks;
        }
    }
}
