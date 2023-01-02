using Sequlite.ALF.Common;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media.Imaging;
using static Sequlite.ALF.Common.ThreadBase;

namespace Sequlite.ALF.App
{
    class SeqAppSystemCheck : ISystemCheck
    {
        public bool IsAbortCheck { get; set; }
        public double DiskSpaceReq { get; set; }
        public double WasterLevel { get; set; }
        public long DiskSpaceEmp { get; set; }
        //private string PrimingRecipeDir;
        private LEDController _LEDController = LEDController.GetInstance();
        private RecipeRunThreadV2 _RecipeRunThreadV2_Check;
        private bool _IsPrimingSuccess;
        private bool IsSimulationMode { get; }
        private bool _FlowCheck_RunPuming_OK;
        private System.Timers.Timer _LogTimer;
        int[] _progressLock = new int[0];
        //private float _OverallProgress;
        private float _TempCheckProgress;
        private float _FludicsCheckProgress;
        private float _FlowCheckProgress;
        private float _ImagingCheckProgress;
        private float _DiskSpaceCheckProgress;
        private float _DoorClosedCheckProgress;
        private List<double> _PressureList = new List<double>();
        private List<double> _FlowrateList = new List<double>();
        private AutoFocusCommand2 _AutoFocusProcess_check;
        private CoarseAutoFocusCommand _CoarseAutoFocusProcess_check;
        private bool _IsAFSuccess;
        private string _AutoFocusErrorMessage;
        private double _FocusedSharpness;
        private double _TopSurfaceSharpness;
        private double _BottomSurfaceSharpness;
        double FocusedBottomPos { get; set; }
        double FocusedTopPos { get; set; }
        private double _CalibPressure;
        private double _Reference0 = SettingsManager.ConfigSettings.AutoFocusingSettings.Reference0;

        RecipeBuildSettings _RecipeBuildSettings;
        SeqApp SeqApp { get; }
        public SeqAppSystemCheck(SeqApp seqApp)
        {
            SeqApp = seqApp;
            IsSimulationMode = seqApp.IsSimulation;
            _RecipeBuildSettings = SettingsManager.ConfigSettings.SystemConfig.RecipeBuildConfig;
        }

        public HardwareCheckResults HardwareCheckResults
        {
            get
            {
                return new HardwareCheckResults()
                {
                    FocusedBottomPos = this.FocusedBottomPos,
                    FocusedTopPos = this.FocusedTopPos,
                    DiskSpaceEmp = this.DiskSpaceEmp,
                    DiskSpaceReq = this.DiskSpaceReq,
                    WasteLevel = this.WasterLevel
                };
            }
        }

        float OverallProgress
        {
            get
            {
                lock (_progressLock)
                {
                    return _TempCheckProgress + _FludicsCheckProgress + _ImagingCheckProgress + _DiskSpaceCheckProgress + _DoorClosedCheckProgress + _FlowCheckProgress;
                }
            }
        }

        public string SessionId { get; set; }
        void ResetProgress(ref float whichProgress)
        {
            lock (_progressLock)
            {
                whichProgress = 0;
            }
        }

        void UpdateProgress(ref float whichProgress, float addProgress, float maxProgress)
        {
            lock (_progressLock)
            {
                whichProgress += addProgress;
                if (whichProgress > maxProgress)
                {
                    whichProgress = maxProgress;
                }
            }
        }

        void UpdateCheckHardwareProgress(CheckHardwareEnum checkType, CheckHardwareProgressMessageEnum msgType, float progressDelts)
        {
            switch (checkType)
            {
                case CheckHardwareEnum.Door:
                    UpdateProgress(ref _DoorClosedCheckProgress, progressDelts, 5);
                    break;

                case CheckHardwareEnum.Fluidics:
                    UpdateProgress(ref _FludicsCheckProgress, progressDelts, 5);
                    break;
                case CheckHardwareEnum.Sensor:
                case CheckHardwareEnum.Flow:
                    UpdateProgress(ref _FludicsCheckProgress, progressDelts, 25);
                    break;

                case CheckHardwareEnum.Temperature:
                    UpdateProgress(ref _TempCheckProgress, progressDelts, 30);
                    break;

                case CheckHardwareEnum.Imaging:
                    UpdateProgress(ref _ImagingCheckProgress, progressDelts, 30);
                    break;

                case CheckHardwareEnum.Disk:
                    UpdateProgress(ref _DiskSpaceCheckProgress, progressDelts, 5);
                    break;
            }

            SeqApp.Send(SeqApp.ObservableAppMessage, new AppMessage()
            {
                MessageObject = new CheckHardwareProgressMessage()
                {
                    CheckType = checkType,
                    MessageType = msgType,
                    ProgressPercentage = OverallProgress
                },
                MessageType = AppMessageTypeEnum.Normal
            });

        }
        public bool DoorCheck()
        {
            //Door Check
            ResetProgress(ref _DoorClosedCheckProgress);
            bool b = true;
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Door, CheckHardwareProgressMessageEnum.Start, progressDelta);
            //FC door
            try
            {
                if (IsSimulationMode)
                {
                    SeqApp.UpdateAppMessage("Sim: close FC door");
                    Thread.Sleep(500);
                    b = true;
                }
                else
                {
#if !DisableFCDoor
                    MainBoardController _MBController = MainBoardController.GetInstance();
                    if (_MBController.IsProtocolRev2)
                    {
                        MotionControl.MotionController motionController = MotionControl.MotionController.GetInstance();
                        if (!motionController.GetFCDoorStatus())
                        {
                            Thread.Sleep(100);
                            if (!motionController.GetFCDoorStatus())
                            {
                                b = false;
                                SeqApp.NotifyError("Failed to check FC door status");
                            }
                        }
                        if (motionController.FCDoorIsOpen == true)
                        {
                            SeqApp.NotifyNormalError("Please close the FC door, closing");
                            if (!motionController.SetFCDoorStatus(false))
                            {
                                Thread.Sleep(100);
                                if (!motionController.SetFCDoorStatus(false))
                                {
                                    SeqApp.NotifyError("Failed to close door");
                                    b = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_MBController.HWVersion == "2.0.0.1")
                        {
                            if (!_MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1))
                            {
                                if (!_MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1))
                                {
                                    b = false;
                                    SeqApp.NotifyError("Failed to check FC door status");
                                }
                            }
                            if (!_MBController.FCDoorStatus)
                            {
                                SeqApp.NotifyNormalError("Please close the FC door, closing");
                                if (!_MBController.SetDoorStatus(false))
                                {
                                    Thread.Sleep(100);
                                    if (!_MBController.SetDoorStatus(false))
                                    {
                                        SeqApp.NotifyError("Failed to close door");
                                        b = false;
                                    }
                                }
                            }
                        }
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to check if the FC door is closed with error: {ex.Message}. ");
                b = false;
            }
            //Reagent door

            try
            {
                if (IsSimulationMode)
                {
                    SeqApp.UpdateAppMessage("Sim: Check if Reagent door is closed");
                }
                else
                {
                    Chiller _Chiller = Chiller.GetInstance();
                    _Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
                    if (!_Chiller.CartridgeDoor && MotionControl.MotionController.GetInstance().IsReagentDoorEnabled) // is closed
                    {
#if !DisableReagentDoor
                        SeqApp.NotifyNormalError("Please close the reagent door");
                        b = false;
#endif
                    }
                    else
                    {
                        b = true;
                    }
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to check if the reagent door is closed with error: {ex.Message}. ");
                b = false;
            }
            UpdateCheckHardwareProgress(CheckHardwareEnum.Door,
               b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                (b ? progressDelta : 0));
            return b;
        }

        public bool FluidicsCheck()
        {
            //FC, Reagent cartridge, buffer, waste check
            //Check FC Clamp
            ResetProgress(ref _FludicsCheckProgress);
            int progressDelta = 1;
            bool b = true;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Start, progressDelta);
            try
            {
                if (IsSimulationMode)
                {
                    SeqApp.UpdateAppMessage("Sim: Check if FC door is loaded and clamped");
                }
                else // FC clamp 
                {
                    MainBoardController _MBController = MainBoardController.GetInstance();
                    _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                    if (_MBController.FCClampStatus)
                    {
                        SeqApp.NotifyNormalError("Please load FC and clamp it");
                        b = false;
                    }
                    //else
                    //{
                    //    UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    //}
                }
                // reagent sensor
                if (b)
                {
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);

                    // Reagent Cartridge
                    if (IsSimulationMode)
                    {
                        SeqApp.UpdateAppMessage("Sim: Check if Reagent Cartridge is presented");
                    }
                    else
                    {
                        Chiller _Chiller = Chiller.GetInstance();
                        if (!_Chiller.CartridgePresent)
                        {
                            SeqApp.NotifyNormalError("Please push back the reagent more");
                            b = false;
                        }
                    }
                    //UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                }

                // Reagent sipper
                if (b)
                {
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);

                    if (IsSimulationMode)
                    {
                        SeqApp.UpdateAppMessage("Sim: Positioning Cartridge");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        if (!SeqApp.MotionController.IsCartridgeAvailable) //Sipper controller with chiller board
                        {
                            if (!Chiller.GetInstance().CheckCartridgeSippersReagentPos())
                            {
                                if (Chiller.GetInstance().SetChillerMotorAbsMove(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos) == false)
                                {
                                    SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
                                    b = false;
                                }
                                int retry = 0;
                                Chiller.GetInstance().GetChillerMotorPos();
                                while (!Chiller.GetInstance().CheckCartridgeSippersReagentPos())
                                {
                                    if (++retry > 140)
                                    {
                                        SeqApp.UpdateAppErrorMessage("Load cartridge timeout.");
                                        b = false;
                                        break;
                                    }
                                    Thread.Sleep(1000);
                                }
                            }
                        }
                        else //sipper control with motion board
                        {
                            if (SeqApp.MotionController.CCurrentPos != (int)Math.Round(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]))
                            {
                                int pos = (int)Math.Round(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                                int speed = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                                int accel = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                                if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                                {
                                    SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
                                    b = false;
                                }
                            }
                        }
                    }
                }

                // Buffer cartridge and sipper check

                if (b)
                {
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);

                    if (IsSimulationMode)
                    {
                        SeqApp.UpdateAppMessage("Sim: buffer cartridge is loaded and sipper is lowered");
                    }
                    else
                    {
                        FluidController _FluidController = FluidController.GetInstance();
                        _FluidController.ReadRegisters(FluidController.Registers.BufferLevel, 5);
                        bool bufferin = _FluidController.BufferTrayIn;
                        bool sipperdown = _FluidController.SipperDown;
                        if (bufferin || sipperdown)
                        {
                            b = false;
                            string error = string.Empty;
                            if (bufferin)
                            {
                                error = "Please load the buffer. ";

                            }
                            if (sipperdown)
                            {
                                error += "Please lower the sipper.";

                            }
                            SeqApp.NotifyNormalError(error);
                        }
                    }
                }
                if (b)
                {
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);

                    if (IsSimulationMode)
                    {
                        SeqApp.UpdateAppMessage("Sim: waste level checked");
                        WasterLevel = 0;
                    }
                    else
                    {
                        //Waste check
                        if (MainBoardController.GetInstance().HWVersion != "2.0.0.0" && MainBoardController.GetInstance().HWVersion != "2.0.0.1" && MainBoardController.GetInstance().HWVersion != "2.0.0.2")
                        {
                            WasterLevel = -1;
                            if (FluidController.GetInstance().ReadMassOfWaste())
                            {
                                WasterLevel = FluidController.GetInstance().MassOfWaste;
                            }
                            else
                            {
                                if (FluidController.GetInstance().ReadMassOfWaste())
                                {
                                    WasterLevel = FluidController.GetInstance().MassOfWaste;
                                }
                                else
                                {
                                    SeqApp.NotifyError("Failed to read mass of waste!");
                                }
                            }
                            //if (WasterLevel > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WasteCartridgeEmptyWeight)
                            //{
                            //    SeqApp.NotifyError("Please empty waste");
                            //}
                            //else if (WasterLevel < 0)
                            //{
                            //    SeqApp.NotifyError("Please place a waste container");
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to check fluidics system with error: {ex.Message}.");
                b = false;
            }

            UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics,
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                progressDelta);
            return b;
        }

