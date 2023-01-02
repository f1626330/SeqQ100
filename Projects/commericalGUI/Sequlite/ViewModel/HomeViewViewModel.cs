
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public enum HomeViewActionEnum
    {
        RunSequence,
        RunMaintenance,
        RunEngineering,
        RunSettings,
        RunData,
        ExitApp,
    }

    public class HomeViewActionEventArgs : EventArgs
    {
        public HomeViewActionEnum HomePageAction { get; set; }
    }

    public class UserLoginStatusEventArgs : EventArgs
    {
        //public bool IsLoggedIn { get; set; }
        ////public AuthenticationEnum UserLevel { get; set; }
    }

    public class HomeViewViewModel : ViewBaseViewModel
    {
        public event EventHandler<HomeViewActionEventArgs> OnHomePageAction;
        public event EventHandler<UserLoginStatusEventArgs> OnUserLoggedInStatusChanged;
        ISystemMonitor SystemMonitor { get; }
        IDisposable TemperatureSubscriber { get; set; }
        public UserPageModel UserModel { get; set; }
        bool _IsSyetmReady;
        public bool IsSyetmReady { get => _IsSyetmReady; set => SetProperty(ref _IsSyetmReady, value); }

        bool _IsEnoughDiskSpace=true;
        public bool IsEnoughDiskSpace { get => _IsEnoughDiskSpace; set => SetProperty(ref _IsEnoughDiskSpace, value); }
        bool _IsChillerTempReady;
        public bool IsChillerTempReady { get => _IsChillerTempReady; set => SetProperty(ref _IsChillerTempReady, value); }

        bool _SystemCheckFailure = false;
        public bool SystemCheckFailure { get => _SystemCheckFailure; set => SetProperty(ref _SystemCheckFailure, value); }

        double _ChillerTemperature;
        public double ChillerTemperature { get => _ChillerTemperature; set => SetProperty(ref _ChillerTemperature, value); }

        public bool AllowCancel => true;
        public HomeViewViewModel(ISeqFileLog logger, ISeqApp seqApp, IDialogService dialogs)
        {
            Logger = logger;
            DialogService = dialogs;
            
           // UserModel = new UserPageModel();
            SystemMonitor = seqApp.GetSystemMonitorInterface();
            TemperatureSubscriber = AppObservableSubscriber.Subscribe(SystemMonitor.TemperatureMonitor,
                it => TemperatureUpdated(it)
                );
            Logger.Log("Checking chiller temperature...");
            IsSyetmReady = false;
            SystemMonitor.CheckTemperature();
            //ISystemCheck syscheck = seqApp.CreateSystemCheckInterface();
            //IsEnoughDiskSpace = syscheck.DiskSpaceCheck(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.MaximumReadlength);
            Logger.Log("Please log in");
            //SystemMonitor.CheckTemperature();

        }

        void TemperatureUpdated(TemperatureStatusBase temperStatus)
        {
            if (temperStatus is ChillerTemperatureStatus)
            {
                ChillerTemperatureStatus chillerTemperatureStatus = temperStatus as ChillerTemperatureStatus;
                Dispatch(() =>
                {
                    IsChillerTempReady = chillerTemperatureStatus.IsChillerTempReady;
                    IsSyetmReady = IsChillerTempReady && IsEnoughDiskSpace;
                    CanRunSequence = IsSyetmReady;
                    SystemCheckFailure = chillerTemperatureStatus.HasError;
                    ChillerTemperature = chillerTemperatureStatus.ChillerTemperature;
                });

                if (chillerTemperatureStatus.IsChillerTempReady)
                {
                    Logger.Log($"Chiller Temperature is ready at {chillerTemperatureStatus.ChillerTemperature}");
                    if (!IsEnoughDiskSpace)
                    {
                        Logger.LogError("System check failed since there is not enough disk space");
                    }
                }
                else if (chillerTemperatureStatus.HasError)
                {
                    Logger.LogError($"Temperature Checking returns error, the Chiller Temperature is at {chillerTemperatureStatus.ChillerTemperature}");
                }

                if (IsSyetmReady)
                {
                    Logger.Log("System is ready to use");
                }
            }
        }

        void InvokenHomePageActionEvent(HomeViewActionEnum e)
        {
            OnHomePageAction?.Invoke(this, new HomeViewActionEventArgs() { HomePageAction = e });
        }

        ICommand _RunSequence;
        public ICommand RunSequence
        {
            get
            {
                if (_RunSequence == null)
                    _RunSequence = new RelayCommand(
                        (o) => this.RunSequenceCmd(),
                        (o) => this.CanRunSequence);

                return _RunSequence;
            }
        }


        //public bool CanRunSequence 
        //{
        //    get => IsChillerTempReady;
        //}

        bool _CanRunSequence;
        public bool CanRunSequence
        {
            get => _CanRunSequence;
            set => SetProperty(ref _CanRunSequence, value, nameof(CanRunSequence), true);
        }

        void RunSequenceCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.RunSequence);
        }

        ICommand _RunMaintenance;
        public ICommand RunMaintenance
        {
            get
            {
                if (_RunMaintenance == null)
                    _RunMaintenance = new RelayCommand(
                        (o) => this.RunMaintenanceCmd(),
                        (o) => this.CanRunMaintenance);

                return _RunMaintenance;
            }
        }
        bool CanRunMaintenance
        {
            get { return true; }
        }

        void RunMaintenanceCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.RunMaintenance);
        }

        ICommand _RunEngineering;
        public ICommand RunEngineering
        {
            get
            {
                if (_RunEngineering == null)
                    _RunEngineering = new RelayCommand(
                        (o) => this.RunEngineeringCmd(),
                        (o) => this.CanRunEngineering);

                return _RunEngineering;
            }
        }
        bool CanRunEngineering
        {
            get { return true; }
        }

        void RunEngineeringCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.RunEngineering);
        }

        ICommand _RunSettings;
        public ICommand RunSettings
        {
            get
            {
                if (_RunSettings == null)
                    _RunSettings = new RelayCommand(
                        (o) => this.RunSettingsCmd(),
                        (o) => this.CanRunSettings);

                return _RunSettings;
            }
        }
        bool CanRunSettings
        {
            get { return true; }
        }

        void RunSettingsCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.RunSettings);
        }

        ICommand _RunData;
        public ICommand RunData
        {
            get
            {
                if (_RunData == null)
                    _RunData = new RelayCommand(
                        (o) => this.RunDataCmd(),
                        (o) => this.CanRunData);

                return _RunData;
            }
        }
        bool CanRunData
        {
            get { return true; }
        }

        void RunDataCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.RunData);
        }

        ICommand _ExitApp;
        public ICommand ExitApp
        {
            get
            {
                if (_ExitApp == null)
                    _ExitApp = new RelayCommand(
                        (o) => this.ExitAppCmd(),
                        (o) => this.CanExitApp);

                return _ExitApp;
            }
        }
        bool CanExitApp
        {
            get { return true; }
        }

        void ExitAppCmd()
        {
            InvokenHomePageActionEvent(HomeViewActionEnum.ExitApp);
        }



        private ICommand _LoginCmd = null;
        public ICommand LoginCmd
        {
            get
            {
                if (_LoginCmd == null)
                {
                    _LoginCmd = new RelayCommand(o => Login(o), o => UserModel.CanLogin);
                }
                return _LoginCmd;
            }
        }

        async void  Login(object o)
        {
            IsLogging = true;
            await Task.Run(() =>
            {
                UserModel.Login();
            });
            IsLogging = false;
            if (UserModel.IsLoggedIn)
            {
                Logger.Log($"User {UserModel.UserName} has been logged in");
                OnUserLoggedInStatusChanged?.Invoke(this, new UserLoginStatusEventArgs());// { IsLoggedIn = true , UserLevel = UserModel.AuthenticationLevel});
            }
           
        }

        private ICommand _LogoutCmd = null;
        public ICommand LogoutCmd
        {
            get
            {
                if (_LogoutCmd == null)
                {
                    _LogoutCmd = new RelayCommand(o => Logout(o));
                }
                return _LogoutCmd;
            }
        }

        void Logout(object o)
        {
            UserModel.Logout();
            if (!UserModel.IsLoggedIn)
            {
                OnUserLoggedInStatusChanged?.Invoke(this, new UserLoginStatusEventArgs());// { IsLoggedIn = false, UserLevel=AuthenticationEnum.User });
            }
        }

        bool _IsLogging = false;
        public bool IsLogging { get => _IsLogging; set => SetProperty(ref _IsLogging, value); }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AppObservableSubscriber.Unsubscribe(TemperatureSubscriber);
            TemperatureSubscriber = null;

        }

    }
}
