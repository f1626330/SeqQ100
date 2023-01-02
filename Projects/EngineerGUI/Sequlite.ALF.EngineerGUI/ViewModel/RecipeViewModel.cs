using Microsoft.Win32;
using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Sequlite.Image.Processing;
using System.Collections.Generic;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using InteractiveDataDisplay.WPF;
using System.Windows.Media;
using Sequlite.ALF.Common;
using System.Net.Mail;
using System.Threading.Tasks;
using Sequlite.ALF.SerialPeripherals;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class RecipeViewModel : ViewModelBase
    {
        #region Private fields
        public static readonly string LoggerSubSystemName = "RecipeVM";
        private ISeqFileLog Logger = SeqLogFactory.GetSeqFileLog(LoggerSubSystemName);
        private ISeqFileLog BackupLogger = SeqLogFactory.GetSeqFileLog("RecipeVMBackup");
        private string _LoadedRecipeInfo;
        private Recipe _LoadedRecipe;
        private RecipeRunThread _RecipeRunThread;
        private RecipeRunThreadV2 _RecipeRunThreadV2;
        private LineGraph _linegraph;
        internal void SetGraph(LineGraph linegraph)
        {
            _linegraph = linegraph;
        }
        private StepsTreeViewModel _SelectedStep;
        // private StringBuilder _RunningLog = new StringBuilder();
        private StringBuilder _WriteLog = new StringBuilder();
        private string _RecipeRunTimeStampedLogFullPathName;
        private System.Timers.Timer _Timer;
        private System.Timers.Timer _UsageTimer;
        private static object _RunninglogLock = new object();
        private static object _StepsLock = new object();
        private static PerformanceCounter _RAMCounter;
        private static PerformanceCounter _CPUCounter;
        private string _ImageQC;
        private bool _isEnableOLA = false;
        private bool _isEnablePP = false;
        private bool _isBC = true;
        private bool _isOneRef = false;
        private double _ExposureFactor = 1;
        private double _RedLEDFactor = 1;
        private double _GreenLEDFactor = 1;
        private List<string> _OLAParameterOptions = new List<string>();
        private List<string> _OLATileOptions = new List<string>();
        private string _SelectedParameter;
        private string _SelectedTile;
        private double _B_Offset = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomOffset;
        private double _T_Offset = SettingsManager.ConfigSettings.AutoFocusingSettings.TopOffset;
        private bool _IsIndex = false;
        private int _StartInc = 2;
        private string _SubscribedEmail = null;
        private bool _IsMachineRev2;
        private TemplateOptions _SelectedTemplate;
        private bool _isBackUp = true;
        private bool _IsRecalculateOffset = true;
        #endregion Private fields
        MotionViewModel MotionVM { get; }
        CameraViewModel CameraVM { get; }
        FluidicsViewModel FluidicsVM { get; }
        ChemistryViewModel ChemistryVM { get; }
        MainBoardViewModel MainBoardVM { get; }
        IViewModelStatus ViewModelStatus { get; }
        RecipeRunSettings RecipeRunConfig { get; }
        OLAJobManager OLAJobs { get; set; }
        SequenceDataBackup ImageBackingup { get;  set; }
        public RecipeViewModel(RecipeRunSettings _recipeRunConfig, bool ismachinerev2, IViewModelStatus viewModelStatus, MotionViewModel motionVM, CameraViewModel cameraVM,
            ChemistryViewModel chemistryVM,
            FluidicsViewModel fluidicsVM, MainBoardViewModel mainBoardVM)
        {

            ViewModelStatus = viewModelStatus;
            IsBusy = false;
            MotionVM = motionVM;
            CameraVM = cameraVM;
            ChemistryVM = chemistryVM;
            FluidicsVM = fluidicsVM;
            MainBoardVM = mainBoardVM;
            _IsMachineRev2 = ismachinerev2;
            RecipeRunConfig = _recipeRunConfig;
            InitialLogWindowVM();
            TemplateOptions = new List<TemplateOptions>();
            foreach(TemplateOptions template in Enum.GetValues(typeof(TemplateOptions)))
            {
                TemplateOptions.Add(template);
            }
            SelectedTemplate = TemplateOptions[0];
        }



        private void ViewModelStatus_OnBusyStatusChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(IsBusy));
        }



        #region Public properties
        public bool IsBusy
        {
            get
            {
                if (ViewModelStatus != null)
                {
                    return ViewModelStatus.IsBusy;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                if (ViewModelStatus != null)
                {
                    ViewModelStatus.IsBusy = value;
                }
                RaisePropertyChanged("IsBusy");
            }
        }
        public List<TemplateOptions> TemplateOptions { get; private set; }
        public TemplateOptions SelectedTemplate
        {
            get { return _SelectedTemplate; }
            set
            {
                if (_SelectedTemplate != value)
                {
                    _SelectedTemplate = value;
                    RaisePropertyChanged(nameof(SelectedTemplate));
                }
            }
        }
        public bool IsOneRef
        {
            get
            {
                return _isOneRef;
            }
            set
            {
                if (_isOneRef != value)
                {
                    _isOneRef = value;
                    RaisePropertyChanged(nameof(IsOneRef));
                }
            }
        }
        public bool IsBackUp
        {
            get
            {
                return _isBackUp;
            }
            set
            {
                if (_isBackUp != value)
                {
                    _isBackUp = value;
                    RaisePropertyChanged(nameof(IsBackUp));
                }
            }
        }
        public string SubscribedEmail
        {
            get { return _SubscribedEmail; }
            set
            {
                if (_SubscribedEmail != value && ((value.ToString().ToLower().Contains(".com") && value.Contains("@")) || value == null))
                {
                    _SubscribedEmail = value;
                    RaisePropertyChanged(nameof(SubscribedEmail));
                }
                else
                {
                    MessageBox.Show("Please input a valid email address");
                }
            }
        }
        public OLAStats OLAStats { get; set; }
        public string SelectedParameter
        {
            get { return _SelectedParameter; }
            set
            {
                if (_SelectedParameter != value)
                {
                    _SelectedParameter = value;
                    RaisePropertyChanged(nameof(SelectedParameter));
                }
            }
        }
        public string SelectedTile
        {
            get { return _SelectedTile; }
            set
            {
                if (_SelectedTile != value)
                {
                    _SelectedTile = value;
                    RaisePropertyChanged(nameof(SelectedTile));
                }
            }
        }
        public bool isEnableOLA
        {
            get { return _isEnableOLA; }
            set
            {
                if (_isEnableOLA != value)
                {
                    _isEnableOLA = value;
                    RaisePropertyChanged(nameof(isEnableOLA));
                }
            }
        }
        public bool isBC
        {
            get { return _isBC; }
            set
            {
                if (_isBC != value)
                {
                    _isBC = value;
                    RaisePropertyChanged(nameof(isBC));
                }
            }
        }

        private bool _IsSimulationMode = false;
        public bool IsSimulationMode
        {
            get { return _IsSimulationMode; }
            set
            {
                if (_IsSimulationMode != value)
                {
                    _IsSimulationMode = value;
                    RaisePropertyChanged(nameof(IsSimulationMode));
                }
            }
        }

        public bool isEnablePP
        {
            get { return _isEnablePP; }
            set
            {
                if (_isEnablePP != value)
                {
                    _isEnablePP = value;
                    RaisePropertyChanged(nameof(isEnablePP));
                }
            }
        }
        public List<string> OLATileOptions
        {
            get { return _OLATileOptions; }
            set
            {
                if (_OLATileOptions != value)
                {
                    _OLATileOptions = value;
                    RaisePropertyChanged(nameof(OLATileOptions));
                }
            }
        }
        public List<string> OLAParameterOptions
        {
            get { return _OLAParameterOptions; }
            set
            {
                if (_OLAParameterOptions != value)
                {
                    _OLAParameterOptions = value;
                    RaisePropertyChanged(nameof(OLAParameterOptions));
                }
            }
        }
        public double ExposureFactor
        {
            get { return _ExposureFactor; }
            set
            {
                if (_ExposureFactor != value)
                {
                    _ExposureFactor = value;
                    RaisePropertyChanged(nameof(ExposureFactor));
                }
            }
        }
        public double RedLEDFactor
        {
            get { return _RedLEDFactor; }
            set
            {
                if (_RedLEDFactor != value)
                {
                    _RedLEDFactor = value;
                    RaisePropertyChanged(nameof(RedLEDFactor));
                }
            }
        }

        public double GreenLEDFactor
        {
            get { return _GreenLEDFactor; }
            set
            {
                if (_GreenLEDFactor != value)
                {
                    _GreenLEDFactor = value;
                    RaisePropertyChanged(nameof(GreenLEDFactor));
                }
            }
        }
        public string FolderStr { get; set; }
        public string ImageQC
        {
            get { return _ImageQC; }
            set
            {
                if (_ImageQC != value)
                {
                    _ImageQC = value;
                    RaisePropertyChanged(nameof(ImageQC));
                }
            }
        }
        public string LoadedRecipeInfo
        {
            get { return _LoadedRecipeInfo; }
            set
            {
                if (_LoadedRecipeInfo != value)
                {
                    _LoadedRecipeInfo = value;
                    RaisePropertyChanged(nameof(LoadedRecipeInfo));
                }
            }
        }
        public ObservableCollection<StepsTreeViewModel> Steps { get; } = new ObservableCollection<StepsTreeViewModel>();
        public StepsTreeViewModel SelectedStep
        {
            get { return _SelectedStep; }
            set
            {
                if (_SelectedStep != value)
                {
                    if (value == null)
                    {
                        _SelectedStep.IsSelected = false;
                    }
                    _SelectedStep = value;
                    if (_SelectedStep != null)
                    {
                        _SelectedStep.IsSelected = true;
                    }
                    RaisePropertyChanged(nameof(SelectedStep));
                }
            }
        }
        public string WriteLog
        {
            get { lock (_RunninglogLock) { return _WriteLog.ToString(); } }
        }

        public double B_Offset
        {
            get { return _B_Offset; }
            set
            {
                if (_B_Offset != value)
                {
                    _B_Offset = value;
                    RaisePropertyChanged(nameof(B_Offset));
                }
            }
        }
        public double T_Offset
        {
            get { return _T_Offset; }
            set
            {
                if (_T_Offset != value)
                {
                    _T_Offset = value;
                    RaisePropertyChanged(nameof(T_Offset));
                }
            }
        }

        public bool IsIndex
        {
            get { return _IsIndex; }
            set
            {
                if (_IsIndex != value)
                {
                    _IsIndex = value;
                    RaisePropertyChanged(nameof(IsIndex));
                }
            }
        }
        public bool IsRecalculateOffset
        {
            get { return _IsRecalculateOffset; }
            set
            {
                if(_IsRecalculateOffset != value)
                {
                    _IsRecalculateOffset = value;
                    RaisePropertyChanged(nameof(IsRecalculateOffset));
                }
            }
        }
        public int StartInc
        {
            get { return _StartInc; }
            set
            {
                if (_StartInc != value)
                {
                    _StartInc = value;
                    RaisePropertyChanged(nameof(StartInc));
                }
            }
        }

        string _OLAStatusInfo;

        public string OLAStatusInfo
        {
            get { return _OLAStatusInfo; }
            set
            {
                _OLAStatusInfo = value;
                RaisePropertyChanged(nameof(OLAStatusInfo));
            }
        }

        public List<double> G1Data { get; set; } = new List<double>();
        #endregion Public properties

        #region Load Recipe Command
        RelayCommand _LoadRecipeCmd;
        public ICommand LoadRecipeCmd
        {
            get
            {
                if (_LoadRecipeCmd == null)
                {
                    _LoadRecipeCmd = new RelayCommand(ExecuteLoadRecipeCmd, CanExecuteLoadRecipeCmd);
                }
                return _LoadRecipeCmd;
            }
        }

        string _RecipePath;
        public string RecipePath
        {
            get { return _RecipePath; }
            set
            {
                if (_RecipePath != value)
                {
                    _RecipePath = value;
                    RaisePropertyChanged(nameof(RecipePath));
                }
            }
        }
        private void ExecuteLoadRecipeCmd(object obj)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Filter = "Recipe File|*.xml";
                if (openDialog.ShowDialog() == true)
                {
                    try
                    {
                        _LoadedRecipe = Recipe.LoadFromXmlFile(openDialog.FileName);
                        LoadedRecipeInfo = string.Format("File Path: {0}\nRecipe Name: {1}\nCreated Time: {2}\nUpdated Time: {3}",
                            openDialog.FileName, _LoadedRecipe.RecipeName, _LoadedRecipe.CreatedTime, _LoadedRecipe.UpdatedTime);
                        RecipePath = $"{_LoadedRecipe.RecipeName}: {openDialog.FileName}";
                        Steps.Clear();
                        foreach (var item in _LoadedRecipe.Steps)
                        {
                            Steps.Add(StepsTreeViewModel.CreateViewModel(item, null));
                        }
                        SelectedStep = Steps.First();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error reading the file.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading the recipe");
            }
        }

        private bool CanExecuteLoadRecipeCmd(object obj)
        {
            return true;
        }
        #endregion Load Recipe Command

        #region Run Recipe Command
        private RelayCommand _RunRecipeCmd;
        public ICommand RunRecipeCmd
        {
            get
            {
                if (_RunRecipeCmd == null)
                {
                    _RunRecipeCmd = new RelayCommand(ExecuteRunRecipeCmd, CanExecuteRunRecipeCmd);
                }
                return _RunRecipeCmd;
            }
        }

        private async void ExecuteRunRecipeCmd(object obj)
        {
            try
            {
                string cmdPara = obj.ToString().ToLower();
                //if(_SubscribedEmail == null)
                //{
                //    MessageBox.Show("Please input your email and re-run");
                //    return;
                //}
                if (cmdPara == "start")
                {
                    LogViewerVM.ClearMessages = true;
                    bool loadCartridge = false;
                    if (!_IsMachineRev2) { MessageBox.Show("Load FC Cover", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); }
                    
                    // Check buffer sipper position. Warn if not down
                    FluidController _FluidController = FluidController.GetInstance();
                    _FluidController.ReadRegisters(FluidController.Registers.OnoffInputs, 1);
                    bool bufferin = _FluidController.BufferTrayIn;
                    bool sipperdown = _FluidController.SipperDown;
                    if (bufferin)
                    {
                        var msgResult = MessageBox.Show("Buffer cartridge is not loaded. Continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (msgResult == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                    if (sipperdown)
                    {
                        var msgResult = MessageBox.Show("Buffer sipper is still up. Continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (msgResult == MessageBoxResult.No)
                        {
                            return;
                        }
                    }


                    if (!FluidicsVM.IsCartridgeLoaded && MotionVM.CMotionCurrentPos >= 0) 
                        //FluidicsVM.IsCartridgeLoaded for all ALF2.0 sipper pos, CMotionCurrentPos >= 0 for ALF 1.1 compatiblity
                    {
                        var msgResult = MessageBox.Show("Cartridge is unloaded, load Cartridge before running the recipe?", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                        if (msgResult == MessageBoxResult.Yes)
                        {
                            loadCartridge = true;
                        }
                        else if (msgResult == MessageBoxResult.No)
                        {
                            MessageBox.Show("Recipe will be running with Cartridge unloaded.");
                        }
                        else if (msgResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("Running Recipe is cancelled.");
                            return;
                        }
                    }

                    //create save folder for log
                    string logDir = RecipeRunConfig.GetRecipeRunLogBaseDir();
                    Directory.CreateDirectory(logDir);
                    //example: "C:\Users\V5-571\Documents\Sequlite\ALF\Recipe\Recipelogs\20200307192137CRT136 RecipeLogs.txt"
                    _RecipeRunTimeStampedLogFullPathName = logDir + DateTime.Now.ToString("yyyyMMddHHmmss") + _LoadedRecipe.RecipeName + " RecipeLogs.txt";

                    //Usage Counter
                    _RAMCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                    _CPUCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    RecipeThreadParameters _RecipeParam = new RecipeThreadParameters()
                    {
                        Bottom_Offset = _B_Offset,
                        Top_Offset = _T_Offset,
                        SelectedTemplate = SelectedTemplate,
                        IsSimulation = IsSimulationMode,
                        IsEnableOLA = isEnableOLA,
                        IsEnablePP = isEnablePP,
                        IsBC = isBC,
                        OneRef = IsOneRef,
                        GLEDinc = _GreenLEDFactor,
                        RLEDinc = _RedLEDFactor,
                        Expoinc = _ExposureFactor,
                        StartInc = _StartInc,
                        UserEmail = _SubscribedEmail,
                        LoadCartridge = loadCartridge,
                        BackUpData = IsBackUp,
                        IsIndex = _IsIndex,
                        IsCalculateOffset = IsRecalculateOffset,
                    };

                    if (!_IsMachineRev2)
                    {
                        if (isEnableOLA || isEnablePP)
                        {
                            OLAJobs = OLAJobManager.GetOLAJobManager();
                        }
                        _RecipeRunThread = new RecipeRunThread(
                        TheDispatcher,
                        RecipeRunConfig,
                        _LoadedRecipe,
                        CameraVM.ActiveCamera,
                        MotionVM.MotionController,
                        MainBoardVM.MainBoard,
                        FluidicsVM.FluidicsInterface,
                        _RecipeParam, null, OLAJobs, true);

                        _RecipeRunThread.OnStepRunUpdated += _RecipeRunThread_OnStepRunUpdated;
                        _RecipeRunThread.OnLoopStepUpdated += _RecipeRunThread_OnLoopStepUpdated;
                        _RecipeRunThread.Completed += _RecipeRunThread_Completed;
                        if (OLAJobs != null)
                        {
                            OLAJobs.OLAInfoUpdated += _RecipeRunThread_OLAUpdated;
                            OLAJobs.OnOLAStatusUpdated += _RecipeRunThread_OnOLAStatusUpdated;
                        }
                        _RecipeRunThread.Name = "Recipe";
                        _RecipeRunThread.IsSimulationMode = IsSimulationMode;
                        _RecipeRunThread.IsEnablePP = isEnablePP;
                    }
                    else
                    {
                        if (isEnableOLA || isEnablePP)
                        {
                            OLAJobs = new OLAJobManager(true/*V2*/);
                        }
                        else
                            OLAJobs = null;

                        ImageBackingup = new SequenceDataBackup(BackupLogger) { IsSimulationMode = IsSimulationMode };
                        ImageBackingup.OnSequenceDataBackupStatus += ImageBackingup_OnImageBackupStatus;

                        _RecipeRunThreadV2 = new RecipeRunThreadV2(
                        TheDispatcher,
                        RecipeRunConfig,
                        _LoadedRecipe,
                        CameraVM.EthernetCameraA,
                        CameraVM.EthernetCameraB,
                        MotionVM.MotionController,
                        MainBoardVM.MainBoard,
                        MainBoardVM.LEDController,
                        FluidicsVM.FluidicsInterface,
                        _RecipeParam, null, OLAJobs, true,
                        ImageBackingup, true);

                        _RecipeRunThreadV2.IsSimulationMode = IsSimulationMode;
                        _RecipeRunThreadV2.OnStepRunUpdated += _RecipeRunThread_OnStepRunUpdated;
                        _RecipeRunThreadV2.OnLoopStepUpdated += _RecipeRunThread_OnLoopStepUpdated;
                        _RecipeRunThreadV2.Completed += _RecipeRunThread_Completed;
                        if (OLAJobs != null)
                        {
                            OLAJobs.OLAInfoUpdated += _RecipeRunThread_OLAUpdated;
                            OLAJobs.OnOLAStatusUpdated += _RecipeRunThread_OnOLAStatusUpdated;
                        }
                        _RecipeRunThreadV2.OnImageSaved += _RecipeRunThreadV2_OnImageSaved;
                        _RecipeRunThreadV2.Name = "Recipe";
                        _RecipeRunThreadV2.IsSimulationMode = IsSimulationMode;
                        _RecipeRunThreadV2.IsEnablePP = isEnablePP;
                    }

                    IsBusy = true;
                    // _RunningLog.Clear();

                    //FileStream fs = new FileStream(_RecipeRunTimeStampedLogFullPathName, FileMode.Create, FileAccess.ReadWrite);
                    //StreamWriter sw = new StreamWriter(fs);
                    //sw.WriteLine(string.Format("Recipe name: {0}", _LoadedRecipe.RecipeName));
                    //sw.WriteLine(string.Format("Starting time: {0}", DateTime.Now));
                    //sw.Flush();
                    //sw.Close();
                    //fs.Close();
                    await Writelog(string.Format("Recipe name: {0}", _LoadedRecipe.RecipeName));
                    await Writelog(string.Format("Starting time: {0}", DateTime.Now));

                    _Timer = new System.Timers.Timer();
                    _Timer.Interval = 2 * 60 * 60 * 1000;
                    _Timer.AutoReset = true;
                    _Timer.Elapsed += _Timer_Elapsed;
                    _Timer.Start();
                    _UsageTimer = new System.Timers.Timer(5 * 60 * 1000);
                    _UsageTimer.AutoReset = true;
                    _UsageTimer.Elapsed += _UsageTimer_Elapsed;
                    _UsageTimer.Start();
                    if (!_IsMachineRev2) { _RecipeRunThread.Start(); } else { _RecipeRunThreadV2.Start(); }
                }
                else if (cmdPara == "stop")
                {
                    AbortingRecipeRun = true;
                    await Task.Run(() =>
                    {
                        
                        if (!_IsMachineRev2) { _RecipeRunThread.Abort(); } else { _RecipeRunThreadV2.Abort(); }
                    });
                    UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process is cancelled manually."));
                    SendEmail($"Recipe:{_LoadedRecipe.RecipeName} is canceled manually.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to run recipe with error {0}, Exception:\n {1}", ex.Message, ex.StackTrace));
            }
            finally
            {
                AbortingRecipeRun = false; 
            }
        }

        private void ImageBackingup_OnImageBackupStatus(object sender, SequenceDataBackupEventArgs e)
        {
            if (e.IsError) { BackupLogger.LogError(e.Message); }
            else { BackupLogger.Log(e.Message); }
        }

        bool _AbortingRecipeRun = false;
        public bool AbortingRecipeRun
        {
            get
            {
                return _AbortingRecipeRun;
            }
            set
            {
                SetProperty(ref _AbortingRecipeRun, value);
            }
        }
        private void _RecipeRunThread_OnOLAStatusUpdated(object sender, OLARunningEventArgs e)
        {
            OLAStatusInfo = e.Message;
        }

        private async void _Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await Writelog(WriteLog);
            //lock (_RunninglogLock)
            //{
            //    _RunningLog.Clear();
            //}
        }

        private void _UsageTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                UpdateRunningLog(string.Format("[{0} Temp: C-{1:F1} H-{4:F1}] CPU:{2} Memory:{3:F2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, (int)_CPUCounter.NextValue(), _RAMCounter.NextValue(), ChemistryVM.HeatSinkTemper));
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in usage updating: " + ex.Message));
            }
        }

        private void _RecipeRunThread_OLAUpdated(OLAWorkingDirInfo e)
        {
            try
            {
                OLAStats = new OLAStats(e.BaseWorkingDir);
 
                OLAParameterOptions = OLAStats.AvailableParams();
                if (OLAParameterOptions.Count() > 0)
                    _SelectedParameter = OLAParameterOptions[0];
                else
                    _SelectedParameter = "";

                OLATileOptions = OLAStats.AvailableTiles();
                if (OLATileOptions.Count() > 0)
                    _SelectedTile = OLATileOptions[0];
                else
                    _SelectedTile = "";
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in OLA updating event handler: " + ex.Message));
            }
        }
        
        private async void _RecipeRunThread_OnLoopStepUpdated(StepsTree steptree)
        {
            try
            {
                RecipeStepBase step = steptree.Step;
                if ((!_IsMachineRev2 && !_RecipeRunThread.IsInnerRecipeRunning) || (_IsMachineRev2 && !_RecipeRunThreadV2.IsInnerRecipeRunning))
                {
                    TheDispatcher.Invoke(new Action(() =>
                    {
                        lock (_StepsLock)
                        {
                            foreach (var item in Steps)
                            {
                                if (UpdateLoopCounts(item, step))
                                {
                                    break;
                                }
                            }
                        }

                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in loop step updating event handler: " + ex.Message));
            }
            await Writelog(WriteLog);
        }

        private bool UpdateLoopCounts(StepsTreeViewModel tree, RecipeStepBase step)
        {
            if (tree.Content.Step == step)
            {
                var temp = tree;
                if (temp.Parent == null)
                {
                    int index = Steps.IndexOf(tree);
                    Steps.Remove(tree);
                    Steps.Insert(index, temp);
                }
                else
                {
                    int index = temp.Parent.Children.IndexOf(temp);
                    temp.Parent.Children.Remove(temp);
                    temp.Parent.Children.Insert(index, temp);
                }
                return true;
            }
            foreach (var subItem in tree.Children)
            {
                if (UpdateLoopCounts(subItem, step))
                {
                    return true;
                }
            }
            return false;
        }

        private async void _RecipeRunThread_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            IsBusy = false;
            try
            {
                ViewModelStatus.StatusInfo = null;
                _Timer.Stop();
                _Timer.Close();
                _UsageTimer.Stop();
                _UsageTimer.Close();
                FluidicsVM.FluidicsInterface.Valve.SetToNewPos(24, true);
                
                // refactored from copypasta code. But really RecipeV2 needs to be fixed
                RecipeRunThreadBase t;
                if (_IsMachineRev2)
                {
                    _RecipeRunThreadV2.OnImageSaved -= _RecipeRunThreadV2_OnImageSaved;
                    t = _RecipeRunThreadV2;
                }
                else
                {
                    if (ImageBackingup != null)
                    {
                        ImageBackingup.OnSequenceDataBackupStatus -= ImageBackingup_OnImageBackupStatus;
                    }
                    t = _RecipeRunThread;
                }

                string logMessage;
                string emailMessage; 
                if(t.ExitStat == ThreadBase.ThreadExitStat.None)
                {
                    logMessage = $"[{DateTime.Now.ToString("HH:mm:ss")} Temp: { ChemistryVM.ChemiTemperGet:F1}°] " +
                        $"Recipe has completed with {Logger.WarningCount} warnings and {Logger.ErrorCount} errors.";
                    emailMessage = $"Recipe: {_LoadedRecipe.RecipeName} has completed";
                }
                else //t.ExitStat == ThreadBase.ThreadExitStat.Error or Abort
                {
                   logMessage = $"[{DateTime.Now.ToString("HH:mm:ss")} Temp: { ChemistryVM.ChemiTemperGet:F1}°] Recipe was terminated by errors: {t.ExMessage}";
                    emailMessage = $"Recipe: {_LoadedRecipe.RecipeName} was terminated by errors: {t.ExMessage}";
                }
                UpdateRunningLog(logMessage);
                await Writelog(WriteLog);
                SendEmail(emailMessage);

                t.OnStepRunUpdated -= _RecipeRunThread_OnStepRunUpdated;
                t.OnLoopStepUpdated -= _RecipeRunThread_OnLoopStepUpdated;
                t.Completed -= _RecipeRunThread_Completed;

                if (OLAJobs != null)
                {
                    OLAJobs.OLAInfoUpdated -= _RecipeRunThread_OLAUpdated;
                    OLAJobs.OnOLAStatusUpdated -= _RecipeRunThread_OnOLAStatusUpdated;
                }
                t = null;


                //
                //if (!_IsMachineRev2)
                //{
                //    if (_RecipeRunThread.ExitStat == ThreadBase.ThreadExitStat.None)
                //    {
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Recipe has completed."));
                //        await Writelog(WriteLog);
                //        SendEmail($"Recipe: {_LoadedRecipe.RecipeName} has completed");
                //        //MessageBox.Show("Unload FC Cover", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    }
                //    else if (_RecipeRunThread.ExitStat == ThreadBase.ThreadExitStat.Error)
                //    {
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, _RecipeRunThread.ExMessage));
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process is terminated by errors."));
                //        await Writelog(WriteLog);
                //        SendEmail($"Recipe: {_LoadedRecipe.RecipeName} is terminated by errors.");
                //    }

                //    _RecipeRunThread.OnStepRunUpdated -= _RecipeRunThread_OnStepRunUpdated;
                //    _RecipeRunThread.OnLoopStepUpdated -= _RecipeRunThread_OnLoopStepUpdated;
                //    _RecipeRunThread.Completed -= _RecipeRunThread_Completed;
                //    if (OLAJobs != null)
                //    {
                //        OLAJobs.OLAInfoUpdated -= _RecipeRunThread_OLAUpdated;
                //        OLAJobs.OnOLAStatusUpdated -= _RecipeRunThread_OnOLAStatusUpdated;
                //    }
                //    if (ImageBackingup != null)
                //    {
                //        ImageBackingup.OnSequenceDataBackupStatus -= ImageBackingup_OnImageBackupStatus;
                //    }
                //    _RecipeRunThread = null;
                //}
                //else
                //{
                //    if (_RecipeRunThreadV2.ExitStat == ThreadBase.ThreadExitStat.None)
                //    {
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Recipe has completed."));
                //        await Writelog(WriteLog);
                //        SendEmail($"Recipe: {_LoadedRecipe.RecipeName} has completed");
                //        //MessageBox.Show("Unload FC Cover", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    }
                //    else if (_RecipeRunThreadV2.ExitStat == ThreadBase.ThreadExitStat.Error)
                //    {
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, _RecipeRunThreadV2.ExMessage));
                //        UpdateRunningLog(string.Format("[{0} Temp: {1:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process is terminated by errors."));
                //        await Writelog(WriteLog);
                //        SendEmail($"Recipe: {_LoadedRecipe.RecipeName} was terminated by errors.");
                //    }

                //    _RecipeRunThreadV2.OnStepRunUpdated -= _RecipeRunThread_OnStepRunUpdated;
                //    _RecipeRunThreadV2.OnLoopStepUpdated -= _RecipeRunThread_OnLoopStepUpdated;
                //    _RecipeRunThreadV2.Completed -= _RecipeRunThread_Completed;
                //    if (OLAJobs != null)
                //    {
                //        OLAJobs.OLAInfoUpdated -= _RecipeRunThread_OLAUpdated;
                //        OLAJobs.OnOLAStatusUpdated -= _RecipeRunThread_OnOLAStatusUpdated;
                //    }
                //    _RecipeRunThreadV2.OnImageSaved -= _RecipeRunThreadV2_OnImageSaved;
                //    _RecipeRunThreadV2 = null;
                //}

                _RAMCounter.Close();
                _RAMCounter.Dispose();
                _CPUCounter.Close();
                _RAMCounter.Dispose();
                _Timer.Dispose();
                _UsageTimer.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in recipe complete event handler: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
            }
        }

        private async Task Writelog(String log)
        {
            await Task.Run(() =>
            {
                lock (_RunninglogLock)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(log))
                        {

                            FileStream fs = new FileStream(_RecipeRunTimeStampedLogFullPathName, FileMode.Append, FileAccess.Write,
                                FileShare.Read, 4096, FileOptions.Asynchronous);
                            StreamWriter sw = new StreamWriter(fs);
                            sw.Write(log);
                            sw.Flush();
                            sw.Close();
                            fs.Close();
                        }
                        _WriteLog.Clear();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to write recipe run message to the file" + _RecipeRunTimeStampedLogFullPathName + " with error " + ex.Message);
                    }
                }
            });
        }

        private void _RecipeRunThreadV2_OnImageSaved(ImageSavedEventArgs args)
        {
            UpdateRunningLog(string.Format("[{0} Temp: C-{1:F1}° H-{3:F1}° PH-{4:F1}] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, args.Message, ChemistryVM.HeatSinkTemper, MainBoardVM.MainBoardController.FluidPreHeatingCrntTemper));
        }

        private async void _RecipeRunThread_OnStepRunUpdated(RecipeStepBase step, string msg, bool isCritical)
        {
            try
            {
                ViewModelStatus.StatusInfo = msg + "\r\n" + step.ToString();
                if (RecipeRunThreadBase.IsStartRunningMessage(msg))
                {
                    UpdateRunningLog(string.Format("[{0} Temp: C-{1:F1}° H-{4:F1}] Thread:{3} {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, step.ToString(), Thread.CurrentThread.Name, ChemistryVM.HeatSinkTemper));
                }
                else
                {
                    UpdateRunningLog(string.Format("[{0} Temp: C-{1:F1}° H-{3:F1}° PH-{4:F1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, msg, ChemistryVM.HeatSinkTemper, MainBoardVM.MainBoardController.FluidPreHeatingCrntTemper));
                }

                if (isCritical)
                {

                    await Writelog(WriteLog);
                    lock (_RunninglogLock)
                    {

                        SendEmail(msg);
                    }
                }
                if (!isCritical && RecipeRunThreadBase.IsStartRunningMessage(msg))
                {
                    if ((!_IsMachineRev2 && !_RecipeRunThread.IsInnerRecipeRunning) || (_IsMachineRev2 && !_RecipeRunThreadV2.IsInnerRecipeRunning))
                    {
                        lock (_StepsLock)
                        {
                            foreach (var item in Steps)
                            {
                                if (MarkStep(item, step))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in step run event handler: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
            }
        }
        private void SendEmail(string message)
        {
            if(String.IsNullOrEmpty(_SubscribedEmail))
            {
                return;
            }
            try
            {
                using (MailMessage mail = new MailMessage())
                {
                    SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                    mail.From = new MailAddress("alfred@sequlite.com");
                    //mail.To.Add("xiang@sequlite.com");
                    if (_SubscribedEmail != null)
                    {
                        mail.To.Add(string.Format("{0}", _SubscribedEmail));
                    }
                    mail.Subject = string.Format($"[{ SettingsManager.ConfigSettings.CalibrationSettings.InstrumentInfo.InstrumentName}][Recipe:{_LoadedRecipe.RecipeName} Update]");
                    mail.Body = message;
                    lock (_RunninglogLock)
                    {
                        mail.Attachments.Add(new Attachment(_RecipeRunTimeStampedLogFullPathName));
                        SmtpServer.Port = 587;
                        //SmtpServer.Credentials = new System.Net.NetworkCredential("instrument@sequlite.com", "Sequlite6759!");
                        SmtpServer.Credentials = new System.Net.NetworkCredential("alfred@sequlite.com", "HighlySecure22");
                        SmtpServer.EnableSsl = true;

                        SmtpServer.Send(mail);
                        SmtpServer.Dispose();
                        //MessageBox.Show("mail Send");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
        private void UpdateRunningLog(string newLine)
        {
            if (!string.IsNullOrEmpty(newLine))
            {
                Logger.Log(newLine);
            }
            lock (_RunninglogLock)
            {
                
               // _RunningLog.Append(newLine + "\r\n");
                _WriteLog.Append(newLine + "\r\n");
                //RaisePropertyChanged(nameof(RunningLog));
            }

        }

        private bool CanExecuteRunRecipeCmd(object obj)
        {
            return _LoadedRecipe != null;
        }

        private bool MarkStep(StepsTreeViewModel tree, RecipeStepBase step)
        {
            if (tree.Content.Step == step)
            {
                tree.IsSelected = true;
                return true;
            }
            foreach (var subItem in tree.Children)
            {
                if (MarkStep(subItem, step))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion Run Recipe Command

        #region Pause Recipe Command
        private RelayCommand _PauseRecipeCmd;
        public ICommand PauseRecipeCmd
        {
            get
            {
                if (_PauseRecipeCmd == null)
                {
                    _PauseRecipeCmd = new RelayCommand(o => ExecutePauseRecipeCmd(o), o => CanExecutePauseRecipeCmd);  //new RelayCommand(ExecutePauseRecipeCmd, CanExecutePauseRecipeCmd);
                }
                return _PauseRecipeCmd;
            }
        }

        private void ExecutePauseRecipeCmd(object obj)
        {
            try
            {
                UpdateRunningLog(string.Format("[{0} Temp: {1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process pasued."));
                var msgResult = MessageBox.Show("Recipe Paused, continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (msgResult == MessageBoxResult.Yes)
                {
                    UpdateRunningLog(string.Format("[{0} Temp: {1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process continue."));
                }
                else if (msgResult == MessageBoxResult.No)
                {
                    _RecipeRunThread.Abort();
                    UpdateRunningLog(string.Format("[{0} Temp: {1}°] {2}", DateTime.Now.ToString("HH:mm:ss"), ChemistryVM.ChemiTemperGet, "Process is cancelled manually."));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in Execute Pause Recipe Command: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
            }
        }

        bool _CanExecutePauseRecipeCmd;
        public bool CanExecutePauseRecipeCmd
        {
            get
            {
                return _CanExecutePauseRecipeCmd;
            }
            set
            {
                if (_CanExecutePauseRecipeCmd != value)
                {
                    _CanExecutePauseRecipeCmd = value;
                    RaisePropertyChanged(nameof(CanExecutePauseRecipeCmd));
                    CommandManager.InvalidateRequerySuggested();
                }
            }

        }
        #endregion Pause Recipe Command

        #region Set Factor Command
        private RelayCommand _SetFactorCmd;
        public ICommand SetFactorCmd
        {
            get
            {
                if (_SetFactorCmd == null)
                {
                    _SetFactorCmd = new RelayCommand(ExecuteSetFactorCmd, CanExecuteSetFactorCmd);
                }
                return _SetFactorCmd;
            }
        }

        private void ExecuteSetFactorCmd(object obj)
        {
            try
            {
                string cmdPara = obj.ToString().ToLower();
                if (cmdPara == "exposure")
                {
                    _ExposureFactor = ExposureFactor;
                }
                else if (cmdPara == "greenled")
                {
                    _GreenLEDFactor = GreenLEDFactor;
                }
                else if (cmdPara == "redled")
                {
                    _RedLEDFactor = RedLEDFactor;
                }
                else if (cmdPara == "b_offset")
                {
                    _B_Offset = B_Offset;
                }
                else if (cmdPara == "t_offset")
                {
                    _T_Offset = T_Offset;
                }
                else if (cmdPara == "startinc")
                {
                    _StartInc = StartInc;
                }
                else if (cmdPara == "subscribedemail")
                {
                    _SubscribedEmail = SubscribedEmail;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in Execute Set Factor Command: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
            }
        }

        private bool CanExecuteSetFactorCmd(object obj)
        {
            return true;
        }
        #endregion Set Factor Command

        #region Plot Command
        private RelayCommand _PlotCmd;
        public ICommand PlotCmd
        {
            get
            {
                if (_PlotCmd == null)
                {
                    _PlotCmd = new RelayCommand(ExecutePlotCmd, CanExecutePlotCmd);
                }
                return _PlotCmd;
            }
        }

        private void ExecutePlotCmd(object obj)
        {
            try
            {
                if (_linegraph.Children.Count > 1)
                {
                    for (int i = _linegraph.Children.Count; i > 1; i--)
                        _linegraph.Children.RemoveAt(i - 1);
                }
                List<double> xave = new List<double>();
                List<double> yave = new List<double>();
                Dictionary<int, List<double>> xx = new Dictionary<int, List<double>>();
                Dictionary<int, List<double>> yy = new Dictionary<int, List<double>>();
                Color[] clrs = { Color.FromRgb(0, 255, 0), Color.FromRgb(0, 130, 0), Color.FromRgb(255, 0, 0), Color.FromRgb(130, 0, 0) };
                int maxCycle = 0;

                List<LineGraph> lg = new List<LineGraph>();

                for (int i = 0; i < 4; i++)
                {
                    lg.Add(new LineGraph());
                    _linegraph.Children.Add(lg[i]);
                    lg[i].Stroke = new SolidColorBrush(clrs[i]);
                    lg[i].Description = String.Format("Filter {0}", i);
                    lg[i].StrokeThickness = 1;
                    Dictionary<int, float> values2 = OLAStats.GetByCycle(_SelectedTile, i, _SelectedParameter);
                    xx[i] = new List<double>();
                    yy[i] = new List<double>();
                    maxCycle = values2.Keys.Count;
                    foreach (int cycle in values2.Keys)
                    {
                        xx[i].Add(cycle);
                        yy[i].Add(values2[cycle]);
                        if (i == 0)
                        {
                            xave.Add(cycle);
                            yave.Add(values2[cycle]);
                        }
                        else
                        {
                            yave[cycle - 1] += values2[cycle];
                        }
                    }


                }
                for (int i = 0; i < maxCycle; i++)
                {
                    yave[i] /= 4;
                }
                _linegraph.Plot(xave, yave);
                G1Data = yy[0];

                for (int i = 0; i < 4; i++)
                {
                    lg[i].Plot(xx[i], yy[i]);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Exception error in Execute Plot Command: {0}, Exception:\n{1}",
                    ex.Message, ex.StackTrace));
            }
        }
    

        private bool CanExecutePlotCmd(object obj)
        {
            return OLAStats != null; 
        }
        # endregion Plot Command

        public ILogDisplayFilter LogDisplayFilter { get; private set; }
        private LogViewerViewModel _LogViewerVM = null;
        public LogViewerViewModel LogViewerVM
        {
            get { return _LogViewerVM; }
            set
            {
                _LogViewerVM = value;
                if (value != null)
                {
                    LogDisplayFilter = value.LogDisplayFilter;
                }
                else
                {
                    LogDisplayFilter = null;
                }
                RaisePropertyChanged("LogViewerVM");
                RaisePropertyChanged("LogDisplayFilter");
            }
        }
        private void InitialLogWindowVM()
        {
            if (LogViewerVM == null)
            {
                LogViewerVM = new LogViewerViewModel(Logger) { HideMessageHeader = true, MaxNumberofMessagesBeforeRemove = 1000 };
                LogViewerVM.LogDisplayFilter?.AddSubSystemDisplayFilter(LoggerSubSystemName);
                //LogViewerVM.LogDisplayFilter?.AddSubSystemDisplayFilter(RecipeRunThreadBase.LoggerSubSystemName);
                //LogViewerVM.LogDisplayFilter?.AddSubSystemDisplayFilter(ImageProcessingCMD.LoggerSubSystemName);
            }
        }

    }
}