        public bool FlowCheckAndPriming()
        {
            ResetProgress(ref _FlowCheckProgress);
            int progressDelta = 1;
            bool b = true;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Flow, CheckHardwareProgressMessageEnum.Start, progressDelta);
            //Priming
            try
            {
                UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                if (IsSimulationMode)
                {
                    SeqApp.UpdateAppMessage("Sim: Primed and flow check passed");
                }
                else
                {
                    if (MotionControl.MotionController.GetInstance().IsFluidicCheckEnabled)
                    {
                        b = SeprePriming();
                        if (b)
                        {
                            List<PathOptions> testpath = new List<PathOptions>();
                            foreach (PathOptions pullpath in (PathOptions[])Enum.GetValues(typeof(PathOptions)))
                            {
                                if (pullpath.ToString().Contains("Test") || pullpath.ToString().Contains("TestBypass1"))
                                {
                                    testpath.Add(pullpath);
                                }
                            }
                            b = FlowCheck(SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos, testpath);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                SeqApp.NotifyError(ex.ToString());
                b = false;
            }
            UpdateCheckHardwareProgress(CheckHardwareEnum.Flow, 
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                progressDelta);
            return b;
        }
        public bool TemperatureCheck()
        {
            //return true; //< dssable temperature check

            //FC Temp check
            ResetProgress(ref _TempCheckProgress);
            bool b = true;
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Temperature, CheckHardwareProgressMessageEnum.Start, 0);

            try
            {
                if (IsSimulationMode)
                {
                    int waitMS = 100;
                    int totalWaitMS = 2 * 10000;
                    int totalCount = totalWaitMS / waitMS;
                    int count = 0;
                    SeqApp.UpdateAppMessage("Sim: Checking temperatures");
                    while (count < totalCount)
                    {
                        //Thread.Sleep(10000);
                        if (IsAbortCheck)
                        {
                            break;
                        }
                        if (count % 10 == 0)
                        {
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Temperature, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        Thread.Sleep(waitMS);
                        count++;
                    }
                }
                else
                {
                    //
                    if (b && Chiller.GetInstance().IsProtocolRev2)// check water cooling unit liquid level
                    {
                        if (Chiller.GetInstance().GetCoolingLiquidLevel())
                        {
                            if(!Chiller.GetInstance().CoolingLiquidIsFull)
                            {
                                SeqApp.UpdateAppErrorMessage("cooling liquid level error");
                                b = false;
                            }
                        }
                    }
                    //
                    var _FCTemperControllerRev2 = TemperatureController.GetInstance();
                    _FCTemperControllerRev2.GetTemper();
                    double crntFCTemper = _FCTemperControllerRev2.CurrentTemper;
                    double detaTemper = 10;
                    if (crntFCTemper > 50)
                    {
                        detaTemper = -10;
                    }
                    double tgtFCTemper = crntFCTemper + detaTemper;
                    //_FCTemperControllerRev2.TemperControl(tgtFCTemper, 0);
                    if (!_FCTemperControllerRev2.SetTemperature(tgtFCTemper, 0))
                    {
                        Thread.Sleep(100);
                        if (!_FCTemperControllerRev2.SetTemperature(tgtFCTemper, 0))
                        {
                            SeqApp.UpdateAppErrorMessage("Failed to set temp.");
                            b = false;
                        }
                    }
                    int waitMS = 100;
                    int totalWaitMS = 10000;
                    int totalCount = totalWaitMS / waitMS;
                    int count = 0;
                    while (count < totalCount)
                    {
                        //Thread.Sleep(10000);
                        if (IsAbortCheck)
                        {
                            break;
                        }
                        if (count % 10 == 0)
                        {
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Temperature, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        Thread.Sleep(waitMS);
                        count++;
                    }
                    if (!_FCTemperControllerRev2.GetTemper())
                    {
                        Thread.Sleep(100);
                        if (!_FCTemperControllerRev2.GetTemper())
                        {
                            SeqApp.UpdateAppErrorMessage("Failed to get temp.");
                            b = false;
                        }
                    }
                    if (detaTemper > 0)
                    {
                        if (_FCTemperControllerRev2.CurrentTemper < crntFCTemper + detaTemper / 2)
                        {
                            SeqApp.UpdateAppErrorMessage("Heating failed.");
                            b = false;
                        }
                    }
                    else
                    {
                        if (_FCTemperControllerRev2.CurrentTemper > crntFCTemper + detaTemper * 0.9)
                        {
                            SeqApp.UpdateAppErrorMessage("Cooling failed.");
                            b = false;
                        }
                    }

                    if (b)
                    {
                        if (!_FCTemperControllerRev2.SetTemperature(crntFCTemper, 0))
                        {
                            Thread.Sleep(100);
                            if (!_FCTemperControllerRev2.SetTemperature(crntFCTemper, 0))
                            {
                                SeqApp.UpdateAppErrorMessage("Failed to set temp.");
                                b = false;
                            }
                        }
                        waitMS = 100;
                        totalWaitMS = 10000;
                        totalCount = totalWaitMS / waitMS;
                        count = 0;
                        while (count < totalCount)
                        {
                            //Thread.Sleep(10000);
                            if (IsAbortCheck)
                            {
                                break;
                            }
                            if (count % 10 == 0)
                            {
                                UpdateCheckHardwareProgress(CheckHardwareEnum.Temperature, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                            }
                            Thread.Sleep(waitMS);
                            count++;
                        }
                        if (!_FCTemperControllerRev2.GetTemper())
                        {
                            Thread.Sleep(100);
                            if (!_FCTemperControllerRev2.GetTemper())
                            {
                                SeqApp.UpdateAppErrorMessage("Failed to get temp.");
                                b = false;
                            }
                        }
                        if (detaTemper > 0)
                        {
                            if (_FCTemperControllerRev2.CurrentTemper > crntFCTemper + detaTemper * 0.9)
                            {
                                SeqApp.UpdateAppErrorMessage("Cooling failed.");
                                b = false;
                            }
                        }
                        else
                        {
                            if (_FCTemperControllerRev2.CurrentTemper < crntFCTemper + detaTemper / 2)
                            {
                                SeqApp.UpdateAppErrorMessage("Heating failed.");
                                b = false;
                            }
                        }
                        if (!_FCTemperControllerRev2.SetTemperature(25, 0))
                        {
                            Thread.Sleep(100);
                            if (!_FCTemperControllerRev2.SetTemperature(25, 0))
                            {
                                SeqApp.UpdateAppErrorMessage("Set to imaging temp failed.");
                                b = false;
                            }
                        }
                    }
                }//if !Simulation

                if (b)
                {
                    if (IsSimulationMode)
                    {
                        SeqApp.UpdateAppMessage("Sim: Check Chiller temperatures");
                    }
                    else
                    {
                        //Chiller Temp check
                        Chiller _Chiller = Chiller.GetInstance();
                        if (_Chiller.ReadRegisters(Chiller.Registers.ChillerTemper, 1))
                        {
                            if (Math.Abs(_Chiller.ChillerTemper - 4) > 5)
                            {
                                SeqApp.Logger.Log("chiller temperature above ");
                                SeqApp.NotifyNormalError("chiller cooling, please wait");
                                b = false;
                            }
                        }
                        else
                        {
                            SeqApp.UpdateAppErrorMessage("Failed to get chiller temperature.");
                            b = false;
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to do check temperatures with error: {ex.Message}.");
                b = false;
            }
            UpdateCheckHardwareProgress(CheckHardwareEnum.Temperature,
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                b ? progressDelta : 0);
            return b;
        }

        private bool FCRegionStageCheck(RegionIndex FClocation)
        {
            bool b = true;

            int YtargetPos = (int)Math.Round(SettingsManager.ConfigSettings.StageRegionMaps[FClocation][1] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
            int XtargetPos = (int)Math.Round(SettingsManager.ConfigSettings.StageRegionMaps[FClocation][0] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
            int Yspeed = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
            int Xspeed = (int)Math.Round(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);

            if (IsSimulationMode)
            {
                SeqApp.UpdateAppMessage("Sim: Moving stage /X/Y to the region");
                Thread.Sleep(1000);
            }
            else
            {
                if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, Yspeed,
                            (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true) ||
                        // move X stage
                        !SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, XtargetPos, Xspeed,
                            (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), true))
                {
                    //SeqApp.NotifyNormalError("Failed to move X/Y stage, retry");
                    //if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, Yspeed,
                    //(int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true) ||
                    //!SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, XtargetPos, Xspeed,
                    //(int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), true))
                    //{
                    SeqApp.NotifyNormalError("Failed to move X/Y stage, Recipe stop");
                    b = false;
                    //}
                }
            }

            return b;
        }
        public bool ImageSystemCheck()
        {
            //Image System check
            ResetProgress(ref _ImagingCheckProgress);
            bool b = true;
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Start, progressDelta);
            if (!IsSimulationMode && !TemperatureController.GetInstance().SetTemperature(37, 0))
            {
                Thread.Sleep(100);
                if (!TemperatureController.GetInstance().SetTemperature(37, 0))
                {
                    SeqApp.UpdateAppErrorMessage("Set to imaging temp failed.");
                    b = false;
                }
            }

            try
            {
                if (IsSimulationMode)
                {
                    SeqApp.UpdateAppMessage("Sim: checking LED controller.");
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    Thread.Sleep(500);
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                }
                else
                {
#region LED/PD
                    LEDController _LEDController = LEDController.GetInstance();
                    if (!_LEDController.IsConnected)
                    {
                        SeqApp.UpdateAppErrorMessage("LED Controller is not connected yet.");
                        b = false;
                    }
                    else
                    {
                        UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);

                        int gPDValue = 0, rPDValue = 0;
                        _LEDController.SetLEDIntensity(LEDTypes.Green, 50);
                        _LEDController.SetLEDStatus(LEDTypes.Green, true);
                        if (_LEDController.GetPDValue())
                        {
                            gPDValue = _LEDController.PDValue;
                        }
                        _LEDController.SetLEDStatus(LEDTypes.Green, false);

                        _LEDController.SetLEDIntensity(LEDTypes.Red, 50);
                        _LEDController.SetLEDStatus(LEDTypes.Red, true);
                        if (_LEDController.GetPDValue())
                        {
                            rPDValue = _LEDController.PDValue;
                        }
                        _LEDController.SetLEDStatus(LEDTypes.Red, false);
                        double greenMin = SettingsManager.ConfigSettings.CameraCalibSettings.GreenPDMinCount;
                        double redMin = SettingsManager.ConfigSettings.CameraCalibSettings.RedPDMinCount;
                        if (gPDValue > greenMin 
                            && rPDValue > redMin)
                        {
                            //event fire, update GUI
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            //event fire, update GUI
                            SeqApp.UpdateAppErrorMessage($"Imaging system: failed to pass LED intensity check. Green value:{gPDValue} (min value: {greenMin}) Red value{rPDValue} (min value: {redMin})");
                            b = false;
                        }
                        //CurrentTestItem.Comments = string.Format("PD Value when Green, Red, White LED on:{0},{1},{2}", gPDValue, rPDValue, wPDValue);
                    } //!IsSimulationMode
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to check LED controller with error: {ex.Message}.");
                b = false;
            }

            if (IsAbortCheck)
            {
                b = false;
            }

#endregion LED/PD
#region Capture Image
            if (b)
            {
                if (!IsSimulationMode)
                {
                    if (SeqApp.EthernetCameraA == null)
                    {
                        SeqApp.UpdateAppErrorMessage("None camera found.");
                        b = false;
                    }
                    else if (SeqApp.EthernetCameraB == null)
                    {
                        SeqApp.UpdateAppErrorMessage("Only 1 camera connected.");
                        b = false;
                    }
                }

                if (b)
                {
                    UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    try
                    {
                        if (IsSimulationMode)
                        {
                            SeqApp.UpdateAppMessage("Sim: Checking Camera-A imaging");
                            Thread.Sleep(1000);
                        }
                        else
                        {
                            WriteableBitmap imageA = null;
                            SeqApp.EthernetCameraA.ADCBitDepth = 8;
                            SeqApp.EthernetCameraA.EnableTriggerMode = false;

                            SeqApp.EthernetCameraA.GrabImage(0.1, CaptureFrameType.Normal, ref imageA);
                            if (imageA == null)
                            {
                                SeqApp.UpdateAppErrorMessage(string.Format("Camera {0} failed to grab image.", SeqApp.EthernetCameraA.SerialNumber));
                                b = false;
                            }
                        }

                        if (b)
                        {
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                    }
                    catch (Exception ex)
                    {
                        SeqApp.UpdateAppErrorMessage(string.Format("Camera {0} failed to grab image due to: {1}", SeqApp.EthernetCameraA.SerialNumber, ex.Message));
                        b = false;
                    }
                    finally
                    {
                    }

                    if (IsAbortCheck)
                    {
                        b = false;
                    }

                    if (b)
                    {
                        try
                        {
                            if (IsSimulationMode)
                            {
                                SeqApp.UpdateAppMessage("Sim: Checking Camera-B imaging");
                                Thread.Sleep(1000);
                            }
                            else
                            {
                                WriteableBitmap imageB = null;
                                SeqApp.EthernetCameraB.ADCBitDepth = 8;
                                SeqApp.EthernetCameraB.EnableTriggerMode = false;

                                SeqApp.EthernetCameraB.GrabImage(0.1, CaptureFrameType.Normal, ref imageB);
                                if (imageB == null)
                                {
                                    SeqApp.UpdateAppErrorMessage(string.Format("Camera {0} failed to grab image.", SeqApp.EthernetCameraB.SerialNumber));
                                    b = false;
                                }
                            }
                            if (b)
                            {
                                UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                            }
                        }
                        catch (Exception ex)
                        {
                            SeqApp.UpdateAppErrorMessage(string.Format("Camera {0} failed to grab image due to: {1}", SeqApp.EthernetCameraB.SerialNumber, ex.Message));
                            b = false;
                        }
                        finally
                        {
                            //to do: UPDATE GUI 
                        }
                    }
                }
            }
#endregion Capture Image

#region Autofocus reference
            if (IsAbortCheck)
            {
                b = false;
            }

            //Move to last region then move to first region to check the FC region x y range;
            //region.Lane, region.Column, region.Row {1,1,1}
            RegionIndex FirstFClocation = new RegionIndex(new int[3] { 1, 1, 1 }); 
            RegionIndex LastFClocation = new RegionIndex(new int[3] { SettingsManager.ConfigSettings.CalibrationSettings.FCLane,
                                                                      SettingsManager.ConfigSettings.CalibrationSettings.FCColumn,
                                                                      SettingsManager.ConfigSettings.CalibrationSettings.FCRow });  

            b = FCRegionStageCheck(LastFClocation)&& FCRegionStageCheck(FirstFClocation);

            ILucidCamera AFcamera = null;
            if (!IsSimulationMode) { AFcamera = SeqApp.EthernetCameraA.Channels.Contains("2") ? SeqApp.EthernetCameraA : SeqApp.EthernetCameraB; }
 
#region Coarse Autofocus finding new Reference0
            /*  Coarse Autofocus to find new Reference0 at the beginning of each run. This is to overcome problems cuased by FC bottom glass thickness variation (+/-100um)
            //  If overall difference is bigger than above range, i.e caused by FC casing changes, coarse AF won't find new value. A manual Reference0 calibration is recommended.
            //  ThickVariation =200um, Max(ThinHeight)=230um, Max(ChannelHeight)=200um; use following equation to calculate ZstageLimitH and ZstageLimitL.
            //  ZstageLimitH = Reference0 + ThickVariation + Max(ThinHeight) + Tolerance;
            //  ZstageLimitL = Reference0 - ThickVariation - Max(ChannelHeight) - Tolerance
            */

            //if (b)
            //{ try
            //    {
            //        //Just Scan no HillClimb
            //        AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
            //        _AutoFocusSetting.ZRange = 900;
            //        _AutoFocusSetting.ZstageLimitH = _Reference0 + 465;
            //        _AutoFocusSetting.ZstageLimitL = _Reference0 - 435;
            //        _AutoFocusSetting.ScanInterval = 10; //coarse AF uses step interval 10um to speedup process
            //        _AutoFocusSetting.IsScanonly = true;

            //        _CoarseAutoFocusProcess_check = new CoarseAutoFocusCommand(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
            //        _CoarseAutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
            //        _CoarseAutoFocusProcess_check.Completed += CoarseAutoFocusProcess_check_Completed;
            //        _CoarseAutoFocusProcess_check.Name = "CoarseAutofocus";
            //        _CoarseAutoFocusProcess_check.Start();
            //        _CoarseAutoFocusProcess_check.Join();
            //        _CoarseAutoFocusProcess_check = null;

            //        if (_IsAFSuccess || IsSimulationMode)
            //        {
            //            FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
            //            _Reference0 = FocusedTopPos;
            //            SeqApp.UpdateAppMessage(string.Format("Coarse Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
            //            //UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
            //        }
            //        else
            //        {
            //            // retry
            //            SeqApp.UpdateAppWarningMessage(string.Format("Coarse AF failed at Top surface due to: {0}, retry", _AutoFocusErrorMessage));
            //        }

            //    }
            //    catch (Exception ex)
            //    {
            //        SeqApp.UpdateAppErrorMessage(string.Format("Coarse AF failed due to: {0}", ex.Message));
            //        b = false;
            //    }
            //}


#endregion


            //Top surface focus
            if (b)
            {
                try
                {
                    AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                    _AutoFocusSetting.ZRange = 60;
                    _AutoFocusSetting.ZstageLimitH = _Reference0 + _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ZstageLimitL = _Reference0 - _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ScanInterval = 3;
                    _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                    _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                    _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                    _AutoFocusProcess_check.Name = "Autofocus";
                    _AutoFocusProcess_check.Start();
                    _AutoFocusProcess_check.Join();
                    _AutoFocusProcess_check = null;
                    if (_IsAFSuccess || IsSimulationMode)
                    {
                        FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
                        _Reference0 = FocusedTopPos;
                        SeqApp.UpdateAppMessage(string.Format("Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
                        //UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    }
                    else
                    {
                        // retry
                        SeqApp.UpdateAppWarningMessage(string.Format("AF at Top surface failed due to: {0}, retry", _AutoFocusErrorMessage));
                        if (_AutoFocusErrorMessage != null)
                        {
                            if (_AutoFocusErrorMessage.Contains("Range")) //shift range if out of range
                            {
                                _Reference0 = SeqApp.MotionController.ZCurrentPos;
                            }
                            else if (_AutoFocusErrorMessage.Contains("Focused Sharpness higher than threshold"))
                            {
                                _Reference0 = SeqApp.MotionController.ZCurrentPos - SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness;
                            }
                        }
#region Push potential bubble
                        //OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                        //PumpingSettings _PumpSetting = new PumpingSettings();
                        //_PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
                        //_PumpSetting.PullRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
                        //_PumpSetting.PumpingVolume = 30;
                        //_PumpSetting.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                        //_PumpSetting.SelectedMode = ModeOptions.AspirateDispense;
                        //_PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = 22 };
                        //_PumpSetting.SelectedPullPath = PathOptions.FC;
                        //_PumpSetting.SelectedPushPath = PathOptions.Waste;
                        //_PumpSetting.SelectedPullValve2Pos = 6;
                        //_PumpSetting.SelectedPullValve3Pos = 1;
                        //_PumpSetting.SelectedPushValve2Pos = 6;
                        //_PumpSetting.SelectedPushValve3Pos = 1;
                        //for (int i = 0; i < 4; i++)
                        //{
                        //    if (i + 1 == region.Lane) { _PumpSetting.PumpPullingPaths[i] = true; } else { _PumpSetting.PumpPullingPaths[i] = false; }
                        //    _PumpSetting.PumpPushingPaths[i] = false;
                        //}
                        //FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                        //if (!IsSimulationMode)
                        //{
                        //    FluidicsInterface.WaitForPumpingCompleted();
                        //}

#endregion Push potential bubble
                        _AutoFocusSetting.ZstageLimitH = _Reference0 + _AutoFocusSetting.ZRange;
                        _AutoFocusSetting.ZstageLimitL = _Reference0 - _AutoFocusSetting.ZRange;
                        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                        //_IsAutoFocusing = true;
                        _AutoFocusProcess_check.Name = "Autofocus";
                        _AutoFocusProcess_check.Start();
                        _AutoFocusProcess_check.Join();
                        _AutoFocusProcess_check = null;
                        //while (_IsAutoFocusing) { Thread.Sleep(1); }
                        if (_IsAFSuccess)
                        {
                            FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
                            _Reference0 = FocusedTopPos;
                            SeqApp.UpdateAppMessage(string.Format("Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
                            //UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            b = false;
                            SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", _AutoFocusErrorMessage));
                        }
                    }
                    // Double check top surface in case of local maximum
                    if (b)
                    {
                        _AutoFocusSetting.ZstageLimitH = _Reference0 + 15;
                        _AutoFocusSetting.ZstageLimitL = _Reference0 - 15;
                        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                        //_IsAutoFocusing = true;
                        _AutoFocusProcess_check.Name = "Autofocus";
                        _AutoFocusProcess_check.Start();
                        _AutoFocusProcess_check.Join();
                        _AutoFocusProcess_check = null;
                        //while (_IsAutoFocusing) { Thread.Sleep(1); }
                        if (_IsAFSuccess || IsSimulationMode)
                        {
                            FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
                            _Reference0 = FocusedTopPos;
                            SeqApp.UpdateAppMessage(string.Format("Double check Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            b = false;
                            SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", _AutoFocusErrorMessage));
                        }
                    }
                    _TopSurfaceSharpness = _FocusedSharpness;
                }
                catch (Exception ex)
                {
                    SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", ex.Message));
                    b = false;
                }
            }
            //Bottom Surface AF
            if (b && _TopSurfaceSharpness > SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL)
            {
                try
                {
                    AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                    _AutoFocusSetting.ZRange = SettingsManager.ConfigSettings.AutoFocusingSettings.ZRange;
                    double bottomreference = FocusedTopPos - SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight;
                    _AutoFocusSetting.ZstageLimitH = bottomreference + _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ZstageLimitL = bottomreference - _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ScanInterval = 3;
                    _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                    _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                    _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                    //_IsAutoFocusing = true;
                    _AutoFocusProcess_check.Name = "Autofocus";
                    _AutoFocusProcess_check.Start();
                    _AutoFocusProcess_check.Join();
                    _AutoFocusProcess_check = null;
                    if (_IsAFSuccess || IsSimulationMode)
                    {
                        FocusedBottomPos = SeqApp.MotionController.ZCurrentPos;
                        SeqApp.UpdateAppMessage(string.Format("Autofocus successed at bottom surface, Z:{0}, sharpness:{1}", FocusedBottomPos, _FocusedSharpness));
                        UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    }
                    else
                    {
                        // retry
                        SeqApp.UpdateAppWarningMessage(string.Format("AF at Bottom surface failed due to: {0}, retry", _AutoFocusErrorMessage));

#region Push potential bubble
                        //OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                        //PumpingSettings _PumpSetting = new PumpingSettings();
                        //_PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
                        //_PumpSetting.PullRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
                        //_PumpSetting.PumpingVolume = 200;
                        //_PumpSetting.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                        //_PumpSetting.SelectedMode = ModeOptions.AspirateDispense;
                        //_PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = 22 };
                        //_PumpSetting.SelectedPullPath = PathOptions.FC;
                        //_PumpSetting.SelectedPushPath = PathOptions.Waste;
                        //_PumpSetting.SelectedPullValve2Pos = 6;
                        //_PumpSetting.SelectedPullValve3Pos = 1;
                        //_PumpSetting.SelectedPushValve2Pos = 6;
                        //_PumpSetting.SelectedPushValve3Pos = 1;
                        //for (int i = 0; i < 4; i++)
                        //{
                        //    if (i + 1 == region.Lane) { _PumpSetting.PumpPullingPaths[i] = true; } else { _PumpSetting.PumpPullingPaths[i] = false; }
                        //    _PumpSetting.PumpPushingPaths[i] = false;
                        //}
                        //FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                        //if (!IsSimulationMode)
                        //{
                        //    FluidicsInterface.WaitForPumpingCompleted();
                        //}

#endregion Push potential bubble
                        _AutoFocusSetting.ZstageLimitH = bottomreference + _AutoFocusSetting.ZRange;
                        _AutoFocusSetting.ZstageLimitL = bottomreference - _AutoFocusSetting.ZRange;
                        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                        _AutoFocusProcess_check.Name = "Autofocus";
                        _AutoFocusProcess_check.Start();
                        _AutoFocusProcess_check.Join();
                        _AutoFocusProcess_check = null;
                        if (_IsAFSuccess || IsSimulationMode)
                        {
                            FocusedBottomPos = SeqApp.MotionController.ZCurrentPos;
                            SeqApp.UpdateAppMessage(string.Format("Autofocus successed at bottom surface, Z:{0}, sharpness:{1}", FocusedBottomPos, _FocusedSharpness));
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            b = false;
                            SeqApp.UpdateAppErrorMessage(string.Format("AF at Bottom surface failed due to: {0}", _AutoFocusErrorMessage));
                        }
                    }
                    // Double check Bottom surface in case of local maximum
                    if (b)
                    {
                        _AutoFocusSetting.ZstageLimitH = FocusedBottomPos + 15;
                        _AutoFocusSetting.ZstageLimitL = FocusedBottomPos - 15;
                        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                        //_IsAutoFocusing = true;
                        _AutoFocusProcess_check.Name = "Autofocus";
                        _AutoFocusProcess_check.Start();
                        _AutoFocusProcess_check.Join();
                        _AutoFocusProcess_check = null;
                        //while (_IsAutoFocusing) { Thread.Sleep(1); }
                        if (_IsAFSuccess || IsSimulationMode)
                        {
                            FocusedBottomPos = SeqApp.MotionController.ZCurrentPos;
                            SeqApp.UpdateAppMessage(string.Format("Double check Autofocus successed at bottom surface, Z:{0}, sharpness:{1}", FocusedBottomPos, _FocusedSharpness));
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            b = false;
                            SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", _AutoFocusErrorMessage));
                        }
                    }
                    _BottomSurfaceSharpness = _FocusedSharpness;
                }
                catch (Exception ex)
                {
                    SeqApp.UpdateAppErrorMessage(string.Format("AF at Bottom surface failed due to: {0}", ex.Message));
                    b = false;
                }

            }
            //find bottom instead of top, if sharpness lower than top surface sharpness low threshold
            else if (b && _TopSurfaceSharpness <= SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL)
            {
                FocusedBottomPos = FocusedTopPos;
                try
                {
                    if (!IsSimulationMode)
                    {
                        SeqApp.UpdateAppErrorMessage("Focused sharpness lower than expected for top surface, finding top surface focus again");
                    }
                    AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                    _AutoFocusSetting.ZRange = 20;
                    double topreference = FocusedBottomPos + SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight;
                    _AutoFocusSetting.ZstageLimitH = topreference + _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ZstageLimitL = topreference - _AutoFocusSetting.ZRange / 2;
                    _AutoFocusSetting.ScanInterval = 3;
                    _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                    _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                    _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                    //_IsAutoFocusing = true;
                    _AutoFocusProcess_check.Name = "Autofocus";
                    _AutoFocusProcess_check.Start();
                    _AutoFocusProcess_check.Join();
                    _AutoFocusProcess_check = null;
                    if (_IsAFSuccess || IsSimulationMode)
                    {
                        FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
                        SeqApp.UpdateAppMessage(string.Format("Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
                        UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                    }
                    else
                    {
                        // retry
                        SeqApp.UpdateAppWarningMessage(string.Format("AF at Bottom surface failed due to: {0}, retry", _AutoFocusErrorMessage));
                        if (_AutoFocusErrorMessage != null)
                        {
                            if (_AutoFocusErrorMessage.Contains("Range")) //shift range if out of range
                            {
                                _Reference0 = SeqApp.MotionController.ZCurrentPos;
                            }
                        }
#region Push potential bubble
                        //OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                        //PumpingSettings _PumpSetting = new PumpingSettings();
                        //_PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
                        //_PumpSetting.PullRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
                        //_PumpSetting.PumpingVolume = 200;
                        //_PumpSetting.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                        //_PumpSetting.SelectedMode = ModeOptions.AspirateDispense;
                        //_PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = 22 };
                        //_PumpSetting.SelectedPullPath = PathOptions.FC;
                        //_PumpSetting.SelectedPushPath = PathOptions.Waste;
                        //_PumpSetting.SelectedPullValve2Pos = 6;
                        //_PumpSetting.SelectedPullValve3Pos = 1;
                        //_PumpSetting.SelectedPushValve2Pos = 6;
                        //_PumpSetting.SelectedPushValve3Pos = 1;
                        //for (int i = 0; i < 4; i++)
                        //{
                        //    if (i + 1 == region.Lane) { _PumpSetting.PumpPullingPaths[i] = true; } else { _PumpSetting.PumpPullingPaths[i] = false; }
                        //    _PumpSetting.PumpPushingPaths[i] = false;
                        //}
                        //FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                        //if (!IsSimulationMode)
                        //{
                        //    FluidicsInterface.WaitForPumpingCompleted();
                        //}

#endregion Push potential bubble
                        _AutoFocusSetting.ZstageLimitH = topreference + _AutoFocusSetting.ZRange;
                        _AutoFocusSetting.ZstageLimitL = topreference - _AutoFocusSetting.ZRange;
                        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
                        //_IsAutoFocusing = true;
                        _AutoFocusProcess_check.Name = "Autofocus";
                        _AutoFocusProcess_check.Start();
                        _AutoFocusProcess_check.Join();
                        _AutoFocusProcess_check = null;
                        //while (_IsAutoFocusing) { Thread.Sleep(1); }
                        if (_IsAFSuccess || IsSimulationMode)
                        {
                            FocusedTopPos = SeqApp.MotionController.ZCurrentPos;
                            SeqApp.UpdateAppMessage(string.Format("Autofocus successed at top surface, Z:{0}, sharpness:{1}", FocusedTopPos, _FocusedSharpness));
                            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
                        }
                        else
                        {
                            b = false;
                            SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", _AutoFocusErrorMessage));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SeqApp.UpdateAppErrorMessage(string.Format("AF at Bottom surface failed due to: {0}", ex.Message));
                    b = false;
                }
            }
            //Check difference between surface in case of top surface is outside top
            //This check is not reasonable if FC top layer glass thickness is very similar as FC channel height
            //bool _isSurfaceError = false;
            //if((SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight > SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness
            //    && FocusedTopPos - FocusedBottomPos < SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness) ||
            //    //if FC channel height larger than top glass thickness, difference between AFtop and bottom should not smaller than glass thickness
            //    //For homemade FC, channel height 120-130, thickness of top glass 100-110
            //    (SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight < SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness
            //    && FocusedTopPos - FocusedBottomPos > SettingsManager.ConfigSettings.AutoFocusingSettings.TopGlassThickness))
            ////if FC channel height lower than top glass thickness, difference between AFtop and bottom should not smaller than channel height
            ////For walfarplus FC, channel height ~80, thickness of top glass ~110?
            //{
            //    _isSurfaceError = true;
            //}
            //if(_isSurfaceError)
            //{
            //    // previous bottom should be inner top if the top was outside top surface
            //    FocusedTopPos = FocusedBottomPos;
            //    //rescan bottom surface
            //    if (b)
            //    {
            //        AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings();
            //        _AutoFocusSetting.LEDType = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDType;
            //        _AutoFocusSetting.LEDIntensity = SettingsManager.ConfigSettings.AutoFocusingSettings.LEDIntensity;
            //        _AutoFocusSetting.ExposureTime = SettingsManager.ConfigSettings.AutoFocusingSettings.ExposureTime;
            //        _AutoFocusSetting.ZstageSpeed = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageSpeed;
            //        _AutoFocusSetting.ZstageAccel = SettingsManager.ConfigSettings.AutoFocusingSettings.ZstageAccel;
            //        _AutoFocusSetting.ZRange = 60;
            //        double bottomreference = FocusedTopPos - SettingsManager.ConfigSettings.AutoFocusingSettings.FCChannelHeight;
            //        _AutoFocusSetting.ZstageLimitH = bottomreference + _AutoFocusSetting.ZRange / 2;
            //        _AutoFocusSetting.ZstageLimitL = bottomreference - _AutoFocusSetting.ZRange / 2;
            //        Int32Rect _AutofocusRoi = new Int32Rect();
            //        _AutofocusRoi.X = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.X;
            //        _AutofocusRoi.Y = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Y;
            //        _AutofocusRoi.Width = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Width;
            //        _AutofocusRoi.Height = SettingsManager.ConfigSettings.AutoFocusingSettings.ROI.Height;
            //        _AutoFocusSetting.ROI = _AutofocusRoi;
            //        _AutoFocusSetting.RotationAngle = SettingsManager.ConfigSettings.AutoFocusingSettings.RotationAngle;
            //        _AutoFocusSetting.IsHConly = false;
            //        _AutoFocusSetting.IsRecipe = false;
            //        _AutoFocusSetting.IsScanonly = false;
            //        _AutoFocusSetting.ScanInterval = 3;
            //        _AutoFocusProcess_check = new AutoFocusCommand2(null, SeqApp.MotionController, AFcamera, _LEDController, _AutoFocusSetting);
            //        _AutoFocusProcess_check.IsSimulationMode = IsSimulationMode;
            //        _AutoFocusProcess_check.Completed += AutoFocusProcess_check_Completed;
            //        //_IsAutoFocusing = true;
            //        _AutoFocusProcess_check.Name = "Autofocus";
            //        _AutoFocusProcess_check.Start();
            //        _AutoFocusProcess_check.Join();
            //        _AutoFocusProcess_check = null;
            //        if (_IsAFSuccess || IsSimulationMode)
            //        {
            //            FocusedBottomPos = SeqApp.MotionController.ZCurrentPos;
            //            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging, CheckHardwareProgressMessageEnum.Progress, progressDelta);
            //        }
            //        else
            //        {
            //            b = false;
            //            SeqApp.UpdateAppErrorMessage(string.Format("AF at Top surface failed due to: {0}", _AutoFocusErrorMessage));
            //        }
            //    }
            //}

#endregion Autofocus reference
            UpdateCheckHardwareProgress(CheckHardwareEnum.Imaging,
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                b ? progressDelta : 0);
            return b;
        }

        private void AutoFocusProcess_check_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            //_IsAutoFocusing = false;
            _FocusedSharpness = _AutoFocusProcess_check.FoucsedSharpness;
            if (exitState == ThreadExitStat.None)
            {                
                _IsAFSuccess = true;
            }
            else
            {
                _IsAFSuccess = false;
                _AutoFocusErrorMessage = _AutoFocusProcess_check.ExceptionMessage;
            }
            //overwrite the error message if sharpness out of range.
            if (!IsSimulationMode)
            {
                double _BottomStdLmtL = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL;
                double _TopStdLmtH = SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH;
                if (_FocusedSharpness < _BottomStdLmtL || _FocusedSharpness > _TopStdLmtH)
                {
                    _IsAFSuccess = false;
                    _AutoFocusErrorMessage = $"FocusedSharpness:{_FocusedSharpness} out of threshold [{_BottomStdLmtL},{_TopStdLmtH}]";
                }
            }
            _AutoFocusProcess_check.Completed -= AutoFocusProcess_check_Completed;
            //_AutoFocusProcess_check = null;
        }
        //private void CoarseAutoFocusProcess_check_Completed(ThreadBase sender, ThreadExitStat exitState)
        //{
        //    //_IsAutoFocusing = false;
        //    if (exitState == ThreadExitStat.None)
        //    {
        //        _FocusedSharpness = _CoarseAutoFocusProcess_check.FoucsedSharpness;
        //        _IsAFSuccess = true;

        //    }
        //    else
        //    {
        //        _IsAFSuccess = false;
        //        _AutoFocusErrorMessage = _CoarseAutoFocusProcess_check.ExceptionMessage;
        //    }
        //    //overwrite the error message if sharpness out of range.
        //    if (!IsSimulationMode)
        //    {
        //        if (_FocusedSharpness < SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL)
        //        {
        //            _IsAFSuccess = false;
        //            _AutoFocusErrorMessage = "Focused Sharpness lower than threshold";
        //        }
        //        if (_FocusedSharpness > SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH)
        //        {
        //            _IsAFSuccess = false;
        //            _AutoFocusErrorMessage = "Focused Sharpness higher than threshold";
        //        }
        //    }
        //    _CoarseAutoFocusProcess_check.Completed -= CoarseAutoFocusProcess_check_Completed;
        //    //_AutoFocusProcess_check = null;
        //}
        public bool SeprePriming()
        {
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
            string primingRecipeDir = _RecipeBuildSettings.PrimingRecipeRecipePath;

            //Recipe SeprePrimingRecipe = Recipe.LoadFromXmlFile(primingRecipeDir);
            string str = SessionId;// DateTime.Now.ToString("yyMMdd-HHmmss");
            string recipeDir = SeqApp.CreateTempRecipeLocation(str);
            string originalRecipeDir = _RecipeBuildSettings.RecipeBaseDir;
            string newRecipeFullFilePath = SeqApp.SaveRecipeToNewPath(primingRecipeDir, recipeDir, "SeprePriming.xml", originalRecipeDir);
            Recipe SeprePrimingRecipe = Recipe.LoadFromXmlFile(newRecipeFullFilePath);

            bool loadCartridge = false;
            _IsPrimingSuccess = false;
            RecipeThreadParameters _RecipeParam = new RecipeThreadParameters()
            {

                SelectedTemplate = TemplateOptions.ecoli,
                IsSimulation = IsSimulationMode,
                LoadCartridge = loadCartridge
            };
            _RecipeRunThreadV2_Check = new RecipeRunThreadV2(
                        SeqApp.TheDispatcher,
                        SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                        SeprePrimingRecipe,
                        SeqApp.EthernetCameraA,
                        SeqApp.EthernetCameraB,
                        SeqApp.MotionController,
                        SeqApp.MainboardDevice,
                        SeqApp.LEDController,
                        SeqApp.FluidicsInterface,
                        _RecipeParam, null,
                        null, //no OLA
                        false, //no wait for OLA completion
                        null,
                        false
                        );
            _RecipeRunThreadV2_Check.Completed += _RecipeRunThread_check_Completed;
            _RecipeRunThreadV2_Check.InProgress += _RecipeRunThread_check_InProgress;
            _RecipeRunThreadV2_Check.Name = "PrimingRecipe";
            _RecipeRunThreadV2_Check.IsSimulationMode = IsSimulationMode;
            _RecipeRunThreadV2_Check.Start();
            _RecipeRunThreadV2_Check.WaitForCompleted();
            _RecipeRunThreadV2_Check.Join();
            _RecipeRunThreadV2_Check = null;
            bool b = _IsPrimingSuccess;
            return b;
        }

        private void _RecipeRunThread_check_InProgress(object sender, EventArgs e)
        {
            int progressDelta = 1;

            UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
        }

        private void _RecipeRunThread_check_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            if (_RecipeRunThreadV2_Check.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                _IsPrimingSuccess = true;
            }
            else if (_RecipeRunThreadV2_Check.ExitStat == ThreadBase.ThreadExitStat.Error)
            {
                _IsPrimingSuccess = false;
                SeqApp.UpdateAppErrorMessage("SeprePriming Failed");
            }
            _RecipeRunThreadV2_Check.Completed -= _RecipeRunThread_check_Completed;
            //_RecipeRunThreadV2_Check = null;
        }
        public bool FlowCheck(int selectedsolution, List<PathOptions> paths)
        {
            bool b = true;
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Flow, CheckHardwareProgressMessageEnum.Start, 0);
            try
            {
#region New pumping settings
                PumpingSettings _PumpSetting = new PumpingSettings();
                _PumpSetting.PullRate = 1000;
                _PumpSetting.PushRate = 15000;
                _PumpSetting.PumpingVolume = 250;
                _PumpSetting.SelectedMode = ModeOptions.Pull;
                _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = selectedsolution };
                _PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
#endregion New pumping settings
                if (b)
                {
                    _LogTimer = new System.Timers.Timer(1000);
                    _LogTimer.Elapsed += _LogTimer_Elapsed;
                    _LogTimer.AutoReset = true;
                    foreach (PathOptions pullpath in paths)
                    {
                        if (IsAbortCheck)
                        {
                            b = false;
                            break;
                        }
                        //empty the pump if volumn less than request
                        if (SeqApp.FluidicsInterface.Pump.PumpAbsolutePos != 0)
                        {
                            _FlowCheck_RunPuming_OK = true;
                            PumpingSettings PumpSettings = new PumpingSettings();
                            PumpSettings.SelectedPushPath = PathOptions.Waste;
                            PumpSettings.SelectedMode = ModeOptions.Push;
                            PumpSettings.PushRate = 15000;
                            PumpSettings.SelectedSolution = new ValveSolution() { ValveNumber = selectedsolution };
                            PumpSettings.PumpingVolume = SeqApp.FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor;
                            for (int i = 0; i < 4; i++)
                            {
                                PumpSettings.PumpPushingPaths[i] = false;
                            }
                            PumpSettings.SelectedPushValve2Pos = 6;
                            PumpSettings.SelectedPushValve3Pos = 1;
                            SeqApp.FluidicsInterface.OnPumpingInProgress += FlowCheck_FluidicsInterface_OnPumpingInProgress;
                            SeqApp.FluidicsInterface.OnPumpingCompleted += FlowCheck_RunPuming_Completed;
                            SeqApp.FluidicsInterface.RunPumping(null, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, true, IsSimulationMode);
                            SeqApp.FluidicsInterface.WaitForPumpingCompleted();
                        }

                        _PumpSetting.SelectedPullPath = pullpath;
                        _PumpSetting.SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[pullpath].SelectedPullValve2Pos;
                        _PumpSetting.SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[pullpath].SelectedPullValve3Pos;
                        for (int i = 0; i < 4; i++)
                        {
                            _PumpSetting.PumpPullingPaths[i] = SettingsManager.ConfigSettings.PumpPathDefault[pullpath].PumpPullingPaths[i];
                        }
                        SeqApp.FluidicsInterface.OnPumpingInProgress += FlowCheck_FluidicsInterface_OnPumpingInProgress;
                        SeqApp.FluidicsInterface.OnPumpingCompleted += FlowCheck_RunPuming_Completed;
                        SeqApp.FluidicsInterface.OnPumpStatusUpdated += FluidicsInterface_OnPumpStatusUpdated;
                        _PressureList.Clear();
                        _FlowrateList.Clear();



                        SeqApp.FluidicsInterface.RunPumping(null, 12, _PumpSetting, true, IsSimulationMode);
                        SeqApp.FluidicsInterface.WaitForPumpingCompleted();

                        if (_PressureList.Any() && _FlowrateList.Any())
                        {
                            //// Remove the data recorded during PullDelay waiting time and time for initializing/setting pump and valve, only keep the window that pump moving
                            //_PressureList.RemoveRange(_PressureList.Count - 5, 5);
                            //_FlowrateList.RemoveRange(_FlowrateList.Count - 5, 5);
                            //_PressureList.RemoveRange(0, 10);
                            //_FlowrateList.RemoveRange(0, 10);


                            var validPressureDatas = new List<double>(_PressureList.ToArray());
                            var validFlowrateDatas = new List<double>(_FlowrateList.ToArray());
                            if (validPressureDatas.Count > 20)
                                validPressureDatas.RemoveRange(0, 20);
                            if (validFlowrateDatas.Count > 20)
                                validFlowrateDatas.RemoveRange(0, 20);

                            double avgpressure = /*_PressureList*/validPressureDatas.Count > 0 ? /*_PressureList*/validPressureDatas.Average() : 0.0;
                            double avgflow = /*_FlowrateList*/validFlowrateDatas.Count > 0 ? /*_FlowrateList*/validFlowrateDatas.Average() : 0.0;
                            // Perform the Sum of (value-avg)_2_2.      
                            double sum = /*_FlowrateList*/validFlowrateDatas.Sum(d => Math.Pow(d - avgflow, 2));
                            // Put it all together.      
                            double stdflow = Math.Sqrt((sum) / (/*_FlowrateList*/validFlowrateDatas.Count() - 1));
                            //Compare with pre-calibrated value
                            if (pullpath.ToString().Contains("TestBypass")) { _CalibPressure = SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ByPassPressureCalib; }
                            else { _CalibPressure = SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FCTestPressureCalib; }
                            if (Math.Abs(avgpressure - _CalibPressure) > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.PressureTole
                                || Math.Abs(avgflow - _PumpSetting.PullRate / 1000) > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FlowRateTole
                                || stdflow > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FlowRateStdTole)
                            {
                                _FlowCheck_RunPuming_OK = false;
                                SeqApp.UpdateAppErrorMessage("Failed to pass FlowCheck");
                            }
                            SeqApp.UpdateAppMessage(string.Format("Flow test path: {0}, Pressure Mean: {1:00.00}, Flow Mean: {2:00.00}, Flow Stdv: {3:00.00}", pullpath.ToString(), avgpressure, avgflow, stdflow));
                            string pressuredata = string.Join(", ", /*_PressureList*/validPressureDatas);
                            string flowdata = string.Join(", ", /*_FlowrateList*/validFlowrateDatas);
                            SeqApp.UpdateAppMessage(string.Format("Pressure data:{0}", pressuredata));
                            SeqApp.UpdateAppMessage(string.Format("Flowrate data:{0}", flowdata));

                        }

                        b = _FlowCheck_RunPuming_OK;
                        // to do data processing 
                        if (!b)
                        {
                            break;
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to do flow check with error: {ex.Message}.");
                b = false;
            }
            _LogTimer?.Stop();
            _LogTimer?.Dispose();
            UpdateCheckHardwareProgress(CheckHardwareEnum.Flow,
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                b ? progressDelta : 0);
            return b;
        }
        private void _LogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SeqApp.FluidicsInterface.GetFluidControllerStatus();
            for (int i = 0; i < 10; i++)
            {
                _PressureList.Add(Math.Round(FluidController.GetInstance().PressureArray[i], 2));
                _FlowrateList.Add(Math.Round(FluidController.GetInstance().FlowArray[i], 2));
            }
        }
        private void FluidicsInterface_OnPumpStatusUpdated(bool _isOn, int solution)
        {
            if (_isOn) { _LogTimer.Start(); }
            else { _LogTimer.Stop(); }
        }

        private void FlowCheck_FluidicsInterface_OnPumpingInProgress(object sender, EventArgs e)
        {
            int progressDelta = 1;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Flow, CheckHardwareProgressMessageEnum.Progress, progressDelta);
        }

        private void FlowCheck_RunPuming_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            SeqApp.FluidicsInterface.OnPumpingCompleted -= FlowCheck_RunPuming_Completed;
            SeqApp.FluidicsInterface.OnPumpingInProgress -= FlowCheck_FluidicsInterface_OnPumpingInProgress;
            SeqApp.FluidicsInterface.OnPumpStatusUpdated -= FluidicsInterface_OnPumpStatusUpdated;
            Task.Run(() =>
            {
                // _TecanPump.GetPumpPos();
                if (exitState == ThreadBase.ThreadExitStat.Error)
                {
                    _FlowCheck_RunPuming_OK = false;
                    SeqApp.NotifyNormalError("Error occurred during pumping thread, valve failure");
                }
                else if (exitState == ThreadBase.ThreadExitStat.Abort)
                {
                    _FlowCheck_RunPuming_OK = false;
                    SeqApp.NotifyNormalError("Pumping thread aborted");
                }
                else if (exitState == ThreadBase.ThreadExitStat.None)
                {
                    _FlowCheck_RunPuming_OK = true;
                }
                //_RunTecanPumingThread.Completed -= _RunTecanPumingThread_Completed;
                //_RunTecanPumingThread = null;
            });
        }


        //2%
        public bool DiskSpaceCheck(int readlength)
        {
            ResetProgress(ref _DiskSpaceCheckProgress);
            bool b = true;
            int progressDelta = 1;
            long diskSpace;
            string driveName;
            UpdateCheckHardwareProgress(CheckHardwareEnum.Disk, CheckHardwareProgressMessageEnum.Start, progressDelta);
            try
            {
                b = SeqApp.GetImageDriveDiskSpace(out diskSpace, out driveName);
                DiskSpaceEmp = diskSpace;
                if (b)
                {
                    //tile count * surface count * (4*(Read1+Index1) images' size in MB + OLA data size pre tile in MB) / 1024 to GB
                    DiskSpaceReq = SettingsManager.ConfigSettings.StageRegionMaps.Count * 2 * (4 * 37.5 * (readlength) + /*5 * 1024*/SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLADataSizeMBPerTile) / 1024; //GB
                    if (diskSpace < DiskSpaceReq)
                    {
                        string str = ($"{driveName} disk space ({diskSpace} GB) is lower than {DiskSpaceReq} GB. Please clean up disk space.");
                        if (IsSimulationMode)
                        {
                            SeqApp.UpdateAppWarningMessage(str);
                        }
                        else
                        {
                            b = false;
                            SeqApp.NotifyNormalError(str);
                        }
                    }
                }
                else
                {
                    SeqApp.NotifyNormalError("Failed to check disk space");
                }
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to check disk space with error: {ex.Message}.");
                b = false;
            }
            UpdateCheckHardwareProgress(CheckHardwareEnum.Disk,
                b ? CheckHardwareProgressMessageEnum.End : CheckHardwareProgressMessageEnum.End_Error,
                b ? progressDelta : 0);
            return b;
        }

        public bool CancelChecking()
        {
            IsAbortCheck = true;
            _RecipeRunThreadV2_Check?.AbortWork();
            _AutoFocusProcess_check?.AbortWork();
            Task task1 = Task.Factory.StartNew(() => _RecipeRunThreadV2_Check?.Join());
            Task task2 = Task.Factory.StartNew(() => _AutoFocusProcess_check?.Join());
            Task.WaitAll(task1, task2);
            return true;
        }
    }
}
