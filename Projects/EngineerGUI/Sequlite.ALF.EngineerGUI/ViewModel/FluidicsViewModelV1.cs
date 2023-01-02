using Microsoft.Research.DynamicDataDisplay.DataSources;
using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class FluidicsViewModelV1 : FluidicsViewModel
    {
        //Rev 1
        //private RunPumpingThread _RunPumpingThread;
        public List<PathOptions> AvailablePaths { get; private set; }
       
        public FluidicsViewModelV1(ChemistryViewModel chemistryViewModel) 
            : base(chemistryViewModel)
        {

        }

        public override IFluidics FluidicsInterface { get; protected set; }
        //public override PumpController Pump { get { return FluidicsInterface.Pump; } }
        //public override TecanSmartValve SmartValve2 { get { return null; } }
        //public override TecanSmartValve SmartValve3 { get { return null; } }

        public override void Initialize(IFluidics fluidicsInterface, MotionController motionController)
        {
            base.Initialize(fluidicsInterface, motionController);

            AvailablePaths = new List<PathOptions>();
            RaisePropertyChanged("AvailablePaths");
            AvailablePaths.Add(PathOptions.FC);
            AvailablePaths.Add(PathOptions.Waste);
            AvailablePaths.Add(PathOptions.Bypass);
            Pump.OnStatusChanged += Pump_OnStatusChanged;
            FluidicsInterface.OnSolutionVolUpdated += FluidicsInterface_OnSolutionVolUpdated;
        }

        private void FluidicsInterface_OnSolutionVolUpdated(int valvepos)
        {
            SolutionVolumes[valvepos - 1] = FluidicsInterface.Solutions[valvepos - 1].SolutionVol;
        }

        private void Pump_OnStatusChanged(bool ispathfc)
        {
            ValvePos = Valve.CurrentPos;
            PumpActualPos = Pump.PumpActualPos;
            PumpAbsolutePos = Pump.PumpAbsolutePos;
        }
        public override void UpdateStatusFromFluidController(double xTime)
        {
            //do nothing
        }
        protected override void ExecuteResetVolCmd(object obj)
        {
            base.ExecuteResetVolCmd(obj);
            foreach (ValveSolution solution in SolutionOptions)
            {
                //FluidicsInterface.Solutions[solution.ValveNumber - 1].SolutionVol = 0;
                SolutionVolumes[solution.ValveNumber - 1] = 0;
            }
        }
        protected override  void RunPumping()
        {
            if (!Pump.IsConnected)
            {
                MessageBox.Show("Pump is not connected!");
                return;
            }
            if ((PumpVolume < SettingsManager.ConfigSettings.PumpVolRange.LimitLow) ||
                (PumpVolume > SettingsManager.ConfigSettings.PumpVolRange.LimitHigh))
            {
                MessageBox.Show("Volume out of range!");
                return;
            }

            switch (SelectedMode.Mode)
            {
                case Sequlite.ALF.Common.ModeOptions.AspirateDispense:
                    if ((AspRate < SettingsManager.ConfigSettings.PumpAspRateRange.LimitLow) ||
                        (AspRate > SettingsManager.ConfigSettings.PumpAspRateRange.LimitHigh))
                    {
                        MessageBox.Show("Aspirate Rate out of range!");
                        return;
                    }
                    if ((DispRate < SettingsManager.ConfigSettings.PumpDispRateRange.LimitLow) ||
                        (DispRate > SettingsManager.ConfigSettings.PumpDispRateRange.LimitHigh))
                    {
                        MessageBox.Show("Dispense Rate out of range!");
                        return;
                    }

                    IsPumpRunning = true;
                    break;
                case Sequlite.ALF.Common.ModeOptions.Aspirate:
                    if ((AspRate < SettingsManager.ConfigSettings.PumpAspRateRange.LimitLow) ||
                        (AspRate > SettingsManager.ConfigSettings.PumpAspRateRange.LimitHigh))
                    {
                        MessageBox.Show("Aspirate Rate out of range!");
                        return;
                    }
                    IsPumpRunning = true;
                    break;
                case Sequlite.ALF.Common.ModeOptions.Dispense:
                    if ((DispRate < SettingsManager.ConfigSettings.PumpDispRateRange.LimitLow) ||
                        (DispRate > SettingsManager.ConfigSettings.PumpDispRateRange.LimitHigh))
                    {
                        MessageBox.Show("Dispense Rate out of range!");
                        return;
                    }
                    IsPumpRunning = true;
                    break;
                case Sequlite.ALF.Common.ModeOptions.Pull:
                    if ((AspRate < SettingsManager.ConfigSettings.PumpAspRateRange.LimitLow) ||
                        (AspRate > SettingsManager.ConfigSettings.PumpAspRateRange.LimitHigh))
                    {
                        MessageBox.Show("Pull Rate out of range!");
                        return;
                    }
                    IsPumpRunning = true;
                    break;
                case Sequlite.ALF.Common.ModeOptions.Push:
                    if ((DispRate < SettingsManager.ConfigSettings.PumpDispRateRange.LimitLow) ||
                        (DispRate > SettingsManager.ConfigSettings.PumpDispRateRange.LimitHigh))
                    {
                        MessageBox.Show("Push Rate out of range!");
                        return;
                    }
                    IsPumpRunning = true;
                    break;
            }
            PumpingSettings PumpSettings = new PumpingSettings();
            PumpSettings.PullRate = AspRate;
            PumpSettings.PushRate = DispRate;
            PumpSettings.PumpingVolume = PumpVolume;
            PumpSettings.SelectedMode = SelectedMode.Mode;
            PumpSettings.SelectedPullPath = SelectedPath;
            PumpSettings.SelectedPushPath = SelectedPath;
            PumpSettings.SelectedSolution = SelectedSolution;
            PumpSettings.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;

            FluidicsInterface.RunPumping(TheDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, false, false);
            FluidicsInterface.OnPumpingCompleted += RunPumping_Completed;
        }

        protected override void StopPumping()
        {
            //if (_RunPumpingThread != null)
            //{
            //    _RunPumpingThread.Abort();
            //}
            FluidicsInterface.StopPumping();
        }

        

        private void RunPumping_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            FluidicsInterface.OnPumpingCompleted -= RunPumping_Completed;
            Dispatch(() =>
           {
               IsPumpRunning = false;
               //Pump.GetPumpPos();
               if (exitState == ThreadBase.ThreadExitStat.Error)
               {
                   MessageBox.Show("Error occurred during pumping thread, valve failure");
               }
               //_RunPumpingThread.Completed -= _RunPumpingThread_Completed;
               //_RunPumpingThread = null;
           });
        }


    }
}
