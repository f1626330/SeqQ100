using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.WPF.Framework
{
   
    public class MessageBoxView : IDialogBoxView<MessageBoxViewModel>
    {
        public void Show(MessageBoxViewModel vm, Window owner = null)
        {
            if (vm.IsModal)
            {
                vm.Result = MessageBox.Show(vm.Message, vm.Caption, vm.Buttons, vm.Image, vm.Result, MessageBoxOptions.DefaultDesktopOnly);
            }
            else
            {
                vm.Result = MessageBox.Show(vm.Message, vm.Caption, vm.Buttons, vm.Image);
            }
        }
    }
}
