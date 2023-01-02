using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Sequlite.Adorners
{
    public class MyCanvasAdorner : Adorner
    {
        //const double THUMB_SIZE = 60;
        //const double MINIMAL_SIZE = 120;;
        const double THUMB_SIZE = 16;
        const double MINIMAL_SIZE = 30;
        const double MOVE_OFFSET = 20;
        //const double CORNER_THICKNESS = 10;
        const double CORNER_THICKNESS = 6;
        double dWidth = 0;
        double dHeight = 0;
        Thumb tl, tr, bl, br;
        //Thumb mov;
        VisualCollection visCollec;

        public MyCanvasAdorner(UIElement adorned, double width, double height, bool isFlipped = false)
            : base(adorned)
        {
            dWidth = width;
            dHeight = height;
            visCollec = new VisualCollection(this);
            if (isFlipped)
            {
                visCollec.Add(tl = GetResizeThumbLT(Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Top));
                visCollec.Add(tr = GetResizeThumbRT(Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Top));
                visCollec.Add(bl = GetResizeThumbLB(Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Bottom));
                visCollec.Add(br = GetResizeThumbRB(Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Bottom));
            }
            else
            {
                visCollec.Add(tl = GetResizeThumbLT(Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top));
                visCollec.Add(tr = GetResizeThumbRT(Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top));
                visCollec.Add(bl = GetResizeThumbLB(Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom));
                visCollec.Add(br = GetResizeThumbRB(Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom));
            }

            //visCollec.Add(mov = GetMoveThumb());
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double offset = THUMB_SIZE;// 2;
            Size sz = new Size(THUMB_SIZE, THUMB_SIZE);
            tl.Arrange(new Rect(new Point(CORNER_THICKNESS / 4, CORNER_THICKNESS / 4), sz));
            tr.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width - offset, 0), sz));
            bl.Arrange(new Rect(new Point(0, AdornedElement.RenderSize.Height - offset), sz));
            br.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width - offset, AdornedElement.RenderSize.Height - offset), sz));
            //mov.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width / 2 - THUMB_SIZE / 2, -MOVE_OFFSET), sz));

            return finalSize;
        }

        void Resize(FrameworkElement ff)
        {
            if (Double.IsNaN(ff.Width))
                ff.Width = ff.RenderSize.Width;
            if (Double.IsNaN(ff.Height))
                ff.Height = ff.RenderSize.Height;
        }

        Thumb GetMoveThumb()
        {
            var thumb = new Thumb()
            {
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                Cursor = Cursors.SizeAll,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactory(GetMoveEllipseBack())
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
            };
            return thumb;
        }

        Thumb GetResizeThumb(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactory(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }

        Thumb GetResizeThumbLT(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryLT(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);
                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbRT(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryRT(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange > 0)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbRB(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryRB(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange > 0)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbLB(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryLB(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }

        Brush GetMoveEllipseBack()
        {
            string lan = "M 0,5 h 10 M 5,0 v 10";
            var converter = TypeDescriptor.GetConverter(typeof(Geometry));
            var geometry = (Geometry)converter.ConvertFrom(lan);
            TileBrush bsh = new DrawingBrush(new GeometryDrawing(Brushes.Transparent, new Pen(Brushes.Black, 2), geometry));
            bsh.Stretch = Stretch.Fill;
            return bsh;
        }

        FrameworkElementFactory GetFactory(Brush back)
        {
            back.Opacity = 0.6;
            var fef = new FrameworkElementFactory(typeof(Rectangle));
            fef.SetValue(Rectangle.FillProperty, back);
            fef.SetValue(Rectangle.StrokeProperty, Brushes.Green);
            fef.SetValue(Rectangle.StrokeThicknessProperty, (double)1);
            return fef;
        }

        FrameworkElementFactory GetFactoryLT(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point5 = new System.Windows.Point(0, 0);
            System.Windows.Point Point6 = new System.Windows.Point(0, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)CORNER_THICKNESS / 2);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }

        FrameworkElementFactory GetFactoryRT(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(0, 0);
            System.Windows.Point Point5 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point6 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)CORNER_THICKNESS);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }

        FrameworkElementFactory GetFactoryLB(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(0, 0);
            System.Windows.Point Point5 = new System.Windows.Point(0, THUMB_SIZE);
            System.Windows.Point Point6 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)CORNER_THICKNESS);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }

        FrameworkElementFactory GetFactoryRB(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point5 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            System.Windows.Point Point6 = new System.Windows.Point(0, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)CORNER_THICKNESS);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }

        protected override Visual GetVisualChild(int index)
        {
            return visCollec[index];
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return visCollec.Count;
            }
        }

    }

    public class MyShapeAdorner : Adorner
    {
        const double THUMB_SIZE = 40;
        const double MINIMAL_SIZE = 40;
        const double MOVE_OFFSET = 40;
        double dWidth = 0;
        double dHeight = 0;
        Thumb tl, tr, bl, br;
        Thumb mov;
        VisualCollection visCollec;
        Image bgImage;

        public MyShapeAdorner(UIElement adorned, double width, double height, Image lImg)
            : base(adorned)
        {
            bgImage = lImg;
            dWidth = width;
            dHeight = height;
            visCollec = new VisualCollection(this);
            visCollec.Add(tl = GetResizeThumbLT(Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top));
            visCollec.Add(tr = GetResizeThumbRT(Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top));
            visCollec.Add(bl = GetResizeThumbLB(Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom));
            visCollec.Add(br = GetResizeThumbRB(Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom));
            visCollec.Add(mov = GetMoveThumb());
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double offset = THUMB_SIZE;// 2;
            Size sz = new Size(THUMB_SIZE, THUMB_SIZE);
            tl.Arrange(new Rect(new Point(0, 0), sz));
            tr.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width - offset, 0), sz));
            bl.Arrange(new Rect(new Point(0, AdornedElement.RenderSize.Height - offset), sz));
            br.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width - offset, AdornedElement.RenderSize.Height - offset), sz));
            mov.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width / 2 - THUMB_SIZE / 2, -MOVE_OFFSET), sz));

            return finalSize;
        }

        void Resize(FrameworkElement ff)
        {
            if (Double.IsNaN(ff.Width))
                ff.Width = ff.RenderSize.Width;
            if (Double.IsNaN(ff.Height))
                ff.Height = ff.RenderSize.Height;
        }

        Thumb GetMoveThumb()
        {
            var thumb = new Thumb()
            {
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                Cursor = Cursors.SizeAll,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryMov(GetMoveEllipseBack())
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);

                MatrixTransform lFx = new MatrixTransform(((MatrixTransform)bgImage.RenderTransform).Matrix);
                bgImage.RenderTransform = lFx;
            };

            return thumb;
        }

        Thumb GetResizeThumb(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactory(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }

        Thumb GetResizeThumbLT(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryLT(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbRT(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryRT(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbRB(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryRB(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }
        Thumb GetResizeThumbLB(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryLB(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }

        Brush GetMoveEllipseBack()
        {
            string lan = "M 0,5 h 10 M 5,0 v 10";
            var converter = TypeDescriptor.GetConverter(typeof(Geometry));
            var geometry = (Geometry)converter.ConvertFrom(lan);
            TileBrush bsh = new DrawingBrush(new GeometryDrawing(Brushes.Transparent, new Pen(Brushes.Black, 2), geometry));
            bsh.Stretch = Stretch.Fill;
            return bsh;
        }

        FrameworkElementFactory GetFactory(Brush back)
        {
            back.Opacity = 0.6;
            var fef = new FrameworkElementFactory(typeof(Rectangle));
            fef.SetValue(Rectangle.FillProperty, back);
            fef.SetValue(Rectangle.StrokeProperty, Brushes.Green);
            fef.SetValue(Rectangle.StrokeThicknessProperty, (double)1);
            return fef;
        }

        FrameworkElementFactory GetFactoryLT(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point5 = new System.Windows.Point(0, 0);
            System.Windows.Point Point6 = new System.Windows.Point(0, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)2);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }
        FrameworkElementFactory GetFactoryRT(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(0, 0);
            System.Windows.Point Point5 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point6 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)2);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }
        FrameworkElementFactory GetFactoryLB(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(0, 0);
            System.Windows.Point Point5 = new System.Windows.Point(0, THUMB_SIZE);
            System.Windows.Point Point6 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)2);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }
        FrameworkElementFactory GetFactoryRB(Brush back)
        {
            Brush bs = new SolidColorBrush(System.Windows.Media.Brushes.DarkOrange.Color);
            bs.Opacity = 0;
            System.Windows.Point Point4 = new System.Windows.Point(THUMB_SIZE, 0);
            System.Windows.Point Point5 = new System.Windows.Point(THUMB_SIZE, THUMB_SIZE);
            System.Windows.Point Point6 = new System.Windows.Point(0, THUMB_SIZE);
            PointCollection myPointCollection2 = new PointCollection();
            myPointCollection2.Add(Point4);
            myPointCollection2.Add(Point5);
            myPointCollection2.Add(Point6);
            var fef = new FrameworkElementFactory(typeof(Polyline));
            fef.SetValue(Polyline.FillProperty, bs);
            fef.SetValue(Polyline.StrokeProperty, Brushes.Green);
            fef.SetValue(Polyline.StrokeThicknessProperty, (double)2);
            fef.SetValue(Polyline.PointsProperty, myPointCollection2);
            return fef;
        }

        FrameworkElementFactory GetFactoryMov(Brush back)
        {
            back.Opacity = 0.6;
            var fef = new FrameworkElementFactory(typeof(Ellipse));
            fef.SetValue(Ellipse.FillProperty, back);
            fef.SetValue(Ellipse.StrokeProperty, Brushes.Green);
            fef.SetValue(Ellipse.StrokeThicknessProperty, (double)1);
            return fef;
        }

        protected override Visual GetVisualChild(int index)
        {
            return visCollec[index];
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return visCollec.Count;
            }
        }

    }

    class MyLineAdorner : Adorner
    {
        const double THUMB_SIZE = 20;
        const double MINIMAL_SIZE = 20;
        const double MOVE_OFFSET = 20;
        double dWidth = 0;
        double dHeight = 0;
        Thumb l, r;
        Thumb mov;
        VisualCollection visCollec;

        public MyLineAdorner(UIElement adorned, double width, double height)
            : base(adorned)
        {
            dWidth = width;
            dHeight = height;
            visCollec = new VisualCollection(this);
            visCollec.Add(l = GetResizeThumb(Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top));
            visCollec.Add(r = GetResizeThumb(Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top));
            visCollec.Add(mov = GetMoveThumb());
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double offset = THUMB_SIZE;// 2;
            Size sz = new Size(THUMB_SIZE, THUMB_SIZE);
            l.Arrange(new Rect(new Point(0, 0), sz));
            r.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width - offset, 0), sz));
            mov.Arrange(new Rect(new Point(AdornedElement.RenderSize.Width / 2 - THUMB_SIZE / 2, -MOVE_OFFSET), sz));

            return finalSize;
        }

        void Resize(FrameworkElement ff)
        {
            if (Double.IsNaN(ff.Width))
                ff.Width = ff.RenderSize.Width;
            if (Double.IsNaN(ff.Height))
                ff.Height = ff.RenderSize.Height;
        }

        Thumb GetMoveThumb()
        {
            var thumb = new Thumb()
            {
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                Cursor = Cursors.SizeAll,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactoryMov(GetMoveEllipseBack())
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
            };
            return thumb;
        }

        Thumb GetResizeThumb(Cursor cur, HorizontalAlignment hor, VerticalAlignment ver)
        {
            var thumb = new Thumb()
            {
                Background = Brushes.Red,
                Width = THUMB_SIZE,
                Height = THUMB_SIZE,
                HorizontalAlignment = hor,
                VerticalAlignment = ver,
                Cursor = cur,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = GetFactory(new SolidColorBrush(Colors.Green))
                }
            };
            thumb.DragDelta += (s, e) =>
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null)
                    return;

                Resize(element);

                switch (thumb.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        if (element.Height + e.VerticalChange > MINIMAL_SIZE && element.Height + e.VerticalChange < dHeight)
                        {
                            element.Height += e.VerticalChange;
                        }
                        break;
                    case VerticalAlignment.Top:
                        if (element.Height - e.VerticalChange > MINIMAL_SIZE && element.Height - e.VerticalChange < dHeight)
                        {
                            element.Height -= e.VerticalChange;
                            Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                        }
                        break;
                }
                switch (thumb.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        if (element.Width - e.HorizontalChange > MINIMAL_SIZE && element.Width - e.HorizontalChange < dWidth)
                        {
                            element.Width -= e.HorizontalChange;
                            Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                        }
                        break;
                    case HorizontalAlignment.Right:
                        if (element.Width + e.HorizontalChange > MINIMAL_SIZE && element.Width + e.HorizontalChange < dWidth)
                        {
                            element.Width += e.HorizontalChange;
                        }
                        break;
                }

                e.Handled = true;
            };
            return thumb;
        }

        Brush GetMoveEllipseBack()
        {
            string lan = "M 0,5 h 10 M 5,0 v 10";
            var converter = TypeDescriptor.GetConverter(typeof(Geometry));
            var geometry = (Geometry)converter.ConvertFrom(lan);
            TileBrush bsh = new DrawingBrush(new GeometryDrawing(Brushes.Transparent, new Pen(Brushes.Black, 2), geometry));
            bsh.Stretch = Stretch.Fill;
            return bsh;
        }

        FrameworkElementFactory GetFactory(Brush back)
        {
            back.Opacity = 0.6;
            var fef = new FrameworkElementFactory(typeof(Rectangle));
            fef.SetValue(Rectangle.FillProperty, back);
            fef.SetValue(Rectangle.StrokeProperty, Brushes.Green);
            fef.SetValue(Rectangle.StrokeThicknessProperty, (double)1);
            return fef;
        }
        FrameworkElementFactory GetFactoryMov(Brush back)
        {
            back.Opacity = 0.6;
            var fef = new FrameworkElementFactory(typeof(Ellipse));
            fef.SetValue(Ellipse.FillProperty, back);
            fef.SetValue(Ellipse.StrokeProperty, Brushes.Green);
            fef.SetValue(Ellipse.StrokeThicknessProperty, (double)1);
            return fef;
        }

        protected override Visual GetVisualChild(int index)
        {
            return visCollec[index];
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return visCollec.Count;
            }
        }

    }

}
