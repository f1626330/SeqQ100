using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class WaitingStepViewModel : StepsTreeViewModel
    {
        int _WaitingTime = RecipeStepDefaultSettings.WaitingTime;

        #region Constructor
        public WaitingStepViewModel()
        {

        }
        public WaitingStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            WaitingStep step = content.Step as WaitingStep;
            if (step != null)
            {
                WaitingTime = step.Time;
            }
        }
        #endregion Constructor

        public int WaitingTime
        {
            get { return _WaitingTime; }
            set
            {
                if (_WaitingTime != value)
                {
                    _WaitingTime = value;
                    RaisePropertyChanged(nameof(WaitingTime));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            WaitingStepViewModel clonedVm = new WaitingStepViewModel()
            {
                WaitingTime = this.WaitingTime
            };
            return clonedVm;
        }
    }
}
