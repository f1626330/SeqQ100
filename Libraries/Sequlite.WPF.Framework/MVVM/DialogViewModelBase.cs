using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.WPF.Framework
{
    public abstract class DialogViewModelBase : ViewModelBase, IUserDialogBoxViewModel
    {
        public bool IsModal { get; protected set; }

        public Action<DialogViewModelBase> OnCloseRequest { get; set; }
        public event EventHandler DialogBoxClosing;
        public void Close()
        {
            if (this.DialogBoxClosing != null)
            {
                this.DialogBoxClosing(this, new EventArgs());
            }
        }
        public void Show(IList<IDialogBoxViewModel> collection)
        {
            collection.Add(this);
        }
        public virtual void RequestClose()
        {
            if (this.OnCloseRequest != null)
                this.OnCloseRequest(this);
            else
                Close();
        }

        protected abstract void RunOKCommand(object o);

        ICommand _OKCommand;
        public ICommand OKCommand
        {
            get
            {
                if (_OKCommand == null)
                    _OKCommand = new RelayCommand(
                        (o) => this.RunOKCommand(o),
                        (o) => this.CanOKCommand);

                return _OKCommand;
            }
        }

        bool _CanOKCommand = true;
        public bool CanOKCommand
        {
            get => _CanOKCommand; set => SetProperty(ref _CanOKCommand, value);
        }
    }
}