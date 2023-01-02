using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Azure.Controller;
using ManagedLibFLI;

namespace Azure.CameraLib
{
    /// <summary>
    /// Camera class to interface with Finger Lakes Instrumentation camera.
    /// </summary>
    public class FLICamera : CameraLibBase
    {
        #region Constants...
        private const int FLI_SHUTTER_CLOSE = 0x0000;
        private const int FLI_SHUTTER_OPEN = 0x0001;
        #endregion

        #region Private data...
        //private static FLICamera _Instance = null;
        private static ManagedLibFLI_API _LibFLIWrapper = null;
        private ManagedLibFLI_API.DeviceStruct[] _DeviceList = new ManagedLibFLI_API.DeviceStruct[30];
        private sbyte[] _DevName = new sbyte[31];
        private sbyte[] _DevFName = new sbyte[31];

        private static int _DeviceHandle = -1;

        // Camera's visible area
        private int _ul_x = 0;  // Upper-left x-cordinate of image area.
        private int _ul_y = 0;  // Upper-left y-cordinate of image area.
        private int _lr_x = 0;  // Lower-right x-cordinate of image area.
        private int _lr_y = 0;  // Lower-right y-cordinate of image area.

        // Region of interest
        private int _RoiStartX = 0;
        private int _RoiStartY = 0;
        private int _RoiWidth  = 0;
        private int _RoiHeight = 0;

        private int _DeltaW = 0;
        private int _DeltaH = 0;

        //private double _CCDCoolerSetPoint = -20.0;
        //private bool _IsExposing = false;
        #endregion

        #region Public properties...

        //public override CameraLibBase CreateInstance()
        //{
        //    if (_Instance == null)
        //    {
        //        _Instance = new FLICamera();
        //    }
        //    return _Instance;
        //}

        public override int RoiStartX
        {
            get
            {
                return _RoiStartX;
            }
            set
            {
                if (_RoiStartX != value)
                {
                    _RoiStartX = value;
                }
            }
        }

        public override int RoiStartY
        {
            get
            {
                return _RoiStartY;
            }
            set
            {
                if (_RoiStartY != value)
                {
                    _RoiStartY = value;
                }
            }
        }

        public override int RoiWidth
        {
            get
            {
                return _RoiWidth;
            }
            set
            {
                if (_RoiWidth != value)
                {
                    _RoiWidth = value;

                    // prevent image skewing when height/width parameter supply to the camera is an odd value.
                    _DeltaW = 0;
                    if (_RoiWidth % 2 != 0)
                    {
                        _RoiWidth++;
                        _DeltaW = 1;
                    }
                }
            }
        }

        public override int RoiHeight
        {
            get
            {
                return _RoiHeight;
            }
            set
            {
                if (_RoiHeight != value)
                {
                    _RoiHeight = value;

                    // prevent image skewing when height/width parameter supply to the camera is an odd value.
                    _DeltaH = 0;
                    if (_RoiStartY % 2 != 0)
                    {
                        _RoiHeight++;
                        _DeltaH = 1;
                    }
                }
            }
        }

        public override int ImagingColumns
        {
            get { return (_lr_x - _ul_x); }
        }

        public override int ImagingRows
        {
            get { return (_lr_y - _ul_y); }
        }

        public override int HBin
        {
            get { return _HBin; }
            set { _HBin = value; }
        }

        public override int VBin
        {
            get { return _VBin; }
            set { _VBin = value; }
        }

        public override double MinExposure
        {
            get
            {
                return _MinExposure;
            }
        }

        public override double CCDTemperature
        {
            get
            {
                double dCCDTemp = 0.0;
                // Can't check CCD temp more frequent than every 3 seconds.
                _LibFLIWrapper._FLIGetTemperature(_DeviceHandle, ref dCCDTemp);
                return dCCDTemp;
            }
        }

        public override double CCDCoolerSetPoint
        {
            get { return _CCDCoolerSetPoint; }
            set
            {
                if (_CCDCoolerSetPoint != value)
                {
                    _CCDCoolerSetPoint = value;
                }
            }
        }

        public override int GetFirmwareVersion
        {
            get
            {
                int iFWVersion = 0;
                _LibFLIWrapper._FLIGetFWRevision(_DeviceHandle, ref iFWVersion);
                return iFWVersion;
            }
        }

