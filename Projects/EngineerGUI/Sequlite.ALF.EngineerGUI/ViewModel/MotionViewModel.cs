using Hywire.MotionControl;
using Sequlite.ALF.Common;
using Sequlite.ALF.MotionControl;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class MotionViewModel : ViewModelBase
    {
        #region Private Fields
        private double _FMotionSpeed;   //< filter speed
        private double _YMotionSpeed;   //< y-axis (stage) speed
        private double _ZMotionSpeed;   //< z-axis speed
        private double _CMotionSpeed;   //< cartridge speed
        private double _XMotionSpeed;   //< x-axis (stage) speed
        private double _FCDoorSpeed;    //< flow cell door speed

        private double _FMotionAccel;   //< filter acceleration
        private double _YMotionAccel;   //< y-axis (stage) acceleration
        private double _ZMotionAccel;   //< z-axis speed
        private double _CMotionAccel;   //< cartridge acceleration
        private double _XMotionAccel;   //< x-axis (stage) acceleration
        private double _FCDoorAccel;    //< flow cell door acceleration

        // Target position values for absolute move command.
        // Defaults are at app startup from MotionStartupSettings in Config.json
        private double _FMotionAbsolutePos; //< filter absolute position (target)
        private double _YMotionAbsolutePos; //< y-axis (stage) absolute position (target)
        private double _ZMotionAbsolutePos; //< z-axis absolute position (target)
        private double _CMotionAbsolutePos; //< cartridge absolute position (target)
        private double _XMotionAbsolutePos; //< x-axis (stage) absolute position (target)
        private double _FCDoorAbsolutePos; //< flow cell door absolute position (target)

        // Relative move values. Defaults are at app startup from MotionStartupSettings in Config.json
        private int _FMotionPosShift;
        private double _YMotionPosShift;
        private double _ZMotionPosShift;
        private double _XMotionPosShift;
        private double _CMotionPosShift;
        private double _FCDoorPosShift;

        // Current position values
        private int _FMotionCurrentPos;
        private double _YMotionCurrentPos;
        private double _ZMotionCurrentPos;
        private double? _CMotionCurrentPos;
        private double _XMotionCurrentPos;
        private double _FCDoorCurrentPos;

        // Motion Ranges contain ranges for accel, speed, and position
        private MotionRanges _FMotionRange;
        private MotionRanges _YMotionRange;
        private MotionRanges _ZMotionRange;
        private MotionRanges _CMotionRange;
        private MotionRanges _XMotionRange;
        private MotionRanges _FCDoorRange;

        private double _XMotionEncoderPos;
        private double _YMotionEncoderPos;

        private bool _IsXMotionEnabled = true;
        private bool _IsYMotionEnabled = true;
        //private bool _IsZMotionEnabled;

        private bool _IsGalilAlive;
        private bool _IsZStageAlive;

        private double _CMotionCoeff;

        private bool _IsXBusy;
        private bool _IsYBusy;
        private string _CartridgeMotorStatus;
        private SerialPeripherals.Chiller _Chiller;
        private int _SelectedCartridgeMotorSpeed;
        private bool _IsChillerDoorLocked;
        #endregion Private Fields
        FluidicsViewModel FluidicsVM { get; } //< reference to the fluidics view model

        public bool IsMachineRev2 { get; } //< flag to switch controls (false = rev1 machine)
        public bool IsMachineRev2P4 { get; } //< used only for v2.4
        #region Constructor
        public MotionViewModel(FluidicsViewModel fluidicsVM, bool _IsMachineRev2, bool isMachineRev2P4)
        {
            InitializeRanges();
            IsMachineRev2 = _IsMachineRev2;
            IsMachineRev2P4 = isMachineRev2P4;
            FluidicsVM = fluidicsVM;
            MotionController = MotionController.GetInstance();
            MotionController.OnRecordsUpdated += MotionController_OnRecordsUpdated;
            FilterOptions = new int[] { 1, 2, 3, 4 };
            SelectedFilter = FilterOptions[0];
            _Chiller = SerialPeripherals.Chiller.GetInstance();
            //_Chiller.OnMotorStatusChanged += _Chiller_OnMotorStatusChanged;
            if (isMachineRev2P4)
            {
                _Chiller.OnMotorStatusChanged += _Chiller_OnMotorStatusChanged;
                CMotionCurrentPos = _Chiller.CartridgeMotorPos;
                CartridgeMotorStatus = _Chiller.CartridgeMotorStatus.ToString();
            }
            IsProtocolRev2 = _Chiller.IsProtocolRev2;

            CartridgeMotorSpeedOptions = new List<int>();
            for (int i = 1; i <= 10; i++)
            {
                CartridgeMotorSpeedOptions.Add(i);
            }
        }

        /// <summary>
        /// Initializes motion ranges for each axis using values from the SettingsManager.
        /// Ranges are used in set/get methods for local variables
        /// </summary>
        private void InitializeRanges()
        {
            Dictionary<MotionTypes, MotionRanges> motionRanges = SettingsManager.ConfigSettings.MotionSettings;
            _FMotionRange = new MotionRanges(motionRanges[MotionTypes.Filter]);
            _YMotionRange = new MotionRanges(motionRanges[MotionTypes.YStage]);
            _ZMotionRange = new MotionRanges(motionRanges[MotionTypes.ZStage]);
            _CMotionRange = new MotionRanges(motionRanges[MotionTypes.Cartridge]);
            _XMotionRange = new MotionRanges(motionRanges[MotionTypes.XStage]);
            _FCDoorRange = new MotionRanges(motionRanges[MotionTypes.FCDoor]);
        }

        private void _Chiller_OnMotorStatusChanged(SerialPeripherals.Chiller.CartridgeStatusTypes status)
        {
            CartridgeMotorStatus = status.ToString();
            CMotionCurrentPos = Math.Round(_Chiller.CartridgeMotorPos, 2);
        }

        private void MotionController_OnRecordsUpdated()
        {
            var filterPos = MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter];
            Dispatch(() =>
            {
                if (filterPos == 0)
                {
                    FMotionCurrentPos = 0;
                }
                else
                {
                    for (int i = 1; i <= SettingsManager.ConfigSettings.FilterPositionSettings.Count; i++)
                    {
                        if (filterPos == SettingsManager.ConfigSettings.FilterPositionSettings[i])
                        {
                            FMotionCurrentPos = i;
                            break;
                        }
                    }
                }
                YMotionCurrentPos = Math.Round(MotionController.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 3);
                if (!IsMachineRev2P4)
                {
                    CMotionCurrentPos = Math.Round(MotionController.CCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge], 2);
                }
                ZMotionCurrentPos = Math.Round(MotionController.ZCurrentPos, 4);
                //_XMotionCurrentPos = Math.Round(MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactorSettings[MotionTypes.XStage], 3);
                XMotionCurrentPos = Math.Round(MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 3);

                if (IsMachineRev2)
                {
                    XMotionEncoderPos = Math.Round(MotionController.XEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.XStage], 4);
                    YMotionEncoderPos = Math.Round(MotionController.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);

                    IsXBusy = MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy;
                    IsYBusy = MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy;
                }

                if (IsProtocolRev2)
                {
                    FCDoorCurrentPos = Math.Round(MotionController.CCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor], 3);
                }
            });
        }
        #endregion Constructor

        #region Public Properties
        /// <summary>
        /// Sets a value if it is within the range limitLow and limitHigh (inclusive). Otherwise, shows an error message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="limitLow"></param>
        /// <param name="limitHigh"></param>
        /// <param name="newVal"></param>
        /// <param name="value"></param>
        /// <param name="property"></param>
        private void LimitValue<T>(in T limitLow, in T limitHigh, ref T oldValue, in T newValue, string property) where T : IComparable<T>
        {
            if (newValue.CompareTo(oldValue) != 0)
            {
                if (newValue.CompareTo(limitLow) < 0 || newValue.CompareTo(limitHigh) > 0)
                {
                    ShowMessage($"Value of {property} out of range. Valid range: [{limitLow},{limitHigh}]");
                }
                else
                {
                    oldValue = newValue;
                    RaisePropertyChanged(property);
                }
            }
        }
        public MotionController MotionController { get; }
        public bool IsGalilAlive
        {
            get { return _IsGalilAlive; }
            set
            {
                if (_IsGalilAlive != value)
                {
                    _IsGalilAlive = value;
                    RaisePropertyChanged(nameof(IsGalilAlive));
                }
            }
        }
        public bool IsZStageAlive
        {
            get { return _IsZStageAlive; }
            set
            {
                if (_IsZStageAlive != value)
                {
                    _IsZStageAlive = value;
                    RaisePropertyChanged(nameof(IsZStageAlive));
                }
            }
        }
        public double FMotionSpeed
        {
            get => _FMotionSpeed;
            set => LimitValue<double>(0, _FMotionRange.SpeedRange.LimitHigh, ref _FMotionSpeed, value, "FMotionSpeed");
        }
        public double YMotionSpeed
        {
            get => _YMotionSpeed;
            set => LimitValue<double>(0, _YMotionRange.SpeedRange.LimitHigh, ref _YMotionSpeed, value, "YMotionSpeed");
        }
        public double ZMotionSpeed
        {
            get => _ZMotionSpeed;
            set => LimitValue<double>(0, _ZMotionRange.SpeedRange.LimitHigh, ref _ZMotionSpeed, value, "ZMotionSpeed");
        }
        public double CMotionSpeed
        {
            get => _CMotionSpeed;
            set => LimitValue<double>(double.NegativeInfinity, double.PositiveInfinity, ref _CMotionSpeed, value, "CMotionSpeed");
        }
        public double XMotionSpeed
        {
            get => _XMotionSpeed;
            set => LimitValue<double>(0, _XMotionRange.SpeedRange.LimitHigh, ref _XMotionSpeed, value, "XMotionSpeed");
        }
        public double FCDoorSpeed
        {
            get => _FCDoorSpeed;
            set => LimitValue<double>(0, _FCDoorRange.SpeedRange.LimitHigh, ref _FCDoorSpeed, value, nameof(FCDoorSpeed));
        }
        public double FMotionAccel
        {
            get => _FMotionAccel;
            set => LimitValue<double>(0, _FMotionRange.AccelRange.LimitHigh, ref _FMotionAccel, value, "FMotionAccel");
        }
        public double YMotionAccel
        {
            get => _YMotionAccel;
            set => LimitValue<double>(0, _YMotionRange.AccelRange.LimitHigh, ref _YMotionAccel, value, "YMotionAccel");
        }
        public double ZMotionAccel
        {
            get => _ZMotionAccel;
            set => LimitValue<double>(0, _ZMotionRange.AccelRange.LimitHigh, ref _ZMotionAccel, value, "ZMotionAccel");
        }
        public double CMotionAccel
        {
            get => _CMotionAccel;
            set => LimitValue<double>(double.NegativeInfinity, double.PositiveInfinity, ref _CMotionAccel, value, "CMotionAccel");
        }
        public double XMotionAccel
        {
            get => _XMotionAccel;
            set => LimitValue<double>(0, _XMotionRange.AccelRange.LimitHigh, ref _XMotionAccel, value, "XMotionAccel");
        }
        public double FCDoorAccel
        {
            get { return _FCDoorAccel; }
            set => LimitValue<double>(0, _FCDoorRange.AccelRange.LimitHigh, ref _FCDoorAccel, value, "FCDoorAccel");
        }
        public double FMotionAbsolutePos
        {
            get => _FMotionAbsolutePos;
            set => LimitValue<double>(_FMotionRange.MotionRange.LimitLow, _FMotionRange.MotionRange.LimitHigh, ref _FMotionAbsolutePos, value, "FMotionAbsolutePos");
        }
        public double YMotionAbsolutePos
        {
            get => _YMotionAbsolutePos;
            set => LimitValue<double>(_YMotionRange.MotionRange.LimitLow, _YMotionRange.MotionRange.LimitHigh, ref _YMotionAbsolutePos, value, "YMotionAbsolutePos");
        }
        public double ZMotionAbsolutePos
        {
            get => _ZMotionAbsolutePos;
            set => LimitValue<double>(_ZMotionRange.MotionRange.LimitLow, _ZMotionRange.MotionRange.LimitHigh, ref _ZMotionAbsolutePos, value, "ZMotionAbsolutePos");
        }
        public double CMotionAbsolutePos
        {
            get => _CMotionAbsolutePos;
            set => LimitValue<double>(double.NegativeInfinity, double.PositiveInfinity, ref _CMotionAbsolutePos, value, "CMotionAbsolutePos");
        }
        public double XMotionAbsolutePos
        {
            get => _XMotionAbsolutePos;
            set => LimitValue<double>(_XMotionRange.MotionRange.LimitLow, _XMotionRange.MotionRange.LimitHigh, ref _XMotionAbsolutePos, value, "XMotionAbsolutePos");
        }
        public double FCDoorAbsolutePos
        {
            get => _FCDoorAbsolutePos;
            set => LimitValue<double>(_FCDoorRange.MotionRange.LimitLow, _FCDoorRange.MotionRange.LimitHigh, ref _FCDoorAbsolutePos, value, "FCDoorAbsolutePos");
        }
        public int FMotionPosShift
        {
            get => _FMotionPosShift;
            set
            {
                if (_FMotionPosShift != value)
                {
                    _FMotionPosShift = value;
                    RaisePropertyChanged("FMotionPosShift");
                }
            }
        }
        public double YMotionPosShift
        {
            get => _YMotionPosShift;
            set
            {
                if (_YMotionPosShift != value)
                {
                    _YMotionPosShift = value;
                    RaisePropertyChanged("YMotionPosShift");
                }
            }
        }
        public double ZMotionPosShift
        {
            get => _ZMotionPosShift;
            set
            {
                if (_ZMotionPosShift != value)
                {
                    _ZMotionPosShift = value;
                    RaisePropertyChanged("ZMotionPosShift");
                }
            }
        }
        public double XMotionPosShift
        {
            get => _XMotionPosShift;
            set
            {
                if (_XMotionPosShift != value)
                {
                    _XMotionPosShift = value;
                    RaisePropertyChanged("XMotionPosShift");
                }
            }
        }

        public double CMotionPosShift
        {
            get => _CMotionPosShift;
            set
            {
                if (_CMotionPosShift != value)
                {
                    _CMotionPosShift = value;
                    RaisePropertyChanged("CMotionPosShift");
                }
            }
        }
        public double FCDoorPosShift
        {
            get => _FCDoorPosShift;
            set
            {
                if (_FCDoorPosShift != value)
                {
                    _FCDoorPosShift = value;
                    RaisePropertyChanged("FCDoorPosShift");
                }
            }
        }
        public int FMotionCurrentPos
        {
            get => _FMotionCurrentPos;
            set
            {
                if (_FMotionCurrentPos != value)
                {
                    _FMotionCurrentPos = value;
                    RaisePropertyChanged("FMotionCurrentPos");
                }
            }
        }
        public double YMotionCurrentPos
        {
            get => _YMotionCurrentPos;
            set
            {
                if (_YMotionCurrentPos != value)
                {
                    _YMotionCurrentPos = value;
                    RaisePropertyChanged("YMotionCurrentPos");
                }
            }
        }
        public double ZMotionCurrentPos
        {
            get => _ZMotionCurrentPos;
            set
            {
                if (_ZMotionCurrentPos != value)
                {
                    _ZMotionCurrentPos = value;
                    RaisePropertyChanged("ZMotionCurrentPos");
                }
            }
        }
        public double? CMotionCurrentPos
        {
            get { return _CMotionCurrentPos; }
            set
            {
                if (_CMotionCurrentPos != value)
                {
                    _CMotionCurrentPos = value;
                    RaisePropertyChanged("CMotionCurrentPos");

                    if (_CMotionCurrentPos == 0)
                    {
                        FluidicsVM.IsCartridgeLoaded = false;
                    }
                    //else if(_Chiller.CheckCartridgeSippersReagentPos() || _Chiller.CheckCartridgeSippersWashPos())
                    //{
                    //    FluidicsVM.IsCartridgeLoaded = true;
                    //}
                    else if (_CMotionCurrentPos == SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos
                        || _CMotionCurrentPos == SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos)
                    {
                        FluidicsVM.IsCartridgeLoaded = true;
                    }
                }
            }
        }
        public double XMotionCurrentPos
        {
            get { return _XMotionCurrentPos; }
            set
            {
                if (_XMotionCurrentPos != value)
                {
                    _XMotionCurrentPos = value;
                    RaisePropertyChanged("XMotionCurrentPos");
                }
            }
        }
        public double FCDoorCurrentPos
        {
            get { return _FCDoorCurrentPos; }
            set
            {
                if (_FCDoorCurrentPos != value)
                {
                    _FCDoorCurrentPos = value;
                    RaisePropertyChanged("FCDoorCurrentPos");
                }
            }
        }

        public double XMotionEncoderPos
        {
            get { return _XMotionEncoderPos; }
            set
            {
                if (_XMotionEncoderPos != value)
                {
                    _XMotionEncoderPos = value;
                    RaisePropertyChanged(nameof(XMotionEncoderPos));
                }
            }
        }

        public double YMotionEncoderPos
        {
            get { return _YMotionEncoderPos; }
            set
            {
                if (_YMotionEncoderPos != value)
                {
                    _YMotionEncoderPos = value;
                    RaisePropertyChanged(nameof(YMotionEncoderPos));
                }
            }
        }

        public double FSpeedLimitH
        { get => _FMotionRange.SpeedRange.LimitHigh; }

        public double YSpeedLimitH
        { get => _YMotionRange.SpeedRange.LimitHigh; }

        public double ZSpeedLimitH
        { get => _ZMotionRange.SpeedRange.LimitHigh; }

        public double CSpeedLimitH
        { get => /*double.PositiveInfinity*/_CMotionRange.SpeedRange.LimitHigh; }

        public double XSpeedLimitH
        { get => _XMotionRange.SpeedRange.LimitHigh; }

        public double FCDoorSpeedLimitH
        { get => _FCDoorRange.SpeedRange.LimitHigh; }

        public double FAccelLimitH
        { get => _FMotionRange.AccelRange.LimitHigh; }

        public double YAccelLimitH
        { get => _YMotionRange.AccelRange.LimitHigh; }

        public double ZAccelLimitH
        { get => _ZMotionRange.AccelRange.LimitHigh; }

        public double CAccelLimitH
        { get => double.PositiveInfinity; }

        public double XAccelLimitH
        { get => _XMotionRange.AccelRange.LimitHigh; }

        public double FCDoorAccelLimitH
        { get => _FCDoorRange.AccelRange.LimitHigh; }

        public double FMotionLimitL
        {
            get => _FMotionRange.MotionRange.LimitLow;
            set
            {
                if (_FMotionRange.MotionRange.LimitLow != value)
                {
                    _FMotionRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("FMotionLimitL");
                }
            }
        }
        public double YMotionLimitL
        {
            get => _YMotionRange.MotionRange.LimitLow;
            set
            {
                if (_YMotionRange.MotionRange.LimitLow != value)
                {
                    _YMotionRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("YMotionLimitL");
                }
            }
        }
        public double ZMotionLimitL
        {
            get => _ZMotionRange.MotionRange.LimitLow;
            set
            {
                if (_ZMotionRange.MotionRange.LimitLow != value)
                {
                    _ZMotionRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("ZMotionLimitL");
                }
            }
        }
        public double XMotionLimitL
        {
            get => _XMotionRange.MotionRange.LimitLow;
            set
            {
                if (_XMotionRange.MotionRange.LimitLow != value)
                {
                    _XMotionRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("XMotionLimitL");
                }
            }
        }
        public double CMotionLimitL
        {
            get => _CMotionRange.MotionRange.LimitLow;
            set
            {
                if (_CMotionRange.MotionRange.LimitLow != value)
                {
                    _CMotionRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("CMotionLimitL");
                }
            }
        }
        public double FCDoorLimitL
        {
            get => _FCDoorRange.MotionRange.LimitLow;
            set
            {
                if (_FCDoorRange.MotionRange.LimitLow != value)
                {
                    _FCDoorRange.MotionRange.LimitLow = value;
                    RaisePropertyChanged("FCDoorLimitL");
                }
            }
        }
        public double FMotionLimitH
        {
            get => _FMotionRange.MotionRange.LimitHigh;
            set
            {
                if (_FMotionRange.MotionRange.LimitHigh != value)
                {
                    _FMotionRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("FMotionLimitH");
                }
            }
        }
        public double YMotionLimitH
        {
            get => _YMotionRange.MotionRange.LimitHigh;
            set
            {
                if (_YMotionRange.MotionRange.LimitHigh != value)
                {
                    _YMotionRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("YMotionLimitH");
                }
            }
        }
        public double ZMotionLimitH
        {
            get => _ZMotionRange.MotionRange.LimitHigh;
            set
            {
                if (_ZMotionRange.MotionRange.LimitHigh != value)
                {
                    _ZMotionRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("ZMotionLimitH");
                }
            }
        }
        public double XMotionLimitH
        {
            get => _XMotionRange.MotionRange.LimitHigh;
            set
            {
                if (_XMotionRange.MotionRange.LimitHigh != value)
                {
                    _XMotionRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("XMotionLimitH");
                }
            }
        }
        public double CMotionLimitH
        {
            get => _CMotionRange.MotionRange.LimitHigh;
            set
            {
                if (_CMotionRange.MotionRange.LimitHigh != value)
                {
                    _CMotionRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("CMotionLimitH");
                }
            }
        }
        public double FCDoorLimitH
        {
            get => _FCDoorRange.MotionRange.LimitHigh;
            set
            {
                if (_FCDoorRange.MotionRange.LimitHigh != value)
                {
                    _FCDoorRange.MotionRange.LimitHigh = value;
                    RaisePropertyChanged("FCDoorLimitH");
                }
            }
        }
        public double CMotionCoeff
        {
            get => _CMotionCoeff;
            set
            {
                if (_CMotionCoeff != value)
                {
                    if (value <= 0)
                    {
                        ShowMessage("Coefficient must be larger than zero");
                        return;
                    }
                    _CMotionCoeff = value;
                    RaisePropertyChanged(nameof(CMotionCoeff));
                }
            }
        }

        public double YMotionCoeff { get; set; }
        public double FMotionCoeff { get; set; }
        public double XMotionCoeff { get; set; }
        public double FCDoorCoeff { get; set; }

        public int[] FilterOptions { get; }
        private int _SelectedFilter;
        public int SelectedFilter
        {
            get => _SelectedFilter;
            set
            {
                if (_SelectedFilter != value)
                {
                    _SelectedFilter = value;
                    RaisePropertyChanged(nameof(SelectedFilter));
                }
            }
        }
        public bool EnableX
        {
            get => _IsXMotionEnabled;
            set
            {
                if (_IsXMotionEnabled != value)
                {
                    if (MotionController.HywireMotionController.SetEnable(Hywire.MotionControl.MotorTypes.Motor_X, new bool[] { value }))
                    {
                        _IsXMotionEnabled = value;
                        RaisePropertyChanged(nameof(EnableX));
                    }
                }
            }
        }
        public bool EnableY
        {
            get => _IsYMotionEnabled;
            set
            {
                if (_IsYMotionEnabled != value)
                {
                    if (MotionController.HywireMotionController.SetEnable(Hywire.MotionControl.MotorTypes.Motor_Y, new bool[] { value }))
                    {
                        _IsYMotionEnabled = value;
                        RaisePropertyChanged(nameof(EnableY));
                    }
                }
            }
        }

        public bool IsXBusy
        {
            get => _IsXBusy;
            set
            {
                if (_IsXBusy != value)
                {
                    _IsXBusy = value;
                    RaisePropertyChanged(nameof(IsXBusy));
                }
            }
        }
        public bool IsYBusy
        {
            get => _IsYBusy;
            set
            {
                if (_IsYBusy != value)
                {
                    _IsYBusy = value;
                    RaisePropertyChanged(nameof(IsYBusy));
                }
            }
        }
        public string CartridgeMotorStatus
        {
            get => _CartridgeMotorStatus;
            set
            {
                if (_CartridgeMotorStatus != value)
                {
                    _CartridgeMotorStatus = value;
                    RaisePropertyChanged(nameof(CartridgeMotorStatus));
                }
            }
        }
        public bool IsProtocolRev2 { get; set; }
        public List<int> CartridgeMotorSpeedOptions { get; }
        public int SelectedCartridgeMotorSpeed
        {
            get => _SelectedCartridgeMotorSpeed;
            set
            {
                if (_SelectedCartridgeMotorSpeed != value)
                {
                    _SelectedCartridgeMotorSpeed = value;
                    RaisePropertyChanged(nameof(SelectedCartridgeMotorSpeed));
                    _Chiller.SetChillerMotorSpeed(SelectedCartridgeMotorSpeed);
                }
            }
        }
        public bool IsChillerDoorLocked
        {
            get => _IsChillerDoorLocked;
            set
            {
                if (_IsChillerDoorLocked != value)
                {
                    _IsChillerDoorLocked = value;
                    RaisePropertyChanged(nameof(IsChillerDoorLocked));
                    _Chiller.ChillerDoorControl(_IsChillerDoorLocked);
                }
            }
        }

        public bool IsFcDoorSensorEnabled
        {
            get => MotionController.IsFcDoorSensorEnabled;
            set
            {
                if ( MotionController.IsFcDoorSensorEnabled != value)
                {
                    if(value == false)
                    {
                        string message = "Collisions between y-axis and FC door are possible when sensor is disabled";
                        string caption = "Warning";
                        ShowMessage(message, false, caption, MessageBoxImage.Warning);
                    }
                    MotionController.IsFcDoorSensorEnabled = value;
                    SerialPeripherals.MainBoardController.GetInstance().IsFCDoorEnable = value;
                    RaisePropertyChanged(nameof(MotionController.IsFcDoorSensorEnabled));
                }
            }
        }
        #endregion Public Properties

        #region Home Command
        private RelayCommand _HomeCmd;
        public ICommand HomeCmd
        {
            get
            {
                if (_HomeCmd == null)
                {
                    _HomeCmd = new RelayCommand(async o => await ExecuteHomeCmd(o), o => CanExecuteHomeCmd);
                }
                return _HomeCmd;
            }
        }

        private async Task ExecuteHomeCmd(object obj)
        {
            MotionTypes type = (MotionTypes)obj;
            int speed = 0;
            int accel = 0;
            bool backupCanExecuteHomeCmd = CanExecuteHomeCmd;
            CanExecuteHomeCmd = false;
            bool doHome = true;
            if (type == MotionTypes.Filter)
            {
                //speed = (int)(FMotionSpeed * FMotionCoeff);
                //accel = (int)(FMotionAccel * FMotionCoeff);
                speed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Filter].Speed * FMotionCoeff);
                accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Filter].Accel * FMotionCoeff);
            }
            else if (type == MotionTypes.YStage)
            {
                //speed = (int)(YMotionSpeed * YMotionCoeff);
                //accel = (int)(YMotionAccel * YMotionCoeff);
                speed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed * YMotionCoeff);
                accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel * YMotionCoeff);
            }
            else if (type == MotionTypes.ZStage)
            {
                //if (MotionController.HomeZstage() == false)
                if (await Task<bool>.Run(() => MotionController.HomeZstage()) == false)
                {
                    ShowMessage("Z-stage homing failed");
                }
                ZMotionCurrentPos = 0;
                doHome = false;
                //return;
            }
            else if (type == MotionTypes.Cartridge)
            {
                //speed = (int)(CMotionSpeed * CMotionCoeff);
                //accel = (int)(CMotionAccel * CMotionCoeff);
                if (IsMachineRev2P4)
                {
                    SerialPeripherals.Chiller chillerController = SerialPeripherals.Chiller.GetInstance();
                    chillerController.ChillerMotorControl(false);
                    doHome = false;
                }
                else
                {
                    speed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Speed * CMotionCoeff);
                    accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.Cartridge].Accel * CMotionCoeff);
                }
            }
            else if (type == MotionTypes.XStage)
            {
                //speed = (int)(XMotionSpeed * XMotionCoeff);
                //accel = (int)(XMotionAccel * XMotionCoeff);
                speed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed * XMotionCoeff);
                accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel * XMotionCoeff);
            }
            else if (type == MotionTypes.FCDoor)
            {
                if (YMotionCurrentPos > 30)
                {
                    MessageBox.Show("Y stage is out, please move it in first!");
                    doHome = false;
                }
                speed = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.FCDoor].Speed * FCDoorCoeff);
                accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.FCDoor].Accel * FCDoorCoeff);
            }
            else
            {
                //return;
                doHome = false;
            }
            //if (MotionController.HomeMotion(type, speed, accel, false) == false)
            if (doHome)
            {
                if (await Task<bool>.Run(() => MotionController.HomeMotion(type, speed, accel, false)) == false)
                {
                    ShowMessage($"{Enum.GetName(typeof(MotionTypes), type)} Homing Failed");
                }
            }
            CanExecuteHomeCmd = backupCanExecuteHomeCmd;
        }

        //private bool CanExecuteHomeCmd(object obj)
        //{
        //    return true;
        //}
        bool _CanExecuteHomeCmd = true;
        public bool CanExecuteHomeCmd
        {
            get { return _CanExecuteHomeCmd; }
            set
            {
                if (_CanExecuteHomeCmd != value)
                {
                    _CanExecuteHomeCmd = value;
                    RaisePropertyChanged("CanExecuteHomeCmd");
                    ((RelayCommand)HomeCmd).RaiseCanExecuteChanged();
                }
            }
        }


        private void ShowMessage(string message, bool succcess = false, string caption = "Error", MessageBoxImage image = MessageBoxImage.Error)
        {
            Dispatch(() =>
            {
                if (succcess)
                {
                    ShowMessage(message);
                }
                else
                {

                    MessageBox.Show(message, caption, MessageBoxButton.OK, image);
                }
            });
        }
        #endregion Home Command

        #region Absolute Move Command
        private RelayCommand _AbsoluteMoveCmd;
        public ICommand AbsoluteMoveCmd
        {
            get
            {
                if (_AbsoluteMoveCmd == null)
                {
                    _AbsoluteMoveCmd = new RelayCommand(async o => await ExecuteAbsoluteMoveCmd(o), o => CanExecuteAbsoluteMoveCmd);
                }
                return _AbsoluteMoveCmd;
            }
        }

        private async Task ExecuteAbsoluteMoveCmd(object obj)
        {
            MotionTypes type = (MotionTypes)obj;
            int pos = 0;
            int accel = 0;
            int speed = 0;
            bool doMove = true;
            bool backupCanExecuteAbsoluteMoveCmd = CanExecuteAbsoluteMoveCmd;
            CanExecuteAbsoluteMoveCmd = false;
            switch (type)
            {
                case MotionTypes.Filter:
                    pos = (int)Math.Round((SettingsManager.ConfigSettings.FilterPositionSettings[SelectedFilter] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]));
                    speed = (int)Math.Round((FMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]));
                    accel = (int)Math.Round((FMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]));
                    break;
                case MotionTypes.YStage:
                    pos = (int)Math.Round((YMotionAbsolutePos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                    speed = (int)Math.Round((YMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                    accel = (int)Math.Round((YMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                    break;
                case MotionTypes.ZStage:
                    // stage resolution is very high, so use double as parameters data type (unit of um, um/s, um/s^2)
                    if (MotionController.AbsoluteMoveZStage(ZMotionAbsolutePos, ZMotionSpeed, ZMotionAccel) == false)
                    {
                        ShowMessage("Z-stage movement failed");
                    }
                    //return;
                    doMove = false;
                    break;
                case MotionTypes.Cartridge:
                    if (IsMachineRev2P4)
                    {
                        doMove = false;
                        if (_Chiller.SetChillerMotorAbsMove(CMotionAbsolutePos) == false)
                        {
                            ShowMessage("Cartridge movement failed");
                        }
                    }
                    else
                    {
                        pos = (int)Math.Round((CMotionAbsolutePos * CMotionCoeff));
                        speed = (int)Math.Round((CMotionSpeed * CMotionCoeff));
                        accel = (int)Math.Round((CMotionAccel * CMotionCoeff));
                    }
                    break;
                case MotionTypes.XStage:
                    pos = (int)Math.Round((XMotionAbsolutePos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                    speed = (int)Math.Round((XMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                    accel = (int)Math.Round((XMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                    break;
                case MotionTypes.FCDoor:
                    if (YMotionCurrentPos > 30)
                    {
                        MessageBox.Show("Y stage is out, please move it in first!");
                        doMove = false;
                    }
                    pos = (int)Math.Round((FCDoorAbsolutePos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                    speed = (int)Math.Round((FCDoorSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                    accel = (int)Math.Round((FCDoorAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                    break;
                default:
                    //return;
                    doMove = false;
                    break;
            }
            if (doMove)
            {
                if (await Task.Run(() => MotionController.AbsoluteMove(type, pos, speed, accel, true) == false))
                {
                    ShowMessage($"{Enum.GetName(typeof(MotionTypes), type)} movement failed");
                }
            }
            CanExecuteAbsoluteMoveCmd = backupCanExecuteAbsoluteMoveCmd;
        }

        //private bool CanExecuteAbsoluteMoveCmd(object obj)
        //{
        //    return true;
        //}
        bool _CanExecuteAbsoluteMoveCmd = true;
        public bool CanExecuteAbsoluteMoveCmd
        {
            get { return _CanExecuteAbsoluteMoveCmd; }
            set
            {
                if (_CanExecuteAbsoluteMoveCmd != value)
                {
                    _CanExecuteAbsoluteMoveCmd = value;
                    RaisePropertyChanged("CanExecuteAbsoluteMoveCmd");
                    ((RelayCommand)AbsoluteMoveCmd).RaiseCanExecuteChanged();
                }
            }
        }
        #endregion Absolute Move Command

        #region Relative Move Command
        private RelayCommand _RelativeMoveCmd;
        public ICommand RelativeMoveCmd
        {
            get
            {
                if (_RelativeMoveCmd == null)
                {
                    _RelativeMoveCmd = new RelayCommand(async o => await ExecuteRelativeMoveCmd(o), o => CanExecuteRelativeMoveCmd);
                }
                return _RelativeMoveCmd;
            }
        }

        private async Task ExecuteRelativeMoveCmd(object obj)
        {
            string parameter = (string)obj;
            string type = parameter.Substring(0, 1);
            string dir = parameter.Substring(1);
            bool backupCanExecuteRelativeMoveCmd = CanExecuteRelativeMoveCmd;
            CanExecuteRelativeMoveCmd = false;
            switch (type)
            {
                case "F":
                    if (FMotionPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (F)");
                        //return;
                    }
                    else
                    {
                        if (dir == "Positive")
                        {
                            if (FMotionCurrentPos + FMotionPosShift > FMotionLimitH)
                            {
                                ShowMessage("The requested position is above the maximum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    int nextFilter = FMotionCurrentPos + FMotionPosShift;
                                    double pos = SettingsManager.ConfigSettings.FilterPositionSettings[nextFilter];
                                    MotionController.AbsoluteMove(
                                        MotionTypes.Filter,
                                        (int)Math.Round((pos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter])),
                                        (int)Math.Round((FMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter])),
                                        (int)Math.Round((FMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter])));
                                });
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (FMotionCurrentPos - FMotionPosShift < FMotionLimitL)
                            {
                                ShowMessage("The requested position is below the minimum position limit");
                                //return;
                            }
                            else
                            {
                                int nextFilter = FMotionCurrentPos - FMotionPosShift;
                                if (nextFilter <= 0)
                                {
                                    await ExecuteHomeCmd(MotionTypes.Filter);
                                }
                                else
                                {
                                    await Task.Run(() =>
                                    {
                                        double pos = SettingsManager.ConfigSettings.FilterPositionSettings[nextFilter];
                                        MotionController.AbsoluteMove(
                                            MotionTypes.Filter,
                                            (int)Math.Round(pos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter]),
                                            (int)Math.Round((FMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter])),
                                            (int)Math.Round((FMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter])));
                                    });
                                }
                            }
                        }
                    }
                    break;
                case "Y":
                    if (YMotionPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (Y)");
                        //return;
                    }
                    else
                    {
                        if (dir == "Positive")
                        {
                            if (YMotionCurrentPos + YMotionPosShift > YMotionLimitH)
                            {
                                ShowMessage("The requested position is above the maximum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((YMotionPosShift + YMotionCurrentPos) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                                        int speed = (int)Math.Round((YMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                                        int accel = (int)Math.Round((YMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                                        MotionController.AbsoluteMove(MotionTypes.YStage, tgtPos, speed, accel);
                                    }
                                    else
                                    {
                                        MotionController.RelativeMove(
                                            MotionTypes.YStage,
                                            (int)Math.Round((YMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])),
                                            (int)Math.Round((YMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])),
                                            (int)Math.Round((YMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])));
                                    }
                                });
                                //YMotionCurrentPos += YMotionPosShift;
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (YMotionCurrentPos - YMotionPosShift < YMotionLimitL)
                            {
                                ShowMessage("The requested position is below the minimum position limit");
                                // return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((YMotionCurrentPos - YMotionPosShift) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                                        int speed = (int)Math.Round((YMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                                        int accel = (int)Math.Round((YMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                                        MotionController.AbsoluteMove(MotionTypes.YStage, tgtPos, speed, accel);
                                    }
                                    else
                                    {
                                        MotionController.RelativeMove(
                                            MotionTypes.YStage,
                                            (int)Math.Round((-1 * YMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])),
                                            (int)Math.Round((YMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])),
                                            (int)Math.Round((YMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage])));
                                    }
                                    //YMotionCurrentPos -= YMotionPosShift;
                                });
                            }
                        }
                    }
                    break;
                case "Z":
                    if (ZMotionPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (Z)");
                        //return;
                    }
                    else
                    {
                        if (dir == "Positive")
                        {
                            if (ZMotionCurrentPos + ZMotionPosShift > ZMotionLimitH)
                            {
                                ShowMessage("The requested position is above the maximum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    MotionController.RelativeMoveZStage(
                                    ZMotionPosShift,
                                    ZMotionSpeed,
                                    ZMotionAccel);
                                });
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (ZMotionCurrentPos - ZMotionPosShift < ZMotionLimitL)
                            {
                                ShowMessage("The requested position is below the minimum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    MotionController.RelativeMoveZStage(
                                    -1 * ZMotionPosShift,
                                    ZMotionSpeed,
                                    ZMotionAccel);
                                });
                            }
                        }
                    }
                    break;
                case "X":
                    if (XMotionPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (X)");
                        //return;
                    }
                    else
                    {
                        if (dir == "Positive")
                        {
                            if (XMotionCurrentPos + XMotionPosShift > XMotionLimitH)
                            {
                                ShowMessage("The requested position is above the maximum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((XMotionPosShift + XMotionCurrentPos) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                                        int speed = (int)Math.Round((XMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                                        int accel = (int)Math.Round((XMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                                        MotionController.AbsoluteMove(MotionTypes.XStage, tgtPos, speed, accel);
                                    }
                                    else
                                    {
                                        MotionController.RelativeMove(
                                            MotionTypes.XStage,
                                            (int)Math.Round((XMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])),
                                            (int)Math.Round((XMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])),
                                            (int)Math.Round((XMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])));
                                    }
                                    //YMotionCurrentPos += YMotionPosShift;
                                });
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (XMotionCurrentPos - XMotionPosShift < XMotionLimitL)
                            {
                                ShowMessage("The requested position is below the minimum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((XMotionCurrentPos - XMotionPosShift) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                                        int speed = (int)Math.Round((XMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                                        int accel = (int)Math.Round((XMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                                        MotionController.AbsoluteMove(MotionTypes.XStage, tgtPos, speed, accel);
                                    }
                                    else
                                    {
                                        MotionController.RelativeMove(
                                            MotionTypes.XStage,
                                            (int)Math.Round((-1 * XMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])),
                                            (int)Math.Round((XMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])),
                                            (int)Math.Round((XMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage])));
                                    }
                                    //YMotionCurrentPos -= YMotionPosShift;

                                });
                            }
                        }
                    }
                    break;
                case "C":
                    if (CMotionPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (Cartridge)");
                        //return;
                    }
                    else
                    {
                        bool toMove = true;
                        if (dir == "Positive")
                        {
                            if (IsMachineRev2P4)
                            {
                                if (IsProtocolRev2)
                                {
                                    if (CMotionCurrentPos + CMotionPosShift > CMotionLimitH)
                                    {
                                        ShowMessage("The requested position is above the maximum position limit");
                                        toMove = false;
                                        //return;
                                    }
                                }
                                if (toMove)
                                {
                                    SerialPeripherals.Chiller.GetInstance().SetChillerMotorRelMove(false, CMotionPosShift);
                                    int waitCnts = 0;
                                    do
                                    {
                                        waitCnts++;
                                        if (waitCnts > 150)     // timeout if waiting more than 150 sec
                                        {
                                            MessageBox.Show("Cartridge moving down timeout.");
                                            break;
                                        }
                                        if (SerialPeripherals.Chiller.GetInstance().CartridgeMotorStatus == SerialPeripherals.Chiller.CartridgeStatusTypes.MovingUpError)
                                        {
                                            MessageBox.Show("Cartridge moving down failed.");
                                            break;
                                        }
                                        System.Threading.Thread.Sleep(1000);
                                    }
                                    while (SerialPeripherals.Chiller.GetInstance().CartridgeMotorStatus == SerialPeripherals.Chiller.CartridgeStatusTypes.MovingDown);
                                }
                            }
                            else
                            {
                                if (CMotionCurrentPos + CMotionPosShift > CMotionLimitH)
                                {
                                    ShowMessage("The requested position is above the maximum position limit");
                                    //return;
                                }
                                else
                                {
                                    await Task.Run(() =>
                                    {
                                        if (MotionController.UsingControllerV2)
                                        {
                                            int tgtPos = (int)Math.Round((double)(((double)CMotionPosShift + CMotionCurrentPos) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                                            int speed = (int)Math.Round((CMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                                            int accel = (int)Math.Round((CMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                                            MotionController.AbsoluteMove(MotionTypes.Cartridge, tgtPos, speed, accel);
                                        }
                                        else
                                        {
                                            MotionController.RelativeMove(
                                                MotionTypes.Cartridge,
                                                (int)Math.Round((CMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])),
                                                (int)Math.Round((CMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])),
                                                (int)Math.Round((CMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])));
                                        }
                                    });
                                }
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (IsMachineRev2P4)
                            {
                                if (IsProtocolRev2)
                                {
                                    if (CMotionCurrentPos - CMotionPosShift < CMotionLimitL)
                                    {
                                        ShowMessage("The requested position is below the minimum position limit");
                                        toMove = false;
                                        //return;
                                    }
                                }
                                if (toMove)
                                {
                                    SerialPeripherals.Chiller.GetInstance().SetChillerMotorRelMove(true, CMotionPosShift);
                                    int waitCnts = 0;
                                    do
                                    {
                                        waitCnts++;
                                        if (waitCnts > 150)     // timeout if waiting more than 150 sec
                                        {
                                            MessageBox.Show("Cartridge moving up timeout.");
                                            break;
                                        }
                                        if (SerialPeripherals.Chiller.GetInstance().CartridgeMotorStatus == SerialPeripherals.Chiller.CartridgeStatusTypes.MovingDownError)
                                        {
                                            MessageBox.Show("Cartridge moving up failed.");
                                            break;
                                        }
                                        System.Threading.Thread.Sleep(1000);
                                    }
                                    while (SerialPeripherals.Chiller.GetInstance().CartridgeMotorStatus == SerialPeripherals.Chiller.CartridgeStatusTypes.MovingUp);
                                }
                            }
                            else
                            {
                                if (CMotionCurrentPos - CMotionPosShift < CMotionLimitL)
                                {
                                    ShowMessage("The requested position is below the minimum position limit");
                                    //return;
                                }
                                else
                                {
                                    await Task.Run(() =>
                                    {
                                        if (MotionController.UsingControllerV2)
                                        {
                                            int tgtPos = (int)Math.Round(((double)CMotionCurrentPos - CMotionPosShift) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                                            int speed = (int)Math.Round((CMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                                            int accel = (int)Math.Round((CMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                                            MotionController.AbsoluteMove(MotionTypes.Cartridge, tgtPos, speed, accel);
                                        }
                                        else
                                        {
                                            MotionController.RelativeMove(
                                                MotionTypes.Cartridge,
                                                (int)Math.Round((-1 * CMotionPosShift * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])),
                                                (int)Math.Round((CMotionSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])),
                                                (int)Math.Round((CMotionAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge])));
                                        }
                                        //YMotionCurrentPos -= YMotionPosShift;

                                    });
                                }
                            }
                        }
                    }
                    break;
                case "D":
                    if (FCDoorPosShift == 0)
                    {
                        ShowMessage("Please set a non-zero value for Relative Move (FC Door)");
                        //return;
                    }
                    else
                    {
                        if (YMotionCurrentPos > 30)
                        {
                            MessageBox.Show("Y stage is out, please move it in first!");
                        }
                        else if (dir == "Positive")
                        {
                            if (FCDoorCurrentPos + FCDoorPosShift > FCDoorLimitH)
                            {
                                ShowMessage("The requested position is above the maximum position limit");
                                //return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((FCDoorPosShift + FCDoorCurrentPos) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]);
                                        int speed = (int)Math.Round((FCDoorSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                                        int accel = (int)Math.Round((FCDoorAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                                        MotionController.AbsoluteMove(MotionTypes.FCDoor, tgtPos, speed, accel);
                                    }
                                });
                                //YMotionCurrentPos += YMotionPosShift;
                            }
                        }
                        else if (dir == "Negative")
                        {
                            if (FCDoorCurrentPos - FCDoorPosShift < FCDoorLimitL)
                            {
                                ShowMessage("The requested position is below the minimum position limit");
                                // return;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    if (MotionController.UsingControllerV2)
                                    {
                                        int tgtPos = (int)Math.Round((FCDoorCurrentPos - FCDoorPosShift) * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]);
                                        int speed = (int)Math.Round((FCDoorSpeed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                                        int accel = (int)Math.Round((FCDoorAccel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor]));
                                        MotionController.AbsoluteMove(MotionTypes.FCDoor, tgtPos, speed, accel);
                                    }
                                    //YMotionCurrentPos -= YMotionPosShift;
                                });
                            }
                        }
                    }
                    break;
            }
            CanExecuteRelativeMoveCmd = backupCanExecuteRelativeMoveCmd;
        }

        //private bool CanExecuteRelativeMoveCmd(object obj)
        //{
        //    return true;
        //}
        bool _CanExecuteRelativeMoveCmd = true;
        public bool CanExecuteRelativeMoveCmd
        {
            get { return _CanExecuteRelativeMoveCmd; }
            set
            {
                if (_CanExecuteRelativeMoveCmd != value)
                {
                    _CanExecuteRelativeMoveCmd = value;
                    RaisePropertyChanged("CanExecuteRelativeMoveCmd");
                    ((RelayCommand)RelativeMoveCmd).RaiseCanExecuteChanged();
                }
            }
        }
        #endregion Relative Move Command

        #region ResetEncoderPosCmd
        private RelayCommand _ResetEncoderPosCmd;
        public RelayCommand ResetEncoderPosCmd
        {
            get
            {
                if (_ResetEncoderPosCmd == null)
                {
                    _ResetEncoderPosCmd = new RelayCommand(ExecuteResetEncoderPosCmd, CanExecuteResetEncoderPosCmd);
                }
                return _ResetEncoderPosCmd;
            }
        }

        private void ExecuteResetEncoderPosCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "X":
                    MotionController.HywireMotionController.ResetEncoderPosition(Hywire.MotionControl.MotorTypes.Motor_X);
                    MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X);
                    XMotionEncoderPos = MotionController.XEncoderPos;
                    break;
                case "Y":
                    MotionController.HywireMotionController.ResetEncoderPosition(Hywire.MotionControl.MotorTypes.Motor_Y);
                    MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Y);
                    YMotionEncoderPos = MotionController.YEncoderPos;
                    break;
            }
        }

        private bool CanExecuteResetEncoderPosCmd(object obj)
        {
            return true;
        }
        #endregion ResetEncoderPosCmd

        #region Stop Move Command
        private RelayCommand _StopMoveCmd;
        public RelayCommand StopMoveCmd
        {
            get
            {
                if (_StopMoveCmd == null)
                {
                    _StopMoveCmd = new RelayCommand(ExecuteStopMoveCmd, CanExecuteStopMoveCmd);
                }
                return _StopMoveCmd;
            }
        }

        private void ExecuteStopMoveCmd(object obj)
        {
            if (IsMachineRev2)
            {
                MotionTypes motion = (MotionTypes)obj;
                MotorTypes motor;
                switch (motion)
                {
                    case MotionTypes.XStage:
                        motor = MotorTypes.Motor_X;
                        break;
                    case MotionTypes.YStage:
                        motor = MotorTypes.Motor_Y;
                        break;
                    case MotionTypes.Cartridge:
                        motor = MotorTypes.Motor_Z;
                        break;
                    case MotionTypes.FCDoor:
                        motor = MotorTypes.Motor_Z;
                        break;
                    default:
                        return;
                }
                MotionController.HywireMotionController.SetStart(motor, new bool[] { false });
            }
        }

        private bool CanExecuteStopMoveCmd(object obj)
        {
            return true;
        }
        #endregion Stop Move Command
    }
}
