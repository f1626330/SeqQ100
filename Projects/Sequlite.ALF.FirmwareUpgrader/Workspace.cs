using Microsoft.Win32;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.FirmwareUpgrader
{
    class Workspace : ViewModelBase
    {
        public static Workspace This { get; } = new Workspace();

        #region Private Fields
        public MainBoardController Mainboard { get; set; }
        private Chiller _Chiller;
        private LEDController _LED;
        private FluidController _Fluid;
        private string _SelectedDevice;
        private string _SelectedPort;
        private string _SelectedFileName;
        private bool _IsConnected;
        private bool _IsNotConnected = true;
        private StringBuilder _Logs;
        private bool _IsMBSelected;
        private bool _IsUpgrading;

        public byte[] UpgradeBytes { get; private set; }

        private Thread _UpradingThread;
        private bool _IsAlreadyInLoader;
        private Visibility _SetFPGAVisible = Visibility.Collapsed;
        private Visibility _SwitchFPGAVisible = Visibility.Collapsed;
        private bool _IsUpgradingFPGALoader;
        #endregion Private Fields

        public Workspace()
        {
            DeviceOptions = new List<string>();
            DeviceOptions.Add("Mainboard");
            DeviceOptions.Add("Chiller");
            DeviceOptions.Add("LED");
            DeviceOptions.Add("Fluid");

            Mainboard = MainBoardController.GetInstance();
            _Chiller = Chiller.GetInstance();
            _LED = LEDController.GetInstance();
            _Fluid = FluidController.GetInstance();

            _Logs = new StringBuilder();

            var ports = SerialPort.GetPortNames();
            PortOptions = new ObservableCollection<string>(ports);
            PortOptions.Add("Leave None");

            UpgradeMBVm = new UpgradeMBViewModel();
        }

        #region Public Properties
        public SerialPort UpgradingPort { get; set; }

        public List<String> DeviceOptions { get; }

        public string SelectedDevice
        {
            get { return _SelectedDevice; }
            set
            {
                if (_SelectedDevice != value)
                {
                    _SelectedDevice = value;
                    RaisePropertyChanged(nameof(SelectedDevice));

                    switch (value)
                    {
                        case "Mainboard":
                            SelectedPort = "COM12";
                            IsMBSelected = true;
                            break;
                        case "Chiller":
                            SelectedPort = "COM13";
                            IsMBSelected = false;
                            break;
                        case "LED":
                            SelectedPort = "COM14";
                            IsMBSelected = false;
                            break;
                        case "Fluid":
                            SelectedPort = "COM4";
                            IsMBSelected = false;
                            break;
                        default:
                            SelectedPort = PortOptions.Last();
                            IsMBSelected = false;
                            break;
                    }
                }
            }
        }

        public ObservableCollection<string> PortOptions { get; }

        public string SelectedPort
        {
            get { return _SelectedPort; }
            set
            {
                if (_SelectedPort != value)
                {
                    if (PortOptions.Contains(value))
                    {
                        _SelectedPort = value;
                    }
                    else
                    {
                        _SelectedPort = PortOptions.Last();
                    }
                    RaisePropertyChanged(nameof(SelectedPort));
                }
            }
        }

        public string SelectedFileName
        {
            get { return _SelectedFileName; }
            set
            {
                if (_SelectedFileName != value)
                {
                    _SelectedFileName = value;
                    RaisePropertyChanged(nameof(SelectedFileName));
                }
            }
        }

        public bool IsConnected
        {
            get { return _IsConnected; }
            set
            {
                if(_IsConnected!=value)
                {
                    _IsConnected = value;
                    RaisePropertyChanged(nameof(IsConnected));
                    IsNotConnected = !_IsConnected;
                }
            }
        }
        public bool IsNotConnected
        {
            get
            {
                return _IsNotConnected;
            }
            set
            {
                if (_IsNotConnected != value)
                {
                    _IsNotConnected = value;
                    RaisePropertyChanged(nameof(IsNotConnected));
                }
            }
        }

        public string Logs
        {
            get { return _Logs.ToString(); }
        }

        public Visibility SetFPGAVisible
        {
            get { return _SetFPGAVisible; }
            set
            {
                if (_SetFPGAVisible != value)
                {
                    _SetFPGAVisible = value;
                    RaisePropertyChanged(nameof(SetFPGAVisible));
                }
            }
        }
        public Visibility SwitchFPGAVisible
        {
            get { return _SwitchFPGAVisible; }
            set
            {
                if (_SwitchFPGAVisible != value)
                {
                    _SwitchFPGAVisible = value;
                    RaisePropertyChanged(nameof(SwitchFPGAVisible));
                }
            }
        }
        public bool IsUpgradingFPGALoader
        {
            get { return _IsUpgradingFPGALoader; }
            set
            {
                if (_IsUpgradingFPGALoader != value)
                {
                    _IsUpgradingFPGALoader = value;
                    RaisePropertyChanged(nameof(IsUpgradingFPGALoader));
                }
            }
        }

        public UpgradeMBViewModel UpgradeMBVm { get; }
        public bool IsMBSelected
        {
            get { return _IsMBSelected; }
            set
            {
                if (_IsMBSelected != value)
                {
                    _IsMBSelected = value;
                    RaisePropertyChanged(nameof(IsMBSelected));
                }
            }
        }
        public bool IsUpgrading
        {
            get { return _IsUpgrading; }
            set
            {
                if (_IsUpgrading != value)
                {
                    _IsUpgrading = value;
                    RaisePropertyChanged(nameof(IsUpgrading));
                }
            }
        }
        #endregion Public Properties

        #region Setting Command
        private RelayCommand _SettingCommand;
        public ICommand SettingCommand
        {
            get
            {
                if(_SettingCommand == null)
                {
                    _SettingCommand = new RelayCommand(ExecuteSettingCommand, CanExecuteSettingCommand);
                }
                return _SettingCommand;
            }
        }

        private void ExecuteSettingCommand(object obj)
        {
            switch(obj.ToString())
            {
                case "Connect":
                    if(SelectedDevice == null)
                    {
                        MessageBox.Show("Please select a device to connect.");
                        return;
                    }
                    switch (SelectedDevice)
                    {
                        case "Mainboard":
                            IsConnected = UpgradeMBVm.Connect(SelectedPort);
                            break;
                        default:
                            ConnectOtherDevice();
                            break;
                    }
                    break;
                case "DisConnect":
                    break;
                case "File":
                    if (IsNotConnected)
                    {
                        MessageBox.Show("Please select a device to connect.");
                        return;
                    }
                    if(SelectedDevice == "Mainboard")
                    {
                        if(UpgradeMBVm.UpgradeTarget == MainboardUpgradeTargets.None)
                        {
                            MessageBox.Show("Please select upgrade target.");
                            return;
                        }
                    }
                    OpenFileDialog opDlg = new OpenFileDialog();
                    //opDlg.Filter = "(bin)|*.bin";
                    if(opDlg.ShowDialog()==true)
                    {
                        SelectedFileName = opDlg.FileName;
                        using(FileStream fs = new FileStream(SelectedFileName, FileMode.Open, FileAccess.Read))
                        {
                            UpgradeBytes = new byte[fs.Length];
                            fs.Read(UpgradeBytes, 0, (int)fs.Length);
                        }
                    }
                    break;
                case "StartUpgrade":
                    if (IsNotConnected)
                    {
                        MessageBox.Show("Please select a device to connect.");
                        return;
                    }
                    if (SelectedFileName == null)
                    {
                        MessageBox.Show("Please select the upgrade file");
                        return;
                    }
                    if(SelectedDevice == "Mainboard")
                    {
                        UpgradeMBVm.StartUpgrade();
                    }
                    else
                    {
                        _UpradingThread = new Thread(UpgradingProcess);
                        _UpradingThread.IsBackground = true;
                        _UpradingThread.Start();
                    }
                    break;
                case "StopUpgrade":
                    break;
            }
        }

        private bool CanExecuteSettingCommand(object obj)
        {
            if(obj.ToString() == "Connect")
            {
                return IsNotConnected;
            }
            return true;
        }

        public void AddLogLine(string newLine)
        {
            _Logs.Append(string.Format("{0}\n", newLine));
            RaisePropertyChanged(nameof(Logs));
        }

        private void UpgradingProcess()
        {
            if (!_IsAlreadyInLoader)
            {
                string portName = null;
                switch (SelectedDevice)
                {
                    case "Chiller":
                        AddLogLine("Set Upgrading flag...");
                        _Chiller.WriteRegisters(Chiller.Registers.FirmwareReady, 1, new int[] { 0 });
                        AddLogLine("Reset MCU...");
                        _Chiller.WriteRegisters(Chiller.Registers.RelaunchDevice, 1, new int[] { 1 });
                        YModem.ResponseCharacter = "C";
                        portName = _Chiller.PortName;
                        _Chiller.DisConnect();
                        break;
                    case "LED":
                        AddLogLine("Set Upgrading flag...");
                        _LED.WriteRegisters(LEDController.Registers.FirmwareReady, 1, new int[] { 0 });
                        AddLogLine("Reset MCU...");
                        _LED.WriteRegisters(LEDController.Registers.RelaunchDevice, 1, new int[] { 1 });
                        YModem.ResponseCharacter = "L";
                        portName = _LED.PortName;
                        _LED.DisConnect();
                        break;
                    case "Fluid":
                        AddLogLine("Set Upgrading flag...");
                        _Fluid.WriteRegisters(FluidController.Registers.FirmwareReady, 1, new int[] { 0 });
                        AddLogLine("Reset MCU...");
                        _Fluid.WriteRegisters(FluidController.Registers.RelaunchDevice, 1, new int[] { 1 });
                        YModem.ResponseCharacter = "F";
                        portName = _Fluid.PortName;
                        _Fluid.DisConnect();
                        break;
                }
                Thread.Sleep(100);
                UpgradingPort = new SerialPort();
                UpgradingPort.PortName = portName;
                UpgradingPort.BaudRate = 115200;
                UpgradingPort.Parity = Parity.None;
                UpgradingPort.DataBits = 8;
                UpgradingPort.StopBits = StopBits.One;
                UpgradingPort.Open();
            }

            AddLogLine("Downloading firmware...");
            if(YModem.Transmit(UpgradingPort, System.IO.Path.GetFileName(SelectedFileName), UpgradeBytes))
            {
                AddLogLine("Finished successfully.");
                IsConnected = false;
                UpgradingPort.Close();
            }
            else
            {
                AddLogLine("Failed.");
            }
        }
        #endregion Setting Command

        private void ConnectOtherDevice()
        {
            var character = "C";
            SerialPeripheralBase peripheral;
            switch (SelectedDevice)
            {
                case "Chiller":
                    AddLogLine("connecting to Chiller...");
                    peripheral = _Chiller;
                    character = "C";
                    break;
                case "LED":
                    AddLogLine("connecting to LED...");
                    peripheral = _LED;
                    character = "L";
                    break;
                case "Fluid":
                    AddLogLine("connecting to Fluid...");
                    peripheral = _Fluid;
                    character = "F";
                    break;
                default:
                    return;
            }
            if (SelectedPort != "Leave None")
            {
                IsConnected = peripheral.Connect(SelectedPort);
            }
            else
            {
                IsConnected = peripheral.Connect();
            }

            if (IsNotConnected)
            {
                UpgradingPort = new SerialPort();
                if(SelectedPort != "Leave None")
                {
                    try
                    {
                        UpgradingPort = new SerialPort();
                        UpgradingPort.PortName = SelectedPort;
                        UpgradingPort.BaudRate = 115200;
                        UpgradingPort.ReadTimeout = 2000;
                        UpgradingPort.Parity = Parity.None;
                        UpgradingPort.DataBits = 8;
                        UpgradingPort.StopBits = StopBits.One;
                        UpgradingPort.Open();
                        UpgradingPort.ReadTo(character);
                        _IsAlreadyInLoader = true;
                        IsConnected = true;
                        YModem.ResponseCharacter = character;
                        AddLogLine("Connected, already in upgrade loader.");
                    }
                    catch (Exception ex)
                    {
                        if (UpgradingPort.IsOpen)
                        {
                            UpgradingPort.Close();
                        }
                    }
                }
                else
                {
                    var portNames = SerialPort.GetPortNames();
                    foreach (var port in portNames)
                    {
                        try
                        {
                            UpgradingPort.PortName = port;
                            UpgradingPort.BaudRate = 115200;
                            UpgradingPort.ReadTimeout = 2000;
                            UpgradingPort.Parity = Parity.None;
                            UpgradingPort.DataBits = 8;
                            UpgradingPort.StopBits = StopBits.One;
                            UpgradingPort.Open();
                            UpgradingPort.ReadTo(character);
                            _IsAlreadyInLoader = true;
                            IsConnected = true;
                            SetFPGAVisible = Visibility.Visible;
                            SwitchFPGAVisible = Visibility.Collapsed;
                            YModem.ResponseCharacter = character;
                            AddLogLine("Connected, already in upgrade loader.");
                            if (IsMBSelected)
                            {
                                UpgradeMBVm.IsAtMainBoardLoader = true;
                            }
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (UpgradingPort.IsOpen)
                            {
                                UpgradingPort.Close();
                            }
                        }
                    }
                    AddLogLine("Failed");
                }
            }
            else
            {
                AddLogLine("Connected");
            }
        }
    }
}
