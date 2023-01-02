using InteractiveDataDisplay.WPF;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.UI.ViewModel
{
    public class DataByTileViewModel : ViewModelBase
    {
        ISeqLog Logger { get; }
        ISequenceDataFeeder _DataFeeder;
        public DataByTileViewModel(ISeqLog logger, ISequenceDataFeeder dataFeeder)
        {
            Logger = logger;
            _DataFeeder = dataFeeder;
        }
        #region HEAT_MAPS
        //-------------heater maps----------------------------------------------
        ChannelDataEnum _ChannelItem = ChannelDataEnum.All;
        public ChannelDataEnum ChannelItem
        {
            get => _ChannelItem;
            set
            {
                if (SetProperty(ref _ChannelItem, value))
                {
                    OnUpdateMapDataSelectionChanged();
                }
            }
        }

        ObservableCollection<DecoratedItem<int>> _Cycles;
        public ObservableCollection<DecoratedItem<int>> Cycles { get => _Cycles; set => SetProperty(ref _Cycles, value); }
        DecoratedItem<int> _SelectedCycle;
        public DecoratedItem<int> SelectedCycle
        {
            get => _SelectedCycle;
            set
            {
                if (SetProperty(ref _SelectedCycle, value))
                {
                    OnUpdateMapDataSelectionChanged(SelectionChangesEnum.CycleChanged);
                }
            }
        }

        bool _IsBaseRelatedMetric;
        public bool IsBaseRelatedMetric { get => _IsBaseRelatedMetric; set => SetProperty(ref _IsBaseRelatedMetric, value); }
        MetricsDataEnum _MetricsDataItem = MetricsDataEnum.Q20;
        public MetricsDataEnum MetricsDataItem
        {
            get => _MetricsDataItem;
            set
            {
                if (SetProperty(ref _MetricsDataItem, value))
                {
                    IsBaseRelatedMetric = _MetricsDataItem == MetricsDataEnum.Base || _MetricsDataItem == MetricsDataEnum.Intensity;

                    lock (_MapDataLock)
                    {
                        MapMarkValuesModel mapMarkValues = GetDefaultMapMarkValue(_MetricsDataItem);
                        HeatMapMarkValues.CopyFrom(mapMarkValues);

                    }
                    OnUpdateMapDataSelectionChanged(SelectionChangesEnum.MetricChanged);
                    MapCurrentMetricDescription = $"{_DataFeeder.SequenceDataType.Description()}: {_MetricsDataItem.Description()} {SeqenceMetricDataItem.GetMetricUnitDisplayName(_MetricsDataItem)}";
                }
            }
        }
        public static List<MetricsDataEnum> GetMetricsDataItemListForHeatmap()
        {
            var lst = Enum.GetValues(typeof(MetricsDataEnum))
                    .Cast<MetricsDataEnum>()
                    .Where(e => e != MetricsDataEnum.PF && e != MetricsDataEnum.ErrorRate).ToList();

            return lst;
        }
        public List<DecoratedItem<MetricsDataEnum>> MetricsDataItemList2
        {
            get
            {
                var lst = GetMetricsDataItemListForHeatmap();
                List<DecoratedItem<MetricsDataEnum>> metricsDataItemList = new List<DecoratedItem<MetricsDataEnum>>();
                foreach (var it in lst)
                {
                    metricsDataItemList.Add(new DecoratedItem<MetricsDataEnum>(it, it.Description()));
                }

                return metricsDataItemList;
            }
        }
        MapMarkValuesModel GetDefaultMapMarkValue(MetricsDataEnum metric)
        {
            double maxMarkValue = 0;
            double minMarkValue = 0;
            int markCount = 0;
            switch (metric)
            {
                case MetricsDataEnum.Intensity:
                    maxMarkValue = UInt16.MaxValue / 1000.0;
                    markCount = 100;
                    break;
                case MetricsDataEnum.Density:
                    maxMarkValue = 1500;
                    markCount = 1000;
                    break;
                case MetricsDataEnum.Q20:
                case MetricsDataEnum.Q30:
                case MetricsDataEnum.PF:
                case MetricsDataEnum.Base:
                case MetricsDataEnum.MedianQscore:
                case MetricsDataEnum.ErrorRate:
                    maxMarkValue = 100;
                    markCount = 100;
                    break;
                default:

                    maxMarkValue = 100;
                    markCount = 100;
                    break;
            }
            return new MapMarkValuesModel()
            {
                MinMarkValue = minMarkValue,
                MaxMarkValue = maxMarkValue,
                MarkCount = markCount,
                MarkWidth = maxMarkValue - minMarkValue
            };
        }

        string _MapCurrentMetricDescription;
        public string MapCurrentMetricDescription { get => _MapCurrentMetricDescription; set => SetProperty(ref _MapCurrentMetricDescription, value); }

        SurfaceDataEnum _SurfaceDataItem = SurfaceDataEnum.Surface1;
        public SurfaceDataEnum SurfaceDataItem
        {
            get => _SurfaceDataItem;
            set
            {
                if (SetProperty(ref _SurfaceDataItem, value))
                {
                    OnUpdateMapDataSelectionChanged(SelectionChangesEnum.SurfaceChanged);
                }
            }
        }
        public  void InitCycles(int cycle = -1)
        {
            Cycles = new ObservableCollection<DecoratedItem<int>>();
        }
        bool AddCycle(int cycle)
        {
            bool added = false;
            var cycleList = new ObservableCollection<DecoratedItem<int>>(Cycles);
            var it = cycleList.Where((x) => x.Value == cycle);
            if (it.Count() <= 0)
            {
                int curLastcycle = 0;
                if (cycleList.Count > 0)
                {
                    curLastcycle = cycleList[cycleList.Count - 1].Value;
                }
                DecoratedItem<int> item = null;
                for (int i = curLastcycle + 1; i <= cycle; i++)
                {
                    item = new DecoratedItem<int>(i, $"Cycle {i}");
                    cycleList.Add(item);
                }
                Cycles = cycleList;
                SelectedCycle = item;
                added = true;
            }
            return added;

        }
       
        public void UpdateHeatMapOnCycles(int cycles, int totalCycles)
        {
            if (cycles > 0 && cycles <= totalCycles) //SequenceApp.SequenceInformation.Cycles)
            {

                if (!AddCycle(cycles)) //old cycle
                {
                    OnUpdateMapDataSelectionChanged();
                }
            }
        }
        int[] _SlectedMapDataKeyLock = new int[0];
        MapDataKey SlectedMapDataKey { get; set; }
        [Flags]
        enum SelectionChangesEnum
        {
            None = 0,
            MetricChanged = 0x01,
            SurfaceChanged = 0x02,
            CycleChanged = 0x04,
            FitToData = 0x08,
        }
        void OnUpdateMapDataSelectionChanged(SelectionChangesEnum slectedChanges = SelectionChangesEnum.None)
        {
            MetricsDataEnum metric;
            ChannelDataEnum channel;
            SurfaceDataEnum surface;
            int cycle;
            lock (_SlectedMapDataKeyLock)
            {
                metric = MetricsDataItem;
                channel = ChannelItem;
                surface = SurfaceDataItem;
                if (SelectedCycle == null)
                {
                    cycle = 0;
                }
                else
                {
                    cycle = SelectedCycle.Value;
                }
                SlectedMapDataKey = new MapDataKey()
                {
                    Metric = metric,
                    Cycle = cycle,
                    Channel = channel,
                    Surface = surface,
                };
            }
            UpdateAllHeaterMapsAsync(slectedChanges);
        }

        private AutoResetEvent _MapRenderComplete = new AutoResetEvent(true);
        int _MaxNumLanes;
        int MaxNumLanes { get => _MaxNumLanes; }
        MapMarkValuesModel _HeatMapMarkValues = new MapMarkValuesModel() { MinMarkValue = 0, MaxMarkValue = 100, MarkCount = 100, MarkWidth = 100 };
        public MapMarkValuesModel HeatMapMarkValues { get=> _HeatMapMarkValues; set=>SetProperty(ref _HeatMapMarkValues, value) ; }
        MapXYModel _MapXYSetting = new MapXYModel();
        public MapXYModel MapXYSetting { get=> _MapXYSetting; set=>SetProperty(ref _MapXYSetting, value); } 
        //key lane number
        Dictionary<int, HeatMapData> _HeatMapDataList;

        int[] _MapDataLock = new int[0];
        ObservableCollection<HeatmapGraph> _HeatMaps;
        public ObservableCollection<HeatmapGraph> HeatMaps { get => _HeatMaps; set => SetProperty(ref _HeatMaps, value); }
        HeatmapGraph _MapMarks;
        public HeatmapGraph MapMarks { get => _MapMarks; set => SetProperty(ref _MapMarks, value); }

        //for array HeatMaps
        //index is 0 to MaxNumLanes - 1
        HeatMapData HeatMapData(int heatmapIndex) => _HeatMapDataList[heatmapIndex];

        public void Clear()
        {
            HeatMaps = new ObservableCollection<HeatmapGraph>();
        }
        public void BuildHeatMaps(int maxRows, int maxColumns, int maxLanes)
        {
            HeatMaps = new ObservableCollection<HeatmapGraph>();
            _MaxNumLanes = maxLanes;
            int NX = maxColumns;
            int NY = maxRows;
            TileRatio = NY / (double)NX;
            Palette defaultHeat = Palette.Heat;
            Palette heatPalette = new Palette(false, defaultHeat.Range, defaultHeat.Points);
            List<HeatmapGraph> heatMaps = new List<HeatmapGraph>();
            _HeatMapDataList = new Dictionary<int, HeatMapData>();
            int lane;
            Dispatch(() =>
            {
                for (int i = 0; i < MaxNumLanes; i++)
                {
                    lane = maxLanes - i;
                    HeatmapGraph hm = new HeatmapGraph() { Palette = heatPalette, AspectRatio = 1.0 }; //up-down from 0 to  maxLanes-1, the lane# shall be from maxLens to 1
                    hm.TooltipContentFunc = HeaterMapTooltipContent;
                    heatMaps.Add(hm);
                    _HeatMapDataList.Add(i, new HeatMapData() { Data = new double[NX, NY], Lane = lane });

                }
                MapMarks = new HeatmapGraph() { Palette = defaultHeat };
            });
            HeatMaps = new ObservableCollection<HeatmapGraph>(heatMaps);

            double[] XMap = new double[NX + 1];
            double[] YMap = new double[NY + 1];
            for (int i = 0; i <= NX; i++)
                XMap[i] = i;
            for (int j = 0; j <= NY; j++)
                YMap[j] = j;

            MapXYSetting.NXMap = NX;
            MapXYSetting.NYMap = NY;
            MapXYSetting.XMap = XMap;
            MapXYSetting.YMap = YMap;
            MapXYSetting.XMapMin = 0;
            MapXYSetting.XMapWidth = NX - MapXYSetting.XMapMin;
            MapXYSetting.YMapMin = 0;
            MapXYSetting.YMapHeight = NY - MapXYSetting.YMapMin;

            //MapMarks = new HeatmapGraph() { Palette = defaultHeat };
        }

        void UpdateMapMarks(MapMarkValuesModel mapMarkValues)
        {
            try
            {
                this.Dispatch(() =>
                {
                    MapMarks = new HeatmapGraph() { Palette = Palette.Heat };
                    //if (MapMarks != null)
                    {
                        // MapMarkValuesModel mapMarkValues = HeatMapMarkValues.Clone() as MapMarkValuesModel;
                        int NY0 = 3;//column
                        int NX0 = mapMarkValues.MarkCount;
                        double[] x0 = new double[NX0 + 1];
                        double[] y0 = new double[NY0 + 1];
                        double[,] f0 = new double[NX0, NY0];
                        double diff = mapMarkValues.MaxMarkValue - mapMarkValues.MinMarkValue;
                        double start = mapMarkValues.MinMarkValue;
                        for (int i = 0; i <= NX0; i++)
                            x0[i] = start + (i * diff / NX0);
                        for (int j = 0; j <= NY0; j++)
                            y0[j] = j;
                        for (int i = 0; i < NX0; i++)
                            for (int j = 0; j < NY0; j++)
                                f0[i, j] = start + (i * diff / (NX0 - 1)); //not normalize

                        //_MapRenderComplete.WaitOne();
                        //PlotDataDel handler = PlotData;
                        //this.Dispatch( () => PlotData( MapMarks, x0, y0, f0, "MapMark"));
                        MapMarks.Plot(f0, x0, y0);
                    }
                });

            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to UpdateMapMarks with exception error: {ex.Message}");

            }
        }

        void UpdateHeatMaps(MapDataKey mapDataKey, bool fillDefaultIfNoData, bool includeMarkMap)
        {
            bool fitToData = false;

            bool[] fillList = new bool[MaxNumLanes];
            //key -- map index
            Dictionary<int, HeatMapDisplayData> heatMapDisplayList = new Dictionary<int, HeatMapDisplayData>();
            bool hasError = false;
            try
            {
                lock (_MapDataLock)
                {
                    HeatMapData heatMapData;
                    for (int mapIndex = 0; mapIndex < MaxNumLanes; mapIndex++)
                    {
                        heatMapData = HeatMapData(mapIndex);
                        fillList[mapIndex] = _DataFeeder.FillMapData(heatMapData.Lane, mapDataKey, heatMapData, fillDefaultIfNoData);
                    }
                    ///static one
                    MapMarkValuesModel mapMarkValues = GetDefaultMapMarkValue(mapDataKey.Metric);
                    double minV, maxV;
                    double diff;
                    int newMarkcount = mapMarkValues.MarkCount;
                    double defaultMaxMarkValue = mapMarkValues.MaxMarkValue;
                    double defaultMinMarkValue = mapMarkValues.MinMarkValue;

                    if (UsingDynamicRange)
                    {
                        minV = double.MaxValue;
                        maxV = double.MinValue;

                        for (int mapIndex = 0; mapIndex < MaxNumLanes; mapIndex++)
                        {
                            HeatMapData data = HeatMapData(mapIndex);
                            if (fillList[mapIndex] && data.DataBound.IsValidRange)
                            {
                                if (minV > data.DataBound.Min)
                                {
                                    minV = data.DataBound.Min;
                                }
                                if (maxV < data.DataBound.Max)
                                {
                                    maxV = data.DataBound.Max;
                                }
                            }
                        }//for

                        if (minV == double.MaxValue || maxV == double.MinValue)
                        {
                            maxV = defaultMaxMarkValue;
                            minV = defaultMinMarkValue;
                        }
                        else
                        {
                            diff = maxV - minV;
                            if (diff < 1)
                            {
                                minV -= 0.5;
                                maxV += 0.5;
                                if (minV < 0)
                                {
                                    maxV -= minV;
                                    minV = 0;
                                }
                            }
                            minV = (int)(minV - minV / 3);
                            maxV = (int)(maxV + minV / 3);
                            //compare with default
                            if (minV < defaultMinMarkValue)
                            {
                                minV = defaultMinMarkValue;
                            }
                            if (maxV > defaultMaxMarkValue)
                            {
                                maxV = defaultMaxMarkValue;
                            }


                            //calculate markcount
                            diff = maxV - minV;
                            if (diff <= 5)
                            {
                                newMarkcount = 40;
                            }
                            else if (diff <= 100)
                            {
                                newMarkcount = 60;
                            }
                            else if (diff <= 500)
                            {
                                newMarkcount = 100;
                            }
                            else if (diff <= 1000)
                            {
                                newMarkcount = 500;
                            }
                            else
                            {
                                newMarkcount = 1000;
                            }
                        }//else
                        HeatMapMarkValues.MinMarkValue = minV;
                        HeatMapMarkValues.MaxMarkValue = maxV;
                        HeatMapMarkValues.MarkWidth = maxV - minV;
                        HeatMapMarkValues.MarkCount = newMarkcount;
                        fitToData = true;
                    }//if dynamic
                    else //static
                    {
                        maxV = defaultMaxMarkValue;
                        minV = defaultMinMarkValue;
                    }

                    double[,] fp, f;

                    double[] XMap, YMap;
                    int NX, NY;
                    diff = maxV - minV;

                    for (int mapIndex = 0; mapIndex < MaxNumLanes; mapIndex++)
                    {
                        if (fillList[mapIndex])
                        {
                            heatMapData = HeatMapData(mapIndex);
                            f = heatMapData.Data;
                            NX = f.GetLength(0);
                            NY = f.GetLength(1);
                            fp = new double[NX, NY];

                            for (int i = 0; i < NX; i++) //column
                            {
                                for (int j = 0; j < NY; j++) //row
                                {
                                    fp[i, j] = (f[i, j] - minV) / diff; //normalized
                                }
                            }
                            XMap = MapXYSetting.XMap.Clone() as double[];
                            YMap = MapXYSetting.YMap.Clone() as double[];
                            heatMapDisplayList.Add(mapIndex,
                                new HeatMapDisplayData()
                                {
                                    Data = fp,
                                    XMap = XMap,
                                    YMap = YMap,
                                    DataBound = new MinMaxRange() { Min = minV, Max = maxV },
                                    Lane = heatMapData.Lane,
                                });
                        } //if
                    } //for

                } //lock
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to UpdateHeatMaps with exception error: {ex.Message}");
                hasError = true;
            }

            if (!hasError)
            {
                if (includeMarkMap || fitToData)
                {
                    MapMarkValuesModel mapMarkValues = HeatMapMarkValues.Clone() as MapMarkValuesModel;
                    UpdateMapMarks(mapMarkValues);
                }
                foreach (var it in heatMapDisplayList)
                {
                    try
                    {
                        _MapRenderComplete.WaitOne();
                        HeatmapGraph heatmap = HeatMaps[it.Key];
                        //PlotDataDel handler = PlotData;
                        //Application.Current.Dispatcher.BeginInvoke(handler, heatmap, it.Value.XMap, it.Value.YMap, it.Value.Data, $"Heatmap{it.Key}");
                        this.Dispatch(() => PlotData(heatmap, it.Value.XMap, it.Value.YMap, it.Value.Data, $"Heatmap{it.Key}"));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"failed to plot heatmap {it.Key} with exception error: {ex.Message}");
                    }
                }
            }
        }


        private void UpdateAllHeaterMapsAsync(SelectionChangesEnum slectedChanges)
        {
            MapDataKey mapDataKey;
            bool includeMarkMap = ((slectedChanges & SelectionChangesEnum.MetricChanged) == SelectionChangesEnum.MetricChanged) ||
                ((slectedChanges & SelectionChangesEnum.FitToData) == SelectionChangesEnum.FitToData);
            bool fillDefaultIfNoData = ((slectedChanges & SelectionChangesEnum.SurfaceChanged) == SelectionChangesEnum.SurfaceChanged) ||
                ((slectedChanges & SelectionChangesEnum.CycleChanged) == SelectionChangesEnum.CycleChanged);
            lock (_SlectedMapDataKeyLock)
            {
                mapDataKey = SlectedMapDataKey;
            }
            if (mapDataKey == null)
            {
                return;
            }

            Task.Run(() =>
            {
                UpdateHeatMaps(mapDataKey, fillDefaultIfNoData, includeMarkMap);
            });
        }
        private delegate void PlotDataDel(HeatmapGraph hp, double[] x, double[] y, double[,] data, string mapName);
        private void PlotData(HeatmapGraph heatmap, double[] xp, double[] yp, double[,] fp, string mapName)
        {
            // HeatmapGraph objects prepare images to be drawn in a background thread. 
            // The Plot method cancels current incomplete images before starting a new one. 
            // This increases responsiveness of the UI but may result in loss of certain 
            // or even all of the frames.
            // The following code shows how to wait until a certain data is actually drawn.
            try
            {
                long id = heatmap.Plot(fp, xp, yp); // receive a unique operation identifier
                heatmap.RenderCompletion // an observable of completed and canceled operations
                    .Where(rc => rc.TaskId == id) // filter out an operation with the known id
                    .Subscribe(dummy => { OnMapPlotted(); }, (o) => OnMapPlotError(o)); // signal when the id is observed
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to plot {mapName} with exception error: {ex.Message}");
                _MapRenderComplete.Set();
            }
        }

        void OnMapPlotted()
        {
            try
            {
                _MapRenderComplete.Set();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Heat Plot error: {ex.Message}");
                _MapRenderComplete.Set();
            }
        }
        void OnMapPlotError(Exception ex)
        {
            Logger.LogError($"Catched Heat Plot error: {ex.Message}");
            _MapRenderComplete.Set();
        }
        Object HeaterMapTooltipContent(Point pt)
        {
            int x = (int)Math.Ceiling(HeatMaps[CurrrentMap].XFromLeft(pt.X));
            int y = (int)Math.Ceiling(HeatMaps[CurrrentMap].YFromTop(pt.Y));
            double vd;
            string vdStr;

            lock (_MapDataLock)
            {
                string unit = SeqenceMetricDataItem.GetMetricUnitDisplayName(MetricsDataItem);
                if (x >= 1 && x <= _HeatMapDataList[CurrrentMap].Data.GetLongLength(0) &&
                    y >= 1 && y <= _HeatMapDataList[CurrrentMap].Data.GetLongLength(1))
                {
                    vd = _HeatMapDataList[CurrrentMap].Data[x - 1, y - 1];
                    if (double.IsNaN(vd))
                    {
                        vdStr = "N/A";
                    }
                    else
                    {
                        //vd *= MaxMarkValue;
                        vdStr = string.Format("{0:F2}{1}", vd, unit);//{ $"{vd:F2}{unit}";
                    }
                    return $"(L{MaxNumLanes - CurrrentMap}, R{y}, C{x}): {vdStr}";
                }
                else
                {
                    return null;
                }
            }

        }

        int _CurrrentMap = 0;
        public int CurrrentMap { get => _CurrrentMap; set => SetProperty(ref _CurrrentMap, value); }
        bool _UsingDynamicRange;
        public bool UsingDynamicRange
        {
            get => _UsingDynamicRange;
            set
            {
                if (SetProperty(ref _UsingDynamicRange, value))
                {
                    if (!_UsingDynamicRange)
                    {
                        MapMarkValuesModel mapMarkValues = GetDefaultMapMarkValue(MetricsDataItem);
                        HeatMapMarkValues.CopyFrom(mapMarkValues);
                    }
                    UpdateAllHeaterMapsAsync(SelectionChangesEnum.FitToData);
                }
            }
        }
        //ShowTileGridLines
        bool _ShowTileGridLines = true;
        public bool ShowTileGridLines { get => _ShowTileGridLines; set => SetProperty(ref _ShowTileGridLines, value); }
        double _TileRatio;
        public double TileRatio { get => _TileRatio; set => SetProperty(ref _TileRatio, value); }
        int _MaxWidthHeatmap = 530;
        public int MaxWidthHeatmap { get => _MaxWidthHeatmap; set => SetProperty(ref _MaxWidthHeatmap, value); }
        #endregion //HETAER_MAPS
    }
}
