using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class RecipeStepViewModel : ViewModelBase
    {
        #region Private fields
        private string _SelectedStepType;
        private StepsTreeViewModel _SelectedNewStep;
        #endregion Private fields

        #region Constructor
        public RecipeStepViewModel()
        {
            StepTypeOptions = new List<string>();
            StepViewModelOptions = new List<StepsTreeViewModel>();
            foreach(RecipeStepTypes stepType in Enum.GetValues(typeof(RecipeStepTypes)))
            {
                StepTypeOptions.Add(RecipeStepBase.GetTypeName(stepType));
                switch (stepType)
                {
                    case RecipeStepTypes.SetTemper:
                        SetTemperStepViewModel setTemperVm = new SetTemperStepViewModel();
                        StepViewModelOptions.Add(setTemperVm);
                        break;
                    case RecipeStepTypes.StopTemper:
                        StopTemperStepViewModel stopTemperVm = new StopTemperStepViewModel();
                        StepViewModelOptions.Add(stopTemperVm);
                        break;
                    case RecipeStepTypes.Imaging:
                        ImagingStepViewModel imagingVm = new ImagingStepViewModel();
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

                    if(_SelectedStepType == null)
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
