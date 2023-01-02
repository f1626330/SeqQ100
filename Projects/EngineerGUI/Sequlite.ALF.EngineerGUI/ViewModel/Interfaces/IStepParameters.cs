using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public interface IStepParameters
    {
        void GetStepParameterFromViewModel(RecipeStepBase step, RecipetoolVM.StepsTreeViewModel viewModel);
        void SetStepParameterToViewModel(RecipeStepBase step, RecipetoolVM.StepsTreeViewModel viewModel);
    }
}
