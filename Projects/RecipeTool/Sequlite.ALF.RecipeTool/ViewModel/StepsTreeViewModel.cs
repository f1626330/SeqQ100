using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    public class StepsTreeViewModel : ViewModelBase
    {
        #region Private Fields
        private StepsTreeViewModel _Parent;
        private StepsTree _Content;
        private bool _IsSelected;
        private bool _IsExpanded = true;
        #endregion Private Fields

        #region Constructor
        public StepsTreeViewModel()
        {
            Children = new ObservableCollection<StepsTreeViewModel>();
        }
        public StepsTreeViewModel(StepsTree content, StepsTreeViewModel parent)
        {
            _Content = content;
            _Parent = parent;
            Children = new ObservableCollection<StepsTreeViewModel>();

            if (parent != null)
            {
                parent.Children.Add(this);
            }
        }
        #endregion Constructor

        #region Public Properties
        public StepsTree Content
        {
            get { return _Content; }
            set
            {
                if (_Content != value)
                {
                    _Content = value;
                    RaisePropertyChanged(nameof(Content));
                }
            }
        }
        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                if (_IsSelected != value)
                {
                    _IsSelected = value;
                    RaisePropertyChanged(nameof(IsSelected));
                }
            }
        }
        public bool IsExpanded
        {
            get { return _IsExpanded; }
            set
            {
                if (_IsExpanded != value)
                {
                    _IsExpanded = value;
                    RaisePropertyChanged(nameof(IsExpanded));
                }
            }
        }
        public  StepsTreeViewModel Parent
        {
            get { return _Parent; }
            set
            {
                if (_Parent != value)
                {
                    _Parent = value;
                    RaisePropertyChanged(nameof(Parent));
                }
            }
        }
        public ObservableCollection<StepsTreeViewModel> Children { get; }
        #endregion Public Properties

        #region Public Functions
        public void AppendChild(StepsTree child)
        {
            new StepsTreeViewModel(child, this);
        }

        /// <summary>
        /// Create a StepsTreeViewModel object with children's view models added automatically.
        /// </summary>
        /// <param name="root"></param>
        public static StepsTreeViewModel CreateViewModel(StepsTree content, StepsTreeViewModel parent)
        {
            //StepsTreeViewModel result = new StepsTreeViewModel(content, parent);
            StepsTreeViewModel result = null;
            switch (content.Step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    result = new SetTemperStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.StopTemper:
                    result = new StopTemperStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.Imaging:
                    result = new ImagingStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.MoveStage:
                    result = new MoveStageStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.Pumping:
                    result = new PumpingStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.Loop:
                    result = new LoopStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.RunRecipe:
                    result = new RunRecipeStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.Waiting:
                    result = new WaitingStepViewModel(content, parent);
                    break;
                case RecipeStepTypes.Comment:
                    result = new CommentStepViewModel(content, parent);
                    break;
            }
            if (content.Children.Count > 0)
            {
                foreach (var item in content.Children)
                {
                    CreateViewModel(item, result);
                }
            }
            return result;
        }

        public virtual StepsTreeViewModel Clone()
        {
            StepsTreeViewModel clonedModel = new StepsTreeViewModel();
            return clonedModel;
        }
        #endregion Public Functions
    }
}
