using Sequlite.ALF.Fluidics;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class PumpingStepViewModel : StepsTreeViewModel
    {
        int _PumpingVol = RecipeStepDefaultSettings.PumpingVol;
        double _PumpPullingRate = RecipeStepDefaultSettings.PullRate;
        double _PumpPushingRate = RecipeStepDefaultSettings.PushRate;
        PathOptions _SelectedPullingPath = RecipeStepDefaultSettings.PullPath;
        PathOptions _SelectedPushingPath = RecipeStepDefaultSettings.PushPath;
        int _SelectedReagent = RecipeStepDefaultSettings.Reagent;
        PumpMode _SelectedPumpingType;

        public PumpingStepViewModel()
        {
            PumpingTypeOptions = new List<PumpMode>();
            PumpingTypeOptions.Add(new PumpMode("Pull", ModeOptions.Pull));
            PumpingTypeOptions.Add(new PumpMode("Push", ModeOptions.Push));
            PumpingTypeOptions.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));
            SelectedPumpingType = PumpingTypeOptions[2];

            PumpingPathOptions = new List<PathOptions>();
            PumpingPathOptions.Add(PathOptions.FC);
            PumpingPathOptions.Add(PathOptions.Bypass);
            PumpingPathOptions.Add(PathOptions.Waste);

            ReagentOptions = new List<int>();
            for (int i = 1; i < 25; i++)
            {
                ReagentOptions.Add(i);
            }
            SelectedReagent = ReagentOptions[0];
        }

        public PumpingStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            PumpingTypeOptions = new List<PumpMode>();
            PumpingTypeOptions.Add(new PumpMode("Pull", ModeOptions.Pull));
            PumpingTypeOptions.Add(new PumpMode("Push", ModeOptions.Push));
            PumpingTypeOptions.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));

            PumpingPathOptions = new List<PathOptions>();
            PumpingPathOptions.Add(PathOptions.FC);
            PumpingPathOptions.Add(PathOptions.Bypass);
            PumpingPathOptions.Add(PathOptions.Waste);

            ReagentOptions = new List<int>();
            for (int i = 1; i < 25; i++)
            {
                ReagentOptions.Add(i);
            }
            SelectedReagent = ReagentOptions[0];

            PumpingStep step = content.Step as PumpingStep;
            if(step != null)
            {
                PumpingVol = step.Volume;
                PumpPullingRate = step.PullRate;
                SelectedPullingPath = step.PullPath;
                PumpPushingRate = step.PushRate;
                SelectedPushingPath = step.PushPath;
                SelectedPumpingType = PumpingTypeOptions.Find(p => p.Mode == step.PumpingType);
                SelectedReagent = step.Reagent;
            }
        }

        public bool IsPumpPulling
        {
            get
            {
                if (SelectedPumpingType.Mode == ModeOptions.Pull || SelectedPumpingType.Mode == ModeOptions.PullPush)
                {
                    return true;
                }
                else { return false; }
            }
        }
        public bool IsPumpPushing
        {
            get
            {
                if (SelectedPumpingType.Mode == ModeOptions.Push || SelectedPumpingType.Mode == ModeOptions.PullPush)
                {
                    return true;
                }
                else { return false; }
            }
        }
        public int PumpingVol
        {
            get { return _PumpingVol; }
            set
            {
                if (_PumpingVol != value)
                {
                    _PumpingVol = value;
                    RaisePropertyChanged(nameof(PumpingVol));
                }
            }
        }
        public double PumpPullingRate
        {
            get { return _PumpPullingRate; }
            set
            {
                if (_PumpPullingRate != value)
                {
                    _PumpPullingRate = value;
                    RaisePropertyChanged(nameof(PumpPullingRate));
                }
            }
        }
        public double PumpPushingRate
        {
            get { return _PumpPushingRate; }
            set
            {
                if (_PumpPushingRate != value)
                {
                    _PumpPushingRate = value;
                    RaisePropertyChanged(nameof(PumpPushingRate));
                }
            }
        }
        public List<PathOptions> PumpingPathOptions { get; }
        public PathOptions SelectedPullingPath
        {
            get { return _SelectedPullingPath; }
            set
            {
                if (_SelectedPullingPath != value)
                {
                    _SelectedPullingPath = value;
                    RaisePropertyChanged(nameof(SelectedPullingPath));
                }
            }
        }
        public PathOptions SelectedPushingPath
        {
            get { return _SelectedPushingPath; }
            set
            {
                if (_SelectedPushingPath != value)
                {
                    _SelectedPushingPath = value;
                    RaisePropertyChanged(nameof(SelectedPushingPath));
                }
            }
        }
        public List<int> ReagentOptions { get; }
        public int SelectedReagent
        {
            get { return _SelectedReagent; }
            set
            {
                if (_SelectedReagent != value)
                {
                    _SelectedReagent = value;
                    RaisePropertyChanged(nameof(SelectedReagent));
                }
            }
        }
        public List<PumpMode> PumpingTypeOptions { get; }
        public PumpMode SelectedPumpingType
        {
            get { return _SelectedPumpingType; }
            set
            {
                if (_SelectedPumpingType != value)
                {
                    _SelectedPumpingType = value;
                    RaisePropertyChanged(nameof(SelectedPumpingType));
                    RaisePropertyChanged(nameof(IsPumpPulling));
                    RaisePropertyChanged(nameof(IsPumpPushing));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            PumpingStepViewModel clonedVm = new PumpingStepViewModel()
            {
                SelectedPumpingType = this.SelectedPumpingType,
                SelectedPullingPath = this.SelectedPullingPath,
                SelectedPushingPath = this.SelectedPushingPath,
                SelectedReagent = this.SelectedReagent,
                PumpingVol = this.PumpingVol,
                PumpPullingRate = this.PumpPullingRate,
                PumpPushingRate = this.PumpPushingRate,
            };
            clonedVm.SelectedPumpingType = clonedVm.PumpingTypeOptions.Find(p => p.Mode == this.SelectedPumpingType.Mode);
            return clonedVm;
        }
    }
}
