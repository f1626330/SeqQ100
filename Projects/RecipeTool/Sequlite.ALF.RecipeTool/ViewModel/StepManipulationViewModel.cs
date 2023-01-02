using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    public enum AddStepTypes
    {
        Append,
        Insert,
        AddToChild,
    }
    internal class StepManipulationViewModel : ViewModelBase
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
            if ((cmdPara == AddStepTypes.Insert || cmdPara == AddStepTypes.AddToChild) && Workspace.This.RecipeToolRecipeVM.SelectedStep == null)
            {
                MessageBox.Show("There is no step selected.");
                return;
            }
            else if (cmdPara == AddStepTypes.AddToChild && (Workspace.This.RecipeToolRecipeVM.SelectedStep.Content.Step.StepType != RecipeStepTypes.Loop))
            {
                MessageBox.Show("Steps can only be added to looping steps as children.");
                return;
            }

            RecipeStepBase newStep = null;
            RecipeStepTypes stepType = RecipeStepBase.GetStepType(Workspace.This.NewStepVM.SelectedStepType);
            switch (stepType)
            {
                case RecipeStepTypes.SetTemper:
                    newStep = new SetTemperStep();
                    break;
                case RecipeStepTypes.StopTemper:
                    newStep = new StopTemperStep();
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
                default:
                    throw new NotImplementedException("Unknown Step type");
            }
            if (VerifyStepParameters(newStep))
            {
                StepsTreeViewModel parentVM = Workspace.This.RecipeToolRecipeVM.SelectedStep == null ? null : Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent;
                StepsTree parent = parentVM == null ? null : parentVM.Content;
                StepsTree newStepTree = new StepsTree(null, newStep);
                StepsTreeViewModel newStepTreeVM = Workspace.This.NewStepVM.SelectedNewStep.Clone();
                newStepTreeVM.Content = newStepTree;
                if (cmdPara == AddStepTypes.Append)    // append to the children of selected step's parent
                {
                    if (parentVM == null)
                    {
                        Workspace.This.NewRecipe.Steps.Add(newStepTree);
                        Workspace.This.RecipeToolRecipeVM.Steps.Add(newStepTreeVM);
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
                        Workspace.This.NewRecipe.Steps.Insert(Workspace.This.NewRecipe.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content), newStepTree);
                        Workspace.This.RecipeToolRecipeVM.Steps.Insert(Workspace.This.RecipeToolRecipeVM.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep), newStepTreeVM);
                    }
                    else
                    {
                        parentVM.Content.Children.Insert(parentVM.Content.Children.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content), newStepTree);
                        parentVM.Children.Insert(parentVM.Children.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep), newStepTreeVM);
                        newStepTree.Parent = parent;
                        newStepTreeVM.Parent = parentVM;
                    }
                }
                else if (cmdPara == AddStepTypes.AddToChild)
                {
                    Workspace.This.RecipeToolRecipeVM.SelectedStep.Content.Children.Add(newStepTree);
                    Workspace.This.RecipeToolRecipeVM.SelectedStep.Children.Add(newStepTreeVM);
                    newStepTree.Parent = Workspace.This.RecipeToolRecipeVM.SelectedStep.Content;
                    newStepTreeVM.Parent = Workspace.This.RecipeToolRecipeVM.SelectedStep;
                }
                Workspace.This.RecipeToolRecipeVM.SelectedStep = newStepTreeVM;

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
                Workspace.This.GetStepParameterFromViewModel(newStep, Workspace.This.NewStepVM.SelectedNewStep);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion Add Step Command

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
            if (Workspace.This.RecipeToolRecipeVM.SelectedStep == null)
            {
                MessageBox.Show("There is no step selected.");
                return;
            }
            StepsTreeViewModel parentVM = Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent;
            int selectedIndex = 0;
            int existingNums = Workspace.This.RecipeToolRecipeVM.Steps.Count;
            if (parentVM == null)
            {
                selectedIndex = Workspace.This.RecipeToolRecipeVM.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                existingNums = Workspace.This.RecipeToolRecipeVM.Steps.Count;
            }
            else
            {
                selectedIndex = parentVM.Children.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
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
                Workspace.This.NewRecipe.Steps.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content);
                Workspace.This.RecipeToolRecipeVM.Steps.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep);
            }
            else
            {
                parentVM.Content.Children.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content);
                parentVM.Children.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep);
            }
            existingNums -= 1;

            if (existingNums > 0)
            {
                if (parentVM == null)
                {
                    Workspace.This.RecipeToolRecipeVM.SelectedStep = Workspace.This.RecipeToolRecipeVM.Steps[selectedIndex];
                }
                else
                {
                    Workspace.This.RecipeToolRecipeVM.SelectedStep = parentVM.Children[selectedIndex];
                }
            }
            else
            {
                Workspace.This.RecipeToolRecipeVM.SelectedStep = parentVM;
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
                if (Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent == null)
                {
                    stepIndex = Workspace.This.RecipeToolRecipeVM.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex > 0)
                    {
                        StepsTree currentStep = Workspace.This.RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = Workspace.This.RecipeToolRecipeVM.SelectedStep;

                        Workspace.This.RecipeToolRecipeVM.Steps.Move(stepIndex, stepIndex - 1);
                        Workspace.This.NewRecipe.Steps.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content);
                        Workspace.This.NewRecipe.Steps.Insert(stepIndex - 1, currentStep);
                    }
                }
                else
                {
                    stepIndex = Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent.Children.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex > 0)
                    {
                        StepsTree currentStep = Workspace.This.RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = Workspace.This.RecipeToolRecipeVM.SelectedStep;

                        currentStepVM.Parent.Children.Move(stepIndex, stepIndex - 1);
                        currentStep.Parent.Children.Remove(currentStep);
                        currentStep.Parent.Children.Insert(stepIndex - 1, currentStep);
                    }
                }
            }
            else if (cmdPara == "down")
            {
                if (Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent == null)
                {
                    stepIndex = Workspace.This.RecipeToolRecipeVM.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex < Workspace.This.RecipeToolRecipeVM.Steps.Count - 1)
                    {
                        StepsTree currentStep = Workspace.This.RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = Workspace.This.RecipeToolRecipeVM.SelectedStep;

                        Workspace.This.RecipeToolRecipeVM.Steps.Move(stepIndex, stepIndex + 1);
                        Workspace.This.NewRecipe.Steps.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep.Content);
                        Workspace.This.NewRecipe.Steps.Insert(stepIndex + 1, currentStep);
                    }
                }
                else
                {
                    stepIndex = Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent.Children.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                    if (stepIndex < Workspace.This.RecipeToolRecipeVM.SelectedStep.Parent.Children.Count - 1)
                    {
                        StepsTree currentStep = Workspace.This.RecipeToolRecipeVM.SelectedStep.Content;
                        StepsTreeViewModel currentStepVM = Workspace.This.RecipeToolRecipeVM.SelectedStep;

                        currentStepVM.Parent.Children.Move(stepIndex, stepIndex + 1);
                        currentStep.Parent.Children.Remove(currentStep);
                        currentStep.Parent.Children.Insert(stepIndex + 1, currentStep);
                    }
                }
            }
        }

        private bool CanExecuteMoveStepCmd(object obj)
        {
            return Workspace.This.RecipeToolRecipeVM.SelectedStep != null;
        }
        #endregion Move Step Command

    }
}
