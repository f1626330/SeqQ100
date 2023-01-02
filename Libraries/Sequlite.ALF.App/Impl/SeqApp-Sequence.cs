using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.SerialPeripherals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;
using System.Windows.Threading;

namespace Sequlite.ALF.App
{
    partial class SeqAppSequence : ISequence
    {
        class UsageMonitor : IDisposable
        {
            private System.Timers.Timer _UsageTimer;
            private static PerformanceCounter _RAMCounter;
            private static PerformanceCounter _CPUCounter;

            public UsageMonitor(IAppMessage seqApp)
            {
                SeqApp = seqApp;
            }

            private IAppMessage SeqApp { get; }
            private static void CreatePerformanceCounters()
            {
                string counterName1 = "% Committed Bytes In Use";
                string categoryName1 = "Memory";
                if (_RAMCounter == null && PerformanceCounterCategory.CounterExists(counterName1, categoryName1))
                {
                    _RAMCounter = new PerformanceCounter(categoryName1, counterName1);
                }

                string counterName2 = "% Processor Time";
                string categoryName2 = "Processor";
                if (_CPUCounter == null && PerformanceCounterCategory.CounterExists(counterName2, categoryName2))
                {
                    _CPUCounter = new PerformanceCounter(categoryName2, counterName2, "_Total");
                }
            }

            public void StartMonitoring()
            {
                CreatePerformanceCounters();
                _UsageTimer = new System.Timers.Timer(60 * 1000);
                _UsageTimer.AutoReset = true;
                _UsageTimer.Elapsed += _UsageTimer_Elapsed;
                _UsageTimer.Start();
            }
            public void StopMonitoring()
            {
                _UsageTimer?.Stop();
            }

            private void _UsageTimer_Elapsed(object sender, ElapsedEventArgs e)
            {
                try
                {
                    if (_CPUCounter != null && _RAMCounter != null)
                    {
                        SeqApp.UpdateAppMessage(string.Format("CPU:{0}% Memory:{1:F2}%", (int)_CPUCounter.NextValue(), _RAMCounter.NextValue()));
                    }
                }
                catch (Exception ex)
                {
                    SeqApp.UpdateAppMessage(string.Format("Exception error in usage updating: " + ex.Message), AppMessageTypeEnum.ErrorNotification);
                }
            }

            public void Dispose()
            {
                StopMonitoring();
                _UsageTimer?.Dispose();
            }
        }

        RecipeBuildSettings _RecipeBuildSettings;
        RecipeRunThreadV2 _RecipeRunThreadV2_sequnce;
        ThreadBase.ThreadExitStat _ExitState = ThreadBase.ThreadExitStat.None;
        OLAJobManager OLAJobs { get; set; }
        
        SequenceDataInfoHelper _SequenceDataInfoHelper;
        SequenceDataBackup SequenceDataBackingup { get; set; }
        public bool Sequencerunning { get; private set; }

        public bool IsAbort { get; set; }
        public string PostWashingRecipeFile { get; set; }
        public SeqApp SeqApp { get; }
        bool NeedWaitForOLACompletedInSideRecipe { get; set; } = false;
        bool NeedWaitForBackupCompleteInSideRecipe { get; set; } = true;
        public bool IsOLADone { get; set; }
        public bool IsSequenceDataBackupDone { get; private set; }
        public SequenceInfo SequenceInformation { get; private set; }

        UsageMonitor _UsageMonitor;
        private string _SubscribedEmail;
        private Dictionary<ImagingStep.SequenceRead, bool> OLABackupHistory;
        private static object _EmailLock = new object();
        OLAWorkingDirInfo _OLAWorkingDirInfo;
        IDisposable _IncomingAppMessageObserverForDataBackup = null;
        public SeqAppSequence(SeqApp seqApp)
        {
            SeqApp = seqApp;
            SequenceInformation = new SequenceInfo()
            {
                Channels = 4,
                Cycles = 0,
                Lanes = null,
                Template = TemplateOptions.ecoli,
                InstrumentID = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName,
                Instrument = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName, //to do shall read it from calib.json?
            };
        }

