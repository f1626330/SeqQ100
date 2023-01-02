using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
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
    public class AutoFocusChannelOffset : AutoFocusCommandBase
    {
        #region private field
        private double _FocusedScore;
        private double FOGscore = 0;
        private double _FiducialPos;
        private bool _IsMachineRev2;
        private Task _ContinuousThread;
        #endregion private field
        public double FoucsedScore
        {
            get { return _FocusedScore; }

        }
        public AutoFocusChannelOffset(Dispatcher callingDisptcher, MotionController motion, ICamera camera, LEDController ledController, AutoFocusSettings settings)
        {
            _Motion = motion;
            _Camera = camera;
            _LEDController = ledController;
            _Settings = settings;
        }


        protected override void ProcessAutoFocus()
        {
            try
            {                
                CreateImageFolder();
                //string ledn = "G";
                //StringBuilder folderBuilder = new StringBuilder();
                //folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
                //folderBuilder.Append("\\Sequlite\\ALF\\Recipe\\AutofocusFluroImages\\");
                //folderBuilder.Append(DateTime.Now.ToString("yyyyMMddHHmmss"));
                //folderBuilder.Append(string.Format("Channeloffset_X{0:00.00}mm_Y{1:00.00}mm_{2}s\\", Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2), Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2),
                //     _Settings.ExposureTime));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                // 3. set light source
                _Settings.LEDType = LEDTypes.Green;
                _LEDController.SetLEDIntensity(LEDTypes.Green, (int)_Settings.LEDIntensity);
                _LEDController.SetLEDIntensity(LEDTypes.Red, (int)_Settings.LEDIntensity);
                _LEDController.SetLEDControlledByCamera(LEDTypes.Green, true);
                double greenZfocus = 0;
                double redZfocus = 0;
                if (!IsSimulationMode)
                {
                    StartSteamLucidCamera();
                    AbsoluteMoveZStage(_Settings.Reference0, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                    #region AF green LED
                    //AF on green first
                    while (!_IsAutoFocusCompleted)
                    {
                        if (_IsAbort)
                        {
                            ExitStat = ThreadExitStat.Abort;
                            break;
                        }
                        _ScanImage = null;
                        int trycounts = 0;
                        bool hasError = false;
                        CaptureImageV2(ref trycounts, ref hasError);
                        if (!_IsAbort && !hasError)
                        {
                            //_Imagefiledir = _FolderStr + "\\" + string.Format("{0}{1}_Z{2:F2}_X{3:00.00}_Y{4:00.00}.tif", ledn, _Settings.OffsetFilterIndex,
                            //_Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2));
                            ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                            SharpnessCalculation(true);
                            HillClimbingMethod();
                        }
                        else
                        {
                            break;
                        }
                    }
                    Logger.Log($"Finish AF with Green LED, Z: {_Motion.ZCurrentPos:F}");
                    greenZfocus = _Motion.ZCurrentPos;
                    #endregion AF green LED
                    #region AF red LED
                    //AF on red 
                    _LeftSharpness = 0;
                    _CurrentSharpness = 0;
                    _RightSharpness = 0;
                    _IsLeftClimbing = true;
                    _IsFirstSample = true;
                    _IsAutoFocusCompleted = false;
                    //ledn = "R";
                    _Settings.LEDType = LEDTypes.Red;
                    _LEDController.SetLEDControlledByCamera(LEDTypes.Green, false);
                    _LEDController.SetLEDControlledByCamera(LEDTypes.Red, true);
                    while (!_IsAutoFocusCompleted)
                    {
                        if (_IsAbort)
                        {
                            ExitStat = ThreadExitStat.Abort;
                            break;
                        }
                        _ScanImage = null;
                        int trycounts = 0;
                        bool hasError = false;
                        CaptureImageV2(ref trycounts, ref hasError);
                        if (!_IsAbort && !hasError)
                        {
                            //_Imagefiledir = _FolderStr + "\\" + string.Format("{0}{1}_Z{2:F2}_X{3:00.00}_Y{4:00.00}.tif", ledn, _Settings.OffsetFilterIndex,
                            //_Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2));
                            ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                            SharpnessCalculation(true);
                            HillClimbingMethod();
                        }
                        else { break; }
                    }
                    Logger.Log($"Finish AF with Red LED, Z: {_Motion.ZCurrentPos:F}");
                    redZfocus = _Motion.ZCurrentPos;
                    Offset = Math.Round(_Motion.ZCurrentPos - greenZfocus, 2);
                    #endregion Hill Climb

                }
            }
            catch (ThreadAbortException)
            {
                ExitStat = ThreadExitStat.Abort;
            }
            catch (Exception ex)
            {
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


        protected override void SharpnessCalculation(bool isMachineRev2)
        {
            FindQCFromImage QC = new FindQCFromImage(_Imagefiledir);
            FOGscore = QC.QcValues[1] * QC.QcValues[6];
            OnImageSampledInvoke(_Motion.ZCurrentPos, FOGscore);
            if (_IsFirstSample)
            {
                _CurrentSharpness = FOGscore;
            }
            else if (_IsLeftClimbing)
            {
                _LeftSharpness = FOGscore;
            }
            else
            {
                _RightSharpness = FOGscore;
            }
        }
        public override void AbortWork()
        {
            _IsAbort = true;
        }


    }
}