using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class MainBoardViewModel : ViewModelBase
    {
        #region Private Fields
        Mainboard _MainBoard;
        Thread _QueryingThread;

        private string _DeviceType;
        private string _HWVersion;
        private string _FWVersion;
        private uint _GLEDIntensityGet;
        private uint _GLEDIntensitySet = 50;
        private bool _IsGLEDOnGet;
        private bool _IsGLEDOnSet;
        private uint _RLEDIntensityGet;
        private uint _RLEDIntensitySet = 50;
        private bool _IsRLEDOnGet;
        private bool _IsRLEDOnSet;
        private uint _WLEDIntensityGet;
        private uint _WLEDIntensitySet = 50;
        private bool _IsWLEDOnGet;
        private bool _IsWLEDOnSet;

        private bool _IsDoorSnsrOn;

        private uint _PDValue;

        private bool _IsGLEDSelected = true;
        private bool _IsRLEDSelected;
        private bool _IsWLEDSelected;

        private MainBoardController _MBController;
        private LEDController _LEDController;
        private Chiller _Chiller;

        private string _MotionFWVersion;

        private bool _IsFrontLEDAvailable;
        private int _FrontRLEDIntensity;
        private int _FrontGLEDIntensity;
        private int _FrontBLEDIntensity;
        private static ISeqLog _Logger = SeqLogFactory.GetSeqFileLog("EUI");
        #endregion Private Fields
        ChemistryViewModel ChemistryVM { get; }
        FluidicsViewModel FluidicsVM { get; }

        #region Constructor
        public MainBoardViewModel(bool isRve2, ChemistryViewModel chemistryVM, FluidicsViewModel fluidicsVM)
        {
            IsMachineRev2 = isRve2;

            ChemistryVM = chemistryVM;
            FluidicsVM = fluidicsVM;
            _MainBoard = Mainboard.GetInstance();
            _MainBoard.OnUpdateStatus += _MainBoard_OnUpdateStatus;
            _MainBoard.OnLEDStatusChanged += _MainBoard_OnLEDStatusChanged;

            _QueryingThread = new Thread(QueryingProcess);
            _QueryingThread.Name = "Mainboard QueryingProcess";
            _QueryingThread.IsBackground = true;
            _QueryingThread.Start();

            _MBController = MainBoardController.GetInstance();
            _LEDController = LEDController.GetInstance();
            _Chiller = Chiller.GetInstance();

            if (IsMachineRev2)
            {
                DeviceType = "ALF 2.0";
                HWVersion = MainBoardController.HWVersion;
                FWVersion = MainBoardController.FWVersion;

                IsFrontLEDAvailable = _MBController.IsProtocolRev2;
            }
        }

        private void _MainBoard_OnLEDStatusChanged(LEDTypes led, bool status)
        {
            switch (led)
            {
                case LEDTypes.Green:
                    _IsGLEDOnSet = status;
                    RaisePropertyChanged(nameof(IsGLEDOnSet));
                    break;
                case LEDTypes.Red:
                    _IsRLEDOnSet = status;
                    RaisePropertyChanged(nameof(IsRLEDOnSet));
                    break;
                case LEDTypes.White:
                    _IsWLEDOnSet = status;
                    RaisePropertyChanged(nameof(IsWLEDOnSet));
                    break;
            }
        }

        private void _MainBoard_OnUpdateStatus(MBProtocol.Registers startReg, int regNumbers)
        {
            MBProtocol.Registers reg;
            for (int i = 0; i < regNumbers; i++)
            {
                reg = (MBProtocol.Registers)((int)startReg + i);
                switch (reg)
                {
                    case MBProtocol.Registers.DeviceType:
                        DeviceType = MainBoard.DeviceType;
                        break;
                    case MBProtocol.Registers.HWVersion:
                        HWVersion = MainBoard.HWVersion;
                        break;
                    case MBProtocol.Registers.FWVersion:
                        FWVersion = MainBoard.FWVersion;
                        break;
                    case MBProtocol.Registers.GLEDIntensity:
                        GLEDIntensityGet = MainBoard.GLEDIntensity;
                        break;
                    case MBProtocol.Registers.GLEDStatus:
                        IsGLEDOnGet = MainBoard.IsGLEDOn;
                        break;
                    case MBProtocol.Registers.RLEDIntensity:
                        RLEDIntensityGet = MainBoard.RLEDIntensity;
                        break;
                    case MBProtocol.Registers.RLEDStatus:
                        IsRLEDOnGet = MainBoard.IsRLEDOn;
                        break;
                    case MBProtocol.Registers.OnOffInputs:
                        IsDoorOpened = MainBoard.OnOffInputs.IsDoorOpen;
                        break;
                    case MBProtocol.Registers.ChemiTemperCtrlSwitch:
                        ChemistryVM.IsChemiTemperCtrlOnGet = MainBoard.IsChemiTemperCtrlOn;
                        break;
                    case MBProtocol.Registers.ChemiTemperCtrlRamp:
                        ChemistryVM.ChemiTemperCtrlRampGet = MainBoard.ChemiTemperCtrlRamp;
                        break;
                    case MBProtocol.Registers.ChemiTemperCtrlPower:
                        ChemistryVM.ChemiTemperCtrlPowerGet = MainBoard.ChemiTemperCtrlPower;
                        break;
                    case MBProtocol.Registers.ChemiTemper:
                        ChemistryVM.ChemiTemperGet = MainBoard.ChemiTemper;
                        break;
                    case MBProtocol.Registers.HeatSinkTemper:
                        ChemistryVM.HeatSinkTemper = MainBoard.HeatSinkTemper;
                        break;
                    case MBProtocol.Registers.CoolerTemper:
                        ChemistryVM.CoolerTemperGet = MainBoard.CoolerTemper;
                        break;
                    case MBProtocol.Registers.AmbientTemper:
                        ChemistryVM.AmbientTemper = MainBoard.AmbientTemper;
                        break;
                    case MBProtocol.Registers.PDValue:
                        PDValue = MainBoard.PDValue;
                        break;
                    case MBProtocol.Registers.WLEDIntensity:
                        WLEDIntensityGet = MainBoard.WLEDIntensity;
                        break;
                    case MBProtocol.Registers.WLEDStatus:
                        IsWLEDOnGet = MainBoard.IsWLEDOn;
                        break;
                    case MBProtocol.Registers.TemperCtrlPro:
                        ChemistryVM.TemperCtrlKp = MainBoard.TemperCtrlPro;
                        break;
                    case MBProtocol.Registers.TemperCtrlInt:
                        ChemistryVM.TemperCtrlKi = MainBoard.TemperCtrlInt;
                        break;
                    case MBProtocol.Registers.TemperCtrlDif:
                        ChemistryVM.TemperCtrlKd = MainBoard.TemperCtrlDif;
                        break;
                    case MBProtocol.Registers.TemperCtrlPower:
                        ChemistryVM.TemperCtrlMaxCrnt = MainBoard.TemperCtrlPower;
                        break;
                }
            }
        }
        #endregion Constructor

        #region Public Properties
        public bool IsMachineRev2 { get; }
        public Mainboard MainBoard { get { return _MainBoard; } }
        public MainBoardController MainBoardController { get { return _MBController; } }
        public LEDController LEDController { get { return _LEDController; } }
        public Chiller Chiller { get { return _Chiller; } }

        public string DeviceType
        {
            get { return _DeviceType; }
            set
            {
                if (_DeviceType != value)
                {
                    _DeviceType = value;
                    RaisePropertyChanged(nameof(DeviceType));
                }
            }
        }
        public string HWVersion
        {
            get { return _HWVersion; }
            set
            {
                if (_HWVersion != value)
                {
                    _HWVersion = value;
                    RaisePropertyChanged("HWVersion");
                }
            }
        }
        public string FWVersion
        {
            get { return _FWVersion; }
            set
            {
                if (_FWVersion != value)
                {
                    _FWVersion = value;
                    RaisePropertyChanged(nameof(FWVersion));
                }
            }
        }
        public uint GLEDIntensityGet
        {
            get { return _GLEDIntensityGet; }
            set
            {
                if (_GLEDIntensityGet != value)
                {
                    _GLEDIntensityGet = value;
                    RaisePropertyChanged(nameof(GLEDIntensityGet));
                }
            }
        }
        public uint GLEDIntensitySet
        {
            get { return _GLEDIntensitySet; }
            set
            {
                if (_GLEDIntensitySet != value)
                {
                    if (value < SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.Green].LimitLow ||
                        value > SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.Green].LimitHigh)
                    {
                        MessageBox.Show("Intensity out of range");
                    }
                    else
                    {
                        _GLEDIntensitySet = value;
                    }
                    RaisePropertyChanged(nameof(GLEDIntensitySet));
                }
            }
        }
        public bool IsGLEDOnGet
        {
            get { return _IsGLEDOnGet; }
            set
            {
                if (_IsGLEDOnGet != value)
                {
                    _IsGLEDOnGet = value;
                    RaisePropertyChanged(nameof(IsGLEDOnGet));
                }
            }
        }
        public bool IsGLEDOnSet
        {
            get { return _IsGLEDOnSet; }
            set
            {
                if (_IsGLEDOnSet != value)
                {
                    if (!IsMachineRev2)
                    {
                        if (MainBoard.SetLEDStatus(LEDTypes.Green, value))
                        {
                            _IsGLEDOnSet = value;
                        }
                    }
                    else
                    {
                        if (LEDController.SetLEDStatus(LEDTypes.Green, value))
                        {
                            _IsGLEDOnSet = value;
                        }
                    }
                    RaisePropertyChanged(nameof(IsGLEDOnSet));
                }
            }
        }
        public uint RLEDIntensityGet
        {
            get { return _RLEDIntensityGet; }
            set
            {
                if (_RLEDIntensityGet != value)
                {
                    _RLEDIntensityGet = value;
                    RaisePropertyChanged(nameof(RLEDIntensityGet));
                }
            }
        }
        public uint RLEDIntensitySet
        {
            get { return _RLEDIntensitySet; }
            set
            {
                if (_RLEDIntensitySet != value)
                {
                    if (value < SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.Red].LimitLow ||
                        value > SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.Red].LimitHigh)
                    {
                        MessageBox.Show("Intensity out of range");
                    }
                    else
                    {
                        _RLEDIntensitySet = value;
                    }
                    RaisePropertyChanged(nameof(RLEDIntensitySet));
                }
            }
        }
        public bool IsRLEDOnGet
        {
            get { return _IsRLEDOnGet; }
            set
            {
                if (_IsRLEDOnGet != value)
                {
                    _IsRLEDOnGet = value;
                    RaisePropertyChanged(nameof(IsRLEDOnGet));
                }
            }
        }
        public bool IsRLEDOnSet
        {
            get { return _IsRLEDOnSet; }
            set
            {
                if (_IsRLEDOnSet != value)
                {
                    if (!IsMachineRev2)
                    {
                        if (MainBoard.SetLEDStatus(LEDTypes.Red, value))
                        {
                            _IsRLEDOnSet = value;
                        }
                    }
                    else
                    {
                        if (LEDController.SetLEDStatus(LEDTypes.Red, value))
                        {
                            _IsRLEDOnSet = value;
                        }
                    }
                    RaisePropertyChanged(nameof(IsRLEDOnSet));
                }
            }
        }
        public uint WLEDIntensityGet
        {
            get { return _WLEDIntensityGet; }
            set
            {
                if (_WLEDIntensityGet != value)
                {
                    _WLEDIntensityGet = value;
                    RaisePropertyChanged(nameof(WLEDIntensityGet));
                }
            }
        }
        public uint WLEDIntensitySet
        {
            get { return _WLEDIntensitySet; }
            set
            {
                if (_WLEDIntensitySet != value)
                {
                    if (value < SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.White].LimitLow ||
                        value > SettingsManager.ConfigSettings.LEDIntensitiesRange[LEDTypes.White].LimitHigh)
                    {
                        MessageBox.Show("Intensity out of range");
                    }
                    else
                    {
                        _WLEDIntensitySet = value;
                    }
                    RaisePropertyChanged(nameof(WLEDIntensitySet));
                }
            }
        }
        public bool IsWLEDOnGet
        {
            get { return _IsWLEDOnGet; }
            set
            {
                if (_IsWLEDOnGet != value)
                {
                    _IsWLEDOnGet = value;
                    RaisePropertyChanged(nameof(IsWLEDOnGet));
                }
            }
        }
        public bool IsWLEDOnSet
        {
            get { return _IsWLEDOnSet; }
            set
            {
                if (_IsWLEDOnSet != value)
                {
                    if (!IsMachineRev2)
                    {
                        if (MainBoard.SetLEDStatus(LEDTypes.White, value))
                        {
                            _IsWLEDOnSet = value;
                        }
                    }
                    else
                    {
                        if (LEDController.SetLEDStatus(LEDTypes.White, value))
                        {
                            _IsWLEDOnSet = value;
                        }
                    }
                    RaisePropertyChanged(nameof(IsWLEDOnSet));
                }
            }
        }
        public uint PDValue
        {
            get { return _PDValue; }
            set
            {
                if (_PDValue != value)
                {
                    _PDValue = value;
                    RaisePropertyChanged(nameof(PDValue));
                }
            }
        }
        public double ChemiTemperGet
        {
            get { return ChemistryVM.ChemiTemperGet; }
        }
        public double HeatSinkTemper
        {
            get { return ChemistryVM.HeatSinkTemper; }
        }
        public double CoolerTemper
        {
            get { return ChemistryVM.CoolerTemperGet; }
        }
        public double AmbientTemper
        {
            get { return ChemistryVM.AmbientTemper; }
        }
        public bool IsDoorOpened
        {
            get { return _IsDoorSnsrOn; }
            set
            {
                if (_IsDoorSnsrOn != value)
                {
                    _IsDoorSnsrOn = value;
                    RaisePropertyChanged(nameof(IsDoorOpened));
                    RaisePropertyChanged(nameof(DoorStatus));
                }
            }
        }
        public string DoorStatus
        {
            get
            {
                if (!MainBoard.IsConnected) { return "N/A"; }
                else if (IsDoorOpened) return "Opened";
                else return "Closed";
            }
        }
        public bool IsGLEDSelected
        {
            get { return _IsGLEDSelected; }
            set
            {
                if (_IsGLEDSelected != value)
                {
                    _IsGLEDSelected = value;
                    RaisePropertyChanged(nameof(IsGLEDSelected));
                }
            }
        }
        public bool IsRLEDSelected
        {
            get { return _IsRLEDSelected; }
            set
            {
                if (_IsRLEDSelected != value)
                {
                    _IsRLEDSelected = value;
                    RaisePropertyChanged(nameof(IsRLEDSelected));
                }
            }
        }
        public bool IsWLEDSelected
        {
            get { return _IsWLEDSelected; }
            set
            {
                if (_IsWLEDSelected != value)
                {
                    _IsWLEDSelected = value;
                    RaisePropertyChanged(nameof(IsWLEDSelected));
                }
            }
        }

        public string ChillerFWVersion
        {
            get { return Chiller.FWVersion; }
        }
        public string FluidFWVersion
        {
            get { return FluidController.GetInstance().FWVersion; }
        }
        public string LEDFWVersion
        {
            get { return LEDController.FWVersion; }
        }
        public string MotionFWVersion
        {
            get { return _MotionFWVersion; }
            set
            {
                if (_MotionFWVersion != value)
                {
                    _MotionFWVersion = value;
                    RaisePropertyChanged(nameof(MotionFWVersion));
                }
            }
        }
        public bool IsFrontLEDAvailable
        {
            get => _IsFrontLEDAvailable;
            set
            {
                if (_IsFrontLEDAvailable != value)
                {
                    _IsFrontLEDAvailable = value;
                    RaisePropertyChanged(nameof(IsFrontLEDAvailable));
                }
            }
        }
        public int FrontRLEDIntensity
        {
            get => _FrontRLEDIntensity;
            set
            {
                if (_FrontRLEDIntensity != value)
                {
                    _FrontRLEDIntensity = value;
                    RaisePropertyChanged(nameof(FrontRLEDIntensity));
                }
            }
        }
        public int FrontGLEDIntensity
        {
            get => _FrontGLEDIntensity;
            set
            {
                if (_FrontGLEDIntensity != value)
                {
                    _FrontGLEDIntensity = value;
                    RaisePropertyChanged(nameof(FrontGLEDIntensity));
                }
            }
        }
        public int FrontBLEDIntensity
        {
            get => _FrontBLEDIntensity;
            set
            {
                if (_FrontBLEDIntensity != value)
                {
                    _FrontBLEDIntensity = value;
                    RaisePropertyChanged(nameof(FrontBLEDIntensity));
                }
            }
        }
        #endregion Public Properties

        #region Set LED Intensity Command
        private RelayCommand _SetLEDIntensityCmd;
        public ICommand SetLEDIntensityCmd
        {
            get
            {
                if (_SetLEDIntensityCmd == null)
                {
                    _SetLEDIntensityCmd = new RelayCommand(ExecuteSetLEDIntensityCmd, CanExecuteSetLEDIntensityCmd);
                }
                return _SetLEDIntensityCmd;
            }
        }

        private void ExecuteSetLEDIntensityCmd(object obj)
        {
            LEDTypes type = (LEDTypes)obj;
            uint intensity = 0;
            if (type == LEDTypes.Green)
            {
                intensity = GLEDIntensitySet;
            }
            else if (type == LEDTypes.Red)
            {
                intensity = RLEDIntensitySet;
            }
            else if (type == LEDTypes.White)
            {
                intensity = WLEDIntensitySet;
            }
            if (!IsMachineRev2)
            {
                MainBoard.SetLEDIntensity(type, intensity);
            }
            else
            {
                LEDController.SetLEDIntensity(type, (int)intensity);
            }
        }

        private bool CanExecuteSetLEDIntensityCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                return _MainBoard.IsConnected;
            }
            else
            {
                return _LEDController.IsConnected;
            }
        }
        #endregion Set LED Intensity Command

        #region Get PD Value Command
        private RelayCommand _GetPDValueCmd;
        public ICommand GetPDValueCmd
        {
            get
            {
                if (_GetPDValueCmd == null)
                {
                    _GetPDValueCmd = new RelayCommand(ExecuteGetPDValueCmd, CanExecuteGetPDValueCmd);
                }
                return _GetPDValueCmd;
            }
        }

        private void ExecuteGetPDValueCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                MainBoard.GetPDValue();
            }
            else
            {
                if (LEDController.GetPDValue())
                {
                    PDValue = (uint)LEDController.PDValue;
                }
            }
        }

        private bool CanExecuteGetPDValueCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                return _MainBoard.IsConnected;
            }
            else
            {
                return _LEDController.IsConnected;
            }
        }
        #endregion Get PD Value Command

        #region Set Front LED Command
        private RelayCommand _SetFrontLEDCmd;
        public RelayCommand SetFrontLEDCmd
        {
            get
            {
                if (_SetFrontLEDCmd == null)
                {
                    _SetFrontLEDCmd = new RelayCommand(ExecuteSetFrontLEDCmd, CanExecuteSetFrontLEDCmd);
                }
                return _SetFrontLEDCmd;
            }
        }

        private void ExecuteSetFrontLEDCmd(object obj)
        {
            MainBoardController.SetFrontPanelLEDs(FrontRLEDIntensity, FrontGLEDIntensity, FrontBLEDIntensity);
        }

        private bool CanExecuteSetFrontLEDCmd(object obj)
        {
            return true;
        }
        #endregion Set Front LED Command

        private void QueryingProcess()
        {
            double currentTime = DateTime.Now.ToOADate();
            ChemistryVM.StartTime = currentTime;
            int interval = (ChemistryVM.SampleInterval >= 0.5) ? (int)(ChemistryVM.SampleInterval * 1000) - 150 : 1000;
            try
            {
                while (true)
                {
                    if (!ChemistryVM.IsLogging)
                        continue;

                    if (IsMachineRev2)
                    {
                        currentTime = DateTime.Now.ToOADate();
                        double x = (currentTime - ChemistryVM.StartTime) * 24 * 3600;
                        if (MainBoardController.IsConnected)
                        {
                            // querying FC Temper Ctrl Ramp, FC Temper Ctrl Power, FC Temper, FC heatsink temper, MB PCB Temper, FC clamp & FC door status
                            bool queryMainBoardOK = false;
                            if (MainBoardController.IsProtocolRev2)
                            {
                                queryMainBoardOK = MainBoardController.ReadRegisters(MainBoardController.Registers.ChemiTemper, 4);
                            }
                            else
                            {
                                queryMainBoardOK = MainBoardController.ReadRegisters(MainBoardController.Registers.ChemiTemperRamp, 6);
                            }
                            if (queryMainBoardOK)
                            {
                                ChemistryVM.ChemiTemperCtrlRampGet = MainBoardController.ChemiTemperRamp;
                                ChemistryVM.HeatSinkTemper = MainBoardController.HeatSinkTemper;
                                if (MainBoardController.HeatSinkTemper > 100)
                                {
                                    TemperatureController.GetInstance().SetControlSwitch(false);
                                    _Logger.LogError($"Heat Sink Temp too high: {MainBoardController.HeatSinkTemper}");
                                }
                                ChemistryVM.AmbientTemper = MainBoardController.AmbientTemper;
                                FluidicsVM.IsFCClamped = MainBoardController.FCClampStatus;
                                if (MainBoardController.IsProtocolRev2)
                                {
                                    if (MotionControl.MotionController.GetInstance().FCDoorIsOpen == false)
                                    {
                                        FluidicsVM.IsFCDoorClosed = false;
                                    }
                                    else
                                    {
                                        FluidicsVM.IsFCDoorClosed = true;
                                    }
                                }
                                else
                                {
                                    FluidicsVM.IsFCDoorClosed = !MainBoardController.FCDoorStatus;
                                }
                                ChemistryVM.HeatSinkTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, HeatSinkTemper));
                                ChemistryVM.AmbientTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, AmbientTemper));

                                if(MainBoardController.IsProtocolRev2)
                                {
                                    ChemistryVM.ChemiTemperGet = MainBoardController.ChemiTemper;
                                    if (ChemistryVM.ChemiTemperGet > 100)
                                    {
                                        TemperatureController.GetInstance().SetControlSwitch(false);
                                        _Logger.LogError($"FC Temp too high: {ChemistryVM.ChemiTemperGet}");
                                    }
                                    ChemistryVM.ChemiTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, ChemiTemperGet));
                                }
                            }
                        }
                        if (TemperatureController.GetInstance().IsConnected && !MainBoardController.IsProtocolRev2)
                        {
                            if (TemperatureController.GetInstance().GetTemper())
                            {
                                ChemistryVM.ChemiTemperGet = TemperatureController.GetInstance().CurrentTemper;
                                if (TemperatureController.GetInstance().CurrentTemper > 100)
                                {
                                    TemperatureController.GetInstance().SetControlSwitch(false);
                                    _Logger.LogError($"FC Temp too high: {ChemistryVM.ChemiTemperGet}");
                                }
                                ChemistryVM.ChemiTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, ChemiTemperGet));
                            }
                        }
                        if (_Chiller.IsConnected)
                        {
                            // querying Chiller Temper, Heatsink Temper, PCB Temper, Cartridge present & Cartridge Door status
                            if (_Chiller.ReadRegisters(Chiller.Registers.ChillerTemper, 4))
                            {
                                ChemistryVM.CoolerTemperSet = _Chiller.ChillerTargetTemperature;
                                ChemistryVM.CoolerTemperGet = _Chiller.ChillerTemper;
                                //ChemistryVM.HeatSinkTemper = _Chiller.HeatSinkTemper;
                                FluidicsVM.IsCartridgePresented = !_Chiller.CartridgePresent;
                                ((FluidicsViewModelV2)FluidicsVM).ChillerDoorClosed = !_Chiller.CartridgeDoor;
                                ChemistryVM.CoolerTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, CoolerTemper));
                                ChemistryVM.CoolerHeatSinkTemperLine.AppendAsync(TheDispatcher, new Point(x, _Chiller.HeatSinkTemper));
                            }
                        }
                        if((FluidicsVM as FluidicsViewModelV2).IsPreheatLogEnabled && (FluidicsVM as FluidicsViewModelV2).IsFluidPreheatAvailable)
                        {
                            if (MainBoardController.GetFluidPreHeatingTemp())
                            {
                                ChemistryVM.FluidPreheatTemperLine.AppendAsync(TheDispatcher, new Point(x, MainBoardController.FluidPreHeatingCrntTemper));
                            }
                        }

                        Thread.Sleep(interval);
                    } //end rev2
                    else //rev1
                    {
                        if (_MainBoard.IsConnected)
                        {
                            if (_MainBoard.GetOnOffStatus())
                            {
                                Thread.Sleep(10);
                                FluidicsVM.IsCartridgePresented = MainBoard.OnOffInputs.IsCartridgeSnsrOn;
                                IsDoorOpened = MainBoard.OnOffInputs.IsDoorOpen;
                                FluidicsVM.IsFCClamped = MainBoard.OnOffInputs.IsFCClampSnsrOn;
                                FluidicsVM.IsFCDoorClosed = MainBoard.OnOffInputs.IsFCSensorOn;
                                FluidicsVM.IsOverflowSensorOn = MainBoard.OnOffInputs.IsOvflowSnsrOn;
                            }
                            if (_MainBoard.Query(MBProtocol.Registers.ChemiTemper, 4, true))
                            {
                                Thread.Sleep(10);
                                currentTime = DateTime.Now.ToOADate();
                                double x = (currentTime - ChemistryVM.StartTime) * 24 * 3600;
                                ChemistryVM.ChemiTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, ChemiTemperGet));
                                ChemistryVM.HeatSinkTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, HeatSinkTemper));
                                ChemistryVM.CoolerTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, CoolerTemper));
                                ChemistryVM.AmbientTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, AmbientTemper));
                            }
                            if (_MainBoard.Query(MBProtocol.Registers.ChemiTemperCtrlRamp, true))
                            {
                                Thread.Sleep(10);
                                ChemistryVM.ChemiTemperCtrlRampGet = MainBoard.ChemiTemperCtrlRamp;
                            }
                        }

                        Thread.Sleep(interval);
                    }
                                        
                    TheDispatcher.Invoke(new Action(() =>
                    {
                        if (ChemistryVM.AmbientTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.AmbientTemperLine.Collection.RemoveAt(0);
                        if (ChemistryVM.ChemiTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.ChemiTemperLine.Collection.RemoveAt(0);
                        if (ChemistryVM.CoolerTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.CoolerTemperLine.Collection.RemoveAt(0);
                        if (ChemistryVM.FluidPreheatTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.FluidPreheatTemperLine.Collection.RemoveAt(0);
                        if (ChemistryVM.HeatSinkTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.HeatSinkTemperLine.Collection.RemoveAt(0);
                        if (ChemistryVM.CoolerHeatSinkTemperLine.Collection.Count > ChemistryVM.LoggingDataCount)
                            ChemistryVM.CoolerHeatSinkTemperLine.Collection.RemoveAt(0);
                    }));
                }
            }
            catch (Exception ex)
            {
                _Logger.LogError($"QueryingProcess Error: {ex}");
            }
        }
    }
}
