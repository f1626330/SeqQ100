using System;
using System.Collections.Generic;

namespace Sequlite.ALF.Imaging
{

    public enum ChemiSampleType
    {
        Blot,
        Plate,
        Custom,
    }

    public enum SensitivityType
    {
        None,
        Lowest,
        Low,
        Medium,
        High,
        Highest,
    }

    public enum ChemiModeType
    {
        Single,
        Cumulative,
        Multiple,
    }

    public enum ExposureType
    {
        None,
        OverExposure,
        UnderExposure,
        WideDynamicRange,
    }

    public class PvCamSensitivity
    {
        #region Public properties...

        public int Position { get; set; }

        public int HorizontalBin { get; set; }

        public int VerticalBin { get; set; }

        public string DisplayName { get; set; }

        #endregion

        #region Constructors...

        public PvCamSensitivity()
        {
        }

        public PvCamSensitivity(int position, int horizontalBin, int verticalBin, string displayName)
        {
            this.Position = position;
            this.HorizontalBin = horizontalBin;
            this.VerticalBin = verticalBin;
            this.DisplayName = displayName;
        }

        #endregion
    }

    public class RgbLedIntensity
    {
        #region Public properties...

        public int Position { get; set; }

        public int Intensity { get; set; }

        public string DisplayName { get; set; }

        #endregion

        #region Constructors...

        public RgbLedIntensity()
        {
        }

        public RgbLedIntensity(int position, int intensity, string displayName)
        {
            this.Position = position;
            this.Intensity = intensity;
            this.DisplayName = displayName;
        }

        #endregion
    }

    public class CameraModeSetting
    {
        #region Private fields...

        private ChemiSetting _ChemiSettings = new ChemiSetting();
        private VisibleImagingSetting _VisibleImagingSettings = new VisibleImagingSetting();
        private LiveMode _LiveModeSettings = new LiveMode();
        private SystemSettings _SysSettings = new SystemSettings();

        #endregion

        #region Public properties...

        public ChemiSetting ChemiSettings
        {
            get { return _ChemiSettings; }
            set { _ChemiSettings = value; }
        }

        public VisibleImagingSetting VisibleImagingSettings
        {
            get { return _VisibleImagingSettings; }
            set { _VisibleImagingSettings = value; }
        }

        public SystemSettings SysSettings
        {
            get { return _SysSettings; }
            set { _SysSettings = value; }
        }

        public LiveMode LiveModeSettings
        {
            get { return _LiveModeSettings; }
            set { _LiveModeSettings = value; }
        }

        public int CCDCoolerSetPoint { get; set; }
        public bool IsDarkCorrection { get; set; }
        public bool IsFlatCorrection { get; set; }
        public double BarrelParamA { get; set; }
        public double BarrelParamB { get; set; }
        public double BarrelParamC { get; set; }

        //PVCAM post processing features.
        public bool IsDynamicDarkCorrection { get; set; }
        public bool IsEnhanceDynamicRange { get; set; }
        public bool IsDefectivePixelCorrection { get; set; }

        #endregion

        #region Constructors...

        public CameraModeSetting()
        {
            IsDynamicDarkCorrection = false;
            IsEnhanceDynamicRange = false;
            IsDefectivePixelCorrection = false;
        }

        #endregion
    }

    public class ChemiSetting
    {
        public int NumFrames { get; set; }
        public int ReadoutSpeed { get; set; }
        public int Gain { get; set; }
        public double MinExposure { get; set; }
        public double MaxExposure { get; set; }
        public double MarkerRedChExposure { get; set; }
        public double MarkerGreenChExposure { get; set; }
        public double MarkerBlueChExposure { get; set; }
        public int PvCamScalingThreshold { get; set; }
        public int AutoExposureUpperCeiling { get; set; }
        public int AEUnderExposure { get; set; }
        public int AEOverExposure { get; set; }
        public int AEWideDynamicRange { get; set; }
        public bool IsDarkCorrection { get; set; }

        public ChemiSetting()
        {
        }
    }

    public class VisibleImagingSetting
    {
        public int Bin { get; set; }
        public int ReadoutSpeed { get; set; }
        public int Gain { get; set; }
        public double MinExposure { get; set; }
        public double MaxExposure { get; set; }
        public int AutoExposureUpperCeiling { get; set; }
        public int ScalingThreshold { get; set; }

        public VisibleImagingSetting()
        {
        }
    }

    public class LiveMode
    {
        public int BinningMode { get; set; }
        public int ReadoutSpeed { get; set; }
        public double ExposureTime { get; set; }

        public LiveMode()
        {
            this.BinningMode = 8;
            this.ReadoutSpeed = 1;
            this.ExposureTime = 0.001;
        }
    }

    /// <summary>
    /// System specific settings - software update does not affect these settings.
    /// </summary>
    public class SystemSettings
    {
        #region Private data...

        private bool _IsEnableCcdBcRemoval = true;
        private List<CCDBadColumn> _ccdBadColumns = new List<CCDBadColumn>();

        #endregion

        #region Constructors...

        public SystemSettings()
        {
        }

        public SystemSettings(bool bIsCcdBcRemoval, List<CCDBadColumn> badColumns)
        {
            _IsEnableCcdBcRemoval = bIsCcdBcRemoval;
            _ccdBadColumns = new List<CCDBadColumn>(badColumns);
        }

        #endregion

        #region Public properties...

        public bool IsEnableCcdBcRemoval
        {
            get { return _IsEnableCcdBcRemoval; }
            set
            {
                if (_IsEnableCcdBcRemoval != value)
                {
                    _IsEnableCcdBcRemoval = value;
                }
            }
        }

        public int CcdBcCount
        {
            get
            {
                int retVal = 0;
                if (_ccdBadColumns != null)
                {
                    retVal = _ccdBadColumns.Count;
                }
                return retVal;
            }
        }

        public List<CCDBadColumn> CcdBadColumns
        {
            get { return _ccdBadColumns; }
        }

        #endregion
    }

    [Serializable()]
    public class CCDBadColumn
    {
        #region Private data...

        private int _Binning = 3;
        private int _Column = 0;

        #endregion

        #region Constructors...

        public CCDBadColumn()
        {
        }

        public CCDBadColumn(int binningMode, int nBadColumn)
        {
            _Binning = binningMode;
            _Column = nBadColumn;
        }

        #endregion

        #region Public properties...

        public int Binning
        {
            get { return _Binning; }
            set
            {
                if (_Binning != value)
                {
                    _Binning = value;
                }
            }
        }

        public int Column
        {
            get { return _Column; }
            set
            {
                if (_Column != value)
                {
                    _Column = value;
                }
            }
        }

        #endregion

    }

}
