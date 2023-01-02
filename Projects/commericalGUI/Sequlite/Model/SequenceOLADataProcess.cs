using Microsoft.VisualBasic.FileIO;
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    using RegionSeqenceMetricDataList = Dictionary<RegionInfo, SeqenceMetricDataItem>;
    public interface ISequenceDataFeeder
    {
        event EventHandler<EventArgs> OLADataProcessed;
        event EventHandler<EventArgs> OLADataProcessStopped;
        LineMetricDataBuffer GetLineGraphData(LineDataKey lineType, bool checkCyccleFinished);
        List<SequenceDataTableItems> GetSequenceDataTableItems(bool checkCyccleFinished );
        bool FillMapData(int lane, MapDataKey mapDataKey, HeatMapData mapData, bool fillDefaultIfNoData = false);
        
        void NewOLAData(SequenceOLADataInfo seqOLAData);
        bool HasItemInQ();
        void StartOLADataProcessQueue();
        void StopOLADataProcessQueue();
        SequenceDataTypeEnum SequenceDataType { get; }
        void ClearQ();
        
    }

    


    public class SequenceOLADataProcess : ISequenceDataFeeder
    {
        public event EventHandler<EventArgs> OLADataProcessed;
        public event EventHandler<EventArgs> OLADataProcessStopped;
        Dictionary<SequenceOLADataKey, SortedDictionary<int, SeqenceMetricDataItem>> _FinalCycleDataListPerSurfaceAndLane;
        Dictionary<SequenceOLADataKey, SortedDictionary<int, RegionSeqenceMetricDataList>> _FinalRegionCycleDataListPerSurfaceAndLane;
        Dictionary<SequenceOLADataKey, int> _ListNumberOfRegionsPerLaneandSurface;
        Dictionary<string, int> _ListNumberOfRegionsPerLane; //for all surfaces per lane
        Dictionary<int, bool> _ListCycleFinished;//  key cycle
        AutoSetQueue<SequenceOLADataInfo> _OLADataInfoQ;
        Thread _OLADataInfoProcessingThread;
        int[] _FinalDataListsLock;
        ISeqLog Logger { get; }
        public bool IsAbort { get; set; }
        public int CurrentMaxCycle { get; set; }
        public SequenceDataTypeEnum SequenceDataType { get; }
        public SequenceOLADataProcess(ISeqLog logger, SequenceDataTypeEnum sequenceDataType)
        {
            Logger = logger;
            SequenceDataType = sequenceDataType;
            _FinalDataListsLock = new int[0];
            _FinalCycleDataListPerSurfaceAndLane = new Dictionary<SequenceOLADataKey, SortedDictionary<int, SeqenceMetricDataItem>>();
            _ListNumberOfRegionsPerLaneandSurface = new Dictionary<SequenceOLADataKey, int>();
            _ListNumberOfRegionsPerLane = new Dictionary<string, int>(); //key -- lane number
            _FinalRegionCycleDataListPerSurfaceAndLane = new Dictionary<SequenceOLADataKey, SortedDictionary<int, RegionSeqenceMetricDataList>>();
            _ListCycleFinished = new  Dictionary<int, bool>();
        }
        public void ClearQ()
        {
            _OLADataInfoQ?.Clear();
        }

        public void NewOLAData(SequenceOLADataInfo seqOLAData)
        {
            _OLADataInfoQ.Enqueue(seqOLAData);
        }

        public void StartOLADataProcessQueue()
        {
            if (_OLADataInfoQ == null && _OLADataInfoProcessingThread == null)
            {
                _OLADataInfoQ = new AutoSetQueue<SequenceOLADataInfo>();
                _OLADataInfoProcessingThread = new Thread(() => ProcessOLADataInfoInQ());
                _OLADataInfoProcessingThread.IsBackground = true;
                _OLADataInfoProcessingThread.Name = "OLADataDisplayProcess";
                _OLADataInfoProcessingThread.Start();
            }
            else
            {
                Logger.LogWarning("OLA Data Process Queue is already started");
            }
        }

        public bool HasItemInQ()
        {
            return _OLADataInfoQ?.HasItems == true;
        }

        public void StopOLADataProcessQueue()
        {
            _OLADataInfoQ?.Enqueue(null);
            if (_OLADataInfoProcessingThread?.IsAlive == true)
            {
                _OLADataInfoProcessingThread.Join();
            }
            _OLADataInfoProcessingThread = null;
            _OLADataInfoQ = null;
        }

        void ProcessOLADataInfoInQ()
        {
            Logger.Log(" Processing OLA Data thread starts");
            while (true)
            {
                if (IsAbort)
                {
                    Logger.Log("Aborted Processing OLA Data");
                    break;
                }
                SequenceOLADataInfo seqOLAData = _OLADataInfoQ.WaitForNextItem();
                if (seqOLAData != null)
                {
                    PrepareOLADataForDispaly(seqOLAData);
                    OLADataProcessed?.Invoke(this, new EventArgs());
                }
                else
                {
                    OLADataProcessStopped?.Invoke(this, new EventArgs());
                    break;
                }
            }
            Logger.Log(" Processing OLA Data thread exists");
        }

        double ParseDoubleValue(string str, double defaultValue = 0)
        {
            double v;// double.PositiveInfinity;
            if (!double.TryParse(str, out v))
            {
                v = defaultValue;
            }
            return v;
        }

        int ParseIntValue(string str, int defaultValue = 0)
        {
            int v;// double.PositiveInfinity;
            if (!int.TryParse(str, out v))
            {
                v = defaultValue;
            }
            return v;
        }

        int GetRowIndex(string rowName)
        {
            int row = -1;
            switch (rowName)
            {
                case "A":
                    row = 1;
                    break;
                case "B":
                    row = 2;
                    break;
                case "C":
                    row = 3;
                    break;
                case "D":
                    row = 4;
                    break;
            }
            return row;
        }
        
        void PrepareOLADataForDispaly(SequenceOLADataInfo seqOLAData)
        {
            Dictionary<SequenceOLADataKey, OLADataFileInfo> oLADataFileList = new Dictionary<SequenceOLADataKey, OLADataFileInfo>();
            //string pattern = @"^[bt]L\d\d{2}[ABCD]$";
            string namedPattern = @"^(?<Surface>[bt])L(?<Lane>\d)(?<column>\d{2})(?<row>[ABCD])$";
            //var dirs = Directory.GetDirectories(@"D:\OLA_Test_data\bcqc", "*", System.IO.SearchOption.TopDirectoryOnly).Where(dir => Regex.IsMatch(Path.GetFileName(dir), pattern));
            string surface;
            SurfaceDataEnum surfaceEnum;
            int lane, column, row;

            SequenceOLADataKey dataKey;
            int cycle;
            
            //sort data folders into a dictionary
            foreach (var it in seqOLAData.OLATileData)
            {
                var d = Path.Combine(it.FileLocationPath, it.DataFileName);

                string str = it.TileName;// Path.GetFileName(d);
                Match m = Regex.Match(str, namedPattern);
                if (m.Success)
                {
                    surface = m.Groups["Surface"].Value;
                    surfaceEnum = SurfaceDataEnum.Both;
                    if (surface == "b")
                    {
                        surfaceEnum = SurfaceDataEnum.Surface1;
                    }
                    else if (surface == "t")
                    {
                        surfaceEnum = SurfaceDataEnum.Surface2;
                    }

                    lane = int.Parse(m.Groups["Lane"].Value);
                    column = int.Parse(m.Groups["column"].Value);
                    row = GetRowIndex(m.Groups["row"].Value);
                    //string copiedDir = Path.GetDirectoryName(d);
                    //copiedDir = Path.Combine(copiedDir, str.Replace("bL", "tL"));
                    //copiedDir = Path.Combine(copiedDir, str.Replace("L1", "L2"));
                    //DirectoryInfo d1 = new DirectoryInfo(d);
                    //DirectoryInfo d2 = new DirectoryInfo(copiedDir);
                    //CopyEntireDirectory(d1, d2);
                    dataKey = new SequenceOLADataKey() { Surface = surfaceEnum, Lane = (LaneDataEnum)lane };
                    RegionInfo r = new RegionInfo()
                    {
                        Lane = lane,
                        Column = column,
                        Row = row,
                        Path = d,
                    };

                    if (!oLADataFileList.ContainsKey(dataKey))
                    {
                        oLADataFileList.Add(dataKey, new OLADataFileInfo()); //a list of csv file for title name and cycle
                    }
                    oLADataFileList[dataKey].AddDir(r);
                }
                lock (_FinalDataListsLock)
                {
                    cycle = it.Cycle;
                
                    if (_ListCycleFinished.ContainsKey(cycle))
                    {
                        //once cycle is marked as finished, it should remains finished all the time,
                        //so don't turn it back to unfinished
                        //i.e. if it.IsCycleFinished == false; don't set it as the default value in 
                        //listCycleFinishedForCurrentRead[cycle] is false.
                        if ((!_ListCycleFinished[cycle]) && it.IsCycleFinished)
                        {
                            _ListCycleFinished[cycle] = it.IsCycleFinished;
                        }
                    }
                    else
                    {
                        _ListCycleFinished.Add(cycle, it.IsCycleFinished);
                    }
                }
            } //for all OLA tile data

            const double percentageFactor = 100;
            const double intensictyFactor = 1000;
            string laneKeyNumber;
            lock (_FinalDataListsLock)
            {
                _ListNumberOfRegionsPerLaneandSurface.Clear();
                _ListNumberOfRegionsPerLane.Clear();
                _FinalCycleDataListPerSurfaceAndLane.Clear();
                //all data for cycles up to current total cycles per surface and lane
                // Dictionary<SequenceOLADataKey, Dictionary<int, SeqenceLineDataItem>> _FinalCycleDataListPerSurfaceAndLane = new Dictionary<SequenceOLADataKey, Dictionary<int, SeqenceLineDataItem>>();
                int maxCycle = 0;
                foreach (var it in oLADataFileList) //per lane and per surface
                {
                    //for each region in a surface and Lane; get all data for each cycle
                    //RegionSeqenceMetricDataList dataListForARegion = new RegionSeqenceMetricDataList();
                    SortedDictionary<int, RegionSeqenceMetricDataList> allRegionDataForCycle = new SortedDictionary<int, RegionSeqenceMetricDataList>();
                    foreach (var it2 in it.Value.RegionInfos)
                    {
                        var csvFileName = it2.Path;// Directory.GetFiles(it2, $"1_{cycle}_proc-int-bcqc.csv", System.IO.SearchOption.TopDirectoryOnly);
                        if (csvFileName != null && csvFileName.Length > 0)
                        {
                            try
                            {

                                using (TextFieldParser parser = new TextFieldParser(csvFileName))
                                {
                                    parser.TextFieldType = FieldType.Delimited;
                                    parser.SetDelimiters(",");


                                    int count = 0;
                                    while (!parser.EndOfData)
                                    {
                                        string[] fields = parser.ReadFields();
                                        count++;
                                        if (count > 2 && fields.Length >= 15)
                                        {
                                            int data_column = 0;
                                            int curCycle = int.Parse(fields[data_column++]);

                                            RegionSeqenceMetricDataList dataListForARegion = null;
                                            if (!allRegionDataForCycle.ContainsKey(curCycle))
                                            {
                                                dataListForARegion = new RegionSeqenceMetricDataList();
                                                allRegionDataForCycle[curCycle] = dataListForARegion;
                                            }
                                            else
                                            {
                                                dataListForARegion = allRegionDataForCycle[curCycle];
                                            }

                                            SeqenceMetricDataItem dIt = new SeqenceMetricDataItem()
                                            {
                                                Cycle = curCycle,
                                                Q30 = ParseDoubleValue(fields[data_column++]) * percentageFactor, //100
                                                Q20 = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                MedianQ = ParseDoubleValue(fields[data_column++]), //assume it is between 0 to 100
                                                NumberOfCluster = ParseIntValue(fields[data_column++]),
                                                PF = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                ErrorRate = ParseDoubleValue(fields[data_column++], 100.0), //assume it is already in percentage
                                                ABase = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                TBase = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                GBase = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                CBase = ParseDoubleValue(fields[data_column++]) * percentageFactor,
                                                AIntensity = ParseDoubleValue(fields[data_column++]) / intensictyFactor, //1000
                                                TIntensity = ParseDoubleValue(fields[data_column++]) / intensictyFactor,
                                                GIntensity = ParseDoubleValue(fields[data_column++]) / intensictyFactor,
                                                CIntensity = ParseDoubleValue(fields[data_column++]) / intensictyFactor,

                                            };
                                            //PF recalculated: cluster of last cycle / cluster in first cycle
                                            if(curCycle == 1) { dIt.PF = 100; }
                                            else
                                            {
                                                dIt.PF = (double)dIt.NumberOfCluster / (double)allRegionDataForCycle[1][it2].NumberOfCluster * 100;
                                            }
                                            dIt.Density = dIt.NumberOfCluster / SettingsManager.ConfigSettings.InstrumentInfo.FOV;
                                            dataListForARegion.Add(it2, dIt); 
                                        }

                                    } //while paring a file for each cycle row
                                }//using
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"failed to parse file {csvFileName} with exception: {ex.Message}");
                            }
                        }//if
                    } //for each region
                    //key == (lane, surface)  value -- a dictionary of cycle with a list of  values for each region 
                    _FinalRegionCycleDataListPerSurfaceAndLane[it.Key] = allRegionDataForCycle;
                    //key is cycle number
                    //**** for each cycle , calculate average for each value for all regions per surface*******.
                    SortedDictionary<int, SeqenceMetricDataItem> averageAllRegionMetricDataForCycle = new SortedDictionary<int, SeqenceMetricDataItem>();

                    //loop through all cycles
                    foreach (var it2 in allRegionDataForCycle)
                    {

                        if (maxCycle < it2.Key)
                        {
                            maxCycle = it2.Key;
                        }

                        //for a given cycle
                        //for through each region
                        SeqenceMetricDataItem averageDataItem = new SeqenceMetricDataItem() { Cycle = it2.Key };
                        
                        //go through all region per lane and surface
                        int numberOfRegions = it2.Value.Count;
                        if (!_ListNumberOfRegionsPerLaneandSurface.ContainsKey(it.Key))
                        {
                            _ListNumberOfRegionsPerLaneandSurface.Add(it.Key, numberOfRegions);
                        }
                        else
                        {
                            _ListNumberOfRegionsPerLaneandSurface[it.Key] = numberOfRegions;
                        }

                       

                        foreach (var it3 in it2.Value) //it3 key is RegionInfo
                        {
                            var v = it3.Value;
                            averageDataItem.Q20 += v.Q20;
                            averageDataItem.Q30 += v.Q30;
                            averageDataItem.MedianQ += v.MedianQ;
                            averageDataItem.NumberOfCluster += v.NumberOfCluster; //sum up for all regions
                            averageDataItem.PF += v.PF;
                            averageDataItem.ErrorRate += v.ErrorRate;
                            averageDataItem.ABase += v.ABase;
                            averageDataItem.TBase += v.TBase;
                            averageDataItem.GBase += v.GBase;
                            averageDataItem.CBase += v.CBase;
                            averageDataItem.AIntensity += v.AIntensity;
                            averageDataItem.TIntensity += v.TIntensity;
                            averageDataItem.GIntensity += v.GIntensity;
                            averageDataItem.CIntensity += v.CIntensity;
                            
                        }
                        averageDataItem.Q20 /= numberOfRegions;
                        averageDataItem.Q30 /= numberOfRegions;
                        averageDataItem.MedianQ /= numberOfRegions;
                        //remove average on NumberOfCluster
                        //averageDataOtem.NumberOfCluster /= numberOfRegions; //average on all regions

                        averageDataItem.PF /= numberOfRegions;
                        averageDataItem.ErrorRate /= numberOfRegions;
                        averageDataItem.ABase /= numberOfRegions;
                        averageDataItem.TBase /= numberOfRegions;
                        averageDataItem.GBase /= numberOfRegions;
                        averageDataItem.CBase /= numberOfRegions;
                        averageDataItem.AIntensity /= numberOfRegions;
                        averageDataItem.TIntensity /= numberOfRegions;
                        averageDataItem.GIntensity /= numberOfRegions;
                        averageDataItem.CIntensity /= numberOfRegions;

                        averageDataItem.Density = averageDataItem.NumberOfCluster / (SettingsManager.ConfigSettings.InstrumentInfo.FOV * numberOfRegions);
                        averageAllRegionMetricDataForCycle[it2.Key] = averageDataItem;
                    }//for each cycle
                    //key == (lane, surface) , value a dictionary of cycle with  average metric values for all regions
                    _FinalCycleDataListPerSurfaceAndLane[it.Key] = averageAllRegionMetricDataForCycle;
                } //for each surface and lane
                foreach (var it in _ListNumberOfRegionsPerLaneandSurface)
                {
                    laneKeyNumber = ((int)it.Key.Lane).ToString();
                    
                    if (!_ListNumberOfRegionsPerLane.ContainsKey(laneKeyNumber))
                    {
                        _ListNumberOfRegionsPerLane.Add(laneKeyNumber, it.Value);
                    }
                    else
                    {
                        _ListNumberOfRegionsPerLane[laneKeyNumber] += it.Value;
                    }
                }
                CurrentMaxCycle = maxCycle;
            }//end lock
        } //end method

        void CheckMinMax(ref double min, ref double max, double v)
        {
            if (min > v) { min = v; }
            if (max < v) { max = v; }
        }
        public int GetCurrentMaxCycle()
        {
            lock (_FinalDataListsLock)
            {
                {
                    return CurrentMaxCycle;
                }
            }
        }
        public LineMetricDataBuffer GetLineGraphData(LineDataKey lineType, bool checkCyccleFinished)
        {
            LineMetricDataBuffer graphData = null; // new LineMetricDataBuffer();
            double minY = double.MaxValue, maxY = double.MinValue;
            //prepare points
            lock (_FinalDataListsLock)
            {
                if ((!checkCyccleFinished) ||
                    ((_ListCycleFinished.ContainsKey(CurrentMaxCycle) && _ListCycleFinished[CurrentMaxCycle]))
                    )
                {
                    

                    graphData = new LineMetricDataBuffer();
                    if (lineType.Surface != SurfaceDataEnum.Both && lineType.Lane != LaneDataEnum.AllLane)
                    {
                        SequenceOLADataKey olaDataKey = new SequenceOLADataKey() { Surface = lineType.Surface, Lane = lineType.Lane };
                        if (_FinalCycleDataListPerSurfaceAndLane.ContainsKey(olaDataKey))
                        {
                            var dataList = _FinalCycleDataListPerSurfaceAndLane[olaDataKey];
                            double y = 0;
                            foreach (var it in dataList)
                            {
                                y = it.Value.GetMetricValue(lineType.Metric, lineType.Channel);
                                CheckMinMax(ref minY, ref maxY, y);
                                graphData.Add(it.Key, y);
                            }
                        }
                    }
                    else if (lineType.Surface != SurfaceDataEnum.Both && lineType.Lane == LaneDataEnum.AllLane)
                    {
                        Dictionary<LaneDataEnum, SortedDictionary<int, SeqenceMetricDataItem>> laneDataList =
                            new Dictionary<LaneDataEnum, SortedDictionary<int, SeqenceMetricDataItem>>();
                        LaneDataEnum[] laneEnums = (LaneDataEnum[])Enum.GetValues(typeof(LaneDataEnum));
                        SequenceOLADataKey seqOLADataKey = new SequenceOLADataKey()
                        {
                            Surface = lineType.Surface,

                        };


                        foreach (var lane in laneEnums)
                        {
                            if (lane != LaneDataEnum.AllLane)
                            {
                                seqOLADataKey.Lane = lane;
                                if (_FinalCycleDataListPerSurfaceAndLane.ContainsKey(seqOLADataKey))
                                {
                                    laneDataList.Add(lane, _FinalCycleDataListPerSurfaceAndLane[seqOLADataKey]);
                                }
                            }
                        }

                        double y;
                        int maxCycles = CurrentMaxCycle;
                        for (int cycle = 1; cycle <= maxCycles; cycle++)
                        {
                            y = 0;
                            int count = 0;
                            foreach (var it in laneDataList)
                            {
                                if (it.Value.ContainsKey(cycle))
                                {
                                    y += it.Value[cycle].GetMetricValue(lineType.Metric, lineType.Channel);
                                    count++;
                                }
                            }
                            if (count > 0)
                            {
                                y /= count;
                                graphData.Add(cycle, y);
                                CheckMinMax(ref minY, ref maxY, y);
                            }
                        }
                    }
                    else if (lineType.Surface == SurfaceDataEnum.Both && lineType.Lane != LaneDataEnum.AllLane)
                    {
                        Dictionary<SurfaceDataEnum, SortedDictionary<int, SeqenceMetricDataItem>> surfaceDataList =
                           new Dictionary<SurfaceDataEnum, SortedDictionary<int, SeqenceMetricDataItem>>();
                        SurfaceDataEnum[] surfaceEnums = (SurfaceDataEnum[])Enum.GetValues(typeof(SurfaceDataEnum));
                        SequenceOLADataKey seqOLADataKey = new SequenceOLADataKey()
                        {
                            Lane = lineType.Lane,
                        };

                        foreach (var it in surfaceEnums)
                        {
                            if (it != SurfaceDataEnum.Both)
                            {
                                seqOLADataKey.Surface = it;
                                if (_FinalCycleDataListPerSurfaceAndLane.ContainsKey(seqOLADataKey))
                                {
                                    surfaceDataList.Add(it, _FinalCycleDataListPerSurfaceAndLane[seqOLADataKey]);
                                }
                            }
                        }

                        int maxCycles = CurrentMaxCycle;
                        double y;
                        for (int cycle = 1; cycle <= maxCycles; cycle++)
                        {
                            y = 0;
                            int count = 0;
                            foreach (var it in surfaceDataList)
                            {
                                if (it.Value.ContainsKey(cycle))
                                {
                                    y += it.Value[cycle].GetMetricValue(lineType.Metric, lineType.Channel);
                                    count++;
                                }
                            }
                            if (count > 0)
                            {
                                y /= count;
                                graphData.Add(cycle, y);
                                CheckMinMax(ref minY, ref maxY, y);
                            }
                        }
                    }
                    else //lineType.Surface == SurfaceDataEnum.Both && lineType.Lane == LaneDataEnum.AllLane)
                    {
                        double y;
                        int maxCycles = CurrentMaxCycle;
                        for (int cycle = 1; cycle <= maxCycles; cycle++)
                        {
                            y = 0;
                            int count = 0;
                            foreach (var it in _FinalCycleDataListPerSurfaceAndLane)
                            {
                                if (it.Value.ContainsKey(cycle))
                                {
                                    y += it.Value[cycle].GetMetricValue(lineType.Metric, lineType.Channel);
                                    count++;
                                }
                            }
                            if (count > 0)
                            {
                                y /= count;
                                graphData.Add(cycle, y);
                                CheckMinMax(ref minY, ref maxY, y);
                            }
                        }
                    }//else
                }
                else
                {
                    int ok = 0;
                    ok = 1;
                }
            }
            if (graphData != null && minY <= maxY)
            {
                graphData.Yrange = new MinMaxRange() { Min = minY, Max = maxY };
            }
            return graphData;
        }

        
        public List<SequenceDataTableItems> GetSequenceDataTableItems(bool checkCyccleFinished)
        {
            List<SequenceDataTableItems> sequenceDataTableItems = null;// new List<SequenceDataTableItems>();
            //string key is lane number
            Dictionary<string, SequenceDataTableItems> laneSequenceDataTableItems = null;// new Dictionary<string, SequenceDataTableItems>();

            lock (_FinalDataListsLock)
            {
                if ((!checkCyccleFinished) ||  
                    ((_ListCycleFinished.ContainsKey(CurrentMaxCycle) && _ListCycleFinished[CurrentMaxCycle]))
                    )
                {
                    sequenceDataTableItems = new List<SequenceDataTableItems>();
                    laneSequenceDataTableItems = new Dictionary<string, SequenceDataTableItems>();
                    int maxCycles = CurrentMaxCycle;
                    int readLength = maxCycles;
                    double v;
                    string key;
                    for (int cycle = maxCycles; cycle <= maxCycles; cycle++)
                    {
                        //note: _FinalCycleDataListPerSurfaceAndLane has no AllLane key
                        //key == (lane, surface) , value a dictionary of cycle with  average metric values for all regions
                        foreach (var it in _FinalCycleDataListPerSurfaceAndLane)
                        {
                            //for each lane sum up all surfaces

                            if (it.Key.Lane != LaneDataEnum.AllLane &&
                                it.Key.Surface != SurfaceDataEnum.Both &&
                                it.Value.ContainsKey(cycle))
                            {
                                int numberOfRegionPerLaneAndSurface = _ListNumberOfRegionsPerLaneandSurface[it.Key];

                                SeqenceMetricDataItem averageOnAllRegionPerLaneSurfaceItem = it.Value[cycle];
                                key = ((int)it.Key.Lane).ToString();
                                SequenceDataTableItems tableItems;
                                if (!laneSequenceDataTableItems.ContainsKey(key))
                                {
                                    tableItems = new SequenceDataTableItems();
                                    laneSequenceDataTableItems.Add(key, tableItems);
                                    tableItems.CycleCounts++;
                                    tableItems.ErrorRateCycelCounts++;
                                }
                                else
                                {
                                    tableItems = laneSequenceDataTableItems[key];
                                }
                                tableItems.Lane = key;
                                if (tableItems.Cycles < cycle)
                                {
                                    tableItems.Cycles = cycle;
                                }

                                //tableItems.CycleCounts++;
                                v = averageOnAllRegionPerLaneSurfaceItem.GetMetricValue(MetricsDataEnum.PF, ChannelDataEnum.All);
                                //tableItems.PF += v * numberOfRegionPerLaneAndSurface;

                                v = averageOnAllRegionPerLaneSurfaceItem.GetMetricValue(MetricsDataEnum.Q30, ChannelDataEnum.All);
                                tableItems.Q30 += v * numberOfRegionPerLaneAndSurface;

                                v = averageOnAllRegionPerLaneSurfaceItem.GetMetricValue(MetricsDataEnum.ErrorRate, ChannelDataEnum.All);
                                //tableItems.ErrorRate += v * numberOfRegionPerLaneAndSurface;
                                //tableItems.ErrorRateCycelCounts++;

                                //density = total Number of cluster (sum cross regions not cycle ) / total area(FOV * num of regions)
                                v = averageOnAllRegionPerLaneSurfaceItem.GetMetricValue(MetricsDataEnum.Density, ChannelDataEnum.All);
                                tableItems.Density += v * numberOfRegionPerLaneAndSurface;

                                //each lane
                                v = averageOnAllRegionPerLaneSurfaceItem.NumberOfCluster; //this value was not average on number of regions
                                tableItems.ReadsPF += v;  //PF(Total number of Cluster ) 
                            }//if

                        }//for each lane  and surface
                    } //for cycle

                    SequenceDataTableItems tableItemForAllLane = null;
                    int lanes = laneSequenceDataTableItems.Count;
                    if (lanes > 1)
                    {
                        tableItemForAllLane = new SequenceDataTableItems();
                        tableItemForAllLane.Read = 1;
                        tableItemForAllLane.Lane = "Total";
                    }

                    //key == lane
                    foreach (var it in laneSequenceDataTableItems)
                    {
                        SequenceDataTableItems tableItems = it.Value;
                        tableItems.Read = 1;
                        tableItems.SetAverageValues(_ListNumberOfRegionsPerLane[it.Key]); //for all regions in both surfaces 
                        tableItems.SetAverageValuesUsingCycleCount();
                        //Yield(total number of clusters * cycle number )
                        tableItems.Yield = readLength * tableItems.ReadsPF;

                        if (lanes > 1)
                        {
                            if (tableItemForAllLane.Cycles < tableItems.Cycles)
                            {
                                tableItemForAllLane.Cycles = tableItems.Cycles;
                            }
                            //tableItemForAllLane.PF += tableItems.PF;
                            tableItemForAllLane.Q30 += tableItems.Q30;
                            tableItemForAllLane.ReadsPF += tableItems.ReadsPF;
                            //tableItemForAllLane.ErrorRate += tableItems.ErrorRate;
                            tableItemForAllLane.Density += tableItems.Density;
                            tableItemForAllLane.Yield += tableItems.Yield;
                        }
                        sequenceDataTableItems.Add(tableItems);
                    } //for each

                    if (lanes > 1)
                    {

                        tableItemForAllLane.SetAverageValues(lanes); //for all lanes
                        sequenceDataTableItems.Add(tableItemForAllLane);
                    }
                }
                else
                {
                    int ok = 0;
                    ok = 1;
                }
            }
            return sequenceDataTableItems;
        }
        public bool FillMapData(int lane, MapDataKey mapDataKey, HeatMapData mapData, bool fillDefaultIfNoData = false)
        {
            double[,] f = mapData.Data;
            int NX = f.GetLength(0);
            int NY = f.GetLength(1);
            bool filled = false;
            double minV = double.MaxValue;
            double maxV = double.MinValue;
            lock (_FinalDataListsLock)
            {
                Dictionary<SurfaceDataEnum, RegionSeqenceMetricDataList> regionSeqenceMetricDataLists = new Dictionary<SurfaceDataEnum, RegionSeqenceMetricDataList>();
                SurfaceDataEnum[] surfaceList;
                if (mapDataKey.Surface == SurfaceDataEnum.Both)
                {
                    surfaceList = new SurfaceDataEnum[] { SurfaceDataEnum.Surface1, SurfaceDataEnum.Surface2 };

                }
                else
                {
                    surfaceList = new SurfaceDataEnum[] { mapDataKey.Surface };
                }

                foreach (var it in surfaceList)
                {
                    SequenceOLADataKey dataKey = new SequenceOLADataKey()
                    {
                        Lane = (LaneDataEnum)lane,
                        Surface = it,
                    };
                    if (_FinalRegionCycleDataListPerSurfaceAndLane.ContainsKey(dataKey))
                    {
                        if (_FinalRegionCycleDataListPerSurfaceAndLane[dataKey].ContainsKey(mapDataKey.Cycle))
                        {
                            regionSeqenceMetricDataLists.Add(it, _FinalRegionCycleDataListPerSurfaceAndLane[dataKey][mapDataKey.Cycle]);
                        }
                    }
                }

                if (regionSeqenceMetricDataLists.Count > 0)
                {
                    foreach (var it in regionSeqenceMetricDataLists)
                    {
                        if (it.Value.Count > 0)
                        {
                            filled = true;
                            break;
                        }
                    }
                }


                if (filled)
                {
                    RegionInfo regionKey = new RegionInfo();
                    regionKey.Lane = lane;
                    int count;
                    double v;
                    MetricsDataEnum metric = mapDataKey.Metric;
                    ChannelDataEnum channel = mapDataKey.Channel;
                    List<ChannelDataEnum> channels = new List<ChannelDataEnum>((ChannelDataEnum[])Enum.GetValues(typeof(ChannelDataEnum)));
                    channels.Remove(ChannelDataEnum.All);

                    for (int i = 0; i < NX; i++) //column
                    {
                        regionKey.Column = i + 1;
                        for (int j = 0; j < NY; j++) //row
                        {
                            regionKey.Row = j + 1;
                            count = 0;
                            v = double.NaN;
                            foreach (var it2 in regionSeqenceMetricDataLists) //for surface list
                            {
                                if (it2.Value.ContainsKey(regionKey))
                                {
                                    if (metric == MetricsDataEnum.Intensity || metric == MetricsDataEnum.Base)
                                    {
                                        if (channel == ChannelDataEnum.All)
                                        {
                                            if (double.IsNaN(v))
                                            {
                                                v = 0;
                                            }

                                            foreach (var ch in channels)
                                            {
                                                v += it2.Value[regionKey].GetMetricValue(metric, ch);
                                            }
                                            //v /= channels.Count;
                                        }
                                        else
                                        {
                                            if (double.IsNaN(v))
                                            {
                                                v = 0;
                                            }
                                            v += it2.Value[regionKey].GetMetricValue(metric, channel);
                                        }
                                    }
                                    else
                                    {
                                        if (double.IsNaN(v))
                                        {
                                            v = 0;
                                        }
                                        v += it2.Value[regionKey].GetMetricValue(metric, ChannelDataEnum.All);
                                    }
                                    count++;

                                }
                            } //foreach it2 for surface list

                            if (count > 0)
                            {
                                v /= count;
                                if (metric == MetricsDataEnum.Intensity || metric == MetricsDataEnum.Base)
                                {
                                    if (channel == ChannelDataEnum.All)
                                    {
                                        v /= channels.Count;
                                    }
                                }
                            }
                            if (!double.IsNaN(v))
                            {
                                if (minV > v)
                                {
                                    minV = v;
                                }
                                if (maxV < v)
                                {
                                    maxV = v;
                                }
                            }
                            f[i, j] = v;
                        }
                    }
                    mapData.DataBound = new MinMaxRange() { Min = minV, Max = maxV };
                    //mapData.IsMNormalzed = false;
                    //mapData.MaxMarkValue = maxMarkV;
                }
                else if (fillDefaultIfNoData)
                {
                    for (int i = 0; i < NX; i++) //column
                    {
                        for (int j = 0; j < NY; j++) //row
                        {
                            f[i, j] = double.NaN;
                        }
                    }
                    mapData.DataBound = new MinMaxRange() { Min = double.NaN, Max = double.NaN };
                    filled = true;
                }
                return filled;
            }
        }
    }
}
