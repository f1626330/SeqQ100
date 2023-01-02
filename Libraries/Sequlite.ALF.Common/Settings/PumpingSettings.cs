using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public enum ModeOptions
    {
        AspirateDispense,
        Aspirate,
        Dispense,
        Pull,
        Push,
        PullPush,
    }

    //Json serialization
    public enum PathOptions
    {
        FC,
        Waste,
        Bypass,
        BypassPrime,
        Test1,
        Test2,
        Test3,
        Test4,
        TestBypass1,
        TestBypass2,
        FCLane1,
        FCLane2,
        FCLane3,
        FCLane4,
        FCL1L2,
        FCL2L3,
        Manual
    }

    
    public class PumpingSettings
    {
        #region Public Properties
        public ValveSolution SelectedSolution { get; set; }
        public ModeOptions SelectedMode { get; set; }
        public PathOptions SelectedPullPath { get; set; }
        public PathOptions SelectedPushPath { get; set; }
        public double PullRate { get; set; }
        public double PushRate { get; set; }
        public double PumpingVolume { get; set; }
        public double PullDelayTime { get; set; }


        // properties for alf 2.0
        /// <summary>
        /// For alf2.0, the available paths are FC & Waste
        /// </summary>
        public bool[] PumpPullingPaths { get; set; }
        public bool[] PumpPushingPaths { get; set; }
        public int SelectedPullValve2Pos { get; set; }
        public int SelectedPullValve3Pos { get; set; }
        public int SelectedPushValve2Pos { get; set; }
        public int SelectedPushValve3Pos { get; set; }

        public PumpingSettings()
        {
            PumpPullingPaths = new bool[4];
            PumpPushingPaths = new bool[4];
        }
        #endregion Public Properties
    }
}
