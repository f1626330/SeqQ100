using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class DetectorParametersViewModel : ViewModelBase
    {
        #region Constructor
        public DetectorParametersViewModel( bool isMachineRev2, MotionViewModel motionVM,
            MainBoardViewModel mainBoardVM)
        {
            IsMachineRev2 = isMachineRev2;
            MotionVM = motionVM;
            MainBoardVM = mainBoardVM;
            FilterMotionSetup = new MotionParameters(MotionVM)
            {
                Header = "Filter Motion",
            };
            YStageMotionSetup = new MotionParameters(MotionVM)
            {
                Header = "Y Stage Motion",
            };
            ZStageMotionSetup = new MotionParameters(MotionVM)
            {
                Header = "Z Stage Motion",
            };
            CartridgePowerOptions.Add(20);
            CartridgePowerOptions.Add(50);
            CartridgePowerOptions.Add(75);
            CartridgePowerOptions.Add(100);
        }
        #endregion Constructor
        public bool IsMachineRev2 { get; }
       MotionViewModel MotionVM { get; }
        MainBoardViewModel MainBoardVM { get; }
        #region Public Properties
        public MotionParameters FilterMotionSetup { get; }
        public MotionParameters YStageMotionSetup { get; }
        public MotionParameters ZStageMotionSetup { get; }

        private bool _IsCartridgeEnabled;
        public bool IsCartridgeEnabled
        {
            get { return _IsCartridgeEnabled; }
            set
            {
                if (_IsCartridgeEnabled != value)
                {
                    _IsCartridgeEnabled = value;
                    RaisePropertyChanged(nameof(IsCartridgeEnabled));

                    if (!IsMachineRev2)
                    {
                        MainBoardVM.MainBoard.SetCartridgeMotorStatus(_IsCartridgeEnabled);
                    }
                    else
                    {
                        MotionVM.MotionController.HywireMotionController.SetEnable(Hywire.MotionControl.MotorTypes.Motor_Z, new bool[] { _IsCartridgeEnabled });
                    }
                }
            }
        }
        public List<int> CartridgePowerOptions { get; } = new List<int>();

        private int _SelectedCartridgePower;
        public int SelectedCartridgePower
        {
            get { return _SelectedCartridgePower; }
            set
            {
                if (_SelectedCartridgePower != value)
                {
                    _SelectedCartridgePower = value;
                    RaisePropertyChanged(nameof(SelectedCartridgePower));

                    if (_SelectedCartridgePower != 0)
                    {
                        if (!IsMachineRev2)
                        {
                            MainBoardVM.MainBoard.SetCartridgeMotorPower(_SelectedCartridgePower);
                        }
                        else
                        {
                            Hywire.MotionControl.MotionDriveCurrent current;
                            switch (_SelectedCartridgePower)
                            {
                                case 20:
                                    current = Hywire.MotionControl.MotionDriveCurrent.Percent20;
                                    break;
                                case 50:
                                    current = Hywire.MotionControl.MotionDriveCurrent.Percent50;
                                    break;
                                case 75:
                                    current = Hywire.MotionControl.MotionDriveCurrent.Percent75;
                                    break;
                                case 100:
                                    current = Hywire.MotionControl.MotionDriveCurrent.Percent100;
                                    break;
                                default:
                                    current = Hywire.MotionControl.MotionDriveCurrent.Percent100;
                                    break;
                            }
                            MotionVM.MotionController.HywireMotionController.SetMotionDriveCurrent(Hywire.MotionControl.MotorTypes.Motor_Z, new Hywire.MotionControl.MotionDriveCurrent[] { current });
                        }
                    }
                }
            }
        }

        private int _GLEDMaxPower;
        public int GLEDMaxPower
        {
            get { return _GLEDMaxPower; }
            set
            {
                if (_GLEDMaxPower != value)
                {
                    _GLEDMaxPower = value;
                    RaisePropertyChanged("GLEDMaxPower");
                }
            }
        }

        private int _RLEDMaxPower;
        public int RLEDMaxPower
        {
            get { return _RLEDMaxPower; }
            set
            {
                if (_RLEDMaxPower != value)
                {
                    _RLEDMaxPower = value;
                    RaisePropertyChanged("RLEDMaxPower");
                }
            }
        }
        #endregion Public Properties

        #region Set LED Max Power Command
        private RelayCommand _SetLEDMaxPowerCmd;
        public ICommand SetLEDMaxPowerCmd
        {
            get
            {
                if (_SetLEDMaxPowerCmd == null)
                {
                    _SetLEDMaxPowerCmd = new RelayCommand(ExecuteSetLEDMaxPowerCmd, CanExecuteSetLEDMaxPowerCmd);
                }
                return _SetLEDMaxPowerCmd;
            }
        }

        private void ExecuteSetLEDMaxPowerCmd(object obj)
        {
            LEDTypes target = (LEDTypes)obj;
            if (target == LEDTypes.Green)
            {
                MessageBox.Show("Green LED's Max Power is set to " + GLEDMaxPower);
            }
            else if (target == LEDTypes.Red)
            {
                MessageBox.Show("Red LED's Max Power is set to " + RLEDMaxPower);
            }
        }

        private bool CanExecuteSetLEDMaxPowerCmd(object obj)
        {
            return true;
        }
        #endregion Set LED Max Power Command
    }

    public class MotionParameters : ViewModelBase
    {
        #region Private Fields
        private string _Header;
        private double _MaxSpeed;
        private double _MaxAccel;
        private double _RangeHigh;
        private double _RangeLow;
        #endregion Private Fields
        public string Header
        {
            get { return _Header; }
            set
            {
                if (_Header != value)
                {
                    _Header = value;
                    RaisePropertyChanged("Header");
                }
            }
        }
        public double MaxSpeed
        {
            get { return _MaxSpeed; }
            set
            {
                if (_MaxSpeed != value)
                {
                    _MaxSpeed = value;
                    RaisePropertyChanged("MaxSpeed");
                }
            }
        }
        public double MaxAccel
        {
            get { return _MaxAccel; }
            set
            {
                if (_MaxAccel != value)
                {
                    _MaxAccel = value;
                    RaisePropertyChanged("MaxAccel");
                }
            }
        }
        public double RangeHigh
        {
            get { return _RangeHigh; }
            set
            {
                if (_RangeHigh != value)
                {
                    _RangeHigh = value;
                    RaisePropertyChanged("RangeHigh");
                }
            }
        }
        public double RangeLow
        {
            get { return _RangeLow; }
            set
            {
                if (_RangeLow != value)
                {
                    _RangeLow = value;
                    RaisePropertyChanged(nameof(RangeLow));
                }
            }
        }
        MotionViewModel MotionVM { get; }
        #region Set Max Speed Command
        private RelayCommand _SetMaxSpeedCmd;
        public ICommand SetMaxSpeedCmd
        {
            get
            {
                if (_SetMaxSpeedCmd == null)
                {
                    _SetMaxSpeedCmd = new RelayCommand(ExecuteSetMaxSpeedCmd, CanExecuteSetMaxSpeedCmd);
                }
                return _SetMaxSpeedCmd;
            }
        }

        private void ExecuteSetMaxSpeedCmd(object obj)
        {
            if (Save())
            {
                MessageBox.Show(Header + "'s Max Speed is set to " + MaxSpeed);
            }
        }

        private bool CanExecuteSetMaxSpeedCmd(object obj)
        {
            return true;
        }
        #endregion Set Max Speed Command

        #region Set Max Accel Command
        private RelayCommand _SetMaxAccelCmd;
        public ICommand SetMaxAccelCmd
        {
            get
            {
                if (_SetMaxAccelCmd == null)
                {
                    _SetMaxAccelCmd = new RelayCommand(ExecuteSetMaxAccelCmd, CanExecuteSetMaxAccelCmd);
                }
                return _SetMaxAccelCmd;
            }
        }

        private void ExecuteSetMaxAccelCmd(object obj)
        {
            if (Save())
            {
                MessageBox.Show(Header + "'s Max Accel is set to " + MaxAccel);
            }
        }

        private bool CanExecuteSetMaxAccelCmd(object obj)
        {
            return true;
        }
        #endregion Set Max Accel Command

        #region Set Range Command
        private RelayCommand _SetRangeCmd;
        public ICommand SetRangeCmd
        {
            get
            {
                if (_SetRangeCmd == null)
                {
                    _SetRangeCmd = new RelayCommand(ExecuteSetRangeCmd, CanExecuteSetRangeCmd);
                }
                return _SetRangeCmd;
            }
        }

        private void ExecuteSetRangeCmd(object obj)
        {
            string parameter = (string)obj;
            if (parameter == "High")
            {
                if (Save())
                {
                    MessageBox.Show(Header + "'s Range High is set to " + RangeHigh);
                }
            }
            else if (parameter == "Low")
            {
                if (Save())
                {
                    MessageBox.Show(Header + "'s Range Low is set to " + RangeLow);
                }
            }
        }

        private bool CanExecuteSetRangeCmd(object obj)
        {
            return true;
        }
        #endregion Set Range Command

        #region Private Functions
        private bool Save()
        {
            bool result = false;
            if (!string.IsNullOrEmpty(Header))
            {
                MotionTypes type = MotionTypes.None;
                string attrType = string.Empty;
                switch (Header)
                {
                    case "Filter Motion":
                        type = MotionTypes.Filter;
                        attrType = "Filter";
                        MotionVM.FMotionLimitH = RangeHigh;
                        MotionVM.FMotionLimitL = RangeLow;
                        break;
                    case "Y Stage Motion":
                        type = MotionTypes.YStage;
                        attrType = "YStage";
                        MotionVM.YMotionLimitH = RangeHigh;
                        MotionVM.YMotionLimitL = RangeLow;
                        break;
                    case "Z Stage Motion":
                        type = MotionTypes.ZStage;
                        attrType = "ZStage";
                        MotionVM.ZMotionLimitH = RangeHigh;
                        MotionVM.ZMotionLimitL = RangeLow;
                        break;
                }
                if (type != MotionTypes.None)
                {
                    SettingsManager.ConfigSettings.MotionSettings[type].AccelRange.LimitHigh = MaxAccel;
                    SettingsManager.ConfigSettings.MotionSettings[type].SpeedRange.LimitHigh = MaxSpeed;
                    SettingsManager.ConfigSettings.MotionSettings[type].MotionRange.LimitHigh = RangeHigh;
                    SettingsManager.ConfigSettings.MotionSettings[type].MotionRange.LimitLow = RangeLow;

                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        string filePath = SettingsManager.ApplicationDataPath + "\\Config.xml";
                        doc.Load(filePath);

                        var xmlNodes = doc.GetElementsByTagName("MotionSetting");
                        foreach (XmlNode node in xmlNodes)
                        {
                            var attr = node.Attributes["Type"].Value;
                            if (attr == attrType)
                            {
                                node.Attributes["MaxSpeed"].Value = MaxSpeed.ToString();
                                node.Attributes["MaxAccel"].Value = MaxAccel.ToString();
                                node.Attributes["Min"].Value = RangeLow.ToString();
                                node.Attributes["Max"].Value = RangeHigh.ToString();
                                break;
                            }
                        }
                        StringBuilder sb = new StringBuilder();
                        XmlWriterSettings settings = new XmlWriterSettings
                        {
                            Indent = true,
                            IndentChars = "  ",
                            NewLineChars = "\r\n",
                            NewLineHandling = NewLineHandling.Replace
                        };
                        using (XmlWriter writer = XmlWriter.Create(sb, settings))
                        {
                            doc.Save(writer);
                            doc.Save(filePath);
                            result = true;
                        }
                    }
                    catch
                    {

                    }
                }
            }
            return result;
        }
        #endregion Private Functions

        public MotionParameters(MotionViewModel motionMV)
        {
            MotionVM = motionMV;
        }
    }
}
