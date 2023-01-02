using Sequlite.ALF.App;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public class DataProcessDefaultViewModel : PageViewBaseViewModel
    {
        public DataProcessDefaultViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            Description = "Default Data Process Page";
        }

        string _Instruction = "Data Process Instruction";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }

        public override string DisplayName => "Data Process";

        internal override bool IsPageDone()
        {
            return true;
        }
    }
}
