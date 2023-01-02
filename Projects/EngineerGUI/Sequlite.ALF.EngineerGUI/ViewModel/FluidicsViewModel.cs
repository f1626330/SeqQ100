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
using System.Reflection;
using System.ServiceModel.Channels;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public abstract class FluidicsViewModel : ViewModelBase
    {
        protected ISeqLog Logger { get; }  = SeqLogFactory.GetSeqFileLog("UI");
        #region Private Fields
        //common private fields
        private ValveSolution _SelectedSolution;
        private PumpMode _SelectedMode;
        private double _AspRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
        private double _DispRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
        private double _PumpVolume = SettingsManager.ConfigSettings.FluidicsStartupSettings.Volume;
        private bool _IsPumpRunning;
        private bool _IsCartridgeLoaded;
        private bool _IsCartridgeSnsrOn;
        //private bool _IsFCClampSensorOn;
        //private bool _IsShowCartridgeMotorWindow;
        private int _PumpActualPos;
        private int _PumpAbsolutePos;
        private bool _IsMovingCartridge;
        private int _ValvePos;
        private int _CurrentVolume;
        private bool _IsFCClampSnsrOn;
        private bool _IsFCSnsrOn;
        private bool _IsOverflowSensorOn;
        private bool _IsFCLoaded=true;
        private Chiller _Chiller;
        #endregion Private Fields

        #region Public Properties
        //common properties
        public bool IsSimulation { get; set; }
        public List<ValveSolution> SolutionOptions { get; set; }
        public List<PumpMode> ModeOptions { get; private set; }
        public ObservableCollection<int> SolutionVolumes { get; private set; }

        public ValveSolution SelectedSolution
        {
            get { return _SelectedSolution; }
            set
            {
                if (_SelectedSolution != value)
                {
                    _SelectedSolution = value;
                    RaisePropertyChanged(nameof(SelectedSolution));
                }
            }
        }
        //public FluidController FluidController
        //{
        //    get { return FluidicsInterface.FluidController; }
        //}

        public abstract IFluidics FluidicsInterface { get; protected set; }
        public IPump Pump { get; protected set; }
        public IValve SmartValve2 { get; protected set; }
        public IValve SmartValve3 { get; protected set; }

        public bool IsCartridgePresented
        {
            get { return _IsCartridgeSnsrOn; }
            set
            {
                if (_IsCartridgeSnsrOn != value)
                {
                    _IsCartridgeSnsrOn = value;
                    RaisePropertyChanged(nameof(IsCartridgePresented));
                }
            }

        }
        public bool IsFCDoorClosed
        {
            get { return _IsFCSnsrOn; }
            set
            {
                if (_IsFCSnsrOn != value)
                {
                    _IsFCSnsrOn = value;
                    RaisePropertyChanged(nameof(IsFCDoorClosed));
                }
            }
        }

        public bool IsOverflowSensorOn
        {
            get { return _IsOverflowSensorOn; }
            set
            {
                if (_IsOverflowSensorOn != value)
                {
                    _IsOverflowSensorOn = value;
                    RaisePropertyChanged(nameof(IsOverflowSensorOn));
                }
            }
        }
        public bool IsFCClamped
        {
            get { return _IsFCClampSnsrOn; }
            set
            {
                if (_IsFCClampSnsrOn != value)
                {
                    _IsFCClampSnsrOn = value;
                    RaisePropertyChanged(nameof(IsFCClamped));
                }
            }
        }
       
        public virtual PumpMode SelectedMode
        {
            get { return _SelectedMode; }
            set
            {
                if (_SelectedMode != value)
                {
                    _SelectedMode = value;
                    Pump.SelectedMode = value.Mode;
                    switch (value.Mode)
                    {
                        case Common.ModeOptions.AspirateDispense: 
                            SelectedPath = PathOptions.FC;
                            break;
                        case Common.ModeOptions.Aspirate:
                            SelectedPath = PathOptions.FC;
                            break;
                        case Common.ModeOptions.Dispense:
                            SelectedPath = PathOptions.Waste;
                            break;
                        case Common.ModeOptions.Pull:
                            SelectedPath = PathOptions.Bypass;
                            break;
                        case Common.ModeOptions.Push:
                            SelectedPath = PathOptions.Waste;
                            break;
                    }
                    RaisePropertyChanged(nameof(SelectedMode));
                    RaisePropertyChanged(nameof(SelectedPath));
                    //RaisePropertyChanged(nameof(IsAllowPathChange));
                }
            }
        }
        public virtual PathOptions SelectedPath
        {
            get { return Pump.SelectedPath; }
            set
            {
                if (Pump.SelectedPath != value)
                {
                    try
                    {
                        Pump.SelectedPath = value;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Invalid Setting...");
                    }

                    
                }
            }
        }
        public IValve Valve { get { return FluidicsInterface.Valve; } }
        public SettingRange PumpAspRateRange
        {
            get { return SettingsManager.ConfigSettings.PumpAspRateRange; }
        }
        public SettingRange PumpDispRateRange
        {
            get { return SettingsManager.ConfigSettings.PumpDispRateRange; }
        }
        public SettingRange PumpVolRange
        {
            get { return SettingsManager.ConfigSettings.PumpVolRange; }
        }
        public SettingRange ChemiTemperRange
        {
            get { return SettingsManager.ConfigSettings.ChemiTemperRange; }
        }
        public SettingRange ChemiTemperRampRange
        {
            get { return SettingsManager.ConfigSettings.ChemiTemperRampRange; }
        }
        public double AspRate
        {
            get { return _AspRate; }
            set
            {
                if (_AspRate != value)
                {
                    _AspRate = value;
                    RaisePropertyChanged(nameof(AspRate));
                }
            }
        }
        public double DispRate
        {
            get { return _DispRate; }
            set
            {
                if (_DispRate != value)
                {
                    _DispRate = value;
                    RaisePropertyChanged(nameof(DispRate));
                }
            }
        }
        public double PumpVolume
        {
            get { return _PumpVolume; }
            set
            {
                if (_PumpVolume != value)
                {
                    _PumpVolume = value;
                    RaisePropertyChanged(nameof(PumpVolume));
                }
            }
        }
      
        public bool IsPumpRunning
        {
            get { return _IsPumpRunning; }
            set
            {
                if (_IsPumpRunning != value)
                {
                    _IsPumpRunning = value;
                    RaisePropertyChanged(nameof(IsPumpRunning));
                }
            }
        }
        public bool IsCartridgeLoaded
        {
            get { return _IsCartridgeLoaded; }
            set
            {
                if (_IsCartridgeLoaded != value)
                {
                    _IsCartridgeLoaded = value;
                    RaisePropertyChanged(nameof(IsCartridgeLoaded));

                    IsMovingCartridge = false;
                }

                // reduce cartridge load motor current to avoid high temperature of the motor
                if (MotionControllerDevice?.IsCartridgeAvailable == true)
                {
                    FluidicsViewModelV2 vmThis = this as FluidicsViewModelV2;
                    if (vmThis != null)
                    {
                        MotionControllerDevice?.HywireMotionController.SetMotionDriveCurrent(Hywire.MotionControl.MotorTypes.Motor_Z,
                            new Hywire.MotionControl.MotionDriveCurrent[] { Hywire.MotionControl.MotionDriveCurrent.Percent50 });
                    }
                }
            }
        }
        //public bool IsShowCartridgeMotorWindow
        //{
        //    get { return _IsShowCartridgeMotorWindow; }
        //    set
        //    {
        //        if (_IsShowCartridgeMotorWindow != value)
        //        {
        //            _IsShowCartridgeMotorWindow = value;
        //            RaisePropertyChanged(nameof(IsShowCartridgeMotorWindow));

        //            if (value == true)
        //            {
        //                CartridgeMotorWindow cartridgeWind = new CartridgeMotorWindow();
        //                cartridgeWind.DataContext = MotionVM;
        //                cartridgeWind.ShowDialog();
        //                IsShowCartridgeMotorWindow = false;
        //            }
        //        }
        //    }
        //}
        public int PumpActualPos
        {
            get { return _PumpActualPos; }
            set
            {
                if (_PumpActualPos != value)
                {
                    _PumpActualPos = value;
                    RaisePropertyChanged(nameof(PumpActualPos));
                }
            }
        }
        public int PumpAbsolutePos
        {
            get { return _PumpAbsolutePos; }
            set
            {
                if (_PumpAbsolutePos != value)
                {
                    _PumpAbsolutePos = value;
                    RaisePropertyChanged(nameof(PumpAbsolutePos));
                }
            }
        }
        public bool IsMovingCartridge
        {
            get { return _IsMovingCartridge; }
            set
            {
                if (_IsMovingCartridge != value)
                {
                    _IsMovingCartridge = value;
                    RaisePropertyChanged(nameof(IsMovingCartridge));
                }
            }
        }
        private bool _IsFluidChecking;
        public bool IsFluidChecking
        {
            get { return _IsFluidChecking; }
            set
            {
                if (_IsFluidChecking != value)
                {
                    _IsFluidChecking = value;
                    RaisePropertyChanged(nameof(IsFluidChecking));
                }
            }
        }
        public int ValvePos
        {
            get { return _ValvePos; }
            set
            {
                if (_ValvePos != value)
                {
                    _ValvePos = value;
                    RaisePropertyChanged(nameof(ValvePos));
                }
            }
        }

        public int CurrentVolume
        {
            get { return _CurrentVolume; }
            set
            {
                if (_CurrentVolume != value)
                {
                    _CurrentVolume = value;
                    RaisePropertyChanged(nameof(CurrentVolume));
                }
            }
        }

        public bool IsFCLoaded
        {
            get { return _IsFCLoaded; }
            set
            {
                if (_IsFCLoaded != value)
                {
                    _IsFCLoaded = value;
                    RaisePropertyChanged(nameof(IsFCLoaded));
                }
            }
        }

        #endregion Public Properties

        public abstract void UpdateStatusFromFluidController(double xTime);
        protected MotionController MotionControllerDevice { get; set; }
        public ChemistryViewModel ChemistryVM { get; }
        #region Constructor
        protected FluidicsViewModel(ChemistryViewModel chemistryViewModel)
        {
            ChemistryVM = chemistryViewModel;
            //MotionControllerDevice = MotionController.GetInstance();
        }
       
        public virtual void Initialize(IFluidics fluidicsInterface, MotionController motionController)
        {
            FluidicsInterface = fluidicsInterface;
            Pump = FluidicsInterface.Pump;
            SmartValve2 = FluidicsInterface.SmartValve2;
            SmartValve3 = FluidicsInterface.SmartValve3;

            MotionControllerDevice = motionController;
            SolutionOptions = new List<ValveSolution>(FluidicsInterface.Solutions);
            SolutionVolumes = new ObservableCollection<int>();
            RaisePropertyChanged("SolutionOptions");
            RaisePropertyChanged("SolutionVolumes");
            const int intialVolumes = 0;
            foreach (ValveSolution solution in SolutionOptions)
            {
                SolutionVolumes.Add(intialVolumes);
            }
            SelectedSolution = SolutionOptions[0];


            ModeOptions = new List<PumpMode>();
            RaisePropertyChanged("ModeOptions");
            ModeOptions.Add(FluidicsInterface.Modes[0]);
            ModeOptions.Add(FluidicsInterface.Modes[1]);
            ModeOptions.Add(FluidicsInterface.Modes[2]);
            ModeOptions.Add(FluidicsInterface.Modes[3]);
            ModeOptions.Add(FluidicsInterface.Modes[4]);
            SelectedMode = ModeOptions[0];

            _Chiller = Chiller.GetInstance();
            //_Chiller.OnMotorStatusChanged += _Chiller_OnMotorStatusChanged;
        }

        private void _Chiller_OnMotorStatusChanged(Chiller.CartridgeStatusTypes status)
        {
            switch (status)
            {
                case Chiller.CartridgeStatusTypes.Loaded:
                    IsCartridgeLoaded = true;
                    break;
                case Chiller.CartridgeStatusTypes.Unloaded:
                    IsCartridgeLoaded = false;
                    break;
            }
        }
        #endregion Constructor

        // com
        #region Set Solution Command
        private RelayCommand _SetSolutionCmd;
        public ICommand SetSolutionCmd
        {
            get
            {
                if (_SetSolutionCmd == null)
                {
                    _SetSolutionCmd = new RelayCommand(ExecuteSetSolutionCmd, CanExecuteSetSolutionCmd);
                }
                return _SetSolutionCmd;
            }
        }

        private void ExecuteSetSolutionCmd(object obj)
        {
           
            if (!Valve.IsConnected)
            {
                MessageBox.Show("The Valve is not connected!");
                return;
            }
            Valve.SetToNewPos(SelectedSolution.ValveNumber, true);
            //FluidicsManager.Valve.RequestValveStatus();
            ValvePos = Valve.CurrentPos;

        }

        private bool CanExecuteSetSolutionCmd(object obj)
        {
            return true;
        }
        #endregion Set Solution Command

        #region Run Pump Cmd
        private RelayCommand _RunPumpCmd;
        public ICommand RunPumpCmd
        {
            get
            {
                if (_RunPumpCmd == null)
                {
                    _RunPumpCmd = new RelayCommand(ExecuteRunPumpCmd, CanExecuteRunPumpCmd);
                }
                return _RunPumpCmd;
            }
        }

        protected abstract void RunPumping();
        protected abstract void StopPumping();
        private void ExecuteRunPumpCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            if (cmdPara == "start")
            {
                RunPumping();
                
            }
            else if (cmdPara == "stop")
            {
                StopPumping();
            }
        }


        private bool CanExecuteRunPumpCmd(object obj)
        {
            return true;
        }
        #endregion Run Pump Cmd

        #region Move FlowChip Command
        private RelayCommand _MoveFlowChipCmd;
        public ICommand MoveFlowChipCmd
        {
            get
            {
                if (_MoveFlowChipCmd == null)
                {
                    _MoveFlowChipCmd = new RelayCommand(ExecuteMoveFlowChipCmd, CanExecuteMoveFlowChipCmd);
                }
                return _MoveFlowChipCmd;
            }
        }
      
        protected virtual void ExecuteMoveFlowChipCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            if (cmdPara == "load")
            {
                int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                if (MotionControllerDevice.HomeMotion(MotionTypes.YStage, speed, accel, false) == false)
                {
                    MessageBox.Show("Load failed");
                    return;
                }
                IsFCLoaded = true;
            }
            else if (cmdPara == "unload")
            {
                int pos = (int)Math.Round((90 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                if (MotionControllerDevice.AbsoluteMove(MotionTypes.YStage, pos, speed, accel, false) == false)
                {
                    MessageBox.Show("Unload failed");
                    return;
                }
                IsFCLoaded = false;
            }
        }

        private bool CanExecuteMoveFlowChipCmd(object obj)
        {
            return true;
        }
        #endregion Move FlowChip Command

        #region Move Cartridge Command
        private RelayCommand _MoveCartridgeCmd;
        public ICommand MoveCartridgeCmd
        {
            get
            {
                if (_MoveCartridgeCmd == null)
                {
                    _MoveCartridgeCmd = new RelayCommand(ExecuteMoveCartridgeCmd, CanExecuteMoveCartridgeCmd);
                }
                return _MoveCartridgeCmd;
            }
        }

        private void ExecuteMoveCartridgeCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            MotionControllerDevice.HywireMotionController.SetMotionDriveCurrent(Hywire.MotionControl.MotorTypes.Motor_Z,
                new Hywire.MotionControl.MotionDriveCurrent[] { Hywire.MotionControl.MotionDriveCurrent.Percent100 });
            if (cmdPara == "unload")
            {
                if (!MotionControllerDevice.IsCartridgeAvailable)
                {
                    if (_Chiller.ChillerMotorControl(false) == false)
                    {
                        MessageBox.Show("Unload failed");
                        return;
                    }
                }
                else
                {
                    int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    if (MotionControllerDevice.HomeMotion(MotionTypes.Cartridge, speed, accel, false) == false)
                    {
                        MessageBox.Show("Unload failed");
                        return;
                    }
                }
                IsMovingCartridge = true;
            }
            else if (cmdPara == "load")
            {
                _Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
                if (!_Chiller.CartridgePresent)
                {
                    var msgResult = MessageBox.Show("Please push back the reagent more, still loading?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (msgResult == MessageBoxResult.Yes)
                    {
                    }
                    else if (msgResult == MessageBoxResult.No)
                    {
                        return;
                    }
                }
                if (!_Chiller.CartridgeDoor) // is closed
                {
                    var msgResult = MessageBox.Show("Please close the reagent door, still loading?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (msgResult == MessageBoxResult.Yes)
                    {
                    }
                    else if (msgResult == MessageBoxResult.No)
                    {
                        return;
                    }

                }

                if (!MotionControllerDevice.IsCartridgeAvailable)
                {
                    if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos) == false)
                    {
                        MessageBox.Show("Load failed");
                    }
                }
                else
                {

                    int pos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    if (MotionControllerDevice.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, false) == false)
                    {
                        MessageBox.Show("Load failed");
                        return;
                    }
                }
                IsMovingCartridge = true;
            }
        }

        private bool CanExecuteMoveCartridgeCmd(object obj)
        {
            return true;
        }
        #endregion Move Cartridge Command
        //v1
        #region Reset Solution Volume Command
        private RelayCommand _ResetVolCmd;
        public ICommand ResetVolCmd
        {
            get
            {
                if (_ResetVolCmd == null)
                {
                    _ResetVolCmd = new RelayCommand(ExecuteResetVolCmd, CanExecuteResetVolCmd);
                }
                return _ResetVolCmd;
            }
        }

        virtual protected void ExecuteResetVolCmd(object obj)
        {
            CurrentVolume = 0;
            foreach (ValveSolution solution in SolutionOptions)
            {
                FluidicsInterface.Solutions[solution.ValveNumber - 1].SolutionVol = 0;
                //SolutionVolumes[solution.ValveNumber - 1] = 0;
            }
        }

        private bool CanExecuteResetVolCmd(object obj)
        {
            return true;
        }
        #endregion Reset Solution Volume Command
        // com
        #region Reset Valve Command
        private RelayCommand _ResetValveCmd;
        public ICommand ResetValveCmd
        {
            get
            {
                if (_ResetValveCmd == null)
                {
                    _ResetValveCmd = new RelayCommand(ExecuteResetValveCmd, CanExecuteResetValveCmd);
                }
                return _ResetValveCmd;
            }
        }

        private void ExecuteResetValveCmd(object obj)
        {
            Valve.ResetValve();
        }

        private bool CanExecuteResetValveCmd(object obj)
        {
            return true;
        }
        #endregion Reset Valve Command
        //com
        #region SetValveCmd
        private RelayCommand _SetValveCmd;
        public ICommand SetValveCmd
        {
            get
            {
                if (_SetValveCmd == null)
                {
                    _SetValveCmd = new RelayCommand(ExecuteSetValveCmd, CanExecuteSetValveCmd);
                }
                return _SetValveCmd;
            }
        }

        virtual protected void ExecuteSetValveCmd(object obj)
        {
            //do nothing
        }

        //public  abstract bool Connect(Action<string> msgRecord);

        private bool CanExecuteSetValveCmd(object obj)
        {
            return true;
        }
        #endregion SetValveCmd

        protected void AddConnectionRecordMessage(string str, Action<string>  msgRecord, bool success = true)
        {

            LogAndRecordMessage(str, msgRecord, Logger, success);
        }

        protected void LogAndRecordMessage(string str, Action<string> msgRecord, ISeqLog logger, bool success = true)
        {

            if (logger != null)
            {
                if (!success)
                {
                    logger.LogError(str);
                }
                else
                {
                    logger.Log(str);
                }
            }

            if (msgRecord != null)
            {
                msgRecord(str);
            }
        }

    }
}
