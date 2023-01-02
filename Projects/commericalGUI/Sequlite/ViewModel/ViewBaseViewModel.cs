using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class ViewBaseViewModel : ViewModelBase
    {
        public event EventHandler RequestClose;
        public IDialogService DialogService { protected get; set; }
        public ISeqFileLog Logger { protected get; set; }
        protected void OnRequestClose()
        {
            //EventHandler handler = this.RequestClose;
            //if (handler != null)
            //    handler(this, EventArgs.Empty);
            RequestClose?.BeginInvoke(this, EventArgs.Empty, null, null);
        }

      
        public LogWindowViewModel LogWindowVM { protected get; set; }
       
        ICommand _ShowLogWindowCmd = null;
        public ICommand ShowLogWindowCmd
        {
            get
            {
                if (_ShowLogWindowCmd == null)
                {
                    _ShowLogWindowCmd = new RelayCommand(e => ShowLog());
                }
                return _ShowLogWindowCmd;
            }
        }

        protected void ShowLog(bool addUIDisplayFilter = false)
        {

            Dispatch(() =>
            {
                
                if (addUIDisplayFilter)
                {
                    LogWindowVM?.LogDisplayFilter?.AddSubSystemDisplayFilter("UI");
                }

                if (!LogWindowVM.Contains(this.DialogService.Dialogs))
                {
                    LogWindowVM.Show(this.DialogService.Dialogs);
                }
            });
        }

      
    }
}
