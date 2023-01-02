using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Sequlite.ALF.EngineerGUI.ViewModel;
using Sequlite.WPF.Framework;

namespace Sequlite.UI.ViewModel
{
    public class EngineeringViewModel : ViewBaseViewModel
    {
        public Workspace EUIViewModel { get;  set; }

        public EngineeringViewModel(IDialogService dialogs) 
        {
            DialogService = dialogs;
        }

       

        ICommand _ExitCommand;
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
            if (ConfirmExit())
            {
                this.OnRequestClose();
            }
        }


        bool ConfirmExit()
        {
            bool b = false;


            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = "Do you want to exit Engineering UI?",
                Caption = "Exit Engineering",
                Image = MessageBoxImage.Question,
                Buttons = MessageBoxButton.YesNo,
                IsModal = true,
            };

            if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
            {
                b = true;
            }
            return b;
        }
    }
}
