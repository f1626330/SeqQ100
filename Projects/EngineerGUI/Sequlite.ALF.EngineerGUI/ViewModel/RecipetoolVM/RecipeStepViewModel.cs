using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    public class RecipeStepViewModel : ViewModelBase
    {
        #region Private fields
        private string _SelectedStepType;
        private StepsTreeViewModel _SelectedNewStep;
        #endregion Private fields
        public bool IsMachineRev2 { get; private set; }
        public bool IsProtocolRev2 { get; private set; }
        #region Constructor
        public RecipeStepViewModel(bool ismachinerev2, bool isProtocolRev2)
        {
            IsMachineRev2 = ismachinerev2;
            IsProtocolRev2 = isProtocolRev2;
            StepTypeOptions = new List<string>();
            StepViewModelOptions = new List<StepsTreeViewModel>();
            foreach (RecipeStepTypes stepType in Enum.GetValues(typeof(RecipeStepTypes)))
            {
                // Hide old pumping step type from combobox for v2 machines
                if (IsMachineRev2 && stepType == RecipeStepTypes.Pumping)
                    continue;

                StepTypeOptions.Add(RecipeStepBase.GetTypeName(stepType));
                switch (stepType)
                {
                    case RecipeStepTypes.SetTemper:
                        SetTemperStepViewModel setTemperVm = new SetTemperStepViewModel(IsProtocolRev2);
                        StepViewModelOptions.Add(setTemperVm);
                        break;
                    case RecipeStepTypes.StopTemper:
                        StopTemperStepViewModel stopTemperVm = new StopTemperStepViewModel();
                        StepViewModelOptions.Add(stopTemperVm);
                        break;
                    case RecipeStepTypes.SetPreHeatTemp:
                        SetPreHeatTemperStepViewModel PreHeatVm = new SetPreHeatTemperStepViewModel(IsProtocolRev2);
                        StepViewModelOptions.Add(PreHeatVm);
                        break;
                    case RecipeStepTypes.StopPreHeating:
                        StopPreHeatingStepViewModel stopPreHeatVm = new StopPreHeatingStepViewModel();
                        StepViewModelOptions.Add(stopPreHeatVm);
                        break;
                    case RecipeStepTypes.Imaging:
                        ImagingStepViewModel imagingVm = new ImagingStepViewModel(IsMachineRev2);
                        StepViewModelOptions.Add(imagingVm);
                        break;
                    case RecipeStepTypes.MoveStage:
                        MoveStageStepViewModel moveStageVm = new MoveStageStepViewModel();
                        StepViewModelOptions.Add(moveStageVm);
                        break;
                    case RecipeStepTypes.Pumping:
                        PumpingStepViewModel pumpingVm = new PumpingStepViewModel();
                        StepViewModelOptions.Add(pumpingVm);
                        break;
                    case RecipeStepTypes.Loop:
                        LoopStepViewModel loopVm = new LoopStepViewModel();
                        StepViewModelOptions.Add(loopVm);
                        break;
                    case RecipeStepTypes.RunRecipe:
                        RunRecipeStepViewModel runRecipeVm = new RunRecipeStepViewModel();
                        StepViewModelOptions.Add(runRecipeVm);
                        break;
                    case RecipeStepTypes.Waiting:
                        WaitingStepViewModel waitingVm = new WaitingStepViewModel();
                        StepViewModelOptions.Add(waitingVm);
                        break;
                    case RecipeStepTypes.Comment:
                        CommentStepViewModel commentVm = new CommentStepViewModel();
                        StepViewModelOptions.Add(commentVm);
                        break;
                    case RecipeStepTypes.NewPumping:
                        PumpingStepVMRev2 newpumpingVm = new PumpingStepVMRev2();
                        StepViewModelOptions.Add(newpumpingVm);
                        break;
                    case RecipeStepTypes.MoveStageRev2:
                        MoveStageStepVMRev2 newmoveVm = new MoveStageStepVMRev2();
                        StepViewModelOptions.Add(newmoveVm);
                        break;
                    case RecipeStepTypes.AbsoluteMove:
                        AbsoluteMoveStepViewModel Vm = new AbsoluteMoveStepViewModel();
                        StepViewModelOptions.Add(Vm);
                        break;
                    case RecipeStepTypes.RelativeMove:
                        //RelativeMoveStepViewModel Vm = new RelativeMoveStepViewModel();
                        //StepViewModelOptions.Add(Vm);
                        break;
                }
            }
            SelectedStepType = StepTypeOptions[0];
            SelectedNewStep = StepViewModelOptions[0];
        }
        #endregion Constructor

        #region Public properties
        public List<string> StepTypeOptions { get; }
        public string SelectedStepType
        {
            get { return _SelectedStepType; }
            set
            {
                if (_SelectedStepType != value)
                {
                    _SelectedStepType = value;
                    RaisePropertyChanged(nameof(SelectedStepType));

                    if (_SelectedStepType == null)
                    {
                        SelectedNewStep = null;
                    }
                    else
                    {
                        SelectedNewStep = StepViewModelOptions[StepTypeOptions.IndexOf(_SelectedStepType)];
                    }
                }
            }
        }
        public List<StepsTreeViewModel> StepViewModelOptions { get; }
        public StepsTreeViewModel SelectedNewStep
        {
            get
            {
                return _SelectedNewStep;
            }
            set
            {
                if (_SelectedNewStep != value)
                {
                    _SelectedNewStep = value;
                    RaisePropertyChanged(nameof(SelectedNewStep));
                }
            }
        }
        #endregion Public properties

    }
}
