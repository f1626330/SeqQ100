using Microsoft.Win32;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class RunRecipeStepViewModel : StepsTreeViewModel
    {
        string _RunRecipePath = string.Empty;
        public string RunRecipePath
        {
            get { return _RunRecipePath; }
            set
            {
                if (_RunRecipePath != value)
                {
                    _RunRecipePath = value;
                    RaisePropertyChanged(nameof(RunRecipePath));
                }
            }
        }

        #region Browse Recipe Command
        private RelayCommand _BrowseRecipeCmd;

        #region Constructor
        public RunRecipeStepViewModel()
        {

        }
        public RunRecipeStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            RunRecipeStep step = content.Step as RunRecipeStep;
            if (step != null)
            {
                RunRecipePath = step.RecipePath;
            }
        }
        #endregion Constructor

        public ICommand BrowseRecipeCmd
        {
            get
            {
                if (_BrowseRecipeCmd == null)
                {
                    _BrowseRecipeCmd = new RelayCommand(ExecuteBrowseRecipeCmd, CanExecuteBrowseRecipeCmd);
                }
                return _BrowseRecipeCmd;
            }
        }

        private void ExecuteBrowseRecipeCmd(object obj)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            if (openDialog.ShowDialog() == true)
            {
                RunRecipePath = openDialog.FileName;
            }
        }

        private bool CanExecuteBrowseRecipeCmd(object obj)
        {
            return true;
        }
        #endregion Browse Recipe Command

        public override StepsTreeViewModel Clone()
        {
            RunRecipeStepViewModel clonedVm = new RunRecipeStepViewModel()
            {
                RunRecipePath = this.RunRecipePath
            };
            return clonedVm;
        }
    }
}
