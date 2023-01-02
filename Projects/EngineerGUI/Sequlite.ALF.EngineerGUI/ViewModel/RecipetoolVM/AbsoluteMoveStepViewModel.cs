using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class AbsoluteMoveStepViewModel : StepsTreeViewModel
    {
        List<MotionTypes> MotionOptions { get; }

        private MotionTypes _SelectedMotion;

    }
}
