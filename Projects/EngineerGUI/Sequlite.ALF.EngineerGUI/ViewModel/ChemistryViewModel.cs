using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class ChemistryViewModel : ViewModelBase
    {
        #region Private fields
        private double _ChemiTemperGet;
        private double _ChemiTemperSet;
        private bool _IsChemiTemperCtrlOnGet;
        private bool _IsChemiTemperCtrlOnSet;
        private double _ChemiTemperCtrlRampGet;
        private double _ChemiTemperCtrlRampSet;
        private double _ChemiTemperCtrlPowerGet;
        private double _ChemiTemperCtrlPowerSet;
        private double _HeatSinkTemper;
        private double _CoolerTemperGet;
        private double _CoolerTemperSet;
        private double _AmbientTemper;
        private int _TemperCtrlKp;
        private int _TemperCtrlKi;
        private int _TemperCtrlKd;
        private double _SampleInterval = 1;
        private double _ChemiTemperSetLimitHigh;
        private double _ChemiTemperSetLimitLow;
        private double _ChemiTemperRampSetLimitHigh;
        private double _ChemiTemperRampSetLimitLow;
        private int _TemperCtrlMaxCrnt;

        private double _FCTemperCtrlKp;
        private double _FCTemperCtrlKi;
        private double _FCTemperCtrlKd;
        private double _FCTemperCtrlHeatGain;
        private double _FCTemperCtrlCoolGain;
        private TemperatureController _FCTemperControllerRev2;
        private string _CoolerTemperLegend = "Chiller";
        private string _CoolerHeatSinkTemperLegend = "Chiller HeatSink";

        private bool _CoolingLiquidLevelIsFull;
        private int _SelectedCoolingPumpVoltage;
        private int _CoolingPumpSpeed;

        private int _EFanSpeed;
        private int _BFanSpeed;
        private int _FanSpeed;
        #endregion Private fields
        Mainboard MainBoardDevice { get;  }
        MainBoardController MainboardControllerRev2 { get; }
        Chiller ChillerDevice { get; }
        public IViewModelStatus ViewModelStatus { get; }
        public bool IsLogging { get; set; }
        public int LoggingDataCount { get; set; } = 3 * 60 * 60;
        public bool IsBusy
        {
            get
            {
                if (ViewModelStatus != null)
                {
                    return ViewModelStatus.IsBusy;
                }
                else
                {
                    return false;
                }
            }
        }
        public double StartTime { get; set; }
        #region Constructor
        public ChemistryViewModel(bool isMachineRev2, IViewModelStatus viewModelStatus)
        {
            IsMachineRev2 = isMachineRev2;
            ViewModelStatus = viewModelStatus;
           
            MainBoardDevice = Mainboard.GetInstance();
            MainboardControllerRev2 = MainBoardController.GetInstance();
            ChillerDevice = Chiller.GetInstance();
            _FCTemperControllerRev2 = TemperatureController.GetInstance();

            if (MainboardControllerRev2.IsMachineRev2P4)
            {
                _CoolerTemperLegend = "Chiller Air";
                _CoolerHeatSinkTemperLegend = "Chiller TEC";
            }
            if (MainboardControllerRev2.IsProtocolRev2)
            {
                _CoolerTemperLegend = "Chiller HeatSink";
                _CoolerHeatSinkTemperLegend = "Chiller Hot";
            }

            IsProtocolRev2 = ChillerDevice.IsProtocolRev2;
            CoolingPumpVoltageOptions = new List<int>();
            for (int i = 1; i <= 10; i++)
            {
                CoolingPumpVoltageOptions.Add(i);
            }
            SelectedCoolingPumpVoltage = CoolingPumpVoltageOptions[0];
        }



        public void Initialize()
        {
            _ChemiTemperSet = SettingsManager.ConfigSettings.ChemistryStartupSettings.Temper;
            _ChemiTemperCtrlRampSet = SettingsManager.ConfigSettings.ChemistryStartupSettings.Ramp;
            _ChemiTemperSetLimitHigh = SettingsManager.ConfigSettings.ChemiTemperRange.LimitHigh;
            _ChemiTemperSetLimitLow = SettingsManager.ConfigSettings.ChemiTemperRange.LimitLow;
            if(!IsMachineRev2)
            {
                _ChemiTemperRampSetLimitHigh = SettingsManager.ConfigSettings.ChemiTemperRampRange.LimitHigh;
                _ChemiTemperRampSetLimitLow = SettingsManager.ConfigSettings.ChemiTemperRampRange.LimitLow;
            }
            else
            {
                _ChemiTemperRampSetLimitHigh = 100000;
                _ChemiTemperRampSetLimitLow = 5;
                if (TemperatureController.GetInstance().IsConnected)
                {
                    _FCTemperCtrlKp = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP;
                    _FCTemperCtrlKi = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI;
                    _FCTemperCtrlKd = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD;
                    _FCTemperCtrlHeatGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain;
                    _FCTemperCtrlCoolGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain;
                }
                if (GetCoolerTargetTemperatureCmd.CanExecute(null))
                {
                    //ExecuteGetCoolerTargetTemperatureCmd(null);
                    GetCoolerTargetTemperatureCmd.Execute(null);
                }
            }
        }
        #endregion Constructor

        #region Public properties
        public ObservableDataSource<Point> ChemiTemperLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> HeatSinkTemperLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> CoolerTemperLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> AmbientTemperLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> CoolerHeatSinkTemperLine { get; } = new ObservableDataSource<Point>();
        public ObservableDataSource<Point> FluidPreheatTemperLine { get; } = new ObservableDataSource<Point>();
        public string CoolerTemperLegend
        {
            get { return _CoolerTemperLegend; }
            set
            {
                if (_CoolerTemperLegend != value)
                {
                    _CoolerTemperLegend = value;
                    RaisePropertyChanged(nameof(CoolerTemperLegend));
                }
            }
        }
        public string CoolerHeatSinkTemperLegend
        {
            get { return _CoolerHeatSinkTemperLegend; }
            set
            {
                if (_CoolerHeatSinkTemperLegend != value)
                {
                    _CoolerHeatSinkTemperLegend = value;
                    RaisePropertyChanged(nameof(CoolerHeatSinkTemperLegend));
                }
            }
        }
        public double ChemiTemperGet
        {
            get { return _ChemiTemperGet; }
            set
            {
                if (_ChemiTemperGet != value)
                {
                    _ChemiTemperGet = value;
                    RaisePropertyChanged(nameof(ChemiTemperGet));
                }
            }
        }
        public double ChemiTemperSet
        {
            get { return _ChemiTemperSet; }
            set
            {
                if (_ChemiTemperSet != value)
                {
                    if (value < ChemiTemperSetLimitLow || value > ChemiTemperSetLimitHigh)
                    {
                        MessageBox.Show("Set temperature out of range");
                    }
                    else
                    {
                        _ChemiTemperSet = value;
                    }
                    RaisePropertyChanged(nameof(ChemiTemperSet));
                }
            }
        }
        public bool IsChemiTemperCtrlOnGet
        {
            get { return _IsChemiTemperCtrlOnGet; }
            set
            {
                if (_IsChemiTemperCtrlOnGet != value)
                {
                    _IsChemiTemperCtrlOnGet = value;
                    RaisePropertyChanged(nameof(IsChemiTemperCtrlOnGet));
                }
            }
        }
        public bool IsChemiTemperCtrlOnSet
        {
            get { return _IsChemiTemperCtrlOnSet; }
            set
            {
                if (_IsChemiTemperCtrlOnSet != value)
                {
                    _IsChemiTemperCtrlOnSet = value;
                    RaisePropertyChanged(nameof(IsChemiTemperCtrlOnSet));
                }
            }
        }
        public double ChemiTemperCtrlRampGet
        {
            get { return _ChemiTemperCtrlRampGet; }
            set
            {
                if (_ChemiTemperCtrlRampGet != value)
                {
                    _ChemiTemperCtrlRampGet = value;
                    RaisePropertyChanged(nameof(ChemiTemperCtrlRampGet));
                }
            }
        }
        public double ChemiTemperCtrlRampSet
        {
            get { return _ChemiTemperCtrlRampSet; }
            set
            {
                if (_ChemiTemperCtrlRampSet != value)
                {
                    if (value != 0 && (value < ChemiTemperRampSetLimitLow || value > ChemiTemperRampSetLimitHigh))
                    {
                        MessageBox.Show("Temperature ramp out of range");
                    }
                    else
                    {
                        _ChemiTemperCtrlRampSet = value;
                    }
                    RaisePropertyChanged(nameof(ChemiTemperCtrlRampSet));
                }
            }
        }
        public double ChemiTemperCtrlPowerGet
        {
            get { return _ChemiTemperCtrlPowerGet; }
            set
            {
                if (_ChemiTemperCtrlPowerGet != value)
                {
                    _ChemiTemperCtrlPowerGet = value;
                    RaisePropertyChanged(nameof(ChemiTemperCtrlPowerGet));
                }
            }
        }
        public double ChemiTemperCtrlPowerSet
        {
            get { return _ChemiTemperCtrlPowerSet; }
            set
            {
                if (_ChemiTemperCtrlPowerSet != value)
                {
                    _ChemiTemperCtrlPowerSet = value;
                    RaisePropertyChanged(nameof(ChemiTemperCtrlPowerSet));
                }
            }
        }
        public double HeatSinkTemper
        {
            get { return _HeatSinkTemper; }
            set
            {
                if (_HeatSinkTemper != value)
                {
                    _HeatSinkTemper = value;
                    RaisePropertyChanged(nameof(HeatSinkTemper));
                }
            }
        }
        public double CoolerTemperGet
        {
            get { return _CoolerTemperGet; }
            set
            {
                if (_CoolerTemperGet != value)
                {
                    _CoolerTemperGet = value;
                    RaisePropertyChanged(nameof(CoolerTemperGet));
                }
            }
        }
        public double CoolerTemperSet
        {
            get { return _CoolerTemperSet; }
            set
            {
                if (_CoolerTemperSet != value)
                {
                    _CoolerTemperSet = value;
                    RaisePropertyChanged(nameof(CoolerTemperSet));
                }
            }
        }
        public double AmbientTemper
        {
            get { return _AmbientTemper; }
            set
            {
                if (_AmbientTemper != value)
                {
                    _AmbientTemper = value;
                    RaisePropertyChanged(nameof(AmbientTemper));
                }
            }
        }
        public int TemperCtrlKp
        {
            get { return _TemperCtrlKp; }
            set
            {
                if (_TemperCtrlKp != value)
                {
                    _TemperCtrlKp = value;
                    RaisePropertyChanged(nameof(TemperCtrlKp));
                }
            }
        }
        public int TemperCtrlKi
        {
            get { return _TemperCtrlKi; }
            set
            {
                if (_TemperCtrlKi != value)
                {
                    _TemperCtrlKi = value;
                    RaisePropertyChanged(nameof(TemperCtrlKi));
                }
            }
        }
        public int TemperCtrlKd
        {
            get { return _TemperCtrlKd; }
            set
            {
                if (_TemperCtrlKd != value)
                {
                    _TemperCtrlKd = value;
                    RaisePropertyChanged(nameof(TemperCtrlKd));
                }
            }
        }
        public double SampleInterval
        {
            get { return _SampleInterval; }
            set
            {
                if (_SampleInterval != value)
                {
                    if (value < 0.5)
                    {
                        MessageBox.Show("minimum sample interval: 0.5 sec");
                    }
                    else
                    {
                        _SampleInterval = value;
                    }
                    RaisePropertyChanged(nameof(SampleInterval));
                }
            }
        }
        public double ChemiTemperSetLimitHigh
        {
            get { return _ChemiTemperSetLimitHigh; }
        }
        public double ChemiTemperSetLimitLow
        {
            get { return _ChemiTemperSetLimitLow; }
        }
        public double ChemiTemperRampSetLimitHigh
        {
            get { return _ChemiTemperRampSetLimitHigh; }
        }
        public double ChemiTemperRampSetLimitLow
        {
            get { return _ChemiTemperRampSetLimitLow; }
        }
        public int TemperCtrlMaxCrnt
        {
            get
            {
                return _TemperCtrlMaxCrnt;
            }
            set
            {
                if (_TemperCtrlMaxCrnt != value)
                {
                    if (value > 100 || value < 0)
                    {
                        MessageBox.Show("The valid range is 0~100");
                    }
                    else
                    {
                        _TemperCtrlMaxCrnt = value;
                    }
                    RaisePropertyChanged(nameof(TemperCtrlMaxCrnt));
                }
            }
        }
        public bool IsMachineRev2 { get; }

        public double FCTemperCtrlKp
        {
            get { return _FCTemperCtrlKp; }
            set
            {
                if (_FCTemperCtrlKp != value)
                {
                    _FCTemperCtrlKp = value;
                    RaisePropertyChanged(nameof(FCTemperCtrlKp));
                }
            }
        }
        public double FCTemperCtrlKi
        {
            get { return _FCTemperCtrlKi; }
            set
            {
                if (_FCTemperCtrlKi != value)
                {
                    _FCTemperCtrlKi = value;
                    RaisePropertyChanged(nameof(FCTemperCtrlKi));
                }
            }
        }
        public double FCTemperCtrlKd
        {
            get { return _FCTemperCtrlKd; }
            set
            {
                if (_FCTemperCtrlKd != value)
                {
                    _FCTemperCtrlKd = value;
                    RaisePropertyChanged(nameof(FCTemperCtrlKd));
                }
            }
        }
        public double FCTemperCtrlHeatGain
        {
            get { return _FCTemperCtrlHeatGain; }
            set
            {
                if (_FCTemperCtrlHeatGain != value)
                {
                    _FCTemperCtrlHeatGain = value;
                    RaisePropertyChanged(nameof(FCTemperCtrlHeatGain));
                }
            }
        }
        public double FCTemperCtrlCoolGain
        {
            get { return _FCTemperCtrlCoolGain; }
            set
            {
                if (_FCTemperCtrlCoolGain != value)
                {
                    _FCTemperCtrlCoolGain = value;
                    RaisePropertyChanged(nameof(FCTemperCtrlCoolGain));
                }
            }
        }
        public bool IsProtocolRev2 { get; set; }
        public bool CoolingLiquidLevelIsFull
        {
            get => _CoolingLiquidLevelIsFull;
            set
            {
                if (_CoolingLiquidLevelIsFull != value)
                {
                    _CoolingLiquidLevelIsFull = value;
                    RaisePropertyChanged(nameof(CoolingLiquidLevelIsFull));
                }
            }
        }
        public List<int> CoolingPumpVoltageOptions { get; }
        public int SelectedCoolingPumpVoltage
        {
            get => _SelectedCoolingPumpVoltage;
            set
            {
                if (_SelectedCoolingPumpVoltage != value)
                {
                    _SelectedCoolingPumpVoltage = value;
                    RaisePropertyChanged(nameof(SelectedCoolingPumpVoltage));
                }
            }
        }
        public int CoolingPumpSpeed
        {
            get => _CoolingPumpSpeed;
            set
            {
                if (_CoolingPumpSpeed != value)
                {
                    _CoolingPumpSpeed = value;
                    RaisePropertyChanged(nameof(CoolingPumpSpeed));
                }
            }
        }
        public int EFanSpeed
        {
            get => _EFanSpeed;
            set
            {
                if (_EFanSpeed != value)
                {
                    _EFanSpeed = value;
                    RaisePropertyChanged(nameof(EFanSpeed));
                }
            }
        }
        public int BFanSpeed
        {
            get => _BFanSpeed;
            set
            {
                if (_BFanSpeed != value)
                {
                    _BFanSpeed = value;
                    RaisePropertyChanged(nameof(BFanSpeed));
                }
            }
        }
        public int FanSpeed
        {
            get => _FanSpeed;
            set
            {
                if (_FanSpeed != value)
                {
                    _FanSpeed = value;
                    RaisePropertyChanged(nameof(FanSpeed));
                }
            }
        }
        #endregion Public properties

        #region Public functions
        #endregion Public functions

        #region Set Temperature Command
        private RelayCommand _SetTemperCmd;
        public ICommand SetTemperCmd
        {
            get
            {
                if (_SetTemperCmd == null)
                {
                    _SetTemperCmd = new RelayCommand(ExecuteSetTemperCmd, CanExecuteSetTemperCmd);
                }
                return _SetTemperCmd;
            }
        }

        private void ExecuteSetTemperCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                if(MainBoardDevice.IsConnected == false)
                {
                    MessageBox.Show("Mainboard is missing, operation failed!");
                    return;
                }
            }
            //else if (MainboardControllerRev2.IsConnected == false)
            //{
            //    MessageBox.Show("Mainboard is missing, operation failed!");
            //    return;
            //}
            else if (_FCTemperControllerRev2.IsConnected == false)
            {
                MessageBox.Show("Temperature controller is missing, operation failed!");
                return;
            }
            string cmdType = obj.ToString().ToLower();
            if (cmdType == "on")
            {
                if (!IsMachineRev2)
                {
                    if (MainBoardDevice.SetChemiTemper(ChemiTemperSet))
                    {
                        MainBoardDevice.SetChemiTemperCtrlStatus(true);
                    }
                    else
                    {
                        MessageBox.Show("Set Temperature failed.");
                    }
                }
                else
                {
                    //if (MainboardControllerRev2.SetChemiTemper(ChemiTemperSet))
                    //{
                    //    MainboardControllerRev2.SetChemiTemperCtrlStatus(true);
                    //}
                    //else
                    if (_FCTemperControllerRev2.SetTemperature(ChemiTemperSet, ChemiTemperCtrlRampSet) == false)
                    {
                        MessageBox.Show("Set Temperature failed.");
                    }
                }
            }
            else if (cmdType == "off")
            {
                if (!IsMachineRev2)
                {
                    MainBoardDevice.SetChemiTemperCtrlStatus(false);
                }
                else
                {
                    //MainboardControllerRev2.SetChemiTemperCtrlStatus(false);
                    _FCTemperControllerRev2.SetControlSwitch(false);
                }
            }
        }

        private bool CanExecuteSetTemperCmd(object obj)
        {
            return true;
        }
        #endregion Set Temperature Command

        #region Set Temperature Ramp Command
        private RelayCommand _SetTemperRampCmd;
        public ICommand SetTemperRampCmd
        {
            get
            {
                if (_SetTemperRampCmd == null)
                {
                    _SetTemperRampCmd = new RelayCommand(ExecuteSetTemperRampCmd, CanExecuteSetTemperRampCmd);
                }
                return _SetTemperRampCmd;
            }
        }

        private void ExecuteSetTemperRampCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                MainBoardDevice.SetChemiTemperCtrlRamp(ChemiTemperCtrlRampSet);
            }
            //else
            //{
            //    MainboardControllerRev2.SetChemiTemperCtrlRamp(ChemiTemperCtrlRampSet);
            //}
        }

        private bool CanExecuteSetTemperRampCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                return MainBoardDevice.IsConnected;
            }
            else
            {
                //return MainboardControllerRev2.IsConnected;
                return _FCTemperControllerRev2.IsConnected;
            }
        }
        #endregion Set Temperature Ramp Command

        #region Save Data Command
        private RelayCommand _ClearDataCmd;
        public ICommand ClearDataCmd
        {
            get
            {
                if (_ClearDataCmd == null)
                {
                    _ClearDataCmd = new RelayCommand(ExecuteClearDataCmd, CanExecuteClearDataCmd);
                }
                return _ClearDataCmd;
            }
        }

        private void ExecuteClearDataCmd(object obj)
        {
            AmbientTemperLine.Collection.Clear();
            ChemiTemperLine.Collection.Clear();
            HeatSinkTemperLine.Collection.Clear();
            CoolerTemperLine.Collection.Clear();
            CoolerHeatSinkTemperLine.Collection.Clear();
            FluidPreheatTemperLine.Collection.Clear();
        }
        private bool CanExecuteClearDataCmd(object obj)
        {
            return AmbientTemperLine.Collection.Count > 0;
        }

        private RelayCommand _SaveDataCmd;
        public ICommand SaveDataCmd
        {
            get
            {
                if (_SaveDataCmd == null)
                {
                    _SaveDataCmd = new RelayCommand(ExecuteSaveDataCmd, CanExecuteSaveDataCmd);
                }
                return _SaveDataCmd;
            }
        }

        private void ExecuteSaveDataCmd(object obj)
        {
            if (AmbientTemperLine.Collection.Count > 0)
            {
                var ambientTemperArray = AmbientTemperLine.Collection.ToArray();
                var chemiTemperArray = ChemiTemperLine.Collection.ToArray();
                var heatSinkTemperArray = HeatSinkTemperLine.Collection.ToArray();
                var coolerTemperArray = CoolerTemperLine.Collection.ToArray();
                var coolerHeatSinkTemperArray = CoolerHeatSinkTemperLine.Collection.ToArray();
                var fluidPreheatTemperArray = FluidPreheatTemperLine.Collection.ToArray();

                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "csv|*.csv";
                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        FileStream aFile = new FileStream(saveDialog.FileName, FileMode.Create);
                        StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                        if (IsProtocolRev2)
                        {
                            sw.WriteLine(string.Format("Sample Time/Sec,Ambient,Chemistry,Heat sink,Chiller CoolHeatSink,Chiller HotHeatSink, FluidPreheat"));
                        }
                        else
                        {
                            sw.WriteLine(string.Format("Sample Time/Sec,Ambient,Chemistry,Heat sink,Chiller CoolHeatSink,Chiller HotHeatSink"));
                        }

                        int minLength = int.MaxValue;
                        if(ambientTemperArray.Length > 0 && minLength > ambientTemperArray.Length) { minLength = ambientTemperArray.Length; }
                        if(chemiTemperArray.Length > 0 && minLength > chemiTemperArray.Length) { minLength = chemiTemperArray.Length; }
                        if(heatSinkTemperArray.Length > 0 && minLength > heatSinkTemperArray.Length) { minLength = heatSinkTemperArray.Length; }
                        if(coolerTemperArray.Length > 0 && minLength > coolerTemperArray.Length) { minLength = coolerTemperArray.Length; }
                        if(coolerHeatSinkTemperArray.Length > 0 && minLength > coolerHeatSinkTemperArray.Length) { minLength = coolerHeatSinkTemperArray.Length; }
                        //if(fluidPreheatTemperArray.Length > 0 && minLength > fluidPreheatTemperArray.Length) { minLength = fluidPreheatTemperArray.Length; }
                        if(chemiTemperArray.Length == 0)
                        {
                            chemiTemperArray = new Point[minLength];
                            for(int i = 0; i < minLength; i++)
                            {
                                chemiTemperArray[i].Y = double.NaN;
                            }
                        }
                        if(heatSinkTemperArray.Length == 0)
                        {
                            heatSinkTemperArray = new Point[minLength];
                            for(int i = 0; i < minLength; i++)
                            {
                                heatSinkTemperArray[i].Y = double.NaN;
                            }
                        }
                        if(coolerTemperArray.Length == 0)
                        {
                            coolerTemperArray = new Point[minLength];
                            for(int i = 0; i < minLength; i++)
                            {
                                coolerTemperArray[i].Y = double.NaN;
                            }
                        }
                        if(coolerHeatSinkTemperArray.Length == 0)
                        {
                            coolerHeatSinkTemperArray = new Point[minLength];
                            for(int i = 0; i < minLength; i++)
                            {
                                coolerHeatSinkTemperArray[i].Y = double.NaN;
                            }
                        }
                        if(fluidPreheatTemperArray.Length < minLength)
                        {
                            int len = fluidPreheatTemperArray.Length;
                            Point[] tmp = new Point[len];
                            fluidPreheatTemperArray.CopyTo(tmp, 0);
                            fluidPreheatTemperArray = new Point[minLength];
                            for(int i=0;i<minLength;i++)
                            {
                                fluidPreheatTemperArray[i].Y = double.NaN;
                            }
                            for(int i = 0; i < minLength; i++)
                            {
                                double x = chemiTemperArray[i].X;
                                foreach(Point p in tmp)
                                {
                                    if(p.X == x)
                                    {
                                        fluidPreheatTemperArray[i].X = p.X;
                                        fluidPreheatTemperArray[i].Y = p.Y;
                                        break;
                                    };
                                }
                            }
                        }
                        for (int i = 0; i < minLength; i++)
                        {
                            if (IsProtocolRev2)
                            {
                                double OArealtime = ambientTemperArray[i].X / 24 / 3600 + StartTime;
                                DateTime realdate = DateTime.FromOADate(OArealtime);
                                string time = realdate.ToString("HH:mm:ss.fff");
                                sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6}", time,
                                    ambientTemperArray[i].Y, 
                                    chemiTemperArray[i].Y, 
                                    heatSinkTemperArray[i].Y, 
                                    coolerTemperArray[i].Y, 
                                    coolerHeatSinkTemperArray[i].Y,
                                    fluidPreheatTemperArray[i].Y));
                            }
                            else
                            {
                                sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5}",
                                    ambientTemperArray[i].X, ambientTemperArray[i].Y, chemiTemperArray[i].Y, heatSinkTemperArray[i].Y, coolerTemperArray[i].Y, coolerHeatSinkTemperArray[i].Y));
                            }
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

        private bool CanExecuteSaveDataCmd(object obj)
        {
            return AmbientTemperLine.Collection.Count > 0;
        }
        #endregion Save Data Command

        #region Visit Temperature Ctrl Parameters Command
        private RelayCommand _VisitTemperCtrlParaCmd;
        public ICommand VisitTemperCtrlParaCmd
        {
            get
            {
                if (_VisitTemperCtrlParaCmd == null)
                {
                    _VisitTemperCtrlParaCmd = new RelayCommand(ExecuteGetTemperCtrlParaCmd, CanExecuteGetTemperCtrlParaCmd);
                }
                return _VisitTemperCtrlParaCmd;
            }
        }

        private void ExecuteGetTemperCtrlParaCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            if (cmdPara == "get")
            {
                if (!IsMachineRev2)
                {
                    MainBoardDevice.GetTemperCtrlParameters();
                }
                else
                {
                    if (ChillerDevice.GetTemperCtrlParameters())
                    {
                        TemperCtrlKp = ChillerDevice.TemperCtrlP;
                        TemperCtrlKi = ChillerDevice.TemperCtrlI;
                        TemperCtrlKd = ChillerDevice.TemperCtrlD;
                        TemperCtrlMaxCrnt = ChillerDevice.ChillerMaxPower;
                    }
                }
            }
            else if (cmdPara == "set")
            {
                if (!IsMachineRev2)
                {
                    MainBoardDevice.SetTemperCtrlParameters(TemperCtrlKp, TemperCtrlKi, TemperCtrlKd, TemperCtrlMaxCrnt);
                }
                else
                {
                    ChillerDevice.SetTemperCtrlParameters(TemperCtrlKp, TemperCtrlKi, TemperCtrlKd, TemperCtrlMaxCrnt);
                }
            }
            else if(cmdPara == "getfc")
            {
                if (MainboardControllerRev2.IsProtocolRev2)
                {
                    MainboardControllerRev2.GetTemperCtrlParameters();
                    MainboardControllerRev2.GetChemiTemperCtrlGains();
                    FCTemperCtrlKp = MainboardControllerRev2.TemperCtrlP;
                    FCTemperCtrlKi = MainboardControllerRev2.TemperCtrlI;
                    FCTemperCtrlKd = MainboardControllerRev2.TemperCtrlD;
                    FCTemperCtrlHeatGain = MainboardControllerRev2.ChemiTemperHeatGain;
                    FCTemperCtrlCoolGain = MainboardControllerRev2.ChemiTemperCoolGain;
                }
                else
                {
                    if (_FCTemperControllerRev2.GetCtrlP())
                    {
                        FCTemperCtrlKp = _FCTemperControllerRev2.CtrlP;
                    }
                    if (_FCTemperControllerRev2.GetCtrlI())
                    {
                        FCTemperCtrlKi = _FCTemperControllerRev2.CtrlI;
                    }
                    if (_FCTemperControllerRev2.GetCtrlD())
                    {
                        FCTemperCtrlKd = _FCTemperControllerRev2.CtrlD;
                    }
                    if (_FCTemperControllerRev2.GetHeaterGain())
                    {
                        FCTemperCtrlHeatGain = _FCTemperControllerRev2.HeatGain;
                    }
                    if (_FCTemperControllerRev2.GetCoolerGain())
                    {
                        FCTemperCtrlCoolGain = _FCTemperControllerRev2.CoolGain;
                    }
                }
            }
            else if(cmdPara == "setfc")
            {
                if (MainboardControllerRev2.IsProtocolRev2)
                {
                    MainboardControllerRev2.SetTemperCtrlParameters(FCTemperCtrlKp, FCTemperCtrlKi, FCTemperCtrlKd, FCTemperCtrlHeatGain, FCTemperCtrlCoolGain);
                }
                else
                {
                    _FCTemperControllerRev2.SetCtrlP(FCTemperCtrlKp);
                    _FCTemperControllerRev2.SetCtrlI(FCTemperCtrlKi);
                    _FCTemperControllerRev2.SetCtrlD(FCTemperCtrlKd);
                    _FCTemperControllerRev2.SetHeaterGain(FCTemperCtrlHeatGain);
                    _FCTemperControllerRev2.SetCoolerGain(FCTemperCtrlCoolGain);
                }
            }
        }

        private bool CanExecuteGetTemperCtrlParaCmd(object obj)
        {
            //if (!IsMachineRev2)
            //{
            //    return MainBoardDevice.IsConnected;
            //}
            //else
            //{
            //    return ChillerDevice.IsConnected;
            //}
            return true;
        }
        #endregion Get Temperature Ctrl Parameters Command

        #region Set Cooler Temper Command
        private RelayCommand _SetCoolerTargetTemperatureCmd;
        public ICommand SetCoolerTargetTemperatureCmd
        {
            get
            {
                if (_SetCoolerTargetTemperatureCmd == null)
                {
                    _SetCoolerTargetTemperatureCmd = new RelayCommand(ExecuteSetCoolerTargetTemperatureCmd, CanExecuteSetCoolerTargetTemperatureCmd);
                }
                return _SetCoolerTargetTemperatureCmd;
            }
        }

        private void ExecuteSetCoolerTargetTemperatureCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                if (MainBoardDevice.SetCoolerTemper(CoolerTemperSet) == false)
                {
                    MessageBox.Show("Set chiller temperature failed");
                }
            }
            else
            {
                if(ChillerDevice.SetCoolerTargetTemperature(CoolerTemperSet) == false)
                {
                    MessageBox.Show("Set chiller temperature failed");
                }
            }
        }

        private bool CanExecuteSetCoolerTargetTemperatureCmd(object obj)
        {
            return true;
        }
        #endregion Set Cooler Temper Command

        #region Get Cooler Temper Command
        private RelayCommand _GetCoolerTargetTemperatureCmd;
        public ICommand GetCoolerTargetTemperatureCmd
        {
            get
            {
                if (_GetCoolerTargetTemperatureCmd == null)
                {
                    _GetCoolerTargetTemperatureCmd = new RelayCommand(ExecuteGetCoolerTargetTemperatureCmd, CanExecuteGetCoolerTargetTemperatureCmd);
                }
                return _GetCoolerTargetTemperatureCmd;
            }
        }

        private void ExecuteGetCoolerTargetTemperatureCmd(object obj)
        {
            if (!IsMachineRev2)
            {
                if (MainBoardDevice.GetCoolerTemper() == false)
                {
                    MessageBox.Show("Get chiller temperature failed");
                }
            }
            else
            {
                if (ChillerDevice.GetCoolerTargetTemperature() == false)
                {
                    MessageBox.Show("Get chiller target temperature failed");
                }
            }
        }

        private bool CanExecuteGetCoolerTargetTemperatureCmd(object obj)
        {
            if(IsMachineRev2)
            {
                return ChillerDevice.IsConnected;
            }
            else
            {
                return true;
            }
        }
        #endregion Get Cooler Temper Command

        #region set Sample Interval command
        private RelayCommand _SetSampleIntervalCmd;
        public ICommand SetSampleIntervalCmd
        {
            get
            {
                if (_SetSampleIntervalCmd == null)
                {
                    _SetSampleIntervalCmd = new RelayCommand(ExecuteSetSampleIntervalCmd, CanExecuteSetSampleIntervalCmd);
                }
                return _SetSampleIntervalCmd;
            }
        }

        private void ExecuteSetSampleIntervalCmd(object obj)
        {
            SampleInterval = _SampleInterval;
        }

        private bool CanExecuteSetSampleIntervalCmd(object obj)
        {
            return true;
        }
        #endregion set SampleInterval command

        #region Cooling Parameter Command
        private RelayCommand _CoolingParameterCmd;
        public RelayCommand CoolingParameterCmd
        {
            get
            {
                if (_CoolingParameterCmd == null)
                {
                    _CoolingParameterCmd = new RelayCommand(ExecuteCoolingParameterCmd, CanExecuteCoolingParameterCmd);
                }
                return _CoolingParameterCmd;
            }
        }

        private void ExecuteCoolingParameterCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "GetLiquidLevel":
                    if (ChillerDevice.GetCoolingLiquidLevel())
                    {
                        CoolingLiquidLevelIsFull = ChillerDevice.CoolingLiquidIsFull;
                    }
                    break;
                case "GetPumpSpeed":
                    if (ChillerDevice.GetCoolingPumpSpeed())
                    {
                        CoolingPumpSpeed = ChillerDevice.CoolingPumpSpeed;
                    }
                    break;
                case "SetPumpVoltage":
                    ChillerDevice.SetCoolingPumpVoltage(SelectedCoolingPumpVoltage);
                    break;
            }
        }

        private bool CanExecuteCoolingParameterCmd(object obj)
        {
            return true;
        }
        #endregion Cooling Parameter Command

        #region Fan Speed Command
        private RelayCommand _FanSpeedCmd;
        public RelayCommand FanSpeedCmd
        {
            get
            {
                if (_FanSpeedCmd == null)
                {
                    _FanSpeedCmd = new RelayCommand(ExecuteFanSpeedCmd, CanExecuteFanSpeedCmd);
                }
                return _FanSpeedCmd;
            }
        }

        private void ExecuteFanSpeedCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "GET":
                    if (MainboardControllerRev2.GetFanPWMValues())
                    {
                        EFanSpeed = MainboardControllerRev2.EFanPWMValue;
                        BFanSpeed = MainboardControllerRev2.BFanPWMValue;
                        FanSpeed = MainboardControllerRev2.FanPWMValue;
                    }
                    break;
                case "SET":
                    MainboardControllerRev2.SetFanPWMValues(EFanSpeed, BFanSpeed, FanSpeed);
                    break;
            }
        }

        private bool CanExecuteFanSpeedCmd(object obj)
        {
            return true;
        }
        #endregion Fan Speed Command
    }
}
