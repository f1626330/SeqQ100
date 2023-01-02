using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class MaintenanceViewModel : ViewBaseViewModel
    {
        const string PostRunodel_UnloadStatus = "Unload reagent Cartridge";
        const string PostRunodel_LoadStatus = "Load Wash tray";
        const string PostRunodel_WashStatus = "Wash";
        public IPostRun PostRunApp;
        IDisposable AppMessageObserver { get; set; }

        public List<WashOption> WashOptions { get; set; }

        private WashOption _SelectedWashOption;
        public WashOption SelectedWashOption
        {
            get => _SelectedWashOption;
            set
            {
                if(value == WashOption.ManualPostWash)
                {
                    CanWash = true;
                    CanUnload = false;
                    CanUnloadB = false;
                    new MessageBoxViewModel
                    {
                        Message = "ManualPostWash start with reagent cartridge first, replace with wash tray later",
                        Caption = "ManualPostWash Message",
                        Image = MessageBoxImage.Information,
                        Buttons = MessageBoxButton.OK,
                        IsModal = true
                    }.Show(DialogService.Dialogs);

                }
                else
                {
                    CanWash = false;
                    CanUnload = true;
                    CanUnloadB = false;
                }
                SetProperty(ref _SelectedWashOption, value);
            }
        }

        public UserPageModel UserModel { get; }
        public PostRunPageModel PostRunModel { get; }
        bool StopRunByUser { get; set; }
        bool IsWashSDone { get; set; }
        private string _Instruction = Instructions.PostRunPage_Instruction;
        ObservableCollection<PageStatusModel> _StatusList;
        public ObservableCollection<PageStatusModel> StatusList
        {
            get
            {
                return _StatusList;
            }
            set
            {
                SetProperty(ref _StatusList, value, nameof(StatusList));
            }
        }

        public MaintenanceViewModel(ISeqApp seqApp, IDialogService dialogs)
        {
            DialogService = dialogs;
            UserModel = new UserPageModel();
            string sessionId = UserModel.GetNewSessionId();
            UserModel.CurrentSessionId = sessionId;
            PostRunModel = new PostRunPageModel();
            PostRunApp = seqApp.CreatePostRunInterface();

            if (AppMessageObserver == null)
            {
                AppMessageObserver = AppObservableSubscriber.Subscribe(seqApp.ObservableAppMessage,
                        it => AppMessagegUpdated(it));

            }
            if (StatusList == null)
            {
                ObservableCollection<PageStatusModel> ls = new ObservableCollection<PageStatusModel>()
                    {
                         new PageStatusModel() {Name = PostRunodel_UnloadStatus  },
                         new PageStatusModel() {Name = PostRunodel_LoadStatus},
                         new PageStatusModel() {Name = PostRunodel_WashStatus},
                            };
                StatusList = ls;
            }
            WashOptions = new List<WashOption>();
            foreach (WashOption washes in Enum.GetValues(typeof(WashOption)))
            {
                if (washes != WashOption.PostWash) { WashOptions.Add(washes); }
            }
            SelectedWashOption = WashOptions[0];
        }
        private string _Instruction_inline = @"
                <h4><u>Change wash solutions:</u></h4>   
                <ol> 
                    <li>Select &quot;Unload Reagent Cartridge&quot; button</li>
                    <li>Remove the reagent cartridge</li>
                    <li>Load the Reagent wash tray and buffer wash tray with wash solution</li>
                    <li>Slide in the Reagent wash tray</li>
                    <li>Replace the buffer tray</li>
                    <li>Click &quot;Load Reagent wash + buffer wash Trays&quot; button</li>
                    <li>Click &quot;Start Wash&quot; button</li>
                </ol>";

        public string Instruction_inline => _Instruction_inline;
        void AppMessagegUpdated(AppMessage appMsg)
        {
            Dispatch((Action)(() =>
            {
                AppStatus st = appMsg.MessageObject as AppStatus;
                if (st != null)
                {
                    switch (st.AppStatusType)
                    {
                        case AppStatusTypeEnum.AppRunWashStatus:
                            {
                                PostRunModel.UpdateWashingStatus(st as AppRunWashStatus);
                                //CheckIfShowMoveNext();
                            }
                            break;
                    }
                }
                if (appMsg.MessageType == AppMessageTypeEnum.ErrorNotification)
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

                }
            }));
        }

        #region Unload
        //-----------------------------------Unload---------------------------------
        private ICommand _UnloadCmd = null;
        public ICommand UnloadCmd
        {
            get
            {
                if (_UnloadCmd == null)
                {
                    _UnloadCmd = new RelayCommand(o => Unload(o), o => CanUnload);
                }
                return _UnloadCmd;
            }
        }

        bool _CanUnload = true;
        public bool CanUnload
        {
            get => _CanUnload;
            set => SetProperty(ref _CanUnload, value, nameof(CanUnload), true);
        }

        //button function
        async void Unload(object o)
        {
            bool bUnloaded = false;
            try
            {

                CanUnload = false;
                CanLoad = false;
                CanWash = false;
                UpdateCurrentPageStatus(PostRunodel_LoadStatus, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(PostRunodel_WashStatus, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(PostRunodel_UnloadStatus, PageStatusEnum.Start);
                bUnloaded = await RunUnload();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to unload reagent cartridge with error: {ex.Message}");
            }
            finally
            {

                CanUnload = !bUnloaded;
                CanUnloadB = true;
                //CanLoad = bUnloaded;
                UpdateCurrentPageStatus(PostRunodel_UnloadStatus, bUnloaded ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }


        //lift reagent snipper
        private async Task<bool> RunUnload()
        {
            bool bUnloaded = false;
            bUnloaded = await Task<bool>.Run(() =>
            {
                bool b = PostRunApp.UnloadCart();
                return b;
            });
            return bUnloaded;
        }
        #endregion

        #region UnloadB
        //-----------------------------------Unload---------------------------------
        private ICommand _UnloadBCmd = null;
        public ICommand UnloadBCmd
        {
            get
            {
                if (_UnloadBCmd == null)
                {
                    _UnloadBCmd = new RelayCommand(o => UnloadB(o), o => CanUnloadB);
                }
                return _UnloadBCmd;
            }
        }

        bool _CanUnloadB = true;
        public bool CanUnloadB
        {
            get => _CanUnloadB;
            set => SetProperty(ref _CanUnloadB, value, nameof(CanUnloadB), true);
        }

        //button function unload buffer
        void UnloadB(object o)
        {
            bool bUnloaded = false;
            try
            {

                CanUnloadB = false;
                CanLoad = false;
                CanWash = false;
                UpdateCurrentPageStatus(PostRunodel_LoadStatus, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(PostRunodel_WashStatus, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(PostRunodel_UnloadStatus, PageStatusEnum.Start);
                bUnloaded = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to unload reagent cartridge with error: {ex.Message}");
            }
            finally
            {

                CanUnloadB = !bUnloaded;
                CanLoad = bUnloaded && !CanUnload;
                UpdateCurrentPageStatus(PostRunodel_UnloadStatus, bUnloaded ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }
        #endregion

        #region Load
        //----------------- Load -----------------------------------------------------------------
        private ICommand _LoadCmd = null;
        public ICommand LoadCmd
        {
            get
            {
                if (_LoadCmd == null)
                {
                    _LoadCmd = new RelayCommand(o => Load(o), o => CanLoad);
                }
                return _LoadCmd;
            }
        }

        bool _CanLoad = false;
        public bool CanLoad
        {
            get => _CanLoad;
            set => SetProperty(ref _CanLoad, value, nameof(CanLoad), true);
        }

        //button function
        async void Load(object o)
        {
            bool bLoaded = false;

            try
            {
                //CanUnload = false;
                CanLoad = false;
                CanWash = false;

                UpdateCurrentPageStatus(PostRunodel_WashStatus, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(PostRunodel_LoadStatus, PageStatusEnum.Start);
                bLoaded = await RunLoad();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load wash buffer with error: {ex.Message}");
            }
            finally
            {

                CanLoad = !bLoaded;
                //CanUnload = true;
                CanWash = bLoaded;
                UpdateCurrentPageStatus(PostRunodel_LoadStatus, bLoaded ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }


        //1. Lower the reagent Cartridges
        //2. Check whether buffer snipper was lower down manually, user have to lower down buffer wash cartridge manually also. 
        private async Task<bool> RunLoad()
        {
            bool bLoaded = false;
            bLoaded = await Task<bool>.Run(() =>
            {
                bool b = PostRunApp.LoadWash();
                return b;
            });
            return bLoaded;
        }
        #endregion


        #region Wash
        //-------------- Run Wash ----------------------------------------------------------
        private ICommand _WashCmd = null;
        public ICommand WashCmd
        {
            get
            {
                if (_WashCmd == null)
                {
                    _WashCmd = new RelayCommand(o => Wash(o), o => CanWash);
                }
                return _WashCmd;
            }
        }

        bool _CanWash = false;
        public bool CanWash
        {
            get => _CanWash;
            set => SetProperty(ref _CanWash, value, nameof(CanWash), true);
        }

        //button function
        async void Wash(object o)
        {
            bool bWashed = false;
            bool previousCanCancelPage = CanCancelPage;
            try
            {
                //PostRunModel.IsWashing = true;
                //CanUnload = false;
                //CanLoad = false;
                CanWash = false;

                CanCancelPage = true;
                UpdateCurrentPageStatus(PostRunodel_WashStatus, PageStatusEnum.Start);
                bWashed = await RunWash();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run wash with error: {ex.Message}");
            }
            finally
            {
                CanUnload = !bWashed;
                //CanLoad = false;
                //CanWash = false;
                CanCancelPage = previousCanCancelPage;
                //PostRunModel.IsWashing = false;
                //PostRunModel.IsWashDone = bWashed;
                UpdateCurrentPageStatus(PostRunodel_WashStatus, bWashed ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }



        //1. Lower the reagent Cartridges
        //2. Check whether buffer snipper was lower down manually, user have to lower down buffer wash cartridge manually also. 
        private async Task<bool> RunWash()
        {
            bool bWashed = false;
            RunWashParams parameters = new RunWashParams()
            {
                SessionId = UserModel.CurrentSessionId,
                SelectedWashOption = SelectedWashOption
            };
            bWashed = await Task<bool>.Run(() =>
            {
                bool b = PostRunApp.RunWash(parameters);
                return b;
            });
            return bWashed;
        }

        //return true if don't want  wizard to handle cancel.
        

        private async void StopWashing()
        {

            await Task<bool>.Run(() =>
            {
                return PostRunApp?.CancelWashing();
            });
        }

        bool ConfirmCancel()
        {
            bool b = false;

            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = "Washing in progress, do you want to abort it?",
                Caption = "Stop Washing",
                Image = MessageBoxImage.Question,
                Buttons = MessageBoxButton.YesNo,
                IsModal = true
            };

            if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
            {
                b = true;
            }

            return b;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AppObservableSubscriber.Unsubscribe(AppMessageObserver);
            AppMessageObserver = null;
        }
        #endregion
        #region exit button
        

        ICommand _ExitCommand;
        public ICommand ExitCommand
        {
            get
            {
                if (_ExitCommand == null)
                    _ExitCommand = new RelayCommand(
                        (o) => this.RunExitCommand(),
                        (o) => this.CanCancelPage);

                return _ExitCommand;
            }
        }

        bool _CanCancelPage = true;
        public bool CanCancelPage
        {
            get
            {
                return _CanCancelPage;
            }
            set
            {
                _CanCancelPage = value;
                RaisePropertyChanged(nameof(CanCancelPage));
                CommandManager.InvalidateRequerySuggested();
            }
        }



        void RunExitCommand()
        {
            if (ConfirmExit())
            {
                this.OnRequestClose();
            }
        }

        bool ConfirmExit()
        {

            if (PostRunModel.IsWashing)
            {
                if (ConfirmCancel())
                {
                    StopWashing();
                }


                return true;
            }
            else
            {
                bool b = false;


                MessageBoxViewModel msgVm = new MessageBoxViewModel()
                {
                    Message = "Do you want to exit Maintenance UI?",
                    Caption = "Exit Maintenance",
                    Image = MessageBoxImage.Question,
                    Buttons = MessageBoxButton.YesNo,
                    IsModal = true,
                };

                if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
                {
                    b = true;
                }
                return b;
            }

        }


       #endregion exit button

        protected  bool UpdateCurrentPageStatus(string statusName, PageStatusEnum status, string msg = "")
        {
            bool updated = false;
            PageStatusModel st = StatusList.Where(x => x.Name == statusName).FirstOrDefault();
            if (st != default(PageStatusModel))
            {
                st.Status = status;
                st.Message = msg;

                updated = true;
            }
            return updated;
        }


    }
}
