using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.ALF.EngineerGUI.ViewModel;
using Sequlite.UI.Model;
using Sequlite.UI.ViewModel;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AuthenticationEnum = Sequlite.ALF.Common.AccessRightEnum;
namespace Sequlite.UI
{
    public class MainWindowViewModel : ViewModelBase, IDialogService
    {
        ISeqFileLog Logger { get; set; }
        HomeViewViewModel HomePageVM { get; set; }
        public ObservableCollection<IDialogBoxViewModel> Dialogs { get; private set; }
        ISeqApp SeqApp { get; }
        UserPageModel UserModel { get; }
        IUser UserAccount { get; }
        public MainWindowViewModel(ISeqFileLog logger, ISeqApp seqApp, IUser userAcc)
        {
            Logger = logger;
            SeqApp = seqApp;
            UserAccount = userAcc;
            Dialogs = new ObservableCollection<IDialogBoxViewModel>();
            BindingOperations.EnableCollectionSynchronization(Dialogs, new object());
            InitialLogWindowVM();
            UserModel = new UserPageModel() { UserAccount = userAcc };
            HomePageVM = new HomeViewViewModel(Logger, seqApp, this) { LogWindowVM = this.LogWindowVM, UserModel= UserModel };
            HomePageVM.OnHomePageAction += HomePageVM_OnHomePageAction;
            HomePageVM.OnUserLoggedInStatusChanged += HomePageVM_OnUserLoggedInStatusChanged;
            CurrentPage = HomePageVM;
        }

        private void HomePageVM_OnUserLoggedInStatusChanged(object sender, UserLoginStatusEventArgs e)
        {
            if (UserModel.IsLoggedIn)
            {
                switch (UserModel.AuthenticationLevel)
                {
                    case AuthenticationEnum.User:
                    case AuthenticationEnum.Admin:
                    case AuthenticationEnum.Tech:
                        MainWindowTopMost = true;
                        MainWindowState = WindowState.Maximized;
                        MainWindowStyle = WindowStyle.None;
                        MainWindowResizeMode = ResizeMode.NoResize;
                        break;
                    case AuthenticationEnum.Master:
                        MainWindowTopMost = false;
                        MainWindowStyle = WindowStyle.SingleBorderWindow;
                        MainWindowResizeMode = ResizeMode.CanResizeWithGrip;
                        break;
                }
            }
            else
            {
                MainWindowTopMost = true;
                MainWindowState = WindowState.Normal; //do not delete this .
                MainWindowStyle = WindowStyle.None;
                MainWindowResizeMode = ResizeMode.NoResize;
                MainWindowState = WindowState.Maximized;
            }
        }

        private void InitialLogWindowVM()
        {
            if (LogWindowVM == null)
            {
                LogWindowVM = new LogWindowViewModel(false) { LogViewerVM = new LogViewerViewModel(Logger), DispalyDebugMessage = false };
                LogWindowVM.Title = "Log Viewer";
            }
        }

        private LogWindowViewModel _LogWindowVM;
        public LogWindowViewModel LogWindowVM
        {
            get { return _LogWindowVM; }
            set { _LogWindowVM = value; RaisePropertyChanged("LogWindowVM"); }
        }

        private ICommand _WindowClosing = null;
        public ICommand WindowClosing
        {
            get
            {
                if (_WindowClosing == null)
                {
                    _WindowClosing = new RelayCommand(o => Closeing(o), o => CanClose);
                }
                return _WindowClosing;
            }
        }

        private ICommand _WindowClosed = null;
        public ICommand WindowClosed
        {
            get
            {
                if (_WindowClosed == null)
                {
                    _WindowClosed = new RelayCommand(o => Close());
                }
                return _WindowClosed;
            }
        }

        private void Closeing(object obj)
        {
            
        }

        private void Close()
        {
            Application.Current.Shutdown();
        }

        bool _canClose;
        public bool CanClose
        {
            get
            {
                return _canClose;
            }
            set
            {
                _canClose = value;
                RaisePropertyChanged(nameof(CanClose));
            }

        }

