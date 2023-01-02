using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class SettingsViewModel : ViewBaseViewModel
    {
         //ISeqFileLog Logger {  get; }


        UserAccountSettingViewModel _UserAccountSettingVM;
        public UserAccountSettingViewModel UserAccountSettingVM { get => _UserAccountSettingVM; set => SetProperty(ref _UserAccountSettingVM, value); }
        ICommand _ExitCommand;

        public SettingsViewModel(IDialogService dialogs, UserPageModel userModel, ISeqFileLog logger)
        {
            DialogService = dialogs;
            Logger = logger;
            UserAccountSettingVM = new UserAccountSettingViewModel(userModel) { Logger = this.Logger ,DialogService=this.DialogService};
        }

        

        public ICommand ExitCommand
        {
            get
            {
                if (_ExitCommand == null)
                    _ExitCommand = new RelayCommand(
                        (o) => this.RunExitCommand(),
                        (o) => this.CanExitCommand);

                return _ExitCommand;
            }
        }
        bool CanExitCommand
        {
            get { return true; }
        }

        void RunExitCommand()
        {
            UserAccountSettingVM?.OnClose();
            this.OnRequestClose();
        }
    }
}
