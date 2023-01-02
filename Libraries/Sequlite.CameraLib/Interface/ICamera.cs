using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Sequlite.CameraLib
{
   
    public delegate void CameraNotifHandler(Object sender);
    public delegate void ExposureChangedHandle(bool starts);
    public interface ICamera
    {
        event CameraNotifHandler CameraNotif;
        event ExposureChangedHandle OnExposureChanged;
        bool IsConnected { get; }
        CameraMake CameraType { get; }
        int RoiStartX { get; set; }
        int RoiStartY { get; set; }
        int RoiWidth { get; set; }
        int RoiHeight { get; set; }
        int ImagingColumns { get; }
        int ImagingRows { get; }
        int HBin { get; set; }
        int VBin { get; set; }
        double MinExposure { get; }
        int ReadoutSpeed { get; set; }
        bool Open();
        void Close();
        double CCDTemperature { get; }
        double CCDCoolerSetPoint { get; set; }
        string FirmwareVersion { get; }
        bool IsAbortCapture { get; set; }
        bool ForceShutterOpen { get; set; }
        //public abstract string CameraModel { get; }
        int Gain { get; set; }
        bool IsAcqRunning { get; }
        bool IsDynamicDarkCorrection { get; set; }
        int ADCBitDepth { get; set; }
        int PixelFormatBitDepth { get; set; }
        string Channels { get; set; }
        void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage);

        void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage,
                                       int lightType);
        void StopCapture();

        void StartContinuousMode(double exposureTime);

        void StopAcquisition();
    }
}
