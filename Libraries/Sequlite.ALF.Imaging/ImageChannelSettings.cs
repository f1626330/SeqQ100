using Sequlite.ALF.Common;

namespace Sequlite.ALF.Imaging
{

    public enum ChannelType
    {
        NONE = 0,
        RED = 1,
        GREEN = 2,
        WHITE = 3,
    };

    public class ImageChannelSettings
    {
        #region Private members...

        //private double _Exposure = 0.0;
        //private bool _IsAutoExposure = false;
        //private int _AutoExposureUpperCeiling = 50000;
        //private LEDTypes _Channel;
        //private int _BinningMode = 1;
        //private int _AdGain = 0;
        //private int _ReadoutSpeed = 0;
        //private int _FrameCount = 1;
        //private int _LightType = 0;
        //private List<double> _ExposureList = new List<double>();

        #endregion

        #region Public properties...
        //public bool IsAutoExposure
        //{
        //    get { return _IsAutoExposure; }
        //    set
        //    {
        //        _IsAutoExposure = value;
        //        //OnPropertyChanged("IsAutoExposure");
        //    }
        //}

        //public int AutoExposureUpperCeiling
        //{
        //    get { return _AutoExposureUpperCeiling; }
        //    set { _AutoExposureUpperCeiling = value; }
        //}

        public LEDTypes LED { get; set; }
        public uint LedIntensity { get; set; }
        /// <summary>
        /// unit of sec.
        /// </summary>
        public double Exposure { get; set; }
        public int BinningMode { get; set; }
        public int AdGain { get; set; }
        public int ReadoutSpeed { get; set; }

        //public bool IsAutoExposureToBand { get; set; }

        public bool IsCaptureFullRoi { get; set; }

        public bool IsCaptureAvg { get; set; }

        //public bool IsApplyFlatCorrection { get; set; }

        //public bool IsApplyDarkCorrection { get; set; }

        //public bool IsApplyDynamicDarkCorrection { get; set; }

        public bool IsEnableBadImageCheck { get; set; }

        public double ExtraExposure { get; set; }
        public int ADCBitDepth { get; set; }
        public int PixelFormatBitDepth { get; set; }

        //public double BarrelParamA { get; set; }
        //public double BarrelParamB { get; set; }
        //public double BarrelParamC { get; set; }

        #endregion

        #region Constructors...

        public ImageChannelSettings()
        {
        }

        #endregion

    }

}
