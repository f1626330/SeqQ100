using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.WPF.Framework
{
    public class LineItem
    {
        public string Item { get; set; }
    }

    

    public class CSVFileViewModel : ViewModelBase
    {
       
        public CSVFileViewModel()
        {
        }

        ObservableCollection<ObservableCollection<LineItem>> _Lines;
        public ObservableCollection<ObservableCollection<LineItem>> Lines
        {
            get
            {
                return _Lines;
            }
            set
            {
                SetProperty(ref _Lines, value, nameof(Lines));
            }
        }

        int _LineItemCounts = 1;
        public int LineItemCounts
        {
            get => _LineItemCounts;
            set
            {
                SetProperty(ref _LineItemCounts, value, nameof(LineItemCounts));
            }
        }

        bool _IsReadOnly = true;
        public bool IsReadonly
        {
            get => _IsReadOnly;
            set
            {
                SetProperty(ref _IsReadOnly, value, nameof(IsReadonly));
            }
        }

        bool _IsTextChanged = true;
        public bool IsTextChanged
        {
            get => _IsTextChanged;
            set
            {
                SetProperty(ref _IsTextChanged, value, nameof(IsTextChanged));
            }
        }

        private ICommand _TextChangedCommand = null;
        public ICommand TextChangedCommand
        {
            get
            {
                if (_TextChangedCommand == null)
                {
                    _TextChangedCommand = new RelayCommand(o => TextChanged());
                }
                return _TextChangedCommand;
            }
        }


        void TextChanged()
        {
            IsTextChanged = true;
        }

        public string FileName { get; set; }

        public bool LoadFile(string fileName)
        {
            ObservableCollection<ObservableCollection<LineItem>> lines = new ObservableCollection<ObservableCollection<LineItem>>();
            //int indexCol = -1;
            using (TextFieldParser parser = new TextFieldParser(fileName))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                int maxItemCount = 0;
                while (!parser.EndOfData)
                {
                    //Processing row
                    string[] fields = parser.ReadFields();
                    if (maxItemCount < fields.Length)
                    {
                        maxItemCount = fields.Length;
                    }
                    ObservableCollection<LineItem> line1 = new ObservableCollection<LineItem>();
                    foreach (string field in fields)
                    {
                        line1.Add(new LineItem() { Item = field });
                    }
                    lines.Add(line1);
                }
                LineItemCounts = maxItemCount;
                Lines = lines;
                IsTextChanged = false;
                FileName = fileName;
            }
            return true;
        }

        public bool SaveFile(string fileName, bool reload )
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = FileName;
                }
                using (StreamWriter bw = new StreamWriter(File.Create(fileName)))
                {

                    foreach (var line in Lines)
                    {
                        int counts = 0;
                        int totalCounts = line.Count;

                        foreach (LineItem it in line)
                        {
                            if (string.IsNullOrEmpty(it.Item))
                            {
                                if (counts < totalCounts - 1)
                                {
                                    bw.Write(",");
                                }
                            }
                            else
                            {
                                if (it.Item.Contains(","))
                                {

                                    bw.Write(string.Format("\"{0}\"", it.Item));

                                }
                                else
                                {
                                    bw.Write(it.Item);
                                }
                                if (totalCounts > 1 && counts < totalCounts - 1)
                                {
                                    bw.Write(",");
                                }
                            }
                            counts++;
                        }
                        bw.WriteLine();
                    }

                    bw.Close();
                }
                IsTextChanged = false;
                if (reload)
                {
                    LoadFile(fileName);
                }
                return true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }

        }
    }
}
