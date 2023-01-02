using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Timers;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using Sequlite.ALF.App;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class FluidicsViewModelV2 : FluidicsViewModel
    {
        #region private fields
        //private RunTecanPumpingThread _RunTecanPumingThread;

        private int _SelectedPullValve2Pos;
        private int _SelectedPullValve3Pos;
        private int _SelectedPushValve2Pos;
        private int _SelectedPushValve3Pos;
        private double _Pressure;
        private double _FlowRate;
        private uint _BufferLevel;
        private uint _WasteLevel;
        private uint _BubbleCounts;
        private bool _BubbleDetected;
        private bool _SipperIsDown;
        private bool _WasteIn;
        private bool _BufferPresented;
        private int _CurrentValve2;
        private int _CurrentValve3;
        private string _ReagentId;
        private bool _ChillerDoorClosed;
        private bool _IsLogging;
        private Timer _LogTimer;
        private double _LogStartTime;
        //private bool _IsFCDoorAvailable;
        //private bool _IsFluidPreheatAvailable;
        private bool _IsFluidPreheatOn;
        private double _PreheatTemper;
        private bool _IsPreheatLogEnabled = true;
        private double _FluidPreheatCtrlKp;
        private double _FluidPreheatCtrlKi;
        private double _FluidPreheatCtrlKd;
        private double _FluidPreheatCtrlGain;
        private double _MassOfWaste;
        #endregion private field


        #region Public Properties
        public RFIDController RFIDReader
        {
            get
            {
                return RFIDController.GetInstance();
            }
        }
        //Rev 2 properties

        public override IFluidics FluidicsInterface { get; protected set; }
        public override PumpMode SelectedMode
        {
            get { return base.SelectedMode; }
            set
            {
                if (base.SelectedMode != value)
                {
                    base.SelectedMode = value;
                    RaisePropertyChanged(nameof(IsAllowPathChange));
                    if (base.SelectedMode.Mode == Common.ModeOptions.AspirateDispense || base.SelectedMode.Mode == Common.ModeOptions.Dispense)
                    {
                        SelectedPushValve2Pos = 6;
                        SelectedPushValve3Pos = 1;

                    }
                }
            }
        }

        public override PathOptions SelectedPath
        {
            get { return base.SelectedPath; }
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
                    if (SelectedPath == PathOptions.FC || SelectedPath == PathOptions.Waste)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SelectedPath == PathOptions.FC;
                        }
                        SelectedPullValve2Pos = 6;
                        SelectedPullValve3Pos = 1;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));

                    }
                    if (SelectedPath == PathOptions.BypassPrime)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.BypassPrime].SelectedPullValve2Pos;
                        SelectedSolution = SolutionOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedSolution));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.Bypass)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Bypass].SelectedPullValve2Pos;
                        SelectedSolution = SolutionOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedSolution));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.Test1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.Test2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.Test3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.Test4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.Test4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.TestBypass2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass2].SelectedPullValve2Pos;
                        SelectedSolution = SolutionOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos-1];
                        RaisePropertyChanged(nameof(SelectedSolution));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.TestBypass1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.TestBypass1].SelectedPullValve2Pos;
                        SelectedSolution = SolutionOptions[SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos - 1];
                        RaisePropertyChanged(nameof(SelectedSolution));
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCLane1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane1].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCLane2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCLane3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCLane4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCLane4].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCL1L2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL1L2].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    if (SelectedPath == PathOptions.FCL2L3)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SyringeSelectFC[i] = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].PumpPullingPaths[i];
                        }
                        SelectedPullValve3Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve3Pos;
                        SelectedPullValve2Pos = SettingsManager.ConfigSettings.PumpPathDefault[PathOptions.FCL2L3].SelectedPullValve2Pos;
                        RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                        RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                    }
                    RaisePropertyChanged(nameof(SelectedPath));

                }
            }


        }
        public List<SolutionVolumeTrackingViewModel> TrackingSolutionVolumes { get; }
        public bool IsAllowPathChange
        {
            get { return Pump.IsAllowPathChange; }
        }


        //public List<PathOptions> AvailablePaths { get; private set; }
        public List<PathOptions> Rev2AvailablePaths { get; private set; }
        public ObservableCollection<bool> SyringeSelectFC { get; private set; }
        //private TecanXMP6000Pump XMP6000Pump
        //{
        //    get { return FluidicsInterface.XMP6000Pump; }
        //}
        //public override PumpController Pump { get { return XMP6000Pump; } }
        //public override TecanSmartValve SmartValve2
        //{
        //    get { return FluidicsInterface.SmartValve2; }
        //}
        //public override TecanSmartValve SmartValve3
        //{
        //    get { return FluidicsInterface.SmartValve3; }
        //}

        public List<int> Valve2PosOptions { get; }
        public List<int> Valve3PosOptions { get; }
        public int SelectedPullValve2Pos
        {
            get { return _SelectedPullValve2Pos; }
            set
            {
                if (_SelectedPullValve2Pos != value)
                {
                    _SelectedPullValve2Pos = value;
                    RaisePropertyChanged(nameof(SelectedPullValve2Pos));
                }
            }
        }
        public int SelectedPullValve3Pos
        {
            get { return _SelectedPullValve3Pos; }
            set
            {
                if (_SelectedPullValve3Pos != value)
                {
                    _SelectedPullValve3Pos = value;
                    RaisePropertyChanged(nameof(SelectedPullValve3Pos));
                }
            }
        }

        public int SelectedPushValve2Pos
        {
            get { return _SelectedPushValve2Pos; }
            set
            {
                if (_SelectedPushValve2Pos != value)
                {
                    _SelectedPushValve2Pos = value;
                    RaisePropertyChanged(nameof(SelectedPushValve2Pos));
                }
            }
        }
        public int SelectedPushValve3Pos
        {
            get { return _SelectedPushValve3Pos; }
            set
            {
                if (_SelectedPushValve3Pos != value)
                {
                    _SelectedPushValve3Pos = value;
                    RaisePropertyChanged(nameof(SelectedPushValve3Pos));
                }
            }
        }
        public int CurrentValve2
        {
            get { return _CurrentValve2; }
            set
            {
                if (_CurrentValve2 != value)
                {
                    _CurrentValve2 = value;
                    RaisePropertyChanged(nameof(CurrentValve2));
                }
            }
        }
        public int CurrentValve3
        {
            get { return _CurrentValve3; }
            set
            {
                if (_CurrentValve3 != value)
                {
                    _CurrentValve3 = value;
                    RaisePropertyChanged(nameof(CurrentValve3));
                }
            }
        }
        public double Pressure
        {
            get { return _Pressure; }
            set
            {
                if (_Pressure != value)
                {
                    _Pressure = value;
                    RaisePropertyChanged(nameof(Pressure));
                }
            }
        }
        public double FlowRate
        {
            get { return _FlowRate; }
            set
            {
                if (_FlowRate != value)
                {
                    _FlowRate = value;
                    RaisePropertyChanged(nameof(FlowRate));
                }
            }
        }
        public uint BufferLevel
        {
            get { return _BufferLevel; }
            set
            {
                if (_BufferLevel != value)
                {
                    _BufferLevel = value;
                    RaisePropertyChanged(nameof(BufferLevel));
                }
            }
        }
        public uint WasteLevel
        {
            get { return _WasteLevel; }
            set
            {
                if (_WasteLevel != value)
                {
                    _WasteLevel = value;
                    RaisePropertyChanged(nameof(WasteLevel));
                }
            }
        }
        public uint BubbleCounts
        {
            get { return _BubbleCounts; }
            set
            {
                if (_BubbleCounts != value)
                {
                    _BubbleCounts = value;
                    RaisePropertyChanged(nameof(BubbleCounts));
                }
            }
        }
        public bool BubbleDetected
        {
            get { return _BubbleDetected; }
            set
            {
                if (_BubbleDetected != value)
                {
                    _BubbleDetected = value;
                    RaisePropertyChanged(nameof(BubbleDetected));
                }
            }
        }
        public bool IsSipperDown
        {
            get { return _SipperIsDown; }
            set
            {
                if (_SipperIsDown != value)
                {
                    _SipperIsDown = value;
                    RaisePropertyChanged(nameof(IsSipperDown));
                }
            }
        }
        public bool WasteIn
        {
            get { return _WasteIn; }
            set
            {
                if (_WasteIn != value)
                {
                    _WasteIn = value;
                    RaisePropertyChanged(nameof(WasteIn));
                }
            }
        }
        public bool IsBufferPresented
        {
            get { return _BufferPresented; }
            set
            {
                if (_BufferPresented != value)
                {
                    _BufferPresented = value;
                    RaisePropertyChanged(nameof(IsBufferPresented));
                }
            }
        }
        public string ReagentId
        {
            get { return _ReagentId; }
            set
            {
                if (_ReagentId != value)
                {
                    _ReagentId = value;
                    RaisePropertyChanged(nameof(ReagentId));
                }
            }
        }
        public bool ChillerDoorClosed
        {
            get { return _ChillerDoorClosed; }
            set
            {
                if (_ChillerDoorClosed != value)
                {
                    _ChillerDoorClosed = value;
                    RaisePropertyChanged(nameof(ChillerDoorClosed));
                }
            }
        }

        public ObservableDataSource<Point> PressureLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> FlowRateLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> BubbleStatusLine { get; } = new ObservableDataSource<Point>();
        public bool IsLogging
        {
            get { return _IsLogging; }
            set
            {
                if (_IsLogging != value)
                {
                    _IsLogging = value;
                    RaisePropertyChanged(nameof(IsLogging));
                    ChemistryVM.IsLogging = value;

                    if (_IsLogging)
                    {
                        // clear previous lines
                        ChemistryVM.StartTime = DateTime.Now.ToOADate();
                        ChemistryVM.AmbientTemperLine.Collection.Clear();
                        ChemistryVM.ChemiTemperLine.Collection.Clear();
                        ChemistryVM.CoolerTemperLine.Collection.Clear();
                        ChemistryVM.FluidPreheatTemperLine.Collection.Clear();
                        ChemistryVM.HeatSinkTemperLine.Collection.Clear();
                        ChemistryVM.CoolerHeatSinkTemperLine.Collection.Clear();

                        PressureLine.Collection.Clear();
                        FlowRateLine.Collection.Clear();
                        BubbleStatusLine.Collection.Clear();
                        _LogTimer = new Timer(1000);
                        _LogTimer.Elapsed += _LogTimer_Elapsed;
                        _LogTimer.AutoReset = true;
                        _LogTimer.Start();
                        _LogStartTime = DateTime.Now.ToOADate();
                    }
                    else
                    {
                        if (_LogTimer != null)
                        {
                            _LogTimer.Stop();
                            _LogTimer.Elapsed -= _LogTimer_Elapsed;
                            _LogTimer.Dispose();
                            _LogTimer = null;
                        }
                    }
                }
            }
        }
        public bool IsFCDoorAvailable{ get => MainBoardController.GetInstance().IsFCDoorAvailable; }
        public bool IsFluidPreheatAvailable{ get => MainBoardController.GetInstance().IsFluidPreheatAvailable; }
        public bool IsFluidPreheatOn
        {
            get { return _IsFluidPreheatOn; }
            set
            {
                if (_IsFluidPreheatOn != value)
                {
                    _IsFluidPreheatOn = value;
                    RaisePropertyChanged(nameof(IsFluidPreheatOn));
                    if (MainBoardController.GetInstance().IsProtocolRev2)
                    {
                        MainBoardController.GetInstance().SetFluidPreHeatingEnable(_IsFluidPreheatOn);
                    }
                    else
                    {
                        Chiller.GetInstance().SetFluidHeatingEnable(_IsFluidPreheatOn);
                    }
                }
            }
        }
        public double PreheatTemper
        {
            get { return _PreheatTemper; }
            set
            {
                if (_PreheatTemper != value)
                {
                    _PreheatTemper = value;
                    RaisePropertyChanged(nameof(PreheatTemper));
                }
            }
        }
        public bool IsPreheatLogEnabled
        {
            get => _IsPreheatLogEnabled;
            set
            {
                if (_IsPreheatLogEnabled != value)
                {
                    _IsPreheatLogEnabled = value;
                    RaisePropertyChanged(nameof(IsPreheatLogEnabled));
                    if (_IsPreheatLogEnabled == false)
                    {
                        ChemistryVM.FluidPreheatTemperLine.Collection.Clear();
                    }
                }
            }
        }
        public double FluidPreheatCtrlKp
        {
            get => _FluidPreheatCtrlKp;
            set
            {
                if (_FluidPreheatCtrlKp != value)
                {
                    _FluidPreheatCtrlKp = value;
                    RaisePropertyChanged(nameof(FluidPreheatCtrlKp));
                }
            }
        }
        public double FluidPreheatCtrlKi
        {
            get => _FluidPreheatCtrlKi;
            set
            {
                if (_FluidPreheatCtrlKi != value)
                {
                    _FluidPreheatCtrlKi = value;
                    RaisePropertyChanged(nameof(FluidPreheatCtrlKi));
                }
            }
        }
        public double FluidPreheatCtrlKd
        {
            get => _FluidPreheatCtrlKd;
            set
            {
                if (_FluidPreheatCtrlKd != value)
                {
                    _FluidPreheatCtrlKd = value;
                    RaisePropertyChanged(nameof(FluidPreheatCtrlKd));
                }
            }
        }
        public double FluidPreheatCtrlGain
        {
            get => _FluidPreheatCtrlGain;
            set
            {
                if (_FluidPreheatCtrlGain != value)
                {
                    _FluidPreheatCtrlGain = value;
                    RaisePropertyChanged(nameof(FluidPreheatCtrlGain));
                }
            }
        }
        public double MassOfWaste
        {
            get => _MassOfWaste;
            set
            {
                if (_MassOfWaste != value)
                {
                    _MassOfWaste = value;
                    RaisePropertyChanged(nameof(MassOfWaste));
                }
            }
        }
        #endregion Public Properties

        private void _LogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var currentTime = DateTime.Now.ToOADate();
            double x = (currentTime - _LogStartTime) * 24 * 3600;
            UpdateStatusFromFluidController(x);
        }


        public FluidicsViewModelV2(ChemistryViewModel chemistryViewModel) 
            : base(chemistryViewModel)
        {
            IsLogging = true;
            IsFluidChecking = true;
            TrackingSolutionVolumes = new List<SolutionVolumeTrackingViewModel>();
            for (int i = 1; i <= 24; i++)
            {
                TrackingSolutionVolumes.Add(new SolutionVolumeTrackingViewModel(i));
            }

            Valve2PosOptions = new List<int>();
            Valve3PosOptions = new List<int>();
            for (int i = 1; i <= 6; i++)
            {
                Valve2PosOptions.Add(i);
            }
            for (int i = 1; i <= 3; i++)
            {
                Valve3PosOptions.Add(i);
            }
            SelectedPullValve2Pos = Valve2PosOptions[5];
            SelectedPullValve3Pos = Valve3PosOptions[0];
            SelectedPushValve2Pos = 6;
            SelectedPushValve3Pos = 1;

            //string hwversion = MainBoardController.GetInstance().HWVersion;
            // "2.0.0.0": alf2.1, no FC door, no Fluid Preheat

            // "2.0.0.1": alf2.2, alf2.3, has FC door, no Fluid Preheat
            // "2.0.0.2": alf2.4, no FC door, no Fluid Preheat
            // "2.0.0.3": alf2.5, has FC door, has Fluid Preheat
            //if (hwversion != "2.0.0.0" && hwversion != "2.0.0.2")
            //{
            //    IsFCDoorAvailable = true;
            //}
            //else
            //{
            //    IsFCDoorAvailable = false;
            //}
            //if(hwversion != "2.0.0.0" && hwversion != "2.0.0.1" && hwversion != "2.0.0.2")
            //{
            //    IsFluidPreheatAvailable = true;
            //}
            //else
            //{
            //    IsFluidPreheatAvailable = false;
            //}
        }

        private void SmartValve2_OnPositionUpdated()
        {
            CurrentValve2 = SmartValve2.CurrentPos;
        }

        private void SmartValve3_OnPositionUpdated()
        {
            CurrentValve3 = SmartValve3.CurrentPos;
        }

        public override void Initialize(IFluidics fluidicsInterface, MotionController motionController)
        {
            Rev2AvailablePaths = new List<PathOptions>();
            foreach (PathOptions path in Enum.GetValues(typeof(PathOptions)))
            {
                Rev2AvailablePaths.Add(path);
            }
            RaisePropertyChanged("Rev2AvailablePaths");

            SyringeSelectFC = new ObservableCollection<bool>();
            for (int i = 0; i < 4; i++)
            {
                SyringeSelectFC.Add(true);
            }
            RaisePropertyChanged("SyringeSelectFC");

            base.Initialize(fluidicsInterface, motionController);

            Pump.OnStatusChanged += TecanPump_OnStatusChanged;
            SmartValve2.OnPositionUpdated += SmartValve2_OnPositionUpdated;
            SmartValve3.OnPositionUpdated += SmartValve3_OnPositionUpdated;
            FluidicsInterface.OnSolutionVolUpdated += FluidicsInterface_OnSolutionVolUpdated;
            RFIDReader.OnReceivedPacket += RFIDReader_OnReceivedPacket;
        }
        private void TecanPump_OnStatusChanged(bool ispathfc)
        {
            PumpActualPos = Pump.PumpActualPos;
            PumpAbsolutePos = Pump.PumpAbsolutePos;
            ValvePos = Valve.CurrentPos;
        }
        private void FluidicsInterface_OnSolutionVolUpdated(int valvepos)
        {
            TrackingSolutionVolumes[valvepos - 1].TrackedVolume = FluidicsInterface.Solutions[valvepos - 1].SolutionVol;
        }

        private void RFIDReader_OnReceivedPacket()
        {
            if (RFIDReader.ReadIDs.Count > 0)
            {
                ReagentId = RFIDReader.ReadIDs[0].EPC;
            }
            else
            {
                ReagentId = null;
            }
        }

        public override void UpdateStatusFromFluidController(double x)
        {
            if (FluidicsInterface == null)
            {
                return;
            }
            if (FluidController.GetInstance().IsBusyInResettingPressure)
            {
                return;
            }
            FluidControllerStatus status = FluidicsInterface.GetFluidControllerStatus();
            if (status != null)
            {
                Pressure = status.Pressure;
                FlowRate = status.FlowRate;
                BufferLevel = status.BufferLevel;
                WasteLevel = status.WasteLevel;
                BubbleCounts = status.Bubble;
                IsSipperDown = status.SipperDown;
                WasteIn = status.WasteIn;
                IsBufferPresented = status.BufferTrayIn;
                // bubble status, pressure and flowrate are sampled at 100 ms interval, so we add 10 points every seconds
                var pressureArray = new Point[10];
                var flowArray = new Point[10];
                var bubbleStatusArray = new Point[10];
                for (int i = 0; i < 10; i++)
                {
                    pressureArray[i].X = x - 0.1 * (9 - i);
                    pressureArray[i].Y = Math.Abs(FluidController.GetInstance().PressureOffset) > 0.1 ? FluidController.GetInstance().PressureArray[i] : FluidController.GetInstance().PressureArray[i] - FluidController.GetInstance().LastPressureOffset;
                    flowArray[i].X = pressureArray[i].X;
                    flowArray[i].Y = FluidController.GetInstance().FlowArray[i];
                    bubbleStatusArray[i].X = pressureArray[i].X;
                    bubbleStatusArray[i].Y = FluidController.GetInstance().BubbleStatusArray[i] ? 1 : 0;
                }
                TheDispatcher.Invoke(new Action(() =>
                {
                    int removeCount = PressureLine.Collection.Count + pressureArray.Length - ChemistryVM.LoggingDataCount;
                    for (int i = 0; i < removeCount; i++)
                    {
                        PressureLine.Collection.RemoveAt(0);
                        FlowRateLine.Collection.RemoveAt(0);
                        BubbleStatusLine.Collection.RemoveAt(0);
                    }
                    PressureLine.AppendMany(pressureArray);
                    FlowRateLine.AppendMany(flowArray);
                    BubbleStatusLine.AppendMany(bubbleStatusArray);
                }));
                //PressureLine.AppendAsync(TheDispatcher, new Point(x, Pressure));
                //FlowRateLine.AppendAsync(TheDispatcher, new Point(x, FlowRate));
                //BubbleStatusLine.AppendAsync(TheDispatcher, new Point(x, status.BubbleDetected ? 1 : 0));
            }
        }

        protected override void RunPumping()
        {
            if ((PumpVolume < SettingsManager.ConfigSettings.PumpVolRange.LimitLow) ||
                        (PumpVolume > SettingsManager.ConfigSettings.PumpVolRange.LimitHigh))
            {
                MessageBox.Show("Volume out of range!");
                return;
            }
            PumpingSettings PumpSettings = new PumpingSettings();
            PumpSettings.PullRate = AspRate;
            PumpSettings.PushRate = DispRate;
            PumpSettings.PumpingVolume = PumpVolume;
            PumpSettings.SelectedMode = SelectedMode.Mode;
            PumpSettings.SelectedSolution = SelectedSolution;
            PumpSettings.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
            PumpSettings.SelectedPullValve2Pos = SelectedPullValve2Pos;
            PumpSettings.SelectedPullValve3Pos = SelectedPullValve3Pos;
            if (PumpSettings.SelectedMode == Common.ModeOptions.Push)
            {
                PumpSettings.SelectedPushPath = SelectedPath;
                PumpSettings.SelectedPushValve2Pos = SelectedPullValve2Pos;
                PumpSettings.SelectedPushValve3Pos = SelectedPullValve3Pos;
                for (int i = 0; i < 4; i++)
                {
                    PumpSettings.PumpPushingPaths[i] = SyringeSelectFC[i];
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    PumpSettings.PumpPullingPaths[i] = SyringeSelectFC[i];
                }
                for (int i = 0; i < 4; i++)
                {
                    PumpSettings.PumpPushingPaths[i] = false;
                }
                PumpSettings.SelectedPullPath = SelectedPath;
                PumpSettings.SelectedPushPath = PathOptions.Waste;
                PumpSettings.SelectedPushValve2Pos = SelectedPushValve2Pos;
                PumpSettings.SelectedPushValve3Pos = SelectedPushValve3Pos;
            }

            #region check setting
            switch (SelectedMode.Mode)
            {
                case Common.ModeOptions.AspirateDispense:
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
            #endregion check setting

            //_RunTecanPumingThread = new RunTecanPumpingThread(TheDispatcher,
            //                                        FluidicsInterface,
            //                                        //XMP6000Pump,
            //                                        12,
            //                                        //Valve,
            //                                        //SmartValve2,
            //                                        //SmartValve3,
            //                                        PumpSettings);
            //_RunTecanPumingThread.Completed += _RunTecanPumingThread_Completed;
            //_RunTecanPumingThread.Start();
            FluidicsInterface.RunPumping(TheDispatcher, 12, PumpSettings, false, false);
            FluidicsInterface.OnPumpingCompleted += RunTecanPuming_Completed;
        }
        protected override void StopPumping()
        {

            //if (_RunTecanPumingThread != null)
            //{
            //    _RunTecanPumingThread.Abort();
            //}
            FluidicsInterface.StopPumping();
        }

        protected override void ExecuteSetValveCmd(object obj)
        {
            if (obj.ToString() == "Valve2")
            {
                SmartValve2.SetToNewPos(SelectedPullValve2Pos, false, true);
                //CurrentValve2 = SmartValve2.ValvePos;
            }
            else if (obj.ToString() == "Valve3")
            {
                SmartValve3.SetToNewPos(SelectedPullValve3Pos, false, true);
                //CurrentValve3 = SmartValve3.ValvePos;
            }
        }

        private void RunTecanPuming_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            FluidicsInterface.OnPumpingCompleted -= RunTecanPuming_Completed;
            Dispatch(() =>
            {
                IsPumpRunning = false;
                //XMP6000Pump.GetPumpPos();
                if (exitState == ThreadBase.ThreadExitStat.Error)
                {
                    if (!IsSimulation)
                    {
                        MessageBox.Show("Error occurred during pumping thread, valve failure");
                    }
                    else
                    {
                        Logger.LogError("Error occurred during pumping thread, valve failure");
                    }
                    
                }
                //_RunTecanPumingThread.Completed -= _RunTecanPumingThread_Completed;
                //_RunTecanPumingThread = null;
            });
        }

        
        private RelayCommand _ResetBubbleCountsCmd;
        public ICommand ResetBubbleCountsCmd
        {
            get
            {
                if (_ResetBubbleCountsCmd == null)
                {
                    _ResetBubbleCountsCmd = new RelayCommand(ExecuteResetBubbleCountsCmd, CanExecuteResetBubbleCountsCmd);
                }
                return _ResetBubbleCountsCmd;
            }
        }

        private void ExecuteResetBubbleCountsCmd(object obj)
        {
            FluidController.GetInstance().ResetBubbleCounts();
        }

        private bool CanExecuteResetBubbleCountsCmd(object obj)
        {
            return true;
        }


        #region Read Reagent ID
        private RelayCommand _ReadReagentIdCmd;
        public RelayCommand ReadReagentIdCmd
        {
            get
            {
                if (_ReadReagentIdCmd == null)
                {
                    _ReadReagentIdCmd = new RelayCommand(ExecuteReadReagentIdCmd, CanExecuteReadReagentIdCmd);
                }
                return _ReadReagentIdCmd;
            }
        }

        private void ExecuteReadReagentIdCmd(object obj)
        {
            RFIDReader.ReadId();
        }

        private bool CanExecuteReadReagentIdCmd(object obj)
        {
            return true;
        }
        #endregion Read Reagent ID
        public class SolutionVolumeTrackingViewModel : ViewModelBase
        {
            public SolutionVolumeTrackingViewModel(int solutionNum)
            {
                SolutionNumber = solutionNum;
            }

            private int _TrackedVolume;
            public int SolutionNumber { get; }
            public int TrackedVolume
            {
                get { return _TrackedVolume; }
                set
                {
                    if (_TrackedVolume != value)
                    {
                        _TrackedVolume = value;
                        RaisePropertyChanged(nameof(TrackedVolume));
                    }
                }
            }
        }

        protected override void ExecuteResetVolCmd(object obj)
        {
            base.ExecuteResetVolCmd(obj);
            foreach (ValveSolution solution in SolutionOptions)
            {
                //FluidicsInterface.Solutions[solution.ValveNumber - 1].SolutionVol = 0;
                TrackingSolutionVolumes[solution.ValveNumber - 1].TrackedVolume = 0;
            }
        }

        protected override void ExecuteMoveFlowChipCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            if (cmdPara == "load")
            {
                MainBoardController _MBController = MainBoardController.GetInstance();
                _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
                if (_MBController.FCClampStatus)
                {
                    MessageBox.Show("Please load FC and clamp it");
                    return;
                }
                int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed);
                int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel);
                int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed);
                int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel);
                if (!MotionControllerDevice.HomeMotion(MotionTypes.YStage, ySpeed, yAccel, false) || !MotionControllerDevice.HomeMotion(MotionTypes.XStage, xSpeed, xAccel, false))
                {
                    MessageBox.Show("Load failed");
                    return;
                }
                IsFCLoaded = true;
            }
            else if (cmdPara == "unload")
            {
                int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed);
                int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel);
                int xPos = (int)(20 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed);
                int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel);
                int yPos = (int)(150 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                if (!MotionControllerDevice.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAccel, false) || !MotionControllerDevice.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAccel, false))
                {
                    MessageBox.Show("Unload failed");
                    return;
                }
                IsFCLoaded = false;
            }
        }

        #region Log Control Command
        private RelayCommand _LogCtrlCmd;
        public RelayCommand LogCtrlCmd
        {
            get
            {
                if (_LogCtrlCmd == null)
                {
                    _LogCtrlCmd = new RelayCommand(ExecuteLogCtrlCmd, CanExecuteLogCtrlCmd);
                }
                return _LogCtrlCmd;
            }
        }

        private void ExecuteLogCtrlCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "Start":
                    IsLogging = true;
                    break;
                case "Stop":
                    IsLogging = false;
                    break;
            }
        }

        private bool CanExecuteLogCtrlCmd(object obj)
        {
            return true;
        }
        #endregion Log Control Command

        #region Reset Pressure Command
        private RelayCommand _ResetPressureCmd;
        public RelayCommand ResetPressureCmd
        {
            get
            {
                if (_ResetPressureCmd == null)
                {
                    _ResetPressureCmd = new RelayCommand(ExecuteResetPressureCmd, CanExecuteResetPressureCmd);
                }
                return _ResetPressureCmd;
            }
        }

        private void ExecuteResetPressureCmd(object obj)
        {
            if (FluidController.GetInstance().IsBusyInResettingPressure)
            {
                return;
            }
            Task.Run(() =>
            {
                FluidController.GetInstance().ResetPressure();
            });
        }

        private bool CanExecuteResetPressureCmd(object obj)
        {
            return true;
        }
        #endregion Reset Pressure Command

        #region Save Fluidics Data Command
        private RelayCommand _SaveTempDataCmd;
        public ICommand SaveTempDataCmd => ChemistryVM.SaveDataCmd;
        //{
        //    get
        //    {
        //        if (_SaveTempDataCmd == null)
        //        {
        //            _SaveTempDataCmd = new RelayCommand(ExecuteSaveTempDataCmdDataCmd, CanExecuteSaveTempDataCmd);
        //        }
        //        return _SaveTempDataCmd;
        //    }
        //}

        private void ExecuteSaveTempDataCmdDataCmd(object obj)
        {
            if (PressureLine.Collection.Count > 0)
            {
                

            }
        }

        private bool CanExecuteSaveTempDataCmd(object obj)
        {            
            return ChemistryVM.AmbientTemperLine.Collection.Count > 0;
        }

        private RelayCommand _SaveFluidicsDataCmd;
        public ICommand SaveFluidicsDataCmd
        {
            get
            {
                if (_SaveFluidicsDataCmd == null)
                {
                    _SaveFluidicsDataCmd = new RelayCommand(ExecuteSaveFluidicsDataCmd, CanExecuteSaveFluidicsDataCmd);
                }
                return _SaveFluidicsDataCmd;
            }
        }

        private void ExecuteSaveFluidicsDataCmd(object obj)
        {
            if (PressureLine.Collection.Count > 0)
            {
                var pressureArray = PressureLine.Collection.ToArray();
                var flowrateArray = FlowRateLine.Collection.ToArray();
                var bubbleArray = BubbleStatusLine.Collection.ToArray();

                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "csv|*.csv";
                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        FileStream aFile = new FileStream(saveDialog.FileName, FileMode.Create);
                        StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                        sw.WriteLine(string.Format("Sample Time/Sec,Pressure,FlowRate,Bubble"));

                        int minLength = int.MaxValue;
                        if (pressureArray.Length > 0 && minLength > pressureArray.Length) { minLength = pressureArray.Length; }
                        if (flowrateArray.Length > 0 && minLength > flowrateArray.Length) { minLength = flowrateArray.Length; }
                        if (bubbleArray.Length > 0 && minLength > bubbleArray.Length) { minLength = bubbleArray.Length; }


                        for (int i = 0; i < minLength; i++)
                        {
                            double OArealtime = pressureArray[i].X / 24 / 3600 + _LogStartTime;
                            DateTime realdate = DateTime.FromOADate(OArealtime);
                            string time = realdate.ToString("HH:mm:ss.fff");
                            sw.WriteLine(string.Format("{0},{1},{2},{3}",
                                time, pressureArray[i].Y, flowrateArray[i].Y, bubbleArray[i].Y));
                        }
                        sw.Close();
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
                    }

                }

            }
        }

        private bool CanExecuteSaveFluidicsDataCmd(object obj)
        {
            return PressureLine.Collection.Count > 0;
        }
        #endregion Save Data Command

        #region FC Door Ctrl Command
        private RelayCommand _FCDoorCtrlCmd;
        public RelayCommand FCDoorCtrlCmd
        {
            get
            {
                if (_FCDoorCtrlCmd == null)
                {
                    _FCDoorCtrlCmd = new RelayCommand(ExecuteFCDoorCtrlCmd, CanExecuteFCDoorCtrlCmd);
                }
                return _FCDoorCtrlCmd;
            }
        }

        private void ExecuteFCDoorCtrlCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "Close":
                    // home Y motion before closing the door
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            int homeSpeed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                            int homeAccel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                            MotionController.GetInstance().HomeMotion(MotionTypes.YStage, homeSpeed, homeAccel, true);
                            if (MotionControllerDevice.HywireMotionController.HomeYFailed)
                            {
                                MessageBox.Show("Closing FC Door aborted due to Home Y failed.");
                                return;
                            }
                            if (MainBoardController.GetInstance().IsProtocolRev2)
                            {
                                MotionController.GetInstance().SetFCDoorStatus(false);
                            }
                            else
                            {
                                MainBoardController.GetInstance().SetDoorStatus(false);
                            }
                        }
                        catch(Exception ex)
                        {
                            MessageBox.Show("Close FC Door Failed.\n" + ex.Message);
                        }
                    });
                    break;
                case "Open":
                    Task.Factory.StartNew(() =>
                    {
                        if (MainBoardController.GetInstance().IsProtocolRev2)
                        {
                            MotionController.GetInstance().SetFCDoorStatus(true);
                        }
                        else
                        {
                            MainBoardController.GetInstance().SetDoorStatus(true);
                        }
                    });
                    break;
            }
        }

        private bool CanExecuteFCDoorCtrlCmd(object obj)
        {
            return true;
        }
        #endregion FC Door Ctrl Command

        #region Fluidics Test Command
        private RelayCommand _FluidicsTestCmd;
        public ICommand FluidicsTestCmd
        {
            get
            {
                if (_FluidicsTestCmd == null)
                {
                    _FluidicsTestCmd = new RelayCommand(ExecuteFluidicsTestCmd, CanExecuteFluidicsTestCmd);
                }
                return _FluidicsTestCmd;
            }
        }

        private void ExecuteFluidicsTestCmd(object obj)
        {
            try
            {
                IsFluidChecking = false;
                ISeqApp seqApp = SeqAppFactory.GetSeqApp();
                //List<PathOptions> testpath = new List<PathOptions>();
                //foreach (PathOptions pullpath in (PathOptions[])Enum.GetValues(typeof(PathOptions)))
                //{
                //    if (pullpath.ToString().Contains("Test") || pullpath.ToString().Contains("TestBypass1"))
                //    {
                //        testpath.Add(pullpath);
                //    }
                //}
                //seqApp.CreateSystemCheckInterface().FlowCheck(SettingsManager.ConfigSettings.FluidicsStartupSettings.Buffer2Pos, testpath);
                var task = new Task<bool>(() => { return seqApp.CreateSystemCheckInterface().FlowCheckAndPriming(); });
                task.Start();
                task.GetAwaiter().OnCompleted(() =>
                {
                    IsFluidChecking = true;
                });
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        private bool CanExecuteFluidicsTestCmd(object obj)
        {
            return true;
        }
        #endregion Fluidics Test Command

        #region FluidPreheatTemperCommand
        private RelayCommand _FluidPreheatTemperCmd;
        public RelayCommand FluidPreheatTemperCmd
        {
            get
            {
                if (_FluidPreheatTemperCmd == null)
                {
                    _FluidPreheatTemperCmd = new RelayCommand(ExecuteFluidPreheatTemperCmd, CanExecuteFluidPreheatTemperCmd);
                }
                return _FluidPreheatTemperCmd;
            }
        }

        private void ExecuteFluidPreheatTemperCmd(object obj)
        {
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                switch (obj.ToString())
                {
                    case "SetTemper":
                        if (MainBoardController.GetInstance().SetFluidPreHeatingTemp(PreheatTemper) == false)
                        {
                            MessageBox.Show("Setting preheat temper failed");
                        }
                        break;
                    case "GetParameter":
                        if (MainBoardController.GetInstance().GetFluidPreHeatCtrlParameters() == true)
                        {
                            FluidPreheatCtrlKp = MainBoardController.GetInstance().FluidPreHeatCtrlKp;
                            FluidPreheatCtrlKi = MainBoardController.GetInstance().FluidPreHeatCtrlKi;
                            FluidPreheatCtrlKd = MainBoardController.GetInstance().FluidPreHeatCtrlKd;
                            FluidPreheatCtrlGain = MainBoardController.GetInstance().FluidPreHeatGain;
                        }
                        else
                        {
                            MessageBox.Show("Failed to Read the parameters of fluid preheating!");
                        }
                        break;
                    case "SetParameter":
                        if(MainBoardController.GetInstance().SetFluidPreHeatCtrlParameters(FluidPreheatCtrlKp, FluidPreheatCtrlKi, FluidPreheatCtrlKd, FluidPreheatCtrlGain) == false)
                        {
                            MessageBox.Show("Failed to Set the parameters of fluid preheating!");
                        }
                        break;
                }
            }
            else if (Chiller.GetInstance().SetFluidHeatingTemper(PreheatTemper) == false)
            {
                MessageBox.Show("Setting preheat temper failed");
            }
        }

        private bool CanExecuteFluidPreheatTemperCmd(object obj)
        {
            return true;
        }
        #endregion FluidPreheatTemperCommand

        #region ReadMassOfWasteCmd
        private RelayCommand _ReadMassOfWasteCmd;
        public RelayCommand ReadMassOfWasteCmd
        {
            get
            {
                if (_ReadMassOfWasteCmd == null)
                {
                    _ReadMassOfWasteCmd = new RelayCommand(ExecuteReadMassOfWasteCmd, CanExecuteReadMassOfWasteCmd);
                }
                return _ReadMassOfWasteCmd;
            }
        }

        private void ExecuteReadMassOfWasteCmd(object obj)
        {
            if (FluidController.GetInstance().ReadMassOfWaste())
            {
                MassOfWaste = FluidController.GetInstance().MassOfWaste;
            }
            else
            {
                MessageBox.Show("Failed to read mass of waste!");
            }
        }

        private bool CanExecuteReadMassOfWasteCmd(object obj)
        {
            return true;
        }
        #endregion ReadMassOfWasteCmd
    }
}
