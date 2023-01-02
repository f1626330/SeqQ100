using Sequlite.ALF.EngineerGUI.ViewModel;
using Sequlite.Image.Processing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// ImageViewerControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImageViewerControl : UserControl
    {
        #region Private Fields
        private Point origin;
        private Point start;
        private double _ZoomRate = 1;
        private const double _ZoomRateStep = 1.1;
        private double _ImageZoomRate = 1;
        private double _ShiftX;
        private double _ShiftY;
        private FileViewModel _Vm;
        #endregion Private Fields

        public ImageViewerControl()
        {
            InitializeComponent();
            this.Loaded += ImageViewerControl_Loaded;
        }

        private void ImageViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            _Vm = DataContext as FileViewModel;
            if (_Vm != null)
            {

                _ZoomRate = _Vm.ZoomRate;
                _ShiftX = _Vm.OffsetX;
                _ShiftY = _Vm.OffsetY;
                MatrixTransform m = new MatrixTransform(_ZoomRate, 0, 0, _ZoomRate, _ShiftX, _ShiftY);
                _DisplayImage.RenderTransform = m;
            }
        }

        private void _DisplayImage_MouseMove(object sender, MouseEventArgs e)
        {
            FileViewModel vm = DataContext as FileViewModel;
            if (vm != null)
            {
                Point p = new Point(e.GetPosition(_DisplayImage).X * _ImageZoomRate, e.GetPosition(_DisplayImage).Y * _ImageZoomRate);
                vm.PixelX = ((int)p.X).ToString();
                vm.PixelY = ((int)p.Y).ToString();

                int iRedData = 0;
                int iGreenData = 0;
                int iBlueData = 0;
                int iGrayData = 0;
                ImageProcessingHelper.GetPixelIntensity(vm.SourceImage, p, ref iRedData, ref iGreenData, ref iBlueData, ref iGrayData);
                if (vm.SourceImage.Format == PixelFormats.Gray8 || vm.SourceImage.Format == PixelFormats.Gray16)
                {
                    vm.PixelIntensity = string.Format("{0}", iRedData);
                }
                else if (vm.SourceImage.Format == PixelFormats.Bgr24 ||
                    vm.SourceImage.Format == PixelFormats.Rgb24 ||
                    vm.SourceImage.Format == PixelFormats.Rgb48)
                {
                    vm.PixelIntensity = string.Format("R:{0} G:{1} B:{2}", iRedData, iGreenData, iBlueData);
                }
                else
                {
                    vm.PixelIntensity = string.Format("R:{0} G:{1} B:{2} K:{3}", iRedData, iGreenData, iBlueData, iGrayData);
                }
            }

            if (_DisplayImage.IsMouseCaptured)
            {
                Point p = e.MouseDevice.GetPosition(_DisplayCanvas);
                Matrix m = _DisplayImage.RenderTransform.Value;
                double dx = origin.X + (p.X - start.X);
                double dy = origin.Y + (p.Y - start.Y);
                if (p.X > 0 && p.Y > 0 && p.X < _DisplayCanvas.ActualWidth && p.Y < _DisplayCanvas.ActualHeight)
                {
                    m.OffsetX = dx;
                    m.OffsetY = dy;
                    _DisplayImage.RenderTransform = new MatrixTransform(m);
                    if (_Vm != null)
                    {
                        _Vm.ZoomRate = _ZoomRate;
                        _Vm.OffsetX = dx;
                        _Vm.OffsetY = dy;
                    }
                }
            }
        }

        private void _DisplayImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext == null) { return; }

            FileViewModel vm = DataContext as FileViewModel;
            if (vm == null) { return; }

            if (_DisplayImage.Source == null)
            {
                _ImageZoomRate = 1;
                return;
            }
            if (_DisplayImage.ActualWidth < _DisplayImage.Width)
            {
                _ImageZoomRate = vm.SourceImage.PixelHeight / _DisplayImage.ActualHeight;
            }
            else if (_DisplayImage.ActualHeight < _DisplayImage.Height)
            {
                _ImageZoomRate = vm.SourceImage.PixelWidth / _DisplayImage.ActualWidth;
            }
        }

        private void _DisplayImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _DisplayImage.ReleaseMouseCapture();
        }

        private void _DisplayImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_DisplayImage.IsMouseCaptured) { return; }
            _DisplayImage.CaptureMouse();
            start = e.GetPosition(_DisplayCanvas);
            origin.X = _DisplayImage.RenderTransform.Value.OffsetX;
            origin.Y = _DisplayImage.RenderTransform.Value.OffsetY;
        }

        private void _DisplayImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_DisplayImage.Source == null) { return; }
            Point p = e.MouseDevice.GetPosition(_DisplayImage);
            Matrix m = _DisplayImage.RenderTransform.Value;
            if (e.Delta > 0)
            {
                _ZoomRate *= _ZoomRateStep;
                m.ScaleAtPrepend(_ZoomRateStep, _ZoomRateStep, p.X, p.Y);
                _DisplayImage.RenderTransform = new MatrixTransform(m);
            }
            else
            {
                if (_ZoomRate / _ZoomRateStep < 1.0)
                {
                    _ZoomRate = 1.0;
                }
                else
                {
                    _ZoomRate /= _ZoomRateStep;
                }
                if (_ZoomRate == 1)
                {
                    RecoverTransform();
                }
                else
                {
                    m.ScaleAtPrepend(1 / _ZoomRateStep, 1 / _ZoomRateStep, p.X, p.Y);
                    _DisplayImage.RenderTransform = new MatrixTransform(m);
                }
            }
            if (_Vm != null)
            {
                _Vm.OffsetX = _DisplayImage.RenderTransform.Value.OffsetX;
                _Vm.OffsetY = _DisplayImage.RenderTransform.Value.OffsetY;
                _Vm.ZoomRate = _ZoomRate;
                //_Vm.OffsetX = p.X;
                //_Vm.OffsetY = p.Y;
            }
        }
        public void RecoverTransform()
        {
            _DisplayImage.RenderTransform = new MatrixTransform(_ZoomRate, 0, 0, _ZoomRate, -_ShiftX, -_ShiftY);
            _ShiftX = 0;
            _ShiftY = 0;
            _DisplayImage.RenderTransform = new MatrixTransform(1, 0, 0, 1, -_ShiftX, -_ShiftY);
            if (_Vm != null)
            {
                _Vm.ZoomRate = 1;
                _Vm.OffsetX = 0;
                _Vm.OffsetY = 0;
            }
        }

    }
}
