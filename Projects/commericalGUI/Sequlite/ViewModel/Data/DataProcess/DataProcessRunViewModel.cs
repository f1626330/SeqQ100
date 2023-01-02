using Sequlite.ALF.App;
using Sequlite.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System.Threading;
using System.Windows;
using System.IO;

namespace Sequlite.UI.ViewModel
{
    public class DataProcessRunViewModel : SeqencePageBaseViewModel
    {
         DataProcessInfoModel DataProcessInfo { get; set; }
        
        public override string DisplayName => "Processing";
        string _Instruction = "Instruction for Off-line Data Processing";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }


        public DataProcessRunViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : 
            base(seqApp, _PageNavigator, dialogs)
        {
            Description = "Run Off-line Data Processing";
            UserModel = (UserPageModel)_PageNavigator.GetPageModel("UserModel");
            //_PageNavigator.AddPageModel("SequenceStatus", SequenceStatus);
            CancelButtonContent = "Cancel";
           
        }

        public override void OnUpdateCurrentPageChanged()
        {
            DataProcessInfo = (DataProcessInfoModel)PageNavigator.GetPageModel("DataProcessInfo");
            SequenceStatus.Clear();
            SequenceStatus.UserName = UserModel?.UserName;
            SequenceStatus.RunName = DataProcessInfo.ExpName;//"Data Process";
            SequenceStatus.RunDescription = $"Re-analyze sequence data for {DataProcessInfo.DataInputPath}";
            SequenceStatus.IsOLAEnabled = true;
            SeqApp.UpdateAppMessage("Start data processing");
            base.OnUpdateCurrentPageChanged();
        }
        protected override ISequence GetSequenceApp()
        {
            return DataProcessInfo.SequenceApp;
        }

        
        protected override async void RunSeqence()
        {
            
            StopRunByUser = false;
            SequenceStatus.IsSequenceDone = false;
            bool done = false;
            CanCancelPage = true;
            //Description += ": " + DataProcessInfo.ExpName;
            Description += ": " + $"{DataProcessInfo.ExpName}_{DataProcessInfo.SessionId} ({DataProcessInfo.WorkingDir})";

            await Task.Run(() =>
            {
                done = false;
                try
                {
                    Logger.Log($"Start processing for data {DataProcessInfo.DataInputPath}, output results to {DataProcessInfo.WorkingDir}");
                    var seqParams = new RunOfflineImageDataSeqenceParameters()
                    {
                        SeqInfo = DataProcessInfo.SeqInfo,
                        SessionId = DataProcessInfo.SessionId,// UserModel.GetNewSessionId(),
                        WorkingDir = DataProcessInfo.WorkingDir,//DataOutputDir,
                        ImageDataDir = Path.GetDirectoryName(DataProcessInfo.DataInputPath),
                        DataInfoFilePath = DataProcessInfo.DataInputPath,
                        ExpName = DataProcessInfo.ExpName,
                        UsingPreviousWorkingDir = DataProcessInfo.UsingPreviousWorkingDir,
                        UseSlidingWindow = DataProcessInfo.UseSlidingWindow,
                        Tiles = DataProcessInfo.SelectedTiles
                    };
                    done = SequenceApp.OfflineImageDataSequence(seqParams);
                }
                catch (Exception ex)
                {
                    done = false;
                    Logger.LogError($"Off-line data processing failed: {ex.StackTrace}");

                }
            }).ContinueWith((o) =>
            {
                SequenceStatus.IsSequenceDone = !SequenceApp.Sequencerunning;
                SequenceStatus.SequenceInformation = SequenceApp?.SequenceInformation;
                if (SequenceStatus.EndDateTime == default(DateTime))
                {
                    SequenceStatus.EndDateTime = DateTime.Now;
                }
            });
            
            if (SequenceStatus.IsSequenceDone)
            {
                PageNavigator.CanMoveToNextPage = true;
                Show_WizardView_Button_Cancel = false;
                if (done)
                {
                    if (SequenceApp.IsAbort)
                    {
                        SeqApp.UpdateAppMessage("Data processing is aborted", AppMessageTypeEnum.Warning);
                        Show_WizardView_Button_MoverPrevious = true;
                    }
                    else
                    {
                        SeqApp.UpdateAppMessage("Complete data processing");
                        if (SequenceStatus.OLAMessageType == ProgressTypeEnum.Completed)
                        {
                            WizardView_Button_MovePrevious = "Pdf Export";
                            Show_WizardView_Button_MoverPrevious = true;
                        }
                        else
                        {
                            Show_WizardView_Button_MoverPrevious = true;
                        }
                    }
                }
                else
                {
                    SeqApp.UpdateAppMessage("Data processing failed", AppMessageTypeEnum.Error);
                    Show_WizardView_Button_MoverPrevious = true;
                }
            }

            if (SequenceStatus.OLAMessageType == ProgressTypeEnum.Completed)
            {
                WizardView_Button_MovePrevious = "Pdf Export";
            }
            //to do, need pass real data type
            if (SequenceStatus.SequenceInformation.WorkingDir != null)
            {
                RunPdfReport(
                       SequenceStatus,
                        Path.Combine(SequenceStatus.SequenceInformation.WorkingDir, "Report.pdf"),
                        "Generating Pdf report, please wait...", true, true);
            }
            else
            {
                Logger.LogError("Failed to generate report since working dir is null");
            }
        }

        protected override async void StopRun()
        {
            await Task<bool>.Run(() =>
            {
                SequenceApp?.StopSequence();
            });
        }

        protected override bool ConfirmCancel()
        {
            return ConfirmCancel("Off-line data processing is running, do you want to abort it?", "Stop Off-line Data Processing");
        }

        public override bool MoveToNextPage()
        {
            Finish();
            return false;
        }

        void Finish()
        {
            PageNavigator.CanMoveToNextPage = false;

            // SequenceStatus?.SequenceDataFeeder?.StopOLADataProcessQueue();
            MultipleDataGraphVM?.StopOLADataProcessQueue();
        }

        public override bool MoveToPreviousPage()
        {
            if (SequenceStatus.OLAMessageType == ProgressTypeEnum.Completed)
            {
                ShowPdfReport(
                   SequenceStatus,
                    "",
                    $"Export to Pdf File");
                return true;
            }
            else
            {
                return base.MoveToPreviousPage();
            }
        }

        protected override void CheckSequenceCompleted()
        {
            if (SequenceStatus.IsSequenceDone && !SequenceStatus.IsOLARunning )
            {
                Show_WizardView_Button_Cancel = false;
                if (SequenceStatus.EndDateTime == default(DateTime))
                {
                    SequenceStatus.EndDateTime = DateTime.Now;
                   // RunPdfReport(SequenceStatus.SequenceDataFeeder,
                   //SequenceStatus,
                   // Path.Combine(SequenceStatus.SequenceInformation.WorkingDir, "Report.pdf"),
                   // "Generating Pdf report, please wait...", false, true);
                }
            }
        }

        
    }
}
