using System;
using System.Collections.Generic;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class StopPreHeatingStepViewModel : StepsTreeViewModel
    {
        public StopPreHeatingStepViewModel()
        {

        }
        public StopPreHeatingStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
        }

        public override StepsTreeViewModel Clone()
        {
            StopPreHeatingStepViewModel clonedVm = new StopPreHeatingStepViewModel();
            return clonedVm;
        }
    }
}
