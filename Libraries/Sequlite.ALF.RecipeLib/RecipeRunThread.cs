using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace Sequlite.ALF.RecipeLib
{
    public class RecipeRunThread : RecipeRunThreadBase
    {
        //define all static fields here for easy  maintainers  -----------------------------------------------------------------
        //todo: need re-think if we really need "static ", static sometime causes problem
        private static int _IncCount;
        private static bool _IsOneRef;
        //this may have problem because it is onetime assigned, if _IsOneRef later it won't get it new value
        //so shall we change it to  something like this??   =>  !_IsOneRef
        private static bool _ScanEveryRegion = !_IsOneRef; 
        private static readonly int regioninterval = 6;
        //-------------------------------------------------------------------------------------------------------------

        #region Private field
        private ICamera _Camera;
        private WriteableBitmap _CapturedImage;
        private int _LastLoopIncCount;
        #endregion Private fields
      
        #region Constructor
        public RecipeRunThread(Dispatcher callingDispatcher,
            RecipeRunSettings _recipeRunConfig,
            Recipe recipe,
            ICamera camera,
            MotionController motionController,
            Mainboard mainBoard,
            IFluidics fluidics,
            RecipeThreadParameters recipeparam,
            RecipeRunThreadBase outterThread,
             OLAJobManager olaJob, 
             bool waitForOLAComplete ) : //wait OLA done inside recipe thread -- if true) : 
            base(callingDispatcher, _recipeRunConfig, recipe, motionController, mainBoard, fluidics, recipeparam, outterThread, olaJob, waitForOLAComplete)
        {
            _Camera = camera;
            _LoadCartridge = recipeparam.LoadCartridge;
            _isEnableOLA = recipeparam.IsEnableOLA;
            _Expoinc = recipeparam.Expoinc;
            _RLEDinc = recipeparam.RLEDinc;
            _GLEDinc = recipeparam.GLEDinc;
            B_Offset = recipeparam.Bottom_Offset;
            T_Offset = recipeparam.Top_Offset;
            _StartInc = recipeparam.StartInc;
            _UserEmail = recipeparam.UserEmail;
            _IsBC = recipeparam.IsBC;
            _SelectedTemplate = recipeparam.SelectedTemplate;
            _IsOneRef = recipeparam.OneRef;
            _IsBackUp = recipeparam.BackUpData;
            _ScanEveryRegion = !_IsOneRef;
            WaitTimeThreshold = 10;
            _IsIndex = recipeparam.IsIndex;
            Logger.LogMessage("Recipe Thread Created");
            Logger.LogMessage(string.Format("OLA:{0}, Expo:{1}, GLED:{2}, RLED:{3}, B_Offset:{4}, T_Offset:{5}, BC:{6}, Template:{7}", _isEnableOLA, _Expoinc, _GLEDinc, _RLEDinc, B_Offset,
                T_Offset, _IsBC, _SelectedTemplate.ToString()));
        }
        #endregion Constructor

        public override void ThreadFunction()
        {
            try
            {
                //OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = "" });
                if (_LoadCartridge)
                {
                    int tgtPos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    if (_MotionController.CCurrentPos != tgtPos)
                    {
                        int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                        int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                        // do not do absolute moves if in simulation
                        if (!IsSimulationMode)
                            _MotionController.AbsoluteMove(MotionTypes.Cartridge, tgtPos, speed, accel, true);
                    }
                }

                foreach (var item in Recipe.Steps)
                {
                    if (IsAbort) 
                    {
                        OnStepRunUpdatedInvoke(item.Step, "Recipe Abort", true);
                        return; 
                    }
                    RunStepTree(item);
                }

                if (FailImageTranfer.Count > 0)
                {
                    ImagingStep step = new ImagingStep();
                    OnStepRunUpdatedInvoke(step, string.Format("Retry image transfer{0}", FailImageTranfer), false);
                    List<(string, string)> filestocopy = FailImageTranfer;
                    FailImageTranfer = new List<(string, string)>();
                    foreach((string, string) filename in filestocopy)
                    {
                        BackupImage(step, filename.Item1, filename.Item2);
                    }
                }
                WaitForBackingupImageComplete();
                if (FailImageTranfer.Count > 0 && !IsInnerRecipeRunning)
                {
                    foreach(var item in FailImageTranfer)
                    {
                        Logger.LogMessage(string.Format("Images failed to transfer: {0}", item));
                    }
                }
                if (!IsInnerRecipeRunning && !IsSimulationMode && _IsBackUp)
                {
                    Logger.LogMessage("Transfer txt file");
                    for (int i = 0; i < _Folderlist.Count; i++)
                    {
                        if(File.Exists(Path.Combine(_Folderlist[i], "list.txt")))
                        {
                            File.Copy(Path.Combine(_Folderlist[i], "list.txt"), Path.Combine(Directory.GetParent(_NasFolderlist[i].TrimEnd(Path.DirectorySeparatorChar)).FullName, "list.txt"), true);
                            Logger.LogMessage(string.Format("Finish transfer txt file{0}", _Folderlist[i]));
                        }
                    }
                    _NasFolderlist.Clear();
                    _Folderlist.Clear();
                }
                if (!IsInnerRecipeRunning)
                {
                    _MainBoard.SetChemiTemperCtrlStatus(false);
                }
            }
            catch (ThreadAbortException)
            {
                Logger.LogWarning("Recipe Thread Aborted");
                OLAJobs?.Stop();
            }
            catch (Exception ex)
            {
                ExMessage = ex.ToString();
                Logger.LogError(ExMessage);
                MessageBox.Show(ExMessage);
                ExitStat = ThreadExitStat.Error;
                OLAJobs?.Stop();
            }
            finally
            {
                _Camera.OnExposureChanged -= _Camera_OnExposureChanged;
                WaitForOLAComplete();
            }
        }

        private void RunStepTree(StepsTree tree)
        {
            Stopwatch sw = Stopwatch.StartNew();
            OnStepRunUpdatedInvoke(tree.Step, StartRunningMessage, false);
            _CurrentTree = tree;
            switch (tree.Step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    RunStepProc((SetTemperStep)tree.Step);
                    break;
                case RecipeStepTypes.StopTemper:
                    RunStepProc((StopTemperStep)tree.Step);
                    break;
                case RecipeStepTypes.Imaging:
                    RunStepProc((ImagingStep)tree.Step);
                    break;
                case RecipeStepTypes.MoveStage:
                    RunStepProc((MoveStageStep)tree.Step);
                    break;
                case RecipeStepTypes.Pumping:
                    RunStepProc((PumpingStep)tree.Step);
                    break;
                case RecipeStepTypes.Loop:
                    ((LoopStep)tree.Step).LoopCounts = 1;
                    OnLoopStepUpdatedInvoke(tree);
                    for (int i = 0; i < ((LoopStep)tree.Step).LoopCycles; i++)
                    {
                        OnStepRunUpdatedInvoke(tree.Step, string.Format("Loop: {0}", i + 1), false);
                        foreach (var item in tree.Children)
                        {
                            if (IsAbort)
                            {
                                OnStepRunUpdatedInvoke(item.Step, "Recipe Abort", true);
                                return;
                            }
                            RunStepTree(item);
                        }
                        if (i < ((LoopStep)tree.Step).LoopCycles - 1)
                        {
                            ((LoopStep)tree.Step).LoopCounts++;
                        }
                        OnLoopStepUpdatedInvoke(tree);
                    }
                    ((LoopStep)tree.Step).LoopCounts = 0;
                    OnLoopStepUpdatedInvoke(tree);
                    break;
                case RecipeStepTypes.RunRecipe:
                    RunStepProc((RunRecipeStep)tree.Step);
                    break;
                case RecipeStepTypes.Waiting:
                    RunStepProc((WaitingStep)tree.Step);
                    break;
            }
            OnStepRunUpdatedInvoke(tree.Step, tree.Step.ToString() + " took " + sw.ElapsedMilliseconds + " ms", false);
        }
        public override void AbortWork()
        {
            IsAbort = true;
            if (_InnerRecipeRunThread != null)
            {
                _InnerRecipeRunThread.IsAbort = true;
                _InnerRecipeRunThread.Abort();
                while (_AutoFocusProcess != null)
                {
                    Thread.Sleep(10);
                }
            }
            FluidicsInterface.StopPumping();
            FluidicsInterface.WaitForPumpingCompleted();

            if (_AutoFocusProcess != null)
            {
                _AutoFocusProcess.Abort();
                while (_AutoFocusProcess != null)
                {
                    Thread.Sleep(10);
                }
            }
            _MotionController.HaltAllMotions();
            _MainBoard.SetLEDStatus(LEDTypes.Green, false);
            _MainBoard.SetLEDStatus(LEDTypes.Red, false);
            _MainBoard.SetLEDStatus(LEDTypes.White, false);
            _MainBoard.SetChemiTemperCtrlStatus(false);
            _Camera.StopCapture();
            _Camera.OnExposureChanged -= _Camera_OnExposureChanged;

            foreach (var item in Recipe.Steps)
            {
                ResetLoopStepCounts(item);
            }
            _NasFolderlist.Clear();
            _Folderlist.Clear();
        }
        //private bool ResetLoopStepCounts(StepsTree tree)
        //{
        //    if (tree.Step is LoopStep)
        //    {
        //        ((LoopStep)tree.Step).LoopCounts = 0;
        //        OnLoopStepUpdatedInvoke(tree);
        //        foreach (var item in tree.Children)
        //        {
        //            ResetLoopStepCounts(item);
        //        }
        //        return true;
        //    }
        //    else { return false; }
        //}

        #region Run Step Functions
        private void RunStepProc(SetTemperStep step)
        {
            Logger.LogMessage(string.Format("Setting temperature to {0}.", step.TargetTemper));
            // Calculate the ramp according to the duration: ramp = (target temper - current temper)/duration
            _MainBoard.GetChemiTemper();
            double currentTemper = _MainBoard.ChemiTemper;

            double ramp;
            if (step.Duration == 0)       // at full speed
            {
                ramp = 0;
            }
            else
            {
                ramp = Math.Abs((step.TargetTemper - currentTemper) / step.Duration);
                if (ramp < 0.001) { ramp = 0.001; }
                else if (ramp > 2) { ramp = 2; }
            }
            
            var startTimeSpan = TimeSpan.FromMilliseconds(0);
            var periodTimeSpan = TimeSpan.FromSeconds(30);
            using (var timer = new Timer(e => OnStepRunUpdatedInvoke(step, String.Format("Thread:{0}: {1}", Thread.CurrentThread.Name, "Heating/Cooling"), false), null, startTimeSpan, periodTimeSpan))
            {
                _MainBoard.SetChemiTemperCtrlRamp(ramp);
                Thread.Sleep(100);
                _MainBoard.SetChemiTemper(step.TargetTemper);
                Thread.Sleep(100);
                _MainBoard.SetChemiTemperCtrlStatus(true);
                Thread.Sleep(100);
                if (step.WaitForComplete)
                {
                    double temperDif = _MainBoard.ChemiTemper - step.TargetTemper; 
                    int count = 0;
                    while (Math.Abs(temperDif) > step.Tolerance && !IsSimulationMode)
                    {
                        // do not sleep in simulation
                        if (!IsSimulationMode)
                            Thread.Sleep(2000);
                        _MainBoard.GetChemiTemper();
                        temperDif = _MainBoard.ChemiTemper - step.TargetTemper;
                        Thread.Sleep(1);
                        count++;
                        // If wait more than expect send the command again,
                        if ((step.Duration == 0 && count == Math.Round((Math.Abs((step.TargetTemper - currentTemper) / 2) + 1) * 5)) || (step.Duration != 0 && count == ((step.Duration + 1) * 5)))
                        {
                            _MainBoard.SetChemiTemperCtrlStatus(false);
                            Thread.Sleep(1000);
                            _MainBoard.SetChemiTemperCtrlRamp(ramp);
                            Thread.Sleep(100);
                            OnStepRunUpdatedInvoke(step, "Resend Temp CMD", true);
                            if (_MainBoard.SetChemiTemper(step.TargetTemper))
                            {
                                _MainBoard.SetChemiTemperCtrlStatus(true);

                            }
                            else
                            {
                                OnStepRunUpdatedInvoke(step, "Temp Control failed to setTemper", true);
                                AbortWork();
                                throw new Exception("Temp Control failed to setTemper");
                            }
                        }
                        // If wait more than twice expect, stop and throw exception
                        if ((step.Duration == 0 && count > Math.Round((Math.Abs((step.TargetTemper - currentTemper) / 2) + 1) * 10)) || (step.Duration != 0 && count > ((step.Duration + 1) * 10)))
                        {
                            OnStepRunUpdatedInvoke(step, "Temp Control failed", true);
                            AbortWork();
                            throw new Exception("Temp Control failure");
                        }

                    }
                    // do not wait if in simulation mode
                    
                }
                startTimeSpan = TimeSpan.FromMilliseconds(-1);
                Thread.Sleep(50);
            }
            Logger.LogMessage("End Setting temperature");
        }
        private void RunStepProc(PumpingStep step)
        {
            Logger.LogMessage(string.Format("Start Pumping for {0}.", Enum.GetName(typeof(ModeOptions), step.PumpingType)));
            PumpingSettings _PumpSetting = new PumpingSettings();
            _PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
            _PumpSetting.PullRate = step.PullRate;
            _PumpSetting.PumpingVolume = step.Volume;
            _PumpSetting.PushRate = step.PushRate;
            _PumpSetting.SelectedMode = step.PumpingType;
            _PumpSetting.SelectedPullPath = step.PullPath;
            _PumpSetting.SelectedPushPath = step.PushPath;
            _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = step.Reagent };
            int trycounts = 0;
            int startvol = FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol;
            int pumpstartpos = FluidicsInterface.Pump.PumpAbsolutePos;
            int predvol = startvol + step.Volume;
            if (_PumpSetting.SelectedMode == ModeOptions.Pull) 
            { 
                if((500 - FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor) < step.Volume)
                {
                    PumpingSettings PumpSettings = new PumpingSettings();
                    PumpSettings.SelectedPushPath = PathOptions.Waste;
                    PumpSettings.SelectedMode = ModeOptions.Dispense;
                    PumpSettings.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                    PumpSettings.PumpingVolume = FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor;
                    FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, true, IsSimulationMode);
                }
                Thread.Sleep(50);
            }
            if (_PumpSetting.SelectedMode == ModeOptions.Push) 
            { 
                if(FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor < step.Volume)
                {
                    _PumpSetting.PumpingVolume = Math.Round(FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor);
                    //step.Volume = (int)Math.Round(FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor);
                }
                predvol = startvol - (int)_PumpSetting.PumpingVolume;
                if (_PumpSetting.SelectedPushPath == PathOptions.Waste) { predvol = startvol; }
            }
            do
            {
                if (IsAbort)
                {
                    OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                    return;
                }
                if (trycounts > 0)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Solution{1} pump step failed, try counts: [{0}]", trycounts, _PumpSetting.SelectedSolution.ValveNumber), false);
                }
                if (trycounts == 2 && !IsSimulationMode)
                {
                    FluidicsInterface.Pump.Reconnect();
                    Thread.Sleep(2000);
                    FluidicsInterface.Pump.GetPumpPos();
                    OnStepRunUpdatedInvoke(step, "Pump reconnected", true);
                }
                int voldif = Math.Abs(FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol);
                if (voldif < step.Volume)
                {
                    if(trycounts > 0)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format("Pumped solution{2} {0}uL less than target volume {1}uL, retry.", 
                            FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol, step.Volume, _PumpSetting.SelectedSolution.ValveNumber), true);
                    }
                    _PumpSetting.PumpingVolume = step.Volume - voldif;
                }
                else if (voldif > step.Volume)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Pumped solution{2} {0}uL more than target volume {1}uL, recipe continue.", 
                        FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol, step.Volume, _PumpSetting.SelectedSolution.ValveNumber), true);
                    break;
                }
                OnStepRunUpdatedInvoke(step, string.Format("[Before pumping: Pump position: {0}, Valve current position: {1},  Selected Solution {3} Solution Volume:{2}]", FluidicsInterface.Pump.PumpAbsolutePos, _Valve.CurrentPos,
                    FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol, _PumpSetting.SelectedSolution.ValveNumber), false);
                FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                if (!IsSimulationMode)
                {
                    FluidicsInterface.WaitForPumpingCompleted();
                }

                OnStepRunUpdatedInvoke(step, string.Format("[After pumping: Pump position: {0}, Valve current position: {1},  Selected Solution {3} Solution Volume:{2}]", FluidicsInterface.Pump.PumpAbsolutePos, _Valve.CurrentPos,
                FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol, _PumpSetting.SelectedSolution.ValveNumber), false);
                // do not check retrys if in simulation
                trycounts++;
                if (trycounts > 5 && !IsSimulationMode)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Pumping step tried 5 times, failed."), true);
                    var msgResult = MessageBox.Show("Pumping step failed, stop?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (msgResult == MessageBoxResult.Yes)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format("Pumping step failed, recipe stop."), true);
                        AbortWork();
                        ExitStat = ThreadExitStat.Error;
                        throw new System.InvalidOperationException("Pumping Failure");
                    }
                    else if (msgResult == MessageBoxResult.No)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format("Pumping step failed, recipe continue."), true);
                        break;
                    }

                }

            }
            // do not check difference from setpoint in simulations
            while (!IsSimulationMode && Math.Abs(FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - predvol) > 3 
            && _PumpSetting.SelectedPushPath != PathOptions.Manual && _PumpSetting.SelectedPullPath != PathOptions.Manual
            && _PumpSetting.SelectedPullPath != PathOptions.Waste
            && _PumpSetting.SelectedPushPath != PathOptions.Bypass && _PumpSetting.SelectedPullPath != PathOptions.Bypass);
            Logger.LogMessage(string.Format("End Pumping for {0}.", Enum.GetName(typeof(ModeOptions), step.PumpingType)));
        }
        private void RunStepProc(ImagingStep step)
        {
            // Image Intensity check.
            ImageStatistics ImageStat = new ImageStatistics();
            // 1. create save folder
            if (!Directory.Exists(_RecipeRunImageDataDir))
            {
                Directory.CreateDirectory(_RecipeRunImageDataDir);
                using (StreamWriter sw = File.AppendText(Path.Combine(_RecipeRunImageDataDir, "list.txt")))
                {
                    if (_IsBC && !Recipe.RecipeName.Contains("_CL"))
                    {
                        sw.WriteLine("bc");
                    }
                    else { sw.WriteLine("qc"); }
                    sw.WriteLine(Path.GetFileName(_UserEmail));
                    sw.WriteLine(_SelectedTemplate.ToString());
                    if (!_IsIndex) { sw.WriteLine("1"); }
                    else { sw.WriteLine("0"); }
                    sw.WriteLine(SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName);
                }
            }
            if (!Directory.Exists(_NasFolder) && !IsSimulationMode && _IsBackUp)
            {
                try
                {
                    Directory.CreateDirectory(_NasFolder);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.ToString());
                    OnStepRunUpdatedInvoke(step, "Failed to create Nas folder", true);
                }
            }

            //Set loop count
            string loopInfo = string.Empty;
            _loopCount = 0;
            if (_CurrentTree.Parent != null)
            {
                if (_CurrentTree.Parent.Step.StepType == RecipeStepTypes.Loop)
                {
                    _loopCount = ((LoopStep)_CurrentTree.Parent.Step).LoopCounts + _StartInc - 1;
                    loopInfo = string.Format("Inc{0}", _loopCount);
                }
            }
            else
            {
                _loopCount = _StartInc - 1;
                loopInfo = string.Format("Inc{0}", _loopCount);
            }
            
            int skipcounts = 0;
            int regioncounts = 0;
            int Ystartpos = 0;
            int Ystartspeed = 0;
            double cyclediff = 0;
            #region One reference method
            if (_IsOneRef && _loopCount == 1)
            {
                for(int i= step.Regions[0].RegionIndex+1; i< SettingsManager.ConfigSettings.YStageRegionPositions.Count; i++)
                {
                    ImagingRegion newregion = new ImagingRegion();
                    newregion.RegionIndex = i;
                    for(int j=0; j < step.Regions[0].Imagings.Count; j++)
                    {
                        newregion.Imagings.Add(step.Regions[0].Imagings[j]);
                    }
                    for(int k=0; k<step.Regions[0].ReferenceFocuses.Count; k++)
                    {
                        FocusSetting focussetting = new FocusSetting();
                        focussetting.Position = step.Regions[0].ReferenceFocuses[k].Position;
                        newregion.ReferenceFocuses.Add(focussetting);
                    }
                    step.Regions.Add(newregion);
                }
            }
            #endregion One reference method
            // 2. capture images for each region
            foreach (var region in step.Regions)
            {

                regioncounts++;
                // 1. move Y stage to the region
                int YtargetPos = (int)Math.Round((SettingsManager.ConfigSettings.YStageRegionPositions[region.RegionIndex] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                if (!IsSimulationMode)
                {
                    if(!_MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, speed,
                        (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true))
                    {
                        OnStepRunUpdatedInvoke(step, "Failed to Move Y stage, retry", false);
                        if (!_MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, speed,
                        (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true))
                        {
                            OnStepRunUpdatedInvoke(step, "Failed to Move Y stage, Recipe stop", true);
                            AbortWork();
                            ExitStat = ThreadExitStat.Error;
                            throw new System.InvalidOperationException("Y-Stage Movement Failure");
                        }
                    }
                    if (regioncounts == 1) { Ystartpos = YtargetPos; Ystartspeed = speed; }
                    _ImageInfo.MixChannel.YPosition = Math.Round(_MotionController.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);

                }
                else
                {
                    if (regioncounts == 1) { Ystartpos = YtargetPos; Ystartspeed = speed; }
                    _ImageInfo.MixChannel.YPosition = Math.Round(YtargetPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);
                    Thread.Sleep(200);
                }
                int focusIndex = 0;
                for (; focusIndex < region.ReferenceFocuses.Count; focusIndex++)
                {
                    string _Surface = (focusIndex == 0) ? "b" : "t";
                    double refFocus0 = region.ReferenceFocuses[focusIndex].Position;
                    #region 2. do auto focus if necessary, or else just move z stage to refFocus0
                    if(_ScanEveryRegion || _loopCount == 1 || ((regioncounts - 1) % regioninterval == 0 && focusIndex == 0))
                    {
                        if (step.IsAutoFocusOn)
                        {
                            AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                            //_AutoFocusSetting.LEDType = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDType;
                            //_AutoFocusSetting.LEDIntensity = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDIntensity;
                            //_AutoFocusSetting.ExposureTime = SettingsManager.ConfigSettings.AutoFocusingSettings.ExposureTime;
                            //_AutoFocusSetting.ZstageSpeed = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageSpeed;
                            //_AutoFocusSetting.ZstageAccel = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageAccel;
                            //_AutoFocusSetting.ZRange = SettingsManager.ConfigSettings.AutoFocusingSettings.ZRange;
                            //_AutoFocusSetting.FilterIndex = SettingsManager.ConfigSettings.AutoFocusingSettings.FilterIndex;
                            _AutoFocusSetting.ZstageLimitH = refFocus0 + _AutoFocusSetting.ZRange / 2;
                            _AutoFocusSetting.ZstageLimitL = refFocus0 - _AutoFocusSetting.ZRange / 2;
                            _AutoFocusSetting.ExtraExposure = SettingsManager.ConfigSettings.CameraDefaultSettings.ExtraExposure;
                            Int32Rect _AutofocusRoi = new Int32Rect();
                            _AutofocusRoi.X = (_Camera.ImagingColumns - 1) - (SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.X + SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Width); //fliped roi 
                            _AutofocusRoi.Y = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Y;
                            _AutofocusRoi.Width = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Width;
                            _AutofocusRoi.Height = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Height;
                            _AutoFocusSetting.ROI = _AutofocusRoi;
                            //_AutoFocusSetting.RotationAngle = SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle;
                            _AutoFocusSetting.ScanInterval = 1;
                            //_AutoFocusSetting.IsScanonly = false;
                            _AutoFocusProcess = new AutoFocusCommand(_CallingDispatcher, _MotionController, _Camera, _MainBoard, _AutoFocusSetting);
                            _AutoFocusProcess.IsSimulationMode = IsSimulationMode;
                            _AutoFocusProcess.Completed += AutoFocusProcess_Completed;
                            //_IsAutoFocusing = true;
                            _AutoFocusProcess.Name = "Autofocus";
                            _AutoFocusProcess.Start();
                            _AutoFocusProcess.Join();

                            // make it always succeed in simulation
                            if (_IsAutoFocusingSucceeded || IsSimulationMode)
                            {
                                refFocus0 = _MotionController.ZCurrentPos;
                                OnStepRunUpdatedInvoke(step, string.Format("Thread:{4}: Autofocus Succeed at y:{2}m z:{0:F2}u, sharpness:{1:F2}, trycount:{3}, Filter Fail:{5}", _MotionController.ZCurrentPos, _FocusedSharpness, _ImageInfo.MixChannel.YPosition, _AutoFocustrycount, Thread.CurrentThread.Name, _FilterFail), false);
                            }
                            else
                            {
                                // retry
                                if (_AutoFocusErrorMessage.Contains("Range")) //shift range if out of range
                                {
                                    refFocus0 = _MotionController.ZCurrentPos;
                                }
                                OnStepRunUpdatedInvoke(step, string.Format("Autofocus fail at at y:{2}m z:{0:F2}u, sharpness:{1:F2}, LED Failure:{3}, Capture Failure:{4}, trycount:{5}, Error Message:{6}, Filter Fail:{7}, retry.", _MotionController.ZCurrentPos, _FocusedSharpness, _ImageInfo.MixChannel.YPosition, _IsFailedtoSetALED, _IsFailedCaptureAImage, _AutoFocustrycount, _AutoFocusErrorMessage, _FilterFail), false);
                                #region Push potential bubble
                                OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                                PumpingSettings _PumpSetting = new PumpingSettings();
                                _PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
                                _PumpSetting.PullRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
                                _PumpSetting.PumpingVolume = 30;
                                _PumpSetting.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                                _PumpSetting.SelectedMode = ModeOptions.AspirateDispense;
                                _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = 10 };
                                _PumpSetting.SelectedPullPath = PathOptions.FC;
                                _PumpSetting.SelectedPushPath = PathOptions.Waste;
                                FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                                if (!IsSimulationMode)
                                {
                                    FluidicsInterface.WaitForPumpingCompleted();
                                }

                                #endregion Push potential bubble
                                _AutoFocusSetting.ZstageLimitH = _MotionController.ZCurrentPos + 10;
                                _AutoFocusSetting.ZstageLimitL = _MotionController.ZCurrentPos - 10;
                                if (_IsFailedCaptureAImage || (_FocusedSharpness == 0 && !_IsFailedtoSetALED))
                                {
                                    OnStepRunUpdatedInvoke(step, "Force GC", false);
                                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                                    GC.WaitForPendingFinalizers();
                                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                                    GC.Collect();
                                    OnStepRunUpdatedInvoke(step, "Restart Camera", false);
                                    _Camera.Close();
                                    Thread.Sleep(1500);
                                    OnStepRunUpdatedInvoke(step, "Restarting", false);
                                    _Camera.Open();
                                    Thread.Sleep(100);
                                    OnStepRunUpdatedInvoke(step, "Restarted", false);
                                }
                                _AutoFocusSetting.ScanInterval = 1;
                                _AutoFocusSetting.IsScanonly = false;
                                _AutoFocusProcess = new AutoFocusCommand(_CallingDispatcher, _MotionController, _Camera, _MainBoard, _AutoFocusSetting);
                                _AutoFocusProcess.Completed += AutoFocusProcess_Completed;
                                //_IsAutoFocusing = true;
                                _AutoFocusProcess.Name = "Autofocus";
                                _AutoFocusProcess.Start();
                                _AutoFocusProcess.Join();
                                //while (_IsAutoFocusing) { Thread.Sleep(1); }
                                if (_IsAutoFocusingSucceeded)
                                {
                                    refFocus0 = _MotionController.ZCurrentPos;
                                    OnStepRunUpdatedInvoke(step, string.Format("Autofocus Succeed at y:{2}m z:{0:F2}u, sharpness:{1:F2}, trycount:{3}, Filter Fail:{4}",
                                        _MotionController.ZCurrentPos, _FocusedSharpness, _ImageInfo.MixChannel.YPosition, _AutoFocustrycount, _FilterFail), false);
                                }
                                else
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format("Autofocus fail at at y:{2}m z:{0:F2}u, sharpness:{1:F2}, skip region, LED Failure:{3}, Capture Failure:{4}, trycount:{5}. Exception Message:{6}, Filter Fail:{7}",
                                        _MotionController.ZCurrentPos, _FocusedSharpness, _ImageInfo.MixChannel.YPosition, _IsFailedtoSetALED, _IsFailedCaptureAImage, _AutoFocustrycount, _AutoFocusErrorMessage, _FilterFail), true);
                                    skipcounts += 1;
                                    if (skipcounts == 1)
                                    {
                                        OnStepRunUpdatedInvoke(step, string.Format("Autofocus failed in region{0}.", _ImageInfo.MixChannel.YPosition), true);
                                        var msgResult = MessageBox.Show("Autofocus failed, Continue?", "Warning", MessageBoxButton.YesNo);
                                        if (msgResult == MessageBoxResult.No)
                                        {
                                            OnStepRunUpdatedInvoke(step, string.Format("Autofocus failed, mannual stopped."), true);
                                            AbortWork();
                                            ExitStat = ThreadExitStat.Error;
                                            throw new System.InvalidOperationException("Autofocus Failure");
                                        }
                                    }
                                    else if (skipcounts > 1)
                                    {
                                        OnStepRunUpdatedInvoke(step, string.Format("Autofocus failed again, recipe stop."), true);
                                        AbortWork();
                                        ExitStat = ThreadExitStat.Error;
                                        throw new System.InvalidOperationException("Autofocus Failure");
                                    }
                                    continue;
                                }
                            }
                            cyclediff = refFocus0 - region.ReferenceFocuses[focusIndex].Position;
                            region.ReferenceFocuses[focusIndex].Position = refFocus0;
                            #region Recalculate Offset every 30 cycle at the mid region
                            if (regioncounts == Math.Round((double)step.Regions.Count / 2) && ((_loopCount) % 30 == 0 && (_loopCount) >= 30) && Recipe.RecipeName.Contains("Inc"))
                            {
                                double previouoffset = B_Offset;
                                if (focusIndex == 1) { previouoffset = T_Offset; }

                                AutoFocusSettings OffsetAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                                Int32Rect offsetroi = new Int32Rect();
                                offsetroi.X = 800;
                                offsetroi.Y = 600;
                                offsetroi.Width = 1500;
                                offsetroi.Height = 1500;
                                OffsetAFSettings.ROI = offsetroi;
                                OffsetAFSettings.ZstageLimitH = refFocus0 + 2 - previouoffset;
                                OffsetAFSettings.ZstageLimitL = refFocus0 - 2 - previouoffset;
                                //OffsetAFSettings.ZstageAccel = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageAccel;
                                //OffsetAFSettings.ZstageSpeed = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageSpeed;
                                OffsetAFSettings.LEDType = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetLEDType;
                                if (OffsetAFSettings.LEDType == LEDTypes.Green)
                                {
                                    OffsetAFSettings.LEDIntensity = _OffsetGreenLEDInt;
                                    OffsetAFSettings.ExposureTime = _OffsetGreenLEDExp;

                                }
                                else if (OffsetAFSettings.LEDType == LEDTypes.Red)
                                {
                                    OffsetAFSettings.LEDIntensity = _OffsetRedLEDInt;
                                    OffsetAFSettings.ExposureTime = _OffsetRedLEDExp;
                                }
                                //OffsetAFSettings.OffsetFilterIndex = SettingsManager.ConfigSettings.AutoFocusingSettings.OffsetFilterIndex;
                                _AutoFocusFluoProcess = new AutofocusOnFluoV1(_CallingDispatcher, _MotionController, _Camera, _MainBoard, OffsetAFSettings, refFocus0);
                                _AutoFocusFluoProcess.IsSimulationMode = IsSimulationMode;
                                _AutoFocusFluoProcess.Completed += _AutoFocusFluoProcess_Completed;
                                _AutoFocusFluoProcess.Start();
                                _AutoFocusFluoProcess.Join();
                                if (_IsOffsetCalSucc)
                                {
                                    if (Math.Abs(_CalculatedOffset - previouoffset) < 1.1)
                                    {
                                        if (focusIndex == 0)
                                        {
                                            B_Offset = _CalculatedOffset;
                                            OnStepRunUpdatedInvoke(step, string.Format("Recalculate Offset success, offset changed from {1} to {0}", B_Offset, previouoffset), false);
                                        }
                                        if (focusIndex == 1)
                                        {
                                            T_Offset = _CalculatedOffset;
                                            OnStepRunUpdatedInvoke(step, string.Format("Recalculate Offset success, offset changed from {1} to {0}", T_Offset, previouoffset), false);
                                        }
                                    }
                                    else
                                    {
                                        _IsOffsetCalSucc = false;
                                        Logger.LogMessage(string.Format("Calculated Offset:{0}, pervious offset{1}, difference too large", _CalculatedOffset, previouoffset));
                                    }
                                }
                                else { OnStepRunUpdatedInvoke(step, "Recalculate offset failed, use pervious offset", true); }
                            }
                            #endregion Recalculate offset
                        }
                        else
                        {
                            if (!IsSimulationMode)
                            {
                                _MotionController.AbsoluteMoveZStage(refFocus0,
                                    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed,
                                    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel,
                                    true);
                            }
                            else
                            {
                                Thread.Sleep(200);
                            }
                        }
                    }
                    else
                    {
                        refFocus0 = region.ReferenceFocuses[focusIndex].Position + cyclediff;
                        region.ReferenceFocuses[focusIndex].Position = refFocus0;
                    }

                    

                    #endregion AF
                    if (_IsOneRef && _loopCount == 1 && regioncounts < step.Regions.Count)
                    {
                        step.Regions[regioncounts].ReferenceFocuses[focusIndex].Position = refFocus0;
                    }
                    #region 3. config Camera settings (ROI, binning, gain, readout speed)
                    Int32Rect roi = new Int32Rect();
                    if (SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth > 0 && SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight > 0)
                    {
                        roi.X = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiLeft;
                        roi.Width = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth;
                        roi.Y = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiTop;
                        roi.Height = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight;
                        roi.X = _Camera.ImagingColumns - 1 - (roi.Width + roi.X);
                    }
                    else
                    {
                        roi.X = 0;
                        roi.Y = 0;

                        // do not set here in simulation because unintialized
                        if (!IsSimulationMode)
                        {
                            roi.Width = _Camera.ImagingColumns / _Camera.HBin;
                            roi.Height = _Camera.ImagingRows / _Camera.VBin;
                        }
                    }
                    _ImageSetting = new ImageChannelSettings();
                    _ImageSetting.AdGain = SettingsManager.ConfigSettings.CameraDefaultSettings.Gain;
                    _ImageSetting.BinningMode = SettingsManager.ConfigSettings.CameraDefaultSettings.BinFactor;
                    if (SettingsManager.ConfigSettings.CameraDefaultSettings.ReadoutSpeed == "Normal")
                    {
                        _ImageSetting.ReadoutSpeed = 0;
                    }
                    else
                    {
                        _ImageSetting.ReadoutSpeed = 1;
                    }
                    _ImageSetting.IsCaptureFullRoi = false;
                    _ImageSetting.IsEnableBadImageCheck = true;
                    _ImageSetting.ExtraExposure = SettingsManager.ConfigSettings.CameraDefaultSettings.ExtraExposure;
                    DateTime dateTime = DateTime.Now;
                    _ImageInfo.DateTime = System.String.Format("{0:G}", dateTime.ToString());
                    _ImageInfo.BinFactor = _ImageSetting.BinningMode;
                    _ImageInfo.ReadoutSpeed = (_ImageSetting.ReadoutSpeed == 0) ? "Normal" : "Fast";
                    _ImageInfo.GainValue = _ImageSetting.AdGain;
                    #endregion Config camera setting
                    // 4. capture all images at all reference focuses
                    double zPos = refFocus0;
                    if (!IsSimulationMode)
                    {
                        #region Camera setup
                        //Set binning mode
                        _Camera.HBin = _ImageSetting.BinningMode;
                        _Camera.VBin = _ImageSetting.BinningMode;
                        //Set CCD readout speed (0: Normal, 1: Fast)
                        _Camera.ReadoutSpeed = _ImageSetting.ReadoutSpeed;
                        //Set gain
                        _Camera.Gain = _ImageSetting.AdGain;
                        //Set region of interest
                        if (roi.Width > 0 && roi.Height > 0 && !_ImageSetting.IsCaptureFullRoi)
                        {
                            _Camera.RoiStartX = (ushort)roi.X;
                            _Camera.RoiWidth = (ushort)(roi.Width);
                            _Camera.RoiStartY = (ushort)roi.Y;
                            _Camera.RoiHeight = (ushort)(roi.Height);
                        }
                        else
                        {
                            _Camera.RoiStartX = 0;
                            _Camera.RoiStartY = 0;
                            _Camera.RoiWidth = _Camera.ImagingColumns / _Camera.HBin - 1;
                            _Camera.RoiHeight = _Camera.ImagingRows / _Camera.VBin - 1;
                        }
                    }
                    #endregion Camera setup
                    _Camera.OnExposureChanged += _Camera_OnExposureChanged;
                    //1.move z stage to the reference position
                    if (focusIndex == 0)
                    {
                        zPos = refFocus0 - B_Offset;
                    }
                    else if (focusIndex == 1)
                    {
                        zPos = refFocus0 - T_Offset;
                    }
                    double zPosc = zPos;

                    // 2. capture all images in the region at the same focus
                    for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
                    {
                        if (IsAbort)
                        {
                            OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                            return;
                        }
                        var imaging = region.Imagings[imagingIndex];
                        if (imaging.Channels == ImagingChannels.Red)
                        {
                            zPosc = zPos + SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset;
                        }
                        else
                        {
                            zPosc = zPos;
                        }
                       
                        _MotionController.AbsoluteMoveZStage(zPosc,
                        SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed,
                        SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel,
                        true);
                        Thread.Sleep(10);   // wait for stage stop moving
                        // 1. Select Filter
                        int filterIndex = 0;
                        if (imaging.Filter != FilterTypes.None) //ALF 1.1 compatiblity
                        {
                            switch (imaging.Filter)
                            {
                                case FilterTypes.Filter1:
                                    filterIndex = 1;
                                    break;
                                case FilterTypes.Filter2:
                                    filterIndex = 2;
                                    break;
                                case FilterTypes.Filter3:
                                    filterIndex = 3;
                                    break;
                                case FilterTypes.Filter4:
                                    filterIndex = 4;
                                    break;
                            }
                            if (!IsSimulationMode)
                            {
                                if (!_MotionController.SelectFilter(filterIndex, true))
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format("Filter Move failed in region{0}, retry", _ImageInfo.MixChannel.YPosition), false);
                                    Thread.Sleep(30 * 1000);
                                    if (!_MotionController.SelectFilter(filterIndex, true))
                                    {
                                        OnStepRunUpdatedInvoke(step, string.Format("Filter Move failed in region{0}", _ImageInfo.MixChannel.YPosition), true);
                                        var msgResult = MessageBox.Show("Filter Movement failed, Continue?", "Warning", MessageBoxButton.YesNo);
                                        if (msgResult == MessageBoxResult.No)
                                        {
                                            OnStepRunUpdatedInvoke(step, string.Format("Filter Movement failed, mannual stopped."), true);
                                            ExitStat = ThreadExitStat.Error;
                                            AbortWork();
                                            throw new System.InvalidOperationException("Filter Movement Failure");
                                        }
                                    }
                                    
                                }
                            }
                            else
                            {
                                Thread.Sleep(50);
                            }
                        }
                        // 2. Set Capturing parameters and image metadata

                        if (imaging.Channels != ImagingChannels.RedGreen)
                        {
                            // 1. set led and exposure
                            switch (imaging.Channels)       // ignore RedGreen Channel
                            {
                                case ImagingChannels.Green:
                                    _ImageSetting.LED = LEDTypes.Green;
                                    _ImageSetting.LedIntensity = imaging.GreenIntensity;
                                    _ImageSetting.Exposure = imaging.GreenExposureTime;
                                    break;
                                case ImagingChannels.Red:
                                    _ImageSetting.LED = LEDTypes.Red;
                                    _ImageSetting.LedIntensity = imaging.RedIntensity;
                                    _ImageSetting.Exposure = imaging.RedExposureTime;
                                    break;
                                case ImagingChannels.White:
                                    _ImageSetting.LED = LEDTypes.White;
                                    _ImageSetting.LedIntensity = imaging.WhiteIntensity;
                                    _ImageSetting.Exposure = imaging.WhiteExposureTime;
                                    break;
                            }
                            // increase intensity and expo For long cycle
                            if (_loopCount != 0)
                            {
                                _ImageSetting.Exposure = Math.Round((_ImageSetting.Exposure * Math.Pow(_Expoinc, _loopCount - 1)), 3);
                                if (_ImageSetting.LED == LEDTypes.Green)
                                {
                                    _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * Math.Pow(_GLEDinc, _loopCount - 1)));
                                    _OffsetGreenLEDInt = _ImageSetting.LedIntensity;
                                    _OffsetGreenLEDExp = _ImageSetting.Exposure;
                                }
                                if (_ImageSetting.LED == LEDTypes.Red)
                                {
                                    _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * Math.Pow(_RLEDinc, _loopCount - 1)));
                                    _OffsetRedLEDInt = _ImageSetting.LedIntensity;
                                    _OffsetRedLEDExp = _ImageSetting.Exposure;
                                }
                            }
                            else
                            {
                                _ImageSetting.Exposure = Math.Round(_ImageSetting.Exposure * _Expoinc, 3);
                                if (_ImageSetting.LED == LEDTypes.Green)
                                {
                                    _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * _GLEDinc));
                                    _OffsetGreenLEDInt = _ImageSetting.LedIntensity;
                                    _OffsetGreenLEDExp = _ImageSetting.Exposure;
                                }
                                if (_ImageSetting.LED == LEDTypes.Red)
                                {
                                    _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * _RLEDinc));
                                    _OffsetRedLEDInt = _ImageSetting.LedIntensity;
                                    _OffsetRedLEDExp = _ImageSetting.Exposure;
                                }
                            }
                            #region Captured Image Info
                            _ImageInfo.MixChannel.LightIntensity = (int)_ImageSetting.LedIntensity;
                            _ImageInfo.MixChannel.Exposure = _ImageSetting.Exposure;
                            _ImageInfo.MixChannel.LightSource = _ImageSetting.LED.ToString();
                            Int32Rect roiRect = new Int32Rect((_Camera.ImagingColumns - 1 - (_Camera.RoiWidth + _Camera.RoiStartX)), _Camera.RoiStartY, _Camera.RoiWidth, _Camera.RoiHeight);
                            _ImageInfo.MixChannel.ROI = roiRect;
                            _ImageInfo.MixChannel.FilterPosition = filterIndex;
                            _ImageInfo.MixChannel.IsAutoFocus = step.IsAutoFocusOn;

                            string ledn = (_ImageSetting.LED == LEDTypes.Green) ? "G" : "R";
                            #endregion Captured Image Info
                            // 2. Capture image
                            for (int imagecount = 0; imagecount < _ImageCounts; imagecount++)
                            {
                                _ImageInfo.MixChannel.FocusPosition = _MotionController.ZCurrentPos;
                                #region capture image
                                int tryCounts;
                                int _LowIntImagecounts = 0;
                                bool _LowIntImage = false;
                                double _ImageMean;
                                if (!IsSimulationMode)
                                {
                                    do
                                    {
                                        _ImageMean = 0;
                                        tryCounts = 0;
                                        CaptureImage(step, ref roi, ref tryCounts);
                                        _ImageMean = ImageStat.GetAverage(_CapturedImage);
                                        if (_ImageMean < 450 && Recipe.RecipeName.Contains("Inc"))
                                        {
                                            _LowIntImage = true;
                                            OnStepRunUpdatedInvoke(step, string.Format("Thread:{0} Captured low intensity image: {1}_{2}_{3}{4}_{15}{5:00.00}mm_{6:F2}um_{7:F3}s_{8}Int_PD{13}_Mean{14:F2}.tif, retry, LED Failed:{9}, trycounts{10}, Badimage{11}, Nullimage{12}, Low Intensity Image{15}", 
                                                Thread.CurrentThread.Name, Recipe.RecipeName, loopInfo, ledn, filterIndex, _ImageInfo.MixChannel.YPosition,
                                                _MotionController.ZCurrentPos, _ImageSetting.Exposure, _ImageSetting.LedIntensity, _LEDFailure, tryCounts, 
                                                BadImageCounts, NullImageCounts, _PDValue, _ImageMean, _LowIntImagecounts), true);
                                            if (_LowIntImagecounts++ > 5)
                                            {
                                                throw new Exception("Capturing low intensity Image, possible LED Failure");
                                            }
                                            _CapturedImage = null;
                                            _MainBoard.SetLEDStatus(_ImageSetting.LED, false);
                                            Thread.Sleep(100);
                                        }
                                        else
                                        {
                                            _LowIntImage = false;
                                            if (_CapturedImage.CanFreeze) { _CapturedImage.Freeze(); }
                                        }

                                    }
                                    while (_LowIntImage);
                                }
                                else //simulation
                                {
                                    _ImageMean = 0;
                                    tryCounts = 0;
                                    Thread.Sleep((int) (_ImageSetting.Exposure * 1000));
                                }
                                string imagename = string.Format("{0}_{1}_{2}{3}_{4}{5:00.00}mm_{6:F2}um_{7:F3}s_{8}Int_PD{9}_Mean{10:F2}.tif",
                                    Recipe.RecipeName, loopInfo, ledn, filterIndex, _Surface, 
                                    _ImageInfo.MixChannel.YPosition,
                                    (IsSimulationMode? zPosc: _MotionController.ZCurrentPos), 
                                    _ImageSetting.Exposure, _ImageSetting.LedIntensity, _PDValue, _ImageMean);
                                Logger.LogMessage(string.Format("Captured image{0}, LED Failed:{1}, trycounts{2}, Badimage{3}, Nullimage{4}, Low Intensity Image{5}", 
                                    imagename, _LEDFailure, tryCounts, BadImageCounts, NullImageCounts, _LowIntImagecounts));
                                #endregion Capture image
                                //Save Image
                                if (!IsSimulationMode)
                                {
                                    if (_CapturedImage != null && !_IsFailedtoSetLED)
                                    {
                                        try
                                        {
                                            string filename = _RecipeRunImageDataDir + "\\" + imagename;
                                            int imagenum = 0;
                                            _ImageFileName = filename;
                                            while (File.Exists(_ImageFileName))
                                            {
                                                imagenum += 1;
                                                _ImageFileName = filename.Replace(filename.Substring(filename.Length - 4), string.Format("({0}){1}", imagenum, filename.Substring(filename.Length - 4)));
                                            }
                                            ImageProcessing.Save(_ImageFileName, _CapturedImage, _ImageInfo, false);
                                            using (StreamWriter sw = File.AppendText(Path.Combine(_RecipeRunImageDataDir, "list.txt")))
                                            {
                                                sw.WriteLine(Path.GetFileName(filename));
                                            }
                                            OnStepRunUpdatedInvoke(step, string.Format("Thread:{0} Saved image: {1}", Thread.CurrentThread.Name, imagename), false);
                                            if (_IsBackUp) 
                                            {
                                                string destpath = Path.Combine(_NasFolder, Path.GetFileName(_ImageFileName));
                                                BackupImage(step, _ImageFileName, destpath); 
                                            }
                                            _CapturedImage = null;
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.LogError(ex.ToString());
                                            throw;
                                        }
                                    }
                                    else
                                    {
                                        if (_IsFailedtoSetLED)
                                        {
                                            OnStepRunUpdatedInvoke(step, "Failed to set LED, skip this image", true);
                                        }
                                        OnStepRunUpdatedInvoke(step, string.Format("Thread:{0} Failed Captured image: {1}, LED Failed: {2}", Thread.CurrentThread.Name, imagename, _LEDFailure), true);
                                        if (_FailedImage++ > 5)
                                        {
                                            ErrorMessage = "Failed Capture 5 Images";
                                            throw new Exception(ErrorMessage);
                                        }
                                    }
                                }
                                else //simulation
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format("Thread:{0} Saved image (simulation): {1}", Thread.CurrentThread.Name, imagename), false);

                                }
                                //zPosc = zPosc + 0.5;
                                //_MotionController.AbsoluteMoveZStage(zPosc,
                                //    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed,
                                //    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel,
                                //    true);
                                //Thread.Sleep(10);   // wait for stage stop moving
                            }
                        }
                    }
                    _Camera.OnExposureChanged -= _Camera_OnExposureChanged;
                }
            }
            if (!IsSimulationMode)
            {
                _MotionController.AbsoluteMove(MotionTypes.YStage,
                        Ystartpos,
                        Ystartspeed,
                        (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]),
                        false
                        );
            }
            else
            {
                Thread.Sleep(200);
            }

            #region Online Image Analysis
            if (!Recipe.RecipeName.Contains("_CL"))
                RunOLAJobManager(_loopCount, step, false);
            #endregion Online Image Analysis
        }

        private void CaptureImage(ImagingStep step, ref Int32Rect roi, ref int tryCounts)
        {
            try
            {
                NullImageCounts = 0;
                BadImageCounts = 0;
                _MainBoard.SetLEDIntensity(_ImageSetting.LED, _ImageSetting.LedIntensity);
                _ledStateGet = false;
                do
                {
                    double ccdExposure = _ImageSetting.Exposure;
                    if (_IsBadImage)
                    {
                        ccdExposure = 0.2;
                    }

                    _Camera.GrabImage(ccdExposure, CaptureFrameType.Normal, ref _CapturedImage);

                    if (_CapturedImage != null)
                    {
                        _IsBadImage = Sequlite.Image.Processing.BadImageIdentifier.IsBadImage(_CapturedImage);
                    }

                    if (_IsBadImage || _CapturedImage == null || !_ledStateGet)
                    {
                        if (_IsBadImage)
                        {
                            BadImageCounts++;
                        }
                        else
                        {
                            NullImageCounts++;
                        }

                        tryCounts += 1;
                        if (BadImageCounts == 2)
                        {
                            _Camera.Close();
                            Thread.Sleep(1500);
                            _Camera.Open();
                            Thread.Sleep(100);

                            #region Camera setup
                            //Set binning mode
                            _Camera.HBin = _ImageSetting.BinningMode;
                            _Camera.VBin = _ImageSetting.BinningMode;
                            //Set CCD readout speed (0: Normal, 1: Fast)
                            _Camera.ReadoutSpeed = _ImageSetting.ReadoutSpeed;
                            //Set gain
                            _Camera.Gain = _ImageSetting.AdGain;
                            //Set region of interest
                            if (roi.Width > 0 && roi.Height > 0 && !_ImageSetting.IsCaptureFullRoi)
                            {
                                _Camera.RoiStartX = (ushort)roi.X;
                                _Camera.RoiWidth = (ushort)(roi.Width);
                                _Camera.RoiStartY = (ushort)roi.Y;
                                _Camera.RoiHeight = (ushort)(roi.Height);
                            }
                            else
                            {
                                _Camera.RoiStartX = 0;
                                _Camera.RoiStartY = 0;
                                _Camera.RoiWidth = _Camera.ImagingColumns / _Camera.HBin - 1;
                                _Camera.RoiHeight = _Camera.ImagingRows / _Camera.VBin - 1;
                            }
                            #endregion Camera setup
                        }
                        if (tryCounts > 10)
                        {
                            OnStepRunUpdatedInvoke(step, string.Format("Failed to capture, skip this image, trycounts:{0}, BadCounts:{1}, NullCounts{2}.", tryCounts, BadImageCounts, NullImageCounts), true);
                            _CapturedImage = null;
                            break;
                        }
                    }
                }
                while (_IsBadImage || _CapturedImage == null || !_ledStateGet);
                if (_CapturedImage != null)
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
                    _Camera.StopCapture();
                    throw new Exception(ex.ToString());
                }
            }
            finally
            {
                #region double check led status
                _MainBoard.GetLEDStatus(_ImageSetting.LED);
                int trycount = 0;
                bool _isLEDon = true;
                switch (_ImageSetting.LED)
                {
                    case LEDTypes.Green:
                        _isLEDon = _MainBoard.IsGLEDOn;
                        break;
                    case LEDTypes.Red:
                        _isLEDon = _MainBoard.IsRLEDOn;
                        break;
                    case LEDTypes.White:
                        _isLEDon = _MainBoard.IsWLEDOn;
                        break;
                }
                if (_isLEDon)
                {
                    do
                    {
                        if (trycount++ > 5)
                        {
                            ExitStat = ThreadExitStat.Error;
                            ErrorMessage = "Failed to turn off LED";
                            throw new Exception(ErrorMessage);
                        }
                        _MainBoard.SetLEDStatus(_ImageSetting.LED, false);
                        _MainBoard.GetLEDStatus(_ImageSetting.LED);
                        switch (_ImageSetting.LED)
                        {
                            case LEDTypes.Green:
                                _isLEDon = _MainBoard.IsGLEDOn;
                                break;
                            case LEDTypes.Red:
                                _isLEDon = _MainBoard.IsRLEDOn;
                                break;
                            case LEDTypes.White:
                                _isLEDon = _MainBoard.IsWLEDOn;
                                break;
                        }
                    }
                    while (_isLEDon);
                }
                #endregion double check led status
                _PDTimer.Dispose();
            }
        }
        private void _Camera_OnExposureChanged(bool starts)
        {
            _ledStateGet = false;
            _IsFailedtoSetLED = false;
            if (starts && !_IsBadImage)
            {
                _ledStateGet = true;
                _PDTimer = new System.Timers.Timer();
                _PDTimer.Interval = 200;
                _PDTimer.AutoReset = false;
                _PDTimer.Elapsed += _PDTimer_Elapsed;
                _MainBoard.SetLEDStatus(_ImageSetting.LED, true);
                _PDTimer.Start();
            }
        }

        private void AutoFocusProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            //_IsAutoFocusing = false;
            if (exitState == ThreadExitStat.None)
            {
                _IsAutoFocusingSucceeded = true;
                _FocusedSharpness = _AutoFocusProcess.FoucsedSharpness;
                if (!IsSimulationMode)
                {
                    if (_FocusedSharpness < 100)
                    {
                        _IsAutoFocusingSucceeded = false;
                        _AutoFocusErrorMessage = "Sharpness too low";
                    }
                }
                _AutoFocustrycount = _AutoFocusProcess.TryCounts;

            }
            else
            {
                _IsAutoFocusingSucceeded = false;
                _IsFailedCaptureAImage = _AutoFocusProcess.IsFailedCaptureImage;
                _FocusedSharpness = _AutoFocusProcess.FoucsedSharpness;
                _IsFailedtoSetALED = _AutoFocusProcess.IsFailedToSetLED;
                _AutoFocustrycount = _AutoFocusProcess.TryCounts;
                _AutoFocusErrorMessage = _AutoFocusProcess.ExceptionMessage;
            }
            _FilterFail = _AutoFocusProcess.Filterfail;
            _AutoFocusProcess.Completed -= AutoFocusProcess_Completed;
            _AutoFocusProcess = null;
        }
        private void _AutoFocusFluoProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                Logger.LogMessage("AF on Fluo Successed");
                _IsOffsetCalSucc = true;
                _CalculatedOffset = _AutoFocusFluoProcess.Offset;
            }
            else if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _IsOffsetCalSucc = false;
                Logger.LogWarning("Auto focusing on Fluo aborted.");
            }
            else
            {
                _IsOffsetCalSucc = false;
                Logger.LogError(string.Format("Auto focusing on Fluo failed. Exception:{0}, LED Failure:{1}, Failed to Capture Image{2}", _AutoFocusFluoProcess.ExceptionMessage, _AutoFocusFluoProcess.IsFailedToSetLED, _AutoFocusFluoProcess.IsFailedCaptureImage));
            }
            _AutoFocusFluoProcess.Completed -= _AutoFocusFluoProcess_Completed;
            _AutoFocusFluoProcess = null;
        }
        private void RunStepProc(MoveStageStep step)
        {
            int targetPos = (int)Math.Round((SettingsManager.ConfigSettings.YStageRegionPositions[step.Region] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
            _MotionController.AbsoluteMove(MotionTypes.YStage,
                targetPos,
                speed,
                (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])),
                true
                );
        }
        private void RunStepProc(RunRecipeStep step)
        {
            int startInc = _StartInc;
            if (_CurrentTree.Parent != null)
            {
                if (_CurrentTree.Parent.Step.StepType == RecipeStepTypes.Loop)
                {
                    startInc = ((LoopStep)_CurrentTree.Parent.Step).LoopCounts + _StartInc ;
                }
                if (((LoopStep)_CurrentTree.Parent.Step).LoopCounts == 1)
                {
                    _IncCount = _LastLoopIncCount;
                }
                if (_IncCount != 0)
                {
                    startInc = startInc + _IncCount - _StartInc + 1;
                }
                if (((LoopStep)_CurrentTree.Parent.Step).LoopCounts == ((LoopStep)_CurrentTree.Parent.Step).LoopCycles)
                {
                    _LastLoopIncCount = startInc - 1;
                }
            }
            else
            {
                startInc = _StartInc;
            }

            string recipePath = CheckInnerRecipePath(step);//.RecipePath;
            _RecipeParameters.StartInc = startInc;
            Logger.Log($"Loading inner recipe from {recipePath}");
            Recipe recipe = Recipe.LoadFromXmlFile(recipePath);
            //Recipe recipe = Recipe.LoadFromXmlFile(step.RecipePath);
            _InnerRecipeRunThread = new RecipeRunThread(_CallingDispatcher, RecipeRunConfig, recipe, _Camera, _MotionController, _MainBoard, FluidicsInterface,
                _RecipeParameters, this, OLAJobs, false);
            _InnerRecipeRunThread.OnRecipeRunUpdated += _InnerRecipeRunThread_OnRecipeRunUpdated;
            _InnerRecipeRunThread.OnStepRunUpdated += _InnerRecipeRunThread_OnStepRunUpdated;
            _InnerRecipeRunThread.OnLoopStepUpdated += _InnerRecipeRunThread_OnLoopStepUpdated;
            _InnerRecipeRunThread.Completed += _InnerRecipeRunThread_Completed;
            _InnerRecipeRunThread.Name = "Inner Recipe";
            _InnerRecipeRunThread.IsSimulationMode = IsSimulationMode;
            _InnerRecipeRunThread.IsEnablePP = IsEnablePP;
            //_InnerRecipeRunThread.RootRecipeThread = this.RootRecipeThread != null ? this.RootRecipeThread : this;
            _InnerRecipeRunThread.Start();
            //IsInnerRecipeRunning = true;
            _InnerRecipeRunThread.Join();
            OnStepRunUpdatedInvoke(step, string.Format("Thread:{0}, Update Recipe", Thread.CurrentThread.Name), false);
            //why need ave recipe here ??
            Logger.Log($"Save inner recipe {recipePath}");
            Recipe.SaveToXmlFile(recipe, recipePath);// step.RecipePath);
            while (_InnerRecipeRunThread != null)
            {
                Thread.Sleep(100);
            }
            //IsInnerRecipeRunning = false;
        }

        //private void _InnerRecipeRunThread_OnLoopStepUpdated(RecipeStepBase step)
        //{
        //    OnLoopStepUpdatedInvoke(step);
        //}

        //private void _InnerRecipeRunThread_Completed(ThreadBase sender, ThreadExitStat exitState)
        //{
        //    _InnerRecipeRunThread.OnStepRunUpdated -= _InnerRecipeRunThread_OnStepRunUpdated;
        //    _InnerRecipeRunThread.OnLoopStepUpdated -= _InnerRecipeRunThread_OnLoopStepUpdated;
        //    _InnerRecipeRunThread.Completed -= _InnerRecipeRunThread_Completed;
        //    _InnerRecipeRunThread = null;
        //}

        //private void _InnerRecipeRunThread_OnStepRunUpdated(RecipeStepBase step, string msg, bool isError)
        //{
        //    OnStepRunUpdatedInvoke(step, msg, isError);
        //}

        /// <summary>
        /// Wait for the specified amount of time for wait command
        /// </summary>
        /// <param name="step"></param>
        //private void RunStepProc(WaitingStep step)
        //{
        //    Stopwatch stopwatch = new Stopwatch();
        //    stopwatch.Start();
        //    var startTimeSpan = TimeSpan.FromMilliseconds(0);
        //    var periodTimeSpan = TimeSpan.FromSeconds(30);
        //    using (var timer = new System.Threading.Timer(e => OnStepRunUpdatedInvoke(step, String.Format("Thread:{1}: {0}", "Waiting", Thread.CurrentThread.Name), false), null, startTimeSpan, periodTimeSpan))
        //    {
        //        // do not wait if in simulation mode
        //        if (step.Time > 1000 && !IsSimulationMode)
        //            Thread.Sleep(step.Time * 1000 - 100); // sleep most of the time
        //        while (stopwatch.ElapsedMilliseconds < step.Time * 1000 && !IsSimulationMode)
        //        {
        //            if (IsAbort)
        //            {
        //                OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
        //                break;
        //            }
        //            Thread.Sleep(1); // we are close now, so check frequently
        //        }
        //        stopwatch.Stop();
        //        startTimeSpan = TimeSpan.FromMilliseconds(-1);
        //        //Thread.Sleep(50); // KBH removed - not sure of the purpose of an extra 50 ms sleep
        //    }
        //}

        //private void RunStepProc(CommentStep step)
        //{

        //}
        #endregion Run Step Functions
    }
}
