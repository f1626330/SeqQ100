using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Sequlite.LogAnalyzer
{
    /// <summary>
    /// Defines the graph options that are available
    /// </summary>
    public enum GraphType
    {
        [Description("Line")]
        LineGraph,
        [Description("Scatter")]
        ScatterGraph,
        [Description("Bar")]
        BarGraph
    }

    /// <summary>
    /// Defines the options for the x axis of the graph
    /// </summary>
    public enum AxisType
    {
        [Description("OLE Automation Date")]
        OATime,
        [Description("Relative time [s]")]
        RelativeTime,
        [Description("Count")]
        CountTime
    }


    /// <summary>
    /// Used to convert the descriptions of enumerations into text for ui elements
    /// </summary>
    public class EnumDescriptionConverter : IValueConverter
    {
        public static string GetEnumDescription(Enum enumObj)
        {
            FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());

            object[] attribArray = fieldInfo.GetCustomAttributes(false);

            if (attribArray.Length == 0)
            {
                return enumObj.ToString();
            }
            else
            {
                DescriptionAttribute attrib = attribArray[0] as DescriptionAttribute;
                return attrib.Description;
            }
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Enum myEnum = (Enum)value;
            string description = GetEnumDescription(myEnum);
            return description;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// 
    /// For information on Dynamic Data Display graphing library:
    /// https://swharden.com/csdv/plotting-free/interactive-data-display/#bar-graph
    /// https://swharden.com/csdv/plotting-free/interactive-data-display/D3-WPF-Version-2.pdf
    /// https://github.com/artemiusgreat/Interactive-Data-Display-WPF
    /// </summary>
    public partial class MainWindow : UserControl
    {
        //< a random number generator to set graph colors
        static Random _rng = new Random();

        //< store colors generated for graphs
        List<string> _palette = new List<string>();

        //< dictionary to store all datasets from all syslogs
        // outer key (string) = syslog name, outer value (dictionary)= dictionary of benchmarks
        // inner key (string) = benchmark name, inner value (tuple)= <datetime / datapoint>

        private Dictionary<string, Dictionary<string, List<Tuple<string, string>>>> _benchmarks = new Dictionary<string, Dictionary<string, List<Tuple<string, string>>>>();

        //< dictionary to store selected datasets. Key (string) is the syslog file name, value (tuple) are x,y points
        private Dictionary<string, List<Tuple<string, string>>> _selectedData = new Dictionary<string, List<Tuple<string, string>>>();
        public MainWindow()
        {
            InitializeComponent();
            //Title = "Sequlite SysLog Analyzer";
            ComboBoxChartTypeSelect.SelectedIndex = 0; // initialize index here to let xaml populate box first
            ComboBoxXAxisSelect.SelectedIndex = 0;
        }

        private void ButtonLoadLog_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.InitialDirectory = "c:\\";
            dlg.Filter = "SysLog files (*.log)|*.log";
            dlg.RestoreDirectory = true;
            if (dlg.ShowDialog() == true)
            {
                ProcessInput(dlg.FileName);
            }
        }
        private void ButtonLoadFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = "Select folder to batch process",
                RootFolder = Environment.SpecialFolder.DesktopDirectory,
                ShowNewFolderButton = false
            };

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ProcessInput(dialog.SelectedPath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputPath">A path to a syslog or a folder of syslogs</param>
        private void ProcessInput(in string inputPath)
        {
            // clear stored data
            ListLogKeys.Items.Clear();
            _benchmarks.Clear();
            _selectedData.Clear();


            // detect if a directory has been selected
            FileAttributes attr = File.GetAttributes(inputPath);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                LogFileName.Text = $"Loaded syslog directory: {inputPath}";
                foreach (string fileName in Directory.GetFiles(inputPath))
                {
                    ReadLog(fileName);
                }
            }
            else
            {
                LogFileName.Text = $"Loaded syslog file: {inputPath}";
                ReadLog(inputPath);
            }
        }

        private void ReadLog(in string logFilePath)
        {
            string fileName = Path.GetFileName(logFilePath);
            foreach (string line in File.ReadLines(logFilePath, Encoding.UTF8))
            {
                string[] lineParts = line.Split('|');
                if (lineParts.Length > 5 && lineParts[1] == "0040") // todo: replace with string from bitmask
                {
                    string date = lineParts[0]; // first part is the date time
                    string key = lineParts[4]; // left side of the benchmark message
                    string value = lineParts[5]; // right side of the benchmark message

                    // create a new entry in the outer dictionary for each syslog file
                    if (!_benchmarks.ContainsKey(fileName))
                    {
                        _benchmarks.Add(fileName, new Dictionary<string, List<Tuple<string, string>>>());
                    }

                    // populate the inner dictionary with the points from each benchmark dataset
                    Dictionary<string, List<Tuple<string, string>>> dataset = _benchmarks[fileName];
                    if (dataset.ContainsKey(key))
                    {
                        dataset[key].Add(new Tuple<string, string>(date, value));
                    }
                    else
                    {
                        dataset.Add(key, new List<Tuple<string, string>>() { new Tuple<string, string>(date, value) });
                    }
                }
            }

            // populate the listbox with the superset of all the benchmark datasets available
            foreach (Dictionary<string, List<Tuple<string, string>>> dataset in _benchmarks.Values)
                foreach (string key in dataset.Keys)
                    if (!ListLogKeys.Items.Contains(key))
                        ListLogKeys.Items.Add(key);

        }
        private void Chart_TypeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // auto fit gets turned off if the uder has interacted with the data
            plotter.IsAutoFitEnabled = true;

            UpdateChart();
        }

        private void Chart_AxisChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // auto fit gets turned off if the uder has interacted with the data
            plotter.IsAutoFitEnabled = true;

            UpdateChart();
        }

        /// <summary>
        /// Event handler that is called when the user changes the selection in the listbox of available data sets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Chart_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

            // enable x axis selections for all graph types except bar graph
            ComboBoxXAxisSelect.IsEnabled = (GraphType)ComboBoxChartTypeSelect.SelectedItem != GraphType.BarGraph;

            // auto fit gets turned off if the uder has interacted with the data
            plotter.IsAutoFitEnabled = true;

            UpdateSelectedData();
            UpdateChart();
        }

        private void ButtonExport_Click(object senser, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog()
            {
                //Filter = "CSV|*.csv",
                FileName = $"Benchmark Export-{ListLogKeys.SelectedItem}",
                Title = "Export selected data to CSV"
            };
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            string outputPath = "";
            string outputName = "";
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                outputName = dialog.FileName;
                outputPath = Path.GetDirectoryName(outputName);
            }
            if (!string.IsNullOrEmpty(outputPath))
                DumpSelectedData(outputPath, outputName);
        }

        /// <summary>
        /// 
        /// </summary>
        private void DumpSelectedData(in string outputPath, in string outputName)
        {
            // make a folder with the selected benchmark key and current date
            // example: Benchmark Export Recipe step Elapsed Time [ms] [yyyy-MM-dd HH:mm:ss.fff]
            string folderName;
            if(string.IsNullOrEmpty(outputName))
            {
                folderName = $"Benchmark Export {ListLogKeys.SelectedItem.ToString()}";
            }
            else
            {
                folderName = outputName;
            }

            Directory.CreateDirectory(folderName);

            foreach(string key in _selectedData.Keys)
            {
                var fileName = new StringBuilder("Benchmark-");
                fileName.Append(key);
                fileName.Append(ListLogKeys.SelectedItem);
                fileName.Append(".CSV");
                var csv = new StringBuilder();
                foreach(Tuple<string, string> dataPoint in _selectedData[key])
                {
                    csv.AppendLine($"{dataPoint.Item1.ToString()},{dataPoint.Item2.ToString()}");
                }
                string filePath = Path.Combine(folderName, fileName.ToString());
                File.WriteAllText(filePath, csv.ToString());
            }
        }

        private void UpdateSelectedData()
        {
            // get selected data set
            if (ListLogKeys.SelectedIndex == -1) return;
            string key = ListLogKeys.SelectedItem.ToString();
            _selectedData.Clear();

            long totalDataSets = 0;
            long totalDataPoints = 0;
            foreach (string logfile in _benchmarks.Keys)
            {

                if (_benchmarks[logfile].ContainsKey(key))
                {
                    _selectedData.Add(logfile, _benchmarks[logfile][key]);
                    totalDataSets++;
                    totalDataPoints += _benchmarks[logfile][key].Count();
                }
            }

            TextBlockDataInfo.Text = $"{totalDataSets} data sets ({totalDataPoints} total data points)";

            UpdateColorPalette(_selectedData.Count);
        }

        private void UpdateChart()
        {
            plotGrid.Children.Clear();

            if (_selectedData.Count < 1) return;


            // get selected chart type and format
            if (ComboBoxChartTypeSelect.SelectedIndex == -1) return;
            if (ComboBoxXAxisSelect.SelectedIndex == -1) return;

            // ensure a data set is selected
            if (ListLogKeys.SelectedIndex == -1) return;

            // plot
            plotter.LeftTitle = ListLogKeys.SelectedItem.ToString();
            plotter.BottomTitle = EnumDescriptionConverter.GetEnumDescription((Enum)ComboBoxXAxisSelect.SelectedItem);

            switch (ComboBoxChartTypeSelect.SelectedItem)
            {
                case GraphType.LineGraph:
                    {
                        for (int i = 0; i < _selectedData.Count; ++i)
                        {
                            string seriesName = _selectedData.Keys.ToList()[i];

                            // make a new line graph for each series in the data
                            var line = new InteractiveDataDisplay.WPF.LineGraph();
                            line.Description = seriesName;
                            line.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_palette[i]));

                            List<Tuple<string, string>> series = _selectedData[seriesName];
                            List<string> x1 = series.Select(_ => _.Item1).ToList();
                            List<double> xs = new List<double>();
                            List<string> y1 = series.Select(_ => _.Item2).ToList();
                            List<double> ys = y1.Select(y => double.Parse(y)).ToList();

                            switch (ComboBoxXAxisSelect.SelectedItem)
                            {
                                case AxisType.OATime:
                                    xs = x1.Select(x => DateTime.ParseExact(x, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture).ToOADate()).ToList();
                                    break;
                                case AxisType.RelativeTime:
                                    // compute time offset if relative time is selected
                                    long timeOffset = -1; // units = [s]
                                    foreach (Tuple<string, string> point in series)
                                    {
                                        DateTime t = DateTime.ParseExact(point.Item1, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture);
                                        timeOffset = (timeOffset < 0) ? t.Ticks / TimeSpan.TicksPerMillisecond / 1000 : Math.Min(timeOffset, t.Ticks / TimeSpan.TicksPerMillisecond / 1000);
                                    }
                                    xs = x1.Select(x => (DateTime.ParseExact(x, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture).Ticks / TimeSpan.TicksPerMillisecond / 1000) - timeOffset).Select(t => (double)t).ToList();
                                    break;
                                case AxisType.CountTime:
                                    xs = Enumerable.Range(0, y1.Count).Select(x => (double)x).ToList();
                                    break;
                            }
                            line.Plot(xs, ys);
                            plotGrid.Children.Add(line);
                        }
                        break;
                    }
                case GraphType.ScatterGraph:
                    {
                        for (int i = 0; i < _selectedData.Count; ++i)
                        {
                            string seriesName = _selectedData.Keys.ToList()[i];

                            // make a new scatter graph for each series in the data
                            var scatter = new InteractiveDataDisplay.WPF.CircleMarkerGraph();
                            scatter.Description = seriesName;
                            scatter.StrokeThickness = 1;
                            scatter.Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_palette[i]));

                            List<Tuple<string, string>> series = _selectedData[seriesName];
                            List<string> x1 = series.Select(_ => _.Item1).ToList();
                            List<double> xs = new List<double>();
                            List<string> y1 = series.Select(_ => _.Item2).ToList();
                            List<double> ys = y1.Select(y => double.Parse(y)).ToList();

                            switch (ComboBoxXAxisSelect.SelectedItem)
                            {
                                case AxisType.OATime:
                                    xs = x1.Select(x => DateTime.ParseExact(x, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture).ToOADate()).ToList();
                                    break;
                                case AxisType.RelativeTime:
                                    // compute time offset if relative time is selected
                                    long timeOffset = -1; // units = [s]
                                    foreach (Tuple<string, string> point in series)
                                    {
                                        DateTime t = DateTime.ParseExact(point.Item1, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture);
                                        timeOffset = (timeOffset < 0) ? t.Ticks / TimeSpan.TicksPerMillisecond / 1000 : Math.Min(timeOffset, t.Ticks / TimeSpan.TicksPerMillisecond / 1000);
                                    }
                                    xs = x1.Select(x => (DateTime.ParseExact(x, "[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture).Ticks / TimeSpan.TicksPerMillisecond / 1000) - timeOffset).Select(t => (double)t).ToList();
                                    break;
                                case AxisType.CountTime:
                                    xs = Enumerable.Range(0, y1.Count).Select(x => (double)x).ToList();
                                    break;
                            }

                            List<double> sizes = Enumerable.Repeat(10, y1.Count).Select(x => (double)x).ToList();
                            scatter.PlotSize(xs, ys, sizes);
                            plotGrid.Children.Add(scatter);
                        }
                        break;
                    }
                case GraphType.BarGraph:
                    {
                        for (int i = 0; i < _selectedData.Count; ++i)
                        {
                            string seriesName = _selectedData.Keys.ToList()[i];
                            var bars = new InteractiveDataDisplay.WPF.BarGraph();
                            bars.Description = seriesName;
                            bars.Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_palette[i]));

                            plotter.BottomTitle = "";

                            List<Tuple<string, string>> series = _selectedData[seriesName];
                            List<string> y1 = series.Select(_ => _.Item2).ToList();
                            List<double> y2 = y1.Select(y => double.Parse(y)).ToList();
                            bars.PlotBars(y2);

                            plotGrid.Children.Add(bars);
                        }
                        break;
                    }
            }
        }

        private void UpdateColorPalette(in int count)
        {
            _palette.Clear();
            for (int i = 0; i < count; ++i)
            {
                _palette.Add(RandomColor());
            }
        }

        /// <summary>
        /// Generate a random color in the format #RRGGBB
        /// </summary>
        /// <returns></returns>
        private static string RandomColor()
        {
            string hexOutput = String.Format("{0:X}", _rng.Next(0, 0xFFFFFF));
            while (hexOutput.Length < 6)
                hexOutput = "0" + hexOutput;
            return "#" + hexOutput;
        }
    }
}
