using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Sequlite.UI.Model
{
    public class DecoratedItem<T>
    {
        public T Value { get; }
        public string Description { get; }
        public DecoratedItem(T value, string dispalyame)
        {
            Value = value;
            Description = dispalyame;
        }
    }
    public enum MetricsDataEnum
    {
        [Description("%>=Q20")]
        
        Q20,

        [Description("%>=Q30")]
        Q30,

        [Description("Median Qscore")]
        MedianQscore,

        [Description("Intensity")]
        Intensity,

        [Description("Density")]
        Density,

        [Description("%Base")]
        Base,

        [Description("%PF")]
        PF,

        [Description("Error Rate")]
        ErrorRate,
    }


    public enum SurfaceDataEnum
    {
        [Description("Bottom")]
        Surface1 = 1,

        [Description("Top")]
        Surface2,

        [Description("Both Surfaces")]
        Both,
    }

    public enum ChannelDataEnum
    {
        [Description("A")]
        A,

        [Description("C")]
        C,

        [Description("G")]
        G,

        [Description("T")]
        T,

        [Description("All Bases")]
        All,
    }

    public enum LaneDataEnum
    {
        [Description("Lane 1")]
        Lane1 = 1,

        [Description("Lane 2")]
        Lane2,

        [Description("Lane 3")]
        Lane3,

        [Description("Lane 4")]
        Lane4,

        [Description("All Lanes")]
        AllLane,
    }


    public class LineMetricDataBuffer
    {
        Tuple<List<double>, List<double>> GraphData { get; } = new Tuple<List<double>, List<double>>(new List<double>(), new List<double>());
        public void Add(double x, double y)
        {
            GraphData.Item1.Add(x);
            GraphData.Item2.Add(y);
        }

        public Tuple<List<double>, List<double>> GetDataPoints()
        {
            Tuple<List<double>, List<double>> graphData
                = new Tuple<List<double>, List<double>>(new List<double>(GraphData.Item1), new List<double>(GraphData.Item2));
            return graphData;
        }
        public MinMaxRange Yrange { get; set; }

    }

    public class LineDataKey
    {
        public MetricsDataEnum Metric { get; set; }
        public SurfaceDataEnum Surface { get; set; }
        public ChannelDataEnum Channel { get; set; }
        public LaneDataEnum Lane { get; set; }

        public override int GetHashCode()
        {
            if (Metric == MetricsDataEnum.Intensity || Metric == MetricsDataEnum.Base)
            {
                //if (Channel == ChannelDataEnum.All)
                //{
                //    return Metric.GetHashCode() ^ Surface.GetHashCode() ^ Lane.GetHashCode();
                //}
                //else
                {
                    return Channel.GetHashCode() ^ Metric.GetHashCode() ^ Surface.GetHashCode() ^ Lane.GetHashCode();
                }
            }
            else
            {
                return Metric.GetHashCode() ^ Surface.GetHashCode() ^ Lane.GetHashCode();
            }
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as LineDataKey);
        }

        public bool Equals(LineDataKey obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {

                if (Metric == MetricsDataEnum.Intensity || Metric == MetricsDataEnum.Base)
                {

                    //if (Channel == ChannelDataEnum.All)
                    //{
                    //    return  Metric == obj.Metric && Surface == obj.Surface && Lane == obj.Lane;
                    //}
                    //else
                    {
                        return Channel == obj.Channel && Metric == obj.Metric && Surface == obj.Surface && Lane == obj.Lane;
                    }

                }
                else
                {
                    return Metric == obj.Metric && Surface == obj.Surface && Lane == obj.Lane;
                }

            }
        }
    }

    public class LineMetricDataAttributes
    {
        public int Index { get; set; }
        public Color Color { get; set; }
        public string Description { get; set; }
    }

    public class SequenceDataTableItems : ModelBase
    {
        static readonly double _MbUnit = 1000000;
        static readonly double _KbUnit = 1000;
        [DisplayName("Lane")]
        public string Lane { get => Get<string>(); set => Set(value); }

        [DisplayName("Read")]
        public int Read { get => Get<int>(); set => Set(value); }

        [DisplayName("Cycles")]
        public int Cycles { get => Get<int>(); set => Set(value); }

        //[DisplayName("%>PF")]
        //public double PF { get => Get<double>(); set => Set(value); }

        [DisplayName("%>Q30")]
        public double Q30 { get => Get<double>(); set => Set(value); }

        //Est. Yield = cluster count X read length (for example, 50bp/75bp). 
        [DisplayName("Est. Yield")]
        public double Yield 
        { 
            get => Get<double>(); 
            set { Set(value); OnPropertyChanged(nameof(YieldString)); } 
        }

        public string YieldString
        {
            get
            {
                double d = Yield;
                if (d >= _MbUnit)
                {
                    return $"{d / _MbUnit:F2}Mb";
                }
                else
                {
                    return $"{d / _KbUnit:F2}kb";
                }
            }
        }

        //[DisplayName("Error Rate")]
        //public double ErrorRate { get => Get<double>(); set => Set(value); }

        //cluster count
        [DisplayName("Reads PF")]
        public double ReadsPF 
        {
            get => Get<double>();
            set
            {
                Set(value);
                OnPropertyChanged(nameof(ReadsPFString));
            }
        }

        public string ReadsPFString
        {
            get
            {
                double d = ReadsPF;
                if (d >= _MbUnit)
                {
                    return $"{d / _MbUnit:F2}Mb";
                }
                else
                {
                    return $"{d / _KbUnit:F2}kb";
                }
            }
        }

        //cluster count  / 0.85
        [DisplayName("Density")]
        public double Density { get => Get<double>(); set => Set(value); }

        public void SetAverageValues(double factor) 
        {
            if (factor > 0)
            {
                //PF /= factor;
                Q30 /= factor;
                //ErrorRate /= factor;
                //ReadsPF /= factor;
                Density /= factor;
                //Yield /= factor;
            }
        }

       

        public void SetAverageValuesUsingCycleCount()
        {
            if (CycleCounts > 0)
            {
                //PF /= CycleCounts;
                Q30 /= CycleCounts;

                ReadsPF /= CycleCounts;
                Density /= CycleCounts;
                //Yield /= CycleCounts;
            }

            if (ErrorRateCycelCounts > 0)
            {
                //ErrorRate /= ErrorRateCycelCounts;
            }
        }

        public int CycleCounts { get; set; }
        public int ErrorRateCycelCounts { get; set; }
    }
    public class SeqenceMetricDataItem
    {
        public int Cycle { get; set; }
        public double Q30 { get; set; }
        public double Q20 { get; set; }
        public double MedianQ { get; set; }
        public int NumberOfCluster { get; set; }
        public double PF { get; set; }
        public double ErrorRate { get; set; }
        public double ABase { get; set; }
        public double TBase { get; set; }
        public double GBase { get; set; }
        public double CBase { get; set; }
        public double AIntensity { get; set; }
        public double TIntensity { get; set; }
        public double GIntensity { get; set; }
        public double CIntensity { get; set; }
        public double Density { get; set; }

        readonly double _DensityUnit = 1000;
        public double GetMetricValue(MetricsDataEnum metricEnum, ChannelDataEnum baseEnum)
        {
            double v = 0;
            switch (metricEnum)
            {
                case MetricsDataEnum.Q20:
                    v = Q20;
                    break;
                case MetricsDataEnum.Q30:
                    v = Q30;
                    break;
                case MetricsDataEnum.MedianQscore:
                    v = MedianQ;
                    break;
                case MetricsDataEnum.Intensity:
                    {
                        switch (baseEnum)
                        {
                            case ChannelDataEnum.A:
                                v = AIntensity;
                                break;
                            case ChannelDataEnum.C:
                                v = CIntensity;
                                break;
                            case ChannelDataEnum.G:
                                v = GIntensity;
                                break;
                            case ChannelDataEnum.T:
                                v = TIntensity;
                                break;
                        }
                    }
                    break;
                case MetricsDataEnum.Density:
                    v = Density / _DensityUnit;// (NumberOfCluster / FOV / 1000);
                    break;
                case MetricsDataEnum.Base:
                    switch (baseEnum)
                    {
                        case ChannelDataEnum.A:
                            v = ABase;
                            break;
                        case ChannelDataEnum.C:
                            v = CBase;
                            break;
                        case ChannelDataEnum.G:
                            v = GBase;
                            break;
                        case ChannelDataEnum.T:
                            v = TBase;
                            break;
                    }
                    break;
                case MetricsDataEnum.PF:
                    v = PF;
                    break;
                case MetricsDataEnum.ErrorRate:
                    v = ErrorRate;
                    break;
            }
            return v;
        }

        public static string GetMetricUnitDisplayName(MetricsDataEnum metricEnum)
        {
            string str = "";
            switch (metricEnum)
            {
                case MetricsDataEnum.ErrorRate:
                    str = "(%)";
                    break;
                case MetricsDataEnum.Density:
                case MetricsDataEnum.Intensity:
                    str = "(K)";
                    break;

            }
            return str;
        }
    }

    public class RegionInfo
    {
        public int Lane { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public string Path {get;set;}

        public override int GetHashCode()
        {
            return Lane.GetHashCode() ^ Column.GetHashCode() ^ Row.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as RegionInfo);
        }

        public bool Equals(RegionInfo obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return Lane == obj.Lane && Column == obj.Column && Row == obj.Row;
            }
        }
    }

    public class OLADataFileInfo
    {
        public List<RegionInfo> RegionInfos { get; }
        public OLADataFileInfo()
        {
            RegionInfos = new List<RegionInfo>();
        }
        public void AddDir(RegionInfo r)
        {
            RegionInfos.Add(r);
        }
    }

    public class SequenceOLADataKey
    {
        public SurfaceDataEnum Surface { get; set; }
        public LaneDataEnum Lane { get; set; }
        public override int GetHashCode()
        {
            return Surface.GetHashCode() ^ Lane.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as SequenceOLADataKey);
        }

        public bool Equals(SequenceOLADataKey obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return Surface == obj.Surface ||
                Lane == obj.Lane;
            }
        }


    }

    public class MinMaxRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public bool IsValidRange => !double.IsNaN(Min) && !double.IsNaN(Max) && (Min <= Max);
    }

    public class MapDataKey
    {
        public MetricsDataEnum Metric { get; set; }
        public SurfaceDataEnum Surface { get; set; }
        public ChannelDataEnum Channel { get; set; }
        public int  Cycle { get; set; }

        public override int GetHashCode()
        {
            return Metric.GetHashCode() ^ Surface.GetHashCode() ^ Channel.GetHashCode() ^ Cycle.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as MapDataKey);
        }

        public bool Equals(MapDataKey obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                return Metric == obj.Metric && Surface == obj.Surface && Channel == obj.Channel && Cycle == obj.Cycle;
            }
        }
    }

    public class HeatMapData
    {
        public double[,] Data { get; set; }
        public MinMaxRange DataBound { get; set; } //dynamic range
        public int Lane { get; set; }
    }

    public class HeatMapDisplayData : HeatMapData
    {
        public double[] XMap { get; set; }
        public double[] YMap { get; set; }
    }

    public class MapMarkValuesModel : ModelBase, ICloneable
    {
        public double MaxMarkValue { get; set; }
        public double MinMarkValue { get => Get<double>(); set => Set(value); }
        public double MarkWidth { get => Get<double>(); set => Set(value); }
        public int MarkCount { get; set; }

        public void CopyFrom(MapMarkValuesModel v)
        {
            MaxMarkValue = v.MaxMarkValue;
            MinMarkValue = v.MinMarkValue;
            MarkWidth = v.MarkWidth;
            MarkCount = v.MarkCount;
        }
        public object Clone()
        {
            MapMarkValuesModel v = new MapMarkValuesModel();
            v.CopyFrom(this);
            return v;
        }
    }

    public class MapXYModel : ModelBase
    {
        public double XMapMin { get => Get<double>(); set => Set(value); }
       
        public double XMapWidth { get => Get<double>(); set => Set(value); }

        public double YMapMin { get => Get<double>(); set => Set(value); }
       
        public double YMapHeight { get => Get<double>(); set => Set(value); }

        public int NXMap { get => Get<int>(); set => Set(value); }
        public int NYMap { get => Get<int>(); set => Set(value); }
        public double[] XMap { get; set; }
        public double[] YMap { get; set; }
    }
}
