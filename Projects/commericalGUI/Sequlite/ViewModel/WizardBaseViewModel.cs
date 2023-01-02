using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public abstract class WizardBaseViewModel : ViewBaseViewModel, IPageNavigator
    {
        #region Fields

        RelayCommand _cancelCommand;

        protected PageViewBaseViewModel _currentPage;
        RelayCommand _moveNextCommand;
        RelayCommand _movePreviousCommand;
        protected ReadOnlyCollection<PageViewBaseViewModel> _pages;
        protected  Dictionary<string, ModelBase> _pageModels;
        #endregion // Fields

        #region Constructor
        protected  ISeqApp SeqApp { get; }
        IDisposable AppMessageSubscriber { get; }
        //IDisposable TemperatureSubscriber { get; }
        //public TemperatureModel TemperModel { get; }
        //public UserPageModel UserModel { get; set; }
        public WizardBaseViewModel(ISeqApp seqApp, IDialogService dialogs)
        {
            _pageModels = new Dictionary<string, ModelBase>();
            DialogService = dialogs;
            SeqApp = seqApp;
            IsSimulation = seqApp.IsSimulation;
            //UserModel = userModel;
            //AddPageModel(SequencePageTypeEnum.SequenceWizard, userModel);

            //TemperModel = new TemperatureModel();
            //TemperModel.UpdateTemperarures(SeqApp.GetSystemMonitorInterface().TemperatureData);
            //ISystemMonitor systemMonitor = SeqApp.GetSystemMonitorInterface();
            //TemperatureSubscriber = AppObservableSubscriber.Subscribe(systemMonitor.TemperatureMonitor,
            //    it => TemperatureUpdated(it)
            //    );

            AppMessageSubscriber = AppObservableSubscriber.Subscribe(SeqApp.ObservableAppMessage,
                it => AppMessagegUpdated(it),
                it => AppMessgeError(it),
                () => AppMessageCompleted());

            //CurrentPage = Pages[0];
        }

        void AppMessgeError(Exception ex)
        {
        }

        void AppMessageCompleted()
        {

        }

        void AppMessagegUpdated(AppMessage appMsg)
        {

            //AppMessageTypeEnum.Status will be displayed in different ways
            if (appMsg.MessageType != AppMessageTypeEnum.Status)
            {
                ApplicationStatus = appMsg;
            }
            if (appMsg.MessageType == AppMessageTypeEnum.ErrorNotification)
            {
                Dispatch(() =>
                {
                    //MessageBox.Show(appMsg.Message, "Attention", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    new MessageBoxViewModel
                    {
                        Message = appMsg.Message,
                        Caption = "Attention",
                        Image = MessageBoxImage.Exclamation,
                        Buttons = MessageBoxButton.OK,
                        IsModal = true,
                    }.Show(DialogService.Dialogs);

                });
            }

        }
        //void TemperatureUpdated(TemperatureStatusBase temperStatus)
        //{
        //    Dispatch(() =>
        //    {
        //        TemperModel.UpdateTemperarures(temperStatus as TemperatureStatus);
        //    });
        //}

        #endregion // Constructor

        #region Commands

        #region CancelCommand


        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                    _cancelCommand = new RelayCommand((o) => CancelPage(),
                         (o) => (CurrentPage != null) ? CurrentPage.CanCancelPage : true);

                return _cancelCommand;
            }
        }

        public void CancelPage(bool confirmed = true)
        {
            if (CurrentPage == null)
            {
                OnRequestClose();
            }
            else
            {
                if (confirmed)
                {
                    if (!CurrentPage.CanCelPage())
                    {
                        if (ConfirmCancel())
                        {
                            CurrentPage.SetCurrentPageState(false);
                            OnRequestClose();
                        }
                    }
                }
                else
                {
                    CurrentPage.SetCurrentPageState(false);
                    OnRequestClose(); 
                }
            }
        }

        protected abstract bool ConfirmCancel();
        #endregion // CancelCommand

        #region MovePreviousCommand

        /// <summary>
        /// Returns the command which, when executed, causes the CurrentPage 
        /// property to reference the previous page in the work flow.
        /// </summary>
        public ICommand MovePreviousCommand
        {
            get
            {
                if (_movePreviousCommand == null)
                    _movePreviousCommand = new RelayCommand(
                        (o) => MoveToPreviousPage(),
                        (o) => CanMoveToPreviousPage);

                return _movePreviousCommand;
            }
        }

        bool _CanMoveToPreviousPage = true;
        public bool CanMoveToPreviousPage
        {
            get
            {
                return _CanMoveToPreviousPage &&
                    (0 < CurrentPageIndex || (CurrentPageIndex == 0 && CurrentPage.HasSubpages && (!CurrentPage.IsOnFirstSubpage)));
            }
            set
            {
                _CanMoveToPreviousPage = value;
                RaisePropertyChanged(nameof(CanMoveToPreviousPage));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void MoveToPreviousPage()
        {
            if (CurrentPage == null)
            {
                return;
            }
            if (!CurrentPage.MoveToPreviousPage() && CanMoveToPreviousPage)
            {
                if (CurrentPage.HasSubpages)
                {
                    if (CurrentPage.IsOnFirstSubpage)
                    {
                        if (CurrentPage.CanMoveOutSubpages)
                        {
                            CurrentPage.SetCurrentPageState(false);
                            CurrentPage = Pages[CurrentPageIndex - 1];
                            CurrentPage.SetCurrentPageState(true);
                        }
                    }
                    else
                    {
                        CurrentPage.MoveToPreviousSubpage();
                    }
                }
                else
                {
                    CurrentPage.SetCurrentPageState(false);
                    CurrentPage = Pages[CurrentPageIndex - 1];
                    CurrentPage.SetCurrentPageState(true);
                }
            }
        }

        #endregion // MovePreviousCommand

        #region MoveNextCommand

        /// <summary>
        /// Returns the command which, when executed, causes the CurrentPage 
        /// property to reference the next page in the workflow.  If the user
        /// is viewing the last page in the workflow, this causes the Wizard
        /// to finish and be removed from the user interface.
        /// </summary>
        public ICommand MoveNextCommand
        {
            get
            {
                if (_moveNextCommand == null)
                    _moveNextCommand = new RelayCommand(
                        (o) => MoveToNextPage(),
                        (o) => CanMoveToNextPage);

                return _moveNextCommand;
            }
        }

        bool _CanMoveToNextPage = true;
        public bool CanMoveToNextPage
        {
            get { return _CanMoveToNextPage && (CurrentPage != null && CurrentPage.IsPageDone()); }
            set
            {
                _CanMoveToNextPage = value;
                RaisePropertyChanged(nameof(CanMoveToNextPage));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void MoveToNextPage()
        {
            if (CurrentPage == null)
            {
                return;
            }
            if (!CurrentPage.MoveToNextPage())// && CanMoveToNextPage)
            {
                //bool canMoveToNextPage = CanMoveToNextPage;
                //CanMoveToNextPage = false;
                try
                {
                    if (CurrentPage.HasSubpages)
                    {
                        if (CurrentPage.IsOnLastSubpage)
                        {
                            if (CurrentPage.CanMoveOutSubpages)
                            {
                                if (CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count - 1)
                                {
                                    CurrentPage.SetCurrentPageState(false);
                                    CurrentPage = Pages[CurrentPageIndex + 1];
                                    CurrentPage.SetCurrentPageState(true);
                                }
                                else
                                {
                                    OnRequestClose();
                                }
                            }
                        }
                        else
                        {
                            CurrentPage.MoveToNextSubpage();
                        }
                    }
                    else
                    {
                        if (CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count - 1)
                        {
                            CurrentPage.SetCurrentPageState(false);
                            CurrentPage = Pages[CurrentPageIndex + 1];
                            CurrentPage.SetCurrentPageState(true);
                        }
                        else
                        {
                            OnRequestClose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to move to next page with exception {ex}");
                    
                }
                finally
                {
                   // CanMoveToNextPage = canMoveToNextPage;
                }
            }

        }

        #endregion // MoveNextCommand

        #endregion // Commands

        #region Properties


        /// Returns the page ViewModel that the user is currently viewing.
        /// </summary>
        public PageViewBaseViewModel CurrentPage
        {
            get { return _currentPage; }
            protected set
            {
                if (value == _currentPage)
                    return;

                if (_currentPage != null)
                    _currentPage.IsCurrentPage = false;

                _currentPage = value;

                if (_currentPage != null)
                    _currentPage.IsCurrentPage = true;

                RaisePropertyChanged(nameof(CurrentPage));
                RaisePropertyChanged(nameof(IsOnLastPage));
                //RaisePropertyChanged(nameof(SubpageNames));
                CurrentPage.OnUpdateCurrentPageChanged();
            }
        }

        /// <summary>
        /// Returns true if the user is currently viewing the last page 
        /// in the workflow.  This property is used by SeqenceWizardViewModel
        /// to switch the Next button's text to "Finish" when the user
        /// has reached the final page.
        /// </summary>
        virtual public bool IsOnLastPage
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a read-only collection of all page ViewModels.
        /// </summary>
        public ReadOnlyCollection<PageViewBaseViewModel> Pages
        {
            get
            {
                if (_pages == null)
                    CreatePages(SeqApp);

                return _pages;
            }
        }

        #endregion // Properties

        #region Events

        /// <summary>
        /// Raised when the wizard should be removed from the UI.
        /// </summary>

        #endregion // Events

        #region Private Helpers

        abstract protected void CreatePages(ISeqApp seqApp);
        //{

        //    var pages = new List<PageViewBaseViewModel>();

        //    pages.Add(new UserPageViewModel(this, seqApp, DialogService));
        //    pages.Add(new LoadPageViewModel(this, seqApp, DialogService));
        //    pages.Add(new RunSetupPageViewModel(this, seqApp, DialogService));
        //    pages.Add(new CheckPageViewModel(this, seqApp, DialogService));
        //    pages.Add(new SeqencePageViewModel(this, seqApp, DialogService));
        //    pages.Add(new PostRunPageViewModel(this, seqApp, DialogService));
        //    pages.Add(new SummaryPageViewModel(this, seqApp, DialogService));
        //    _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
        //}

        protected int CurrentPageIndex
        {
            get
            {

                if (CurrentPage == null)
                {
                    //Debug.Fail("Why is the current page null?");
                    return -1;
                }

                return Pages.IndexOf(CurrentPage);
            }
        }
        #endregion // Private Helpers

        private AppMessage _ApplicationStatus;
        public AppMessage ApplicationStatus
        {
            get
            {
                return _ApplicationStatus;
            }
            set
            {
                SetProperty(ref _ApplicationStatus, value, nameof(ApplicationStatus));
            }
        }

        bool _IsSimulation = false;
        public bool IsSimulation
        {
            get => _IsSimulation;
            set
            {
                SetProperty(ref _IsSimulation, value, nameof(IsSimulation));
            }
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Pages != null)
            {
                foreach (var p in Pages)
                {
                    p.Dispose();
                }
            }
            //AppObservableSubscriber.Unsubscribe(TemperatureSubscriber);
            AppObservableSubscriber.Unsubscribe(AppMessageSubscriber);
        }

        public ModelBase GetPageModel(string pageType)
        {
            if (_pageModels.ContainsKey(pageType))
            {
                return _pageModels[pageType];
            }
            else
            {
                return null;
            }
        }

        public void AddPageModel(string pageType, ModelBase model)
        {
            if (!_pageModels.ContainsKey(pageType))
            {
                _pageModels.Add(pageType, model);
            }
            else
            {
                throw new Exception($"A page model is already added for page type {pageType}");
            }
        }
    }
}
