using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public interface IDialogService
    {
        ObservableCollection<IDialogBoxViewModel> Dialogs { get; }
    }
}
