using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    abstract  public class DataViewDefaultViewModel : PageViewBaseViewModel
    {
        
        public DataViewDefaultViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            //Description = "Default Data View Page";
        }

        string _Instruction = "Data View Instruction";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }

        //public override string DisplayName => "Data View";
        //protected bool _IsPageDone = false;
        //internal override bool IsPageDone()
        //{
        //    return _IsPageDone;
        //}

    }
}
