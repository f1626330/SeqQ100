using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class SetTemperStepViewModel : StepsTreeViewModel
    {
        double _SetTemperature = RecipeStepDefaultSettings.TargetTemper;
        double _TemperTolerance = RecipeStepDefaultSettings.TemperTolerance;
        int _TemperDuration = RecipeStepDefaultSettings.Duration;
        bool _WaitForTemperCtrlFinished = RecipeStepDefaultSettings.WaitForComplete;

        public SetTemperStepViewModel()
        {

        }

        public SetTemperStepViewModel(StepsTree content, StepsTreeViewModel parent):base(content,parent)
        {
            SetTemperature = ((SetTemperStep)content.Step).TargetTemper;
            TemperTolerance = ((SetTemperStep)content.Step).Tolerance;
            TemperDuration = ((SetTemperStep)content.Step).Duration;
            WaitForTemperCtrlFinished = ((SetTemperStep)content.Step).WaitForComplete;
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

        public override StepsTreeViewModel Clone()
        {
            SetTemperStepViewModel clonedVm = new SetTemperStepViewModel()
            {
                SetTemperature = this.SetTemperature,
                TemperTolerance = this.TemperTolerance,
                TemperDuration = this.TemperDuration,
                WaitForTemperCtrlFinished = this.WaitForTemperCtrlFinished,
            };
            return clonedVm;
        }
    }
}
