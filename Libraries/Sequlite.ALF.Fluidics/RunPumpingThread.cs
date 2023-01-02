using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using Sequlite.ALF.Common;

namespace Sequlite.ALF.Fluidics
{
    internal class RunPumpingThread : ThreadBase
    {
        #region Private Fields
        private Dispatcher _CallingDispatcher;
        private IPump _Pump;
        private double _PumpPosToVolFactor;
        private IValve _Valve;
        private PumpingSettings _PumpingSettings;
        private bool _IsProcessCanceled;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("Pumping Thread");
        #endregion Private Fields

        #region Constructor
        public RunPumpingThread(Dispatcher callingDispather,
                                IFluidics fluidicsInterface,
                                //PumpController pump,
                                double posToVolFactor,
                                //ValveController valve,
                                PumpingSettings settings)
        {
            _CallingDispatcher = callingDispather;
            _Pump = fluidicsInterface.Pump;
            _PumpPosToVolFactor = posToVolFactor;
            _Valve = fluidicsInterface.Valve;
            _PumpingSettings = settings;
        }
        #endregion Constructor

        #region Public Functions
        public override void Initialize()
        {

        }
        public override void ThreadFunction()
        {
            int vtrycounts = 0;
            // if in simulation, do not check current position
            while (_Valve.CurrentPos != _PumpingSettings.SelectedSolution.ValveNumber && !IsSimulationMode)
            {
                _Valve.SetToNewPos(_PumpingSettings.SelectedSolution.ValveNumber, true);
                if (++vtrycounts > 5)
                {
                    Logger.Log("Selector Valve Trycount > 5");
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
                if (vtrycounts == 3)
                {
                    if (!_Valve.ResetValve())
                    {
                        Logger.Log("Reset Valve Failed");
                        ExitStat = ThreadExitStat.Error;
                        return;
                    }
                }
            }
            if(_PumpingSettings.PumpingVolume == 0) { Logger.Log("Volume 0, set selector valve only"); return; }
            //_Valve.SetToNewPos(_PumpingSettings.SelectedSolution.ValveNumber, true);
            Thread.Sleep(100);
            switch (_PumpingSettings.SelectedMode)
            {
                case ModeOptions.AspirateDispense:
                    _PumpingSettings.SelectedPullPath = PathOptions.FC;
                    _PumpingSettings.SelectedPushPath = PathOptions.Waste;
                    RunPullPush();
                    break;
                case ModeOptions.Aspirate:
                    _PumpingSettings.SelectedPullPath = PathOptions.FC;
                    RunPull();
                    break;
                case ModeOptions.Dispense:
                    _PumpingSettings.SelectedPushPath = PathOptions.Waste;
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
        }
        public override void Finish()
        {
            if (!IsSimulationMode)
            {
                _Pump.GetPumpPos();
            }
        }
        public override void AbortWork()
        {
            //_Pump.TerminateAction();
            _IsProcessCanceled = true;
            _Pump.CancelPumping();
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
            Stopwatch sw = Stopwatch.StartNew();
            // 1. read the pump position, calculate the loop cycles to aspirate and dispense
            _Pump.GetPumpPos();
            double startVol = (3000 - _Pump.PumpAbsolutePos) / _PumpPosToVolFactor;
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
                loops = (int)((_PumpingSettings.PumpingVolume - startVol) / (3000 / _PumpPosToVolFactor));       // syringer full vol is 3000 pulses
                endVol = _PumpingSettings.PumpingVolume - startVol - (3000 / _PumpPosToVolFactor) * loops;
            }

            if (startVol > 0)
            {
                // 2.1 set pull path
                if(!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true))
                {
                    if(!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true)){Logger.LogError("Pump Valve error");return;}
                }
                Thread.Sleep(100);
                // 2.2 set pull speed
                if(!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true))
                {
                    if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true)) { Logger.LogError("Pump speed set error"); return; }
                }
                Thread.Sleep(100);
                // 2.3 set pull volume
                if(!_Pump.SetPumpRelPos((int)(startVol * _PumpPosToVolFactor), true))
                {
                    if(!_Pump.SetPumpRelPos((int)(startVol * _PumpPosToVolFactor), true)) { Logger.LogError("Pump move error"); return; }
                }
                // do not sleep if we are in simulation
                if (!IsSimulationMode)
                    Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
                if (_IsProcessCanceled) { return; }
            }
            // 2.4 set pump to home pos if more volume is required
            if (loops > 0 || endVol > 0)
            {
                if(!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true))
                {
                    if(!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true)) { Logger.LogError("Pump Valve error"); return; }
                }
                Thread.Sleep(100);
                if(!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true))
                {
                    if(!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true)) { Logger.LogError("Pump speed set error"); return; }
                }
                Thread.Sleep(100);
                if(!_Pump.SetPumpRelPos(-1*_Pump.PumpAbsolutePos , true))
                {
                    if(!_Pump.SetPumpRelPos(-1 * _Pump.PumpAbsolutePos, true)) { Logger.LogError("Pump movement error"); return; }
                }
                if (_IsProcessCanceled) { return; }
            }
            // 2.5 set loops(aspirate & dispense) if needed
            if (loops > 0)
            {
                for (int i = 0; i < loops; i++)
                {
                    if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true))
                    {
                        if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true)) { Logger.LogError("Pump Valve error"); return; }
                    }
                    Thread.Sleep(100);
                    // 2.2 set pull speed
                    if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true))
                    {
                        if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true)) { Logger.LogError("Pump speed set error"); return; }
                    }
                    Thread.Sleep(100);
                    // 2.3 set pull volume
                    if (!_Pump.SetPumpRelPos((int)(500 * _PumpPosToVolFactor), true))
                    {
                        if (!_Pump.SetPumpRelPos((int)(500 * _PumpPosToVolFactor), true)) { Logger.LogError("Pump Valve error"); return; }
                    }
                    //_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true);
                    //Thread.Sleep(100);
                    //_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true);
                    //Thread.Sleep(100);
                    //_Pump.SetPumpRelPos((int)(500 * _PumpPosToVolFactor), true);
                    Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
                    if (i < loops - 1 || endVol > 0)
                    {
                        //_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true);
                        //Thread.Sleep(100);
                        //_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true);
                        //Thread.Sleep(100);
                        ////_Pump.SetPumpAbsPos(0, true);
                        //_Pump.SetPumpRelPos((int)(-500 * _PumpPosToVolFactor), true);
                        if (!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true))
                        {
                            if (!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true)){Logger.LogError("Pump Valve error"); return;}
                        }
                        Thread.Sleep(100);
                        if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true))
                        {
                            if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true)){Logger.LogError("Pump set speed error");return;}
                        }
                        Thread.Sleep(100);
                        if (!_Pump.SetPumpRelPos(-1 * _Pump.PumpAbsolutePos, true))
                        {
                            if (!_Pump.SetPumpRelPos(-1 * _Pump.PumpAbsolutePos, true)) { Logger.LogError("Pump movement error"); return; }
                        }
                    }
                    if (_IsProcessCanceled) { return; }
                }
            }
            // 2.6 pump remained volume if needed
            // but do not check if in simulation
            if (endVol > 0 && !IsSimulationMode)
            {
                //_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true);
                //Thread.Sleep(100);
                //_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true);
                //Thread.Sleep(100);
                //_Pump.SetPumpRelPos((int)(endVol * _PumpPosToVolFactor), true);
                if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true))
                {
                    if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true)){ Logger.LogError("Pump Valve error");return;}
                }
                Thread.Sleep(100);
                // 2.2 set pull speed
                if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true))
                {
                    if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true)){Logger.LogError("Pump set speed error");return;}
                }
                Thread.Sleep(100);
                // 2.3 set pull volume
                if (!_Pump.SetPumpRelPos((int)(endVol * _PumpPosToVolFactor), true))
                {
                    if (!_Pump.SetPumpRelPos((int)(endVol * _PumpPosToVolFactor), true)){Logger.LogError("Pump movement error");return;}
                }
                Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
            }
            Logger.Log($"Pump pull and push elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
            sw.Stop();
        }

        /// <summary>
        /// Pull from the selected path. stops if the syringer goes to the bottom position.
        /// </summary>
        private void RunPull()
        {
            // 1. read the pump position, calculate the legal volume
            _Pump.GetPumpPos();
            double startVol = (3000 - _Pump.PumpAbsolutePos) / _PumpPosToVolFactor;
            if (_PumpingSettings.PumpingVolume < startVol)
            {
                startVol = _PumpingSettings.PumpingVolume;
            }

            // 3. set path to selected path
            //_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true);
            if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true))
            {
                if (!_Pump.SetValvePath(_PumpingSettings.SelectedPullPath, true)) { Logger.LogError("Pump Valve error");return; }
            }
            Thread.Sleep(100);
            if (startVol > 0)
            {
                
                // 2.2 set pull speed
                if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true))
                {
                    if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true)){Logger.LogError("Pump set speed error");return;}
                }
                Thread.Sleep(100);
                // 2.3 set pull volume
                if (!_Pump.SetPumpRelPos((int)(startVol * _PumpPosToVolFactor), true))
                {
                    if (!_Pump.SetPumpRelPos((int)(startVol * _PumpPosToVolFactor), true)) {Logger.LogError("Pump movement error");return;}
                }
                //// 4. set aspirate speed
                //_Pump.SetTopFlowRate((int)(_PumpingSettings.PullRate * _PumpPosToVolFactor * 2 / 60), true);
                //Thread.Sleep(100);
                //// 5. set aspirate volume
                //_Pump.SetPumpRelPos((int)(startVol * _PumpPosToVolFactor), true);

                Thread.Sleep((int)(1000 * _PumpingSettings.PullDelayTime));
            }
        }

        /// <summary>
        /// Push to the selected path. stops if the syringer goes to the top position.
        /// </summary>
        private void RunPush()
        {
            // 1. read the pump position, calculate the legal volume
            _Pump.GetPumpPos();
            double startVol = _Pump.PumpAbsolutePos / _PumpPosToVolFactor;
            if (_PumpingSettings.PumpingVolume < startVol)
            {
                startVol = _PumpingSettings.PumpingVolume;
            }

            // 3. set path to selected path
            //_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true);
            //Thread.Sleep(100);
            if (!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true))
            {
                if (!_Pump.SetValvePath(_PumpingSettings.SelectedPushPath, true)) { Logger.LogError("Set Valve error"); return; }
            }
            Thread.Sleep(100);
            if (startVol > 0)
            {
                //// 4. set dispense speed
                //_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true);
                //Thread.Sleep(100);
                //// 5. set dispense volume
                //_Pump.SetPumpRelPos((int)(-1 * startVol * _PumpPosToVolFactor), true);
                
                if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true))
                {
                    if (!_Pump.SetTopFlowRate((int)(_PumpingSettings.PushRate * _PumpPosToVolFactor * 2 / 60), true)) { Logger.LogError("Pump speed set error"); return; }
                }
                Thread.Sleep(100);
                if (!_Pump.SetPumpRelPos((int)(-1 * startVol * _PumpPosToVolFactor), true))
                {
                    if (!_Pump.SetPumpRelPos((int)(-1 * startVol * _PumpPosToVolFactor), true)) { Logger.LogError("Pump push error"); return; }
                }
            }
        }

        #endregion Private Functions

    }

}
