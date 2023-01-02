using Sequlite.ALF.Common;
using Sequlite.ALF.SerialPeripherals;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Sequlite.ALF.Fluidics
{
    public enum FluidicsVersion
    {
        V1,
        V2
    }

    public delegate void PumpingCompletedEven(ThreadBase sender,
        ThreadBase.ThreadExitStat exitState);

    public delegate void UpdateSolutionVolHandler(int valvepos);
    public delegate void UpdatePumpStatusHandler(bool _isOn, int solution);
    public interface IFluidics
    {
        event PumpingCompletedEven OnPumpingCompleted;
        event EventHandler<EventArgs> OnPumpingInProgress;
        event EventHandler<ComponentConnectionEventArgs> OnConnectionUpdated;
        event UpdateSolutionVolHandler OnSolutionVolUpdated;
        event UpdatePumpStatusHandler OnPumpStatusUpdated;

        bool IsConnected { get; }
        List<ValveSolution> Solutions { get; }
        List<PumpMode> Modes { get; }
        List<PathOptions> Paths { get; }

        IPump Pump { get; }
        IValve Valve { get; }
        IValve SmartValve2 { get; }
        IValve SmartValve3 { get; }
       
        FluidControllerStatus GetFluidControllerStatus();
        bool Connect();
        void RunPumping(Dispatcher callingDispather, double posToVolFactor, 
            PumpingSettings settings, bool joinCallerThread, bool IsSimulation);
        void StopPumping();
        void WaitForPumpingCompleted(int waitMs = 100);
    }
}

