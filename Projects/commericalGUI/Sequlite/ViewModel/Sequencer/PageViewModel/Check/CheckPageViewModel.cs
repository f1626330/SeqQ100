using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class CheckPageViewModel : PageViewBaseViewModel
    {
        const string CheckHardwareModel_DoorClosedStatus = "0";
        const string CheckHardwareModel_FCRelatedStatus = "1";
        const string CheckHardwareModel_TemperatureStatus = "2";
        const string CheckHardwareModel_ImagingStatus = "3";
        const string CheckHardwareModel_FlowCheckStatus = "4";
        const string CheckHardwareModel_SensorStatus = "5";
        const string CheckHardwareModel_DiskSpaceStatus = "6";

        public ISystemCheck SystemCheckApp { get; }

        int[] locker = new int[0];
        public CheckHardwarePageModel CheckHardwareModel { get; }
        public UserPageModel UserModel { get; }
        public LoadPageModel LoadModel { get; }
        public RunSetupPageModel RunSetupModel { get; }
        protected IDialogService Dialogs { get; }
        IDisposable AppMessageObserver { get; set; }
       bool StopRunByUser { get; set; }
        public ObservableCollection<string> ErrorMessages { get; }
        public ObservableCollection<string> WarningMessage { get; }

        ObservableCollection<SampleLaneIndexDataInfo> _SampleSheetData;
        public ObservableCollection<SampleLaneIndexDataInfo> SampleSheetData
        {
            get => _SampleSheetData; set => SetProperty(ref _SampleSheetData, value);
        }


        bool _HasError = false;
        public bool HasError
        {
            get => _HasError;
            set
            {
                SetProperty(ref _HasError, value);
            }
        }
        string _DiskSpace;
        public string DiskSpace
        {
            get => _DiskSpace;
            set
            {
                SetProperty(ref _DiskSpace, value);
            }
        }

        string _WasteLevel;
        public string WasteLevel
        {
            get => _WasteLevel;
            set
            {
                SetProperty(ref _WasteLevel, value);
            }
        }
        //int[] _ErrorWarningLock = new int[0];
        void ClearErrors()
        {
            ErrorMessages?.Clear();
        }

        void AddError(string msg)
        {
            
            ErrorMessages.Add(msg);
           
        }

        void ClearWarnings()
        {
            WarningMessage?.Clear();
        }

        void AddWarning(string msg)
        {
            WarningMessage.Add(msg);
        }

        public CheckPageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) :
            base(seqApp, _PageNavigator, dialogs)
        {
            SystemCheckApp = seqApp.CreateSystemCheckInterface();
            SubpageCount = 2;
            
            SubpageNames = new string[] {Strings.PageDisplayName_Check_RunParams,
                Strings.PageDisplayName_Check_Hardware };
            CheckHardwareModel = new CheckHardwarePageModel() ;
            _PageNavigator.AddPageModel(SequencePageTypeEnum.Check, CheckHardwareModel);
            UserModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.User) as UserPageModel;
            LoadModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.Load) as LoadPageModel;
            RunSetupModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.RunSetup) as RunSetupPageModel;
            Dialogs = dialogs;
            ErrorMessages = new ObservableCollection<string>();
            WarningMessage = new ObservableCollection<string>();
            BindingOperations.EnableCollectionSynchronization(ErrorMessages, locker);
            BindingOperations.EnableCollectionSynchronization(WarningMessage, locker);

            CurrentSubpageIndex = 0;
        }

        void AppMessagegUpdated(AppMessage appMsg)
        {
            Dispatch(() =>
            {
                lock (locker)
                {
                    switch (appMsg.MessageType)
                    {
                        case AppMessageTypeEnum.Error:
                        case AppMessageTypeEnum.ErrorNotification:
                            AddError(appMsg.Message);
                            break;
                        case AppMessageTypeEnum.Warning:
                            AddWarning(appMsg.Message);
                            break;
                        case AppMessageTypeEnum.Normal:
                            {
                                if (appMsg.MessageObject is CheckHardwareProgressMessage)
                                {
                                    CheckHardwareProgressMessage msg = appMsg.MessageObject as CheckHardwareProgressMessage;
                                    CurrentProgress = msg.ProgressPercentage;
                                    switch (msg.CheckType)
                                    {
                                        case CheckHardwareEnum.Disk:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DiskSpaceStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DiskSpaceStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DiskSpaceStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                        case CheckHardwareEnum.Door:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DoorClosedStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DoorClosedStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_DoorClosedStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                        case CheckHardwareEnum.Flow:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FlowCheckStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FlowCheckStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FlowCheckStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                        case CheckHardwareEnum.Fluidics:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FCRelatedStatus, PageStatusEnum.Start);
                                                    UpdateCurrentPageStatus(CheckHardwareModel_SensorStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FCRelatedStatus, PageStatusEnum.Complted_Success);
                                                    UpdateCurrentPageStatus(CheckHardwareModel_SensorStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_FCRelatedStatus, PageStatusEnum.Complted_Error);
                                                    UpdateCurrentPageStatus(CheckHardwareModel_SensorStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                        case CheckHardwareEnum.Imaging:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_ImagingStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_ImagingStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_ImagingStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                        case CheckHardwareEnum.Sensor: // handled by Fluidics
                                            break;
                                        case CheckHardwareEnum.Temperature:
                                            switch (msg.MessageType)
                                            {
                                                case CheckHardwareProgressMessageEnum.Start:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_TemperatureStatus, PageStatusEnum.Start);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.Progress:
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_TemperatureStatus, PageStatusEnum.Complted_Success);
                                                    break;
                                                case CheckHardwareProgressMessageEnum.End_Error:
                                                    UpdateCurrentPageStatus(CheckHardwareModel_TemperatureStatus, PageStatusEnum.Complted_Error);
                                                    break;
                                            }
                                            break;
                                    }

                                }
                            }
                            break;
                    }
                } //lock
            });
        }

        protected  override  void OnUpdateSubpageInderChanged(int subpageIndex)
        {
            if (CurrentSubpageIndex == 0)
            {
                Show_WizardView_Button_MoverNext = true;
                Show_WizardView_Button_Cancel = true;
                Show_WizardView_Button_MoverPrevious = true;
                WizardView_Button_MoveNext = Strings.SequenceWizardView_Button_MoveNext_Confirm;
                AppObservableSubscriber.Unsubscribe(AppMessageObserver);
                AppMessageObserver = null;
               
            }
            else if (CurrentSubpageIndex == 1)
            {
                if (AppMessageObserver == null)
                {
                    ClearErrors();
                    ClearWarnings();
                    HasError = false;
                    AppMessageObserver = AppObservableSubscriber.Subscribe(SeqApp.ObservableAppMessage,
                            it => AppMessagegUpdated(it));
                }
                Show_WizardView_Button_MoverNext = false;
                Show_WizardView_Button_Cancel = true;
                Show_WizardView_Button_MoverPrevious = false;
                if (StatusList == null)
                {
                    ObservableCollection<PageStatusModel> ls = new ObservableCollection<PageStatusModel>()
                    {
                         new PageStatusModel() {Name = CheckHardwareModel_DoorClosedStatus, DisaplyName="Doors Closed"  },
                         new PageStatusModel() {Name = CheckHardwareModel_FCRelatedStatus,  DisaplyName="FC, Reagent Cartridge, Buffer, Waste"},
                         new PageStatusModel() {Name = CheckHardwareModel_TemperatureStatus,   DisaplyName="Temperature"},
                         new PageStatusModel() {Name = CheckHardwareModel_ImagingStatus,   DisaplyName="Imaging (Cameras, LED)"},
                         new PageStatusModel() {Name = CheckHardwareModel_FlowCheckStatus,   DisaplyName="Flow Check"},
                         new PageStatusModel() {Name = CheckHardwareModel_SensorStatus ,  DisaplyName="Sensors"},
                         new PageStatusModel() {Name = CheckHardwareModel_DiskSpaceStatus,   DisaplyName="Disk Space"}
                    };
                    StatusList = ls;
                }

                RunCheckHardware();
            }
        }

        public override void OnUpdateCurrentPageChanged()
        {
            if (CurrentSubpageIndex == 0)
            {
                if (RunSetupModel?.UseSampleSheet == true && RunSetupModel.SampleSheetData != null)
                {

                    SampleSheetData = new ObservableCollection<SampleLaneIndexDataInfo>(RunSetupModel.SampleSheetData.SampleLaneDataInfos);
                }
            }
        }
        private bool RunChecks(string it)
        {
            bool b = true;

            try
            {
                switch (it)
                {
                    case CheckHardwareModel_DoorClosedStatus:
                        b = SystemCheckApp.DoorCheck();
                        break;
                    case CheckHardwareModel_FCRelatedStatus:
                        b = SystemCheckApp.FluidicsCheck();
                        break;
                    case CheckHardwareModel_TemperatureStatus:
                        b = SystemCheckApp.TemperatureCheck();
                        break;
                    case CheckHardwareModel_ImagingStatus:
                        b = SystemCheckApp.ImageSystemCheck();
                        break;
                    case CheckHardwareModel_FlowCheckStatus:
                        b = SystemCheckApp.FlowCheckAndPriming();
                        break;
                    case CheckHardwareModel_SensorStatus:
                        //it is part of Fluidics check so do nothing
                        break;
                    case CheckHardwareModel_DiskSpaceStatus:
                        int totalLength = (RunSetupModel.EnableRead2 ? 2 * RunSetupModel.Read1Value : RunSetupModel.Read1Value) + RunSetupModel.Index1Value + RunSetupModel.Index2Value;
                        b = SystemCheckApp.DiskSpaceCheck(totalLength);
                        break;
                }
                if (!b)
                {
                    Logger.LogError($"Failed to check {it}.");
                    
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check {it} with error: {ex.Message}");
            }

            return b;
        }

        
        //return true if don't wizard to handle move to next.
        public override bool MoveToNextPage()
        {
            if (CurrentSubpageIndex == 1 && Show_WizardView_Button_MoverNext)
            {
                // run checked again
                Show_WizardView_Button_MoverPrevious = false;
                Show_WizardView_Button_MoverNext = false;
                RunCheckHardware();
                return true;
            }
            else
            {
                AppObservableSubscriber.Unsubscribe(AppMessageObserver);
                AppMessageObserver = null;
                return base.MoveToNextPage();
            }
        }

        private async void RunCheckHardware()
        {
            StopRunByUser = false;
            ClearErrors();
            ClearWarnings();
            HasError = false;
            CurrentProgress = 0;
            List<string[]> taskNameList = new List<string[]>()
            {
                new string[]
                {
                        CheckHardwareModel_DoorClosedStatus  ,
                        CheckHardwareModel_FCRelatedStatus , //fluidic check
                        CheckHardwareModel_DiskSpaceStatus,
                        CheckHardwareModel_SensorStatus  ,//do nothing since it is part of fluidic check
                },
                new string[]
                {
                    CheckHardwareModel_TemperatureStatus,
                    CheckHardwareModel_FlowCheckStatus  , //flow check
                },
                new string[]
                {
                    CheckHardwareModel_ImagingStatus  ,
                },
           };

            SystemCheckApp.IsAbortCheck = false;
            SystemCheckApp.SessionId = UserModel.CurrentSessionId;
            bool b = false;
            foreach (var list in taskNameList)
            {
                List<Task<bool>> taskList = new List<Task<bool>>();
                //run each item (it) as a list of parallel tasks
                b = true;
                for (int k = 0; k < list.Length; k++)
                {
                    
                    string it = list[k];
                    var task = new Task<bool>(() =>
                    {
                        return RunChecks(it);
                       
                    });

                    task.Start();
                    taskList.Add(task);
                }//for each
                bool[] bbs = await Task<bool>.WhenAll(taskList.ToArray());
                b = bbs.All(x => x == true);
                if (!b)
                {
                    break;
                }
            } //for each
            CheckHardwareModel.HardwareCheckData = SystemCheckApp.HardwareCheckResults;
            DiskSpace = string.Format("{0} / {1}", CheckHardwareModel.HardwareCheckData.DiskSpaceReq, CheckHardwareModel.HardwareCheckData.DiskSpaceEmp);
            WasteLevel = string.Format("Current weight:{0} / Expect weight:{1}", CheckHardwareModel.HardwareCheckData.WasteLevel, SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WasteCartridgeEmptyWeight);
            if (b && !StopRunByUser)
            {
                await Task.Delay(1000).ContinueWith(_ =>
                {
                    lock (locker)
                    {
                        CurrentProgress = 100;
                    }
                    Thread.Sleep(4000);
                });
               
                PageNavigator.MoveToNextPage();
            }
            else
            {
                HasError = ErrorMessages.Count > 0;
                Show_WizardView_Button_MoverPrevious = true;
                Show_WizardView_Button_MoverNext = true;
                WizardView_Button_MoveNext = Strings.SequenceWizardView_Button_MoveNext_RunCheck;
            }
        }

        public override string DisplayName => Strings.PageDisplayName_Check;

        internal override bool IsPageDone()
        {
            return true;
        }

        private string _Instruction = Instructions.CheckPage_Instruction;



        public override string Instruction
        {
            get
            {
                return HtmlDecorator.CSS1 + _Instruction;
            }
            protected set
            {
                _Instruction = value;
                RaisePropertyChanged(nameof(Instruction));
            }
        }

        string[] subPageDesps = new string[]
            {
                Descriptions.CheckPage_Description_0,
               Descriptions.CheckPage_Description_1,
               
            };
        protected override void SetSubPageDiscription(int pageIndex)
        {
            Description = subPageDesps[pageIndex];
        }

        private float _currentProgress;
        public float CurrentProgress
        {
            get { return _currentProgress; }
            private set
            {
                SetProperty(ref _currentProgress, value, nameof(CurrentProgress));
            }
        }

        protected override bool UpdateCurrentPageStatus(string statusName, PageStatusEnum status, string msg = "")
        {
            lock (locker)
            {
                return base.UpdateCurrentPageStatus(statusName, status, msg);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AppObservableSubscriber.Unsubscribe(AppMessageObserver);
            AppMessageObserver = null;
        }
        private async void StopRun()
        {

            await Task<bool>.Run(() =>
            {
                SystemCheckApp.CancelChecking();
            });
        }

        public override bool CanCelPage()
        {
            if (StopRunByUser)
            {
                return false;
            }
            else
            {
                if (ConfirmCancel())
                {
                    StopRunByUser = true;
                    StopRun();

                }
                return true;
            }
        }

        bool ConfirmCancel()
        {
            bool b = false;

            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = "Hardware check is in progress, do you want to abort it?",
                Caption = "Stop Check",
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
    }
}
