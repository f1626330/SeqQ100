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
    internal class FluidicsV2 :  FluidicsBase, IFluidics
    {
        
        RunTecanPumpingThread _RunPumpingThread = null;

        public override IPump Pump { get; } = new TecanXMP6000Pump();

        public override IValve SmartValve2 { get; } = new TecanSmartValve("SmartValve2");
        public override IValve SmartValve3 { get; } = new TecanSmartValve("SmartValve3");

        protected override FluidController FluidController { get; } = FluidController.GetInstance();
        public override bool IsConnected { get; protected set; }
        private int CurrentVolume;
        public FluidicsV2()
        {
            Pump.OnStatusChanged += TecanPump_OnStatusChanged;
        }

        private void TecanPump_OnStatusChanged(bool ispathfc)
        {
            int valvepos = Valve.CurrentPos;
            if (ispathfc)
            {
                Solutions[valvepos - 1].SolutionVol = (int)Math.Round((Pump.FinalPos - Pump.StartPos) / SettingsManager.ConfigSettings.PumpIncToVolFactor * 4 + CurrentVolume);
                FireOnSolutionVolUpdateEvent(valvepos, Solutions[valvepos - 1].SolutionVol);
            }
            else
            {
                CurrentVolume = Solutions[valvepos - 1].SolutionVol;
            }
        }
        public override void RunPumping(Dispatcher callingDispather, double posToVolFactor, PumpingSettings settings, bool joinCallerThread, bool isSimulation)
        {
            _RunPumpingThread = new RunTecanPumpingThread(callingDispather, this, posToVolFactor, settings);
            _RunPumpingThread.IsSimulationMode = isSimulation;
            _RunPumpingThread.Completed += _RunPumpingThread_Completed;
            _RunPumpingThread.OnPumpStatusUpdated += _RunPumpingThread_OnPumpStatusUpdated;
            _RunPumpingThread.Start();
            if (joinCallerThread)
            {
                if (!isSimulation)
                    _RunPumpingThread.Join();
                else
                    _RunPumpingThread?.Join(); // _RunPumpingThread may be null in simulation mode, so checking for null reference
            }
        }

        private void _RunPumpingThread_OnPumpStatusUpdated(bool _isOn, int solution)
        {
            FireOnPumpStatusUpdateEvent(_isOn, solution);
        }

        public override void StopPumping()
        {
            if (_RunPumpingThread != null)
            {
                _RunPumpingThread.Abort();
            }
        }

        public override void WaitForPumpingCompleted(int waitMs = 100)
        {
            while (_RunPumpingThread != null)
            {
                FirePumpingInProgressEvent(_RunPumpingThread, null);
                Thread.Sleep(waitMs);
            }
        }

        private void _RunPumpingThread_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            //XMP6000Pump.GetPumpPos();
            Pump.GetPumpPos();
            FirePumpingCompletedEvent(sender, exitState);
            _RunPumpingThread.Completed -= _RunPumpingThread_Completed;
            _RunPumpingThread.OnPumpStatusUpdated -= _RunPumpingThread_OnPumpStatusUpdated;
            _RunPumpingThread = null;
        }

        public override bool Connect()
        {
            bool isAllDeviceConnected = true;
            SettingsManager.ConfigSettings.PumpIncToVolFactor = 12;
            FireConnectionUpdatedEvent("Valve connecting...", true, "Valve");
            if (Valve.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.ValveSelector].PortName) == false)
            {
                Valve.Connect();
            }
            if (Valve.IsConnected)
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "Valve");
                Valve.SetToNewPos(24, true);
                Valve.GetCurrentPos();
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "Valve");
                isAllDeviceConnected = false;
            }
            var smartValve2PortName = SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.SmartValve2].PortName;
            FireConnectionUpdatedEvent("Smart Valve #2 connecting...", true, "SmartValve2-" + smartValve2PortName);
            if (SmartValve2.Connect(smartValve2PortName, 9600))
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "SmartValve2-" + smartValve2PortName);
                SmartValve2.GetCurrentPos();
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "SmartValve2-" + smartValve2PortName);
                isAllDeviceConnected = false;
            }
            var smartValve3PortName = SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.SmartValve3].PortName;
            FireConnectionUpdatedEvent("Smart Valve #3 connecting...", true, "SmartValve3-" + smartValve3PortName);
            if (SmartValve3.Connect(smartValve3PortName, 9600))
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "SmartValve3-" + smartValve3PortName);
                SmartValve3.GetCurrentPos();
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "SmartValve3-" + smartValve3PortName);
                isAllDeviceConnected = false;
            }
            //Don't initialize pump if valve failed to initialize.
            if (isAllDeviceConnected)
            {
                var pumpPortName = SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.Pump].PortName;
                FireConnectionUpdatedEvent("XMP6000 Multichannel Pump connecting...", true, "XMP6000Pump-" + pumpPortName);
                if (Pump.Connect(pumpPortName))
                {
                    FireConnectionUpdatedEvent("Succeeded.\n", true, "XMP6000Pump-" + pumpPortName);
                }
                else if (Pump.Connect())
                {
                    FireConnectionUpdatedEvent("Succeeded.\n", true, "XMP6000Pump-" + pumpPortName);
                }
                else
                {
                    FireConnectionUpdatedEvent("Failed.\n", false, "XMP6000Pump-" + pumpPortName);
                    isAllDeviceConnected = false;
                }
                Pump.GetPumpPos();
            }

            FireConnectionUpdatedEvent("Fluid controller connecting...", true, "FluidController");
            if (FluidController.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.FluidController].PortName))
            {
                FireConnectionUpdatedEvent("Succeeded.\n",true, "FluidController");
            }
            else if (FluidController.Connect())
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "FluidController");
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "FluidController");
                isAllDeviceConnected = false;
            }

            FireConnectionUpdatedEvent("RFID Reader connecting...", true, "RFID Reader-COM5");
            if (RFIDController.GetInstance().Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.RFIDReader].PortName))
            {
                FireConnectionUpdatedEvent("Succeeded.\n", true, "FluidController");
            }
            else
            {
                FireConnectionUpdatedEvent("Failed.\n", false, "FluidController");
                isAllDeviceConnected = false;
            }
            IsConnected = isAllDeviceConnected;
            return isAllDeviceConnected;
        }

        public override FluidControllerStatus GetFluidControllerStatus()
        {
            if (FluidController.IsConnected && 
                FluidController.ReadRegisters(FluidController.Registers.BufferLevel, 5))
            {
                FluidControllerStatus status = new FluidControllerStatus()
                {
                    //Pressure = FluidController.Pressure,
                    //FlowRate = FluidController.FlowRate,
                    BufferLevel = FluidController.BufferLevel,
                    WasteLevel = FluidController.WasteLevel,
                    Bubble = FluidController.Bubble,
                    Pressure = FluidController.PressureArray[9],
                    FlowRate = FluidController.FlowArray[9],
                    SipperDown = FluidController.SipperDown,
                    WasteIn = FluidController.WasteIn,
                    BufferTrayIn = FluidController.BufferTrayIn
                };
                return status;
            }
            else
            {
                return null;
            }
        }
    }
}