        private void HomePageVM_OnHomePageAction(object sender, HomeViewActionEventArgs e)
        {
            try
            {
                HomeViewViewModel hmVM = sender as HomeViewViewModel;
                switch (e.HomePageAction)
                {
                    case HomeViewActionEnum.RunSequence:
                        {
                            SeqenceWizardViewModel page = new SeqenceWizardViewModel(SeqApp, this, hmVM.UserModel)
                            { Logger = this.Logger, LogWindowVM = this.LogWindowVM };

                            page.RequestClose += Page_RequestClose;
                            CurrentPage = page;
                        }
                        break;
                    case HomeViewActionEnum.RunMaintenance:
                        {
                            MaintenanceViewModel page = new MaintenanceViewModel(SeqApp, this) { Logger = this.Logger, LogWindowVM = this.LogWindowVM };
                            page.RequestClose += Page_RequestClose;
                            CurrentPage = page;
                        }
                        break;
                    case HomeViewActionEnum.RunEngineering:
                        {

                            Workspace seqWorkspace = new Workspace(Logger, Dialogs);
                            seqWorkspace.CreatViewModels(SeqApp);
                            seqWorkspace.LogWindowVM = this.LogWindowVM;
                            EngineeringViewModel page = new EngineeringViewModel(this) { EUIViewModel = seqWorkspace };
                            page.RequestClose += Page_RequestClose;
                            CurrentPage = page;
                        }
                        break;
                    case HomeViewActionEnum.RunSettings:
                        {
                            SettingsViewModel page = new SettingsViewModel(this, hmVM.UserModel, this.Logger)
                            {
                                LogWindowVM = this.LogWindowVM
                            };


                            page.RequestClose += Page_RequestClose;
                            CurrentPage = page;
                        }
                        break;

                    case HomeViewActionEnum.RunData:
                        {
                            DataOptionViewModel page = new DataOptionViewModel(SeqApp, this) { LogWindowVM = this.LogWindowVM , UserModel= hmVM.UserModel };

                            page.RequestClose += Page_RequestClose;
                            CurrentPage = page;
                        }
                        break;
                    case HomeViewActionEnum.ExitApp:
                        {
                            if (UserModel.AuthenticationLevel != AuthenticationEnum.Master)
                            {
                                bool shutdown = true;
#if DEBUG
                                if (UserModel.AuthenticationLevel == AuthenticationEnum.None)
                                {
                                    shutdown = false;
                                }
#endif
                                if (shutdown)
                                {
                                    MessageBoxViewModel msgVm = new MessageBoxViewModel()
                                    {
                                        Message = "Are you sure you want to shutdown and power off?",
                                        Caption = "Shutdown and power off",
                                        Image = MessageBoxImage.Question,
                                        Buttons = MessageBoxButton.YesNo,
                                        IsModal = true,
                                    };

                                    if (msgVm.Show(this.Dialogs) == MessageBoxResult.Yes)
                                    {
                                        Close();
                                        Process.Start(new ProcessStartInfo("shutdown", "/s /f /t 0")
                                        {
                                            CreateNoWindow = true,
                                            UseShellExecute = false
                                        });
                                    }
                                }
                                else
                                {
                                    Close();
                                }
                            }
                            else
                            {
                                Close();
                            }

                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                string str  = $"Failed to create UI for {Enum.GetName(typeof(HomeViewActionEnum), e.HomePageAction)} with error: {ex.Message} : Exception: {ex.StackTrace}";

                Logger.LogError (str);
                MessageBoxViewModel msgVm = new MessageBoxViewModel()
                {
                    Message = $"Failed to create UI for {Enum.GetName(typeof(HomeViewActionEnum), e.HomePageAction)} with error: {ex.Message}.",
                    Caption = "Error",
                    Image = MessageBoxImage.Error,
                    Buttons = MessageBoxButton.OK,
                    IsModal = true
                };
                msgVm.Show(this.Dialogs);
            }
        }

        private void Page_RequestClose(object sender, EventArgs e)
        {
            ViewModelBase vm = sender as ViewModelBase;
            if (vm != null)
            {
                vm.Dispose();
            }
            CurrentPage = HomePageVM;
           
        }

        ViewModelBase _CurrentPage;
        public ViewModelBase CurrentPage
        {
            get { return _CurrentPage; }
            set
            {
                _CurrentPage = value;
                if (_CurrentPage != HomePageVM)
                {
                    CanClose = false;
                }
                else
                {
                    CanClose = true;
                    //ISystemCheck syscheck = SeqApp.CreateSystemCheckInterface();
                    //HomePageVM.IsEnoughDiskSpace = syscheck.DiskSpaceCheck(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.MaximumReadlength);
                    //if (!HomePageVM.IsEnoughDiskSpace)
                    //{
                    //    MessageBoxViewModel msgVm = new MessageBoxViewModel()
                    //    {
                    //        Message = $"Not enough disk space, please transfer or delete data",
                    //        Caption = "Error",
                    //        Image = MessageBoxImage.Error,
                    //        Buttons = MessageBoxButton.OK,
                    //        IsModal = true
                    //    };
                    //    msgVm.Show(this.Dialogs);
                    //}
                    HomePageVM.IsSyetmReady = HomePageVM.IsEnoughDiskSpace && HomePageVM.IsChillerTempReady;
                    HomePageVM.CanRunSequence = HomePageVM.IsSyetmReady;
                }
                RaisePropertyChanged(nameof(CurrentPage));
            }
        }

        WindowStyle _MainWindowStyle = WindowStyle.None;
        public WindowStyle MainWindowStyle { get => _MainWindowStyle; set => SetProperty(ref _MainWindowStyle, value); }

        bool _MainWindowTopMost = true;
        public bool MainWindowTopMost
        {
            get => _MainWindowTopMost;
            set =>  SetProperty(ref _MainWindowTopMost, value);
        }

        ResizeMode _MainWindowResizeMode = ResizeMode.NoResize;
        public ResizeMode MainWindowResizeMode { get => _MainWindowResizeMode; set => SetProperty(ref _MainWindowResizeMode, value); }
        WindowState _MainWindowState = WindowState.Maximized;
        public WindowState MainWindowState { get => _MainWindowState; set => SetProperty(ref _MainWindowState, value); }

        private ICommand _MainWindowSizeChangedCommand;
        public ICommand MainWindowSizeChangedCommand
        {
            get
            {
                if (_MainWindowSizeChangedCommand == null)
                {
                    _MainWindowSizeChangedCommand = new RelayCommand(o => MainWindowSizeChangedCmd(o));
                }
                return _MainWindowSizeChangedCommand;
            }
        }

        private ICommand _KeyDownCommand = null;
        public ICommand KeyDownCommand
        {
            get
            {
                if (_KeyDownCommand == null)
                {
                    _KeyDownCommand = new RelayCommand(o => KeyDownCmdd(o));
                }
                return _KeyDownCommand;
            }
        }

        private void KeyDownCmdd(object obj)
        {
            //KeyEventArgs e = obj as KeyEventArgs;
            //if (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt)
            //{
            //    if (e.Key == Key.System)
            //    {
            //        e.Handled = true;
            //    }
            //}
            if (obj != null)
            {
                int n = -1;
                if (int.TryParse(obj.ToString(), out n))
                {
                    if (n == 1)
                    {
                        MainWindowState = WindowState.Maximized;
                    }
                }
            }
        }

        Size _winSize = Size.Empty;
        void MainWindowSizeChangedCmd(object obj)
        {
            SizeChangedEventArgs e = obj as SizeChangedEventArgs;

            if (e != null && UserModel.AuthenticationLevel != AuthenticationEnum.Master)
            {
                if (e.NewSize != _winSize)
                {
                    MainWindowTopMost = true;
                    MainWindowState = WindowState.Normal; //do not delete this .
                    MainWindowStyle = WindowStyle.None;
                    MainWindowResizeMode = ResizeMode.NoResize;
                    MainWindowState = WindowState.Maximized;
                }
                _winSize = e.NewSize;
            }
        }
    }
}
