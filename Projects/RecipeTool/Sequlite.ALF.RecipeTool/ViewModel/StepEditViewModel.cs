using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class StepEditViewModel : ViewModelBase
    {
        public delegate void ClosingWindHandle(bool updated);
        public event ClosingWindHandle OnClosingWindow;

        private StepsTreeViewModel _CurrentStep;

        public StepsTreeViewModel CurrentStep
        {
            get { return _CurrentStep; }
            set
            {
                if (_CurrentStep != value)
                {
                    _CurrentStep = value;
                    RaisePropertyChanged(nameof(CurrentStep));
                }
            }
        }

        #region Update Command
        private RelayCommand _UpdateCmd;
        public ICommand UpdateCmd
        {
            get
            {
                if (_UpdateCmd == null)
                {
                    _UpdateCmd = new RelayCommand(ExecuteUpdateCmd, CanExecuteUpdateCmd);
                }
                return _UpdateCmd;
            }
        }

        private void ExecuteUpdateCmd(object obj)
        {
            Workspace.This.GetStepParameterFromViewModel(CurrentStep.Content.Step, CurrentStep);

            // update the UI by removing-recovering the selected step
            var temp = Workspace.This.RecipeToolRecipeVM.SelectedStep;
            if (temp.Parent == null)
            {
                int index = Workspace.This.RecipeToolRecipeVM.Steps.IndexOf(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                Workspace.This.RecipeToolRecipeVM.Steps.Remove(Workspace.This.RecipeToolRecipeVM.SelectedStep);
                Workspace.This.RecipeToolRecipeVM.Steps.Insert(index, temp);
                Workspace.This.RecipeToolRecipeVM.SelectedStep = temp;
            }
            else
            {
                int index = temp.Parent.Children.IndexOf(temp);
                temp.Parent.Children.Remove(temp);
                temp.Parent.Children.Insert(index, temp);
                Workspace.This.RecipeToolRecipeVM.SelectedStep = temp;
            }
            OnClosingWindow?.Invoke(true);
        }

        private bool CanExecuteUpdateCmd(object obj)
        {
            return true;
        }
        #endregion Update Command
    }
}