        public override bool IsExposing
        {
            get
            {
                return _IsExposing;
            }
            set
            {
                if (_IsExposing != value)
                {
                    _IsExposing = value;
                }
            }
        }

        public override bool ForceShutterOpen
        {
            get
            {
                return true;
            }
            set
            {
                int shutter = (value == true) ? FLI_SHUTTER_OPEN : FLI_SHUTTER_CLOSE;
                _LibFLIWrapper._FLIControlShutter(_DeviceHandle, shutter);
            }
        }

        /// <summary>
        /// Returns/Sets the camera’s acquisition speed (Apogee's camera only)
        /// 0 : Normal
        /// 1 : Fast
        /// </summary>
        public override int ReadoutSpeed
        {
            get { return 0; }   // Set it to 'normal' acquisition speed for non-Apogee's camera
            set
            {
                if (_ReadoutSpeed != value)
                {
                    _ReadoutSpeed = value;
                }
            }
        }

        #endregion

        #region Constructors...
        public FLICamera()
        {
            _LibFLIWrapper = new ManagedLibFLI_API();
        }

        public FLICamera(double ccdCoolerSetPoint)
        {
            _LibFLIWrapper = new ManagedLibFLI_API();
            _CCDCoolerSetPoint = ccdCoolerSetPoint;
        }
        #endregion

        #region Public methods...

        /// <summary>
        /// Get a handle to a FLI device.
        /// </summary>
        /// <returns></returns>
        public override bool Open()
        {
            bool bResult = true;

            try
            {
                long retval = _LibFLIWrapper._FLICreateList(ManagedLibFLI_API._FLIDEVICE_CAMERA);
                if (retval != 0)
                {
                    //throw new Exception("No Response from FLI Camera.");
                    bResult = false;
                }
                else
                {
                    _DeviceList[0] = _LibFLIWrapper._FLIListFirst();

                    try
                    {
                        while (true)
                        {
                            int i = 1;
                            _DeviceList[i] = _LibFLIWrapper._FLIListNext();
                            i++;
                        }
                    }
                    catch
                    {
                        //no more devices in the list
                    }

                    foreach (var currDevice in _DeviceList)
                    {
                        if (currDevice == null)
                        {
                            break;
                        }

                        retval = _LibFLIWrapper._FLIOpen(ref _DeviceHandle, currDevice.FileName, currDevice.DomainID);
                        if (retval != 0)
                        {
                            bResult = false;
                        }

                        if (_DeviceHandle == -1)
                        {
                            bResult = false;
                        }
                        else
                        {
                            retval = _LibFLIWrapper._FLIGetVisibleArea(_DeviceHandle, ref _ul_x, ref _ul_y, ref _lr_x, ref _lr_y);
                            if (retval != 0)
                            {
                                //throw new Exception("Operation failed. No response from camera.");
                                bResult = false;
                            }
                            _ImagingColumns = _lr_x - _ul_x;
                            _ImagingRows = _lr_y - _ul_y;
                        }

                    }   // foreach

                }   // else
            }
            catch
            {
                bResult = false;
            }
            finally
            {
                //_LibFLIWrapper._FLIDeleteList();
            }

            _LibFLIWrapper._FLISetTemperature(_DeviceHandle, _CCDCoolerSetPoint);

            return bResult;
        }

        /// <summary>
        /// Close a handle to a FLI device.
        /// </summary>
        public override void Close()
        {
            if (_DeviceHandle != -1)
            {
                _LibFLIWrapper._FLIClose(_DeviceHandle);
            }
        }

        //public static FLICamera CreateInstance()
        //{
        //    if (_Instance == null)
        //    {
        //        try
        //        {
        //            _Instance = new FLICamera();
        //        }
        //        catch
        //        {
        //            return null;
        //        }
        //    }
        //    return _Instance;
        //}

        public override string CameraModel
        {
            get { return _LibFLIWrapper._FLIGetModel(_DeviceHandle); }
        }

