using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    public class AutofocusOnFluoV1: AutoFocusCommandBase
    {
        #region private field
        private double FOGscore = 0;
        private double _FiducialPos;
        #endregion private field



        public AutofocusOnFluoV1(Dispatcher callingDisptcher, MotionController motion, ICamera camera, Mainboard mainboard, AutoFocusSettings settings, double fiducialpos)
        {
            _Motion = motion;
            _Camera = camera;
            _MainBoard = mainboard;
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
                //folderBuilder.Append(string.Format("_{0:00.00}m_{1}{2}_{3}s\\", Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2),
                //    ledn, _Settings.OffsetFilterIndex, _Settings.ExposureTime));
                //_FolderStr = folderBuilder.ToString();
                //Directory.CreateDirectory(_FolderStr);
                // 1. select the filter if filter index is not zero
                if (_Settings.OffsetFilterIndex >= 0) //ALF 1.1 compatiblity
                {
                    _Motion.SelectFilter(_Settings.OffsetFilterIndex, true);
                }
                _Motion.AbsoluteMoveZStage((_Settings.ZstageLimitH + _Settings.ZstageLimitL) / 2, _Settings.ZstageSpeed, _Settings.ZstageAccel, true);
                // 3. set light source
                _MainBoard.SetLEDIntensity(_Settings.LEDType, _Settings.LEDIntensity);
                SetupCamera();

                #region Hill Climb
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
                    _TryCounts += trycounts;
                    //_Imagefiledir = _FolderStr + "\\" + string.Format("{0}{1}_Z{2:F2}_Y{3:00.00}.tif", ledn, _Settings.OffsetFilterIndex, _Motion.ZCurrentPos, Math.Round(_Motion.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2));
                    ImageProcessing.Save(_Imagefiledir, _ScanImage, Info, false);
                    SharpnessCalculation(false);
                    HillClimbingMethod();
                }
                Offset = _FiducialPos - _Motion.ZCurrentPos;
                if (_Settings.LEDType == LEDTypes.Red) { Offset += 0.25; }

                #endregion Hill Climb

            }
            catch (ThreadAbortException)
            {
                ExitStat = ThreadExitStat.Abort;
            }
            catch (Exception ex)
            {
                if (ex.Message == "LED Failure")
                {
                    ExceptionMessage = ex.Message;
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