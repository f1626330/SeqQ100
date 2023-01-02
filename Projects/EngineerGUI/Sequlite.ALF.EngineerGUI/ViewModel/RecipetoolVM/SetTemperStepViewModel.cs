using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class SetTemperStepViewModel : StepsTreeViewModel
    {
        double _SetTemperature = RecipeStepDefaultSettings.TargetTemper;
        double _TemperTolerance = RecipeStepDefaultSettings.TemperTolerance;
        int _TemperDuration = RecipeStepDefaultSettings.Duration;
        bool _WaitForTemperCtrlFinished = RecipeStepDefaultSettings.WaitForComplete;
        double _TemperCtrlP = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP;
        double _TemperCtrlI = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI;
        double _TemperCtrlD = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD;
        double _TemperCtrlHeatGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain;
        double _TemperCtrlCoolGain = SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain;

        public SetTemperStepViewModel(bool isProtocolRev2)
        {
            IsProtocolRev2 = isProtocolRev2;
        }

        public SetTemperStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            SetTemperature = ((SetTemperStep)content.Step).TargetTemper;
            TemperTolerance = ((SetTemperStep)content.Step).Tolerance;
            TemperDuration = ((SetTemperStep)content.Step).Duration;
            WaitForTemperCtrlFinished = ((SetTemperStep)content.Step).WaitForComplete;
            TemperCtrlP = ((SetTemperStep)content.Step).CtrlP;
            TemperCtrlI = ((SetTemperStep)content.Step).CtrlI;
            TemperCtrlD = ((SetTemperStep)content.Step).CtrlD;
            TemperCtrlHeatGain = ((SetTemperStep)content.Step).CtrlHeatGain;
            TemperCtrlCoolGain = ((SetTemperStep)content.Step).CtrlCoolGain;
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
        public int TemperDuration
        {
            get { return _TemperDuration; }
            set
            {
                if (_TemperDuration != value)
                {
                    _TemperDuration = value;
                    RaisePropertyChanged(nameof(TemperDuration));
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
        public double TemperCtrlP
        {
            get => _TemperCtrlP;
            set
            {
                if (_TemperCtrlP != value)
                {
                    _TemperCtrlP = value;
                    RaisePropertyChanged(nameof(TemperCtrlP));
                }
            }
        }
        public double TemperCtrlI
        {
            get => _TemperCtrlI;
            set
            {
                if (_TemperCtrlI != value)
                {
                    _TemperCtrlI = value;
                    RaisePropertyChanged(nameof(TemperCtrlI));
                }
            }
        }
        public double TemperCtrlD
        {
            get => _TemperCtrlD;
            set
            {
                if (_TemperCtrlD != value)
                {
                    _TemperCtrlD = value;
                    RaisePropertyChanged(nameof(TemperCtrlD));
                }
            }
        }
        public double TemperCtrlHeatGain
        {
            get => _TemperCtrlHeatGain;
            set
            {
                if (_TemperCtrlHeatGain != value)
                {
                    _TemperCtrlHeatGain = value;
                    RaisePropertyChanged(nameof(TemperCtrlHeatGain));
                }
            }
        }
        public double TemperCtrlCoolGain
        {
            get => _TemperCtrlCoolGain;
            set
            {
                if (_TemperCtrlCoolGain != value)
                {
                    _TemperCtrlCoolGain = value;
                    RaisePropertyChanged(nameof(TemperCtrlCoolGain));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            SetTemperStepViewModel clonedVm = new SetTemperStepViewModel(IsProtocolRev2)
            {
                SetTemperature = this.SetTemperature,
                TemperTolerance = this.TemperTolerance,
                TemperDuration = this.TemperDuration,
                WaitForTemperCtrlFinished = this.WaitForTemperCtrlFinished,
                TemperCtrlP = this.TemperCtrlP,
                TemperCtrlI = this.TemperCtrlI,
                TemperCtrlD = this.TemperCtrlD,
                TemperCtrlHeatGain = this.TemperCtrlHeatGain,
                TemperCtrlCoolGain = this.TemperCtrlCoolGain,
            };
            return clonedVm;
        }
    }
}
