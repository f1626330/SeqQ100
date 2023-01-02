using System.Threading;
using System.Windows.Diagnostics;
using System.Windows.Threading;
using Sequlite.ALF.Common;
using Sequlite.ALF.SerialPeripherals;

namespace Sequlite.ALF.Fluidics
{
    internal class RunTecanPumpingThread : ThreadBase
    {
        public delegate void UpdatePumpStatusHandler(bool _isOn, int solution);
        public event UpdatePumpStatusHandler OnPumpStatusUpdated;
        #region Private Fields
        private Dispatcher _CallingDispatcher;
        //private TecanXMP6000Pump _TecanPump;
        private IPump _TecanPump;
        
        private double _PumpPosToVolFactor;
        private IValve _Valve;
        private IValve _Valve2;
        private IValve _Valve3;
        private PumpingSettings _PumpingSettings;
        private bool _IsProcessCanceled;
        private int vtrycounts;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("2.0 Pumping Thread");

        #endregion Private Fields

        #region Constructor
        public RunTecanPumpingThread(Dispatcher callingDispather,
                                IFluidics fluidicsInterface,
                                //TecanXMP6000Pump pump,
                                double posToVolFactor,
                                //ValveController valve,
                                //TecanSmartValve valve2,
                                //TecanSmartValve valve3,
                                PumpingSettings settings)
        {
            _CallingDispatcher = callingDispather;
            _TecanPump = fluidicsInterface.Pump;// XMP6000Pump;
            _PumpPosToVolFactor = posToVolFactor;
            _Valve = fluidicsInterface.Valve;
            _Valve2 = fluidicsInterface.SmartValve2;
            _Valve3 = fluidicsInterface.SmartValve3;
            _PumpingSettings = settings;
        }
        #endregion Constructor

