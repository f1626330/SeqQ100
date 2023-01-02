using Microsoft.Win32.SafeHandles;
using Sequlite.ALF.Common;
using Sequlite.Image.Processing;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public enum DirtyType { NewCreate, Modified, None }
    public delegate void ClosingFileEvent(FileViewModel fileVM);
    public class FileViewModel : PaneViewModel
    {
        public event ClosingFileEvent OnClosingFile;
        // Instantiate a SafeHandle instance.
        SafeHandle handle = new SafeFileHandle(IntPtr.Zero, true);
        
        #region Private Fields
        private string _PixelX = string.Empty;
        private string _PixelY = string.Empty;
        private string _PixelIntensity = string.Empty;
        private WriteableBitmap _SourceImage;
        private WriteableBitmap _DisplayImage;
        private ImageInfo _ImageInfo;
        private Sequlite.Image.Processing.ImageInfo _SequliteImageInfo = new Sequlite.Image.Processing.ImageInfo();
        private string _FilePath = null;
        private bool _IsDirty;
        // Use to distinguish between newly created image, and a modified image
        private DirtyType _DocDirtyType = DirtyType.None;
        private int _BlackValue;
        private int _WhiteValue;
        private double _GammaValue;
        private int _MaxPixelValue;

        // zoom info
        private double _ZoomRate = 1;
        private double _OffSetX = 0;
        private double _OffsetY = 0;
        #endregion Private Fields

        #region Constructors
        public FileViewModel(string filePath)
        {
            
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    FilePath = filePath;
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.None | BitmapCreateOptions.PreservePixelFormat;
                    image.StreamSource = fs;
                    image.EndInit();
                    image.Freeze();
                    _SourceImage = new WriteableBitmap(image);
                    fs.Close();
                    int width = image.PixelWidth;
                    int height = image.PixelHeight;
                    var format = image.Format;
                    _DisplayImage = new WriteableBitmap(image);
                }
                if (_SourceImage.CanFreeze)
                {
                    _SourceImage.Freeze();
                }

                _ImageInfo = ImageProcessing.ReadMetadata(filePath);
                if (_ImageInfo == null)
                {
                    _ImageInfo = new ImageInfo();
                    _ImageInfo.SelectedChannel = ImageChannelType.Mix;
                }
                // Set max pixel value
                int bpp = _SourceImage.Format.BitsPerPixel;
                MaxPixelValue = (bpp == 16 || bpp == 48 || bpp == 64) ? 65535 : 255;
                WhiteValue = MaxPixelValue;
                BlackValue = 0;
                GammaValue = 1.0;

                Title = Path.GetFileName(filePath);
            }
            catch (InvalidCastException)
            {
                if (_ImageInfo == null)
                {
                    _ImageInfo = new ImageInfo();
                    _ImageInfo.SelectedChannel = ImageChannelType.Mix;
                    // Set max pixel value
                    int bpp = _SourceImage.Format.BitsPerPixel;
                    MaxPixelValue = (bpp == 16 || bpp == 48 || bpp == 64) ? 65535 : 255;
                    WhiteValue = MaxPixelValue;
                    BlackValue = 0;
                    GammaValue = 1.0;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            _SequliteImageInfo.SelectedChannel = Sequlite.Image.Processing.ImageChannelType.Mix;
            _SequliteImageInfo.MixChannel.IsAutoChecked = true;
            UpdateDisplayImage();
        }

        public FileViewModel( WriteableBitmap newImage, ImageInfo imageInfo, string title)
        {
            FilePath = null;
            Title = title;

            _SourceImage = newImage;
            ImageInfo = imageInfo;

            int bpp = newImage.Format.BitsPerPixel;


            BitmapPalette palette;
            PixelFormat dstPixelFormat = PixelFormats.Rgb24;
            if (bpp == 8 || bpp == 16)
            {
                _ImageInfo.NumOfChannels = 1;
                bool bIsSaturation = _ImageInfo.MixChannel.IsSaturationChecked;
                dstPixelFormat = PixelFormats.Indexed8;
                palette = new BitmapPalette(Sequlite.Image.Processing.ImageProcessing.GetColorTableIndexed(bIsSaturation));
                _DisplayImage = new WriteableBitmap(Width, Height, 96, 96, dstPixelFormat, palette);
            }
            else if (bpp == 24 || bpp == 48 || bpp == 64)
            {
                dstPixelFormat = PixelFormats.Rgb24;
                palette = null;
                _DisplayImage = new WriteableBitmap(Width, Height, 96, 96, dstPixelFormat, palette);
            }

            MaxPixelValue = (bpp == 16 || bpp == 48 || bpp == 64) ? 65535 : 255;

            _SequliteImageInfo.SelectedChannel = Sequlite.Image.Processing.ImageChannelType.Mix;
            _SequliteImageInfo.MixChannel.IsAutoChecked = true;
            UpdateDisplayImage();
        }
        #endregion Constructors


        #region Public Properties
        public WriteableBitmap SourceImage
        {
            get { return _SourceImage; }
        }

        public WriteableBitmap DisplayImage
        {
            get { return _DisplayImage; }
            set
            {
                if (_DisplayImage != value)
                {
                    _DisplayImage = value;
                    RaisePropertyChanged(nameof(DisplayImage));
                }
            }
        }

        public ImageInfo ImageInfo
        {
            get { return _ImageInfo; }
            set { _ImageInfo = value; }
        }

        public int Width
        {
            get
            {
                if (_SourceImage == null) { return 0; }
                return _SourceImage.PixelWidth;
            }
        }

        public int Height
        {
            get
            {
                if (_SourceImage == null) { return 0; }
                return _SourceImage.PixelHeight;
            }
        }

        public string FilePath
        {
            get
            {
                return _FilePath;
            }
            set
            {
                if (_FilePath != value)
                {
                    _FilePath = value;
                    RaisePropertyChanged(nameof(FilePath));
                    RaisePropertyChanged(nameof(FileName));
                    RaisePropertyChanged(nameof(Title));
                }
            }
        }
        public string FileName
        {
            get
            {
                if (FilePath == null)
                {
                    return Title + (IsDirty ? "*" : "");
                }
                return Path.GetFileName(FilePath) + (IsDirty ? "*" : "");
            }
        }
        public bool IsDirty
        {
            get { return _IsDirty; }
            set
            {
                if (_IsDirty != value)
                {
                    _IsDirty = value;
                    RaisePropertyChanged(nameof(IsDirty));
                    RaisePropertyChanged(nameof(FileName));
                    if (!_IsDirty)
                    {
                        _DocDirtyType = DirtyType.None;
                    }
                }
            }
        }
        public DirtyType DocDirtyType
        {
            get { return _DocDirtyType; }
            set
            {
                if (_DocDirtyType != value)
                {
                    _DocDirtyType = value;
                    RaisePropertyChanged("FileDirtyType");
                    if (_DocDirtyType == DirtyType.NewCreate ||
                        _DocDirtyType == DirtyType.Modified)
                    {
                        IsDirty = true;
                    }
                    else
                    {
                        IsDirty = false;
                    }
                }
            }
        }
        public int BlackValue
        {
            get { return _BlackValue; }
            set
            {
                if (_SourceImage != null && _ImageInfo != null)
                {
                    if (value >= _WhiteValue)
                    {
                        value = _WhiteValue - 1;
                    }
                    _BlackValue = value;
                    if (_ImageInfo.SelectedChannel == ImageChannelType.Red)
                    {
                        if (_ImageInfo.RedChannel.BlackValue != value)
                        {
                            _ImageInfo.RedChannel.BlackValue = value;
                            RaisePropertyChanged(nameof(BlackValue));
                        }
                    }
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Green)
                    {
                        if (_ImageInfo.GreenChannel.BlackValue != value)
                        {
                            _ImageInfo.GreenChannel.BlackValue = value;
                            RaisePropertyChanged(nameof(BlackValue));
                        }
                    }
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Blue)
                    //{
                    //    if (_ImageInfo.BlueChannel.BlackValue != value)
                    //    {
                    //        _ImageInfo.BlueChannel.BlackValue = value;
                    //        RaisePropertyChanged(nameof(BlackValue));
                    //    }
                    //}
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Gray)
                    //{
                    //    if (_ImageInfo.GrayChannel.BlackValue != value)
                    //    {
                    //        _ImageInfo.GrayChannel.BlackValue = value;
                    //        RaisePropertyChanged(nameof(BlackValue));
                    //    }
                    //}
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Mix)
                    {
                        if (_ImageInfo.MixChannel.BlackValue != value)
                        {
                            _ImageInfo.MixChannel.BlackValue = value;
                            RaisePropertyChanged(nameof(BlackValue));
                        }
                    }
                }
            }
        }
        public int WhiteValue
        {
            get { return _WhiteValue; }
            set
            {
                if (_SourceImage != null && _ImageInfo != null)
                {
                    if (value <= _BlackValue)
                    {
                        value = _BlackValue + 1;
                    }
                    _WhiteValue = value;
                    if (_ImageInfo.SelectedChannel == ImageChannelType.Red)
                    {
                        if (_ImageInfo.RedChannel.WhiteValue != value)
                        {
                            _ImageInfo.RedChannel.WhiteValue = value;
                            RaisePropertyChanged(nameof(WhiteValue));
                        }
                    }
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Green)
                    {
                        if (_ImageInfo.GreenChannel.WhiteValue != value)
                        {
                            _ImageInfo.GreenChannel.WhiteValue = value;
                            RaisePropertyChanged(nameof(WhiteValue));
                        }
                    }
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Blue)
                    //{
                    //    if (_ImageInfo.BlueChannel.WhiteValue != value)
                    //    {
                    //        _ImageInfo.BlueChannel.WhiteValue = value;
                    //        RaisePropertyChanged(nameof(WhiteValue));
                    //    }
                    //}
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Gray)
                    //{
                    //    if (_ImageInfo.GrayChannel.WhiteValue != value)
                    //    {
                    //        _ImageInfo.GrayChannel.WhiteValue = value;
                    //        RaisePropertyChanged(nameof(WhiteValue));
                    //    }
                    //}
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Mix)
                    {
                        if (_ImageInfo.MixChannel.WhiteValue != value)
                        {
                            _ImageInfo.MixChannel.WhiteValue = value;
                            RaisePropertyChanged(nameof(WhiteValue));
                        }
                    }
                }
            }
        }
        public double GammaValue
        {
            get { return _GammaValue; }
            set
            {
                if (_SourceImage != null && _ImageInfo != null)
                {
                    _GammaValue = value;
                    if (_ImageInfo.SelectedChannel == ImageChannelType.Red)
                    {
                        if (_ImageInfo.RedChannel.GammaValue != value)
                        {
                            _ImageInfo.RedChannel.GammaValue = value;
                            RaisePropertyChanged(nameof(GammaValue));
                        }
                    }
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Green)
                    {
                        if (_ImageInfo.GreenChannel.GammaValue != value)
                        {
                            _ImageInfo.GreenChannel.GammaValue = value;
                            RaisePropertyChanged(nameof(GammaValue));
                        }
                    }
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Blue)
                    //{
                    //    if (_ImageInfo.BlueChannel.GammaValue != value)
                    //    {
                    //        _ImageInfo.BlueChannel.GammaValue = value;
                    //        RaisePropertyChanged(nameof(GammaValue));
                    //    }
                    //}
                    //else if (_ImageInfo.SelectedChannel == ImageChannelType.Gray)
                    //{
                    //    if (_ImageInfo.GrayChannel.GammaValue != value)
                    //    {
                    //        _ImageInfo.GrayChannel.GammaValue = value;
                    //        RaisePropertyChanged(nameof(GammaValue));
                    //    }
                    //}
                    else if (_ImageInfo.SelectedChannel == ImageChannelType.Mix)
                    {
                        if (_ImageInfo.MixChannel.GammaValue != value)
                        {
                            _ImageInfo.MixChannel.GammaValue = value;
                            RaisePropertyChanged(nameof(GammaValue));
                        }
                    }
                }
            }
        }
        public int MaxPixelValue
        {
            get { return _MaxPixelValue; }
            set
            {
                if (_MaxPixelValue != value)
                {
                    _MaxPixelValue = value;
                    RaisePropertyChanged(nameof(MaxPixelValue));
                }
            }
        }
        #region public int LargeChange
        private int _LargeChange = 10;
        public int LargeChange
        {
            get
            {
                if (_SourceImage != null)
                {
                    //_LargeChange = (MaxPixelValue > 255) ? ((MaxPixelValue + 1) / 256) : 10;
                    _LargeChange = (MaxPixelValue > 255) ? 60 : 20;
                }

                return _LargeChange;
            }
            set
            {
                if (_LargeChange != value)
                {
                    _LargeChange = value;
                    RaisePropertyChanged("LargeChange");
                }
            }
        }
        #endregion

        #region public int SmallChange
        private int _SmallChange = 1;
        public int SmallChange
        {
            get
            {
                if (_SourceImage != null)
                {
                    //_SmallChange = (MaxPixelValue > 255) ? ((MaxPixelValue + 1) / 256) : 10;
                    _SmallChange = (MaxPixelValue > 255) ? 30 : 10;
                }

                return _SmallChange;
            }
            set
            {
                if (_SmallChange != value)
                {
                    _SmallChange = value;
                    RaisePropertyChanged("SmallChange");
                }
            }
        }
        #endregion

        public string PixelX
        {
            get { return _PixelX; }
            set
            {
                if (_PixelX != value)
                {
                    _PixelX = value;
                    RaisePropertyChanged(nameof(PixelX));
                }
            }
        }
        public string PixelY
        {
            get { return _PixelY; }
            set
            {
                if (_PixelY != value)
                {
                    _PixelY = value;
                    RaisePropertyChanged(nameof(PixelY));
                }
            }
        }
        public string PixelIntensity
        {
            get { return _PixelIntensity; }
            set
            {
                if (_PixelIntensity != value)
                {
                    _PixelIntensity = value;
                    RaisePropertyChanged(nameof(PixelIntensity));
                }
            }
        }

        public double ZoomRate
        {
            get { return _ZoomRate; }
            set
            {
                if (_ZoomRate != value)
                {
                    _ZoomRate = value;
                    RaisePropertyChanged(nameof(ZoomRate));
                }
            }
        }
        public double OffsetX
        {
            get { return _OffSetX; }
            set
            {
                if (_OffSetX != value)
                {
                    _OffSetX = value;
                    RaisePropertyChanged(nameof(OffsetX));
                }
            }
        }
        public double OffsetY
        {
            get { return _OffsetY; }
            set
            {
                if (_OffsetY != value)
                {
                    _OffsetY = value;
                    RaisePropertyChanged(nameof(OffsetY));
                }
            }
        }
        #endregion Public Properties

        public override void Close()

        {
            //Workspace.This.ImageGalleryVM.CloseFile(this);
            if (OnClosingFile != null)
            {
                OnClosingFile(this);
                //OnClosingFile = (ClosingFileEvent)EventRaiser.RaiseEventAsync(OnClosingFile, new object[] { this });
            }
        }
        

        #region Dispose
        // Flag: Has Dispose already been called?
        bool _Disposed = false;
        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            if (_Disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                // Free any other managed objects here.
                //

                if (_SourceImage != null)
                {
                    try
                    {
                        // In extreme situations force a garbage collection to free 
                        // up memory as quickly as possible.
                        if (_SourceImage != null &&
                            _SourceImage.PixelHeight * _SourceImage.PixelWidth > (10000 * 10000))
                        {
                            _SourceImage = null;
                            _DisplayImage = null;
                            // Forces an immediate garbage collection.
                            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                            //GC.WaitForPendingFinalizers();
                            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                            // Force garbage collection.
                            GC.Collect();
                            // Wait for all finalizers to complete before continuing.
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        }
                        else
                        {
                            _SourceImage = null;
                            _DisplayImage = null;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            // Free any unmanaged objects here.
            //

            _Disposed = true;
            // Call base class implementation.
            base.Dispose(disposing);
            GC.SuppressFinalize(this);
        }

        #endregion Dispose

        public void UpdateDisplayImage()
        {
            _SequliteImageInfo.SelectedChannel = Sequlite.Image.Processing.ImageChannelType.Mix;
            _SequliteImageInfo.MixChannel.BlackValue = BlackValue;
            _SequliteImageInfo.MixChannel.WhiteValue = WhiteValue;
            _DisplayImage.Lock();
            Sequlite.Image.Processing.ImageProcessingHelper.UpdateDisplayImage(
                ref _SourceImage, _SequliteImageInfo, ref _DisplayImage);
            _DisplayImage.AddDirtyRect(new System.Windows.Int32Rect(0, 0, Width, Height));
            _DisplayImage.Unlock();
            RaisePropertyChanged(nameof(DisplayImage));
            WhiteValue = _SequliteImageInfo.MixChannel.WhiteValue;
            BlackValue = _SequliteImageInfo.MixChannel.BlackValue;
            GammaValue = _SequliteImageInfo.MixChannel.GammaValue;
            _SequliteImageInfo.MixChannel.IsAutoChecked = false;
        }

    }
}
