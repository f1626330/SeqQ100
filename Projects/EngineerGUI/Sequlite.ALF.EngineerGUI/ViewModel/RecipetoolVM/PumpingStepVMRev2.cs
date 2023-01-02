using Sequlite.ALF.Fluidics;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class PumpingStepVMRev2 : StepsTreeViewModel
    {
        int _PumpingVol = RecipeStepDefaultSettings.PumpingVol;
        double _PumpPullingRate = RecipeStepDefaultSettings.PullRate;
        double _PumpPushingRate = RecipeStepDefaultSettings.PushRate;
        PathOptions _SelectedPullingPath = RecipeStepDefaultSettings.PullPath;
        PathOptions _SelectedPushingPath = RecipeStepDefaultSettings.PushPath;
        int _SelectedReagent = RecipeStepDefaultSettings.Reagent;
        private int _SelectedPullValve2Pos;
        private int _SelectedPullValve3Pos;
        private int _SelectedPushValve2Pos;
        private int _SelectedPushValve3Pos;

        PumpMode _SelectedPumpingType;

        #region Properties
        public bool IsPumpPulling
        {
            get
            {
                if (SelectedPumpingType.Mode == ModeOptions.Pull || SelectedPumpingType.Mode == ModeOptions.PullPush)
                {
                    return true;
                }
                else { return false; }
            }
        }
        public bool IsPumpPushing
        {
            get
            {
                if (SelectedPumpingType.Mode == ModeOptions.Push || SelectedPumpingType.Mode == ModeOptions.PullPush)
                {
                    return true;
                }
                else { return false; }
            }
        }
        public int PumpingVol
        {
            get { return _PumpingVol; }
            set
            {
                if (_PumpingVol != value)
                {
                    _PumpingVol = value;
                    RaisePropertyChanged(nameof(PumpingVol));
                }
            }
        }
        public double PumpPullingRate
        {
            get { return _PumpPullingRate; }
            set
            {
                if (_PumpPullingRate != value)
                {
                    _PumpPullingRate = value;
                    RaisePropertyChanged(nameof(PumpPullingRate));
                }
            }
        }
        public double PumpPushingRate
        {
            get { return _PumpPushingRate; }
            set
            {
                if (_PumpPushingRate != value)
                {
                    _PumpPushingRate = value;
                    RaisePropertyChanged(nameof(PumpPushingRate));
                }
            }
        }
        public List<PathOptions> PumpingPathOptions { get; }
        public PathOptions SelectedPullingPath
        {
            get { return _SelectedPullingPath; }
            set
            {
                if (_SelectedPullingPath != value)
                {
                    try
                    {
                        _SelectedPullingPath = value;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Invalid Setting...");
                    }



                    if (_SelectedPullingPath == PathOptions.FC || _SelectedPullingPath == PathOptions.Waste)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = _SelectedPullingPath == PathOptions.FC;
                        }
                        SelectedPullValve3Pos = 1;
                        SelectedPullValve2Pos = 6;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));

                    }
                    if (_SelectedPullingPath == PathOptions.BypassPrime)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve2Pos;
                        SelectedReagent = ReagentOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedReagent));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.Bypass)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve2Pos;
                        SelectedReagent = ReagentOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedReagent));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.Test1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.Test2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.Test3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.Test4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.TestBypass2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve2Pos;
                        SelectedReagent = ReagentOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedReagent));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.TestBypass1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve2Pos;
                        SelectedReagent = ReagentOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedReagent));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCLane1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCLane2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCLane3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCLane4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCL1L2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (_SelectedPullingPath == PathOptions.FCL2L3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PullSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    RaisePropertyChanged(nameof(SelectedPullingPath));
                    
                }
            }
        }
        public PathOptions SelectedPushingPath
        {
            get { return _SelectedPushingPath; }
            set
            {
                if (_SelectedPushingPath != value)
                {
                    try
                    {
                        _SelectedPushingPath = value;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Invalid Setting...");
                    }

                    if (_SelectedPushingPath == PathOptions.FC || _SelectedPushingPath == PathOptions.Waste)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = _SelectedPushingPath == PathOptions.FC;
                        }
                        SelectedPushValve3Pos = 1;
                        SelectedPushValve2Pos = 6;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));

                    }
                    if (_SelectedPushingPath == PathOptions.BypassPrime)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.Bypass)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.Test1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.Test2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.Test3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.Test4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.TestBypass1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.TestBypass2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCLane1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCLane2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCLane3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCLane4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCL1L2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    if (_SelectedPushingPath == PathOptions.FCL2L3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PushSyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].PumpPullingPaths[i];
                        }
                        SelectedPushValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve3Pos;
                        SelectedPushValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                    }
                    RaisePropertyChanged(nameof(SelectedPushingPath));
                    
                }
            }
        }
        public List<int> ReagentOptions { get; }
        public int SelectedReagent
        {
            get { return _SelectedReagent; }
            set
            {
                if (_SelectedReagent != value)
                {
                    _SelectedReagent = value;
                    RaisePropertyChanged(nameof(SelectedReagent));
                }
            }
        }
        public List<PumpMode> PumpingTypeOptions { get; }
        public PumpMode SelectedPumpingType
        {
            get { return _SelectedPumpingType; }
            set
            {
                if (_SelectedPumpingType != value)
                {
                    _SelectedPumpingType = value;
                    if(_SelectedPumpingType.Mode == Common.ModeOptions.Push)
                    {
                        SelectedPullingPath = PathOptions.Manual;
                    }
                    RaisePropertyChanged(nameof(SelectedPumpingType));
                    RaisePropertyChanged(nameof(IsPumpPulling));
                    RaisePropertyChanged(nameof(IsPumpPushing));
                }
            }
        }

        public List<int> Valve2PosOptions { get; }
        public List<int> Valve3PosOptions { get; }

        public int SelectedPullValve2Pos
        {
            get { return _SelectedPullValve2Pos; }
            set
            {
                if (_SelectedPullValve2Pos != value)
                {
                    _SelectedPullValve2Pos = value;
                    RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                }
            }
        }
        public int SelectedPullValve3Pos
        {
            get { return _SelectedPullValve3Pos; }
            set
            {
                if (_SelectedPullValve3Pos != value)
                {
                    _SelectedPullValve3Pos = value;
                    RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                }
            }
        }
        public int SelectedPushValve2Pos
        {
            get { return _SelectedPushValve2Pos; }
            set
            {
                if (_SelectedPushValve2Pos != value)
                {
                    _SelectedPushValve2Pos = value;
                    RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                }
            }
        }
        public int SelectedPushValve3Pos
        {
            get { return _SelectedPushValve3Pos; }
            set
            {
                if (_SelectedPushValve3Pos != value)
                {
                    _SelectedPushValve3Pos = value;
                    RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                }
            }
        }
        public ObservableCollection<bool> PullSyringeSelectFC { get; set; } = new ObservableCollection<bool>();
        public ObservableCollection<bool> PushSyringeSelectFC { get; set; } = new ObservableCollection<bool>();
        #endregion Properties
        public PumpingStepVMRev2()
        {
            PumpingTypeOptions = new List<PumpMode>();
            PumpingTypeOptions.Add(new PumpMode("Pull", ModeOptions.Pull));
            PumpingTypeOptions.Add(new PumpMode("Push", ModeOptions.Push));
            PumpingTypeOptions.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));
            SelectedPumpingType = PumpingTypeOptions[2];

            PumpingPathOptions = new List<PathOptions>();
            foreach (PathOptions path in Enum.GetValues(typeof(PathOptions)))
            {
                PumpingPathOptions.Add(path);
            }
            ReagentOptions = new List<int>();
            for (int i = 1; i < 25; i++)
            {
                ReagentOptions.Add(i);
            }
            SelectedReagent = ReagentOptions[0];
            for (int i = 0; i < 4; i++)
            {
                PullSyringeSelectFC.Add(true);
            }
            for (int i = 0; i < 4; i++)
            {
                PushSyringeSelectFC.Add(false);
            }
            Valve2PosOptions = new List<int>();
            Valve3PosOptions = new List<int>();
            for (int i = 1; i <= 6; i++)
            {
                Valve2PosOptions.Add(i);
            }
            for (int i = 1; i <= 3; i++)
            {
                Valve3PosOptions.Add(i);
            }
            SelectedPushValve2Pos = 6;
            SelectedPushValve3Pos = 1;
            SelectedPullValve2Pos = 6;
            SelectedPullValve3Pos = 1;

        }

        public PumpingStepVMRev2(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            PumpingTypeOptions = new List<PumpMode>();
            PumpingTypeOptions.Add(new PumpMode("Pull", ModeOptions.Pull));
            PumpingTypeOptions.Add(new PumpMode("Push", ModeOptions.Push));
            PumpingTypeOptions.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));

            PumpingPathOptions = new List<PathOptions>();
            foreach (PathOptions path in Enum.GetValues(typeof(PathOptions)))
            {
                PumpingPathOptions.Add(path);
            }

            ReagentOptions = new List<int>();
            for (int i = 1; i < 25; i++)
            {
                ReagentOptions.Add(i);
            }
            SelectedReagent = ReagentOptions[0];
            for (int i = 0; i < 4; i++)
            {
                PullSyringeSelectFC.Add(true);
            }
            for (int i = 0; i < 4; i++)
            {
                PushSyringeSelectFC.Add(false);
            }
            Valve2PosOptions = new List<int>();
            Valve3PosOptions = new List<int>();
            for (int i = 1; i <= 6; i++)
            {
                Valve2PosOptions.Add(i);
            }
            for (int i = 1; i <= 3; i++)
            {
                Valve3PosOptions.Add(i);
            }
            NewPumpingStep step = content.Step as NewPumpingStep;
            if (step != null)
            {
                PumpingVol = step.Volume;
                PumpPullingRate = step.PullRate;
                SelectedPullingPath = step.PullPath;
                PumpPushingRate = step.PushRate;
                SelectedPushingPath = step.PushPath;
                SelectedPumpingType = PumpingTypeOptions.Find(p => p.Mode == step.PumpingType);
                SelectedReagent = step.Reagent;
                SelectedPullValve2Pos = step.SelectedPullValve2Pos;
                SelectedPullValve3Pos = step.SelectedPullValve3Pos;
                SelectedPushValve2Pos = step.SelectedPushValve2Pos;
                SelectedPushValve3Pos = step.SelectedPushValve3Pos;
                for(int i = 0;i<4; i++)
                {
                    PullSyringeSelectFC[i] = step.PumpPullingPaths[i];
                }
                for (int i = 0; i < 4; i++)
                {
                    PushSyringeSelectFC[i] = step.PumpPushingPaths[i];
                }
            }
        }


        public override StepsTreeViewModel Clone()
        {
            PumpingStepVMRev2 clonedVm = new PumpingStepVMRev2()
            {
                SelectedPumpingType = this.SelectedPumpingType,
                SelectedPullingPath = this.SelectedPullingPath,
                SelectedPushingPath = this.SelectedPushingPath,
                SelectedReagent = this.SelectedReagent,
                PumpingVol = this.PumpingVol,
                PumpPullingRate = this.PumpPullingRate,
                PumpPushingRate = this.PumpPushingRate,
                SelectedPullValve2Pos = this.SelectedPullValve2Pos,
                SelectedPullValve3Pos = this.SelectedPullValve3Pos,
                SelectedPushValve2Pos = this.SelectedPushValve2Pos,
                SelectedPushValve3Pos = this.SelectedPushValve3Pos,
                PullSyringeSelectFC  = this.PullSyringeSelectFC,
                PushSyringeSelectFC = this.PushSyringeSelectFC,
        };
            clonedVm.SelectedPumpingType = clonedVm.PumpingTypeOptions.Find(p => p.Mode == this.SelectedPumpingType.Mode);
            return clonedVm;
        }
    }
}
