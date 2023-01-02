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
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    public class AutoFocusTiltFid : AutoFocusCommandBase
    {
        #region private fields
        //private double CrossedIndex;
        private List<double> MetricPattern = new List<double>(); 
        private List<List<double>> _IntersectPattern = new List<List<double>>();
        
        #endregion private fields

        #region public properties
        public double Threshold { get; set; }
        //public List<List<double>> IntersectPattern = new List<List<double>>();
        public Dictionary<double, List<double>> NFMetricPatterns = new Dictionary<double, List<double>>();
        #endregion public properties

        #region Constructor

        public AutoFocusTiltFid(Dispatcher callingDispatcher, MotionController motion, ICamera camera, LEDController ledController, AutoFocusSettings settings, 
            Dictionary<double, List<double>>nfmetricpattern)
        {
            _Motion = motion;
            _Camera = camera;
            _LEDController = ledController;
            _Settings = settings;
            IsScanOnly = settings.IsScanonly;
            ScanInterval = settings.ScanInterval;
            IsRecipe = settings.IsRecipe;
            IsHCOnly = settings.IsHConly;
            if (IsScanOnly) { IsHCOnly = false; }
            NFMetricPatterns = nfmetricpattern;
            _IsUsingTiltFiducial = true;
        }
        #endregion Constructor

        protected override void ProcessAutoFocus()
        {
            try
            {
                CreateImageFolder();
                //StringBuilder folderBuilder = new StringBuilder();
                //folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
                //folderBuilder.Append("\\Sequlite\\ALF\\AutofocusImages\\");
                //folderBuilder.Append("Tilefidu-"); 
                //folderBuilder.Append(DateTime.Now.ToString("yyyyMMdd"));
                //folderBuilder.Append("\\");
                //folderBuilder.Append(DateTime.Now.ToString("HHmmss"));
                //folderBuilder.Append(string.Format("_X{0:00.00}mm_Y{1:00.00}mm\\", Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2), Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2)));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                // 3. set light source
                _LEDController.SetLEDIntensity(_Settings.LEDType, (int)_Settings.LEDIntensity);
                _LEDController.SetLEDControlledByCamera(_Settings.LEDType, true);
                if ((_Settings.ZstageLimitH - _Settings.ZstageLimitL) % ScanInterval != 0)
                {
                    _Settings.ZstageLimitH += ScanInterval - (_Settings.ZstageLimitH - _Settings.ZstageLimitL) % ScanInterval;
                }
                if (!IsSimulationMode)
                {
                    StartSteamLucidCamera();
                    // If have input metric pattern as reference
                    // Start Hillclimb
                    if (NFMetricPatterns?.Count() > 0)
                    {
                        _Motion.AbsoluteMoveZStage(_Settings.Reference0, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                        int aftrycount = 0;
                        while (!_IsAutoFocusCompleted)
                        {
                            if (_IsAbort)
                            {
                                ExitStat = ThreadExitStat.Abort;
                                return;
                            }
                            
                            int imgtrycounts = 0;
                            Info.MixChannel.FocusPosition = _Motion.ZCurrentPos;
                            _ScanImage = null;
                            //capture image
                            int trycounts = 0;
                            bool hasError = false;
                            CaptureImageV2(ref trycounts, ref hasError);
                            if (!_IsAbort && !hasError)
                            {
                                if (_Settings.RotationAngle != 0)
                                {
                                    _ScanImage = ImageProcessing.Rotate(_ScanImage, _Settings.RotationAngle);
                                }
                                _TryCounts += imgtrycounts;
                                SharpnessCalculation(true);
                                RecordMetric();
                                //_Imagefiledir = _FolderStr + "\\" + string.Format("Z{0:F2}_X{1:00.00}_Y{2:00.00}.tif", _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                                //    _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                                //ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, true);
                                HillClimbingMethod();

                                if (aftrycount++ > 20)
                                {
                                    ExitStat = ThreadExitStat.Error;
                                    Logger.Log("Error with AF fiducial Version 2.1 ");
                                    break;
                                }
                            }
                            
                        }
                    }
                    //if no input metric pattern, need first scan and build the pattern
                    else
                    {
                        if (ScanRangeForRev2())
                        {
                            _IsAutoFocusCompleted = true;
                            ExitStat = ThreadExitStat.None;
                            _FocusedSharpness = _CurrentSharpness;
                            return;
                        }
                        else
                        {
                            _IsAutoFocusCompleted = true;
                            ExitStat = ThreadExitStat.Error;
                            _FocusedSharpness = _CurrentSharpness;
                            return;
                        }
                    }
                }

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
        //for LucidCamera
        

        protected override void SharpnessCalculation(bool isMachineRev2)
        {
            Rect rect = new Rect(0, 0, 1, 1);
            MetricPattern = SharpnessEvaluation.MovingWindHStdDev(ref _ScanImage, _Settings.ROI.Height / (double)_ScanImage.PixelHeight,
                                    _Settings.ROI.Height / (double)_ScanImage.PixelHeight / 5);
        }

        private bool ScanRangeForRev2()
        {
            double stdMax = 0;
            double zMax = 0;
            double ZLimitL = _Settings.ZstageLimitL;
            double ZLimitH = _Settings.ZstageLimitH;
            bool ret = true;
            //((ILucidCamera)_Camera).SetExposure(_Settings.ExposureTime / 2 * 1000);
            try
            {
                int num = (int)((ZLimitH - ZLimitL) / ScanInterval) + 1;
                double[] zposrecord = new double[num];
                Dictionary<double, List<double>> metriccurves = new Dictionary<double, List<double>>();
                //List<List<double>> mtfcurves = new List<List<double>>();
                int poscount = 0;
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
                    CaptureImageV2(ref trycounts, ref ret);
                    if (ret)
                    {
                        if (_Settings.RotationAngle != 0)
                        {
                            _ScanImage = ImageProcessing.Rotate(_ScanImage, _Settings.RotationAngle);
                        }
                        Rect rect = new Rect(0, (double)_Settings.ROI.Y / _ScanImage.PixelHeight,
                        1, (double)_Settings.ROI.Height / _ScanImage.PixelHeight);
                        double stdDev = 0;
                        //Used previous method to map the curve and AF at first cycle, with modified ROI
                        stdDev = SharpnessEvaluation.HorizontalAveragedStdDev(ref _ScanImage, rect);
                        //_Imagefiledir = _FolderStr + "\\" + string.Format("Scan_Z{0:F2}_X{1:00.00}_Y{2:00.00}_Std{3:F3}.tif", _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                        //                _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], stdDev);
                        //Calculate curve. map curve pattern
                        List<double> metriccurve = new List<double>();
                        metriccurve = SharpnessEvaluation.MovingWindHStdDev(ref _ScanImage, _Settings.ROI.Height / (double)_ScanImage.PixelHeight, _Settings.ROI.Height / (double)_ScanImage.PixelHeight / 5);
                        metriccurves[zPos] = metriccurve;
                        zposrecord[poscount] = zPos;
                        poscount++;
                        if (stdDev > stdMax) { stdMax = stdDev; zMax = zPos; }
                        //ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, true);
                        _ScanImage = null;
                        FireOnImageSampledEvent(_Motion.ZCurrentPos, stdDev);
                    }

                }
                _FocusedSharpness = stdMax;
                //record pattern
                if (ret)
                {
                    RecordPattern(metriccurves, zposrecord,zMax);
                }
                _Motion.AbsoluteMoveZStage(zMax, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                if (zMax == ZLimitH || zMax == ZLimitL || _FocusedSharpness < 10)
                {
                    _IsAutoFocusCompleted = true;
                    ExitStat = ThreadExitStat.Error;
                    ExceptionMessage = "Out of Range";
                    return false;
                }
                else //check abnormal peaks.
                {
                    foreach(var pos in metriccurves.Keys)
                    {
                        if(metriccurves[pos].Max() < stdMax * 1.5)
                        {
                            NFMetricPatterns[pos - zMax] = metriccurves[pos];
                        }
                        else { Logger.LogError($"Z:{pos} Abnormal pattern scanned"); }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                ExceptionMessage = ex.ToString();
                return false;
            }

        }
        
        protected override void HillClimbingMethod()
        {
            double step = 0.5;
            step = SharpnessEvaluation.LikelihoodRelFoc(MetricPattern, NFMetricPatterns);
            if (step != double.NaN && _Motion.ZCurrentPos - step >= _Settings.ZstageLimitL && _Motion.ZCurrentPos + step <= _Settings.ZstageLimitH)
            {
                _IsAutoFocusCompleted = true;
                _Motion.RelativeMoveZStage(step, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                ExitStat = ThreadExitStat.None;
                _FocusedSharpness = _CurrentSharpness;
                return;
            }
            else
            {
                _IsAutoFocusCompleted = true;
                ExitStat = ThreadExitStat.Error;
                _FocusedSharpness = _CurrentSharpness;
                return;
            }

        }
        
        public override void AbortWork()
        {
            _IsAbort = true;
        }
        private void RecordMetric()
        {
            try
            {
                string _Filename = string.Format("{0}\\metriccurve-{1}.txt", _FolderStr, Path.GetFileName(_Imagefiledir));
                FileStream aFile = new FileStream(_Filename, FileMode.Create);
                StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                foreach (var t in MetricPattern)
                {
                    sw.WriteLine(t);
                }
                sw.Close();
                aFile.Close();
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
        private void RecordPattern(Dictionary<double, List<double>> metriccurves, double[] zposrecord, double zMax)
        {
            try
            {
                string _FileName = _FolderStr + "\\" + string.Format("ScanSTD_X{0:00.00}_Y{1:00.00}.csv", _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage],
                                        _Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);

                if (!File.Exists(_FileName))
                {
                    FileStream aFile = new FileStream(_FileName, FileMode.Create);
                    StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                    StringBuilder zposes = new StringBuilder();
                    for (int m = 0; m < zposrecord.Count(); m++)
                    {
                        zposes.Append(Math.Round(zposrecord[m] - zMax, 2));
                        zposes.Append(",");
                    }
                    sw.WriteLine(zposes.ToString());
                    for (int i = 0; i < metriccurves.ElementAt(0).Value.Count; i++)
                    {
                        StringBuilder samezdata = new StringBuilder();
                        for (int k = 0; k < metriccurves.Count(); k++)
                        {
                            samezdata.Append(metriccurves.ElementAt(k).Value[i]);
                            samezdata.Append(",");
                        }
                        sw.WriteLine(samezdata.ToString());
                    }
                    sw.Close();
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
    }
}

