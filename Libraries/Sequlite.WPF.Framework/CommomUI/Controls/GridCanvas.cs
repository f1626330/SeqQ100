using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sequlite.WPF.Framework
{
    public class GridCanvas : Canvas
    {
        public static readonly DependencyProperty ImageContentProperty =
            DependencyProperty.Register("GridCanvas", typeof(string), typeof(GridCanvas));
        public static readonly DependencyProperty MaxRowsProperty =
           DependencyProperty.RegisterAttached("MaxRows", typeof(int), typeof(GridCanvas), new UIPropertyMetadata(1));
        public static readonly DependencyProperty MaxColumnsProperty =
           DependencyProperty.RegisterAttached("MaxColumns", typeof(int), typeof(GridCanvas), new UIPropertyMetadata(1));
        public static int GetMaxRows(DependencyObject obj)
        {
            return (int)obj.GetValue(MaxRowsProperty);
        }
        public static void SetMaxRows(DependencyObject obj, int value)
        {
            obj.SetValue(MaxRowsProperty, value);
        }

        public static int GetMaxColumns(DependencyObject obj)
        {
            return (int)obj.GetValue(MaxColumnsProperty);
        }
        public static void SetMaxColumns(DependencyObject obj, int value)
        {
            obj.SetValue(MaxColumnsProperty, value);
        }
        private static readonly Color GraphColor = Colors.Black;// Color.FromArgb(200, 255, 187, 187);
        //private static readonly Color GraphSecondColor = Color.FromArgb(150, 255, 229, 229);
        private readonly SolidColorBrush _color1 = new SolidColorBrush(GraphColor);
        //private readonly SolidColorBrush _color2 = new SolidColorBrush(GraphSecondColor);
        int _Rows;
        int _Columns;
        double _XOff;
        double _YOff;
        public GridCanvas()
        {
            //InitializeComponent();
            this.SizeChanged += ShapeCanvas_SizeChanged;
        }

        private void DrawGraph(Canvas mainCanvas)
        {
            RemoveGraph(mainCanvas);
            Image lines = new Image();
            //lines.SetValue(Panel.ZIndexProperty, 1);// -100);
            //Draw the grid        
            DrawingVisual gridLinesVisual = new DrawingVisual();
            DrawingContext dct = gridLinesVisual.RenderOpen();
            Pen lightPen = new Pen(_color1, 0.7);//, darkPen = new Pen(_color2, 1);
            lightPen.Freeze();
            //darkPen.Freeze();
            double startX = 0;// 0.5;
            double startY = 0;// 0.5;

            double yOffset = _YOff,
                xOffset = _XOff;
            int rows = _Rows,
                 columns = _Columns,
                 alternate = 1,// yOffset == 5 ? yOffset : 1,

                j = 0;

            //Draw the horizontal lines        
            Point x = new Point(0, startY);
            Point y = new Point(_Columns, startY);


            for (int i = 0; i <= rows; i++, j++)
            {
                //dct.DrawLine(j % alternate == 0 ? lightPen : darkPen, x, y);
                if (j % alternate == 0)
                {
                    dct.DrawLine(lightPen, x, y);
                }
                x.Offset(0, yOffset);
                y.Offset(0, yOffset);
            }
            j = 0;
            //Draw the vertical lines        
            x = new Point(startX, 0);
            y = new Point(startX, _Rows);

            for (int i = 0; i <= columns; i++, j++)
            {
                // dct.DrawLine(j % alternate == 0 ? lightPen : darkPen, x, y);
                if (j % alternate == 0)
                {
                    dct.DrawLine(lightPen, x, y);
                }
                x.Offset(xOffset, 0);
                y.Offset(xOffset, 0);
            }

            dct.Close();

            RenderTargetBitmap bmp = new RenderTargetBitmap(_Columns,
                (int)_Rows, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(gridLinesVisual);
            bmp.Freeze();
            lines.Source = bmp;

            mainCanvas.Children.Add(lines);
        }

        private void RemoveGraph(Canvas mainCanvas)
        {
            foreach (UIElement obj in mainCanvas.Children)
            {
                if (obj is Image)
                {
                    mainCanvas.Children.Remove(obj);
                    break;
                }
            }
        }

        private void ShapeCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _Rows = (int)this.ActualHeight;// 100;// (int)(SystemParameters.PrimaryScreenHeight);
            _Columns = (int)this.ActualWidth;//  400;// (int)(SystemParameters.PrimaryScreenWidth);

            //ShowGridlines.IsChecked = true;
            if (_Rows > 0 && _Columns > 0)
            {
                _XOff = _Columns / (double)GetMaxColumns(this);// 45.0;// 5;
                _YOff = _Rows / (double)GetMaxRows(this);// 4.0;// 5;
                DrawGraph(this);
            }
        }
    }
}
