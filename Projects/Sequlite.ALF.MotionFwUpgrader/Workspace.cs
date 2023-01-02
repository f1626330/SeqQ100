using Microsoft.Win32;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.ALF.MotionFwUpgrader
{
    class Workspace : ViewModelBase
    {
        #region Private Fields
        private UpgradeComm _UpgradeComm;
        static byte[] _ServerIP = { 192, 168, 1, 100 };
        static byte[] _NetCardIP = { 192, 168, 1, 110 };
        static int _CommandPortNum = 5000;
        static int _StreamPortNum = 8000;

        private bool _IsConnected;
        private bool _IsInUserMode;
        private bool _IsInFactoryMode;
        private bool _IsUpgrading;

        private StringBuilder _HistoryLogger;
        private int _Progress;

        private UpgradeMaster _UpgradeProcess;
        private string _UpgradeInfo;
        private byte[] _UpgradeData;
        #endregion Private Fields

        #region Constructor
        public static Workspace This { get; } = new Workspace();
        protected Workspace()
        {
            _UpgradeComm = UpgradeComm.GetInstance();
            _HistoryLogger = new StringBuilder();
        }
        #endregion Constructor

        #region Public Properties
        public string HistoryLog
        {
            get { return _HistoryLogger.ToString(); }
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
        public bool IsInUserMode
        {
            get { return _IsInUserMode; }
            set
            {
                if (_IsInUserMode != value)
                {
                    _IsInUserMode = value;
                    RaisePropertyChanged(nameof(IsInUserMode));
                }
            }
        }
        public bool IsInFactoryMode
        {
            get { return _IsInFactoryMode; }
            set
            {
                if (_IsInFactoryMode != value)
                {
                    _IsInFactoryMode = value;
                    RaisePropertyChanged(nameof(IsInFactoryMode));
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
        public int Progress
        {
            get { return _Progress; }
            set
            {
                if (_Progress != value)
                {
                    _Progress = value;
                    RaisePropertyChanged(nameof(Progress));
                }
            }
        }
        #endregion Public Properties

        #region Logger
        public void ResetHistory()
        {
            _HistoryLogger.Clear();
            RaisePropertyChanged(nameof(HistoryLog));
        }
        public void AddHistory(string log)
        {
            _HistoryLogger.Append(log);
            RaisePropertyChanged(nameof(HistoryLog));
        }
        #endregion Logger

        #region Connect Command
        private RelayCommand _ConnectCmd;
        public RelayCommand ConnectCmd
        {
            get
            {
                if (_ConnectCmd == null)
                {
                    _ConnectCmd = new RelayCommand(ExecuteConnectCmd, CanExecuteConnectCmd);
                }
                return _ConnectCmd;
            }
        }

        private void ExecuteConnectCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "Connect":
                    AddHistory("Connecting Controller...");
                    IsConnected = _UpgradeComm.Connect();
                    if (IsConnected)
                    {
                        if (_UpgradeComm.IsInFactoryMode)
                        {
                            AddHistory(string.Format("OK, controller is in factory mode, reconfig code is{0}\n", _UpgradeComm.ReconfigTrigger));
                            _UpgradeComm.UpgraderReadLastUpgradeInfo();
                            AddHistory(string.Format("Last Upgrade info: {0}\n", _UpgradeComm.LastUpgradeInfo));
                            IsInFactoryMode = true;
                        }
                        else if (_UpgradeComm.IsInUserMode)
                        {
                            AddHistory(string.Format("OK, controller is in user mode, FW version is {0}\n", _UpgradeComm.UserFWVersion));
                            IsInUserMode = true;
                        }
                    }
                    else
                    {
                        AddHistory(string.Format("Failed: {0}\n", _UpgradeComm.ErrorMessage));
                    }
                    break;
                case "DisConnect":
                    break;
            }
        }

        private bool CanExecuteConnectCmd(object obj)
        {
            return true;
        }
        #endregion Connect Command

        #region Switch Command
        private RelayCommand _SwitchCmd;
        public RelayCommand SwitchCmd
        {
            get
            {
                if (_SwitchCmd == null)
                {
                    _SwitchCmd = new RelayCommand(ExecuteSwitchCmd, CanExecuteSwitchCmd);
                }
                return _SwitchCmd;
            }
        }

        private void ExecuteSwitchCmd(object obj)
        {
            AddHistory("Switching to upgrader loader...");
            if (_UpgradeComm.UserImageSwitchToUpgrader())
            {
                AddHistory("OK.\n");
                Task.Factory.StartNew(new Action(() =>
                {
                    Thread.Sleep(3000);
                    ConnectCmd.Execute("Connect");
                }));
            }
            else
            {
                AddHistory("Failed.\n");
            }
        }

        private bool CanExecuteSwitchCmd(object obj)
        {
            return true;
        }
        #endregion Switch Command

        #region Select File Command
        private RelayCommand _SelectFileCmd;
        public RelayCommand SelectFileCmd
        {
            get
            {
                if (_SelectFileCmd == null)
                {
                    _SelectFileCmd = new RelayCommand(ExecuteSelectFileCmd, CanExecuteSelectFileCmd);
                }
                return _SelectFileCmd;
            }
        }

        private void ExecuteSelectFileCmd(object obj)
        {
            OpenFileDialog opDlg = new OpenFileDialog();
            opDlg.Filter = "Raw Program Data File|*.rpd";
            if (opDlg.ShowDialog() == true)
            {
                try
                {
                    AddHistory("Reading upgrade file...");
                    using (FileStream fs = new FileStream(opDlg.FileName, FileMode.Open))
                    {
                        _UpgradeData = new byte[fs.Length];
                        fs.Read(_UpgradeData, 0, _UpgradeData.Length);
                    }
                    AddHistory(string.Format("OK, file size:{0} Bytes\n", _UpgradeData.Length));
                }
                catch (Exception ex)
                {
                    AddHistory(ex.Message + "\n");
                }

            }
        }

        private bool CanExecuteSelectFileCmd(object obj)
        {
            return true;
        }
        #endregion Select File Command

        #region Upgrade Command
        private RelayCommand _UpgradeCmd;
        public RelayCommand UpgradeCmd
        {
            get
            {
                if (_UpgradeCmd == null)
                {
                    _UpgradeCmd = new RelayCommand(ExecuteUpgradeCmd, CanExecuteUpgradeCmd);
                }
                return _UpgradeCmd;
            }
        }

        private void ExecuteUpgradeCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "Start":
                    if (_UpgradeComm.IsConnected == false)
                    {
                        MessageBox.Show("Please Connect the device.");
                        return;
                    }
                    if (IsInFactoryMode == false)
                    {
                        MessageBox.Show("Please switch the device to Upgrader.");
                        return;
                    }
                    if (_UpgradeData == null || _UpgradeData.Length == 0)
                    {
                        MessageBox.Show("Please select the upgrade file.");
                        return;
                    }
                    _UpgradeProcess = new UpgradeMaster(_UpgradeComm, _UpgradeInfo, _UpgradeData);
                    _UpgradeProcess.OnMessageNotified += AddHistory;
                    _UpgradeProcess.OnProgressUpdated += _UpgradeProcess_OnProgressUpdated;
                    _UpgradeProcess.Completed += _UpgradeProcess_Completed;
                    _UpgradeProcess.Start();
                    IsUpgrading = true;
                    break;
                case "Stop":
                    _UpgradeProcess.Abort();
                    _UpgradeProcess.OnMessageNotified -= AddHistory;
                    _UpgradeProcess = null;
                    IsUpgrading = false;
                    break;
            }
        }

        private void _UpgradeProcess_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            IsUpgrading = false;
            if (exitState == ThreadBase.ThreadExitStat.None)
            {
                MessageBox.Show("Upgrade is completed, please recycle the machine's power before running the machine.", "Congratulations", MessageBoxButton.OK, MessageBoxImage.Information);
                Environment.Exit(0);
            }
        }

        private bool CanExecuteUpgradeCmd(object obj)
        {
            return true;
        }
        private void _UpgradeProcess_OnProgressUpdated(int percentage)
        {
            Progress = percentage;
        }

        #endregion Upgrade Command
    }
}
