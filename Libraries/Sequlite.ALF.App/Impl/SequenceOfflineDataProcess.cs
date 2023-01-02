using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    class SequenceOfflineDataProcess
    {
        
        Thread _ProcessThread;
        public bool IsAborted { get; set; }
        public bool HasError { get; set; }
        bool _RunProcess;
        int _TotalCycle = 50;
        int _CycleStepSize = 5;
        int _FirstCycle = 25;
        IAppMessage _IAppMessage;
        string _OfflineDataPath;//= @"D:\OLA_Test_data\bcqc";
        string _TilePattern = @"^[bt]L\d\d{2}[ABCD]$";
        string _CSVFilePattern = @"^1_(?<Cycle>[1-9]\d*)_proc-int-bcqc.csv$";
        List<string> _TileList;
        ISeqLog Logger { get; }
        public SequenceOfflineDataProcess(IAppMessage seqAppMessage, ISeqLog loggger) //, string offlineOLADataFile)
        {
            //_OfflineDataPath = offlineOLADataFile;
            _IAppMessage = seqAppMessage;
            Logger = loggger;
            //var tiles = Directory.GetDirectories(_OfflineDataPath, "*", System.IO.SearchOption.TopDirectoryOnly).Where(dir => Regex.IsMatch(Path.GetFileName(dir), _TilePattern));
            //_TileList = new List<string>(tiles);
        }


        public void StartSequenceProcess(SequenceDataInfoHelper seq,
            SequenceInfo segInfo, //string offlineOLADataFile, int maxCycle, 
            bool simulationSequence = true)
        {
            
            _ProcessThread = new Thread(() =>
            {
                if (simulationSequence)
                {
                    DoSequenceSimulation(segInfo, seq);
                }
                else //process the whole data once
                {
                    ProcessAllSequenceOfflineData(segInfo, seq);
                }
            }
            );
            _ProcessThread.IsBackground = true;
            _ProcessThread.Start();

        }

        public void StopSequenceProcessAndWaitForDone()
        {
            _RunProcess = false;
            if (_ProcessThread?.IsAlive == true)
            {
                _ProcessThread?.Join();
            }
        }

        void PrepareTileListFromFile(string offlineOLADataFile)
        {
            _OfflineDataPath = offlineOLADataFile;
            

            if (Directory.Exists(_OfflineDataPath))
            {
                var tiles = Directory.GetDirectories(_OfflineDataPath, "*", System.IO.SearchOption.TopDirectoryOnly).Where(dir => Regex.IsMatch(Path.GetFileName(dir), _TilePattern));
                _TileList = new List<string>(tiles);
            }
            else
            {
                Logger.LogError($"Data path {offlineOLADataFile} doesn't exist");
                _TileList = new List<string>();
            }
        }

        void ProcessAllSequenceOfflineData(SequenceInfo segInfo, SequenceDataInfoHelper seq)
        {
            _RunProcess = true;
            List<SequenceDataTypeInfo> listSequenceDataTypeInfos = SequenceDataTypeInfo.GetListSequenceDataTypeInfos(segInfo);
            bool hasError;
            string lastError = string.Empty;
            foreach (var it in listSequenceDataTypeInfos)
            {
                hasError = false;
                int maxCycle = segInfo.Cycles;
                //to do: get multiple run information from segInfo
                SequenceDataTypeEnum sequenceDataType = it.SequenceDataType;// SequenceDataTypeEnum.Read1;
                string offlineOLADataFile = Path.Combine(segInfo.WorkingDir, it.DataFolderName);// "Read1");
                if (!Directory.Exists(offlineOLADataFile))
                {

                    string offlineOLADataFileOrg = offlineOLADataFile;
                    offlineOLADataFile = Path.Combine(segInfo.WorkingDir, OLAWorkingDirInfo.BcqcFolderName, it.DataFolderName);
                    Logger.LogWarning($"Data path {offlineOLADataFileOrg} doesn't exist, try the path {offlineOLADataFile}");
                }
                PrepareTileListFromFile(offlineOLADataFile);
                _IAppMessage.UpdateAppMessage($"Processing {it.DataFolderName} data for display");
                _IAppMessage.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started, SequenceDataType = sequenceDataType },
                    AppMessageTypeEnum.Status);

                
                int cycle = maxCycle;
                List<OLAResultsInfo> results = new List<OLAResultsInfo>();
                int count = _TileList.Count;
                string errStr = string.Empty;
                if (count <= 0)
                {
                    errStr = $"Tile list is empty for {it.DataFolderName}";
                    Logger.LogError(errStr);
                    hasError = true;
                }
                else
                {
                    int start = 0;
                    int ends = count;
                    int resultCount = 0;
                    int curMaxCycle = 0;
                    int cycleNum;
                    string dataFileName;
                    string f;
                    for (int k = start; k < ends; k++)
                    {
                        if (IsAborted) break;
                        dataFileName = string.Empty;
                        var files = Directory.GetFiles(_TileList[k], "1_*_proc-int-bcqc.csv", SearchOption.TopDirectoryOnly);
                        if (files != null)
                        {
                            curMaxCycle = -1;
                            if (IsAborted) break;
                            foreach (var file in files)
                            {
                                f = Path.GetFileName(file);
                                Match m = Regex.Match(f, _CSVFilePattern);
                                if (m.Success)
                                {
                                    cycleNum = -1;
                                    if (int.TryParse(m.Groups["Cycle"].Value, out cycleNum))
                                    {
                                        if (cycleNum == maxCycle)
                                        {
                                            curMaxCycle = maxCycle;
                                            dataFileName = f;
                                            break;
                                        }
                                        else if (cycleNum < maxCycle)
                                        {
                                            if (curMaxCycle < cycleNum)
                                            {
                                                curMaxCycle = cycleNum;
                                            }
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(dataFileName) && curMaxCycle > 0)
                            {
                                dataFileName = $"1_{curMaxCycle}_proc-int-bcqc.csv";
                            }
                        }

                        if (string.IsNullOrEmpty(dataFileName))
                        {
                            Logger.LogWarning($"Tile {_TileList[k]} doesn't have any CSV cycle files");
                            continue;
                        }

                        string tileFileName = Path.GetFileName(_TileList[k]);
                        //string dataFileName = $"1_{cycle}_proc-int-bcqc.csv";
                        _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA()
                        {
                            Message = $"Processing off-line data {dataFileName} on tile {tileFileName} for {it.DataFolderName}",
                            OLAStatus = ProgressTypeEnum.InProgress,

                        }, AppMessageTypeEnum.Status);

                        results.Add(new OLAResultsInfo()
                        {
                            Cycle = curMaxCycle,//cycle,
                            TileName = tileFileName,
                            FileLocationPath = _OfflineDataPath,
                            DataFileName = dataFileName//file name only
                        });
                        resultCount++;
                        if (!IsAborted && resultCount % 10 == 0)
                        {
                            seq.SendSequnceOLAResults(results,it.SequenceDataType, false);
                            results.Clear();
                            Thread.Sleep(1000);
                        }
                    }//for

                    if (!IsAborted)
                    {
                        seq.SendSequnceOLAResults(results,it.SequenceDataType, true);
                        Thread.Sleep(1000);
                    }
                }

                ProgressTypeEnum oLAStatus = ProgressTypeEnum.None;
                string str;
                if (IsAborted)
                {
                    oLAStatus = ProgressTypeEnum.Aborted;
                    str = "Aborting OLA off-line data display";
                    _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { Message = str, OLAStatus = oLAStatus },
                          AppMessageTypeEnum.Status);
                    break;
                }
                else
                {
                    if (hasError)
                    {
                        oLAStatus = ProgressTypeEnum.InProgressWithWarning;
                        str = $"OLA off-line data display has error: {errStr} for {it.DataFolderName}";
                        _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { Message = str, OLAStatus = oLAStatus },
                         AppMessageTypeEnum.Status);
                        HasError = hasError;
                        lastError = str;
                    }
                   
                }
               
            }//for
            if (!IsAborted)
            {
                if (HasError)
                {
                    
                    _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { 
                        Message = $"Failure in OLA off-line data display, the last error is {lastError}", 
                        OLAStatus = ProgressTypeEnum.Failed },
                     AppMessageTypeEnum.Status);
                }
                else
                {
                    _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { 
                        Message = "Finishing OLA off-line data display",
                        OLAStatus = ProgressTypeEnum.Completed },
                          AppMessageTypeEnum.Status);
                }
            }
        }

        

        void DoSequenceSimulation( SequenceInfo segInfo, SequenceDataInfoHelper seq)
        {

            List<SequenceDataTypeInfo> listSequenceDataTypeInfos = SequenceDataTypeInfo.GetListSequenceDataTypeInfos(segInfo);

            
            //int maxCycle = segInfo.Cycles;
            //to do: get multiple run information from segInfo
            SequenceDataTypeEnum sequenceDataType = SequenceDataTypeEnum.Read1;
            string offlineOLADataFile = Path.Combine(segInfo.WorkingDir, "Read1");
            PrepareTileListFromFile(offlineOLADataFile);
            _IAppMessage.UpdateAppMessage(new AppSequenceStatusProgress() { SequenceProgressStatus = ProgressTypeEnum.Started, SequenceDataType = sequenceDataType },
                AppMessageTypeEnum.Status);

            _RunProcess = true;
            int cycle = _FirstCycle;
            int i = 0;

            while (_RunProcess)
            {
                if (cycle > _TotalCycle || IsAborted)
                {
                    _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA()
                    {
                        Message = IsAborted ? "Sim: OLA Aborted" : "Sim: OLA Ends",
                        OLAStatus = IsAborted ? ProgressTypeEnum.Aborted : ProgressTypeEnum.Completed,

                    });
                    break;
                }
                else
                {
                    int curCycle = cycle;
                    _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA()
                    {
                        Message = $"Sim: OLA starts on cycle {curCycle}",
                        OLAStatus = ProgressTypeEnum.InProgress,

                    }, AppMessageTypeEnum.Status);

                    int nn = 1;
                    while (!IsAborted && nn < 30)
                    {
                        Thread.Sleep(2 * 1000);
                        nn++;
                    }

                    if (IsAborted) break;

                    int count = _TileList.Count;
                    int halfCount = count / 2;
                    int start = (i == 0) ? 0 : halfCount;
                    int ends = (i == 0) ? halfCount : count;
                    i++;

                    if (i > 1)
                    {
                        i = 0;
                        _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA()
                        {
                            Message = $"Sim: OLA finishes on cycle {curCycle}",
                            OLAStatus = ProgressTypeEnum.InProgress,

                        }, AppMessageTypeEnum.Status);
                        cycle += _CycleStepSize;
                    }

                    List<OLAResultsInfo> results = new List<OLAResultsInfo>();
                    for (int k = start; k < ends; k++)
                    {
                        if (IsAborted) break;
                        results.Add(new OLAResultsInfo()
                        {
                            Cycle = curCycle,
                            TileName = Path.GetFileName(_TileList[k]),
                            FileLocationPath = _OfflineDataPath,
                            DataFileName = $"1_{curCycle}_proc-int-bcqc.csv"
                        }); 
                    }

                    if (!IsAborted)
                    {
                        seq.SendSequnceOLAResults(results, sequenceDataType, true);
                    }
                    //Thread.Sleep(10 * 1000);
                }
            }//while
            ProgressTypeEnum oLAStatus = ProgressTypeEnum.None;
            string str;
            if (IsAborted)
            {
                oLAStatus = ProgressTypeEnum.Aborted;
                str = "OLA Off-line Sim  Aborted";
            }
            else
            {
                oLAStatus = ProgressTypeEnum.Completed;
                str = "OLA Off-line Sim  Ends";
            }
            _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { Message = str, OLAStatus = oLAStatus },
                      AppMessageTypeEnum.Status);
            //seq.IsOLADone = true;
        }

        public void WaitForSequenceDone()
        {
            if (_ProcessThread?.IsAlive == true)
            {
                _ProcessThread?.Join();
            }
        }
    }
}
