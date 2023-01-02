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
    public class AutofocusOnFluoV2 : AutoFocusCommandBase
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
        public AutofocusOnFluoV2(Dispatcher callingDisptcher, MotionController motion, ICamera camera, LEDController ledController, AutoFocusSettings settings, double fiducialpos)
        {
            _Motion = motion;
            _Camera = camera;
            _LEDController = ledController;
            _Settings = settings;
            _FiducialPos = fiducialpos;
  
        }
        

        protected override void ProcessAutoFocus()
        {
            try
            {
                CreateImageFolder();
                //string ledn = (_Settings.LEDType == LEDTypes.Green) ? "G" : "R";
                //StringBuilder folderBuilder = new StringBuilder();
                //folderBuilder.Append(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.ImagingBaseDirSelection.TrimEnd(Path.DirectorySeparatorChar));
                //folderBuilder.Append("\\Sequlite\\ALF\\Recipe\\AutofocusFluroImages\\");
                //folderBuilder.Append(DateTime.Now.ToString("yyyyMMddHHmmss"));
                //folderBuilder.Append(string.Format("_X{0:00.00}mm_Y{1:00.00}mm_{2}{3}_{4}s\\", Math.Round(_Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2), Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2),
                //    ledn, _Settings.OffsetFilterIndex, _Settings.ExposureTime));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                // 3. set light source
                _LEDController.SetLEDIntensity(_Settings.LEDType, (int)_Settings.LEDIntensity);
                _LEDController.SetLEDControlledByCamera(_Settings.LEDType, true);
                if (!IsSimulationMode)
                {
                    StartSteamLucidCamera();
                    AbsoluteMoveZStage(_Settings.Reference0, _Settings.ZstageSpeed, _Settings.ZstageAccel);//, true);
                    #region Hill Climb
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
                            //    _Motion.ZCurrentPos, _Motion.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2));
                            ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                            SharpnessCalculation(true);
                            HillClimbingMethod();
                        }
                        else { break;  }
                        
                    }
                    Offset = Math.Round( (_FiducialPos - _Motion.ZCurrentPos), 2);
                    if (_Settings.LEDType == LEDTypes.Red) { Offset +=  SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset; }
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