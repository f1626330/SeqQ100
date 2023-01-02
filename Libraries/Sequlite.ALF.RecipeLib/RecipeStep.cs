using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Sequlite.ALF.RecipeLib
{
    public enum RecipeStepTypes
    {
        SetTemper,
        StopTemper,
        SetPreHeatTemp,
        StopPreHeating,
        Imaging,
        MoveStage,
        Pumping,
        Loop,
        RunRecipe,
        Waiting,
        Comment,
        NewPumping,
        MoveStageRev2,
        HomeMotion,
        AbsoluteMove,
        RelativeMove,
        HywireImaging,
        LEDCtrl,
    }
    public enum ImagingChannels
    {
        Red,
        Green,
        RedGreen,
        White,
    }

    public enum FilterTypes
    {
        None,
        Filter1,
        Filter2,
        Filter3,
        Filter4,
    }

    public abstract class RecipeStepBase
    {
        public RecipeStepTypes StepType { get; protected set; }
        public string StepName
        {
            get
            {
                return GetTypeName(StepType);
            }
        }
        public virtual string DisplayName
        {
            get
            {
                return ToString();
            }
        }
        public static string GetTypeName(RecipeStepTypes type)
        {
            string typeName = string.Empty;
            switch (type)
            {
                case RecipeStepTypes.SetTemper:
                    typeName = "Set Temperature";
                    break;
                case RecipeStepTypes.StopTemper:
                    typeName = "Stop Temperature";
                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    typeName = "Set PreHeating Temp";
                    break;
                case RecipeStepTypes.StopPreHeating:
                    typeName = "Stop PreHeating";
                    break;
                case RecipeStepTypes.Imaging:
                    typeName = "Capture Image";
                    break;
                case RecipeStepTypes.MoveStage:
                    typeName = "Move Stage";
                    break;
                case RecipeStepTypes.Pumping:
                    typeName = "Pumping";
                    break;
                case RecipeStepTypes.Loop:
                    typeName = "Looping";
                    break;
                case RecipeStepTypes.RunRecipe:
                    typeName = "Run Recipe";
                    break;
                case RecipeStepTypes.Waiting:
                    typeName = "Waiting";
                    break;
                case RecipeStepTypes.Comment:
                    typeName = "Comment";
                    break;
                case RecipeStepTypes.NewPumping:
                    typeName = "New Pumping";
                    break;
                case RecipeStepTypes.MoveStageRev2:
                    typeName = "New Move Stage";
                    break;
                case RecipeStepTypes.HomeMotion:
                    typeName = "Home Motion";
                    break;
                case RecipeStepTypes.AbsoluteMove:
                    typeName = "Absolute Move";
                    break;
                case RecipeStepTypes.RelativeMove:
                    typeName = "Relative Move";
                    break;
                case RecipeStepTypes.HywireImaging:
                    typeName = "Hywire Capture Image";
                    break;
                case RecipeStepTypes.LEDCtrl:
                    typeName = "LED Control";
                    break;
                default:
                    throw new NotImplementedException($"Unkown step type name: {type}");
            }
            return typeName;
        }
        public static RecipeStepTypes GetStepType(string typeName)
        {
            foreach (RecipeStepTypes stepType in Enum.GetValues(typeof(RecipeStepTypes)))
            {
                if (GetTypeName(stepType) == typeName)
                {
                    return stepType;
                }
            }
            throw new NotImplementedException($"Unkown step type: {typeName}");
        }
    }

    #region Temperature Steps
    public class SetTemperStep : RecipeStepBase
    {
        public double TargetTemper { get; set; } //< the target temperature for this step (units == [°C])
        public int Duration { get; set; } //< the target duration for this step (units == [seconds])
        public double Tolerance { get; set; } //< the maximum allowed difference between target and actual temperatures (units == [°C])
        public bool WaitForComplete { get; set; } //< if set to true, the recipe will not proceed until this step has completed
        public double CtrlP { get; set; } //< PID controller proportional gain (units == [unitless])
        public double CtrlI { get; set; } //< PID controller integral gain (units == [time? x*t?])
        public double CtrlD { get; set; } //< PID controller derivative gain (units == [time? x/t?])
        public double CtrlHeatGain { get; set; } //< TE Heat Gain. 0 = 1% = 0V, 1 = 100% = 24V. Two Decimal places of precision.
        public double CtrlCoolGain { get; set; } //< TE Cool Gain. 0 = 1% = 0V, 1 = 100% = 24V. Two Decimal places of precision.
        public SetTemperStep()
        {
            StepType = RecipeStepTypes.SetTemper;
            TargetTemper = 22;
            Duration = 60;
            Tolerance = 1;
            WaitForComplete = true;
            CtrlP = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP;
            CtrlI = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI;
            CtrlD = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD;
            CtrlHeatGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain;
            CtrlCoolGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain;
        }
        public override string ToString()
        {
            return $"Set FC Temperature: {TargetTemper}°C, duration:{Duration} s, tolerance:{Tolerance}°C, wait:{WaitForComplete}";
        }

    }
    public class SetPreHeatTempStep : RecipeStepBase
    {
        public double TargetTemper { get; set; }
        //public int Duration { get; set; }
        public double Tolerance { get; set; }
        public bool WaitForComplete { get; set; }
        //public double CtrlP { get; set; }
        //public double CtrlI { get; set; }
        //public double CtrlD { get; set; }
        //public double CtrlHeatGain { get; set; }
        //public double CtrlCoolGain { get; set; }
        public SetPreHeatTempStep()
        {
            StepType = RecipeStepTypes.SetPreHeatTemp;
            TargetTemper = 30;
            //Duration = 60;
            Tolerance = 5;
            WaitForComplete = true;
            //CtrlP = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP;
            //CtrlI = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI;
            //CtrlD = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD;
            //CtrlHeatGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain;
            //CtrlCoolGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain;
        }
        public override string ToString()
        {
            return $"Set preheating temperature:{TargetTemper}°C, tolerance:{Tolerance}°C, wait:{WaitForComplete}";
        }

    }
    public class StopPreHeatingStep : RecipeStepBase
    {
        public StopPreHeatingStep()
        {
            StepType = RecipeStepTypes.StopPreHeating;
        }
        public override string ToString()
        {
            return $"Stop Preheating";
        }
    }
    public class StopTemperStep : RecipeStepBase
    {
        public StopTemperStep()
        {
            StepType = RecipeStepTypes.StopTemper;
        }
        public override string ToString()
        {
            return "Stop Temperature";
        }
    }
    #endregion Temperature Steps

    #region Fluidics Steps
    public class PumpingStep : RecipeStepBase
    {
        public ModeOptions PumpingType { get; set; }
        public int Volume { get; set; }
        public PathOptions PullPath { get; set; }
        public double PullRate { get; set; }
        public PathOptions PushPath { get; set; }
        public double PushRate { get; set; }
        public int Reagent { get; set; }
        public PumpingStep()
        {
            StepType = RecipeStepTypes.Pumping;
            PumpingType = ModeOptions.PullPush;
            Volume = 200;
            PullRate = 100;
            Reagent = 1;
        }
        public override string ToString()
        {
            string pumpingStr;
            switch (PumpingType)
            {
                case ModeOptions.PullPush:
                    pumpingStr = string.Format("Pumping: vol={0}uL, pull path={1}, pull rate={2}uL/min, push path={3}, push rate={4}uL/min, reagent={5}",
                        Volume, PullPath, PullRate, PushPath, PushRate, Reagent);
                    break;
                case ModeOptions.Pull:
                    pumpingStr = string.Format("Pulling: vol={0}uL, pull path={1}, pull rate={2}uL/min, reagent={3}",
                        Volume, PullPath, PullRate, Reagent);
                    break;
                case ModeOptions.Push:
                    pumpingStr = string.Format("Pushing: vol={0}uL, push path={1}, push rate={2}uL/min, reagent={3}",
                        Volume, PushPath, PushRate, Reagent);
                    break;
                default:
                    pumpingStr = "Unknown pumping type";
                    break;
            }
            return pumpingStr;
        }
    }
    public class NewPumpingStep : RecipeStepBase
    {
        public ModeOptions PumpingType { get; set; }
        public int Volume { get; set; }
        public PathOptions PullPath { get; set; }
        public double PullRate { get; set; }
        public PathOptions PushPath { get; set; }
        public double PushRate { get; set; }
        public int Reagent { get; set; }
        public int SelectedPullValve2Pos { get; set; }
        public int SelectedPullValve3Pos { get; set; }
        public int SelectedPushValve2Pos { get; set; }
        public int SelectedPushValve3Pos { get; set; }
        public bool[] PumpPullingPaths { get; set; }
        public bool[] PumpPushingPaths { get; set; }
        public NewPumpingStep()
        {
            StepType = RecipeStepTypes.NewPumping;
            PumpingType = ModeOptions.PullPush;
            Volume = 200;
            PullRate = 100;
            Reagent = 1;
            PumpPullingPaths = new bool[4];
            PumpPushingPaths = new bool[4];
        }
        public override string ToString()
        {
            string pumpingStr;
            string pushpaths = null;
            string pullpaths=null;
            for (int i = 0; i < 4; i++)
            {
                if (PumpPushingPaths[i]) { pushpaths += "1"; } else { pushpaths += "0"; }
                if (PumpPullingPaths[i]) { pullpaths += "1"; } else { pullpaths += "0"; }
            }
            switch (PumpingType)
            {
                case ModeOptions.PullPush:
                    pumpingStr = string.Format("New Pumping: Reagent={0}, Vol={1}uL, PullPath={2}, PumpPullPath={3}, PullValve2={4}, PullValve3={5}, pull rate={6}uL/min, " +
                        "push path={7}, PumpPushPath={8}, PushValve2={9}, PushValve3={10}, push rate={11}uL/min",
                        Reagent, Volume, PullPath, pullpaths, SelectedPullValve2Pos,SelectedPullValve3Pos,PullRate,
                        PushPath, pushpaths, SelectedPushValve2Pos, SelectedPushValve3Pos, PushRate);
                    break;
                case ModeOptions.Pull:
                    pumpingStr = string.Format("New Pulling: Reagent={0}, Vol={1}uL, PullPath={2}, PumpPullPath={3}, PullValve2={4}, PullValve3={5}, PullRate={6}",
                        Reagent, Volume, PullPath, pullpaths,  SelectedPullValve2Pos, SelectedPullValve3Pos, PullRate);
                    break;
                case ModeOptions.Push:
                    pumpingStr = string.Format("New Pushing: Reagent={0}, Vol={1}uL, PushPath={2}, PumpPushPath={3}, PushValve2={4}, PushValve3={5}, PushRate={6}",
                        Reagent, Volume, PushPath, pushpaths, SelectedPushValve2Pos, SelectedPushValve3Pos, PushRate);
                    break;
                default:
                    pumpingStr = "Unknown pumping type";
                    break;
            }
            return pumpingStr;
        }
    }
    #endregion Fluidics Steps

    #region Imaging Steps
    public class ImagingStep : RecipeStepBase
    {
        public List<ImagingRegion> Regions { get; set; }
        public bool IsAutoFocusOn { get; set; }
 
        public enum SequenceRead
        {
            Read1,
            Index1,
            Index2,
            Read2,
            None,
        };

        public SequenceRead Read { get; set; } = SequenceRead.Read1;

        public ImagingStep()
        {
            StepType = RecipeStepTypes.Imaging;
            Regions = new List<ImagingRegion>();
        }
        public override string ToString()
        {
            string regions = "";
            for (int i = 0; i < Regions.Count; i++)
            {
                if(Regions[0].Lane < 1 )
                {
                    regions += Regions[i].RegionIndex.ToString();
                }
                else
                {
                    string loc = "[" + Regions[i].Lane.ToString() + " " + Regions[i].Column.ToString() + " " + Regions[i].Row.ToString() + "]";
                    regions += loc;
                    
                }
                if (i < Regions.Count - 1)
                {
                    regions += ", ";
                }
            }
            return string.Format("Capture Image; Regions:[{0}], Auto focus:{1}", regions, IsAutoFocusOn);
        }
    }

    public class ImagingRegion
    {
        public int Lane { get ; set ; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int RegionIndex { get; set; }
        public List<ImagingSetting> Imagings { get; set; } = new List<ImagingSetting>();
        public List<FocusSetting> ReferenceFocuses { get; set; } = new List<FocusSetting>();
    }
    public class ImagingSetting
    {
        public ImagingChannels Channels { get; set; }
        public double RedExposureTime { get; set; }
        public double GreenExposureTime { get; set; }
        public double WhiteExposureTime { get; set; }
        public uint RedIntensity { get; set; }
        public uint GreenIntensity { get; set; }
        public uint WhiteIntensity { get; set; }
        public FilterTypes Filter { get; set; }
        public ImagingSetting()
        {
            Channels = ImagingChannels.Green;
        }
    }
    public class FocusSetting
    {
        public string Name { get; set; }
        public double Position { get; set; }
    }
    #endregion Imaging Steps
    public class MoveStageStep : RecipeStepBase
    {
        public int Region { get; set; }
        public MoveStageStep()
        {
            StepType = RecipeStepTypes.MoveStage;
        }
        public override string ToString()
        {
            return string.Format("Move Stage: region={0}", Region);
        }
    }

    public class MoveStageStepRev2 : RecipeStepBase
    {
        public int Column { get; set; }
        public int Lane { get; set; }
        public int Row { get; set; }
        public MoveStageStepRev2()
        {
            StepType = RecipeStepTypes.MoveStageRev2;
        }
        public override string ToString()
        {
            return string.Format("Move Stage: Lane={0}, Row={1}, Column={2}", Lane, Row, Column);
        }
    }
    

    #region Misc Steps
    public class LoopStep : RecipeStepBase
    {
        public string LoopName { get; set; }
        public int LoopCycles { get; set; }
        public int LoopCounts { get; set; }
        public LoopStep()
        {
            StepType = RecipeStepTypes.Loop;
            LoopName = "Looping";
            LoopCycles = 3;
            LoopCounts = 0;
        }
        public override string ToString()
        {
            return string.Format("Loop: name={0}, cycles={1}, loop counts={2}", LoopName, LoopCycles, LoopCounts);
        }
    }

    public class RunRecipeStep : RecipeStepBase
    {
        public string RecipePath { get; set; }
        public string RecipeName { get; set; }
        public RunRecipeStep()
        {
            StepType = RecipeStepTypes.RunRecipe;
            RecipePath = string.Empty;
        }
        public override string ToString()
        {
            if (string.IsNullOrEmpty(RecipeName))
            {
                return string.Format("Run Recipe: {0}", RecipePath);
            }
            else 
            {

                return $"Run Recipe ({RecipeName}): {RecipePath}";
            }
        }
    }

    public class WaitingStep : RecipeStepBase
    {
        public double Time { get; set; }
        public bool ResetPump { get; set; }
        public WaitingStep()
        {
            StepType = RecipeStepTypes.Waiting;
            Time = 30;
            ResetPump = true;
        }
        public override string ToString()
        {
            return string.Format("Waiting: {0} seconds, Reset Pump Position: {1}", Time, ResetPump);
        }
    }

    public class CommentStep : RecipeStepBase
    {
        public string Comment { get; set; }
        public CommentStep()
        {
            StepType = RecipeStepTypes.Comment;
        }
        public override string ToString()
        {
            return string.Format("Comment: {0}", Comment);
        }
    }

    #endregion Misc Steps

    #region Hywire inhouse tests steps
    public class HomeMotionStep : RecipeStepBase
    {
        public MotionTypes MotionType { get; set; }
        public double Speed { get; set; }
        public bool WaitForComplete { get; set; }
        public HomeMotionStep()
        {
            StepType = RecipeStepTypes.HomeMotion;
            MotionType = MotionTypes.XStage;
            Speed = 5;
            WaitForComplete = true;
        }
        public override string ToString()
        {
            return string.Format("Home Motion: {0}, Speed={1}, Wait for complete={2}", MotionType, Speed, WaitForComplete);
        }
    }
    public class AbsoluteMoveStep : RecipeStepBase
    {
        public MotionTypes MotionType { get; set; }
        public double Speed { get; set; }
        public double TargetPos { get; set; }
        public bool WaitForComplete { get; set; }
        public AbsoluteMoveStep()
        {
            StepType = RecipeStepTypes.AbsoluteMove;
            MotionType = MotionTypes.XStage;
            Speed = 5;
            TargetPos = 0;
            WaitForComplete = true;
        }
        public override string ToString()
        {
            return string.Format("Absolute Move Motion: {0}, Speed={1}, Target Pos={2}, Wait for complete={3}", MotionType, Speed, TargetPos, WaitForComplete);
        }
    }
    public class RelativeMoveStep : RecipeStepBase
    {
        public MotionTypes MotionType { get; set; }
        public double Speed { get; set; }
        public double MoveStep { get; set; }
        public bool WaitForComplete { get; set; }
        public RelativeMoveStep()
        {
            StepType = RecipeStepTypes.RelativeMove;
            MotionType = MotionTypes.XStage;
            Speed = 5;
            MoveStep = 1;
            WaitForComplete = true;
        }
        public override string ToString()
        {
            return string.Format("Relative Move Motion: {0}, Speed={1}, Move Step={2}, Wait for complete={3}", MotionType, Speed, MoveStep, WaitForComplete);
        }
    }
    public class HywireImagingStep : RecipeStepBase
    {
        public string CameraSN { get; set; }
        public double ExposureTime { get; set; }
        public int Gain { get; set; }
        public int ADCBitDepth { get; set; }
        public int PixelBitDepth { get; set; }
        public Int32Rect ROI { get; set; }
        public LEDTypes LED { get; set; }
        public int Intensity { get; set; }
        public HywireImagingStep()
        {
            StepType = RecipeStepTypes.HywireImaging;
        }
        public override string ToString()
        {
            return string.Format("Hywire Imaging: Camera SN={0}, Exposure={1}, Gain={2}, ADC BitDepth={3}, PixelBitDepth={4}, ROI={5}, LED={6}, Intensity={7}",
                CameraSN, ExposureTime, Gain, ADCBitDepth, PixelBitDepth, ROI, LED, Intensity);
        }
    }
    public class LEDControlStep : RecipeStepBase
    {
        public LEDTypes LED { get; set; }
        public int Intensity { get; set; }
        public bool SetOn { get; set; }
        public LEDControlStep()
        {
            StepType = RecipeStepTypes.LEDCtrl;
            LED = LEDTypes.Green;
            Intensity = 50;
            SetOn = true;
        }
        public override string ToString()
        {
            return string.Format("LED Control: Type={0}, Intensity={1}, SetOn={2}", LED, Intensity, SetOn);
        }
    }
    #endregion Hywire inhouse tests steps
}
