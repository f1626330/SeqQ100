using InteractiveDataDisplay.WPF;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace Sequlite.UI.ViewModel
{
    public class DataByCycleViewModel : ViewModelBase
    {
        ISeqLog Logger { get; }
        ISequenceDataFeeder _DataFeeder;
        public DataByCycleViewModel(ISeqLog logger, ISequenceDataFeeder dataFeeder) 
        {
            Logger = logger;
            _DataFeeder = dataFeeder;
            DataTypeString = _DataFeeder.SequenceDataType.Description();
        }

        string _DataTypeString;
        public string DataTypeString { get => _DataTypeString; set => SetProperty(ref _DataTypeString, value); }
        #region LIN_GRAPHS
            //----- line graph ------------------------------------------------------------------------------------
        public void WaitForLastLineDataFilled()
        {
            do
            {
                lock (_LineDataListLock)
                {
                    if (!_FillingLineDataList)
                    {
                        _FillingLineDataList = true;
                        break;
                    }
                }
                Thread.Sleep(500);
            } while (true);
        }
        public void SetLineDataFilled()
        {
            lock (_LineDataListLock)
            {
                _FillingLineDataList = false;
            }
        }
        public void FillLineData(bool checkCycleFinished)
        {
            if (_LineDataList != null)
            {
                var keys = _LineDataList.Keys.ToArray();
                foreach (var it in keys)
                {
                    lock (_LineDataListLock)
                    {
                        if (_LineDataList.ContainsKey(it))
                        {
                            var list = _DataFeeder.GetLineGraphData(it, checkCycleFinished);
                            if (list != null)
                            {
                                _LineDataList[it] = list;
                            }
                        }
                    }
                }
            }
        }

        bool _ShowLegend = true;
        public bool ShowLegend { get => _ShowLegend; set => SetProperty(ref _ShowLegend, value); }
        double _XMin;
        public double XMin { get => _XMin; set => SetProperty(ref _XMin, value); }
        double _XWidth;
        public double XWidth { get => _XWidth; set => SetProperty(ref _XWidth, value); }

        double _YMin;
        public double YMin { get => _YMin; set => SetProperty(ref _YMin, value); }
        double _YHeight;
        public double YHeight { get => _YHeight; set => SetProperty(ref _YHeight, value); }
        Dictionary<LineDataKey, LineMetricDataAttributes> _LineAttributeList;
        Dictionary<LineDataKey, LineMetricDataBuffer> _LineDataList;
        bool _FillingLineDataList;
        int[] _LineDataListLock = new int[0];
        int[] _SelectedLineDataKeysLock = new int[0];
        List<LineDataKey> SelectedLineDataKeys { get; set; }

        List<LineGraph> _TheList;
        public List<LineGraph> TheList { get => _TheList; set => SetProperty(ref _TheList, value); }

        const double percentageMax = 100.1;
        Dictionary<MetricsDataEnum, MinMaxRange> _LineGraphYDefaultRangeList = new Dictionary<MetricsDataEnum, MinMaxRange>
                {
                    { MetricsDataEnum.Q20, new MinMaxRange() { Min = 0, Max = percentageMax }} ,
                    { MetricsDataEnum.Q30, new MinMaxRange() { Min = 0, Max = percentageMax }} ,
                    { MetricsDataEnum.MedianQscore, new MinMaxRange() { Min = 0, Max = 101 } } ,
                    { MetricsDataEnum.PF, new MinMaxRange() { Min = 0, Max = percentageMax } } ,
                    { MetricsDataEnum.ErrorRate, new MinMaxRange() { Min = 0, Max = percentageMax } } ,
                    { MetricsDataEnum.Base, new MinMaxRange() { Min = 0, Max = percentageMax } } ,
                    { MetricsDataEnum.Intensity, new MinMaxRange() { Min = 0, Max = UInt16.MaxValue / 1000.0 } } ,
                    { MetricsDataEnum.Density, new MinMaxRange() { Min = 0, Max = 150 } } , //150K
        };

        bool _IsBaseRelatedMetric2;
        public bool IsBaseRelatedMetric2 { get => _IsBaseRelatedMetric2; set => SetProperty(ref _IsBaseRelatedMetric2, value); }
        List<DecoratedItem<LaneDataEnum>> _Lanes;
        public List<DecoratedItem<LaneDataEnum>> Lanes { get => _Lanes; set => SetProperty(ref _Lanes, value); }
        DecoratedItem<LaneDataEnum> _SelectedLane;
        public DecoratedItem<LaneDataEnum> SelectedLane
        {
            get => _SelectedLane;
            set
            {
                if (SetProperty(ref _SelectedLane, value))
                {
                    OnUpdateLineDataSelectionChanged();
                }
            }
        }

        public static List<MetricsDataEnum> GetMetricsDataItemListForLineGraph()
        {
            var lst = Enum.GetValues(typeof(MetricsDataEnum))
                    .Cast<MetricsDataEnum>()
                    .Where(e => e != MetricsDataEnum.PF && e != MetricsDataEnum.ErrorRate && e != MetricsDataEnum.Density && e!= MetricsDataEnum.Base).ToList();

            return lst;
        }


        public List<DecoratedItem<MetricsDataEnum>> MetricsDataItemList2 
        {
            get
            {
                var lst = GetMetricsDataItemListForLineGraph();
                List<DecoratedItem<MetricsDataEnum>> metricsDataItemList = new List<DecoratedItem<MetricsDataEnum>>();
                foreach (var it in lst)
                {
                    metricsDataItemList.Add(new DecoratedItem<MetricsDataEnum>(it, it.Description()));
                }

                return metricsDataItemList;
            }
        }



        MetricsDataEnum _MetricsDataItem2 = MetricsDataEnum.Q20;
        public MetricsDataEnum MetricsDataItem2
        {
            get => _MetricsDataItem2;
            set
            {
                if (SetProperty(ref _MetricsDataItem2, value))
                {
                    IsBaseRelatedMetric2 = _MetricsDataItem2 == MetricsDataEnum.Base || _MetricsDataItem2 == MetricsDataEnum.Intensity;
                    if (_MetricsDataItem2 == MetricsDataEnum.Intensity || _MetricsDataItem2 == MetricsDataEnum.Density)
                    {
                        YMin = _LineGraphYDefaultRangeList[_MetricsDataItem2].Min;
                        YHeight = _LineGraphYDefaultRangeList[_MetricsDataItem2].Max - YMin;
                    }
                    SelectedMetric = $"{_MetricsDataItem2.Description()} {SeqenceMetricDataItem.GetMetricUnitDisplayName(_MetricsDataItem2)}";
                    OnUpdateLineDataSelectionChanged();
                }
            }
        }
        //for line graph
        string _SelectedMetric;
        public string SelectedMetric { get => _SelectedMetric; set => SetProperty(ref _SelectedMetric, value); }
        SurfaceDataEnum _SurfaceDataItem2 = SurfaceDataEnum.Surface1;
        public SurfaceDataEnum SurfaceDataItem2
        {
            get => _SurfaceDataItem2;
            set
            {
                if (SetProperty(ref _SurfaceDataItem2, value))
                {
                    OnUpdateLineDataSelectionChanged();
                }
            }
        }
        ChannelDataEnum _ChannelItem2 = ChannelDataEnum.All;
        public ChannelDataEnum ChannelItem2
        {
            get => _ChannelItem2;
            set
            {
                if (SetProperty(ref _ChannelItem2, value))
                {
                    OnUpdateLineDataSelectionChanged();
                }
            }
        }

        public void Clear()
        {
            TheList = new List<LineGraph>();
        }

        public void BuildLines()
        {
            TheList = new List<LineGraph>();
            _LineAttributeList = new Dictionary<LineDataKey, LineMetricDataAttributes>();
            MetricsDataEnum[] metrics = Enum.GetValues(typeof(MetricsDataEnum)) as MetricsDataEnum[];
            SurfaceDataEnum[] surfases = Enum.GetValues(typeof(SurfaceDataEnum)) as SurfaceDataEnum[];
            LaneDataEnum[] lanes = Enum.GetValues(typeof(LaneDataEnum)) as LaneDataEnum[];
            ChannelDataEnum[] channels = Enum.GetValues(typeof(ChannelDataEnum)) as ChannelDataEnum[];
            int index = 0;
            foreach (var metric in metrics)
            {
                if (metric == MetricsDataEnum.PF || metric == MetricsDataEnum.Density || metric == MetricsDataEnum.ErrorRate || 
                    metric == MetricsDataEnum.Base)
                {
                    continue; // no graph for PF, density, base% and error rate
                }
                if (metric == MetricsDataEnum.Intensity)
                {
                    foreach (var channel in channels)
                    {
                        if (channel != ChannelDataEnum.All)
                        {
                            Color cl;
                            string deps;
                            switch (channel)
                            {
                                case ChannelDataEnum.A:
                                    cl = Colors.Aquamarine;
                                    deps = "A";
                                    break;
                                case ChannelDataEnum.T:
                                    cl = Color.FromRgb(253, 245, 1);
                                    deps = "T";
                                    break;
                                case ChannelDataEnum.G:
                                    cl = Colors.Green;
                                    deps = "G";
                                    break;
                                case ChannelDataEnum.C:
                                    cl = Color.FromRgb(153, 0, 28);
                                    deps = "C";
                                    break;
                                default:
                                    cl = Colors.Black;
                                    deps = "Unknown";
                                    break;
                            }
                            foreach (var surface in surfases)
                            {
                                foreach (var lane in lanes)
                                {
                                    LineDataKey key = new LineDataKey()
                                    {
                                        Channel = channel,
                                        Metric = metric,
                                        Surface = surface,
                                        Lane = lane,
                                    };
                                    LineMetricDataAttributes attr = new LineMetricDataAttributes()
                                    {
                                        Color = cl,
                                        Description = $"{deps} {metric} {GetSurfaceDesp(surface)} {GetLaneDesp(lane)} {GetIndexDesp(index)} ",
                                        Index = index++,
                                    };

                                    _LineAttributeList.Add(key, attr);
                                }
                            }
                        }
                    }
                }
                else
                {

                    foreach (var surface in surfases)
                    {
                        foreach (var lane in lanes)
                        {
                            LineDataKey key = new LineDataKey()
                            {
                                Channel = ChannelDataEnum.All,
                                Metric = metric,
                                Surface = surface,
                                Lane = lane,
                            };
                            LineMetricDataAttributes attr = new LineMetricDataAttributes()
                            {
                                Color = Colors.Green,
                                Description = $"{metric.Description()} - {GetSurfaceDesp(surface)} {GetLaneDesp(lane)} {GetIndexDesp(index)}",
                                Index = index++,
                            };

                            _LineAttributeList.Add(key, attr);
                        }
                    }
                }
            }

            LinkedList<LineGraph> lineList = new LinkedList<LineGraph>();
            index = -1;
            LinkedListNode<LineGraph> node = null;

            Dispatch(() =>
            {
                foreach (var it in _LineAttributeList)
                {
                    LineGraph lg = null;

                    lg = new LineGraph();
                    lg.Visibility = Visibility.Collapsed;

                    lg.Stroke = new SolidColorBrush(it.Value.Color);
                    lg.Description = it.Value.Description;
                    lg.StrokeThickness = 2;

                    if (lineList.Count == 0)
                    {
                        node = lineList.AddFirst(lg);
                        index = it.Value.Index;
                    }
                    else
                    {
                        if (it.Value.Index > index)
                        {
                            node = lineList.AddAfter(node, lg);
                        }
                        else
                        {
                            node = lineList.AddBefore(node, lg);
                        }
                    }
                    index = it.Value.Index;
                }
            });
            //TheList = lineList.ToList();


            TheList = lineList.ToList();
            lock (_LineDataListLock)
            {
                _LineDataList = new Dictionary<LineDataKey, LineMetricDataBuffer>();

                foreach (var it in _LineAttributeList)
                {
                    _LineDataList[it.Key] = new LineMetricDataBuffer();
                }
            }
        }

        bool _UsingDynamicRangeOnLineData;
        public bool UsingDynamicRangeOnLineData
        {
            get => _UsingDynamicRangeOnLineData;
            set
            {
                if (SetProperty(ref _UsingDynamicRangeOnLineData, value))
                {
                    UpdateLineDataDisplayOnSelectedKey();
                }
            }
        }
        bool UseDynamicRangeForLineGraphs(MetricsDataEnum metric) => UsingDynamicRangeOnLineData;
        //(metric == MetricsDataEnum.Density) || (metric == MetricsDataEnum.Intensity);
        void Plot(LineMetricDataAttributes index, Tuple<List<double>, List<double>> graphData)
        {
            try
            {
                TheList[index.Index].Visibility = Visibility.Visible;
                ((LineGraph)TheList[index.Index]).Plot(graphData.Item1, graphData.Item2);
            }
            catch (Exception ex)
            {
                Logger.LogError($"failed to plot a line graph {index.Description} with exception error: {ex.Message}");
            }
        }

        void Unplot(LineMetricDataAttributes index)
        {

            TheList[index.Index].Visibility = Visibility.Collapsed;

        }

        

        public void SetTotalCycles(int cycles)
        {
            
             XMin = 1;
            XWidth = cycles - 1;
            if (XWidth <= 0)
            {
                XWidth = 1;
            }
        }

        public void InitLanes(int[] lanes)
        {
            //int[] lanes = SequenceApp.SequenceInformation.Lanes;
            List<DecoratedItem<LaneDataEnum>> laneList = new List<DecoratedItem<LaneDataEnum>>();
            if (lanes != null)
            {
                foreach (var it in lanes)
                {
                    LaneDataEnum laneItem = LaneDataEnum.AllLane;
                    switch (it)
                    {
                        case 1:
                            laneItem = LaneDataEnum.Lane1;
                            break;
                        case 2:
                            laneItem = LaneDataEnum.Lane2;
                            break;
                        case 3:
                            laneItem = LaneDataEnum.Lane3;
                            break;
                        case 4:
                            laneItem = LaneDataEnum.Lane4;
                            break;
                    }
                    if (laneItem != LaneDataEnum.AllLane)
                    {
                        laneList.Add(new DecoratedItem<LaneDataEnum>(laneItem, laneItem.Description()));
                    }
                }
                if (lanes.Length > 1)
                {
                    laneList.Add(new DecoratedItem<LaneDataEnum>(LaneDataEnum.AllLane, LaneDataEnum.AllLane.Description()));
                }

                Lanes = laneList;
                SelectedLane = Lanes[0];
            }
        }

        public void UpdateLineDataDisplayOnSelectedKey(bool hasNewData = false)
        {
            List<LineDataKey> dataKeys = null;
            lock (_SelectedLineDataKeysLock)
            {
                if (SelectedLineDataKeys != null)
                {
                    dataKeys = SelectedLineDataKeys;
                }
            }

            if (dataKeys != null)
            {
                Dictionary<LineDataKey, Tuple<List<double>, List<double>>> graphDataList = new Dictionary<LineDataKey, Tuple<List<double>, List<double>>>();
                double newYMin = double.MaxValue;
                double newYMax = double.MinValue;
                lock (_LineDataListLock)
                {
                    LineDataKey lineType;
                    MinMaxRange yRange;
                    MetricsDataEnum metric;
                    foreach (var it in _LineAttributeList)
                    {
                        if (dataKeys.Contains(it.Key))
                        {
                            lineType = it.Key;
                            graphDataList.Add(lineType, _LineDataList[lineType].GetDataPoints());
                            yRange = _LineDataList[lineType].Yrange;
                            metric = lineType.Metric;
                            if (yRange != null && UseDynamicRangeForLineGraphs(metric))
                            {
                                if (newYMin > yRange.Min)
                                {
                                    newYMin = yRange.Min;
                                }

                                if (newYMax < yRange.Max)
                                {
                                    newYMax = yRange.Max;
                                }
                            }//if
                            else
                            {
                                if (newYMin > _LineGraphYDefaultRangeList[metric].Min)
                                {
                                    newYMin = _LineGraphYDefaultRangeList[metric].Min;
                                }

                                if (newYMax < _LineGraphYDefaultRangeList[metric].Max)
                                {
                                    newYMax = _LineGraphYDefaultRangeList[metric].Max;
                                }
                            }//else
                        } //if
                    } //for
                }//lock

                Dispatch(() =>
                {
                    foreach (var it in _LineAttributeList)
                    {
                        if (dataKeys.Contains(it.Key))
                        {
                            YMin = (newYMin > 0.5) ? (newYMin - 0.5) : 0;
                            YHeight = (newYMax - newYMin <= 0) ? 1 : (newYMax - newYMin + 1);
                            Plot(_LineAttributeList[it.Key], graphDataList[it.Key]);
                        }
                        else
                        {
                            Unplot(_LineAttributeList[it.Key]);
                        }

                    }
                });
            }
        }

        void OnUpdateLineDataSelectionChanged()
        {
            if (SelectedLane == null)
            {
                return;
            }

            MetricsDataEnum metric;
            ChannelDataEnum channel;
            SurfaceDataEnum surface;
            LaneDataEnum lane;

            lock (_SelectedLineDataKeysLock)
            {
                metric = MetricsDataItem2;
                channel = ChannelItem2;
                surface = SurfaceDataItem2;
                lane = SelectedLane.Value;
            }


            List<LineDataKey> dataKeys = new List<LineDataKey>();
            if (channel == ChannelDataEnum.All &&
                (metric == MetricsDataEnum.Base || metric == MetricsDataEnum.Intensity))
            {
                ChannelDataEnum[] channels = Enum.GetValues(typeof(ChannelDataEnum)) as ChannelDataEnum[];
                foreach (var it in channels)
                {
                    if (it != ChannelDataEnum.All)
                    {
                        dataKeys.Add(new LineDataKey()
                        {
                            Metric = metric,
                            Channel = it,
                            Surface = surface,
                            Lane = lane,
                        });
                    }
                }
            }
            else
            {
                dataKeys.Add(new LineDataKey()
                {
                    Metric = metric,
                    Channel = channel,
                    Surface = surface,
                    Lane = lane,
                });
            }

            lock (_SelectedLineDataKeysLock)
            {
                SelectedLineDataKeys = dataKeys;
            }
            UpdateLineDataDisplayOnSelectedKey();
        }

        string GetSurfaceDesp(SurfaceDataEnum surfaceEnum)
        {
            string str;
            if (surfaceEnum == SurfaceDataEnum.Both)
            {
                str = "S-Both";
            }
            else
            {
                str = $"S{(int)surfaceEnum}";
            }
            return str;
        }

        string GetLaneDesp(LaneDataEnum lane)
        {
            string str;
            if (lane == LaneDataEnum.AllLane)
            {
                str = "L-ALL";
            }
            else
            {
                str = $"L{(int)lane}";
            }
            return str;
        }

        string GetIndexDesp(int index)
        {
            string str = $"#{index}";
            return str;
        }
        int _MaxHeightLineCharts = 450;
        public int MaxHeightLineCharts { get => _MaxHeightLineCharts; set => SetProperty(ref _MaxHeightLineCharts, value); }

        bool _EnableSelections = true;
        public bool EnableSelections { get => _EnableSelections; set => SetProperty(ref _EnableSelections, value); }

        bool _ShowTitle = false;
        public bool ShowTitle { get => _ShowTitle; set => SetProperty(ref _ShowTitle, value); }
        //--------------end line graph -----------------------------------------------------------------------
        #endregion //LINE_GRAPHS
    }
}
