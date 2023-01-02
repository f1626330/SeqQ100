using System;
using System.Windows.Media.Imaging;

namespace Sequlite.CameraLib
{
    public enum CaptureFrameType
    {
        Normal = 0,
        Dark = 1
    }

    public enum CameraMake
    {
        Apogee,
        Photometrics,
    }

    public abstract class CameraLibBase
    {
        public delegate void CameraNotifHandler(Object sender);
        public abstract event CameraNotifHandler CameraNotif;
        public delegate void ExposureChangedHandle(bool starts);
        public abstract event ExposureChangedHandle OnExposureChanged;

        protected int _ImagingColumns = 0;
        protected int _ImagingRows = 0;
        protected int _VBin = 1;                // Vertical bin factor
        protected int _HBin = 1;                // Horizontal bin factor
        protected double _MinExposure = 0.001;  // 1 milliseconds
        protected double _CCDCoolerSetPoint = -20.0;
        protected double _CurrCcdTemperature = 0;
        protected int _Gain = 0;
        protected int _ReadoutSpeed = 0;
        protected bool _IsAcqRunning = false;
        protected bool _IsAbortCapture = false;
        protected bool _IsForceShutterOpen = false;

        public abstract CameraMake CameraType { get; }
        public abstract int RoiStartX { get; set; }
        public abstract int RoiStartY { get; set; }
        public abstract int RoiWidth { get; set; }
        public abstract int RoiHeight { get; set; }
        public abstract int ImagingColumns { get; }
        public abstract int ImagingRows { get; }
        public abstract int HBin { get; set; }
        public abstract int VBin { get; set; }
        public abstract double MinExposure { get; }
        public abstract int ReadoutSpeed { get; set; }
        public abstract bool Open();
        public abstract void Close();
        public abstract double CCDTemperature { get; }
        public abstract double CCDCoolerSetPoint { get; set; }
        public abstract string FirmwareVersion { get; }
        public abstract bool IsAbortCapture { get; set; }
        public abstract bool ForceShutterOpen { get; set; }
        //public abstract string CameraModel { get; }
        public abstract int Gain { get; set; }
        public abstract bool IsAcqRunning { get; }
        public abstract bool IsDynamicDarkCorrection { get; set; }
        public abstract int ADCBitDepth { get; set; }
        public abstract int PixelFormatBitDepth { get; set; }

        public abstract void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage);

        public abstract void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage,
                                       int lightType);

        //public abstract void GrabImage(ref WriteableBitmap capturedImage,
        //                               double exposureTime,
        //                               int lightDelayTime,
        //                               APDTransfer apdTransfer,
        //                               int lightType);

        public abstract void StopCapture();

        public abstract void StartContinuousMode(double exposureTime);

        public abstract void StopAcquisition();
    }
}
