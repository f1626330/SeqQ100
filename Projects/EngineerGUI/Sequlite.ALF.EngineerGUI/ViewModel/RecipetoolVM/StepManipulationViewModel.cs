using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;


namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    public enum AddStepTypes
    {
        Append,
        Insert,
        AddToChild,
    }
    public class StepManipulationViewModel : ViewModelBase
    {
        #region Add Step Command
        private RelayCommand _AddStepCmd;
        public ICommand AddStepCmd
        {
            get
            {
                if (_AddStepCmd == null)
                {
                    _AddStepCmd = new RelayCommand(ExecuteAddStepCmd, CanExecuteAddStepCmd);
                }
                return _AddStepCmd;
            }
        }

        private void ExecuteAddStepCmd(object obj)
        {
            AddStepTypes cmdPara = (AddStepTypes)obj;
            if ((cmdPara == AddStepTypes.Insert || cmdPara == AddStepTypes.AddToChild) && RecipeToolRecipeVM.SelectedStep == null)
            {
                MessageBox.Show("There is no step selected.");
                return;
            }
            else if (cmdPara == AddStepTypes.AddToChild && (RecipeToolRecipeVM.SelectedStep.Content.Step.StepType != RecipeStepTypes.Loop))
            {
                MessageBox.Show("Steps can only be added to looping steps as children.");
                return;
            }

            RecipeStepBase newStep = null;
            RecipeStepTypes stepType = RecipeStepBase.GetStepType(NewStepVM.SelectedStepType);
            switch (stepType)
            {
                case RecipeStepTypes.SetTemper:
                    newStep = new SetTemperStep();
                    break;
                case RecipeStepTypes.StopTemper:
                    newStep = new StopTemperStep();
                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    newStep = new SetPreHeatTempStep();
                    break;
                case RecipeStepTypes.StopPreHeating:
                    newStep = new StopPreHeatingStep();
                    break;
                case RecipeStepTypes.Imaging:
                    newStep = new ImagingStep();
                    break;
                case RecipeStepTypes.MoveStage:
                    newStep = new MoveStageStep();
                    break;
                case RecipeStepTypes.Pumping:
                    newStep = new PumpingStep();
                    break;
                case RecipeStepTypes.Loop:
                    newStep = new LoopStep();
                    break;
                case RecipeStepTypes.RunRecipe:
                    newStep = new RunRecipeStep();
                    break;
                case RecipeStepTypes.Waiting:
                    newStep = new WaitingStep();
                    break;
                case RecipeStepTypes.Comment:
                    newStep = new CommentStep();
                    break;
                case RecipeStepTypes.NewPumping:
                    newStep = new NewPumpingStep();
                    break;
                case RecipeStepTypes.MoveStageRev2:
                    newStep = new MoveStageStepRev2();
                    break;
                default:
                    throw new NotImplementedException("Unknown Step type");
            }
            if (VerifyStepParameters(newStep))
            {
                StepsTreeViewModel parentVM = RecipeToolRecipeVM.SelectedStep == null ? null : RecipeToolRecipeVM.SelectedStep.Parent;
                StepsTree parent = parentVM == null ? null : parentVM.Content;
                StepsTree newStepTree = new StepsTree(null, newStep);
                StepsTreeViewModel newStepTreeVM = NewStepVM.SelectedNewStep.Clone();
                newStepTreeVM.Content = newStepTree;
                if (cmdPara == AddStepTypes.Append)    // append to the children of selected step's parent
                {
                    if (parentVM == null)
                    {
                        NewRecipe.Steps.Add(newStepTree);
                        RecipeToolRecipeVM.Steps.Add(newStepTreeVM);
                    }
                    else
                    {
                        parentVM.Content.Children.Add(newStepTree);
                        parentVM.Children.Add(newStepTreeVM);
                        newStepTree.Parent = parent;
                        newStepTreeVM.Parent = parentVM;
                    }
                }
                else if (cmdPara == AddStepTypes.Insert)
                {
                    if (parentVM == null)
                    {
                        NewRecipe.Steps.Insert(NewRecipe.Steps.IndexOf(RecipeToolRecipeVM.SelectedStep.Content), newStepTree);
                        RecipeToolRecipeVM.Steps.Insert(RecipeToolRecipeVM.Steps.IndexOf(RecipeToolRecipeVM.SelectedStep), newStepTreeVM);
                    }
                    else
                    {
                        parentVM.Content.Children.Insert(parentVM.Content.Children.IndexOf(RecipeToolRecipeVM.SelectedStep.Content), newStepTree);
                        parentVM.Children.Insert(parentVM.Children.IndexOf(RecipeToolRecipeVM.SelectedStep), newStepTreeVM);
                        newStepTree.Parent = parent;
                        newStepTreeVM.Parent = parentVM;
                    }
                }
                else if (cmdPara == AddStepTypes.AddToChild)
                {
                    RecipeToolRecipeVM.SelectedStep.Content.Children.Add(newStepTree);
                    RecipeToolRecipeVM.SelectedStep.Children.Add(newStepTreeVM);
                    newStepTree.Parent = RecipeToolRecipeVM.SelectedStep.Content;
                    newStepTreeVM.Parent = RecipeToolRecipeVM.SelectedStep;
                }
                RecipeToolRecipeVM.SelectedStep = newStepTreeVM;

            }
        }

        private bool CanExecuteAddStepCmd(object obj)
        {
            return true;
        }

        private bool VerifyStepParameters(RecipeStepBase newStep)
        {
            try
            {
                StepParameters.GetStepParameterFromViewModel(newStep, NewStepVM.SelectedNewStep);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion Add Step Command

        RecipeToolRecipeViewModel RecipeToolRecipeVM { get; }
        RecipeStepViewModel NewStepVM { get; }
        Recipe NewRecipe { get; set; }
        IStepParameters StepParameters { get; }
        public StepManipulationViewModel(IStepParameters stepParameters,RecipeToolRecipeViewModel recipeToolRecipeVM,
            RecipeStepViewModel newStepVM)
        {
            StepParameters = stepParameters;
            RecipeToolRecipeVM = recipeToolRecipeVM;
            NewRecipe = recipeToolRecipeVM.NewRecipe;
            recipeToolRecipeVM.OnRecipeChanged += RecipeToolRecipeVM_OnRecipeChanged;
            NewStepVM = newStepVM;
        }

        private void RecipeToolRecipeVM_OnRecipeChanged(object sender, Recipe newRecipe)
        {
            NewRecipe = newRecipe;
        }
        #region Remove Step Command
        private RelayCommand _RemoveStepCmd;
        public ICommand RemoveStepCmd
        {
            get
            {
                if (_RemoveStepCmd == null)
                {
                    _RemoveStepCmd = new RelayCommand(ExecuteRemoveStepCmd, CanExecuteRemoveStepCmd);
                }
                return _RemoveStepCmd;
            }
        }

        private void ExecuteRemoveStepCmd(object obj)
        {
            if (RecipeToolRecipeVM.SelectedStep == null)
            {
                MessageBox.Show("There is no step selected.");
                return;
            }
            StepsTreeViewModel parentVM = RecipeToolRecipeVM.SelectedStep.Parent;
            int selectedIndex = 0;
            int existingNums = RecipeToolRecipeVM.Steps.Count;
            if (parentVM == null)
            {
                selectedIndex = RecipeToolRecipeVM.Steps.IndexOf(RecipeToolRecipeVM.SelectedStep);
                existingNums = RecipeToolRecipeVM.Steps.Count;
            }
            else
            {
                selectedIndex = parentVM.Children.IndexOf(RecipeToolRecipeVM.SelectedStep);
                existingNums = parentVM.Children.Count;
            }

            if (existingNums > 1)   // it means there is steps after removing current step, so we select the previous step or first step after removing
            {
                if (selectedIndex == 0)
                {
                    selectedIndex = 0;
                }
                else { selectedIndex -= 1; }
            }
            if (parentVM == null)
            {
                NewRecipe.Steps.Remove(RecipeToolRecipeVM.SelectedStep.Content);
                RecipeToolRecipeVM.Steps.Remove(RecipeToolRecipeVM.SelectedStep);
            }
            else
            {
                parentVM.Content.Children.Remove(RecipeToolRecipeVM.SelectedStep.Content);
                parentVM.Children.Remove(RecipeToolRecipeVM.SelectedStep);
            }
            existingNums -= 1;

            if (existingNums > 0)
            {
                if (parentVM == null)
                {
                    RecipeToolRecipeVM.SelectedStep = RecipeToolRecipeVM.Steps[selectedIndex];
                }
                else
                {
                    RecipeToolRecipeVM.SelectedStep = parentVM.Children[selectedIndex];
                }
            }
            else
            {
                RecipeToolRecipeVM.SelectedStep = parentVM;
            }
        }

        private bool CanExecuteRemoveStepCmd(object obj)
        {
            return true;
        }
        #endregion Remove Step Command

        #region Move Step Command
        private RelayCommand _MoveStepCmd;
        public ICommand MoveStepCmd
        {
            get
            {
                if (_MoveStepCmd == null)
                {
                    _MoveStepCmd = new RelayCommand(ExecuteMoveStepCmd, CanExecuteMoveStepCmd);
                }
                return _MoveStepCmd;
            }
        }

        private void ExecuteMoveStepCmd(object obj)
        {
            string cmdPara = obj.ToString().ToLower();
            int stepIndex;
            if (cmdPara == "up")
            {
                if (RecipeToolRecipeVM.SelectedStep.Parent == null)
                {
                    stepIndex = RecipeToolRecipeVM.Steps.IndexOf(RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex > 0)
                    {
                        StepsTree currentStep = RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = RecipeToolRecipeVM.SelectedStep;

                        RecipeToolRecipeVM.Steps.Move(stepIndex, stepIndex - 1);
                        NewRecipe.Steps.Remove(RecipeToolRecipeVM.SelectedStep.Content);
                        NewRecipe.Steps.Insert(stepIndex - 1, currentStep);
                    }
                }
                else
                {
                    stepIndex = RecipeToolRecipeVM.SelectedStep.Parent.Children.IndexOf(RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex > 0)
                    {
                        StepsTree currentStep = RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = RecipeToolRecipeVM.SelectedStep;

                        currentStepVM.Parent.Children.Move(stepIndex, stepIndex - 1);
                        currentStep.Parent.Children.Remove(currentStep);
                        currentStep.Parent.Children.Insert(stepIndex - 1, currentStep);
                    }
                }
            }
            else if (cmdPara == "down")
            {
                if (RecipeToolRecipeVM.SelectedStep.Parent == null)
                {
                    stepIndex = RecipeToolRecipeVM.Steps.IndexOf(RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex < RecipeToolRecipeVM.Steps.Count - 1)
                    {
                        StepsTree currentStep = RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = RecipeToolRecipeVM.SelectedStep;

                        RecipeToolRecipeVM.Steps.Move(stepIndex, stepIndex + 1);
                        NewRecipe.Steps.Remove(RecipeToolRecipeVM.SelectedStep.Content);
                        NewRecipe.Steps.Insert(stepIndex + 1, currentStep);
                    }
                }
                else
                {
                    stepIndex = RecipeToolRecipeVM.SelectedStep.Parent.Children.IndexOf(RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex < RecipeToolRecipeVM.SelectedStep.Parent.Children.Count - 1)
                    {
                        StepsTree currentStep = RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = RecipeToolRecipeVM.SelectedStep;

                        currentStepVM.Parent.Children.Move(stepIndex, stepIndex + 1);
                        currentStep.Parent.Children.Remove(currentStep);
                        currentStep.Parent.Children.Insert(stepIndex + 1, currentStep);
                    }
                }
            }
        }

        private bool CanExecuteMoveStepCmd(object obj)
        {
            return RecipeToolRecipeVM.SelectedStep != null;
        }
        #endregion Move Step Command

    }

}
