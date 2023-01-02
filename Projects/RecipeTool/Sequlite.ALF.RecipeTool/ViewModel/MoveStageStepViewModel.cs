using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class MoveStageStepViewModel : StepsTreeViewModel
    {
        int _MoveStageRegion;

        public MoveStageStepViewModel()
        {
            RegionOptions = new List<int>();
            for(int i = 1; i <= 40; i++)
            {
                RegionOptions.Add(i);
            }
            MoveStageRegion = RegionOptions[0];
        }

        public MoveStageStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            RegionOptions = new List<int>();
            for (int i = 1; i <= 40; i++)
            {
                RegionOptions.Add(i);
            }
            MoveStageStep step = content.Step as MoveStageStep;
            if (step != null)
            {
                MoveStageRegion = RegionOptions.Find(p => p == step.Region);
            }
        }

        public List<int> RegionOptions { get; }
        public int MoveStageRegion
        {
            get { return _MoveStageRegion; }
            set
            {
                if (_MoveStageRegion != value)
                {
                    _MoveStageRegion = value;
                    RaisePropertyChanged(nameof(MoveStageRegion));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            MoveStageStepViewModel clonedVm = new MoveStageStepViewModel()
            {
                MoveStageRegion = this.MoveStageRegion
            };
            return clonedVm;
        }
    }
}
