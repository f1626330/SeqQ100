using Sequlite.WPF.Framework;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class PaneViewModel : ViewModelBase
    {
        #region Properties

        #region Title
        private string _Title;
        public string Title
        {
            get { return _Title; }
            set
            {
                if (_Title != value)
                {
                    _Title = value;
                    RaisePropertyChanged(nameof(Title));
                }
            }
        }
        #endregion

        #region IsActive
        private bool _IsActive;
        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                if (_IsActive != value)
                {
                    _IsActive = value;
                    RaisePropertyChanged(nameof(IsActive));
                }
            }
        }
        #endregion IsActive

        #region CloseCommand
        private ICommand _CloseCommand;
        public ICommand CloseCommand
        {
            get
            {
                if (_CloseCommand == null)
                    _CloseCommand = new RelayCommand(call => Close());
                return _CloseCommand;
            }
        }

        public virtual void Close()
        {
        }
        #endregion

        #endregion

    }
}