        public bool Sequence(RunSeqenceParameters seqParams)
        {
            _IncomingAppMessageObserverForDataBackup = null;
            try
            {
                
                _SubscribedEmail = seqParams.UserEmail;
                bool isSimulation = seqParams.IsSimulation;

                if (Sequencerunning)
                {
                    SeqApp.NotifyNormalError("Cannot start a new sequence while another sequence is still running.");
                    return false;
                }

                Sequencerunning = true;
                if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.BackupOLAData)
                {
                    OLABackupHistory = new Dictionary<ImagingStep.SequenceRead, bool>();
                }
                _SequenceDataInfoHelper = new SequenceDataInfoHelper(this.SeqApp);
                _RecipeBuildSettings = SettingsManager.ConfigSettings.SystemConfig.RecipeBuildConfig;
                Recipe finalrecipe = BuildRecipe(seqParams);

                SequenceInformation.Cycles = seqParams.Readlength * (seqParams.Paired ? 2 : 1);
                List<int> laneList = new List<int>();
                for (int i = 1; i <= SettingsManager.ConfigSettings.FCLane; i++)
                {
                    laneList.Add(i);
                }
                SequenceInformation.Lanes = laneList.ToArray();
                SequenceInformation.Rows = SettingsManager.ConfigSettings.FCRow;
                SequenceInformation.Column = SettingsManager.ConfigSettings.FCColumn;

                SequenceInformation.SampleID = seqParams.SampleID;
                SequenceInformation.SampleID = seqParams.SampleID;
                SequenceInformation.FlowCellID = seqParams.FlowCellID;
                SequenceInformation.ReagentID = seqParams.ReagentID;

                SequenceInformation.Paired = seqParams.Paired;
                SequenceInformation.Index1Enabled = seqParams.Index1Enable;
                SequenceInformation.Index2Enabled = seqParams.Index2Enable;
                SequenceInformation.Index1Cycle = seqParams.Index1Number;
                SequenceInformation.Index2Cycle = seqParams.Index2Number;

                List<OLALaneSampleIndexInfo> olaLaneSampleIndexInfo = PrepareOLALaneSampleIndexInfo(seqParams.SampleSheetData);

                string error;
                if (olaLaneSampleIndexInfo != null && !ValidateOLALaneSampleIndexInfo(olaLaneSampleIndexInfo, out error))
                {
                    SeqApp.NotifyNormalError($"Sample sheet {seqParams.SampleSheetData} is invalid. {error}");
                    return false;
                }

                OLASequenceInfo olaSeqInfo = new OLASequenceInfo(seqParams.ExpName,
                                                                 seqParams.SessionId,
                                                                 seqParams.Paired,
                                                                 SequenceInformation.Cycles,
                                                                 SequenceInformation.IndexCycles,
                                                                 SequenceInformation.Lanes,
                                                                 SequenceInformation.Rows,
                                                                 SequenceInformation.Column,
                                                                 seqParams.SeqTemp,
                                                                 seqParams.SeqIndexTemp,
                                                                 olaLaneSampleIndexInfo
                                                                 )
                {
                    SampleID = SequenceInformation.SampleID,
                    FlowCellID = SequenceInformation.FlowCellID,
                    ReagentID = SequenceInformation.ReagentID,
                    Instrument = SequenceInformation.Instrument,
                    InstrumentID = SequenceInformation.InstrumentID,

                };


                OLAJobManager.SerializeSequenceInfo(olaSeqInfo, SeqApp.Logger);

                if (seqParams.IsEnableOLA || seqParams.IsEnablePP)
                {
                    OLAJobs = new OLAJobManager(true /*V2 recipe*/, olaSeqInfo);
                    IsOLADone = false;
                }
                else
                {
                    OLAJobs = null;
                    IsOLADone = true;
                }

                
                IsSequenceDataBackupDone = false;
                RecipeThreadParameters _RecipeParam = new RecipeThreadParameters()
                {
                    Bottom_Offset = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomOffset,
                    Top_Offset = SettingsManager.ConfigSettings.AutoFocusingSettings.TopOffset,
                    SelectedTemplate = seqParams.SeqTemp,
                    SelectedIndTemplate = seqParams.SeqIndexTemp,
                    IsSimulation = isSimulation,
                    IsEnableOLA = seqParams.IsEnableOLA,
                    IsEnablePP = seqParams.IsEnablePP,
                    IsBC = true,
                    OneRef = _RecipeBuildSettings.UsingOneRef,//true,
                    GLEDinc = 1,
                    RLEDinc = 1,
                    Expoinc = 1.014,
                    StartInc = 2,
                    LoadCartridge = false,
                    UserEmail = seqParams.UserEmail,
                    BackUpData = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.BackupImage,
                    IsIndex = SequenceInformation.Index1Enabled = seqParams.Index1Enable,
                    IsCalculateOffset = true
                };

                if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.BackupData)
                {
                    _IncomingAppMessageObserverForDataBackup =
                    AppObservableSubscriber.Subscribe(SeqApp.IncomingObservableAppMessage,
                            it => IncomingAppMessagegUpdated(it));
                    SequenceDataBackup dataBackup = new SequenceDataBackup(SeqApp.Logger) { IsSimulationMode = isSimulation };
                    dataBackup.OnSequenceDataBackupStatus += OnSequenceDataBackupStatus;
                    SequenceDataBackingup = dataBackup;
                    SequenceDataBackingup.StartBackupQProcessingThread();
                }
                else
                {
                    SequenceDataBackingup = null;
                }
                NeedWaitForBackupCompleteInSideRecipe = false;
                _RecipeRunThreadV2_sequnce = new RecipeRunThreadV2(
                             SeqApp.TheDispatcher,
                            SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                            finalrecipe,
                            SeqApp.EthernetCameraA,
                            SeqApp.EthernetCameraB,
                            SeqApp.MotionController,
                            SeqApp.MainboardDevice,
                            SeqApp.LEDController,
                            SeqApp.FluidicsInterface,
                            _RecipeParam,
                            null, //outterThread
                             OLAJobs, // OLA
                             NeedWaitForOLACompletedInSideRecipe, //no wait for OLA completion
                             SequenceDataBackingup, NeedWaitForBackupCompleteInSideRecipe
                            );

                _RecipeRunThreadV2_sequnce.IsSimulationMode = isSimulation;
                _RecipeRunThreadV2_sequnce.IsEnablePP = seqParams.IsEnablePP;
                _RecipeRunThreadV2_sequnce.OnRecipeRunUpdated += _RecipeRunThreadV2_sequnce_OnRecipeRunUpdated;
                _RecipeRunThreadV2_sequnce.OnStepRunUpdated += _RecipeRunThreadV2_sequnce_OnStepRunUpdated;
                _RecipeRunThreadV2_sequnce.OnLoopStepUpdated += _RecipeRunThreadV2_sequnce_OnLoopStepUpdated;
                _RecipeRunThreadV2_sequnce.Completed += _RecipeRunThreadV2_sequnce_Completed;

