using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// A Value Converter to divide values in two.
    /// Used to scale the increment and decrement buttons of the DoubleSpinBox control.
    /// </summary>
    public class HalfConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double halfSize = System.Convert.ToDouble(value) / 2.0;
            return (double)Math.Max(0.1, halfSize);
            //return halfSize.ToString("G0", CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;

    }
    /// <summary>
    /// A Value Converter to change font size based on control size.
    /// Used to shink the font of the increment and decrement buttons of the
    /// DoubleSpinBox control when the control is small.
    /// Bind the FontSize property to (height, h), then the Font Size is scaled down when height <= the cutoff (48 px)
    /// </summary>
    public class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const double defaultSize = 11.0;
            const double cutoff = 48.0; // the point to begin scaling
            double curSize = System.Convert.ToDouble(value); // the current size
            double scaleFactor = (curSize > cutoff) ? 1 : curSize / cutoff;
            double size = defaultSize * scaleFactor;
            return (double)Math.Max(1.0, size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;

    }

    /// <summary>
    /// Interaction logic for DoubleSpinBox.xaml
    /// This control allows mouse or keyboard entry of a double-precision numeric value value.
    /// Supported entry types:
    ///     * Key (up/down) with auto-repeat
    ///     * Mouse wheel
    ///     * Up/down buttons for mouse click
    /// </summary>
    public partial class DoubleSpinBox : UserControl
    {
        /// <summary>
        /// Properties to hold the values used by the control
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value",
            typeof(double), typeof(DoubleSpinBox),
            new PropertyMetadata(10.0));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue",
                typeof(double), typeof(DoubleSpinBox),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register("MinValue",
                typeof(double), typeof(DoubleSpinBox),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty IncrementProperty =
            DependencyProperty.Register("Increment",
                typeof(double), typeof(DoubleSpinBox),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty LargeIncrementProperty =
            DependencyProperty.Register("LargeIncrement",
            typeof(double), typeof(DoubleSpinBox),
            new PropertyMetadata(5.0));

        public static readonly DependencyProperty ButtonsVisibleProperty =
            DependencyProperty.Register("ButtonsVisible",
            typeof(bool), typeof(DoubleSpinBox),
            new PropertyMetadata(true, visibilityChanged));

        public static readonly DependencyProperty HighlightVisibleProperty =
            DependencyProperty.Register("HighlightVisible",
            typeof(bool), typeof(DoubleSpinBox),
            new PropertyMetadata(true));

        public static readonly DependencyProperty PrecisionProperty =
            DependencyProperty.Register("Precision",
            typeof(int), typeof(DoubleSpinBox),
            new PropertyMetadata(3));

        /// <summary>
        /// Shows or hides the buttons on the control when the ButtonsVisibleProperty changes
        /// </summary>
        /// <param name="o"></param>
        /// <param name="e"></param>
        static void visibilityChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spinBox = o as DoubleSpinBox;
            if (spinBox != null)
            {
                if (spinBox.ButtonsVisible)
                {
                    spinBox.buttonIncrement.Visibility = Visibility.Visible;
                    spinBox.buttonDecrement.Visibility = Visibility.Visible;
                }
                else
                {
                    spinBox.buttonIncrement.Visibility = Visibility.Collapsed;
                    spinBox.buttonDecrement.Visibility = Visibility.Collapsed;
                }
            }
        }

        // TODO: add support for horizontal layout and layout switching with property
        /*public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register("Layout",
            typeof(Orientation), typeof(DoubleSpinBox),
            new PropertyMetadata(Orientation.Horizontal));*/
        private double _previousValue = 0; //< used to store the last value for the control in case new input is invalid
        private DispatcherTimer _timer = new DispatcherTimer(); //< used to fire multipe increment or decrement operations when the mouse button is held down
        private bool _isIncrementing = false; //< used to switch timer events between increment and decrement operations
        private static int _delayRate = System.Windows.SystemParameters.KeyboardDelay; //< sets the initial time period before the timer simulates repeated button presses. Units = [ms]
        private static int _repeatSpeed = Math.Max(1, System.Windows.SystemParameters.KeyboardSpeed); //< sets the time period in between simulated repeated button presses. Units = [ms]
        //private static readonly Regex _regex = new Regex("[^0-9.-]"); //regex that matches allowed text
        private static readonly Regex _regex = new Regex(@"/^-?(0|[1-9]\d*)?(\.\d+)?(?<=\d)$/"); //regex that matches allowed text
                                                                                                 //< int counter to track when the value is changed programmatically for highlighting effect. If equal to 1, the value was changed by the program.
                                                                                                 //< this value is incremented again (value = 2) to ignore programmatic changes that come form this class.
        private int _isTextProgrammaticallySet = 0;


        /*public Orientation Layout
        {
            get => (Orientation)GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }*/

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set
            {
                _isTextProgrammaticallySet++;
                SetValue(ValueProperty, Math.Round(value, Precision));
                _isTextProgrammaticallySet--;
            }
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double Increment
        {
            get => (double)GetValue(IncrementProperty);
            set => SetValue(IncrementProperty, value);
        }
        public double LargeIncrement
        {
            get => (double)GetValue(LargeIncrementProperty);
            set => SetValue(LargeIncrementProperty, value);
        }
        public bool ButtonsVisible
        {
            get => (bool)GetValue(ButtonsVisibleProperty);
            set => SetValue(ButtonsVisibleProperty, value);
        }

        public bool HighlightVisible
        {
            get => (bool)GetValue(HighlightVisibleProperty);
            set => SetValue(HighlightVisibleProperty, value);
        }

        public int Precision
        {
            get => (int)GetValue(PrecisionProperty);
            set => SetValue(PrecisionProperty, value);
        }
        public DoubleSpinBox()
        {
            InitializeComponent();
            if (ButtonsVisible)
            {
                buttonIncrement.Visibility = Visibility.Visible;
                buttonDecrement.Visibility = Visibility.Visible;
            }
            else
            {
                buttonIncrement.Visibility = Visibility.Collapsed;
                buttonDecrement.Visibility = Visibility.Collapsed;
            }
            textBox.PreviewTextInput += new TextCompositionEventHandler(textbox_PreviewTextInput);
            textBox.PreviewKeyDown += new KeyEventHandler(textbox_PreviewKeyDown);
            textBox.GotFocus += new RoutedEventHandler(textbox_GotFocus);
            textBox.LostFocus += new RoutedEventHandler(textbox_LostFocus);
            textBox.MouseWheel += new MouseWheelEventHandler(textbox_MouseWheel);

            buttonIncrement.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(buttonIncrement_PreviewMouseLeftButtonDown);
            buttonIncrement.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(buttonIncrement_PreviewMouseLeftButtonUp);

            buttonDecrement.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(buttonDecrement_PreviewMouseLeftButtonDown);
            buttonDecrement.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(buttonDecrement_PreviewMouseLeftButtonUp);

            _timer.Tick += new EventHandler(_timer_Tick);

            textBox.Text = Value.ToString();
        }
        void textbox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                IncrementValue(Increment);
            }
            else
            {
                DecrementValue(Increment);
            }
        }
        void buttonIncrement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            buttonIncrement.CaptureMouse();
            _timer.Interval = TimeSpan.FromMilliseconds(_delayRate * 250);
            _timer.Start();

            _isIncrementing = true;
        }

        void buttonIncrement_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();
            buttonIncrement.ReleaseMouseCapture();
            IncrementValue(Increment);
        }

        void buttonDecrement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            buttonDecrement.CaptureMouse();
            _timer.Interval = TimeSpan.FromMilliseconds(_delayRate * 250);
            _timer.Start();

            _isIncrementing = false;
        }

        void buttonDecrement_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();
            buttonDecrement.ReleaseMouseCapture();
            DecrementValue(Increment);
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (_isIncrementing)
            {
                IncrementValue(Increment);
            }
            else
            {
                DecrementValue(Increment);
            }
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / _repeatSpeed);
        }

        private void textbox_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousValue = Value;
        }

        private void textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            textbox_LimitValue(true);
        }

        private void textbox_LimitValue(in bool hasLostFocus = false)
        {
            // do not limit strings ending with a decimal to prevent removing the decimal
            if (textBox.Text.EndsWith(".") && hasLostFocus)
            {
                return;
            }
            if (double.TryParse(textBox.Text, out double newValue))
            {
                if (newValue > MaxValue)
                {
                    newValue = MaxValue;
                }
                else if (newValue < MinValue)
                {
                    newValue = MinValue;
                }
            }
            else
            {
                newValue = _previousValue;
            }
            _isTextProgrammaticallySet++;
            Value = newValue;
            _isTextProgrammaticallySet--;
            textBox.Text = newValue.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            e.Handled = _regex.IsMatch(textBox.Text);
        }

        /// <summary>
        /// Note: When the enter key is pressed, the source is updated. No need to set UpdateSourceTrigger to PropertyChanged to make sure the most recent value has been stored.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    IncrementValue(Increment);
                    break;
                case Key.Down:
                    DecrementValue(Increment);
                    break;
                case Key.PageUp:
                    IncrementValue(LargeIncrement);
                    break;
                case Key.PageDown:
                    DecrementValue(LargeIncrement);
                    break;
                case Key.Return:
                    textbox_LimitValue();
                    TextBox t = (TextBox)sender;
                    DependencyProperty p = TextBox.TextProperty;
                    BindingExpression b = BindingOperations.GetBindingExpression(t, p);
                    if (b != null)
                    {
                        b.UpdateSource();
                    }
                    break;
                default:
                    //do nothing
                    break;
            }
        }

        private void IncrementValue(double d)
        {
            _isTextProgrammaticallySet++;
            Value = Math.Min(Value + d, MaxValue);
            _isTextProgrammaticallySet--;
        }

        private void DecrementValue(double d)
        {
            _isTextProgrammaticallySet++;
            Value = Math.Max(Value - d, MinValue);
            _isTextProgrammaticallySet--;
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isTextProgrammaticallySet == 1 && HighlightVisible)
            {
                Storyboard s = FindResource("HighlightAnimation") as Storyboard;
                Storyboard.SetTarget(s, textBox);
                s.Stop();
                s.Begin();
            }
        }
    }
}
