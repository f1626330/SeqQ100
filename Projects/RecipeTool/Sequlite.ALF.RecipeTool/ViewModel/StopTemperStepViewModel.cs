using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class StopTemperStepViewModel : StepsTreeViewModel
    {
        public StopTemperStepViewModel()
        {

        }
        public StopTemperStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
        }

        public override StepsTreeViewModel Clone()
        {
            StopTemperStepViewModel clonedVm = new StopTemperStepViewModel();
            return clonedVm;
        }
    }
}
