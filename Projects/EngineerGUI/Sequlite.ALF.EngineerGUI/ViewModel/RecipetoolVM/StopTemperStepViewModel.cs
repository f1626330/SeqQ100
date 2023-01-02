using System;
using System.Collections.Generic;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
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
