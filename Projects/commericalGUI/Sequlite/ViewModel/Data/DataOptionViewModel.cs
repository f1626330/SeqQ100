using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{

    public enum DataOptionTypeEnum
    {
        [Display(Name="Data View", Description = "Graphic views on sequence data")]
        View,

        [Display(Name = "Data Process", Description = "Reprocess sequence images to generate sequence data")]
        Process,

        [Display(Name = "Data Transfer", Description = "Transfer sequence results") ]
        Transfer,

        [Display(Name = "Data Delete", Description = "Delete sequence data and images")]
        Delete,

        [Display(Name = "Go Back", Description = "Go back to the home page")]
        Back,
    }
    public class DataOption
    {
        public DataOption()
        {
            DataOptionType = DataOptionTypeEnum.View;
        }

        public DataOption(DataOptionTypeEnum t)
        {
            DataOptionType = t;
        }
        public DataOptionTypeEnum DataOptionType { get; }
        public string Display { get; set; }
        public string Description { get; set; }
    }

    public class DataOptionViewModel : ViewBaseViewModel
    {
        ISeqApp SeqApp { get; }
        public UserPageModel UserModel { get; set; }
        public DataOptionViewModel(ISeqApp seqApp, IDialogService dialogs)
        {
            DialogService = dialogs;
            SeqApp = seqApp;
        }
        //public DataOption[] Options
        //{
        //    get
        //    {
        //        return new DataOption[] {
        //        new DataOption(DataOptionTypeEnum.View) { Display = "View",         Description = "Graphic views on sequence data" },
        //        new DataOption(DataOptionTypeEnum.Process) { Display = "Process",   Description="Reprocess sequence images to generate sequence data"},
        //        new DataOption(DataOptionTypeEnum.Transfer) { Display = "Transfer", Description="Transfer sequence data and images"} ,
        //        new DataOption(DataOptionTypeEnum.Delete) { Display = "Delete",     Description="Delete sequence data and images"}
        //        };
        //    }
        //}

        //DataOption _SelectedOption;
        //public DataOption SelectedOption { get=> _SelectedOption; set=>SetProperty(ref _SelectedOption, value) ; }

        WizardBaseViewModel _CurrentPage;
        public WizardBaseViewModel CurrentPage { get => _CurrentPage; set => SetProperty(ref _CurrentPage, value); }

        //ICommand _ExitCommand;
        //public ICommand ExitCommand
        //{
        //    get
        //    {
        //        if (_ExitCommand == null)
        //            _ExitCommand = new RelayCommand(
        //                (o) => this.RunExitCommand(),
        //                (o) => this.CanCancelPage);

        //        return _ExitCommand;
        //    }
        //}

        //bool _CanCancelPage = true;
        //public bool CanCancelPage
        //{
        //    get
        //    {
        //        return _CanCancelPage;
        //    }
        //    set
        //    {
        //        _CanCancelPage = value;
        //        RaisePropertyChanged(nameof(CanCancelPage));
        //        CommandManager.InvalidateRequerySuggested();
        //    }
        //}



        void RunExitCommand()
        {
            if (ConfirmExit())
            {
                this.OnRequestClose();
            }
        }

        bool ConfirmExit()
        {
            return true;
        }


        ICommand _ContinueCommand;
        public ICommand ContinueCommand
        {
            get
            {
                if (_ContinueCommand == null)
                    _ContinueCommand = new RelayCommand(
                        (o) => this.RunContinueCommand(o),
                        (o) => this.CanContinueCommand);

                return _ContinueCommand;
            }
        }

        bool _CanContinueCommand = true;
        public bool CanContinueCommand
        {
            get
            {
                return _CanContinueCommand;
            }
            set
            {
                _CanContinueCommand = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }


        void RunContinueCommand(object o)
        {
            DataOptionTypeEnum dataOptionType = (DataOptionTypeEnum)o;
            if (dataOptionType != DataOptionTypeEnum.Back)
            {
                DataWizardViewModel vm = new DataWizardViewModel(SeqApp, DialogService) { LogWindowVM = this.LogWindowVM, UserModel = this.UserModel };
                CurrentPage = vm;
                vm.RequestClose += CurrentPage_RequestClose;
                //vm.CreateDataWizardPages(SelectedOption.DataOptionType);
                vm.CreateDataWizardPages(dataOptionType);
            }
            else
            {
                RunExitCommand();
            }
        }

        private void CurrentPage_RequestClose(object sender, EventArgs e)
        {

            ViewModelBase vm = sender as ViewModelBase;
            if (vm != null)
            {
                vm.Dispose();
            }
            CurrentPage = null;
        }
    }
}
