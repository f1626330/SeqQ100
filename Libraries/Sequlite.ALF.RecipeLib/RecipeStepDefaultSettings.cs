using Sequlite.ALF.Fluidics;
using Sequlite.ALF.Common;

namespace Sequlite.ALF.RecipeLib
{
    public static class RecipeStepDefaultSettings
    {
        #region Set Temperature
        public static double TargetTemper { get; } = 25;
        public static double TemperTolerance { get; } = 1;
        public static int Duration { get; } = 0;
        public static bool WaitForComplete { get; } = true;
        #endregion Set Temperature

        #region Capture Image
        public static ImagingChannels CaptureChannel { get; } = ImagingChannels.Green;
        public static uint RedIntensity { get; } = 60;
        public static uint GreenIntensity { get; } = 50;
        public static double RedExposure { get; } = 0.05;
        public static double GreenExposure { get; } = 0.05;
        public static bool IsAutoFocusOn { get; } = true;
        #endregion Capture Image

        #region Pumping
        public static ModeOptions PumpingType { get; } = ModeOptions.PullPush;
        public static int PumpingVol { get; } = 200;
        public static double PullRate { get; } = 1000;
        public static double PushRate { get; } = 5000;
        public static PathOptions PullPath { get; } = PathOptions.FC;
        public static PathOptions PushPath { get; } = PathOptions.Waste;
        public static int Reagent { get; } = 0;
        #endregion Pumping

        #region New Fludics
        public static bool[] PumpPullingPaths { get; }
        public static bool[] PumpPushingPaths { get; }
        public static int SelectedValve2Pos { get; set; }
        public static int SelectedValve3Pos { get; set; }
        #endregion New Fludics

        #region Looping
        public static int LoopCycles { get; } = 10;
        public static string LoopName { get; } = "Default Loop";
        #endregion Looping

        #region Waiting
        public static int WaitingTime { get; } = 60;
        #endregion Waiting

    }
}
