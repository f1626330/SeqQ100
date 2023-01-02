using Sequlite.ALF.Common;
using Sequlite.ALF.SerialPeripherals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Sequlite.ALF.Fluidics
{
    internal class FluidicsV1 : FluidicsBase, IFluidics
    {
        protected override FluidController FluidController { get { return null; } }

        public override IPump Pump { get; } = new PumpController();

        public override IValve SmartValve3 { get { return null; } }

        public override IValve SmartValve2 { get { return null; } }

        public override bool IsConnected { get; protected set; }
        RunPumpingThread _RunPumpingThread = null;
        private int CurrentVolume;

        public FluidicsV1()
        {
            Pump.OnStatusChanged += Pump_OnStatusChanged;
        }
        private void Pump_OnStatusChanged(bool ispathfc)
        {
            int valvepos = Valve.CurrentPos;
            if (ispathfc)
            {
                Solutions[valvepos - 1].SolutionVol = (int)Math.Round((Pump.FinalPos - Pump.StartPos) / SettingsManager.ConfigSettings.PumpIncToVolFactor + CurrentVolume);
                FireOnSolutionVolUpdateEvent(valvepos, Solutions[valvepos - 1].SolutionVol);
            }
            else
            {
                CurrentVolume = Solutions[valvepos - 1].SolutionVol;
            }
        }

        

        public override void RunPumping(Dispatcher callingDispather, double posToVolFactor, PumpingSettings settings, bool joinCallerThread, bool isSimulation)
        {
            _RunPumpingThread = new RunPumpingThread(callingDispather, this,posToVolFactor, settings);
            _RunPumpingThread.IsSimulationMode = isSimulation;
            _RunPumpingThread.Completed += _RunPumpingThread_Completed;
            _RunPumpingThread.Start();
            if (joinCallerThread)
            {
                _RunPumpingThread.Join();
            }
        }
        public override void StopPumping()
        {
            if (_RunPumpingThread != null)
            {
                _RunPumpingThread.Abort();
            }
        }
        public override void WaitForPumpingCompleted(int waitMs = 1000)
        {
            while (_RunPumpingThread != null )
            {
                FirePumpingInProgressEvent(_RunPumpingThread, null);
                Thread.Sleep(waitMs);
            }
        }
        private void _RunPumpingThread_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            Pump.GetPumpPos();
            FirePumpingCompletedEvent(sender, exitState);  
            _RunPumpingThread.Completed -= _RunPumpingThread_Completed;
            _RunPumpingThread = null;
        }

        public override bool Connect()
        {
            bool isAllDeviceConnected = true;
            FireConnectionUpdatedEvent("Pump connecting...", true, "Pump");
            Pump.Connect();
            if (Pump.IsConnected)
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "Pump");
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "Pump");
                isAllDeviceConnected = false;
            }

            FireConnectionUpdatedEvent("Valve connecting...", true, "Valve");
            Valve.Connect();
            if (Valve.IsConnected)
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "Valve");
                Valve.GetCurrentPos();
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "Valve");
                isAllDeviceConnected = false;
            }
            Pump.GetPumpPos();
            IsConnected = isAllDeviceConnected;
            return isAllDeviceConnected;
        }

        public override FluidControllerStatus GetFluidControllerStatus()
        {
            throw new NotImplementedException("FluidController Status only available in V2");
        }
    }
}
