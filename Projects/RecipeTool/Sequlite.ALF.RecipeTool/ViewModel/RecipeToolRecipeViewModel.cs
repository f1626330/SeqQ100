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
using Sequlite.ALF.RecipeTool.View;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    public class RecipeToolRecipeViewModel : ViewModelBase
    {
        #region Private Fields
        private string _RecipeName;
        private string _RecipePath;
        private DateTime _CreatedTime;
        private DateTime _UpdatedTime;
        private string _ToolVersion;
        private StepsTreeViewModel _SelectedStep;
        #endregion Private Fields

        #region Constructor
        public RecipeToolRecipeViewModel()
        {
            _RecipeName = "Default recipe";
        }
        #endregion Constructor

        #region Public Properties
        public string RecipeName
        {
            get { return _RecipeName; }
            set
            {
                if (_RecipeName != value)
                {
                    _RecipeName = value;
                    RaisePropertyChanged(nameof(RecipeName));
                }
            }
        }
        public string RecipePath
        {
            get { return _RecipePath; }
            set
            {
                if (_RecipePath != value)
                {
                    _RecipePath = value;
                    RaisePropertyChanged(nameof(RecipePath));
                }
            }
        }
        public DateTime CreatedTime
        {
            get { return _CreatedTime; }
            set
            {
                if (_CreatedTime != value)
                {
                    _CreatedTime = value;
                    RaisePropertyChanged(nameof(CreatedTime));
                }
            }
        }
        public DateTime UpdatedTime
        {
            get { return _UpdatedTime; }
            set
            {
                if (_UpdatedTime != value)
                {
                    _UpdatedTime = value;
                    RaisePropertyChanged(nameof(UpdatedTime));
                }
            }
        }
        public string ToolVersion
        {
            get { return _ToolVersion; }
            set
            {
                if (_ToolVersion != value)
                {
                    _ToolVersion = value;
                    RaisePropertyChanged(nameof(ToolVersion));
                }
            }
        }
        public ObservableCollection<StepsTreeViewModel> Steps { get; } = new ObservableCollection<StepsTreeViewModel>();
        public StepsTreeViewModel SelectedStep
        {
            get { return _SelectedStep; }
            set
            {
                if (_SelectedStep != value)
                {
                    if (value == null)
                    {
                        _SelectedStep.IsSelected = false;
                    }
                    _SelectedStep = value;
                    if (_SelectedStep != null)
                    {
                        _SelectedStep.IsSelected = true;
                        RaisePropertyChanged(nameof(SelectedStep));
                    }
                }
            }
        }
        #endregion Public Properties

        #region Load Recipe Command
        private RelayCommand _LoadRecipeCmd;
        public ICommand LoadRecipeCmd
        {
            get
            {
                if (_LoadRecipeCmd == null)
                {
                    _LoadRecipeCmd = new RelayCommand(ExecuteLoadRecipeCmd, CanExecuteLoadRecipeCmd);
                }
                return _LoadRecipeCmd;
            }
        }

        private void ExecuteLoadRecipeCmd(object obj)
        {
            OpenFileDialog opDialog = new OpenFileDialog();
            opDialog.Filter = "xml|*.xml";
            if (opDialog.ShowDialog() == true)
            {
                try
                {
                    Workspace.This.NewRecipe = Recipe.LoadFromXmlFile(opDialog.FileName);
                    Workspace.This.RecipeToolRecipeVM.RecipeName = Workspace.This.NewRecipe.RecipeName;
                    Workspace.This.RecipeToolRecipeVM.CreatedTime = Workspace.This.NewRecipe.CreatedTime;
                    Workspace.This.RecipeToolRecipeVM.UpdatedTime = Workspace.This.NewRecipe.UpdatedTime;
                    Workspace.This.RecipeToolRecipeVM.ToolVersion = Workspace.This.NewRecipe.ToolVersion;
                    Workspace.This.RecipeToolRecipeVM.RecipePath = opDialog.FileName;

                    Workspace.This.RecipeToolRecipeVM.Steps.Clear();
                    foreach (var item in Workspace.This.NewRecipe.Steps)
                    {
                        Workspace.This.RecipeToolRecipeVM.Steps.Add(StepsTreeViewModel.CreateViewModel(item, null));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading the file.");
                }
            }
        }

        private bool CanExecuteLoadRecipeCmd(object obj)
        {
            return true;
        }
        #endregion Load Recipe Command

        #region Save Recipe Command
        private RelayCommand _SaveRecipeCmd;
        public ICommand SaveRecipeCmd
        {
            get
            {
                if (_SaveRecipeCmd == null)
                {
                    _SaveRecipeCmd = new RelayCommand(ExecuteSaveRecipeCmd, CanExecuteSaveRecipeCmd);
                }
                return _SaveRecipeCmd;
            }
        }

        private void ExecuteSaveRecipeCmd(object obj)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Filter = "xml|*.xml";
                if (saveDialog.ShowDialog() == true)
                {
                    Workspace.This.NewRecipe.RecipeName = RecipeName;
                    Workspace.This.NewRecipe.ToolVersion = string.Format(" {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                    if (Workspace.This.NewRecipe.CreatedTime.Year == 1)
                    {
                        Workspace.This.NewRecipe.CreatedTime = DateTime.Now;
                        Workspace.This.NewRecipe.UpdatedTime = Workspace.This.NewRecipe.CreatedTime;
                        CreatedTime = Workspace.This.NewRecipe.CreatedTime;
                        UpdatedTime = Workspace.This.NewRecipe.CreatedTime;
                    }
                    else
                    {
                        Workspace.This.NewRecipe.UpdatedTime = DateTime.Now;
                        UpdatedTime = Workspace.This.NewRecipe.UpdatedTime;
                    }
                    Recipe.SaveToXmlFile(Workspace.This.NewRecipe, saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error saving the recipe...");
            }
        }

        private bool CanExecuteSaveRecipeCmd(object obj)
        {
            return Workspace.This.NewRecipe.Steps.Count > 0;
        }
        #endregion Create Recipe Command

        #region Modify Step Command
        private RelayCommand _ModifyStepCmd;
        public ICommand ModifyStepCmd
        {
            get
            {
                if (_ModifyStepCmd == null)
                {
                    _ModifyStepCmd = new RelayCommand(ExecuteModifyStepCmd, CanExecuteModifyStepCmd);
                }
                return _ModifyStepCmd;
            }
        }

        private void ExecuteModifyStepCmd(object obj)
        {
            StepEditWindow editWind = new StepEditWindow();
            StepEditViewModel vm = new StepEditViewModel();
            vm.CurrentStep = SelectedStep;
            editWind.DataContext = vm;
            var result = editWind.ShowDialog();
            if(result == true)
            {
                MessageBox.Show("Selected Step is updated.");
            }
            else
            {
                Workspace.This.SetStepParameterToViewModel(SelectedStep.Content.Step, SelectedStep);
            }
        }

        private bool CanExecuteModifyStepCmd(object obj)
        {
            return SelectedStep != null;
        }
        #endregion Modify Step Command

    }
}
