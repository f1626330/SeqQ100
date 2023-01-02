using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System.Collections.ObjectModel;

namespace Sequlite.ALF.EngineerGUI.ViewModel
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
        public StepsTreeViewModel() { }
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
                    RaisePropertyChanged(nameof(Content)); //content may change such as DisplayName for RunRecipe
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
        public StepsTreeViewModel Parent
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
            StepsTreeViewModel result = new StepsTreeViewModel(content, parent);
            if (content.Children.Count > 0)
            {
                foreach (var item in content.Children)
                {
                    CreateViewModel(item, result);
                }
            }
            return result;
        }
        #endregion Public Functions
    }
}
