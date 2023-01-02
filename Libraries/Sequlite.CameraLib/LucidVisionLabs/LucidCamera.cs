using Sequlite.ALF.Common;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sequlite.CameraLib
{
    public class LucidCamera : ILucidCamera
    {
        #region Event statement
        public event TriggerStartHandle OnTriggerStartRequested;
        public event ExposureEndHandle ExposureEndNotif;
        #endregion Event statement

        #region Private Fields
        //private static ArenaNET.ISystem _System;
        private ArenaNET.IDevice _Device;
        private object _GetImageToken = new object();
        private bool _IsAcqRunning = false;
        //private bool _IsAbortCapture;
        private bool _PreviousSetOfTriggerMode;
        private ISeqLog Logger { get; } = SeqLogFactory.GetSeqFileLog("LucidCamera");
        private const UInt32 _Timeout = 4000; //ms
        #endregion Private Fields

        #region Public Properties
        public string ModelName { get; private set; }
        public string SerialNumber { get; private set; }
        public string DisplayName
        {
            get { return string.Format("C:{2}, SN:{1}, {0}", ModelName, SerialNumber, Channels); }
        }
        /// <summary>
        /// unit of ms
        /// </summary>
        public double Exposure { get; set; }
        public ArenaNET.EPfncFormat PixelFormat { get; set; }
        internal ArenaNET.IDevice Device
        {
            get { return _Device; }
        }
        public bool UsingContinuousMode { get; set; }
        public bool EnableTriggerMode { get; set; }
        public bool IsTriggerFromOutside { get; set; }
        public bool TriggeredByOutside { get; set; }
        public bool ContinuousModeStarted { get; private set; }
        /// <summary>
        /// two channels: G1/R3 and G2/R4
        /// </summary>
        public string Channels { get; set; }
        public bool IsRecipeImaging { get; set; }

        public int ImageId { private get; set; }
        public ArenaNET.IDevice ArenaDevice
        {
            get { return _Device; }
        }
        #endregion Public Properties

        #region Constructor
        public LucidCamera(ArenaNET.IDevice device, string modelName, string serialNumber)
        {
            _Device = device;
            ModelName = modelName;
            SerialNumber = serialNumber;
            IsConnected = device.IsConnected;
            // Get parameters: ROI, Exposure, PixelFormat
            var node = device.NodeMap.GetNode("Width") as ArenaNET.IInteger;
            if (node != null)
            {
                RoiWidth = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("Height") as ArenaNET.IInteger;
            if (node != null)
            {
                RoiHeight = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("OffsetX") as ArenaNET.IInteger;
            if (node != null)
            {
                RoiStartX = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("OffsetY") as ArenaNET.IInteger;
            if (node != null)
            {
                RoiStartY = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("BinningHorizontal") as ArenaNET.IInteger;
            if (node != null)
            {
                HBin = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("BinningVertical") as ArenaNET.IInteger;
            if (node != null)
            {
                VBin = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("SensorWidth") as ArenaNET.IInteger;
            if (node != null)
            {
                ImagingColumns = (int)node.Value;
            }
            node = _Device.NodeMap.GetNode("SensorHeight") as ArenaNET.IInteger;
            if (node != null)
            {
                ImagingRows = (int)node.Value;
            }
            var expNode = _Device.NodeMap.GetNode("ExposureTime") as ArenaNET.IFloat;
            if (node != null)
            {
                Exposure = expNode.Value * 0.001;        // convert from us to ms
            }
            var sizeNode = _Device.NodeMap.GetNode("GevSCPSPacketSize") as ArenaNET.IInteger;
            if (sizeNode != null)
            {
                sizeNode.Value = 9000;
            }
            //Max packet size
            var deviceStreamChannelPacketSizeNode =
                device.NodeMap.GetNode("DeviceStreamChannelPacketSize") as ArenaNET.IInteger;
            if (deviceStreamChannelPacketSizeNode != null)
            {
                deviceStreamChannelPacketSizeNode.Value = deviceStreamChannelPacketSizeNode.Max;
            }
            //Stream Auto Negotiate Packet Size
            var streamAutoNegotiatePacketSizeNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamAutoNegotiatePacketSize");
            if (streamAutoNegotiatePacketSizeNode != null)
            {
                streamAutoNegotiatePacketSizeNode.Value = true;
            }
            // Enable stream packet resend
            
            var streamPacketResendEnableNode = (ArenaNET.IBoolean)device.TLStreamNodeMap.GetNode("StreamPacketResendEnable");
            if (streamPacketResendEnableNode != null)
            {
                streamPacketResendEnableNode.Value = true;
            }
            var packetResendWinFrameCountNode = device.NodeMap.GetNode("PacketResendWindowFrameCount") as ArenaNET.IInteger;
            if (packetResendWinFrameCountNode != null)
            {
                packetResendWinFrameCountNode.Value = 8;
            }
            // Throughput reserve for resend missing packet
            var deviceLinkThroughputReservenode = device.NodeMap.GetNode("DeviceLinkThroughputReserve") as ArenaNET.IInteger;
            if (deviceLinkThroughputReservenode != null)
            {
                deviceLinkThroughputReservenode.Value = 15;
            }
            //Reverse
            var reverseNode = _Device.NodeMap.GetNode("ReverseX") as ArenaNET.IBoolean;
            reverseNode.Value = false;
            reverseNode = _Device.NodeMap.GetNode("ReverseY") as ArenaNET.IBoolean;
            reverseNode.Value = true;

            // config digital IO: line 0 to trigger source input; line 1 to exposure active output;
            var lineNode = _Device.NodeMap.GetNode("LineSelector") as ArenaNET.IEnumeration;
            lineNode.FromString("Line0");
            var lineModeNode = _Device.NodeMap.GetNode("LineMode") as ArenaNET.IEnumeration;
            lineModeNode.FromString("Input");
            lineNode.FromString("Line1");
            lineModeNode.FromString("Output");
            var lineSrcNode = _Device.NodeMap.GetNode("LineSource") as ArenaNET.IEnumeration;
            lineSrcNode.FromString("ExposureActive");
            IsRecipeImaging = false;
        }
        #endregion Constructor

        #region Private Functions
        private void _ContinuousProcess()
        {
            if (EnableTriggerMode && !_PreviousSetOfTriggerMode)
            {
                Thread.Sleep(1000);      // the camera would lost connection without this delay
            }
            _PreviousSetOfTriggerMode = EnableTriggerMode;
            Logger.Log($"Try to start stream-{SerialNumber}");
            _Device.StartStream(100);
            Logger.Log($"Start Stream - {SerialNumber}");
            if (EnableTriggerMode)
            {
                WaitTriggerArmed();
            }
            TriggeredByOutside = false;
            ContinuousModeStarted = true;
            ContinuousProcess_SingleThread();
        }
        private void ContinuousProcess_SingleThread()//object name)
        {
            try
            {
                string threadName = "LucidCam";
                Logger.Log("The thread " + threadName + " starts with IsAcqRunning = " + IsAcqRunning);
                while (IsAcqRunning)
                {
                    if (EnableTriggerMode)
                    {
                        if (IsTriggerFromOutside)
                        {
                            while (!TriggeredByOutside)
                            {
                                Thread.Sleep(1);
                                if (!IsAcqRunning)
                                {
                                    //lock (_GetImageToken)
                                    //{
                                    //    ContinuousModeStarted = false;
                                    //    _Device.StopStream();
                                    //    Logger.Log($"Stop Stream - {SerialNumber}");
                                    //}
                                    //Logger.Log("The thread " + threadName + " ends.");
                                    //return;
                                    break;
                                }
                            }
                            TriggeredByOutside = false;
                        }
                        else
                        {
                            OnTriggerStartRequested?.Invoke();
                        }
                    }
                    if (IsAcqRunning)
                    {
                        if (IsRecipeImaging)
                        {
                            Logger.Log($"{SerialNumber}-Wait Exposure finish");
                            _Device.WaitOnEvent((ulong)Exposure + _Timeout);
                            ExposureEndNotif?.Invoke(true);
                        }

                        var image = GetImageInThreadSafeMode();
                        if (image == null)
                        {
                            Thread.Sleep(1000);
                            image = GetImageInThreadSafeMode();
                        }
                        else if (image.IsIncomplete)
                        {
                            _Device.RequeueBuffer(image);
                            image = null;
                        }
                        if (image != null && CameraNotif?.GetInvocationList().Length > 0)
                        {
                            byte[] srcData = new byte[image.DataArray.Length];
                            Buffer.BlockCopy(image.DataArray, 0, srcData, 0, srcData.Length);
                            Logger.Log($"{SerialNumber}-Buffer Requeued");
                            _Device.RequeueBuffer(image);
                            CameraNotif?.Invoke(new CameraNotifArgs(ImageId, srcData));
                        }
                        else if (image == null && CameraNotif?.GetInvocationList().Length > 0) //faile to readout
                        {
                            byte[] srcData = null;
                            #region Reinitialize camera
                            Logger.Log($"ReStart Stream - {SerialNumber}");
                            _Device.StopStream();
                            Logger.Log($"Try to start stream-{SerialNumber}");
                            _Device.StartStream(100);
                            Logger.Log($"Start Stream - {SerialNumber}");
                            if (EnableTriggerMode)
                            {
                                WaitTriggerArmed();
                            }
                            TriggeredByOutside = false;
                            ContinuousModeStarted = true;
                            #endregion Reinitialize camera
                            CameraNotif?.Invoke(new CameraNotifArgs(ImageId, srcData));
                        }
                        Thread.Sleep(1);
                    }
                } //while
                lock (_GetImageToken)
                {
                    ContinuousModeStarted = false;
                    _Device.StopStream();
                    Logger.Log($"Stop Stream - {SerialNumber}");
                }
                Logger.Log("The thread " + threadName + " ends.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                if (ex.ToString().Contains("WaitOnEvent"))
                {
                    ExposureEndNotif?.Invoke(false);
                }
                ContinuousModeStarted = false;
                try
                {
                    _Device.StopStream();
                    Logger.Log($"Stop Stream - {SerialNumber}");
                }
                catch (Exception ex1)
                {
                    Logger.LogError(string.Format("{1} - {0}", ex1.ToString(), SerialNumber));
                }

            }
        }
        private ArenaNET.IImage GetImageInThreadSafeMode()
        {
            ArenaNET.IImage image = null;
            try
            {
                lock (_GetImageToken)
                {
                    if (IsAcqRunning)
                    {
                        Logger.Log($"{SerialNumber}-Start readout");
                        image = _Device.GetImage((ulong)(Exposure + _Timeout));
                        Logger.Log($"{SerialNumber}-Readout finished");
                        if (image.IsIncomplete)
                        {
                            double missprecent = Math.Round((double)image.SizeFilled/ (double)image.PayloadSize * 100, 2);
                            Logger.LogError($"{SerialNumber}-Data is incomplete, {missprecent}% filled, readout failed");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("{1} - {0}",ex.ToString(), SerialNumber));
                //return null;
            }
            return image;

        }
        private unsafe WriteableBitmap ToWriteableBitmap(ArenaNET.IImage src)
        {
            WriteableBitmap result = null;
            try
            {
                DateTime start = DateTime.Now;
                switch (src.PixelFormat)
                {
                    case ArenaNET.EPfncFormat.Mono8:
                        result = new WriteableBitmap((int)src.Width, (int)src.Height, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                        break;
                    case ArenaNET.EPfncFormat.Mono16:
                        result = new WriteableBitmap((int)src.Width, (int)src.Height, 96, 96, System.Windows.Media.PixelFormats.Gray16, null);
                        break;
                    default:
                        return null;
                }
                result.Lock();
                if (src.DataArray?.Length > 0)
                {
                    byte* bptr = (byte*)result.BackBuffer.ToPointer();
                    fixed (byte* p = src.DataArray)
                    {

                        Buffer.MemoryCopy(p, bptr, src.DataArray.Length, src.DataArray.Length);
                    }
                    result.Unlock();
                    if (result.CanFreeze)
                    {
                        result.Freeze();
                    }
                    DateTime end = DateTime.Now;
                    var deta = (end - start).TotalMilliseconds;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
            return result;
        }
        private void PresetCamera(bool autoExposure)
        {
            try
            {
                Logger.Log($"{SerialNumber}-Setting up camera");
                // 6. set trigger mode
                Logger.Log(String.Format("TriggerMode: {0}", EnableTriggerMode ? "On" : "Off"));
                var triggerModeNode = _Device.NodeMap.GetNode("TriggerMode") as ArenaNET.IEnumeration;
                if (EnableTriggerMode)
                {
                    
                    var triggerSrcNode = _Device.NodeMap.GetNode("TriggerSource") as ArenaNET.IEnumeration;
                    triggerSrcNode.FromString("Line0");
                    var triggerSelectorNode = _Device.NodeMap.GetNode("TriggerSelector") as ArenaNET.IEnumeration;
                    triggerSelectorNode.FromString("FrameStart");
                    var triggerActivationNode = _Device.NodeMap.GetNode("TriggerActivation") as ArenaNET.IEnumeration;
                    triggerActivationNode.FromString("RisingEdge");
                    //var triggerOverlapNode = _Device.NodeMap.GetNode("TriggerOverlap") as ArenaNET.IEnumeration;
                    //triggerOverlapNode.FromString("PreviousFrame");
                    triggerModeNode.FromString("On");
                }
                else
                {
                    triggerModeNode.FromString("Off");
                }
                //7. Acquisition Start Mode
                Logger.Log("AcquisitionStartMode:LowLatency");
                var acquisitionstartmodeNode = _Device.NodeMap.GetNode("AcquisitionStartMode") as ArenaNET.IEnumeration;
                acquisitionstartmodeNode.FromString("LowLatency");

                // 1. reset ROI to allow free set
                var roiNode = _Device.NodeMap.GetNode("OffsetY") as ArenaNET.IInteger;
                roiNode.Value = 0;
                roiNode = _Device.NodeMap.GetNode("OffsetX") as ArenaNET.IInteger;
                roiNode.Value = 0;

                roiNode = _Device.NodeMap.GetNode("Width") as ArenaNET.IInteger;
                roiNode.Value = ImagingColumns;
                roiNode = _Device.NodeMap.GetNode("Height") as ArenaNET.IInteger;
                roiNode.Value = ImagingRows;
                // 2. set Bin, ROI
                // reverse OffsetY since we had reversed Y
                int reversedY = ImagingRows / HBin - RoiStartY - RoiHeight;
                SetIntValue(Device.NodeMap, "BinningHorizontal", HBin);
                SetIntValue(_Device.NodeMap, "Width", RoiWidth);
                SetIntValue(_Device.NodeMap, "Height", RoiHeight);
                SetIntValue(Device.NodeMap, "OffsetX", RoiStartX);
                SetIntValue(Device.NodeMap, "OffsetY", reversedY);
                // 3. set ADC Bit Depth & pixel format
                var adcBitDepthNode = _Device.NodeMap.GetNode("ADCBitDepth") as ArenaNET.IEnumeration;
                adcBitDepthNode.Symbolic = string.Format("Bits{0}", ADCBitDepth);
                if (PixelFormatBitDepth == 8)
                {
                    PixelFormat = ArenaNET.EPfncFormat.Mono8;
                }
                else
                {
                    PixelFormat = ArenaNET.EPfncFormat.Mono16;
                }
                var pixelFormatNode = _Device.NodeMap.GetNode("PixelFormat") as ArenaNET.IEnumeration;
                pixelFormatNode.Symbolic = PixelFormat.ToString();
                // 4. set exposure
                Logger.Log(String.Format("Set Exposure:{0}", Exposure));
                // disable auto exposure
                var exposureAutoNode = _Device.NodeMap.GetNode("ExposureAuto") as ArenaNET.IEnumeration;
                if (!autoExposure)
                {
                    exposureAutoNode.Symbolic = "Off";
                    // set frame rate before setting exposure
                    var frameRateEnableNode = _Device.NodeMap.GetNode("AcquisitionFrameRateEnable") as ArenaNET.IBoolean;
                    frameRateEnableNode.Value = true;
                    SetFloatValue(_Device.NodeMap, "AcquisitionFrameRate", (float)(1 / (Exposure + 10) * 1000));     // frame rate = 1/exposure, here we multiply 1000 to convert exposure time unit to sec
                    SetFloatValue(_Device.NodeMap, "ExposureTime", (float)(Exposure * 1000));                   // convert exposure time unit from ms to us
                }
                else
                {
                    exposureAutoNode.Symbolic = "On";
                }
                // disable auto gain
                var gainAutoNode = (ArenaNET.IEnumeration)_Device.NodeMap.GetNode("GainAuto");
                gainAutoNode.FromString("Off");
                SetFloatValue(_Device.NodeMap, "Gain", (float)Gain);
                // 5. set acquisition mode
                var acqModeNode = _Device.NodeMap.GetNode("AcquisitionMode") as ArenaNET.IEnumeration;
                if (UsingContinuousMode)
                {
                    acqModeNode.FromString("Continuous");
                }
                else
                {
                    acqModeNode.FromString("SingleFrame");
                }
                var streamBufferHandlingModeNode = (ArenaNET.IEnumeration)_Device.TLStreamNodeMap.GetNode("StreamBufferHandlingMode");
                streamBufferHandlingModeNode.FromString("NewestOnly");
            }

            catch (Exception ex)
            {
                Logger.LogError(string.Format("{1} - {0}", ex.ToString(), SerialNumber));
                throw ex;
            }
        }
        private long SetIntValue(ArenaNET.INodeMap nodeMap, string nodeName, long value)
        {
            // get node
            var integerNode = (ArenaNET.IInteger)nodeMap.GetNode(nodeName);

            // Ensure increment
            //    If a node has an increment (all integer nodes & some float
            //    nodes), only multiples of the increment can be set. Ensure this
            //    by dividing and then multiplying by the increment. If a value
            //    is between two increments, this will push it to the lower
            //    value. Most minimum values are divisible by the increment. If
            //    not, this must also be considered in the calculation.
            value = (((value - integerNode.Min) / integerNode.Inc) * integerNode.Inc) + integerNode.Min;

            // Check min/max values
            //    Values must not be less than the minimum or exceed the maximum
            //    value of a node. If a value does so, simply push it within
            //    range.
            if (value < integerNode.Min)
                value = integerNode.Min;

            if (value > integerNode.Max && integerNode.Max > integerNode.Min)
                value = integerNode.Max;

            // set value
            integerNode.Value = value;

            // return value for output
            return value;
        }
        private double SetFloatValue(ArenaNET.INodeMap nodeMap, string nodeName, double value)
        {
            // get node
            var floatNode = nodeMap.GetNode(nodeName) as ArenaNET.IFloat;

            // Ensure increment
            //    If a node has an increment (all integer nodes & some float
            //    nodes), only multiples of the increment can be set. Ensure this
            //    by dividing and then multiplying by the increment. If a value
            //    is between two increments, this will push it to the lower
            //    value. Most minimum values are divisible by the increment. If
            //    not, this must also be considered in the calculation.
            if (floatNode.HasInc)
            {
                value = (((value - floatNode.Min) / floatNode.Inc) * floatNode.Inc) + floatNode.Min;
            }

            // Check min/max values
            //    Values must not be less than the minimum or exceed the maximum
            //    value of a node. If a value does so, simply push it within
            //    range.
            if (value < floatNode.Min)
                value = floatNode.Min;

            if (value > floatNode.Max)
                value = floatNode.Max;

            // set value
            floatNode.Value = value;

            // return value for output
            return value;
        }
        #endregion Private Functions

        #region Public Functions
        /// <summary>
        /// set camera's exposure. could be called during streaming to change exposure.
        /// </summary>
        /// <param name="expInMsec">Unit of msec</param>
        /// <returns></returns>
        public bool SetExposure(double expInMsec)
        {
            try
            {
                if (expInMsec == Exposure) { Logger.Log($"{SerialNumber} - Expsoure was already set"); return true; }
                Logger.Log(string.Format($"{SerialNumber} - Set exposure to {0}", expInMsec));
                var frameRateEnableNode = _Device.NodeMap.GetNode("AcquisitionFrameRateEnable") as ArenaNET.IBoolean;
                frameRateEnableNode.Value = true;
                SetFloatValue(_Device.NodeMap, "AcquisitionFrameRate", (float)(1 / (expInMsec + 1) * 1000));     // frame rate = 1/exposure, here we multiply 1000 to convert exposure time unit to sec
                SetFloatValue(_Device.NodeMap, "ExposureTime", (float)(expInMsec * 1000));                   // convert exposure time unit from ms to us
                Exposure = expInMsec;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(SerialNumber + "Failed to set camera's exposure: " + ex.Message);
                return false;
            }
        }
        public bool WaitTriggerArmed()
        {
            Logger.Log($"{SerialNumber} - Wait Arm trigger");
            int counts = 0;
            bool triggerArmed = false;
            do
            {
                var triggerArmedNode = (ArenaNET.IBoolean)_Device.NodeMap.GetNode("TriggerArmed");
                triggerArmed = triggerArmedNode.Value;
                counts++;
                //if (counts > 60 * 1000) 
                //{ 
                //    break;
                //    //return false; 
                //}
                Thread.Sleep(1);
            } while (triggerArmed == false && counts <= 60 * 1000);
            return triggerArmed;// true;
        }
        static public unsafe WriteableBitmap ToWriteableBitmap(int roiWidth, int roiHeight, byte[] dataarray, int pixelformat)
        {
            WriteableBitmap result = null;

            try
            {
                //if (pixelformat == 8)
                //{
                //    result = new WriteableBitmap(roiWidth, roiHeight, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                //}
                //else if (pixelformat == 16)
                //{
                //    result = new WriteableBitmap(roiWidth, roiHeight, 96, 96, System.Windows.Media.PixelFormats.Gray16, null);
                //}
                //result.Lock();
                //byte* bptr = (byte*)result.BackBuffer.ToPointer();
                if (dataarray != null && dataarray.Length > 0)
                {
                    if (pixelformat == 8)
                    {
                        result = new WriteableBitmap(roiWidth, roiHeight, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                    }
                    else if (pixelformat == 16)
                    {
                        result = new WriteableBitmap(roiWidth, roiHeight, 96, 96, System.Windows.Media.PixelFormats.Gray16, null);
                    }
                    result.Lock();
                    byte* bptr = (byte*)result.BackBuffer.ToPointer();
                    fixed (byte* p = dataarray)
                    {

                        Buffer.MemoryCopy(p, bptr, dataarray.Length, dataarray.Length);
                    }
                    result.Unlock();
                    if (result.CanFreeze)
                    {
                        result.Freeze();
                    }
                }
                else
                {
                    result = new WriteableBitmap(roiWidth, roiHeight, 96, 96, System.Windows.Media.PixelFormats.Gray16, null);
                    SeqLogFactory.GetSeqFileLog("LucidCamera").LogError("Null Data Array in ToWriteableBitmap");
                }

            }
            catch (Exception ex)
            {
                SeqLogFactory.GetSeqFileLog("LucidCamera").LogError(ex.ToString());
            }
            return result;
        }
        public bool SwitchExpoEvent(bool OnOrOff)
        {
            try
            {
                if (OnOrOff)
                {
                    //Enable Initialize Events for finish exposure
                    _Device.InitializeEvents();
                    var eventSelectorNode = (ArenaNET.IEnumeration)_Device.NodeMap.GetNode("EventSelector");
                    var eventNotificationNode = (ArenaNET.IEnumeration)_Device.NodeMap.GetNode("EventNotification");
                    eventSelectorNode.FromString("ExposureEnd");
                    eventNotificationNode.FromString("On");
                }
                else { _Device.DeinitializeEvents(); }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(String.Format("Set exposure event failed.{0}", ex.ToString()));
                return false;
            }

        }
        public bool WaitExposureEnd()
        {
            try
            {
                _Device.WaitOnEvent((ulong)Exposure + 2000);
                return true;
            }
            catch (Exception ex) { Logger.LogError(ex.ToString()); return false; }
        }
        //public byte[] ReadDataArray()
        #endregion Public Functions

        #region Implementation of CameraLibBase
        public event CameraNotifHandler CameraNotif;
        public event ExposureChangedHandle OnExposureChanged;

        public CameraMake CameraType { get; }
        public int RoiStartX { get; set; }
        public int RoiStartY { get; set; }
        public int RoiWidth { get; set; }
        public int RoiHeight { get; set; }
        public int ImagingColumns { get; }
        public int ImagingRows { get; }
        public int HBin { get; set; }
        public int VBin { get; set; }
        public double MinExposure { get; }
        public int ReadoutSpeed { get; set; }
        public bool Open()
        {
            return true;
            //       return LucidCameraManager.OpenCamera(SerialNumber);
        }
        public void Close()
        {
            //            LucidCameraManager.CloseCamera(this);
        }
        public double CCDTemperature
        {
            get
            {
                var temperNode = _Device.NodeMap.GetNode("DeviceTemperature") as ArenaNET.IFloat;
                if (temperNode != null)
                {
                    return temperNode.Value;
                }
                else
                {
                    return float.NaN;
                }
            }
        }
        public double CCDCoolerSetPoint { get; set; }
        public string FirmwareVersion { get; }
        public bool IsAbortCapture { get; set; }
        public bool ForceShutterOpen { get; set; }
        public int Gain { get; set; }
        public bool IsAcqRunning { get { return _IsAcqRunning; } }
        public bool IsDynamicDarkCorrection { get; set; }
        public int ADCBitDepth { get; set; }
        public int PixelFormatBitDepth { get; set; }
        public bool IsConnected { get; private set; }

        public void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage)
        {
            try
            {
                Exposure = exposureTime * 1e3;    // convert from sec  to milli sec
                UsingContinuousMode = false;
                PresetCamera(false);
                _PreviousSetOfTriggerMode = EnableTriggerMode;
                bool triggerArmed;
                TriggeredByOutside = false;
                _Device.StartStream();
                if (EnableTriggerMode)
                {
                    do
                    {
                        var triggerArmedNode = (ArenaNET.IBoolean)_Device.NodeMap.GetNode("TriggerArmed");
                        triggerArmed = triggerArmedNode.Value;
                        Thread.Sleep(1);
                    } while (triggerArmed == false);

                    if (IsTriggerFromOutside)
                    {
                        while (!TriggeredByOutside)
                        {
                            Thread.Sleep(1);
                        }
                    }
                    else
                    {
                        OnTriggerStartRequested?.Invoke();
                    }
                }
                ArenaNET.IImage image = null;
                lock (_GetImageToken)
                {
                    image = _Device.GetImage((ulong)(Exposure + _Timeout));

                }
                if (image != null || !image.IsIncomplete)
                {
                    capturedImage = ToWriteableBitmap(image);
                    _Device.RequeueBuffer(image);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                try
                {
                    _Device.StopStream();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }
        }

        public void GrabImage(double exposureTime,
                                       CaptureFrameType frameType,
                                       ref WriteableBitmap capturedImage,
                                       int lightType)
        {
            //no impl, or should just call the same function above??
        }

        public void StopCapture() //same as StopAcquisition??
        {
            try
            {
                //_IsAbortCapture = true;// this flag is not in  use, should be removed
                StopAcquisition();
            }
            catch
            {
            }
        }

        public void StartContinuousMode(double exposureTime)
        {
            try
            {
                lock (_GetImageToken)
                {
                    if (IsAcqRunning)
                    {
                        Logger.LogWarning("Continuous Mode is running, cannot start a new run");
                        return;
                    }
                    _IsAcqRunning = true;
                }

                Exposure = exposureTime * 1e3; //s to ms
                UsingContinuousMode = true;
                TriggeredByOutside = false;
                PresetCamera(false);
                _ContinuousProcess();
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("{1} - {0}", ex.ToString(), SerialNumber));
                try
                {
                    _Device.StopStream();
                }
                catch (Exception ex1)
                {
                    Logger.LogError(string.Format("{1} - {0}", ex1.ToString(), SerialNumber));
                }
                throw ex;
            }

        }

        public void StopAcquisition()
        {
            lock (_GetImageToken)
            {
                //if (_IsAcqRunning)
                {
                    _IsAcqRunning = false;
                    Logger.Log($"Stopping Camera Image Acquisition - {SerialNumber}");
                }
            }
            //if (!_IsAcqRunning) { return; }

            //_IsAcqRunning = false;
            ////_Device.StopStream();
        }
        #endregion Implementation of CameraLibBase
    }
}
