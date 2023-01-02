using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Fluidics
{
    public delegate void UpdateStatusHandler(bool ispathfc);

    public interface IPump
    {
        event UpdateStatusHandler OnStatusChanged;
        ModeOptions SelectedMode { get; set; }
        PathOptions SelectedPath { get; set; }
        bool IsAllowPathChange { get; }
        bool IsConnected { get; }
        bool IsPumpingCanceled { get; }
        TechDeviceAddresses PumpAddress { get; set; }
        bool IsReady { get; }
        TechDeviceErrorCodes ErrorCode { get; }
        int CurrentValvePos { get; }
        int PumpAbsolutePos { get; }
        int PumpActualPos { get; }
        int StartPos { get; set; }
        int FinalPos { get; set; }
        bool IsPathFC { get; set; }
        bool Reconnect();
        bool Connect(TecanBaudrateOptions baudRate = TecanBaudrateOptions.Rate_9600);
        bool Connect(string portName, TecanBaudrateOptions baudrate = TecanBaudrateOptions.Rate_9600);
        bool InitializePump(bool isCCW = false, int speed = 1, int inputPort = 0, int outputPort = 1);
        bool SetMicroStepMode(bool enable);
        bool GetPumpPos();
        bool SetPumpAbsPos(int pos, bool waitEnded = false);
        bool SetPumpRelPos(int pos, bool waitEnded = false);
        bool SetValvePath(PathOptions path, bool waitEnded = false);
        bool SetTopFlowRate(int speed, bool waitEnded = false);
        void CancelPumping();
        bool SetValvePositions(bool[] setToOutput);
        bool TecanPumpMove(bool[] setToOutput, int speed, int pos, bool waitEnded = false);
        bool ReportValvePos();
    }
}