        #region Public Functions
        public override void Initialize()
        {

        }
        public override void ThreadFunction()
        {
            vtrycounts = 0;
            // if in simulation, do not check current position
            //Solution valve 24 pos.
            //while (FluidicsManager.Valve.CurrentPos != _PumpingSettings.SelectedSolution.ValveNumber && !IsSimulationMode)
            while (_Valve.CurrentPos != _PumpingSettings.SelectedSolution.ValveNumber && !IsSimulationMode)
            {
                _Valve.SetToNewPos(_PumpingSettings.SelectedSolution.ValveNumber, true);
                if (++vtrycounts > 5)
                {
                    Logger.LogError("Selector Valve Trycount > 5");
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
                if (vtrycounts == 3)
                {
                    if (!_Valve.ResetValve())
                    {
                        Logger.LogError("Reset Valve Failed");
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
            }
            if (_PumpingSettings.PumpingVolume == 0) { Logger.Log("Volume 0, set selector valve only"); return; }
            // Pump valve/path
            switch (_PumpingSettings.SelectedMode)
            {
                case ModeOptions.AspirateDispense:
                    RunPullPush();
                    break;
                case ModeOptions.Aspirate:
                    RunPull();
                    break;
                case ModeOptions.Dispense:
                    RunPush();
                    break;
                case ModeOptions.Pull:
                    RunPull();
                    break;
                case ModeOptions.Push:
                    RunPush();
                    break;
                case ModeOptions.PullPush:
                    RunPullPush();
                    break;
            }
            Logger.Log($"Pumping thread:{Name} exits");
        }
        public override void Finish()
        {
            _TecanPump.GetPumpPos();
        }
        public override void AbortWork()
        {
            _IsProcessCanceled = true;
            _TecanPump.CancelPumping();
            ExitStat = ThreadExitStat.Abort;
        }
        #endregion Public Functions

        #region Private Functions
        /// <summary>
        /// Pull specified volume from pull path, dispense to push path if the syringer goes to the bottom.
        /// Automatically do loops if needed.
        /// </summary>
        private void RunPullPush()
        {
            // 1. read the pump position, calculate the loop cycles to aspirate and dispense
            _TecanPump.GetPumpPos();
            double startVol = (6000 - _TecanPump.PumpAbsolutePos) / _PumpPosToVolFactor;
            int loops;
            double endVol;
            if (_PumpingSettings.PumpingVolume <= startVol)
            {
                loops = 0;
                endVol = 0;
                startVol = _PumpingSettings.PumpingVolume;
            }
            else
            {
                loops = (int)((_PumpingSettings.PumpingVolume - startVol) / (6000 / _PumpPosToVolFactor));       // syringer full vol is 3000 pulses
                endVol = _PumpingSettings.PumpingVolume - startVol - (6000 / _PumpPosToVolFactor) * loops;
            }

            if (startVol > 0)
            {
                #region Set Valves 
                // Valve 2, 6 pos
                vtrycounts = 0;
                while (_Valve2.ValvePos != _PumpingSettings.SelectedPullValve2Pos && !IsSimulationMode)
                {
                    _Valve2.SetToNewPos(_PumpingSettings.SelectedPullValve2Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                //Valve 3, 3 pos
                vtrycounts = 0;
                while (_Valve3.ValvePos != _PumpingSettings.SelectedPullValve3Pos && !IsSimulationMode)
                {
                    _Valve3.SetToNewPos(_PumpingSettings.SelectedPullValve3Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                Thread.Sleep(10);
                if (_PumpingSettings.SelectedPullPath != PathOptions.Waste && _PumpingSettings.SelectedPullPath != PathOptions.Manual)
                {
                    _TecanPump.IsPathFC = true;
                }
                #endregion Set Valves
                // 2.1 Pump movement includes speed/valve/position
                if(!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(startVol * _PumpPosToVolFactor), true))
                {
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(startVol * _PumpPosToVolFactor), true))
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                // do not sleep if we are in simulation
                if (!IsSimulationMode)
                    Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
                if (_IsProcessCanceled) { return; }
            }
            // 2.4 set pump to home pos if more volume is required
            if (loops > 0 || endVol > 0)
            {
                //push
                #region Set Valves
                // Valve 2, 6 pos
                vtrycounts = 0;
                while (_Valve2.ValvePos != _PumpingSettings.SelectedPushValve2Pos && !IsSimulationMode)
                {
                    _Valve2.SetToNewPos(_PumpingSettings.SelectedPushValve2Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                //Valve 3, 3 pos
                vtrycounts = 0;
                while (_Valve3.ValvePos != _PumpingSettings.SelectedPushValve3Pos && !IsSimulationMode)
                {
                    _Valve3.SetToNewPos(_PumpingSettings.SelectedPushValve3Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                //Thread.Sleep(10);
                if (_PumpingSettings.SelectedPushPath != PathOptions.Waste && _PumpingSettings.SelectedPushPath != PathOptions.Manual)
                {
                    _TecanPump.IsPathFC = true;
                }
                #endregion Set Valves
                if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60), (int)(-500 * _PumpPosToVolFactor), true))
                {
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60), 
                        (int)(-1 * _TecanPump.PumpAbsolutePos * _PumpPosToVolFactor), true))
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                if (_IsProcessCanceled) { return; }
            }
            // 2.5 set loops(aspirate & dispense) if needed
            if (loops > 0)
            {
                for (int i = 0; i < loops; i++)
                {
                    //pull
                    #region Set Valves
                    // Valve 2, 6 pos
                    vtrycounts = 0;
                    while (_Valve2.ValvePos != _PumpingSettings.SelectedPullValve2Pos && !IsSimulationMode)
                    {
                        _Valve2.SetToNewPos(_PumpingSettings.SelectedPullValve2Pos, false, true);
                        if (++vtrycounts > 5)
                        {
                            ExitStat = ThreadExitStat.Error;
                            return;
                        }
                    }
                    //Valve 3, 3 pos
                    vtrycounts = 0;
                    while (_Valve3.ValvePos != _PumpingSettings.SelectedPullValve3Pos && !IsSimulationMode)
                    {
                        _Valve3.SetToNewPos(_PumpingSettings.SelectedPullValve3Pos, false, true);
                        if (++vtrycounts > 5)
                        {
                            ExitStat = ThreadExitStat.Error;
                            return;
                        }
                    }
                    //Thread.Sleep(10);
                    if (_PumpingSettings.SelectedPullPath != PathOptions.Waste && _PumpingSettings.SelectedPullPath != PathOptions.Manual)
                    {
                        _TecanPump.IsPathFC = true;
                    }
                    #endregion Set Valves
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(500 * _PumpPosToVolFactor), true))
                    {
                        if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), 6000 - _TecanPump.PumpAbsolutePos, true))
                        {
                            ExitStat = ThreadExitStat.Error;
                            return;
                        }
                    }
                    Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
                    if (i < loops - 1 || endVol > 0)
                    {
                        //push
                        #region Set Valve
                        // Valve 2, 6 pos
                        vtrycounts = 0;
                        while (_Valve2.ValvePos != _PumpingSettings.SelectedPushValve2Pos && !IsSimulationMode)
                        {
                            _Valve2.SetToNewPos(_PumpingSettings.SelectedPushValve2Pos, false, true);
                            if (++vtrycounts > 5)
                            {
                                ExitStat = ThreadExitStat.Error;
                                return;
                            }
                        }
                        //Valve 3, 3 pos
                        vtrycounts = 0;
                        while (_Valve3.ValvePos != _PumpingSettings.SelectedPushValve3Pos && !IsSimulationMode)
                        {
                            _Valve3.SetToNewPos(_PumpingSettings.SelectedPushValve3Pos, false, true);
                            if (++vtrycounts > 5)
                            {
                                ExitStat = ThreadExitStat.Error;
                                return;
                            }
                        }
                        //Thread.Sleep(10);
                        if (_PumpingSettings.SelectedPushPath != PathOptions.Waste && _PumpingSettings.SelectedPushPath != PathOptions.Manual)
                        {
                            _TecanPump.IsPathFC = true;
                        }
                        #endregion Set Valves
                        if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60), (int)(-500 * _PumpPosToVolFactor), true))
                        {
                            if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60),
                                (int)(-1 * _TecanPump.PumpAbsolutePos * _PumpPosToVolFactor), true))
                            {
                                ExitStat = ThreadExitStat.Error;
                                return;
                            }
                        }
                    }
                    if (_IsProcessCanceled) { return; }
                }
            }
            // 2.6 pump remained volume if needed
            // but do not check if in simulation
            if (endVol > 0 && !IsSimulationMode)
            {
                #region Set Valves
                // Valve 2, 6 pos
                vtrycounts = 0;
                while (_Valve2.ValvePos != _PumpingSettings.SelectedPullValve2Pos && !IsSimulationMode)
                {
                    _Valve2.SetToNewPos(_PumpingSettings.SelectedPullValve2Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                //Valve 3, 3 pos
                vtrycounts = 0;
                while (_Valve3.ValvePos != _PumpingSettings.SelectedPullValve3Pos && !IsSimulationMode)
                {
                    _Valve3.SetToNewPos(_PumpingSettings.SelectedPullValve3Pos, false, true);
                    if (++vtrycounts > 5)
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                //Thread.Sleep(10);
                if (_PumpingSettings.SelectedPullPath != PathOptions.Waste && _PumpingSettings.SelectedPullPath != PathOptions.Manual)
                {
                    _TecanPump.IsPathFC = true;
                }
                #endregion Set Valves
                if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(endVol * _PumpPosToVolFactor), true))
                {
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(endVol * _PumpPosToVolFactor), true))
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
            }
        }

        /// <summary>
        /// Pull from the selected path. stops if the syringer goes to the bottom position.
        /// </summary>
        private void RunPull()
        {
            // 1. read the pump position, calculate the legal volume
            _TecanPump.GetPumpPos();
            double startVol = (6000 - _TecanPump.PumpAbsolutePos) / _PumpPosToVolFactor;
            if (_PumpingSettings.PumpingVolume < startVol)
            {
                startVol = _PumpingSettings.PumpingVolume;
            }

            // 3. set path to selected path
            #region Set Valves
            // Valve 2, 6 pos
            vtrycounts = 0;
            while (_Valve2.ValvePos != _PumpingSettings.SelectedPullValve2Pos && !IsSimulationMode)
            {
                _Valve2.SetToNewPos(_PumpingSettings.SelectedPullValve2Pos, false, true);
                if (++vtrycounts > 5)
                {
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
            }
            //Valve 3, 3 pos
            vtrycounts = 0;
            while (_Valve3.ValvePos != _PumpingSettings.SelectedPullValve3Pos && !IsSimulationMode)
            {
                _Valve3.SetToNewPos(_PumpingSettings.SelectedPullValve3Pos, false, true);
                if (++vtrycounts > 5)
                {
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
            }

            if (_PumpingSettings.SelectedPullPath != PathOptions.Waste && _PumpingSettings.SelectedPullPath != PathOptions.Manual)
            {
                _TecanPump.IsPathFC = true;
            }
            
            #endregion Set Valves
            if (_PumpingSettings.SelectedPullPath.ToString().Contains("Test"))
            {
                Thread.Sleep(7 * 1000); //Wait 7 sec before zeroing
                FluidController.GetInstance().ResetPressure();
            }

            if (startVol > 0)
            {
                OnPumpStatusUpdated?.Invoke(true, _PumpingSettings.SelectedSolution.ValveNumber);
                if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(startVol * _PumpPosToVolFactor), true))
                {
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPullingPaths, (int)(_PumpingSettings.PullRate * _PumpPosToVolFactor / 60), (int)(startVol * _PumpPosToVolFactor), true))
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
                OnPumpStatusUpdated?.Invoke(false, _PumpingSettings.SelectedSolution.ValveNumber);
                Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
            }
        }

        /// <summary>
        /// Push to the selected path. stops if the syringer goes to the top position.
        /// </summary>
        private void RunPush()
        {
            // 1. read the pump position, calculate the legal volume
            _TecanPump.GetPumpPos();
            double startVol = _TecanPump.PumpAbsolutePos / _PumpPosToVolFactor;
            if (_PumpingSettings.PumpingVolume < startVol)
            {
                startVol = _PumpingSettings.PumpingVolume;
            }
            // 3. set path to selected path
            #region Set Valve
            // Valve 2, 6 pos
            vtrycounts = 0;
            while (_Valve2.ValvePos != _PumpingSettings.SelectedPushValve2Pos && !IsSimulationMode)
            {
                _Valve2.SetToNewPos(_PumpingSettings.SelectedPushValve2Pos, false, true);
                if (++vtrycounts > 5)
                {
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
            }
            //Valve 3, 3 pos
            vtrycounts = 0;
            while (_Valve3.ValvePos != _PumpingSettings.SelectedPushValve3Pos && !IsSimulationMode)
            {
                _Valve3.SetToNewPos(_PumpingSettings.SelectedPushValve3Pos, false, true);
                if (++vtrycounts > 5)
                {
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
            }
            if (_PumpingSettings.SelectedPushPath != PathOptions.Waste && _PumpingSettings.SelectedPushPath != PathOptions.Manual)
            {
                _TecanPump.IsPathFC = true;
            }
            #endregion Set Valves
            if (startVol > 0)
            {
                if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60), (int)(-1 * startVol * _PumpPosToVolFactor), true))
                {
                    if (!_TecanPump.TecanPumpMove(_PumpingSettings.PumpPushingPaths, (int)(_PumpingSettings.PushRate * _PumpPosToVolFactor / 60),
                        (int)(-1 * startVol * _PumpPosToVolFactor), true))
                    {
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
            }
        }

        #endregion Private Functions
    }
}