                if (OLAJobs != null)
                {
                    OLAJobs.OLAInfoUpdated += Sequnce_OLAUpdated;
                    OLAJobs.OnOLAStatusUpdated += Sequnce_OnOLAStatusUpdated;

                    string str = "";
                    if (seqParams.IsEnableOLA)
                    {
                        if (seqParams.IsEnablePP)
                        {
                            str = "OLA & Post Processing ON";
                        }
                        else
                        {
                            str = "OLA ON";
                        }
                    }
                    else if (seqParams.IsEnablePP)
                    {
                        str = "Post Processing ON";
                    }
                    else
                    {
                        str = "OLA OFF";
                    }
                    OLAJobs.OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = str });

                }
                _RecipeRunThreadV2_sequnce.OnImageSaved += _RecipeRunThreadV2_sequnce_OnImageSaved;
                _RecipeRunThreadV2_sequnce.Name = "Sequence";
                _ExitState = ThreadBase.ThreadExitStat.None;


                _RecipeRunThreadV2_sequnce.Start();
                Thread th = new Thread(() => MonitoringTimer());
                th.Name = "SequenceTimer";
                th.IsBackground = true;
                th.Start();

                _UsageMonitor = new UsageMonitor(SeqApp);
                _UsageMonitor.StartMonitoring();

                //if (isSimulation && OLAJobs == null)
                //{
                //    //start a pure OLA simulation
                //    SequenceInformation.Cycles = 50;
                //    SequenceInformation.Lanes = new int[] { 1, 2, 3, 4 };
                //    string _OfflineDataPath = @"D:\OLA_Test_data\bcqc";
                //    SequenceInformation.WorkingDir = _OfflineDataPath;
                //    SequenceInformation.Index1Enabled = false;
                //    SequenceInformation.Index2Enabled = false;
                //    SequenceInformation.Paired = false;
                //    _OLAOfflineDataSeq = new SequenceOfflineDataProcess(this.SeqApp, this.SeqApp.Logger);
                //    _OLAOfflineDataSeq.StartSequenceProcess(_SequenceDataInfoHelper, SequenceInformation);// _OfflineDataPath, SequenceInformation.Cycles);
                //}
                SeqApp.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started }, AppMessageTypeEnum.Status);

                return WaitForSequenceCompleted();
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to run sequence: Exception error {ex.Message}");
                _RecipeRunThreadV2_sequnce?.AbortWork();
                WaitForSequenceCompleted();
                return false;
            }
        }

        private void _RecipeRunThreadV2_sequnce_OnRecipeRunUpdated(string msg)=> SeqApp.UpdateAppMessage(msg, AppMessageTypeEnum.Normal);

        private void OnSequenceDataBackupStatus(object sender, SequenceDataBackupEventArgs e)
        {
            if (_RecipeRunThreadV2_sequnce != null)
            {
                _RecipeRunThreadV2_sequnce.OnStepRunUpdatedInvoke(e.Step, e.Message, e.IsError);
            }
            else
            {
                SeqApp.UpdateAppMessage(e.Message, e.IsError ? AppMessageTypeEnum.Error : AppMessageTypeEnum.Normal);
            }
            SeqApp.UpdateAppMessage(new AppSequenceStatusDataBackup() { IsError = e.IsError, Message = e.Message, DataBackupStatus = ProgressTypeEnum.InProgress },
                AppMessageTypeEnum.Status);
        }

        bool WaitForSequenceCompleted()
        {
            if (_RecipeRunThreadV2_sequnce != null)
            {
                _RecipeRunThreadV2_sequnce.WaitForCompleted();
                _RecipeRunThreadV2_sequnce = null;
            }

            if (OLAJobs != null)
            {
                Task tk = Task.Run(() =>
                  {
                      OLAJobs.WaitForAllDone();
                      //OLAJobs.OLAUpdated -= Sequnce_OLAUpdated;
                      //OLAJobs.OnOLAStatusUpdated -= Sequnce_OnOLAStatusUpdated;
                  }).ContinueWith((o) =>
                  {
                      ProgressTypeEnum oLAStatus = ProgressTypeEnum.None;
                      string str;
                      if (OLAJobs.IsAbort)
                      {
                          oLAStatus = ProgressTypeEnum.Aborted;
                          str = "OLA Aborted";
                      }
                      else
                      {
                          oLAStatus = ProgressTypeEnum.Completed;
                          if (!string.IsNullOrEmpty(OLAJobs.LastUserErrorMessage))
                          {
                              //oLAStatus = ProgressTypeEnum.Failed;
                              str = $"OLA Ends with error: {OLAJobs.LastUserErrorMessage}";
                          }
                          else
                          {
                              str = "OLA Ends";
                          }
                      }
                      
                      //SequenceDataBackingup?.SetLastData();
                      
                      IsOLADone = true;
                      Sequencerunning = false; //sequence is done but backup could be still running
                      SeqApp.UpdateAppMessage(new AppSequenceStatusOLA() { Message = str, OLAStatus = oLAStatus },
                               AppMessageTypeEnum.Status);
                      OLAJobs.OLAInfoUpdated -= Sequnce_OLAUpdated;
                      OLAJobs.OnOLAStatusUpdated -= Sequnce_OnOLAStatusUpdated;
                  });

                if (NeedWaitForOLACompletedInSideRecipe) //it is false in this case
                {
                    tk.Wait();
                }
            }
            else if (_OLAOfflineDataSeq != null)
            {

                Task tk = Task.Run(() =>
                {
                    _OLAOfflineDataSeq.WaitForSequenceDone();
                    //OLAJobs.OLAUpdated -= _RecipeRunThreadV2_sequnce_OLAUpdated;
                    // OLAJobs.OnOLAStatusUpdated -= _RecipeRunThreadV2_sequnce_OnOLAStatusUpdated;
                }).ContinueWith((o) =>
                {
                    IsOLADone = true;
                });
                tk.Wait();
            }
            
            if (SequenceDataBackingup != null)
            {
                Task tk = Task.Run(() =>
                {
                    SequenceDataBackingup.WaitForBackingupComplete();
                    //SequenceDataBackingup.OnSequenceDataBackupStatus -= OnSequenceDataBackupStatus;
                }).ContinueWith((o) =>
                {
                    ProgressTypeEnum status = ProgressTypeEnum.None;
                    string str;
                    if (SequenceDataBackingup.IsAbort)
                    {
                        status = ProgressTypeEnum.Aborted;
                        str = "Sequence data backup was aborted";
                    }
                    else
                    {
                        status = ProgressTypeEnum.Completed;
                        str = "Sequence data back up ends";
                    }
                    SeqApp.UpdateAppMessage(new AppSequenceStatusDataBackup() { Message = str, DataBackupStatus = status },
                              AppMessageTypeEnum.Status);
                    SequenceDataBackingup.OnSequenceDataBackupStatus -= OnSequenceDataBackupStatus;
                    IsSequenceDataBackupDone = true;
                    AppObservableSubscriber.Unsubscribe(_IncomingAppMessageObserverForDataBackup);
                });
                if (NeedWaitForBackupCompleteInSideRecipe) //always true
                {
                    tk.Wait();
                }
            }

            Sequencerunning = false;
             return _ExitState == ThreadBase.ThreadExitStat.None;
        }

        void MonitoringTimer()
        {
            Stopwatch sw = Stopwatch.StartNew();
            int sleepSec = 1;
            //int count = 0;
            while (Sequencerunning || !IsOLADone || !IsSequenceDataBackupDone)
            {
                Thread.Sleep(sleepSec * 1000);

                SeqApp.UpdateAppMessage(new AppSequenceStatusTime() { TimeElapsed = sw.Elapsed }, AppMessageTypeEnum.Status);

            }
            sw.Stop();
        }

        private SequenceDataTypeEnum FromSeqqunceRead(ImagingStep.SequenceRead seqRead)
        {
            SequenceDataTypeEnum seqDataType = SequenceDataTypeEnum.None;
            switch (seqRead)
            {
                case ImagingStep.SequenceRead.Read1:
                    seqDataType = SequenceDataTypeEnum.Read1;
                    break;
                case ImagingStep.SequenceRead.Index1:
                    seqDataType = SequenceDataTypeEnum.Index1;
                    break;
                case ImagingStep.SequenceRead.Index2:
                    seqDataType = SequenceDataTypeEnum.Index2;
                    break;
                case ImagingStep.SequenceRead.Read2:
                    seqDataType = SequenceDataTypeEnum.Read2;
                    break;
                case ImagingStep.SequenceRead.None:
                    seqDataType = SequenceDataTypeEnum.None;
                    break;
            }
            return seqDataType;
        }
        private void _RecipeRunThreadV2_sequnce_OnImageSaved(ImageSavedEventArgs args)
        {
            SeqApp.UpdateAppMessage(args.Message);
            SeqApp.UpdateAppMessage(new AppSequenceStatusImage() { ImageSaved = Path.GetFileName(args.ImageFile) }, AppMessageTypeEnum.Status);
            SequenceDataTypeEnum seqRead = SequenceDataTypeEnum.None;
            if (args.Step is ImagingStep)
            {
                seqRead = FromSeqqunceRead(((ImagingStep)args.Step).Read);
            }
            SeqApp.UpdateAppMessage(new AppSequenceStatusCycle() { Cycle = args.ImageCurrentLoopCount, StepSequenceRead = seqRead }, AppMessageTypeEnum.Status);
        }

        private void _RecipeRunThreadV2_sequnce_OnStepRunUpdated(RecipeStepBase step, string msgOriginal, bool isCritical)
        {
            AppMessageTypeEnum msgtype = AppMessageTypeEnum.Normal;
            string msg = string.Format("[Temp:C-{0:F1}° H-{1:F1} PH-{3:F1}] {2}", TemperatureController.GetInstance().CurrentTemper, SeqApp.MainBoardController.HeatSinkTemper, msgOriginal, SeqApp.MainBoardController.FluidPreHeatingCrntTemper);
            if (isCritical)
            {
                SendEmail(msg);
                msgtype = AppMessageTypeEnum.Error;
            }
            if (RecipeRunThreadBase.IsStartRunningMessage(msgOriginal))
            {
                SeqApp.UpdateAppMessage(new AppSequenceStatusStep() { Step = step.StepName }, AppMessageTypeEnum.Status);
            }
            else
            {
                SeqApp.UpdateAppMessage(msg, msgtype);
            }
        }

        private void SendEmail(string message)
        {
            try
            {
                using (MailMessage mail = new MailMessage())
                {
                    SmtpClient SmtpServer = new SmtpClient("smtp.fapon.com");
                    mail.From = new MailAddress("sequlite@fapon.com");
                    //mail.To.Add("xiang@sequlite.com");
                    if (_SubscribedEmail != null)
                    {
                        mail.To.Add(string.Format("{0}", _SubscribedEmail));
                    }
                    mail.Subject = string.Format($"[{SettingsManager.ConfigSettings.CalibrationSettings.InstrumentInfo.InstrumentName}] CUI Update");
                    mail.Body = message;
                    lock (_EmailLock)
                    {
                        //SmtpServer.Port = 587;
                        //SmtpServer.Credentials = new System.Net.NetworkCredential("instrument@sequlite.com", "Sequlite6759!");
                        SmtpServer.Credentials = new System.Net.NetworkCredential("sequlite@fapon.com", "seq100++");
                        //SmtpServer.EnableSsl = true;

                        SmtpServer.Send(mail);
                        SmtpServer.Dispose();
                        //MessageBox.Show("mail Send");
                    }
                }
            }
            catch (Exception ex)
            {
                SeqApp.UpdateAppMessage(ex.ToString(), AppMessageTypeEnum.Error);
            }
        }

        private void _RecipeRunThreadV2_sequnce_OnLoopStepUpdated(StepsTree steptree)
        {
        }


        private void _RecipeRunThreadV2_sequnce_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            try
            {
                _ExitState = _RecipeRunThreadV2_sequnce.ExitStat;
                string str = "";
                AppMessageTypeEnum appMessageTypeEnum = AppMessageTypeEnum.Normal;
                ProgressTypeEnum stepMessageType = ProgressTypeEnum.InProgress;
                if (_RecipeRunThreadV2_sequnce.IsAbort)
                {
                    _ExitState = ThreadBase.ThreadExitStat.Abort;
                    str = "Imaging Aborted";
                    appMessageTypeEnum = AppMessageTypeEnum.Warning;
                    stepMessageType = ProgressTypeEnum.Aborted;
                }

                if (_ExitState == ThreadBase.ThreadExitStat.None)
                {
                    str = "Imaging Completed";
                    appMessageTypeEnum = AppMessageTypeEnum.Completed;
                    stepMessageType = ProgressTypeEnum.Completed;
                }
                else if (_ExitState == ThreadBase.ThreadExitStat.Error)
                {
                    str = "Imaging Failed";
                    appMessageTypeEnum = AppMessageTypeEnum.Error;
                    stepMessageType = ProgressTypeEnum.Failed;
                }

                _RecipeRunThreadV2_sequnce.OnRecipeRunUpdated -= _RecipeRunThreadV2_sequnce_OnRecipeRunUpdated;
                _RecipeRunThreadV2_sequnce.OnStepRunUpdated -= _RecipeRunThreadV2_sequnce_OnStepRunUpdated;
                _RecipeRunThreadV2_sequnce.OnLoopStepUpdated -= _RecipeRunThreadV2_sequnce_OnLoopStepUpdated;
                _RecipeRunThreadV2_sequnce.Completed -= _RecipeRunThreadV2_sequnce_Completed;

                SeqApp.UpdateAppMessage(str, appMessageTypeEnum);

                SeqApp.UpdateAppMessage(new AppSequenceStatusStep() { Step = str, StepMessageType = stepMessageType }, AppMessageTypeEnum.Status);
                // Unlock Chiller door
                //if (Chiller.GetInstance().IsProtocolRev2)
                //{
                //    if (_Chiller.ChillerDoorControl(false))
                //    {
                //        if (_Chiller.ChillerDoorControl(false))
                //        {
                //            //SeqApp.UpdateAppErrorMessage("Lock Chiller Door failed.");
                //            //return false;
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                SeqApp.Logger.LogError(string.Format("Exception error in recipe complete event handler: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
                SeqApp.UpdateAppMessage($"Exception error in recipe complete event handler: {ex.Message}", AppMessageTypeEnum.Error);
            }
        }

        ImagingStep.SequenceRead PreviousSequenceRead { get; set; } = ImagingStep.SequenceRead.None;

        private void Sequnce_OLAUpdated(OLAWorkingDirInfo e)
        {
            this.SequenceInformation.WorkingDir = e.BaseWorkingDir;
            _OLAWorkingDirInfo = e;
        }

        private void Sequnce_OnOLAStatusUpdated(object sender, OLARunningEventArgs e)
        {
            OLAJobManager job = sender as OLAJobManager;
            if (job != null)
            {
                ImagingStep.SequenceRead read = job.SequenceRead;
                SequenceDataTypeEnum sequenceDataType = GetSequenceDataType(read);

                if (read != PreviousSequenceRead)
                {
                    
                    PreviousSequenceRead = read;
                    SeqApp.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started, SequenceDataType = sequenceDataType }, AppMessageTypeEnum.Status);
                }

                ImagingStep.SequenceRead read2 = read;
                if (e.MessageType == OLARunningEventArgs.OLARunningMessageTypeEnum.Exit)
                {
                    read2 = ImagingStep.SequenceRead.None;
                }

                if (OLABackupHistory != null)
                {
                    if (!OLABackupHistory.ContainsKey(read2) || !OLABackupHistory[read2])
                    {

                        if (BackupOLAData(e.MessageType, read2, job))
                        {
                            OLABackupHistory[read2] = true;
                        }
                    }
                }

                Task tk = Task.Run(() =>
                {
                    _SequenceDataInfoHelper?.SendSequnceOLAResults(e.Results, sequenceDataType,
                        e.MessageType == OLARunningEventArgs.OLARunningMessageTypeEnum.Cycle_Finished);                    
                });
            }

            SeqApp.UpdateAppMessage(new AppSequenceStatusOLA() { Message = e.Message, OLAStatus = ProgressTypeEnum.InProgress },
                    AppMessageTypeEnum.Status);
        }



        public SequenceOLADataInfo SequenceOLAData(SequenceDataTypeEnum sequenceDataType)
        {
            return _SequenceDataInfoHelper.SequenceOLAData(sequenceDataType);
        }

        public Recipe BuildRecipe(RunSeqenceParameters seqParams)
        {
            int readlength = seqParams.Readlength;
            bool paired = seqParams.Paired;
            bool IndexEnable = seqParams.Index1Enable;
            int IndexNumber = seqParams.Index1Number;
            double focusedBottomPos = seqParams.FocusedBottomPos;
            double focusedTopPos = seqParams.FocusedTopPos;
            Recipe newrecipe = new Recipe("Sequence");
            newrecipe.CreatedTime = DateTime.Now;
            newrecipe.UpdatedTime = newrecipe.CreatedTime;
            newrecipe.ToolVersion = string.Format($" {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");

            //Edit Image recipe with new AF reference pos
            string str = seqParams.SessionId;// DateTime.Now.ToString("yyMMdd-HHmmss");
            string recipeDir = SeqApp.CreateTempRecipeLocation(str);
            string originalRecipeDir = _RecipeBuildSettings.RecipeBaseDir;
            //image recipe
            Recipe OrignalRecipe = Recipe.LoadFromXmlFile(_RecipeBuildSettings.OriginalImageRecipePath);
            OrignalRecipe.RecipeName = $"{seqParams.ExpName}_{seqParams.SessionId}_Read1";
            OrignalRecipe.UpdatedTime = newrecipe.UpdatedTime;
            ((ImagingStep)OrignalRecipe.Steps[0].Step).Regions[0].ReferenceFocuses[0].Position = focusedTopPos;
            ((ImagingStep)OrignalRecipe.Steps[0].Step).Regions[0].ReferenceFocuses[1].Position = focusedBottomPos;
            string fileName = Path.Combine(recipeDir, Path.GetFileName(_RecipeBuildSettings.OriginalImageRecipePath));
            //probably no effects foe imaging recipe
            SeqApp.ReplaceAndCopyRunRecipePath(OrignalRecipe, recipeDir, originalRecipeDir);
            Recipe.SaveToXmlFile(OrignalRecipe, fileName);
            RunRecipeStep _Read1ImageRecipe = new RunRecipeStep();
            _Read1ImageRecipe.RecipePath = fileName;

            //Cluster Gen may include first inc
            RunRecipeStep _ClusterGen = new RunRecipeStep();
            _ClusterGen.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.ClusterGenRecipePath, recipeDir, null, originalRecipeDir);

            //Hyb
            RunRecipeStep _HybRecipe = new RunRecipeStep();
            _HybRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.HybRecipePath, recipeDir, null, originalRecipeDir);
            // Inc + CL
            RunRecipeStep _IncRecipe = new RunRecipeStep();
            _IncRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.IncRecipePath, recipeDir, null, originalRecipeDir);
            RunRecipeStep _CLRecipe = new RunRecipeStep();
            _CLRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.CLRecipePath, recipeDir, null, originalRecipeDir);

            //Paired end Turnaround ?
            RunRecipeStep _PairedT = null;
            if (!string.IsNullOrEmpty(_RecipeBuildSettings.PairedTRecipePath))
            {
                _PairedT = new RunRecipeStep();
                _PairedT.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.PairedTRecipePath, recipeDir, null, originalRecipeDir);
            }

            //Loop for Seq
            LoopStep _SeqLoopStep = new LoopStep();
            _SeqLoopStep.LoopCycles = readlength - 1;
            StepsTree loopSequnceStepTree = new StepsTree(null, _SeqLoopStep);

            //SeqWash
            RunRecipeStep _SeqWashRecipe = new RunRecipeStep();
            _SeqWashRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.SeqWashRecipePath, recipeDir, null, originalRecipeDir);

            //Edit new outter recipe
            if (seqParams.IsCG) { newrecipe.Steps.Add(new StepsTree(null, _ClusterGen)); }
            newrecipe.Steps.Add(new StepsTree(null, _HybRecipe));
            newrecipe.Steps.Add(new StepsTree(null, _Read1ImageRecipe));
            newrecipe.Steps.Add(new StepsTree(null, _SeqLoopStep));

            //Add Inc/CL/Imaging steps inside loop
            newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _CLRecipe));
            newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _IncRecipe));
            newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _Read1ImageRecipe));

            if (IndexEnable)
            {
                //Index strip and Hyb
                RunRecipeStep _IndexHybRecipe = new RunRecipeStep();
                _IndexHybRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.IndexHybRecipePath, recipeDir, null, originalRecipeDir);
                RunRecipeStep _StpRecipe = new RunRecipeStep();
                _StpRecipe.RecipePath = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.StpRecipePath, recipeDir, null, originalRecipeDir);
                // Index image recipe
                //OrignalRecipe = Recipe.LoadFromXmlFile(_RecipeBuildSettings.OriginalImageRecipePath);
                OrignalRecipe.RecipeName = $"{seqParams.ExpName}_{seqParams.SessionId}_Index1";
                OrignalRecipe.UpdatedTime = newrecipe.UpdatedTime;
                fileName = Path.Combine(recipeDir, "Image_Index.xml");

                SeqApp.ReplaceAndCopyRunRecipePath(OrignalRecipe, recipeDir, originalRecipeDir);
                Recipe.SaveToXmlFile(OrignalRecipe, fileName);
                RunRecipeStep _Index1ImageRecipe = new RunRecipeStep();
                _Index1ImageRecipe.RecipePath = fileName;
                //Loop 
                LoopStep _IndLoopStep = new LoopStep();
                _IndLoopStep.LoopCycles = IndexNumber - 1;
                //Add to FullRecipe
                //newrecipe.Steps.Add(new StepsTree(null, _StpRecipe));
                newrecipe.Steps.Add(new StepsTree(null, _IndexHybRecipe));
                newrecipe.Steps.Add(new StepsTree(null, _Index1ImageRecipe));
                newrecipe.Steps.Add(new StepsTree(null, _IndLoopStep));
                newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _CLRecipe));
                newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _IncRecipe));
                newrecipe.Steps[newrecipe.Steps.Count - 1].Children.Add(new StepsTree(loopSequnceStepTree, _Index1ImageRecipe));

            }
            //Seq Wash
            //newrecipe.Steps.Add(new StepsTree(null, _SeqWashRecipe));

            if (_PairedT != null) //todo: add paired recipe
            {
            }
            //to do: add paired recipe
            //if (paired)
            //{
            //    newrecipe.Steps.Add(new StepsTree(null, _PairedT));
            //    newrecipe.Steps.Add(new StepsTree(new StepsTree(null, _SeqLoopStep), _Sequencing2));
            //    if (IndexNumber != 0) { }// todo
            //}

            // Post Wash recipe--- will be used by IPostRun
            PostWashingRecipeFile = SeqApp.SaveRecipeToNewPath(_RecipeBuildSettings.PostWashRecipePath, recipeDir, "PostWash.xml", originalRecipeDir);

            //RunRecipeStep postwashRecipe = new RunRecipeStep();
            //postwashRecipe.RecipePath = PostWashingRecipeFile;
            //newrecipe.Steps.Add(new StepsTree(null, postwashRecipe));

            string newRecipeFile = Path.Combine(recipeDir, newrecipe.RecipeName + ".xml");
            Recipe.SaveToXmlFile(newrecipe, newRecipeFile);
            //load it again to ensure child and parent relationship
            newrecipe = Recipe.LoadFromXmlFile(newRecipeFile);

            return newrecipe;
        }

        public bool StopSequence()
        {
            IsAbort = true;
            _RecipeRunThreadV2_sequnce?.AbortWork();
            OLAJobs?.Stop();
            if (_OLAOfflineDataSeq != null)
            {
                _OLAOfflineDataSeq.IsAborted = true;
                _OLAOfflineDataSeq.StopSequenceProcessAndWaitForDone();
            }
            if (SequenceDataBackingup != null)
            {
                SequenceDataBackingup.IsAbort = true;
            }
            WaitForSequenceCompleted();
            return true;
        }

        async Task<SequenceInfo> PrepareSequenceInformation(string dInfoFile)
        {
            //bool b = false;
            SequenceInfo seqInfo = null;
            await Task.Run(() =>
            {
                OLASequenceInfo dInfo = null;
                dInfo = DeserializeSequenceInfo(dInfoFile);
                if (dInfo != null)
                {
                    seqInfo = new SequenceInfo();
                    seqInfo.Paired = dInfo.Paired;
                    seqInfo.Index1Cycle = dInfo.IndexCycles.Item1;
                    seqInfo.Index2Cycle = dInfo.IndexCycles.Item2;
                    seqInfo.Index1Enabled = dInfo.Index1Enabled;
                    seqInfo.Index2Enabled = dInfo.Index2Enabled;

                    seqInfo.Lanes = dInfo.Lanes.ToArray();
                    seqInfo.Rows = dInfo.Rows;
                    seqInfo.Column = dInfo.Columns;
                    seqInfo.Cycles = dInfo.Cycles;
                    seqInfo.Template = dInfo.Template;

                    seqInfo.Instrument = dInfo.Instrument;
                    seqInfo.InstrumentID = dInfo.InstrumentID;
                    seqInfo.SampleID = dInfo.SampleID;
                    seqInfo.FlowCellID = dInfo.FlowCellID;
                    seqInfo.ReagentID = dInfo.ReagentID;
                    //b = true;
                }

            });
            return seqInfo;
        }

        OLASequenceInfo DeserializeSequenceInfo(string infoFilePath)
        {
            OLASequenceInfo seqInfo = null;
            try
            {
                seqInfo = OLAJobManager.DeserializeSequenceInfo(infoFilePath);
            }
            catch (Exception ex)
            {
                SeqApp.Logger.LogError($"failed to parse data info file {infoFilePath} with exception: {ex}");
            }
            return seqInfo;
        }

       

        SequenceDataTypeEnum GetSequenceDataType(ImagingStep.SequenceRead sequenceRead)
        {
            SequenceDataTypeEnum sequenceDataType = SequenceDataTypeEnum.None;
            switch (sequenceRead)
            {
                case ImagingStep.SequenceRead.None:
                    sequenceDataType = SequenceDataTypeEnum.None;
                    break;
                case ImagingStep.SequenceRead.Read1:
                    sequenceDataType = SequenceDataTypeEnum.Read1;
                    break;
                case ImagingStep.SequenceRead.Index1:
                    sequenceDataType = SequenceDataTypeEnum.Index1;
                    break;
                case ImagingStep.SequenceRead.Index2:
                    sequenceDataType = SequenceDataTypeEnum.Index2;
                    break;
                case ImagingStep.SequenceRead.Read2:
                    sequenceDataType = SequenceDataTypeEnum.Read2;
                    break;
            }
            return sequenceDataType;
        }

        private List<OLALaneSampleIndexInfo> PrepareOLALaneSampleIndexInfo(SampleSheetDataInfo sData)
        {
            return SeqAppRunSetup.PrepareOLALaneSampleIndexInfo(sData);
        }

        private static bool ValidateOLALaneSampleIndexInfo(List<OLALaneSampleIndexInfo> lanes, out string error)
        {
            return SeqAppRunSetup.ValidateOLALaneSampleIndexInfo(lanes, out error);
        }

        bool CanBackupBcqc(ImagingStep.SequenceRead read)
        {
            bool b = false;
            if ((!SequenceInformation.Index1Enabled) && (!SequenceInformation.Index2Enabled))
            {
                if (SequenceInformation.Paired)
                {
                    b = (read == ImagingStep.SequenceRead.Read2);
                }
                else
                {
                    b = true;// (read == ImagingStep.SequenceRead.Read1);
                }
            }
            else
            {
                if (SequenceInformation.Index1Enabled && SequenceInformation.Index2Enabled)
                {
                    b = (read == ImagingStep.SequenceRead.Index2);
                }
                else if (SequenceInformation.Index1Enabled)
                {
                    b = (read == ImagingStep.SequenceRead.Index1);
                }
                else
                {
                    b = (read == ImagingStep.SequenceRead.Index2);
                }
            }
            return b;
        }

        private bool BackupOLAData(OLARunningEventArgs.OLARunningMessageTypeEnum t,
            ImagingStep.SequenceRead read, OLAJobManager job)
        {
            //if current message indicating  real1 or read2 finishes
            //back up RecipeWorkingDir/Read#/OLA;
            //if no index, and all read(s) finish, backup RecipeWorkingDir/ bcqc

            //if current message indicating    index1 or 2 finishes
            // back up RecipeWorkingDir/Index#/OLA and RecipeWorkingDir/ bcqc

            //if current message indicating "OLA exits"
            //back up RecipeWorkingDir/fastq, and RecipeWorkingDir / GoodTiles.txt,
            //and RecipeWorkingDir/IndexMerge.log 

            //in the very last step:
            //backup RecipeWorkingDir/Report.pdf
            bool b = false;
            if (SequenceDataBackingup != null)
            {
                switch (t)
                {

                    case OLARunningEventArgs.OLARunningMessageTypeEnum.One_Read_Finished:
                        {
                            string rootDataBackupDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.RecipeRunBackupRootDir;
                            rootDataBackupDir = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.DirWithInstrumentName);
                            
                           
                           //OLA folder for current read --- readName/OLA
                           string backupDir = Path.Combine(_OLAWorkingDirInfo.BaseWorkingDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.OLAFolderName);
                            //if (Directory.Exists(backupDir))
                            {
                                string dataBackupDir = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.OLAFolderName);
                                SeqApp.Logger.Log($"Add request: backup {backupDir} to {dataBackupDir}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), backupDir, dataBackupDir, true);
                            }

                            //Data folder for current read -- readname/Data
                            backupDir = Path.Combine(_OLAWorkingDirInfo.BaseWorkingDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.DataFolderName);
                            //if (Directory.Exists(backupDir))
                            {
                                string dataBackupDir = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.DataFolderName);
                                SeqApp.Logger.Log($"Add request: backup {backupDir} to {dataBackupDir}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), backupDir, dataBackupDir, true);
                            }

                            if (CanBackupBcqc(read))
                            {
                                //baseWorkDir/bcqc
                                string olaBcqcDir = Path.Combine(_OLAWorkingDirInfo.BaseWorkingDir, OLAWorkingDirInfo.BcqcFolderName);
                                //if (Directory.Exists(olaBcqcDir))
                                {
                                    string dataBackupDir = Path.Combine(rootDataBackupDir, OLAWorkingDirInfo.BcqcFolderName);
                                    SeqApp.Logger.Log($"Add request: backup {olaBcqcDir} to {dataBackupDir}");
                                    SequenceDataBackingup.AddABackupRequest(new ImagingStep(), olaBcqcDir, dataBackupDir, true);
                                }
                            }

                            //readName/OLAinfo.txt
                            string backupFileName = Path.Combine(_OLAWorkingDirInfo.BaseWorkingDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.OLAInfoFileName);
                            //if (File.Exists(backupFileName))
                            {
                                string dataBackupFile = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.ReadName, _OLAWorkingDirInfo.OLAInfoFileName);
                                SeqApp.Logger.Log($"Add request: backup {backupFileName} to {dataBackupFile}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), backupFileName, dataBackupFile, false);
                            }

                            b = true;
                        }
                        break;
                    case OLARunningEventArgs.OLARunningMessageTypeEnum.Exit:
                        {
                            string rootDataBackupDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.RecipeRunBackupRootDir;
                            rootDataBackupDir = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.DirWithInstrumentName);
                            string olaBaseWorkDir = _OLAWorkingDirInfo.BaseWorkingDir;

                            //baseworkingDir/fastq
                            string fastqDir = Path.Combine(olaBaseWorkDir, _OLAWorkingDirInfo.FastQFolderName);
                            string dataBackupDir = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.FastQFolderName);
                            //if (Directory.Exists(fastqDir))
                            {
                                SeqApp.Logger.Log($"Add request: backup {fastqDir} to {dataBackupDir}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), fastqDir, dataBackupDir, true);
                            }

                            //baseworkingDir/GoodTiles.txt
                            string fileBackup = Path.Combine(olaBaseWorkDir, _OLAWorkingDirInfo.GoodTilesFileName);
                            //if (File.Exists(fileBackup))
                            {
                                string dataBackupFile = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.GoodTilesFileName);
                                SeqApp.Logger.Log($"Add request: backup {fileBackup} to {dataBackupFile}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), fileBackup, dataBackupFile, false);
                            }

                            //baseworkingDir/IndexMergeLog.log
                            fileBackup = Path.Combine(olaBaseWorkDir, _OLAWorkingDirInfo.IndexMergeLogFileName);
                            //if (File.Exists(fileBackup))
                            {
                                string dataBackupFile = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.IndexMergeLogFileName);
                                SeqApp.Logger.Log($"Add request: backup {fileBackup} to {dataBackupFile}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), fileBackup, dataBackupFile, false);
                            }

                            
                            b = true;
                        }
                        break;
                }

            }
            return b;
        }

        void IncomingAppMessagegUpdated(AppMessage appMsg)
        {

            AppStatus st = appMsg.MessageObject as AppStatus;
            if (st != null)
            {
                switch (st.AppStatusType)
                {
                    case AppStatusTypeEnum.AppSequenceStatusSeqenceReport:
                        {
                            string reportFileName = ((AppSequenceStatusSequenceReport)st).ReportSaved;
                            //if (File.Exists(reportFileName))
                            {
                                //olabasewrokingdir/Report.pdf
                                string rootDataBackupDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.RecipeRunBackupRootDir;
                                string fileBackup = Path.Combine(rootDataBackupDir, _OLAWorkingDirInfo.DirWithInstrumentName, Path.GetFileName(reportFileName));

                                SeqApp.Logger.Log($"Add request: backup {reportFileName} to {fileBackup}");
                                SequenceDataBackingup.AddABackupRequest(new ImagingStep(), reportFileName, fileBackup, false);
                            }
                            SequenceDataBackingup?.SetLastData();
                        }
                        break;

                }
            }
        }

        
    }
}