        //
        // frametype parameter is either FLI FRAME TYPE NORMAL for a normal frame where the shutter opens
        // or FLI FRAME TYPE DARK for a dark frame where the shutter remains closed.
        //
        public override void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage,
                                       MVController controller,
                                       LightCode lightType)
        {
            int retval = 0;
            int iExposure = (int)(exposureTime * 1000.0);   // exposure time in msec
            int NFlushes = 2;

            try
            {
                short[][] imgPixels = new short[_RoiHeight][];
                capturedImage = new WriteableBitmap(_RoiWidth, _RoiHeight, 96, 96, PixelFormats.Gray16, null);

                _LibFLIWrapper._FLILockDevice(_DeviceHandle);

                int iFrameType = (frameType == CaptureFrameType.Normal) ? ManagedLibFLI_API._FLI_FRAME_TYPE_NORMAL : ManagedLibFLI_API._FLI_FRAME_TYPE_DARK;
                _LibFLIWrapper._FLISetFrameType(_DeviceHandle, iFrameType);

                _LibFLIWrapper._FLISetHBin(_DeviceHandle, HBin);
                _LibFLIWrapper._FLISetVBin(_DeviceHandle, VBin);

                // upper left corner coordinate
                int x1 = _RoiStartX + _ul_x;
                int y1 = _RoiStartY + _ul_y;
                // lower-right corner coordinate
                int x2 = _DeltaW + _ul_x + (_lr_x - _ul_x) / HBin;
                int y2 = _DeltaH + _ul_y + (_lr_y - _ul_y) / VBin;

                _LibFLIWrapper._FLISetImageArea(_DeviceHandle, x1, y1, x2, y2);

                //_LibFLIWrapper._FLISetExposureTime(_DeviceHandle, iExposure);
                //_LibFLIWrapper._FLISetNFlushes(_DeviceHandle, NFlushes);

                _IsExposing = true;

                if (lightType == LightCode.White) // special case for epi white light
                {
                    // exposure time is actually the led on time
                    _LibFLIWrapper._FLISetExposureTime(_DeviceHandle, iExposure + 500);

                    _LibFLIWrapper._FLISetNFlushes(_DeviceHandle, NFlushes);

                    // start exposure
                    retval = _LibFLIWrapper._FLIExposeFrame(_DeviceHandle);

                    // wait for the shutter to open
                    System.Threading.Thread.Sleep(200);

                    if (controller != null)
                    {
                        controller.SetLightContinueOn((uint)lightType, (uint)(iExposure));
                    }
                }
                else
                {
                    if (controller != null && lightType != LightCode.None)
                    {
                        // turn on the light
                        controller.SetLightOn((uint)lightType);
                    }

                    _LibFLIWrapper._FLISetExposureTime(_DeviceHandle, iExposure);

                    _LibFLIWrapper._FLISetNFlushes(_DeviceHandle, NFlushes);

                    // start exposure
                    retval = _LibFLIWrapper._FLIExposeFrame(_DeviceHandle);
                }

                //retval = _LibFLIWrapper._FLIExposeFrame(_DeviceHandle);

                if (retval != 0)
                {
                    throw new Exception("Operation failed. No response from camera.");
                }

                int ExposureLeft = 0;
                retval = _LibFLIWrapper._FLIGetExposureStatus(_DeviceHandle, ref ExposureLeft);
                if (retval != 0)
                {
                    throw new Exception("Operation failed. No response from camera.");
                }

                while (ExposureLeft != 0)
                {
                    retval = _LibFLIWrapper._FLIGetExposureStatus(_DeviceHandle, ref ExposureLeft);
                    if (retval != 0)
                    {
                        throw new Exception("Operation failed. No response from camera.");
                    }
                    System.Threading.Thread.Sleep(50);
                }

                if (controller != null && lightType != LightCode.None)
                {
                    // turn off the light
                    controller.SetLightOff((uint)lightType);
                }

                _IsExposing = false;

                //
                // grab image from the camera
                //
                for (int y = 0; y < _RoiHeight; y++)
                {
                    imgPixels[y] = new short[_RoiWidth];
                    retval = _LibFLIWrapper._FLIGrabRow(_DeviceHandle, imgPixels[y], (uint)_RoiWidth);
                    if (retval != 0)
                    {
                        throw new Exception("Operation failed. No response from camera.");
                    }
                }

                _LibFLIWrapper._FLIUnlockDevice(_DeviceHandle);

                unsafe
                {
                    ushort* pData = (ushort*)capturedImage.BackBuffer.ToPointer();
                    for (int y = 0; y < _RoiHeight; y++)
                    {
                        for (int x = 0; x < _RoiWidth; x++)
                        {
                            *(pData + x + (y * _RoiWidth)) = (ushort)imgPixels[y][x];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _IsExposing = false;
            }
        }

        /// <summary>
        /// Focus calibration image grab.
        /// [See the other GrabImage method for normal image grab]
        /// </summary>
        /// <param name="capturedImage"></param>
        /// <param name="dExposureTime"></param>
        /// <param name="iLightDelay"></param>
        /// <param name="mvController"></param>
        public override void GrabImage(ref WriteableBitmap capturedImage, double dExposureTime, int iLightDelay, MVController mvController)
        {
            int retval = 0;
            int iExposure = (int)(dExposureTime * 1000.0);   // exposure time in msec
            int NFlushes = 2;

            try
            {
                short[][] imgPixels = new short[_RoiHeight][];
                capturedImage = new WriteableBitmap(_RoiWidth, _RoiHeight, 96, 96, PixelFormats.Gray16, null);

                _LibFLIWrapper._FLILockDevice(_DeviceHandle);

                _LibFLIWrapper._FLISetFrameType(_DeviceHandle, ManagedLibFLI_API._FLI_FRAME_TYPE_NORMAL);

                _LibFLIWrapper._FLISetHBin(_DeviceHandle, HBin);
                _LibFLIWrapper._FLISetVBin(_DeviceHandle, VBin);

                // upper left corner coordinate
                int x1 = _RoiStartX + _ul_x;
                int y1 = _RoiStartY + _ul_y;
                // lower right corner coordinate
                int x2 = _RoiWidth + x1;
                int y2 = _RoiHeight + y1;

                _LibFLIWrapper._FLISetImageArea(_DeviceHandle, x1, y1, x2, y2);

                _LibFLIWrapper._FLISetNFlushes(_DeviceHandle, NFlushes);

                _IsExposing = true;

                // exposure time is actually the led on time
                _LibFLIWrapper._FLISetExposureTime(_DeviceHandle, iExposure + 500);

                // start exposure
                retval = _LibFLIWrapper._FLIExposeFrame(_DeviceHandle);

                // wait for the shutter to open
                System.Threading.Thread.Sleep(200);

                if (mvController != null)
                {
                    mvController.SetLightContinueOn((uint)LightCode.White, (uint)(iLightDelay));
                }

                //retval = _LibFLIWrapper._FLIExposeFrame(_DeviceHandle);
                if (retval != 0)
                {
                    throw new Exception("Operation failed. No response from camera.");
                }

                int ExposureLeft = 0;
                retval = _LibFLIWrapper._FLIGetExposureStatus(_DeviceHandle, ref ExposureLeft);
                if (retval != 0)
                {
                    throw new Exception("Operation failed. No response from camera.");
                }

                while (ExposureLeft != 0)
                {
                    retval = _LibFLIWrapper._FLIGetExposureStatus(_DeviceHandle, ref ExposureLeft);
                    if (retval != 0)
                    {
                        throw new Exception("Operation failed. No response from camera.");
                    }
                    System.Threading.Thread.Sleep(50);
                }

                if (mvController != null)
                {
                    // turn off the light
                    mvController.SetLightOff((uint)LightCode.White);
                }

                _IsExposing = false;

                //
                // grab image from the camera
                //
                for (int y = 0; y < _RoiHeight; y++)
                {
                    imgPixels[y] = new short[_RoiWidth];
                    retval = _LibFLIWrapper._FLIGrabRow(_DeviceHandle, imgPixels[y], (uint)_RoiWidth);
                    if (retval != 0)
                    {
                        throw new Exception("Operation failed. No response from camera.");
                    }
                }

                _LibFLIWrapper._FLIUnlockDevice(_DeviceHandle);

                unsafe
                {
                    ushort* pData = (ushort*)capturedImage.BackBuffer.ToPointer();
                    for (int y = 0; y < _RoiHeight; y++)
                    {
                        for (int x = 0; x < _RoiWidth; x++)
                        {
                            *(pData + x + (y * _RoiWidth)) = (ushort)imgPixels[y][x];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _IsExposing = false;
            }
        }

        public override void StopCapture()
        {
            _LibFLIWrapper._FLIUnlockDevice(_DeviceHandle);
            _LibFLIWrapper._FLICancelExposure(_DeviceHandle);
            _IsExposing = false;
        }

        #endregion
    }
}
