using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class LoopStepViewModel : StepsTreeViewModel
    {
        int _LoopCycles = RecipeStepDefaultSettings.LoopCycles;
        string _LoopName = RecipeStepDefaultSettings.LoopName;

        #region Constructor
        public LoopStepViewModel()
        {

        }
        public LoopStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            LoopStep step = content.Step as LoopStep;
            if (step != null)
            {
                LoopCycles = step.LoopCycles;
                LoopName = step.LoopName;
            }
        }
        #endregion Constructor

        public int LoopCycles
        {
            get { return _LoopCycles; }
            set
            {
                if (_LoopCycles != value)
                {
                    _LoopCycles = value;
                    RaisePropertyChanged(nameof(LoopCycles));
                }
            }
        }
        public string LoopName
        {
            get { return _LoopName; }
            set
            {
                if (_LoopName != value)
                {
                    _LoopName = value;
                    RaisePropertyChanged(nameof(LoopName));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            LoopStepViewModel clonedVm = new LoopStepViewModel()
            {
                LoopCycles = this.LoopCycles,
                LoopName = this.LoopName,
            };
            return clonedVm;
        }
    }
}
