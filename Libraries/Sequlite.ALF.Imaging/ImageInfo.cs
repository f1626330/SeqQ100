using System;
using System.Runtime.Serialization;
using System.Windows;

namespace Sequlite.ALF.Imaging
{
    public enum ImageChannelFlag
    {
        None = 0x00,
        Red = 0x01,
        Green = 0x02,
        Blue = 0x04,
        Gray = 0x08,   //following ImageJ image color channel naming scheme (see ImageJ color merge)
    }

    [Serializable()]
    public enum ImageChannelType
    {
        None,
        Red,
        Green,
        Blue,
        Gray,
        Mix,    //All channels
    }

    [Serializable()]
    public class ImageChannel : ICloneable
    {
        public ImageChannelType ColorChannel { get; set; }
        public double Exposure { get; set; }
        public string LightSource { get; set; }
        public int LightIntensity { get; set; }
        public int FilterPosition { get; set; }
        public double FocusPosition { get; set; }
        public double YPosition { get; set; }
        public int PDValue { get; set; }
        public int BlackValue { get; set; }
        public int WhiteValue { get; set; }
        public double GammaValue { get; set; }
        public bool IsAutoChecked { get; set; }
        public bool IsInvertChecked { get; set; }
        public bool IsSaturationChecked { get; set; }
        public int PrevBlackValue { get; set; }
        public int PrevWhiteValue { get; set; }
        public double PrevGammaValue { get; set; }
        public bool IsAutoFocus { get; set; }
        public Int32Rect ROI { get; set; }

        public ImageChannel(ImageChannelType colorChannelType = ImageChannelType.Mix)
        {
            this.ColorChannel = colorChannelType;
            this.BlackValue = 0;
            this.WhiteValue = 65535;
            this.GammaValue = 1.0;
        }

        // Copy constructor (deep copy)
        public ImageChannel(ImageChannel otherImageChannel)
        {
            this.ColorChannel = otherImageChannel.ColorChannel;
            this.Exposure = otherImageChannel.Exposure;
            this.LightSource = otherImageChannel.LightSource;
            this.FilterPosition = otherImageChannel.FilterPosition;
            this.FocusPosition = otherImageChannel.FocusPosition;
            this.BlackValue = otherImageChannel.BlackValue;
            this.WhiteValue = otherImageChannel.WhiteValue;
            this.GammaValue = otherImageChannel.GammaValue;
            this.IsAutoChecked = otherImageChannel.IsAutoChecked;
            this.IsInvertChecked = otherImageChannel.IsInvertChecked;
            this.IsSaturationChecked = otherImageChannel.IsSaturationChecked;
            this.PrevBlackValue = otherImageChannel.PrevBlackValue;
            this.PrevWhiteValue = otherImageChannel.PrevWhiteValue;
            this.PrevGammaValue = otherImageChannel.PrevGammaValue;
            this.IsAutoFocus = otherImageChannel.IsAutoFocus;
        }

        public object Clone()
        {
            ImageChannel clone = (ImageChannel)this.MemberwiseClone();
            clone.ColorChannel = new ImageChannelType();
            clone.ColorChannel = this.ColorChannel;
            return clone;
        }
    }

    [Serializable()]
    public class ImageInfo : ICloneable
    {
        #region Private fields/data...

        [OptionalField(VersionAdded = 2)]
        private ImageChannel _RedChannel = null;    // Channel 1
        [OptionalField(VersionAdded = 2)]
        private ImageChannel _GreenChannel = null;  // Channel 2
        [OptionalField(VersionAdded = 2)]
        private ImageChannel _BlueChannel = null;   // Channel 3
        [OptionalField(VersionAdded = 2)]
        private ImageChannel _GrayChannel = null;   // Channel 4 - gray channel (4-channel image)
        [OptionalField(VersionAdded = 2)]
        private ImageChannel _MixChannel = null;   // RGB composite

        #endregion

        #region Public properties...

        // Capture software version string
        public string SoftwareVersion { get; set; }

        public string CameraFirmware { get; set; }
        // User's comment
        public string Comment { get; set; }

        // Camera readout speed
        public string ReadoutSpeed { get; set; }


        // Color image channels
        public ImageChannel RedChannel
        {
            get { return _RedChannel; }
            set { _RedChannel = value; }
        }
        public ImageChannel GreenChannel
        {
            get { return _GreenChannel; }
            set { _GreenChannel = value; }
        }
        public ImageChannel BlueChannel
        {
            get { return _BlueChannel; }
            set { _BlueChannel = value; }
        }
        public ImageChannel GrayChannel
        {
            get { return _GrayChannel; }
            set { _GrayChannel = value; }
        }
        // Composite image
        public ImageChannel MixChannel
        {
            get { return _MixChannel; }
            set { _MixChannel = value; }
        }

        public int NumOfChannels { get; set; }
        // Handle multi-channel (4-channel) image
        //public ImageChannelFlag SelectedChannelFlags { get; set; }

        public int Aperture { get; set; }
        public int BinFactor { get; set; }
        public string DateTime { get; set; }
        public string CaptureType { get; set; }
        public string Calibration { get; set; }

        // Current selected color image channel
        public ImageChannelType SelectedChannel { get; set; }

        public int SaturationThreshold { get; set; }
        public int GainValue { get; set; }  //Camera gain
        public ushort PhotometricInterpretation { get; set; }
        public bool IsPixelInverted { get; set; }

        // Device imaging info
        public string McuFirmware { get; set; }

        #endregion

        #region Constructors...

        public ImageInfo()
        {
            _RedChannel = new ImageChannel(ImageChannelType.Red);       // Channel 1
            _GreenChannel = new ImageChannel(ImageChannelType.Green);   // Channel 2
            //_BlueChannel = new ImageChannel(ImageChannelType.Blue);     // Channel 3
            //_GrayChannel = new ImageChannel(ImageChannelType.Gray);     // Channel 4 - gray channel (4-channel image)
            _MixChannel = new ImageChannel(ImageChannelType.Mix);       // composite

            this.NumOfChannels = 1;

            this.SelectedChannel = ImageChannelType.Mix;

            this.DateTime = string.Empty;
            this.Calibration = string.Empty;
            this.GainValue = -1;
            this.SaturationThreshold = 62000;
            // 0 = WhiteIsZero; 1 = BlackIsZero; 2 = RGB
            this.PhotometricInterpretation = 1;
        }

        #endregion

        #region Public methods...
        public object Clone()
        {
            ImageInfo clone = new ImageInfo();
            clone = this.MemberwiseClone() as ImageInfo;
            clone.SelectedChannel = this.SelectedChannel;
            clone.RedChannel = (this._RedChannel != null) ? (ImageChannel)this._RedChannel.Clone() : null;
            clone.GreenChannel = (this._GreenChannel != null) ? (ImageChannel)this._GreenChannel.Clone() : null;
            //clone.BlueChannel = (this._BlueChannel != null) ? (ImageChannel)this._BlueChannel.Clone() : null;
            //clone.GrayChannel = (this._GrayChannel != null) ? (ImageChannel)this._GrayChannel.Clone() : null;
            clone.MixChannel = (this._MixChannel != null) ? (ImageChannel)this._MixChannel.Clone() : null;
            return clone;
        }

        //public object Clone()
        //{
        //    using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
        //    {
        //        if (this.GetType().IsSerializable)
        //        {
        //            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        //            formatter.Serialize(memStream, this);
        //            memStream.Flush();
        //            memStream.Position = 0;
        //            return formatter.Deserialize(memStream);
        //        }
        //        return null;
        //    }
        //}

        #endregion

    }
}
