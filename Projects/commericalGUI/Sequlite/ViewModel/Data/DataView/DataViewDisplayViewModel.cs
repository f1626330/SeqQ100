using Sequlite.ALF.App;
using Sequlite.UI.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{

    public class DataViewDisplayViewModel : DataViewDefaultViewModel
    {
        ISequence SequenceApp { get; set; }
        
        IDisposable AppMessageObserver { get; set; }
        public SequenceStatusModel SequenceStatus { get; }
        DataInfoFileModel DataInfoFile { get; set; }
       
        MultipleDataGraphViewModel _MultipleDataGraphVM;
        public MultipleDataGraphViewModel MultipleDataGraphVM { get => _MultipleDataGraphVM; set => SetProperty(ref _MultipleDataGraphVM, value); }
        public DataViewDisplayViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            Description = "Data Graphic Views";
            SequenceStatus = new SequenceStatusModel();
           
        }

        public override string DisplayName => "Data View";
        string _ProcessStatus;
        public string ProcessStatus { get => _ProcessStatus; set => SetProperty(ref _ProcessStatus, value); }
       
        public override void OnUpdateCurrentPageChanged()
        {
            
            CanCancelPage = false;
            Show_WizardView_Button_MoverNext = false;
            Show_WizardView_Button_Cancel = true;
           
            Show_WizardView_Button_MoverPrevious = false;
            
            SeqApp.UpdateAppMessage("Start processing data for display");
            DataInfoFileModel vd = (DataInfoFileModel) PageNavigator.GetPageModel("DataInfoFileName");
            DataInfoFile = vd;
            string dInfoFile = vd.DataInfoFileName;

            UserPageModel userModel = (UserPageModel)PageNavigator.GetPageModel("UserModel");
            SequenceStatus.UserName = userModel?.UserName;
            SequenceStatus.RunName = "Data View";
            SequenceStatus.RunDescription = $"Display sequence data for {DataInfoFile.DataInfoFileName}";
           
            if (SequenceApp == null)
            {
                SequenceApp = SeqApp.CreateSequenceInterface(false); //don't need hardware
                SequenceStatus.SequenceInformation = SequenceApp.SequenceInformation;
                MultipleDataGraphVM = new MultipleDataGraphViewModel(Logger, SequenceStatus, SequenceApp.SequenceInformation);
                MultipleDataGraphVM.OnDataProcessStopped += MultipleDataGraphVM_OnDataProcessStopped;
            }
            
           
            if (AppMessageObserver == null)
            {
                AppMessageObserver = AppObservableSubscriber.Subscribe(SeqApp.ObservableAppMessage,
                        it => AppMessagegUpdated(it));
            }
            
            RunSeqence(dInfoFile);
            
        }

        private void MultipleDataGraphVM_OnDataProcessStopped(object sender, EventArgs e)
        {
            if (SequenceStatus.IsSequenceDone)
            {
                bool updated = true;
                switch (SequenceStatus.OLAMessageType)
                {
                    case ProgressTypeEnum.Completed:
                        SequenceStatus.OLAMessage = "Processing data for display completed";
                        break;
                    case ProgressTypeEnum.Aborted:
                        SequenceStatus.OLAMessage = "Processing data for display is aborted";
                        break;
                    case ProgressTypeEnum.Failed:
                        SequenceStatus.OLAMessage = "Processing data for display failed";
                        break;
                    default:
                        updated = false;// SequenceStatus.OLAMessage = "Finishing processing data for display";
                        break;
                }
                if (updated)
                {
                    SeqApp.UpdateAppMessage(SequenceStatus.OLAMessage);// "Complete data processing for display");
                }

            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (MultipleDataGraphVM != null)
            {
                MultipleDataGraphVM.OnDataProcessStopped -= MultipleDataGraphVM_OnDataProcessStopped;
            }
            AppObservableSubscriber.Unsubscribe(AppMessageObserver);
            AppMessageObserver = null;
            
        }

        private async void RunSeqence(string dInfoFile)
        {
            SequenceStatus.OLAMessage = "Processing data for display, please wait...";
            SequenceStatus.OLAMessageType = ProgressTypeEnum.InProgress;
            SequenceStatus.IsOLARunning = true;
            SequenceStatus.IsSequenceDone = false;
            await Task<bool>.Run(() =>
            {
                try
                {
                    Logger.Log("Start to run off-line Data processing for data display");
                    bool b=   SequenceApp.OfflineDataDisplaySequence(
                        new RunOfflineDataSeqenceParameters()
                        {
                            DataInfoFile = dInfoFile
                        });

                    SequenceStatus.IsOLARunning = !SequenceApp.IsOLADone;
                    while (SequenceStatus.IsOLARunning || MultipleDataGraphVM?.HasItemInQ() == true) // SequenceStatus?.SequenceDataFeeder?.HasItemInQ() == true)
                    {
                        SequenceStatus.IsOLARunning = !SequenceApp.IsOLADone;
                        Thread.Sleep(1000);
                    }
                    return b;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"off-line processing failed: {ex.StackTrace}");
                    return false;
                }
            }).ContinueWith( (o) =>
            {
               // await Task.Delay(2000);
                Dispatch(() =>
                {
                    CanCancelPage = false; 
                    Show_WizardView_Button_Cancel = false;
                    SequenceStatus.IsSequenceDone = !SequenceApp.Sequencerunning;
                    Logger.Log("End running off-line data processing for data display");
                    if (SequenceStatus.IsSequenceDone)
                    {
                        if (o.Result)
                        {
                            switch (SequenceStatus.OLAMessageType)
                            {
                                case ProgressTypeEnum.Completed:
                                    SequenceStatus.OLAMessage = "Completing processing data for display";
                                    break;
                                case ProgressTypeEnum.Aborted:
                                    SequenceStatus.OLAMessage = "Aborting processing data for display";
                                    break;
                                case ProgressTypeEnum.Failed:
                                    SequenceStatus.OLAMessage = "Failure in process data for display";
                                    break;
                                default:
                                    SequenceStatus.OLAMessage = "Finishing processing data for display";
                                    break;
                            }
                            SeqApp.UpdateAppMessage("Completing data processing for display");
                            
                        }
                        else
                        {
                            SequenceStatus.OLAMessage = "Failed to process data for display";
                            SequenceStatus.OLAMessageType = ProgressTypeEnum.Failed;
                            SeqApp.UpdateAppMessage("Failed to process data for display", AppMessageTypeEnum.Error);
                            
                        }
                        
                        Show_WizardView_Button_MoverNext = true;
                        Show_WizardView_Button_MoverPrevious = true;
                        SequenceStatus.EndDateTime = DateTime.Now;
                        SequenceStatus.SequenceInformation = SequenceApp.SequenceInformation;
                        MultipleDataGraphVM?.StopOLADataProcessQueue();
                        
                    }

                    if (SequenceStatus.OLAMessageType == ProgressTypeEnum.Completed)
                    {
                        WizardView_Button_MovePrevious = "Pdf Export";
                        //RunPdfReport(_SequenceOLADataProcess, SequenceStatus,
                        //    Path.Combine(SequenceStatus.SequenceInformation.WorkingDir, "Report.pdf"),
                        //    "Generating Pdf report, please wait...", true, true);
                    }
                }); //dispatch
            });
        }

        void AppMessagegUpdated(AppMessage appMsg)
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
                                Dispatch(() =>
                                {
                                    if (appSequenceStatusProgress.SequenceDataType == SequenceDataTypeEnum.None)
                                    {
                                        SequenceStatus.StartDateTime = DateTime.Now;
                                        SequenceStatus.SequenceInformation = SequenceApp.SequenceInformation;

                                        CanCancelPage = true;
                                    }
                                    else
                                    {
                                        ISequenceDataFeeder sequenceOLADataProcess =  SequenceStatus.CreateSequenceDataFeeder(appSequenceStatusProgress.SequenceDataType, Logger);
                                        MultipleDataGraphVM?.AddDataGraph(appSequenceStatusProgress.SequenceDataType, SequenceApp.SequenceInformation, sequenceOLADataProcess);
                                    }
                                    
                                    
                                });

                               
                            }
                        }
                        break;
                    case AppStatusTypeEnum.AppSequenceStatusOLA:
                        {
                            AppSequenceStatusOLA olaSt = st as AppSequenceStatusOLA;

                            
                            if (olaSt.IsOLAResultsUpdated)
                            {
                                
                                MultipleDataGraphVM?.OnOLAResultsUpdated(SequenceStatus.SequenceDataFeeder(olaSt.SequenceDataType), 
                                    SequenceApp.SequenceOLAData(olaSt.SequenceDataType));
                            }
                            else
                            {
                                Dispatch(() =>
                                {
                                    SequenceStatus.UpdateOLAStatus(olaSt, false);
                                    
                                });
                            }

                            if (!string.IsNullOrEmpty(olaSt.Message))
                            {
                                AppMessageTypeEnum appMsgType = AppMessageTypeEnum.Normal;
                                switch (olaSt.OLAStatus)
                                {
                                    case ProgressTypeEnum.InProgress:
                                    case ProgressTypeEnum.Completed:
                                    case ProgressTypeEnum.Started:
                                        appMsgType = AppMessageTypeEnum.Normal;
                                        break;
                                    case ProgressTypeEnum.InProgressWithWarning:
                                    case ProgressTypeEnum.Aborted:
                                        appMsgType = AppMessageTypeEnum.Warning;
                                        break;
                                    case ProgressTypeEnum.Failed:
                                        appMsgType = AppMessageTypeEnum.Error;
                                        break;
                                }

                                SeqApp.UpdateAppMessage(olaSt.Message, appMsgType);
                            }
                        }
                        break;
                }
            }
        }

    
        internal override bool IsPageDone()
        {
            return true; 
        }

        //return true if don't want wizard to handle move to next.
        public override bool MoveToNextPage()
        {
            Finish();
            return false;
        }

         void Finish()
        {
            //PageNavigator.CanMoveToNextPage = false;
            //TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            //Task.Factory.StartNew(() =>
            //{
            //    MultipleDataGraphVM?.StopOLADataProcessQueue();
            //}, CancellationToken.None, TaskCreationOptions.None, uiScheduler);

            PageNavigator.CanMoveToNextPage = false;
            //SequenceStatus?.SequenceDataFeeder?.StopOLADataProcessQueue();
            MultipleDataGraphVM?.StopOLADataProcessQueue();
        }

        public override bool CanCelPage()
        {
            Cancel();
           
            return true;
        }

        async void Cancel()
        {
            if (SequenceStatus.IsOLARunning)
            {
                CanCancelPage = false;
                await Task.Run( () =>  SequenceApp.StopSequence());
            }
        }

        //Use it  as pdf report
        public override bool MoveToPreviousPage()
        {
            if (SequenceStatus.OLAMessageType == ProgressTypeEnum.Completed)
            {

                
                ShowPdfReport(
                    SequenceStatus,
                    "",
                    $"Export to Pdf file");
                return true;
            }
            else
            {
                MultipleDataGraphVM?.CleanAll();
                SequenceApp = null;
                return base.MoveToPreviousPage();
            }
        }

        
    }
}
