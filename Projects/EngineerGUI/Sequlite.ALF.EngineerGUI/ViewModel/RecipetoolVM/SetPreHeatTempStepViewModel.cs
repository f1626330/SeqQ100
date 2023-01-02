using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class SetPreHeatTemperStepViewModel : StepsTreeViewModel
    {
        double _SetTemperature = 30;
        double _TemperTolerance = 5;
        bool _WaitForTemperCtrlFinished = true;
        //double _TemperCtrlP = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP;
        //double _TemperCtrlI = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI;
        //double _TemperCtrlD = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD;
        //double _TemperCtrlHeatGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain;
        //double _TemperCtrlCoolGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain;

        public SetPreHeatTemperStepViewModel(bool isProtocolRev2)
        {
            IsProtocolRev2 = isProtocolRev2;
        }

        public SetPreHeatTemperStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            SetTemperature = ((SetPreHeatTempStep)content.Step).TargetTemper;
            TemperTolerance = ((SetPreHeatTempStep)content.Step).Tolerance;
            WaitForTemperCtrlFinished = ((SetPreHeatTempStep)content.Step).WaitForComplete;
        }

        public double SetTemperature
        {
            get { return _SetTemperature; }
            set
            {
                if (_SetTemperature != value)
                {
                    _SetTemperature = value;
                    RaisePropertyChanged(nameof(SetTemperature));
                }
            }
        }
        public double TemperTolerance
        {
            get { return _TemperTolerance; }
            set
            {
                if (_TemperTolerance != value)
                {
                    _TemperTolerance = value;
                    RaisePropertyChanged(nameof(TemperTolerance));
                }
            }
        }
        public bool WaitForTemperCtrlFinished
        {
            get { return _WaitForTemperCtrlFinished; }
            set
            {
                if (_WaitForTemperCtrlFinished != value)
                {
                    _WaitForTemperCtrlFinished = value;
                    RaisePropertyChanged(nameof(WaitForTemperCtrlFinished));
                }
            }
        }

        public bool IsProtocolRev2 { get; }
        //public double TemperCtrlP
        //{
        //    get => _TemperCtrlP;
        //    set
        //    {
        //        if (_TemperCtrlP != value)
        //        {
        //            _TemperCtrlP = value;
        //            RaisePropertyChanged(nameof(TemperCtrlP));
        //        }
        //    }
        //}
        //public double TemperCtrlI
        //{
        //    get => _TemperCtrlI;
        //    set
        //    {
        //        if (_TemperCtrlI != value)
        //        {
        //            _TemperCtrlI = value;
        //            RaisePropertyChanged(nameof(TemperCtrlI));
        //        }
        //    }
        //}
        //public double TemperCtrlD
        //{
        //    get => _TemperCtrlD;
        //    set
        //    {
        //        if (_TemperCtrlD != value)
        //        {
        //            _TemperCtrlD = value;
        //            RaisePropertyChanged(nameof(TemperCtrlD));
        //        }
        //    }
        //}
        //public double TemperCtrlHeatGain
        //{
        //    get => _TemperCtrlHeatGain;
        //    set
        //    {
        //        if (_TemperCtrlHeatGain != value)
        //        {
        //            _TemperCtrlHeatGain = value;
        //            RaisePropertyChanged(nameof(TemperCtrlHeatGain));
        //        }
        //    }
        //}
        //public double TemperCtrlCoolGain
        //{
        //    get => _TemperCtrlCoolGain;
        //    set
        //    {
        //        if (_TemperCtrlCoolGain != value)
        //        {
        //            _TemperCtrlCoolGain = value;
        //            RaisePropertyChanged(nameof(TemperCtrlCoolGain));
        //        }
        //    }
        //}

        public override StepsTreeViewModel Clone()
        {
            SetPreHeatTemperStepViewModel clonedVm = new SetPreHeatTemperStepViewModel(IsProtocolRev2)
            {
                SetTemperature = this.SetTemperature,
                TemperTolerance = this.TemperTolerance,
                WaitForTemperCtrlFinished = this.WaitForTemperCtrlFinished,
            };
            return clonedVm;
        }
    }
}
