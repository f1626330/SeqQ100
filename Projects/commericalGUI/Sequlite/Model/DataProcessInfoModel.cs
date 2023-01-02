using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class DataProcessInfoModel : ModelBase
    {
        public DataProcessInfoModel(ISeqApp seqApp)
        {
            MaxColumns = 1;
            MaxRows = 1;
            MaxCycles = 1;
            SeqApp = seqApp;
        }
        public ISequence SequenceApp { get; set; }
        ISeqApp SeqApp { get; }
        string _DataInputPath;
        public string DataInputPath { get => _DataInputPath; set => SetProperty(ref _DataInputPath, value); }

        string _DataOutputDir;
        public string DataOutputDir { get => _DataOutputDir; set => SetProperty(ref _DataOutputDir, value); }

        
        string _DataOutputSubDirPrefix = "Work";
        string _previousDataOutputSubDirPrefix = "Work";
        public string DataOutputSubDirPrefix { get => _DataOutputSubDirPrefix; set => SetProperty(ref _DataOutputSubDirPrefix, value); }

        string _SessionId;
       
        public string SessionId { get => _SessionId; set => SetProperty(ref _SessionId, value); }

        //public string ExpName => $"{DataOutputSubDirPrefix}_{SessionId}";
        //public string WorkingDir => Path.Combine(DataOutputDir, ExpName);//$"{DataOutputSubDirPrefix}_{SessionId}");
        public string ExpName => $"{DataOutputSubDirPrefix}";
        public string WorkingDir => UsingPreviousWorkingDir? Path.Combine(DataOutputDir,DataOutputSubDirPrefix): Path.Combine(DataOutputDir, $"{ExpName}_{SessionId}");

        
        public string WrokingDirTooltip { get => UsingPreviousWorkingDir ? "Chose an existing working directory" : 
                "A prefix for the working directory"; }
        public string OutputDirTooltip { get => UsingPreviousWorkingDir ? "Parent directory of the selected working directory" :
                "Input a path that will append a working directory for saving output data"; }

        bool _UseSlidingWindow;
        public bool UseSlidingWindow
        {
            get => _UseSlidingWindow; set => SetProperty(ref _UseSlidingWindow, value);
        }

        bool _UsingPreviousWorkingDir;
        public bool UsingPreviousWorkingDir { get => _UsingPreviousWorkingDir; set
            {
                if (SetProperty(ref _UsingPreviousWorkingDir, value))
                {
                    if (value)
                    {
                        DataOutputDir = "";
                        _previousDataOutputSubDirPrefix = DataOutputSubDirPrefix;
                        DataOutputSubDirPrefix = "";
                        
                    }
                    else if ( _previousDataOutputSubDirPrefix != "")
                    {
                        DataOutputSubDirPrefix = _previousDataOutputSubDirPrefix;
                        
                        OnPropertyChanged(nameof(DataInputPath));
                    }
                    OnPropertyChanged(nameof(WrokingDirTooltip));
                    OnPropertyChanged(nameof(OutputDirTooltip));
                }
            }
        }

        bool _SeqInfoLoaded;
        public bool SeqInfoLoaded { get => _SeqInfoLoaded; set => SetProperty(ref _SeqInfoLoaded, value); }
        SequenceInfo _SeqInfo = new SequenceInfo();
        public SequenceInfo SeqInfo
        {
            get => _SeqInfo;
            set
            {
                if (SetProperty(ref _SeqInfo, value))
                {
                    InitLanes(_SeqInfo.Lanes);
                    
                    MaxColumns = _SeqInfo.Column;
                    MaxRows = _SeqInfo.Rows;
                    MaxCycles = _SeqInfo.Cycles;
                    OnPropertyChanged(nameof(TemplateOptionItem));
                    OnPropertyChanged(nameof(Rows));
                    OnPropertyChanged(nameof(Columns));
                    OnPropertyChanged(nameof(Index1Cycle));
                    OnPropertyChanged(nameof(Index2Cycle));
                    OnPropertyChanged(nameof(Cycles));
                    OnPropertyChanged(nameof(Index1Enabled));
                    OnPropertyChanged(nameof(Index2Enabled));
                    OnPropertyChanged(nameof(Paired));
                    OnPropertyChanged(nameof(TemplateOptionItem));
                    OnPropertyChanged(nameof(SelectedIndTemplate));
                }
            }
        }

        int MaxColumns { get; set; }
        int MaxRows { get; set; }
        int MaxCycles { get; set; }
        //TemplateOptions _TemplateOptionItem = TemplateOptions.t8;
        public TemplateOptions TemplateOptionItem
        {
            get => _SeqInfo.Template;
            set
            {
                if (_SeqInfo.Template != value)
                {
                    _SeqInfo.Template = value;
                    OnPropertyChanged(nameof(TemplateOptionItem));
                    
                }
            }
        }

        public TemplateOptions SelectedIndTemplate
        {
            get => _SeqInfo.IndexTemplate;
            set
            {
                if (_SeqInfo.IndexTemplate != value)
                {
                    _SeqInfo.IndexTemplate = value;
                    OnPropertyChanged(nameof(SelectedIndTemplate));

                }
            }
        }


        List<TemplateOptions> _TemplateOptions;
        List<TemplateOptions> _IndexTemplateOptions;

        public List<TemplateOptions> Templateoptions
        {
            get
            {
                if (_TemplateOptions == null)
                {
                    TemplateOptionsHelper.GetTemplates(out _TemplateOptions, out _IndexTemplateOptions);
                }
                return _TemplateOptions;
            }
        }
        public List<TemplateOptions> IndexTemplateoptions
        {
            get
            {
                if (_IndexTemplateOptions == null)
                {
                    TemplateOptionsHelper.GetTemplates(out _TemplateOptions, out _IndexTemplateOptions);
                }
                return _IndexTemplateOptions;
            }
        }

        public int Cycles
        {
            get => _SeqInfo.Cycles;
            set
            {
                if (_SeqInfo.Cycles != value)
                {
                    _SeqInfo.Cycles = value;
                    OnPropertyChanged(nameof(Cycles));
                }
            }
        }


        public int Index1Cycle
        {
            get => _SeqInfo.Index1Cycle;
            set
            {
                if (_SeqInfo.Index1Cycle != value)
                {
                    _SeqInfo.Index1Cycle = value;
                    OnPropertyChanged(nameof(Index1Cycle));
                }
            }
        }

        public int Index2Cycle
        {
            get => _SeqInfo.Index2Cycle;
            set
            {
                if (_SeqInfo.Index2Cycle != value)
                {
                    _SeqInfo.Index2Cycle = value;
                    OnPropertyChanged(nameof(Index2Cycle));
                }
            }
        }

        public int Rows
        {
            get => _SeqInfo.Rows;
            set
            {
                if (_SeqInfo.Rows != value)
                {
                    _SeqInfo.Rows = value;
                    OnPropertyChanged(nameof(Rows));
                }
            }
        }

        public int Columns
        {
            get => _SeqInfo.Column;
            set
            {
                if (_SeqInfo.Column != value)
                {
                    _SeqInfo.Column = value;
                    OnPropertyChanged(nameof(Columns));
                }
            }
        }

        public bool Paired
        {
            get => _SeqInfo.Paired;
            set
            {
                if (_SeqInfo.Paired != value)
                {
                    _SeqInfo.Paired = value;
                    OnPropertyChanged(nameof(Paired));
                }
            }
        }

        public bool Index1Enabled
        {
            get => _SeqInfo.Index1Enabled;
            set
            {
                if (_SeqInfo.Index1Enabled != value)
                {
                    _SeqInfo.Index1Enabled = value;
                    OnPropertyChanged(nameof(Index1Enabled));
                }
            }
        }

        public bool Index2Enabled
        {
            get => _SeqInfo.Index2Enabled;
            set
            {
                if (_SeqInfo.Index2Enabled != value)
                {
                    _SeqInfo.Index2Enabled = value;
                    OnPropertyChanged(nameof(Index2Enabled));
                }
            }
        }



        public override string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {
                    case "DataInputPath":
                        {
                            if (string.IsNullOrEmpty(DataInputPath))
                            {

                                error = "Run info file path cannot be empty.";
                            }
                            else if (!File.Exists(DataInputPath))
                            {

                                error = "Run info file path doesn't exist.";
                            }
                            else
                            {
                                string dataInfoFile = DataInputPath;// Path.Combine(DataInputDir, DataInfoFileName);
                                if (!LoadDataInfo(dataInfoFile))
                                {
                                    error = $"Invalid run info file {Path.GetFileName(dataInfoFile)}.";
                                }
                            }
                        }
                        break;
                    case "DataOutputDir":
                        {
                            if (string.IsNullOrEmpty(DataOutputDir))
                            {

                                error = "Data output folder cannot be empty.";
                            }
                            else if (!IsValidPath(DataOutputDir))
                            {

                                error = "Data output folder is not valid.";
                            }
                        }
                        break;
                    case "DataOutputSubDirPrefix":
                        {
                            if (string.IsNullOrEmpty(DataOutputSubDirPrefix))
                            {
                                error = "Sub-folder prefix cannot be empty.";
                            }
                        }
                         break;
                    case "Cycles":
                        if (Cycles <= 0)
                        {

                            error = "Must be a positive number.";
                        }
                        else if (MaxCycles > 0 && Cycles > MaxCycles)
                        {
                            error = $"Rows cannot be larger than {MaxCycles}";
                        }
                        break;
                    case "Rows":
                        if (Rows <= 0)
                        {

                            error = "Must be a positive number.";
                        }
                        else if (MaxRows > 0 && Rows > MaxRows)
                        {
                            error = $"Rows cannot be larger than {MaxRows}";
                        }
                        break;
                    case "Columns":
                        if (Columns <= 0)
                        {

                            error = "Must be a positive number.";
                        }
                        else if (MaxColumns > 0 && Columns > MaxColumns)
                        {
                            error = $"Rows cannot be larger than {MaxColumns}";
                        }
                        break;
                    
                }

                UpdateErrorBits(columnName, !string.IsNullOrEmpty(error));
                return error;
            }
        }


        private bool IsValidPath(string path, bool exactPath = true)
        {
            bool isValid = true;

            try
            {
                string fullPath = Path.GetFullPath(path);

                if (exactPath)
                {
                    string root = Path.GetPathRoot(path);
                    isValid = string.IsNullOrEmpty(root.Trim(new char[] { '\\', '/' })) == false;
                }
                else
                {
                    isValid = Path.IsPathRooted(path);
                }
            }
            catch (Exception )
            {
                isValid = false;
            }

            return isValid;
        }

        List<DecoratedItem<LaneDataEnum>> _Lanes;
        public List<DecoratedItem<LaneDataEnum>> Lanes { get => _Lanes; set => SetProperty(ref _Lanes, value); }
        DecoratedItem<LaneDataEnum> _SelectedLane;
        public DecoratedItem<LaneDataEnum> SelectedLane
        {
            get => _SelectedLane;
            set
            {
                if (SetProperty(ref _SelectedLane, value))
                {
                   // OnUpdateLineDataSelectionChanged();
                }
            }
        }
        void InitLanes(int[] lanes)
        {
            //int[] lanes = SequenceApp.SequenceInformation.Lanes;

            if (lanes == null)
            {
                Lanes?.Clear();
                SelectedLane = null;
            }
            else
            {
                List<DecoratedItem<LaneDataEnum>> laneList = new List<DecoratedItem<LaneDataEnum>>();
                foreach (var it in lanes)
                {
                    LaneDataEnum laneItem = LaneDataEnum.AllLane;
                    switch (it)
                    {
                        case 1:
                            laneItem = LaneDataEnum.Lane1;
                            break;
                        case 2:
                            laneItem = LaneDataEnum.Lane2;
                            break;
                        case 3:
                            laneItem = LaneDataEnum.Lane3;
                            break;
                        case 4:
                            laneItem = LaneDataEnum.Lane4;
                            break;
                    }
                    if (laneItem != LaneDataEnum.AllLane)
                    {
                        laneList.Add(new DecoratedItem<LaneDataEnum>(laneItem, laneItem.Description()));
                    }
                }
                //int selectedIndex = 0;
                //if (lanes.Length > 1)
                //{
                //    laneList.Add(new DecoratedItem<LaneDataEnum>(LaneDataEnum.AllLane, LaneDataEnum.AllLane.Description()));
                //    selectedIndex = lanes.Length ;
                //}

                Lanes = laneList;
                //SelectedLane = Lanes[selectedIndex];
            }
        }

        public void SetDataInfo(string fileName)
        {
            if (SequenceApp == null)
            {
                SequenceApp = SeqApp.CreateSequenceInterface(false); //do not need hardware to be initialized
                
            }
            //string dir = Path.GetDirectoryName(fileName);
            //string file = Path.GetFileName(fileName);
            //if (DataInfoFileName != file)
            //{

            //    DataInfoFileName = file;
            //    DataInputPath = "";
            //}
            DataInputPath = fileName;// dir;
        }
        public bool LoadDataInfo(string fileName)
        {
            if (SequenceApp == null)
            {
                SequenceApp = SeqApp.CreateSequenceInterface(false); //do not need hardware to be initialized

            }
            SequenceInfo seqInfo = SequenceApp.GetOfflineDataSequenceInfo(fileName);
            bool loaded = false;
            if (seqInfo != null)
            {
                SeqInfo = seqInfo;
                loaded = true;
            }
            else
            {
                SeqInfo = new SequenceInfo();
            }
            SeqInfoLoaded = loaded;
            return loaded;
        }

        public void SetDataOutputDir(string dir)
        {
            if (UsingPreviousWorkingDir)
            {
                string parentDir = Path.GetDirectoryName(dir);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    DataOutputDir = parentDir;
                    string subDir = Path.GetFileName(dir);
                    DataOutputSubDirPrefix = subDir;
                    
                    //SessionId = "";
                    //string runInfo = Path.Combine(dir, "info.json");

                    //DataInputPath = runInfo;

                }
            }
            else
            {
                DataOutputDir = dir;
            }
        }

        public List<string> SelectedTiles { get; set; } = null;

    }
}
