using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.ALF.FirmwareUpgrader
{
    enum MainboardUpgradeTargets
    {
        None,
        Mainboard,
        MotionLoader,
        MotionFw,
    }
    class UpgradeMBViewModel : ViewModelBase
    {
        #region Private Fields
        private bool _IsAtMainboardProgram;
        private bool _IsAtMainBoardLoader;
        private bool _IsAtMotionLoader;
        private bool _IsAtMotionFwLoader;
        private bool _IsConnected;
        private FPGAUpgrader _FpgaUpgrader;
        private MainboardUpgradeTargets _UpgradeTarget;
        private Thread _UpgradeThread;
        private bool _IsTargetMainboardSelected;
        private bool _IsTargetMotionLoaderSelected;
        private bool _IsTargetMotionFwSelected;
        #endregion Private Fields

        #region Public Properties
        public bool IsAtMainboardProgram
        {
            get { return _IsAtMainboardProgram; }
            set
            {
                if (_IsAtMainboardProgram != value)
                {
                    _IsAtMainboardProgram = value;
                    RaisePropertyChanged(nameof(IsAtMainboardProgram));

                    if (value == true)
                    {
                        IsAtMainBoardLoader = false;
                        IsAtMotionLoader = false;
                        IsAtMotionFwLoader = false;
                        RaisePropertyChanged(nameof(AllowMBUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionLoaderUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionFwUpgrade));
                    }
                }
            }
        }
        public bool IsAtMainBoardLoader
        {
            get { return _IsAtMainBoardLoader; }
            set
            {
                if (_IsAtMainBoardLoader != value)
                {
                    _IsAtMainBoardLoader = value;
                    RaisePropertyChanged(nameof(IsAtMainBoardLoader));

                    if(value == true)
                    {
                        IsAtMainboardProgram = false;
                        IsAtMotionLoader = false;
                        IsAtMotionFwLoader = false;
                        RaisePropertyChanged(nameof(AllowMBUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionLoaderUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionFwUpgrade));
                    }
                }
            }
        }
        public bool IsAtMotionLoader
        {
            get { return _IsAtMotionLoader; }
            set
            {
                if (_IsAtMotionLoader != value)
                {
                    _IsAtMotionLoader = value;
                    RaisePropertyChanged(nameof(IsAtMotionLoader));

                    if(value == true)
                    {
                        IsAtMainboardProgram = false;
                        IsAtMainBoardLoader = false;
                        IsAtMotionFwLoader = false;
                        RaisePropertyChanged(nameof(AllowMBUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionLoaderUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionFwUpgrade));
                    }
                }
            }
        }
        public bool IsAtMotionFwLoader
        {
            get { return _IsAtMotionFwLoader; }
            set
            {
                if (_IsAtMotionFwLoader != value)
                {
                    _IsAtMotionFwLoader = value;
                    RaisePropertyChanged(nameof(IsAtMotionFwLoader));

                    if(value == true)
                    {
                        IsAtMainboardProgram = false;
                        IsAtMainBoardLoader = false;
                        IsAtMotionLoader = false;
                        RaisePropertyChanged(nameof(AllowMBUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionLoaderUpgrade));
                        RaisePropertyChanged(nameof(AllowMotionFwUpgrade));
                    }
                }
            }
        }
        public bool IsConnected
        {
            get { return _IsConnected; }
            set
            {
                if (_IsConnected != value)
                {
                    _IsConnected = value;
                    RaisePropertyChanged(nameof(IsConnected));
                }
            }
        }
        public MainboardUpgradeTargets UpgradeTarget
        {
            get { return _UpgradeTarget; }
            set
            {
                if (_UpgradeTarget != value)
                {
                    _UpgradeTarget = value;
                    RaisePropertyChanged(nameof(UpgradeTarget));
                }
            }
        }
        public bool AllowMBUpgrade
        {
            get
            {
                return IsAtMainboardProgram || IsAtMainBoardLoader;
            }
        }
        public bool AllowMotionLoaderUpgrade
        {
            get
            {
                return IsAtMainboardProgram || IsAtMotionLoader;
            }
        }
        public bool AllowMotionFwUpgrade
        {
            get
            {
                return IsAtMainboardProgram || _IsAtMotionFwLoader;
            }
        }

        public bool IsTargetMainboardSelected
        {
            get { return _IsTargetMainboardSelected; }
            set
            {
                if (_IsTargetMainboardSelected != value)
                {
                    _IsTargetMainboardSelected = value;
                    RaisePropertyChanged(nameof(IsTargetMainboardSelected));
                    if (value == true)
                    {
                        UpgradeTarget = MainboardUpgradeTargets.Mainboard;
                    }
                }
            }
        }
        public bool IsTargetMotionLoaderSelected
        {
            get { return _IsTargetMotionLoaderSelected; }
            set
            {
                if (_IsTargetMotionLoaderSelected != value)
                {
                    _IsTargetMotionLoaderSelected = value;
                    RaisePropertyChanged(nameof(IsTargetMotionLoaderSelected));
                    if (value == true)
                    {
                        UpgradeTarget = MainboardUpgradeTargets.MotionLoader;
                    }
                }
            }
        }
        public bool IsTargetMotionFwSelected
        {
            get { return _IsTargetMotionFwSelected; }
            set
            {
                if (_IsTargetMotionFwSelected != value)
                {
                    _IsTargetMotionFwSelected = value;
                    RaisePropertyChanged(nameof(IsTargetMotionFwSelected));
                    if (value == true)
                    {
                        UpgradeTarget = MainboardUpgradeTargets.MotionFw;
                    }
                }
            }
        }
        #endregion Public Properties

        #region Connect
        public bool Connect(string portName)
        {
            Workspace.This.AddLogLine("Connecting to Mainboard...");
            if(portName != "Leave None")
            {
                IsConnected = MainBoardController.GetInstance().Connect(portName);
            }
            else
            {
                IsConnected = MainBoardController.GetInstance().Connect();
            }
            if (IsConnected)
            {
                IsAtMainboardProgram = true;
                Workspace.This.AddLogLine("Succeeded.");
                return true;
            }

            string yModemCharacter = "M";
            if (portName != "Leave None")
            {
                try
                {
                    Workspace.This.UpgradingPort = new SerialPort();
                    Workspace.This.UpgradingPort.PortName = portName;
                    Workspace.This.UpgradingPort.BaudRate = 115200;
                    Workspace.This.UpgradingPort.ReadTimeout = 2000;
                    Workspace.This.UpgradingPort.Parity = Parity.None;
                    Workspace.This.UpgradingPort.DataBits = 8;
                    Workspace.This.UpgradingPort.StopBits = StopBits.One;
                    Workspace.This.UpgradingPort.Open();
                    Workspace.This.UpgradingPort.ReadTo(yModemCharacter);
                    IsAtMainBoardLoader = true;
                    IsConnected = true;
                    YModem.ResponseCharacter = yModemCharacter;
                    Workspace.This.AddLogLine("Connected, already in mainboard loader program.");
                }
                catch(Exception ex)
                {
                    if (Workspace.This.UpgradingPort.IsOpen)
                    {
                        Workspace.This.UpgradingPort.Close();
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
                        Workspace.This.UpgradingPort.PortName = port;
                        Workspace.This.UpgradingPort.BaudRate = 115200;
                        Workspace.This.UpgradingPort.ReadTimeout = 2000;
                        Workspace.This.UpgradingPort.Parity = Parity.None;
                        Workspace.This.UpgradingPort.DataBits = 8;
                        Workspace.This.UpgradingPort.StopBits = StopBits.One;
                        Workspace.This.UpgradingPort.Open();
                        Workspace.This.UpgradingPort.ReadTo(yModemCharacter);
                        IsConnected = true;
                        IsAtMainBoardLoader = true;
                        YModem.ResponseCharacter = yModemCharacter;
                        Workspace.This.AddLogLine("Connected, already in loader program.");
                    }
                    catch (Exception ex)
                    {
                        if (Workspace.This.UpgradingPort.IsOpen)
                        {
                            Workspace.This.UpgradingPort.Close();
                        }
                    }
                }
            }
            if (IsConnected)
            {
                Workspace.This.AddLogLine("Succeeded.");
                return true;
            }

            yModemCharacter = "P";
            if (portName != "Leave None")
            {
                try
                {
                    Workspace.This.UpgradingPort = new SerialPort();
                    Workspace.This.UpgradingPort.PortName = portName;
                    Workspace.This.UpgradingPort.BaudRate = 115200;
                    Workspace.This.UpgradingPort.ReadTimeout = 2000;
                    Workspace.This.UpgradingPort.Parity = Parity.None;
                    Workspace.This.UpgradingPort.DataBits = 8;
                    Workspace.This.UpgradingPort.StopBits = StopBits.One;
                    Workspace.This.UpgradingPort.Open();
                    Workspace.This.UpgradingPort.ReadTo(yModemCharacter);
                    IsAtMotionLoader = true;
                    IsConnected = true;
                    YModem.ResponseCharacter = yModemCharacter;
                    Workspace.This.AddLogLine("Connected, already in motion loader.");
                }
                catch(Exception ex)
                {
                    if (Workspace.This.UpgradingPort.IsOpen)
                    {
                        Workspace.This.UpgradingPort.Close();
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
                        Workspace.This.UpgradingPort.PortName = port;
                        Workspace.This.UpgradingPort.BaudRate = 115200;
                        Workspace.This.UpgradingPort.ReadTimeout = 2000;
                        Workspace.This.UpgradingPort.Parity = Parity.None;
                        Workspace.This.UpgradingPort.DataBits = 8;
                        Workspace.This.UpgradingPort.StopBits = StopBits.One;
                        Workspace.This.UpgradingPort.Open();
                        Workspace.This.UpgradingPort.ReadTo(yModemCharacter);
                        IsConnected = true;
                        IsAtMotionLoader = true;
                        YModem.ResponseCharacter = yModemCharacter;
                        Workspace.This.AddLogLine("Connected, already in motion loader.");
                    }
                    catch (Exception ex)
                    {
                        if (Workspace.This.UpgradingPort.IsOpen)
                        {
                            Workspace.This.UpgradingPort.Close();
                        }
                    }
                }
            }
            if (IsConnected)
            {
                Workspace.This.AddLogLine("Succeeded.");
                return true;
            }

            _FpgaUpgrader = new FPGAUpgrader();
            if(portName != "Leave None")
            {
                if (_FpgaUpgrader.Connect(portName))
                {
                    IsAtMotionFwLoader = true;
                    IsConnected = true;
                }
                else
                {
                    IsConnected = false;
                }
            }
            else
            {
                if (_FpgaUpgrader.Connect())
                {
                    IsAtMotionFwLoader = true;
                    IsConnected = true;
                }
                else
                {
                    IsConnected = false;
                }
            }

            if (!IsConnected)
            {
                Workspace.This.AddLogLine("Failed.");
            }

            return IsConnected;
        }
        #endregion Connect

        #region Upgrade
        public void StartUpgrade()
        {
            if (!IsConnected)
            {
                MessageBox.Show("Mainboard is not connected yet.");
                return;
            }
            if (Workspace.This.IsUpgrading)
            {
                return;
            }
            switch (UpgradeTarget)
            {
                case MainboardUpgradeTargets.Mainboard:
                    _UpgradeThread = new Thread(UpgradeMainboard);
                    _UpgradeThread.Start();
                    break;
                case MainboardUpgradeTargets.MotionLoader:
                    _UpgradeThread = new Thread(UpgradeMotionLoader);
                    _UpgradeThread.Start();
                    break;
                case MainboardUpgradeTargets.MotionFw:
                    _UpgradeThread = new Thread(UpgradeMotionFirmware);
                    _UpgradeThread.Start();
                    break;
            }
            Workspace.This.IsUpgrading = true;
        }

        public void StopUpgrade()
        {
            var answer = MessageBox.Show("Are you sure to stop upgrading? this may cause unrecoverable error to the system.", "Warning...", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if(answer!= MessageBoxResult.OK)
            {
                return;
            }
            _UpgradeThread.Abort();
            _UpgradeThread.Join();
            Workspace.This.IsUpgrading = false;
            MessageBox.Show("Upgrade has been aborted.");
        }

        private void UpgradeMainboard()
        {
            if(!IsAtMainboardProgram && !IsAtMainBoardLoader)
            {
                Workspace.This.AddLogLine("Mainboard is not allowed to be upgraded at this states.");
                return;
            }

            if (IsAtMainboardProgram)
            {
                Workspace.This.AddLogLine("Set Upgrading flag...");
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.FirmwareReady, 1, new int[] { 0x0000000e });
                Workspace.This.AddLogLine("Reset MCU...");
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.RelaunchDevice, 1, new int[] { 1 });
                var portName = Workspace.This.Mainboard.PortName;
                Workspace.This.Mainboard.DisConnect();
                Thread.Sleep(1000);
                Workspace.This.UpgradingPort = new SerialPort();
                Workspace.This.UpgradingPort.PortName = portName;
                Workspace.This.UpgradingPort.BaudRate = 115200;
                Workspace.This.UpgradingPort.Parity = Parity.None;
                Workspace.This.UpgradingPort.DataBits = 8;
                Workspace.This.UpgradingPort.StopBits = StopBits.One;
                Workspace.This.UpgradingPort.Open();
            }
            YModem.ResponseCharacter = "M";

            Workspace.This.AddLogLine("Downloading firmware...");
            if (YModem.Transmit(Workspace.This.UpgradingPort, System.IO.Path.GetFileName(Workspace.This.SelectedFileName), Workspace.This.UpgradeBytes))
            {
                Workspace.This.AddLogLine("Finished successfully.");
                Workspace.This.UpgradingPort.Close();
            }
            else
            {
                Workspace.This.AddLogLine("Failed.");
            }
            Workspace.This.IsConnected = false;
            IsConnected = false;
            Workspace.This.IsUpgrading = false;
        }
        private void UpgradeMotionLoader()
        {
            if (!IsAtMainboardProgram && !IsAtMotionLoader)
            {
                Workspace.This.AddLogLine("Motion loader is not allowed to be upgraded at this states.");
                return;
            }
            if (IsAtMainboardProgram)
            {
                Workspace.This.AddLogLine("Set Upgrading flag...");
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.FirmwareReady, 1, new int[] { 0x0000000d });
                Workspace.This.AddLogLine("Reset MCU...");
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.RelaunchDevice, 1, new int[] { 1 });
                YModem.ResponseCharacter = "P";
                var portName = Workspace.This.Mainboard.PortName;
                Workspace.This.Mainboard.DisConnect();
                Thread.Sleep(1000);
                Workspace.This.UpgradingPort = new SerialPort();
                Workspace.This.UpgradingPort.PortName = portName;
                Workspace.This.UpgradingPort.BaudRate = 115200;
                Workspace.This.UpgradingPort.Parity = Parity.None;
                Workspace.This.UpgradingPort.DataBits = 8;
                Workspace.This.UpgradingPort.StopBits = StopBits.One;
                Workspace.This.UpgradingPort.Open();

            }

            Workspace.This.AddLogLine("Downloading firmware...");
            if (YModem.Transmit(Workspace.This.UpgradingPort,System.IO.Path.GetFileName(Workspace.This.SelectedFileName), Workspace.This.UpgradeBytes))
            {
                Workspace.This.AddLogLine("Finished successfully.");
                Workspace.This.UpgradingPort.Close();
            }
            else
            {
                Workspace.This.AddLogLine("Failed.");
            }
            Workspace.This.IsConnected = false;
            IsConnected = false;
            Workspace.This.IsUpgrading = false;
        }
        private void UpgradeMotionFirmware()
        {
            if(!IsAtMainboardProgram && !IsAtMotionFwLoader)
            {
                Workspace.This.AddLogLine("Motion firmware is not allowed to be upgraded at this states.");
                return;
            }
            if (IsAtMainboardProgram)
            {
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.FirmwareReady, 1, new int[] { 0x0000000b });
                Workspace.This.Mainboard.WriteRegisters(MainBoardController.Registers.RelaunchDevice, 1, new int[] { 1 });
                Thread.Sleep(1000);
                Workspace.This.AddLogLine("Switched to FPGA upgrader");
            }
            // connect MCU

            // connect EPCS
            _FpgaUpgrader.ConnectEPCS();
            Thread.Sleep(100);
            _FpgaUpgrader.ReadEPCSType();

            // 
            int _delayVal = 100;
            // Step 1: prepare write data information
            int _tempWriteCycle = Workspace.This.UpgradeBytes.Length / 256;
            if (Workspace.This.UpgradeBytes.Length % 256 != 0)
            {
                _tempWriteCycle += 1;
            }
            byte[] _tempWriteArray = new byte[256];
            int _tempWriteAddress = 0;
            double _tempPercent = 0;
            // Step 2: wait for EPCS to finish previous write process
            Workspace.This.AddLogLine("Wait for EPCS to be free...");
            _FpgaUpgrader.ReadEPCSStatus();
            byte StatusReg = _FpgaUpgrader.EPCSStatus;
            StatusReg |= 0x01;  // 设置“write in progress”标志位
            while ((StatusReg & 0x01) == 0x01)
            {
                _FpgaUpgrader.ReadEPCSStatus();
                StatusReg = _FpgaUpgrader.EPCSStatus;
                Thread.Sleep(_delayVal);
            }
            // Step 3: write "programming..." to the upgrade info memory

            // Step 4: erase EPCS
            Thread.Sleep(_delayVal);
            Workspace.This.AddLogLine("Erasing EPCS...");
            _FpgaUpgrader.EraseEPCS();
            // Step 5: wait for EPCS to finish erase
            Thread.Sleep(_delayVal);
            Workspace.This.AddLogLine("Wait for EPCS to be free...");
            _FpgaUpgrader.ReadEPCSStatus();
            StatusReg = _FpgaUpgrader.EPCSStatus;
            StatusReg |= 0x01;  // 设置“write in progress”标志位
            while ((StatusReg & 0x01) == 0x01)
            {
                _FpgaUpgrader.ReadEPCSStatus();
                StatusReg = _FpgaUpgrader.EPCSStatus;
                Thread.Sleep(_delayVal);
            }
            // Step 6: start write progress
            Workspace.This.AddLogLine("programming starts...");
            for (int i = 0; i < _tempWriteCycle;)
            {
                try
                {
                    // Substep 1: prepare programming data
                    for (int j = 0; j < 256; j++)
                    {
                        _tempWriteArray[j] = Workspace.This.UpgradeBytes[_tempWriteAddress + j];
                    }
                    // Substep 2: write program data
                    _FpgaUpgrader.WriteEPCSMemory(_tempWriteAddress, _tempWriteArray);
                    _tempWriteAddress += 256;
                    i++;
                    // Substep 3: update process bar
                    _tempPercent = Convert.ToDouble(i) / _tempWriteCycle * 100.0;
                    //if (Convert.ToInt32(_tempPercent) % 5 == 0)
                    //{
                    //    DownloadPercent = _tempPercent;
                    //}
                }
                catch (Exception e)
                {
                    //AbortProgram(e.Message);
                    return;
                }
            }
            Workspace.This.AddLogLine("Programming finished.");
            // Step 7: release EPCS
            Thread.Sleep(_delayVal);
            _FpgaUpgrader.ReleaseEPCS();
            Workspace.This.AddLogLine("EPCS released.");
            // Step 8: write current upgrade information
            Workspace.This.AddLogLine("Writing upgrade information...");
            Thread.Sleep(_delayVal);
            byte[] tempInfo = ASCIIEncoding.ASCII.GetBytes(Workspace.This.SelectedFileName + "\n" +
                $"Upgrade Time: {DateTime.Now.ToString()}");
            _FpgaUpgrader.WriteUpgradeInfo(tempInfo);
            Workspace.This.AddLogLine("upgrade information is written.");
            // Step 9: finish program
            Workspace.This.AddLogLine("Upgrade completed.\n" + $"End time: {DateTime.Now.ToString("HH:mm:ss")}");


            Workspace.This.IsUpgrading = false;
        }
        #endregion Upgrade
    }
}
