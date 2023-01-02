using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    partial class SeqAppSequence : ISequence
    {
        SequenceOfflineDataProcess _OLAOfflineDataSeq;

        public bool OfflineDataDisplaySequence(RunOfflineDataSeqenceParameters seqParams)
        {
            IsAbort = false;
            string dInfoFile = seqParams.DataInfoFile;
            SeqApp.UpdateAppMessage("Start off-line data processing for display");
            string instrument = SequenceInformation.Instrument;
            string instrumnetId = SequenceInformation.InstrumentID;
            SequenceInformation = Task.Run(() => PrepareSequenceInformation(dInfoFile)).Result;
            bool b = true;
            if (SequenceInformation == null)
            {
                SeqApp.NotifyNormalError($"Failed to parse data info file {dInfoFile}");
                b = false;
            }
            else
            {
                SequenceInformation.Instrument = instrument;
                SequenceInformation.InstrumentID = instrumnetId;
                SequenceInformation.WorkingDir = Path.GetDirectoryName(dInfoFile);
                try
                {
                    SequenceInformation.WorkingDir = Path.GetDirectoryName(dInfoFile);
                    if (Sequencerunning)
                    {
                        SeqApp.NotifyNormalError("Cannot start a new off-line data processing for display while another sequence is still running.");
                        b = false;
                    }
                    if (b)
                    {
                        Sequencerunning = true;
                        SeqApp.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started }, AppMessageTypeEnum.Status);

                        _SequenceDataInfoHelper = new SequenceDataInfoHelper(this.SeqApp);
                        _OLAOfflineDataSeq = new SequenceOfflineDataProcess(this.SeqApp, this.SeqApp.Logger);
                        _OLAOfflineDataSeq.StartSequenceProcess(_SequenceDataInfoHelper,
                           SequenceInformation,// Path.GetDirectoryName(dInfoFile), SequenceInformation.Cycles,
                            false);

                        b = WaitForSequenceCompleted();
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    SeqApp.NotifyNormalError($"off-line data processing for display has error: Exception error {ex.Message}");
                    WaitForSequenceCompleted();
                    b = false;
                }
            }
            b &= !_OLAOfflineDataSeq.HasError;
            Sequencerunning = false;
            IsOLADone = true;
            if (b)
            {
                if (IsAbort)
                {
                    SeqApp.UpdateAppMessage("Aborting off-line data processing for display", AppMessageTypeEnum.Warning);
                }
                else
                {
                    SeqApp.UpdateAppMessage("Completing off-line data processing for display");
                }
            }
            else
            {
                SeqApp.NotifyNormalError("off-line data processing has error");
            }
            return b;
        }

        public SequenceInfo GetOfflineDataSequenceInfo(string dataInfoFile)
        {
            return Task.Run(() => PrepareSequenceInformation(dataInfoFile)).Result;
        }


        public bool OfflineImageDataSequence(RunOfflineImageDataSeqenceParameters seqParams)
        {
            try
            {
                IsAbort = false;
                IsSequenceDataBackupDone = true; //no image backup
                NeedWaitForOLACompletedInSideRecipe = true; //although there is no recipe
                Sequencerunning = true;
                _SequenceDataInfoHelper = new SequenceDataInfoHelper(this.SeqApp);

                string instrument = SequenceInformation.Instrument;
                string instrumnetId = SequenceInformation.InstrumentID;
                SequenceInformation = (SequenceInfo)seqParams.SeqInfo.Clone();
                SequenceInformation.Instrument = instrument;
                SequenceInformation.InstrumentID = instrumnetId;
                //to do : 
                //build a sample sheet info for OLA data
                OLASequenceInfo dInfo = null;
                dInfo = DeserializeSequenceInfo(seqParams.DataInfoFilePath);
                OLASequenceInfo olaSeqInfo = new OLASequenceInfo(seqParams.ExpName,
                                                                 seqParams.SessionId,
                                                                 false, // TODO
                                                                 SequenceInformation.Cycles,
                                                                 SequenceInformation.IndexCycles,
                                                                 SequenceInformation.Lanes,
                                                                 SequenceInformation.Rows,
                                                                 SequenceInformation.Column,
                                                                 SequenceInformation.Template,
                                                                 SequenceInformation.IndexTemplate,
                                                                 dInfo.LaneSampleIndexInfo)
                {
                    SampleID = SequenceInformation.SampleID,
                    FlowCellID = SequenceInformation.FlowCellID,
                    ReagentID = SequenceInformation.ReagentID,
                    Instrument = SequenceInformation.Instrument,
                    InstrumentID = SequenceInformation.InstrumentID,
                };

                OLAJobs = new OLAJobManager(true, olaSeqInfo);
                OLAJobs.OLAInfoUpdated += Sequnce_OLAUpdated;
                OLAJobs.OnOLAStatusUpdated += Sequnce_OnOLAStatusUpdated;
                OLAJobs.OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = "OLA Off-line ON" });
                string workingDir = seqParams.WorkingDir;// Path.Combine(seqParams.WorkingDir, $"{seqParams.ExpName}_{seqParams.SessionId}");
                if (!Directory.Exists(workingDir))
                {
                    Directory.CreateDirectory(workingDir);
                }
                List<OLATile> olaTiles = null;
                if (seqParams.Tiles != null)
                {
                    olaTiles = new List<OLATile>();
                    foreach (var it in seqParams.Tiles)
                    {
                        olaTiles.Add(new OLATile(it));
                    }
                }
                OLAJobs.Start(workingDir, seqParams.ImageDataDir, seqParams.UsingPreviousWorkingDir, seqParams.UseSlidingWindow, olaTiles);

                Thread th = new Thread(() => MonitoringTimer());
                th.Name = "OfflineImageDataSequenceTimer";
                th.IsBackground = true;
                th.Start();

                _UsageMonitor = new UsageMonitor(SeqApp);
                _UsageMonitor.StartMonitoring();

                SeqApp.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started }, AppMessageTypeEnum.Status);

                bool b = WaitForSequenceCompleted();

                _UsageMonitor.Dispose();
                _UsageMonitor = null;
                return b;
            }
            catch (Exception ex)
            {
                SeqApp.NotifyNormalError($"Failed to run off-line image data processing: Exception error {ex.Message}");
                WaitForSequenceCompleted();
                return false;
            }
        }

        public Dictionary<SequenceDataTypeEnum, List<SeqTile>> GetOfflineTileList(string baseDataDir, SequenceInfo segInfo)
        {
            Dictionary<SequenceDataTypeEnum, List<SeqTile>> allTiles = new Dictionary<SequenceDataTypeEnum, List<SeqTile>>();
            List<SequenceDataTypeInfo>  seqTypeList = SequenceDataTypeInfo.GetListSequenceDataTypeInfos(segInfo);
            foreach (var seqTypeInfo in seqTypeList)
            {
                List<OLATile> oleTiles = OLAJobManager.BuildTileList(baseDataDir, null, seqTypeInfo.DataFolderName);
                List<SeqTile> tiles = new List<SeqTile>();
                foreach (var it in oleTiles)
                {
                    tiles.Add(new SeqTile(it.Name));// { Name = it.Name, Lane = it.Lane, Row = it.Row, Column = it.Column });
                }
                allTiles[seqTypeInfo.SequenceDataType] = tiles;
            }
            return allTiles;
        }

        public List<SeqTile> GetTileList(SequenceInfo seqInfo)
        {
            
            
            OLASequenceInfo olaSeqInfo = new OLASequenceInfo("",
                                                                 "",
                                                                 false, 
                                                                 seqInfo.Cycles,
                                                                 seqInfo.IndexCycles,
                                                                 seqInfo.Lanes,
                                                                 seqInfo.Rows,
                                                                 seqInfo.Column,
                                                                 seqInfo.Template,
                                                                 seqInfo.IndexTemplate,
                                                                 null);
            List<OLATile> oleTiles = OLAJobManager.BuildTileList(olaSeqInfo);
            List<SeqTile> tiles = new List<SeqTile>();
            foreach (var it in oleTiles)
            {
                tiles.Add(new SeqTile(it.Name));// { Name = it.Name, Lane = it.Lane, Row = it.Row, Column = it.Column }) ;  
            }
            return tiles;
        }
    }
}
