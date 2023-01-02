using Sequlite.ALF.SerialPeripherals;
using System.Collections.Generic;
using Sequlite.ALF.Common;
using System.Windows.Threading;
using System;

namespace Sequlite.ALF.Fluidics
{
    internal abstract class FluidicsBase : IFluidics
    {
        public event PumpingCompletedEven OnPumpingCompleted;
        public event EventHandler<ComponentConnectionEventArgs> OnConnectionUpdated;
        public event EventHandler<EventArgs> OnPumpingInProgress;
        public event UpdateSolutionVolHandler OnSolutionVolUpdated;
        public event UpdatePumpStatusHandler OnPumpStatusUpdated;

        public List<ValveSolution> Solutions { get; }
        public List<PumpMode> Modes { get; }
        public List<PathOptions> Paths { get; }

        public abstract IPump Pump { get;  }
        public IValve Valve { get; }

        /// <summary>
        /// valve 2 has 6 positions.
        /// </summary>
        public abstract IValve SmartValve2 { get; }
        /// <summary>
        /// valve 3 has 3 positions.
        /// </summary>
        public abstract IValve SmartValve3 { get; }
        protected abstract FluidController FluidController { get;  }
        public abstract bool IsConnected { get; protected set; }

        public abstract FluidControllerStatus GetFluidControllerStatus();
        public abstract bool Connect();

        public abstract void RunPumping(Dispatcher callingDispather, double posToVolFactor,PumpingSettings settings, bool joinCallerThread, bool IsSimulation);
        public abstract void StopPumping();
        public  abstract void WaitForPumpingCompleted(int waitMs = 100);
        protected FluidicsBase()
        {
            Solutions = new List<ValveSolution>();
            for (int i = 0; i < 24; i++)
            {
                Solutions.Add(new ValveSolution() { DisplayName = string.Format("Solution {0}", i + 1), ValveNumber = i + 1, SolutionVol = 0 });
            }

            Modes = new List<PumpMode>();
            Modes.Add(new PumpMode("Asp.&Disp.", ModeOptions.AspirateDispense));
            Modes.Add(new PumpMode("Aspirate", ModeOptions.Aspirate));
            Modes.Add(new PumpMode("Dispense", ModeOptions.Dispense));
            Modes.Add(new PumpMode("Pull", ModeOptions.Pull));
            Modes.Add(new PumpMode("Push", ModeOptions.Push));
            Modes.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));

            Paths = new List<PathOptions>();
            Paths.Add(PathOptions.FC);
            Paths.Add(PathOptions.Waste);
            Paths.Add(PathOptions.Bypass);

            Valve = new ValveController();

        }

        protected void FireOnPumpStatusUpdateEvent(bool isOn, int solution) =>
            OnPumpStatusUpdated?.Invoke(isOn, solution);
         
        protected void FireOnSolutionVolUpdateEvent(int valvepos, int currentvol) =>
            OnSolutionVolUpdated?.Invoke(valvepos);
        protected void FirePumpingCompletedEvent(ThreadBase sender, ThreadBase.ThreadExitStat exitState) =>
            OnPumpingCompleted?.Invoke(sender, exitState);

        protected void FirePumpingInProgressEvent(ThreadBase sender, EventArgs arg) =>
           OnPumpingInProgress?.Invoke( sender, arg);

        protected void FireConnectionUpdatedEvent(string message, bool error, string name) =>
            OnConnectionUpdated?.Invoke(this, new ComponentConnectionEventArgs() { Name = name, Message = message, IsErrorMessage = error });
    }
}
