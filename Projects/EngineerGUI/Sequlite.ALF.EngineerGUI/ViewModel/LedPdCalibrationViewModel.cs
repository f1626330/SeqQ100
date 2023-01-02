using Microsoft.Research.DynamicDataDisplay.DataSources;
using Sequlite.ALF.Common;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class LedPdCalibrationViewModel : ViewModelBase
    {
        #region Private Fields
        LEDController _LEDController;
        private int _LEDCurrentAt1;
        private int _LEDCurrentAt10;
        private int _LEDCurrentAt20;
        private int _LEDCurrentAt30;
        private int _LEDCurrentAt40;
        private int _LEDCurrentAt50;
        private int _LEDCurrentAt60;
        private int _LEDCurrentAt70;
        private int _LEDCurrentAt80;
        private int _LEDCurrentAt90;
        private int _LEDCurrentAt100;
        private int _CalibrateCurrent;
        #endregion Private Fields

        public LedPdCalibrationViewModel(LEDController ledcontroller)
        {
            _LEDController = ledcontroller;
            LEDTypeOptions = new List<LEDTypes>();
            LEDTypeOptions.Add(LEDTypes.Green);
            LEDTypeOptions.Add(LEDTypes.Red);
            LEDTypeOptions.Add(LEDTypes.White);
            SelectedLEDType = LEDTypeOptions[0];
            LEDCurrentSet = 100;
            LEDCurrentAt1 = 1;
            LEDCurrentAt10 = 10;
            LEDCurrentAt20 = 20;
            LEDCurrentAt30 = 30;
            LEDCurrentAt40 = 40;
            LEDCurrentAt50 = 50;
            LEDCurrentAt60 = 60;
            LEDCurrentAt70 = 70;
            LEDCurrentAt80 = 80;
            LEDCurrentAt90 = 90;
            LEDCurrentAt100 = 100;
            SamplePoints = 200;
            PDValueLine = new ObservableDataSource<Point>();

            CalibratePowerOptions = new List<int>();
            for (int i = 1; i < 10; i++)
            {
                CalibratePowerOptions.Add(i);
            }
            for (int i = 1; i <= 10; i++)
            {
                CalibratePowerOptions.Add(i * 10);
            }
            SelectedCalibratePower = 1;
            IsProtocolRev2 = _LEDController.IsProtocolRev2;
        }

        #region Public Properties
        public List<LEDTypes> LEDTypeOptions { get; }
        public LEDTypes SelectedLEDType { get; set; }
        public int LEDCurrentSet { get; set; }
        public int LEDVoltageSet { get; set; }
        public bool IsProtocolRev2 { get; set; }
        public List<int> CalibratePowerOptions { get; }
        public int SelectedCalibratePower { get; set; }
        public int CalibrateCurrent
        {
            get => _CalibrateCurrent;
            set
            {
                if (_CalibrateCurrent != value)
                {
                    _CalibrateCurrent = value;
                    RaisePropertyChanged(nameof(CalibrateCurrent));
                }
            }
        }
        public int LEDCurrentAt1
        {
            get { return _LEDCurrentAt1; }
            set
            {
                if (_LEDCurrentAt1 != value)
                {
                    _LEDCurrentAt1 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt1));
                }
            }
        }
        public int LEDCurrentAt10
        {
            get { return _LEDCurrentAt10; }
            set
            {
                if (_LEDCurrentAt10 != value)
                {
                    _LEDCurrentAt10 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt10));
                }
            }
        }
        public int LEDCurrentAt20
        {
            get { return _LEDCurrentAt20; }
            set
            {
                if (_LEDCurrentAt20 != value)
                {
                    _LEDCurrentAt20 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt20));
                }
            }
        }
        public int LEDCurrentAt30
        {
            get { return _LEDCurrentAt30; }
            set
            {
                if (_LEDCurrentAt30 != value)
                {
                    _LEDCurrentAt30 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt30));
                }
            }
        }
        public int LEDCurrentAt40
        {
            get { return _LEDCurrentAt40; }
            set
            {
                if (_LEDCurrentAt40 != value)
                {
                    _LEDCurrentAt40 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt40));
                }
            }
        }
        public int LEDCurrentAt50
        {
            get { return _LEDCurrentAt50; }
            set
            {
                if (_LEDCurrentAt50 != value)
                {
                    _LEDCurrentAt50 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt50));
                }
            }
        }
        public int LEDCurrentAt60
        {
            get { return _LEDCurrentAt60; }
            set
            {
                if (_LEDCurrentAt60 != value)
                {
                    _LEDCurrentAt60 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt60));
                }
            }
        }
        public int LEDCurrentAt70
        {
            get { return _LEDCurrentAt70; }
            set
            {
                if (_LEDCurrentAt70 != value)
                {
                    _LEDCurrentAt70 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt70));
                }
            }
        }
        public int LEDCurrentAt80
        {
            get { return _LEDCurrentAt80; }
            set
            {
                if (_LEDCurrentAt80 != value)
                {
                    _LEDCurrentAt80 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt80));
                }
            }
        }
        public int LEDCurrentAt90
        {
            get { return _LEDCurrentAt90; }
            set
            {
                if (_LEDCurrentAt90 != value)
                {
                    _LEDCurrentAt90 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt90));
                }
            }
        }
        public int LEDCurrentAt100
        {
            get { return _LEDCurrentAt100; }
            set
            {
                if (_LEDCurrentAt100 != value)
                {
                    _LEDCurrentAt100 = value;
                    RaisePropertyChanged(nameof(LEDCurrentAt100));
                }
            }
        }
        public int SamplePoints { get; set; }
        public ObservableDataSource<Point> PDValueLine { get; }
        #endregion Public Properties

        #region SetCmd
        private RelayCommand _SetCmd;
        public RelayCommand SetCmd
        {
            get
            {
                if (_SetCmd == null)
                {
                    _SetCmd = new RelayCommand(ExecuteSetCmd, CanExecuteSetCmd);
                }
                return _SetCmd;
            }
        }

        private void ExecuteSetCmd(object obj)
        {
            switch (obj.ToString())
            {
                case "LEDOn":
                    _LEDController.SetLEDDriveCurrent(SelectedLEDType, LEDCurrentSet);
                    _LEDController.SetLEDStatus(SelectedLEDType, true);
                    break;
                case "LEDOff":
                    _LEDController.SetLEDStatus(SelectedLEDType, false);
                    break;
                case "Set1":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 1, LEDCurrentAt1);
                    break;
                case "Set10":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 10, LEDCurrentAt10);
                    break;
                case "Set20":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 20, LEDCurrentAt20);
                    break;
                case "Set30":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 30, LEDCurrentAt30);
                    break;
                case "Set40":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 40, LEDCurrentAt40);
                    break;
                case "Set50":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 50, LEDCurrentAt50);
                    break;
                case "Set60":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 60, LEDCurrentAt60);
                    break;
                case "Set70":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 70, LEDCurrentAt70);
                    break;
                case "Set80":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 80, LEDCurrentAt80);
                    break;
                case "Set90":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 90, LEDCurrentAt90);
                    break;
                case "Set100":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, 100, LEDCurrentAt100);
                    break;
                case "SetPoints":
                    _LEDController.WriteRegisters(LEDController.Registers.PDSamplePoints, 1, new int[] { SamplePoints });
                    break;
                case "Curve":
                    if (_LEDController.ReadRegisters(LEDController.Registers.PDSampleData, 1))
                    {
                        PDValueLine.Collection.Clear();
                        Point[] data = new Point[_LEDController.PDCurve.Length];
                        for (int i = 0; i < data.Length; i++)
                        {
                            data[i].X = i * 5;
                            data[i].Y = _LEDController.PDCurve[i];
                        }
                        PDValueLine.AppendMany(data);
                        RaisePropertyChanged(nameof(PDValueLine));
                    }
                    break;
                case "Get1":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 1))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt1 = _LEDController.GLEDCalibrateCurrents[1];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt1 = _LEDController.RLEDCalibrateCurrents[1];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt1 = _LEDController.WLEDCalibrateCurrents[1];
                                break;
                        }
                    }
                    break;
                case "Get10":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 10))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt10 = _LEDController.GLEDCalibrateCurrents[10];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt10 = _LEDController.RLEDCalibrateCurrents[10];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt10 = _LEDController.WLEDCalibrateCurrents[10];
                                break;
                        }
                    }
                    break;
                case "Get20":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 20))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt20 = _LEDController.GLEDCalibrateCurrents[20];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt20 = _LEDController.RLEDCalibrateCurrents[20];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt20 = _LEDController.WLEDCalibrateCurrents[20];
                                break;
                        }
                    }
                    break;
                case "Get30":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 30))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt30 = _LEDController.GLEDCalibrateCurrents[30];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt30 = _LEDController.RLEDCalibrateCurrents[30];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt30 = _LEDController.WLEDCalibrateCurrents[30];
                                break;
                        }
                    }
                    break;
                case "Get40":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 40))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt40 = _LEDController.GLEDCalibrateCurrents[40];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt40 = _LEDController.RLEDCalibrateCurrents[40];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt40 = _LEDController.WLEDCalibrateCurrents[40];
                                break;
                        }
                    }
                    break;
                case "Get50":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 50))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt50 = _LEDController.GLEDCalibrateCurrents[50];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt50 = _LEDController.RLEDCalibrateCurrents[50];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt50 = _LEDController.WLEDCalibrateCurrents[50];
                                break;
                        }
                    }
                    break;
                case "Get60":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 60))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt60 = _LEDController.GLEDCalibrateCurrents[60];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt60 = _LEDController.RLEDCalibrateCurrents[60];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt60 = _LEDController.WLEDCalibrateCurrents[60];
                                break;
                        }
                    }
                    break;
                case "Get70":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 70))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt70 = _LEDController.GLEDCalibrateCurrents[70];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt70 = _LEDController.RLEDCalibrateCurrents[70];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt70 = _LEDController.WLEDCalibrateCurrents[70];
                                break;
                        }
                    }
                    break;
                case "Get80":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 80))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt80 = _LEDController.GLEDCalibrateCurrents[80];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt80 = _LEDController.RLEDCalibrateCurrents[80];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt80 = _LEDController.WLEDCalibrateCurrents[80];
                                break;
                        }
                    }
                    break;
                case "Get90":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 90))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt90 = _LEDController.GLEDCalibrateCurrents[90];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt90 = _LEDController.RLEDCalibrateCurrents[90];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt90 = _LEDController.WLEDCalibrateCurrents[90];
                                break;
                        }
                    }
                    break;
                case "Get100":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, 100))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                LEDCurrentAt100 = _LEDController.GLEDCalibrateCurrents[100];
                                break;
                            case LEDTypes.Red:
                                LEDCurrentAt100 = _LEDController.RLEDCalibrateCurrents[100];
                                break;
                            case LEDTypes.White:
                                LEDCurrentAt100 = _LEDController.WLEDCalibrateCurrents[100];
                                break;
                        }
                    }
                    break;
                case "SetRev2":
                    _LEDController.SetLEDCalibratingCurrent(SelectedLEDType, SelectedCalibratePower, CalibrateCurrent);
                    break;
                case "GetRev2":
                    if (_LEDController.GetLEDCalibratingCurrent(SelectedLEDType, SelectedCalibratePower))
                    {
                        switch (SelectedLEDType)
                        {
                            case LEDTypes.Green:
                                CalibrateCurrent = _LEDController.GLEDCalibrateCurrents[SelectedCalibratePower];
                                break;
                            case LEDTypes.Red:
                                CalibrateCurrent = _LEDController.RLEDCalibrateCurrents[SelectedCalibratePower];
                                break;
                            case LEDTypes.White:
                                CalibrateCurrent = _LEDController.WLEDCalibrateCurrents[SelectedCalibratePower];
                                break;
                        }
                    }
                    break;
                case "LEDVoltageOn":
                    _LEDController.SetLEDDriveVoltage(SelectedLEDType, LEDVoltageSet);
                    break;
                case "LEDVoltageOff":
                    _LEDController.SetLEDStatus(SelectedLEDType, false);
                    break;
            }
        }

        private bool CanExecuteSetCmd(object obj)
        {
            return true;
        }
        #endregion SetCmd
    }
}
