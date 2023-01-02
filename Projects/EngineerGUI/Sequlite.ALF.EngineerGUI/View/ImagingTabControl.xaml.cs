using Sequlite.Adorners;
using Sequlite.ALF.EngineerGUI.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// ImagingTabControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImagingTabControl : UserControl
    {
        #region Private Fields
        private Point origin;
        private Point start;
        private double _ZoomRate = 1;
        private const double _ZoomRateStep = 1.1;
        private double _ShiftX;
        private double _ShiftY;

        private bool _IsRegionAdornerVisible = false;
        private AdornerLayer _AdornerLayer;
        private double _ImageZoomRate = 1;
        #endregion Private Fields
        CameraViewModel _CameraVM;
        CameraViewModel CameraVM 
        { get
            {
                if (_CameraVM == null)
                {
                    object d = this.DataContext;
                    if (d is Workspace)
                    {
                        _CameraVM = ((Workspace)d).CameraVM;
                    }
                }
                return _CameraVM; 
            } 
        }
        public ImagingTabControl()
        {
            InitializeComponent();
        }

        private void _LiveImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_LiveImage.IsMouseCaptured)
            {
                Point p = e.MouseDevice.GetPosition(_LivingCanvas);
                Matrix m = _LiveImage.RenderTransform.Value;
                double dx = origin.X + (p.X - start.X);
                double dy = origin.Y + (p.Y - start.Y);
                if (p.X > 0 && p.Y > 0 && p.X < _LivingCanvas.ActualWidth && p.Y < _LivingCanvas.ActualHeight)
                {
                    m.OffsetX = dx;
                    m.OffsetY = dy;
                    _LiveImage.RenderTransform = new MatrixTransform(m);
                }
            }
        }

        private void _LiveImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _LiveImage.ReleaseMouseCapture();
        }

        private void _LiveImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_LiveImage.IsMouseCaptured) { return; }
            _LiveImage.CaptureMouse();
            start = e.GetPosition(_LivingCanvas);
            origin.X = _LiveImage.RenderTransform.Value.OffsetX;
            origin.Y = _LiveImage.RenderTransform.Value.OffsetY;
        }

        private void _LiveImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_LiveImage.Source == null) { return; }
            Point p = e.MouseDevice.GetPosition(_LiveImage);
            Matrix m = _LiveImage.RenderTransform.Value;
            if (e.Delta > 0)
            {
                _ZoomRate *= _ZoomRateStep;
                m.ScaleAtPrepend(_ZoomRateStep, _ZoomRateStep, p.X, p.Y);
                _LiveImage.RenderTransform = new MatrixTransform(m);
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
                    _LiveImage.RenderTransform = new MatrixTransform(m);
                }
            }
        }

        private void RecoverTransform()
        {
            _LiveImage.RenderTransform = new MatrixTransform(_ZoomRate, 0, 0, _ZoomRate, -_ShiftX, -_ShiftY);
            _ShiftX = 0;
            _ShiftY = 0;
            _LiveImage.RenderTransform = new MatrixTransform(1, 0, 0, 1, -_ShiftX, -_ShiftY);
        }

        private void _Thumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (CameraVM != null)
            {
                CameraVM.SelectedRegion = GetSelectedRegion();
            }
        }

        private void _Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double top = Canvas.GetTop(_Thumb) + e.VerticalChange;
            double left = Canvas.GetLeft(_Thumb) + e.HorizontalChange;

            if (left < 0) { left = 0; }
            if (top < 0) { top = 0; }
            if (left > _LivingCanvas.ActualWidth - _Thumb.Width)
            {
                left = _LivingCanvas.ActualWidth - _Thumb.Width;
            }
            if (top > _LivingCanvas.ActualHeight - _Thumb.Height)
            {
                top = _LivingCanvas.ActualHeight - _Thumb.Height;
            }
            Canvas.SetTop(_Thumb, top);
            Canvas.SetLeft(_Thumb, left);
        }

        private void _Thumb_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            
            if (CameraVM != null)
            {
               CameraVM.SelectedRegion = GetSelectedRegion();
            }
        }

        private Rect GetSelectedRegion()
        {
            if (!_IsRegionAdornerVisible) { return new Rect(); }

            Rect CropRect = new Rect();
            Point ptThumbLT = new Point();
            ptThumbLT.X = Canvas.GetLeft(_Thumb);
            ptThumbLT.Y = Canvas.GetTop(_Thumb);
            Point ptThumbRB = new Point();
            ptThumbRB.X = ptThumbLT.X + _Thumb.Width;
            ptThumbRB.Y = ptThumbLT.Y + _Thumb.Height;

            Point ptThumbInImageLT = new Point();
            ptThumbInImageLT = _LivingCanvas.TranslatePoint(ptThumbLT, _LiveImage);
            Point ptThumbInImageRB = new Point();
            ptThumbInImageRB = _LivingCanvas.TranslatePoint(ptThumbRB, _LiveImage);

            if (ptThumbInImageLT.X < 0) { CropRect.X = 0; } else { CropRect.X = ptThumbInImageLT.X; }
            if (ptThumbInImageLT.Y < 0) { CropRect.Y = 0; } else { CropRect.Y = ptThumbInImageLT.Y; }
            if (ptThumbInImageRB.X > _LiveImage.ActualWidth) { CropRect.Width = _LiveImage.ActualWidth - CropRect.X; }
            else { CropRect.Width = ptThumbInImageRB.X - CropRect.X; }
            if (ptThumbInImageRB.Y > _LiveImage.ActualHeight) { CropRect.Height = _LiveImage.ActualHeight - CropRect.Y; }
            else { CropRect.Height = ptThumbInImageRB.Y - CropRect.Y; }

            CropRect.X *= _ImageZoomRate;
            CropRect.Y *= _ImageZoomRate;
            CropRect.Width *= _ImageZoomRate;
            CropRect.Height *= _ImageZoomRate;

            return CropRect;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (CameraVM != null)
            {
                CameraVM.RegionAdornerChanged += CameraVM_RegionAdornerChanged;
            }
        }

        private void CameraVM_RegionAdornerChanged(bool bIsVisible)
        {
            if (bIsVisible)
            {
                RegionAdornerInit();
            }
            else
            {
                RegionAdornerFini();
            }
        }

        private void RegionAdornerFini()
        {
            if (!_IsRegionAdornerVisible) { return; }

            Adorner[] toRemoveArray = _AdornerLayer.GetAdorners(_Thumb);
            Adorner toRemove;
            if (toRemoveArray != null)
            {
                toRemove = toRemoveArray[0];
                _AdornerLayer.Remove(toRemove);
            }
            _Thumb.Visibility = Visibility.Hidden;
            _IsRegionAdornerVisible = false;
        }

        private void RegionAdornerInit()
        {
            if (_IsRegionAdornerVisible) { return; }

            Canvas.SetLeft(_Thumb, 0);
            Canvas.SetTop(_Thumb, 0);
            _Thumb.Width = _LivingCanvas.ActualWidth;
            _Thumb.Height = _LivingCanvas.ActualHeight;
            _AdornerLayer = AdornerLayer.GetAdornerLayer(_Thumb);
            _AdornerLayer.Add(new MyCanvasAdorner(_Thumb, _LivingCanvas.ActualWidth, _LivingCanvas.ActualHeight));
            _Thumb.Visibility = Visibility.Visible;
            _IsRegionAdornerVisible = true;
        }

        private void _LiveImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CameraVM != null)
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
                }
            }
        }
    }
}
