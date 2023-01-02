using Sequlite.Image.Processing;
using Statistics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.ImageCorrectionTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int Threshold { get; set; } = 15000;
        public int WidthAdjust { get; set; }
        public int HeightAdjust { get; set; }
        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public int WidthRange { get; set; } = 10; //< Ranges used to automatic optimization
        public int HeightRange { get; set; } = 10;
        public int XOffsetRange { get; set; } = 10;
        public int YOffsetRange { get; set; } = 10;
        public string Image1Path { get; set; } = "No Image 1 Loaded";
        public string Image2Path { get; set; } = "No Image 2 Loaded";
        public bool DisplaySourceImagesEnabled { get; set; } = false;
        public bool NormalizeImagesEnabled { get; set; } = true;
        public double Score { get; set; } //< The average difference between the pixels of the two images. Lower is better
        public double BestScore { get; set; } = -1;//< The lowest score recorded since the last time the scores were reset

        private int _bestW;
        private int _bestH;
        private int _bestX;
        private int _bestY;


        private WriteableBitmap _image1source; //< original input images
        private WriteableBitmap _image2source;

        private WriteableBitmap _image1; //< input images after normalization
        private WriteableBitmap _image2;

        private WriteableBitmap _image1Modified; //< input images after affine transformations
        private WriteableBitmap _image2Modified;

        private WriteableBitmap _imageOverlay; //< computed overlay and difference images
        private WriteableBitmap _imageDiff;

        private BackgroundWorker _worker = null; //<
        private ParameterSweeper _sweeper = new ParameterSweeper();


        public MainWindow()
        {
            InitializeComponent();
            //DataContext = this;
        }
        /// <summary>
        /// Load an image and display bitmap and path on GUI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ButtonLoadImage1_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("load image 1");
            string path;
            if (LoadImageFile(out path, out _image1source))
            {
                Image1PathLabel.Text = $"Image 1: {path}";
                if (CheckBoxNormalize.IsChecked == true)
                {
                    _image1 = NormalizeImage(_image1source);
                }
                else
                {
                    _image1 = _image1source;
                }
                if (_image1 != null && _image2 != null)
                {
                    UpdateDifferenceAndOverlayImages();
                }
                Image1.Source = ConvertWriteableBitmapToBitmapImage(_image1);
            }
        }

        public void ButtonLoadImage2_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("load image 2");
            string path;
            if (LoadImageFile(out path, out _image2source))
            {
                Image2PathLabel.Text = $"Image 2: {path}";
                if (CheckBoxNormalize.IsChecked == true)
                {
                    _image2 = NormalizeImage(_image2source);
                }
                else
                {
                    _image2 = _image2source;
                }
                if (_image1 != null && _image2 != null)
                {
                    UpdateDifferenceAndOverlayImages();
                }
                Image2.Source = ConvertWriteableBitmapToBitmapImage(_image2);
            }
        }
        /// <summary>
        /// Prompt user for image path and attempt to load
        /// </summary>
        /// <param name="path"></param>
        /// <param name="img"></param>
        /// <returns></returns>
        private bool LoadImageFile(out string path, out WriteableBitmap img)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.InitialDirectory = "c:\\";
            dlg.Filter = "Image files (*.tif)|*.tif;*.jpg;*.jpeg;*.png;|All Files (*.*)|*.*";
            dlg.RestoreDirectory = true;

            if (dlg.ShowDialog() == true)
            {
                path = dlg.FileName;
                return LoadImage(path, out img);
            }
            else
            {
                path = "";
                img = null;
                return false;
            }
        }
        /// <summary>
        /// Read image into a WriteableBitmap. If both images are present, update the rest of the UI
        /// </summary>
        /// <param name="path"></param>
        /// <param name="img"></param>
        /// <returns></returns>
        private bool LoadImage(in string path, out WriteableBitmap img)
        {
            Stream imageStreamSource = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = new TiffBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            BitmapSource bitmapSource = decoder.Frames[0];

            img = new WriteableBitmap(bitmapSource);

            return true;
        }

        /// <summary>
        /// Helper to make BitmapImages for display in the UI
        /// </summary>
        /// <param name="wbm"></param>
        /// <returns></returns>
        public static BitmapImage ConvertWriteableBitmapToBitmapImage(WriteableBitmap wbm)
        {
            BitmapImage bmImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);
                bmImage.BeginInit();
                bmImage.CacheOption = BitmapCacheOption.OnLoad;
                bmImage.StreamSource = stream;
                bmImage.EndInit();
                bmImage.Freeze();
            }
            return bmImage;
        }

        public void ButtonApplyParameters_Click(object sender, RoutedEventArgs e)
        {
            UpdateDifferenceAndOverlayImages();
        }
        public void ButtonResetScores_Click(object sender, RoutedEventArgs e)
        {
            Score = 0;
            BestScore = double.MaxValue;

            TextBlockScore.Text = $"Score: N/A";
            TextBlockBestScore.Text = $"Best Score: N/A";
            TextBlockBestParam.Text = $"Best Parameters: N/A";

            _bestW = 0;
            _bestH = 0;
            _bestX = 0;
            _bestY = 0;
        }

        public void ButtonRestoreBest_Click(object sender, RoutedEventArgs e)
        {
            WidthAdjust = _bestW;
            HeightAdjust = _bestH;
            XOffset = _bestX;
            YOffset = _bestY;

            UpdateDifferenceAndOverlayImages();
        }
        public void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return)
            {
                return;
            }
            Debug.WriteLine("Updating on key down");
            UpdateDifferenceAndOverlayImages();
        }

        private void UpdateDifferenceAndOverlayImages()
        {
            if (_image1 != null && _image2 != null)
            {
                if (_worker != null && _worker.IsBusy)
                {
                    _worker.CancelAsync();
                }
                _worker = new BackgroundWorker
                {
                    WorkerSupportsCancellation = true,
                    WorkerReportsProgress = true
                };
                _worker.DoWork += WorkerTransform_DoWork;
                _worker.ProgressChanged += WorkerTransform_ProgressChanged;
                _worker.RunWorkerCompleted += WorkerTransform_RunWorkerCompleted;
                Tuple<WriteableBitmap, WriteableBitmap, int, int, int, int> data = new Tuple<WriteableBitmap, WriteableBitmap, int, int, int, int>(_image1, _image2, WidthAdjust, HeightAdjust, XOffset, YOffset);
                _worker.RunWorkerAsync(data);
            }
        }

        /// <summary>
        /// Process Images in another thread. Reports on progress and completion.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WorkerTransform_DoWork(object sender, DoWorkEventArgs e)
        {
            // reset the progress bar
            (sender as BackgroundWorker).ReportProgress(0);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            Tuple<WriteableBitmap, WriteableBitmap, int, int, int, int> data = (Tuple<WriteableBitmap, WriteableBitmap, int, int, int, int>)e.Argument;
            WriteableBitmap wbmp1 = data.Item1;
            WriteableBitmap wbmp2 = data.Item2;
            ImageTransformParameters p1 = new ImageTransformParameters();
            p1.WidthAdjust = data.Item3;
            p1.HeightAdjust = data.Item4;
            p1.XOffset = data.Item5;
            p1.YOffset = data.Item6;
            ImageTransformParameters p2 = new ImageTransformParameters();
            Dictionary<string, ImageTransformParameters> dict = new Dictionary<string, ImageTransformParameters>();
            dict.Add("G1", p1);
            dict.Add("G2", p2);
            ImageTransformer transformer = ImageTransformer.GetImageTransformer();

            Dispatcher.Invoke(new System.Action(() => { _ = transformer.Initialize(wbmp1.PixelWidth, wbmp1.PixelHeight, dict); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(10);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            Dispatcher.Invoke(new System.Action(() => { ImageTransformer.FastTransform(_image1, out _image1Modified, transformer.LookupParameters("G1")); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(30);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            Dispatcher.Invoke(new System.Action(() => { ImageTransformer.FastTransform(_image2, out _image2Modified, transformer.LookupParameters("G2")); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(50);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            Dispatcher.Invoke(new System.Action(() => { _imageOverlay = ComputeOverlayImage(_image1Modified, _image2Modified); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(60);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            double score = new double();
            Dispatcher.Invoke(new System.Action(() => { _imageDiff = ComputeDifferenceImage(_image1Modified, _image2Modified, out score, Threshold); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(80);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            // draw the updated overlay and difference images to the screen
            Dispatcher.Invoke(new System.Action(() =>
            {
                if (_imageOverlay != null)
                {
                    Image3.Source = ConvertWriteableBitmapToBitmapImage(_imageOverlay);
                }
                if (_imageDiff != null)
                {
                    Image4.Source = ConvertWriteableBitmapToBitmapImage(_imageDiff);
                }
            }), DispatcherPriority.Background);


            e.Result = score;
            (sender as BackgroundWorker).ReportProgress(100);

        }
        /// <summary>
        /// Update the UI with background worker progress
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WorkerTransform_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBarCompute.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Compute scores and update ui after transform is completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WorkerTransform_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            double score = (double)e.Result;

            Score = score;
            TextBlockScore.Text = $"Score: {Score:N2}";

            if (score < BestScore || BestScore < 0)
            {
                BestScore = score;
                TextBlockBestScore.Text = $"Best Score: {BestScore:N2}";
                _bestW = WidthAdjust;
                _bestH = HeightAdjust;
                _bestX = XOffset;
                _bestY = YOffset;
                TextBlockBestParam.Text = $"Best Parameters: W: {_bestW} H: {_bestH} X: {_bestX} Y: {_bestY}";
            }

            TextBlockFinalDimensions.Text = $"Final dimensions: w:{_imageOverlay.PixelWidth}px, h:{_imageOverlay.PixelHeight}px";
            TextBlockValidDimensions.Text = (_imageOverlay.PixelWidth % 2 == 0) ? "Valid dimensions" : "Invalid dimensions (width must be even)";
            TextBlockValidDimensions.Foreground = (_imageOverlay.PixelWidth % 2 == 0) ? Brushes.Green : Brushes.Red;
            Debug.WriteLine($"Score:{Score} Best:{BestScore}");
            Debug.WriteLine("Worker Done");

            _sweeper.CheckNext();
        }

        private static unsafe WriteableBitmap ComputeOverlayImage(WriteableBitmap image1, WriteableBitmap image2)
        {
            if (image1 == null | image2 == null)
                return null;
            if (image1.Height != image2.Height || image1.Width != image2.Width)
                return null;

            // note: use BGR32 for later conversion to bitmapimage
            WriteableBitmap imageOverlay = new WriteableBitmap(image1.PixelWidth, image1.PixelHeight, image1.DpiX, image1.DpiY, PixelFormats.Bgr32, null);

            int height = image1.PixelHeight;
            int width = image1.PixelWidth;

            int bitsPerPixelIn = image1.Format.BitsPerPixel;
            int bytesPerPixelIn = bitsPerPixelIn / 8;
            int widthBytesIn = width * bytesPerPixelIn;

            int bitsPerPixelOut = imageOverlay.Format.BitsPerPixel;
            int bytesPerPixelOut = bitsPerPixelOut / 8;
            int widthBytesOut = width * bytesPerPixelOut;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((width * bitsPerPixelIn + 31) & ~31) >> 3;
            int strideOut = ((width * bitsPerPixelOut + 31) & ~31) >> 3;

            //int rowPaddingIn = strideIn - (width * bytesPerPixelIn);
            //int rowPaddingOut = strideOut - (width * bytesPerPixelOut);

            byte* ptr1 = (byte*)image1.BackBuffer.ToPointer();
            byte* ptr2 = (byte*)image2.BackBuffer.ToPointer();
            byte* ptrOut = (byte*)imageOverlay.BackBuffer.ToPointer();

            // iterate over rows
            for (int row = 0; row < height; row++)
            {
                // iterate over columns
                byte* lineIn1 = ptr1 + (row * strideIn);
                byte* lineIn2 = ptr2 + (row * strideIn);
                byte* lineOut = ptrOut + (row * strideOut);
                for (int col = 0; col < width; col++)
                {
                    ushort image1pixel = ((ushort)(lineIn1[col * bytesPerPixelIn] | (ushort)lineIn1[col * bytesPerPixelIn + 1] << 8));
                    ushort image2pixel = ((ushort)(lineIn2[col * bytesPerPixelIn] | (ushort)lineIn2[col * bytesPerPixelIn + 1] << 8));
                    byte rOut = (byte)(image1pixel / 256.0);
                    byte gOut = (byte)(image2pixel / 256.0);
                    lineOut[col * bytesPerPixelOut] = 0;
                    lineOut[col * bytesPerPixelOut + 1] = gOut;
                    lineOut[col * bytesPerPixelOut + 2] = rOut;
                    lineOut[col * bytesPerPixelOut + 3] = 0;
                }
            }
            return imageOverlay;
        }
        private static unsafe WriteableBitmap ComputeDifferenceImage(WriteableBitmap image1, WriteableBitmap image2, out double score, int threshold = 15000)
        {
            RollingStats stats = new RollingStats();
            score = 0;

            if (image1 == null | image2 == null)
                return null;

            if (image1.Height != image2.Height || image1.Width != image2.Width)
                return null;

            Debug.WriteLine($"Computing difference. Threshold:{threshold}");

            // note: use BGR32 for later conversion to bitmapimage
            WriteableBitmap imageDiff = new WriteableBitmap(image1.PixelWidth, image1.PixelHeight, image1.DpiX, image1.DpiY, System.Windows.Media.PixelFormats.Bgr32, null);

            int height = image1.PixelHeight;
            int width = image1.PixelWidth;

            int bitsPerPixelIn = image1.Format.BitsPerPixel;
            int bytesPerPixelIn = bitsPerPixelIn / 8;
            int widthBytesIn = width * bytesPerPixelIn;

            int bitsPerPixelOut = imageDiff.Format.BitsPerPixel;
            int bytesPerPixelOut = bitsPerPixelOut / 8;
            int widthBytesOut = width * bytesPerPixelOut;

            // wpf aligns rows to 32-bit boundaries
            int strideIn = ((width * bitsPerPixelIn + 31) & ~31) >> 3;
            int strideOut = ((width * bitsPerPixelOut + 31) & ~31) >> 3;

            //int rowPaddingIn = strideIn - (width * bytesPerPixelIn);
            //int rowPaddingOut = strideOut - (width * bytesPerPixelOut);

            byte* ptr1 = (byte*)image1.BackBuffer.ToPointer();
            byte* ptr2 = (byte*)image2.BackBuffer.ToPointer();
            byte* ptrOut = (byte*)imageDiff.BackBuffer.ToPointer();

            //byte[] swapColor = new byte[bytesPerPixel];
            //swapColor[0] = matchColor.R;
            //swapColor[1] = matchColor.G;

            ulong newScore = 0;

            for (int row = 0; row < height; row++)
            {
                // iterate over columns
                byte* lineIn1 = ptr1 + (row * strideIn);
                byte* lineIn2 = ptr2 + (row * strideIn);
                byte* lineOut = ptrOut + (row * strideOut);
                for (int col = 0; col < width; col++)
                {
                    // compare pixels
                    ushort image1pixel = (ushort)(lineIn1[col * bytesPerPixelIn] | (lineIn1[col * bytesPerPixelIn + 1] << 8));
                    ushort image2pixel = (ushort)(lineIn2[col * bytesPerPixelIn] | (lineIn2[col * bytesPerPixelIn + 1] << 8));
                    int diff = System.Math.Abs(image1pixel - image2pixel);
                    if (diff > threshold)
                    {
                        //ptrOut[x] = (byte)(diff >> (x * 8));
                        lineOut[col * bytesPerPixelOut] = 0;
                        lineOut[col * bytesPerPixelOut + 1] = 0;
                        lineOut[col * bytesPerPixelOut + 2] = 0;
                        lineOut[col * bytesPerPixelOut + 3] = 0;
                    }
                    else
                    {
                        lineOut[col * bytesPerPixelOut] = 255;
                        lineOut[col * bytesPerPixelOut + 1] = 255;
                        lineOut[col * bytesPerPixelOut + 2] = 255;
                        lineOut[col * bytesPerPixelOut + 3] = 0;
                    }

                    stats.Update(diff);
                }
            }
            Debug.WriteLine($"Total difference score:{newScore}");

            score = stats.Mean;
            return imageDiff;
        }

        private static unsafe void ComputeImageStats(WriteableBitmap image, RollingStats stats)
        {
            int height = image.PixelHeight;
            int width = image.PixelWidth;

            int bitsPerPixel = image.Format.BitsPerPixel;
            int bytesPerPixel = bitsPerPixel / 8;
            int widthBytesIn = width * bytesPerPixel;

            // wpf aligns rows to 32-bit boundaries
            int stride = ((width * bitsPerPixel + 31) & ~31) >> 3;

            byte* ptr = (byte*)image.BackBuffer.ToPointer();

            for (int row = 0; row < height; row++)
            {
                byte* line = ptr + (row * stride);
                for (int col = 0; col < width; col++)
                {
                    ushort pixel = (ushort)(line[col * bytesPerPixel] | ((ushort)line[col * bytesPerPixel + 1] << 8));
                    stats.Update(pixel);
                }
            }
        }

        private static unsafe WriteableBitmap ComputeNormalizedImaged(WriteableBitmap image, RollingStats stats, ushort min, ushort max)
        {
            int height = image.PixelHeight;
            int width = image.PixelWidth;

            int bitsPerPixel = image.Format.BitsPerPixel;
            int bytesPerPixel = bitsPerPixel / 8;
            int widthBytesIn = width * bytesPerPixel;

            // wpf aligns rows to 32-bit boundaries
            int stride = ((width * bitsPerPixel + 31) & ~31) >> 3;

            WriteableBitmap output = image.Clone();
            byte* ptrIn = (byte*)image.BackBuffer.ToPointer();
            byte* ptrOut = (byte*)output.BackBuffer.ToPointer();

            for (int row = 0; row < height; row++)
            {
                byte* lineIn = ptrIn + (row * stride);
                byte* lineOut = ptrOut + (row * stride);
                for (int col = 0; col < width; col++)
                {
                    ushort value = (ushort)(lineIn[col * bytesPerPixel] | ((ushort)lineIn[col * bytesPerPixel + 1] << 8));
                    // scale input [min, max] to range [0, 65535]
                    double mappedValue = (value - min) * 65535.0 / (max - min);
                    if(mappedValue > 65535)
                    {
                        mappedValue = 65535;
                    }
                    if(mappedValue < 0)
                    {
                        mappedValue = 0;
                    }
                    ushort finalValue = (ushort)mappedValue;
                    lineOut[col * bytesPerPixel] = (byte)finalValue;
                    lineOut[col * bytesPerPixel + 1] = (byte)(finalValue >> 8);
                }
            }
            return output;
        }

        private void OnImageMouseWheel(object sender, MouseWheelEventArgs e)
        {
            System.Windows.Controls.Image sourceImage = e.Source as System.Windows.Controls.Image;
            if (sourceImage == null)
            {
                return;
            }

            var matrix = sourceImage.LayoutTransform.Value;

            Debug.WriteLine($"Center:{e.GetPosition(this).X},{e.GetPosition(this).Y}");

            if (e.Delta > 0)
            {
                matrix.ScaleAt(1.25, 1.25, e.GetPosition(this).X, e.GetPosition(this).Y);
            }
            else
            {
                matrix.ScaleAt(1.0 / 1.25, 1.0 / 1.25, e.GetPosition(this).X, e.GetPosition(this).Y);
            }

            sourceImage.LayoutTransform = new System.Windows.Media.MatrixTransform(matrix);

            e.Handled = true;
        }

        private void OnImageMouseDown(object sender, MouseEventArgs e)
        {
            System.Windows.Controls.Image sourceImage = e.Source as System.Windows.Controls.Image;
            if (sourceImage == null)
            {
                return;
            }

            var matrix = sourceImage.LayoutTransform.Value;
        }

        private void OnImageMouseUp(object sender, MouseEventArgs e)
        {

        }

        private void OnImageMouseMove(object sender, MouseEventArgs e)
        {

        }

        private void OnImageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            /*if (CameraVM != null)
            {
                var image = CameraVM.LiveImage;
                if (CameraVM.LiveImage == null) { return; }

                if (_LiveImage.Source == null)
                {
                    _ImageZoomRate = 1.0;
                    return;
                }
                if (_LiveImage.ActualWidth < _LiveImage.Width)
                {
                    _ImageZoomRate = image.PixelHeight / _LiveImage.ActualHeight;
                }
                else if (_LiveImage.ActualHeight < _LiveImage.Height)
                {
                    _ImageZoomRate = image.PixelWidth / _LiveImage.ActualWidth;
                }*/
        }

        private void ButtonAutoTune_Click(object sender, RoutedEventArgs e)
        {
            WidthAdjust = 0;
            HeightAdjust = 0;
            XOffset = 0;
            YOffset = 0;

            int total = (WidthRange + HeightRange + XOffsetRange + YOffsetRange) * 2;
            ProgressBarAuto.Minimum = 0;
            ProgressBarAuto.Maximum = total;
            Debug.WriteLine($"Running auto tune. Parameter space size: {total}");

            _sweeper.Initialize();

            _sweeper.CheckNext();
        }

        private void AutoTuneNext()
        {
            //if(AutoTuner.HasNext())
            //{

            //}
            //else
            //{
            //    // auto-tune is finished
            //}
            //WidthAdjust = AutoTuner.NextParam(0);
            //HeightAdjust = AutoTuner.NextParam(0);
            //XOffset = AutoTuner.NextParam(0);
            //YOffset = AutoTuner.NextParam(0);
            //UpdateDifferenceAndOverlayImages();
        }

        private void WorkerAutoTune_DoWork(object sender, DoWorkEventArgs e)
        {
            int best_w;
            Dispatcher.Invoke(new System.Action(() => { best_w = OptimizeParameter(WidthAdjust, WidthRange); }), DispatcherPriority.Background);
            (sender as BackgroundWorker).ReportProgress(25);
            //Dispatcher.Invoke(new System.Action(() => { WidthAdjust = best_w; }), DispatcherPriority.Background);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            int best_h;
            (sender as BackgroundWorker).ReportProgress(50);
            Dispatcher.Invoke(new System.Action(() => {  best_h = OptimizeParameter(HeightAdjust, HeightRange); }), DispatcherPriority.Background);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            int best_x;
            (sender as BackgroundWorker).ReportProgress(75);
            Dispatcher.Invoke(new System.Action(() => { best_x = OptimizeParameter(XOffset, XOffsetRange); }), DispatcherPriority.Background);
            if (_worker.CancellationPending) { e.Cancel = true; return; }

            int best_y;
            (sender as BackgroundWorker).ReportProgress(100);
            Dispatcher.Invoke(new System.Action(() => { best_y = OptimizeParameter(YOffset, YOffsetRange); }), DispatcherPriority.Background);
        }

        private void WorkerAutoTune_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBarAuto.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WorkerAutoTune_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Debug.WriteLine("AutoTune Done");
        }

        private int OptimizeParameter(int param, int range)
        {
            int best = 0;
            for (int i = (-1 * range); i < range; ++i)
            {
                param = i; //?
                //WidthAdjust = i;
                UpdateDifferenceAndOverlayImages();
                while (_worker.IsBusy) {; ; }
                if (Score < BestScore)
                    best = i;
            }
            return best;
        }

        private void CheckBoxNormalize_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxNormalize.IsChecked == true)
            {
                if (_image1source != null)
                {
                    _image1 = NormalizeImage(_image1source);
                    Image1.Source = ConvertWriteableBitmapToBitmapImage(_image1);
                }
                if (_image2source != null)
                {
                    _image2 = NormalizeImage(_image2source);
                    Image2.Source = ConvertWriteableBitmapToBitmapImage(_image2);
                }
            }
            else
            {
                if (_image1source != null)
                {
                    _image1 = _image1source;
                    Image1.Source = ConvertWriteableBitmapToBitmapImage(_image1);
                }
                if (_image2source != null)

                {
                    _image2 = _image2source;
                    Image2.Source = ConvertWriteableBitmapToBitmapImage(_image2);
                }
            }
        }

        private WriteableBitmap NormalizeImage(WriteableBitmap image)
        {
            RollingStats stats = new RollingStats();
            ComputeImageStats(image, stats);
            // set limits to 5 std.dev above and below the mean
            ushort min = (ushort)Math.Max(stats.Mean - 5.0 * stats.StdDev, stats.Min);
            ushort max = (ushort)Math.Min(stats.Mean + 5.0 * stats.StdDev, stats.Max);

            Debug.WriteLine($"Normalizing image to range: {min} : {max}. Stats: Mean: { stats.Mean} Min: {stats.Min} Max: {stats.Max} Std.Dev: {stats.StdDev}");
            return ComputeNormalizedImaged(image, stats, min, max);
        }

        void ShowStatusBarMessage(string msg)
        {
            statusBarText.Text = msg;
        }

        private void ButtonSaveTransformed_Click(object sender, RoutedEventArgs e)
        {
            if(_image1Modified != null && _image2Modified != null)
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.InitialDirectory = "c:\\";
                dlg.Filter = "Image files (*.tif)|*.tif;*.jpg;*.jpeg;*.png;|All Files (*.*)|*.*";
                dlg.RestoreDirectory = true;

                if (dlg.ShowDialog() == true)
                {
                    string path = dlg.FileName;
                    Debug.WriteLine($"Writing transformed images to:{path}");
                    if (path != string.Empty)
                    {
                        using (FileStream stream = new FileStream(path+"G1_transformed.tif", FileMode.Create))
                        {
                            TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(_image1Modified));
                            encoder.Save(stream);
                        }
                        using (FileStream stream = new FileStream(path + "G2_transformed.tif", FileMode.Create))
                        {
                            TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(_image2Modified));
                            encoder.Save(stream);
                        }
                    }
                }
            }
        }
    }

    class ParameterSweeper
    {
        public bool CheckNext()
        {
            return false;
        }

        public void Initialize()
        {

        }
        public void Start()
        {

        }
        public void Finish()
        {

        }
        
    }
}
