using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public interface IViewModelStatus
    {
       
        bool IsBusy { get; set; }
        string StatusInfo { get; set; }
    }
}
