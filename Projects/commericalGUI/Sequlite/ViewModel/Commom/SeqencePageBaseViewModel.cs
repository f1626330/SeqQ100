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
    public abstract class SeqencePageBaseViewModel : PageViewBaseViewModel
    {
        protected UserPageModel UserModel { get; set; }
        protected IDialogService Dialogs { get; }
        protected IDisposable AppMessageObserver { get; set; }
        protected bool StopRunByUser { get; set; }
        protected ISequence SequenceApp { get; set; }
        public SequenceStatusModel SequenceStatus { get; protected set; }
        MultipleDataGraphViewModel _MultipleDataGraphVM;
        public MultipleDataGraphViewModel MultipleDataGraphVM { get => _MultipleDataGraphVM; set => SetProperty(ref _MultipleDataGraphVM, value); }
       
        
        bool _ReportGenerated = false;
        public SeqencePageBaseViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator, IDialogService dialogs) :
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
                WizardView_Button_Cancel = CancelButtonContent;

                Show_WizardView_Button_MoverPrevious = false;

                if (SequenceApp == null)
                {
                    SequenceApp = GetSequenceApp();
                    SequenceStatus.SequenceInformation = SequenceApp?.SequenceInformation;
                    
                    if (SequenceStatus?.IsOLAEnabled == true)
                    {
                        MultipleDataGraphVM = new MultipleDataGraphViewModel(Logger, SequenceStatus, SequenceApp.SequenceInformation);
                    }
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
                                ProgressTypeEnum status = appSequenceStatusProgress.SequenceProgressStatus;
                                if (status == ProgressTypeEnum.Started)
                                {
                                    if (appSequenceStatusProgress.SequenceDataType == SequenceDataTypeEnum.None)
                                    {
                                        SequenceStatus.StartDateTime = DateTime.Now;

                                    }
                                    else
                                    {
                                        ISequenceDataFeeder sequenceDataFeeder = SequenceStatus.CreateSequenceDataFeeder(appSequenceStatusProgress.SequenceDataType, Logger);
                                        MultipleDataGraphVM?.AddDataGraph(appSequenceStatusProgress.SequenceDataType, SequenceApp.SequenceInformation, sequenceDataFeeder);
                                    }
                                }
                                else if (status == ProgressTypeEnum.Completed ||
                                status == ProgressTypeEnum.Aborted ||
                                    status == ProgressTypeEnum.Failed)
                                {
                                    CheckSequenceCompleted();
                                }
                            }
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusCycle:
                            {
                                SequenceStatus.Cyle = (st as AppSequenceStatusCycle).Cycle;
                                SequenceStatus.ImagingSequenceRead = (st as AppSequenceStatusCycle).StepSequenceRead;
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
                                
                                MultipleDataGraphVM?.OnOLAResultsUpdated(SequenceStatus.SequenceDataFeeder(olaSt.SequenceDataType), 
                                    SequenceApp.SequenceOLAData(olaSt.SequenceDataType));
                            }
                            else
                            {
                                
                                SequenceStatus.UpdateOLAStatus(olaSt);
                                CheckSequenceCompleted();
                            }
                            break;
                        case AppStatusTypeEnum.AppSequenceStatusDataBackup:
                            SequenceStatus.UpdateDataBackupStatus(st as AppSequenceStatusDataBackup);
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
            SequenceStatus.IsSequenceDone = !SequenceApp.Sequencerunning;
            SequenceStatus.IsOLARunning = !SequenceApp.IsOLADone;
            if (SequenceStatus.IsSequenceDone && !SequenceStatus.IsOLARunning )
            {
                if (!SequenceStatus.IsDataBackupRunning)
                {
                    Show_WizardView_Button_Cancel = false;
                    //if (SequenceStatus.EndDateTime == default(DateTime))
                    //{
                    //    SequenceStatus.EndDateTime = DateTime.Now;

                    //}
                }
                if (SequenceStatus.EndDateTime == default(DateTime))
                {
                    SequenceStatus.EndDateTime = DateTime.Now;

                }
                if (!_ReportGenerated)
                {
                    if (SequenceStatus.SequenceInformation.WorkingDir != null)
                    {
                        _ReportGenerated = true;
                        string reportFullPathName = Path.Combine(SequenceStatus.SequenceInformation.WorkingDir, "Report.pdf");
                        RunPdfReport(SequenceStatus, reportFullPathName,
                            "Generating Pdf report, please wait...", true, true);
                       
                        SeqApp.SendMessageToApp(new AppSequenceStatusSequenceReport() { ReportSaved = reportFullPathName },
                          AppMessageTypeEnum.Status);
                    }
                    else
                    {
                        Logger.LogError("Cannot generate Pdf report since working directory is not available");
                    }
                }
            }
        }

    

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
            bool b = false;
            try
            {
                CanCancelPage = false;
                if (StopRunByUser)
                {
                    b= false;
                }
                else
                {
                    if (ConfirmCancel())
                    {
                        StopRunByUser = true;
                        StopRun();

                    }
                    else
                    {
                        CanCancelPage = cancancel;
                    }
                    b= true;
                }
            }
            catch (Exception ex)
            {
                CanCancelPage = cancancel;
                Logger.LogError($"Failed to cancel Sequence: {ex.StackTrace}");
                b= false;
            }
           
            return b;
        }

        protected abstract bool ConfirmCancel();
        protected bool ConfirmCancel(string message, string caption)
        {
            bool b = false;

            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = message, 
                Caption = caption, 
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
