using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.Common;

namespace Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM
{
    internal class MoveStageStepVMRev2 : StepsTreeViewModel
    {
        private int _SelectedLane;
        private int _SelectedRow;
        private int _SelectedColumn;
        public List<int> LaneOptions { get; }
        public List<int> RowOptions { get; }
        public List<int> ColumnOptions { get; }
        public int SelectedLane
        {
            get { return _SelectedLane; }
            set
            {
                if (_SelectedLane != value)
                {
                    _SelectedLane = value;
                    RaisePropertyChanged(nameof(SelectedLane));
                }
            }
        }
        public int SelectedRow
        {
            get { return _SelectedRow; }
            set
            {
                if (_SelectedRow != value)
                {
                    _SelectedRow = value;
                    RaisePropertyChanged(nameof(SelectedRow));
                }
            }
        }
        public int SelectedColumn
        {
            get { return _SelectedColumn; }
            set
            {
                if (_SelectedColumn != value)
                {
                    _SelectedColumn = value;
                    RaisePropertyChanged(nameof(SelectedColumn));
                }
            }
        }

        public MoveStageStepVMRev2()
        {
            LaneOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCLane; i++)
            {
                LaneOptions.Add(i);
            }
            SelectedLane = LaneOptions[0];
            RowOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCRow; i++)
            {
                RowOptions.Add(i);
            }
            SelectedRow = RowOptions[0];
            ColumnOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCColumn; i++)
            {
                ColumnOptions.Add(i);
            }
            SelectedColumn = ColumnOptions[0];
        }

        public MoveStageStepVMRev2(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            LaneOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCLane; i++)
            {
                LaneOptions.Add(i);
            }
            SelectedLane = LaneOptions[0];
            RowOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCRow; i++)
            {
                RowOptions.Add(i);
            }
            SelectedRow = RowOptions[0];
            ColumnOptions = new List<int>();
            for (int i = 1; i <= SettingsManager.ConfigSettings.FCColumn; i++)
            {
                ColumnOptions.Add(i);
            }
            SelectedColumn = ColumnOptions[0];
            MoveStageStepRev2 step = content.Step as MoveStageStepRev2;
            if (step != null)
            {
                SelectedLane = LaneOptions.Find(p => p == step.Lane);
                SelectedRow = RowOptions.Find(p => p == step.Row);
                SelectedColumn = ColumnOptions.Find(p => p == step.Column);
            }
        }


        public override StepsTreeViewModel Clone()
        {
            MoveStageStepVMRev2 clonedVm = new MoveStageStepVMRev2()
            {
                SelectedLane = this.SelectedLane,
                SelectedColumn=this.SelectedColumn,
                SelectedRow = this.SelectedRow,
            };
            return clonedVm;
        }
    }
}
