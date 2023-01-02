using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.WPF.Framework
{
   
    public interface IDialogBoxView<T> where T : IDialogBoxViewModel
    {
        void Show(T viewModel, Window owner = null);
    }
}
