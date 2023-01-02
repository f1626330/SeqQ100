using System.Text.Json.Serialization;
using System.Windows;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public enum LEDTypes
    {
        Green = 0,
        Red,
        White,
    }

    //Json serialization
    public struct ROIRect
    {
        public int RoiLeft { get; set; }
        public int RoiTop { get; set; }
        public int RoiWidth { get; set; }
        public int RoiHeight { get; set; }
        public ROIRect(Int32Rect roi)
        {
            RoiLeft = roi.X;
            RoiTop = roi.Y;
            RoiWidth = roi.Width;
            RoiHeight = roi.Height;
        }

        public static ROIRect ToROIRect(Int32Rect roi)
        {
            return new ROIRect(roi);
        }

        public Int32Rect ToInt32Rect()
        {
            return new Int32Rect(RoiLeft, RoiTop, RoiWidth, RoiHeight);
        }
    }

    //Json serialization
    public class AutoFocusSettings
    {
        
        [JsonPropertyName("ROI")] public ROIRect SeqROI
        {
            get { return ROIRect.ToROIRect(ROI); }
            set
            {
                ROI = value.ToInt32Rect();
            }
        }
        [JsonPropertyName("ROI2")] public ROIRect SeqROI2
        {
            get { return ROIRect.ToROIRect(ROI2); }
            set
            {
                ROI2 = value.ToInt32Rect();
            }
        }
        public LEDTypes LEDType { get; set; }
        //[JsonPropertyName( "Intensity")]
        public uint LEDIntensity { get; set; }
        public double ExposureTime { get; set; }
        public double ZstageSpeed { get; set; }
        public double ZstageAccel { get; set; }
        public double ZRange { get; set; }
        public int FilterIndex { get; set; }
        public LEDTypes OffsetLEDType { get; set; }
        public uint OffsetLEDIntensity { get; set; }
        public double OffsetExposureTime { get; set; }
        public int OffsetFilterIndex { get; set; }
        public double RotationAngle { get; set; }
        public double BottomOffset { get; set; }
        public double TopOffset { get; set; }
        public double ChannelOffset { get; set; }
        public double Reference0 { get; set; }
        public double FiducialVersion { get; set; }
        public bool IsScanonly { get; set; }
        public bool IsRecipe { get; set; }
        public bool IsHConly { get; set; }
        public double ScanInterval { get; set; }
        public double TopStdLmtH { get; set; }
        public double TopStdLmtL { get; set; }
        public double BottomStdLmtL { get; set; }
        public double FCChannelHeight { get; set; }
        public double TopGlassThickness { get; set; }
        [JsonIgnore] public Int32Rect ROI { get; set; }
        [JsonIgnore] public Int32Rect ROI2 { get; set; }
        [JsonIgnore] public double ExtraExposure { get; set; }
        [JsonIgnore] public double ZstageLimitH { get; set; }
        [JsonIgnore] public double ZstageLimitL { get; set; }
        public AutoFocusSettings()
        {
            IsScanonly = false;
            IsHConly = false;
            IsRecipe = false;
            ScanInterval = 1;
            
        }

        public AutoFocusSettings(AutoFocusSettings otherSettings)
        {
            ExposureTime = otherSettings.ExposureTime;
            LEDType = otherSettings.LEDType;
            LEDIntensity = otherSettings.LEDIntensity;
            ROI = new Int32Rect(otherSettings.ROI.X, otherSettings.ROI.Y, otherSettings.ROI.Width, otherSettings.ROI.Height);
            ROI2 = new Int32Rect(otherSettings.ROI2.X, otherSettings.ROI2.Y, otherSettings.ROI2.Width, otherSettings.ROI2.Height);
            ZstageSpeed = otherSettings.ZstageSpeed;
            ZstageAccel = otherSettings.ZstageAccel;
            ZstageLimitH = otherSettings.ZstageLimitH;
            ZstageLimitL = otherSettings.ZstageLimitL;
            ZRange = otherSettings.ZRange;
            FilterIndex = otherSettings.FilterIndex;
            OffsetFilterIndex = otherSettings.OffsetFilterIndex;
            OffsetExposureTime = otherSettings.OffsetExposureTime;
            OffsetLEDType = otherSettings.OffsetLEDType;
            OffsetLEDIntensity = otherSettings.OffsetLEDIntensity;
            RotationAngle = otherSettings.RotationAngle;
            BottomOffset = otherSettings.BottomOffset;
            TopOffset = otherSettings.TopOffset;
            ChannelOffset = otherSettings.ChannelOffset;
            Reference0 = otherSettings.Reference0;
            FiducialVersion = otherSettings.FiducialVersion;
            IsHConly = otherSettings.IsHConly;
            IsScanonly = otherSettings.IsScanonly;
            IsRecipe = otherSettings.IsRecipe;
            ScanInterval = otherSettings.ScanInterval;
            TopStdLmtH = otherSettings.TopStdLmtH;
            TopStdLmtL = otherSettings.TopStdLmtL;
            BottomStdLmtL = otherSettings.BottomStdLmtL;
            FCChannelHeight = otherSettings.FCChannelHeight;
            TopGlassThickness = otherSettings.TopGlassThickness;
            ExtraExposure = otherSettings.ExtraExposure;
        }
    
    }
}
