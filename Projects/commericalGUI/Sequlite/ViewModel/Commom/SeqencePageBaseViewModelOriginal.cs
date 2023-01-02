using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.UI.ViewModel
{
    public abstract class SeqencePageBaseViewModelOriginal : PageViewBaseViewModel
    {
        protected UserPageModel UserModel { get; set; }
        protected IDialogService Dialogs { get; }
        protected IDisposable AppMessageObserver { get; set; }
        protected bool StopRunByUser { get; set; }
        protected ISequence SequenceApp { get; set; }
        public SequenceStatusModel SequenceStatus { get; protected set; }

        public SeqencePageBaseViewModelOriginal(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) :
            base(seqApp, _PageNavigator, dialogs)
        {
            SequenceStatus = new SequenceStatusModel();
            Dialogs = dialogs;
        }

        internal override bool IsPageDone()
        {
            return true;
        }

        protected string CancelButtonContent { get; set; } = Strings.SeqenceWizardView_Button_Cancel_StopRun;
        public override void OnUpdateCurrentPageChanged()
        {
            if (SequenceStatus.IsSequenceDone)
            {
                PageNavigator.CanMoveToNextPage = true;
            }
            else
            {
                PageNavigator.CanMoveToNextPage = false;
                Show_WizardView_Button_Cancel = true;
                WizardView_Button_Cancel = CancelButtonContent;// Strings.SeqenceWizardView_Button_Cancel_StopRun;

                Show_WizardView_Button_MoverPrevious = false;

                if (SequenceApp == null)
                {
                    SequenceApp = GetSequenceApp();
                    SequenceStatus.SequenceInformation = SequenceApp?.SequenceInformation;

                    _SequenceOLADataProcess = new SequenceOLADataProcess(Logger,SequenceDataTypeEnum.Read1);
                    _SequenceOLADataProcess.OLADataProcessed += OnOLADataProcessed;
                    _SequenceOLADataProcess.StartOLADataProcessQueue();
                    SequenceStatus.SequenceDataFeeder = _SequenceOLADataProcess;

                }

                if (AppMessageObserver == null)
                {
                    AppMessageObserver = AppObservableSubscriber.Subscribe(SeqApp.ObservableAppMessage,
                            it => AppMessagegUpdated(it));
                }

                RunSeqence();
            }
        }

        protected virtual ISequence GetSequenceApp()
        {
            return SeqApp.CreateSequenceInterface();
        }
        protected abstract void RunSeqence();
        protected abstract void StopRun();

        void InitDataVisualization(ISequenceDataFeeder sequenceDataFeeder, SequenceInfo sequenceInformation)
        {

            DataByCycleVM = new DataByCycleViewModel(Logger, sequenceDataFeeder);
            DataByCycleVM.BuildLines();
            DataByCycleVM.SetTotalCycles(sequenceInformation.Cycles);
            int[] lanes = sequenceInformation.Lanes;
            DataByCycleVM.InitLanes(lanes);
            DataByCycleVM.MetricsDataItem2 = MetricsDataEnum.Intensity;

            DataInTableVM = new DataInTableViewModel(Logger, sequenceDataFeeder);

            DataByTileVM = new DataByTileViewModel(Logger, sequenceDataFeeder);
            DataByCycleVM.UsingDynamicRangeOnLineData = true;
            DataByTileVM.UsingDynamicRange = true;
            DataByTileVM.BuildHeatMaps(sequenceInformation.Rows, sequenceInformation.Column,
                sequenceInformation.Lanes.Length); //   4, 45, 4);
            DataByTileVM.MetricsDataItem = MetricsDataEnum.Intensity;
            DataByTileVM.InitCycles();
        }

        void AppMessagegUpdated(AppMessage appMsg)
        {
            Dispatch((Action)(() =>
            {
                AppStatus st = appMsg.MessageObject as AppStatus;
                if (st != null)
                {
                    switch (st.AppStatusType)
                    {
                        case AppStatusTypeEnum.AppSequenceStatusProgress:
                            {
                                AppSequenceStatusProgress appSequenceStatusProgress = st as AppSequenceStatusProgress;
                                if (appSequenceStatusProgress.SequenceProgressStatus == ProgressTypeEnum.Started)
                                {
                                    if (appSequenceStatusProgress.SequenceDataType == SequenceDataTypeEnum.None)
                                    {
                                        SequenceStatus.StartDateTime = DateTime.Now;
                                        InitDataVisualization(SequenceStatus.SequenceDataFeeder, SequenceApp.SequenceInformation);
                                    }
                                }
                            }
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusCycle:
                            {
                                SequenceStatus.Cyle = (st as AppSequenceStatusCycle).Cycle;
                            }
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusTime:
                            SequenceStatus.TimeElapsed = (st as AppSequenceStatusTime).TimeElapsed;
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusStep:
                            SequenceStatus.Step = (st as AppSequenceStatusStep).Step;
                            SequenceStatus.StepMessageType = (st as AppSequenceStatusStep).StepMessageType;
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusOLA:
                            AppSequenceStatusOLA olaSt = st as AppSequenceStatusOLA;
                            if (olaSt.IsOLAResultsUpdated)
                            {
                                OnOLAResultsUpdated();
                            }
                            else
                            {
                                SequenceStatus.UpdateOLAStatus(olaSt);
                                CheckSequenceCompleted();
                            }
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusImageBackup:
                            SequenceStatus.UpdateImageBackupStatus(st as AppSequenceStatusImageBackup);
                            CheckSequenceCompleted();
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusTemperature:
                            SequenceStatus.Temperature = (st as AppSequenceStatusTemperature).Temperature;
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusImage:
                            SequenceStatus.ImageSaved = (st as AppSequenceStatusImage).ImageSaved;
                            Logger.Log(SequenceStatus.ImageSaved);
                            break;
                    }
                }
            }));
        }

        protected virtual void CheckSequenceCompleted()
        {
            if (SequenceStatus.IsSequenceDone && !SequenceStatus.IsOLARunning && !SequenceStatus.IsImageBackupRunning)
            {
                Show_WizardView_Button_Cancel = false;
                if (SequenceStatus.EndDateTime == default(DateTime))
                {
                    SequenceStatus.EndDateTime = DateTime.Now;
                    
                }
                if (SequenceStatus.SequenceInformation.WorkingDir != null)
                {
                    RunPdfReport(SequenceStatus.SequenceDataFeeder, SequenceStatus,
                        Path.Combine(SequenceStatus.SequenceInformation.WorkingDir, "Report.pdf"),
                        "Generating Pdf report, please wait...", true, true);
                }
                else
                {
                    Logger.LogError("Cannot generate Pdf report since working directory is not available");
                }
            }
        }

        #region SEQUENCE_DATA_PROCESS
        SequenceOLADataProcess _SequenceOLADataProcess;
        void OnOLAResultsUpdated()
        {
            SequenceOLADataInfo seqOLAData = SequenceApp.SequenceOLAData;
            _SequenceOLADataProcess.NewOLAData(seqOLAData);
        }

        void StartSequenceOLADataProcess()
        {
            _SequenceOLADataProcess = new SequenceOLADataProcess(Logger, SequenceDataTypeEnum.Read1);
            _SequenceOLADataProcess.OLADataProcessed += OnOLADataProcessed;
            _SequenceOLADataProcess.StartOLADataProcessQueue();
        }
        void OnOLADataProcessed(object sender, EventArgs e)
        {
            FillOLAData(_SequenceOLADataProcess);
        }
        void FillOLAData(SequenceOLADataProcess sequenceOLADataProces)
        {
            List<SequenceDataTableItems> tableItems = null;
            Task.Run(() =>
            {
                if (DataByCycleVM != null)
                {
                    DataByCycleVM.WaitForLastLineDataFilled();
                    DataByCycleVM.FillLineData();
                }
                tableItems = DataInTableVM?.FillDataTbale();
                tableItems = DataInTableVM?.FillDataTbale();
                if (tableItems != null && tableItems.Count > 0)
                {
                    SequenceStatus.TotalYieldString = tableItems[tableItems.Count - 1].YieldString;
                    DataInTableVM.TotalYieldString = tableItems[tableItems.Count - 1].YieldString;
                }
            }).ContinueWith((o) =>
            {
                DataByCycleVM?.SetLineDataFilled();
                Dispatch(() =>
                {
                    //update line graphs 
                    DataByCycleVM?.UpdateLineDataDisplayOnSlectedKye();
                    //update table
                    DataInTableVM?.UpdateDataTable(tableItems);
                    //heat map update on cycle
                    DataByTileVM?.UpdateHeatMapOnCycles(_SequenceOLADataProcess.GetCurrentMaxCycle(), SequenceApp.SequenceInformation.Cycles);
                });
            });
        }
        #endregion

        #region DATA_TABLE
        DataInTableViewModel _DataInTableVM;
        public DataInTableViewModel DataInTableVM { get => _DataInTableVM; set => SetProperty(ref _DataInTableVM, value); }
        #endregion

        #region LIN_GRAPHS
        DataByCycleViewModel _DataByCycleVM;
        public DataByCycleViewModel DataByCycleVM { get => _DataByCycleVM; set => SetProperty(ref _DataByCycleVM, value); }
        #endregion //LINE_GRAPHS

        #region HEATER_MAPS
        DataByTileViewModel _DataByTileVM;
        public DataByTileViewModel DataByTileVM { get => _DataByTileVM; set => SetProperty(ref _DataByTileVM, value); }
        #endregion //HETAER_MAPS

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AppObservableSubscriber.Unsubscribe(AppMessageObserver);
            AppMessageObserver = null;
        }

        private int _currentProgress;
        public int CurrentProgress
        {
            get { return _currentProgress; }
            private set
            {
                SetProperty(ref _currentProgress, value, nameof(CurrentProgress));
            }
        }

        public override bool CanCelPage()
        {
            bool cancancel = CanCancelPage;
            try
            {
                CanCancelPage = false;
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
            catch (Exception ex)
            {
                CanCancelPage = cancancel;
                Logger.LogError($"Failed to cancel Sequence: {ex.StackTrace}");
                return false;
            }

        }

        protected abstract bool ConfirmCancel();
        protected bool ConfirmCancel(string message, string caption)
        {
            bool b = false;

            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = message, //"Sequence is running, do you want to abort it?",
                Caption = caption, //"Stop Sequence",
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
