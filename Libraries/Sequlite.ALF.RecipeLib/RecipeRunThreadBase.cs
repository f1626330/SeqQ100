using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.Image.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace Sequlite.ALF.RecipeLib
{
    public class ImageCycleChangedEventArgs : EventArgs
    {
        public int Cycle { get; set; }
        public ImagingStep Step { get; set; }
    }
    public class RecipeThreadParameters
    {
        // Offset between fluro and fiducial focus at bottom surface
        public double Bottom_Offset { get; set; }
        public double Top_Offset { get; set; }
        // different Basecall parameter for different library
        public TemplateOptions SelectedTemplate { get; set; }
        public TemplateOptions SelectedIndTemplate { get; set; } = TemplateOptions.idx;
        public bool IsSimulation { get; set; }
        public bool IsEnablePP { get; set; }
        public bool IsEnableOLA { get; set; }
        public bool IsBC { get; set; }
        //Oneref on for automapping FC regions
        public bool OneRef { get; set; }
        // Increase factor to accommodate decrease in fluro intensity
        public double GLEDinc { get; set; }
        public double RLEDinc { get; set; }
        public double Expoinc { get; set; }
        // Start cycle of this recipe, useful in restarting a exp.
        public int StartInc { get; set; }
        public string UserEmail { get; set; }
        public bool LoadCartridge { get; set; }
        public bool BackUpData { get; set; }
        public bool IsIndex { get; set; }
        public bool IsCalculateOffset { get; set; }
    }
    public abstract class RecipeRunThreadBase : ThreadBase
    {
        #region static fields
        //define all static fields here for easy  maintainers  -----------------------------------------------------------------
        //todo: need re-think if we really need "static ", static sometime causes problem
        //Keep constant/singularity  cross all inner/outter recipe
        protected static double B_Offset;
        protected static double T_Offset;
        //move this out of recipe thread  so that OLA can continue running after the recipe completes
        //protected static OLAJobManager OLAJobs = new OLAJobManager();// { get; set; }
        protected static string LogFile = "";
        protected static object _RecipeCopydatalock = new object();
        //protected static bool IsInnerRecipeRunning = false;
        protected static List<string> _Folderlist = new List<string>();
        protected static List<string> _NasFolderlist = new List<string>();
        protected static int NullImageCounts = 0;
        public static readonly string LoggerSubSystemName = "Recipe Thread";
        protected static readonly string logFileName = "tracking.txt";
        static protected string StartRunningMessage = "Starts Running";
        static public bool IsStartRunningMessage(string str) => str == StartRunningMessage;
        //----------------------------------------------------------------------------------------------------------
        #endregion static fields

        #region event
        public event Action<string> OnRecipeRunUpdated;
        public void OnRecipeRunUpdatedInvoke(string message) => OnRecipeRunUpdated?.Invoke(message);

        public delegate void StepRunStatusHandler(RecipeStepBase step, string msg, bool isError);
        public event StepRunStatusHandler OnStepRunUpdated;

        public void OnStepRunUpdatedInvoke(RecipeStepBase step, string msg, bool isError)
        {
            OnStepRunUpdated?.Invoke(step, msg, isError);
        }

        public delegate void LoopStepUpdateHandler(StepsTree tree);
        public event LoopStepUpdateHandler OnLoopStepUpdated;
        protected void OnLoopStepUpdatedInvoke(StepsTree steptree)
        {
            OnLoopStepUpdated?.Invoke(steptree);
        }
        #endregion event

        #region private fields
        protected OLAJobManager OLAJobs { get; }
        protected ISeqLog Logger = SeqLogFactory.GetSeqFileLog(LoggerSubSystemName);
        protected RecipeRunThreadBase _OutterRecipeThread;
        private Thread _MovefileThread;
        protected Dispatcher _CallingDispatcher;
        protected StepsTree _CurrentTree;
        protected string _NasRootFolder;
        public string DataBackupRootDir => _NasRootFolder;
        protected string _NasFolder;//= @"\\SEQULITENAS\Shared Computers\New Folder\";  image data back up folder
        protected RecipeRunSettings RecipeRunConfig { get; }
        protected IFluidics FluidicsInterface { get; }
        protected MotionController _MotionController;
        protected Mainboard _MainBoard;
        protected IPump _Pump;
        protected IValve _Valve;
        protected RecipeRunThreadBase _InnerRecipeRunThread;
        protected List<(string, string)> FailImageTranfer = new List<(string, string)>();
        protected BoolClass _abortFlag = null;
        protected AutoFocusCommandBase _AutoFocusProcess;
        protected AutoFocusCommandBase _AutoFocusFluoProcess;
        protected ImageChannelSettings _ImageSetting;
        protected Image.Processing.ImageInfo _ImageInfo = new Image.Processing.ImageInfo();
        protected bool _LoadCartridge;
        protected bool _IsAutoFocusingSucceeded;
        protected bool _IsFailedtoSetALED;
        protected bool _IsFailedCaptureAImage;
        protected bool _IsFailedtoSetLED;
        protected bool _isEnableOLA;
        protected bool _IsEnablePP;
        protected bool _IsBackUp;
        protected bool _IsIndex;
        protected bool _IsBadImage = false;
        protected bool _ledStateGet = false;
        protected double _FocusedSharpness;
        protected double _GLEDinc = 1;
        protected double _RLEDinc = 1;
        protected double _Expoinc = 1;
        protected int _FailedImage = 0;
        protected int _AutoFocustrycount;
        protected int BadImageCounts;
        protected int _PDValue;
        protected int _LEDFailure;
        protected string ErrorMessage;
        protected string _AutoFocusErrorMessage;
        protected string _ImageFileName;
        protected string _RecipeRunImageDataDir;
        protected string _RecipeRunWorkingDir;
        protected string _ExpName;
        protected int _loopCount;
        protected int _ImageCounts = 1;
        protected int _FilterFail = 0;
        protected RecipeThreadParameters _RecipeParameters;
        protected System.Timers.Timer _PDTimer;
        protected int _StartInc;
        protected double _CalculatedOffset;
        protected string[] fileArray;
        protected string _UserEmail;
        protected bool _IsBC;
        protected TemplateOptions _SelectedTemplate;
        protected bool _IsOffsetCalSucc;
        protected uint _OffsetGreenLEDInt;
        protected uint _OffsetRedLEDInt;
        protected double _OffsetGreenLEDExp;
        protected double _OffsetRedLEDExp;
        protected int WaitTimeThreshold;
        protected bool NeedWaitForOLAComplete { get; }
        protected bool _IsReCalculateOffset;
        private StringBuilder _ImageQC = new StringBuilder();
        protected string ImageQC
        {
            get { return _ImageQC.ToString(); }
        }
        #endregion private fields

        #region Public Properties
        public RecipeRunThreadBase OutterRecipeThread { get { return _OutterRecipeThread; } set => _OutterRecipeThread = value; }
        public string ExMessage { get; set; }
        public bool IsInnerRecipeRunning
        {
            get { return (_OutterRecipeThread != null); }
        }
        public Recipe Recipe { get; }
        public RecipeThreadParameters RecipeParameters { get => _RecipeParameters; set => _RecipeParameters = value; }
        public bool IsEnablePP { get => _IsEnablePP; set => _IsEnablePP = value; }

        //use this class object to share abort flag with inner recipe run thread, but processor and OLAJobs have their own abort flags, 
        //don't share them since there may have use cases which just cancel OLA or one of image processing CMD but
        //keep running recipe.
        public bool IsAbort
        {
            get { return _abortFlag.BoolValue; }
            set
            {
                _abortFlag.BoolValue = value;
                if (value)
                {
                    OLAJobs?.Stop();
                }
            }
        }
        #endregion Public Properties

        #region Constructor
        protected RecipeRunThreadBase(Dispatcher callingDispatcher,
            RecipeRunSettings _recipeRunConfig,
            Recipe recipe,
            MotionController motionController,
            Mainboard mainBoard,
            IFluidics fluidics,
            //BoolClass abortFlag,
            RecipeThreadParameters recipeparam,
            RecipeRunThreadBase outterThread,
            OLAJobManager olaJob,
            bool waitForOLAComplete  //wait OLA done inside recipe thread -- if true
            )
        {
            OLAJobs = olaJob;
            NeedWaitForOLAComplete = waitForOLAComplete;
            _OutterRecipeThread = outterThread;
            if (outterThread?._abortFlag == null)
            //if (abortFlag == null)
            {
                _abortFlag = new BoolClass();
            }
            else
            {
                _abortFlag = outterThread._abortFlag;// abortFlag;
            }
            _CallingDispatcher = callingDispatcher;
            RecipeRunConfig = _recipeRunConfig;
            Recipe = recipe;
            _MotionController = motionController;
            _MainBoard = mainBoard;
            FluidicsInterface = fluidics;
            _Pump = fluidics.Pump;
            _Valve = fluidics.Valve;
            _RecipeParameters = recipeparam;

            //Load folder directory 
            _ExpName = Recipe.RecipeName;
            string Datasubfolder = "Read1";
            Match match = Regex.Match(Recipe.RecipeName, string.Format(@"_(Read1|Index1|Index2|Read2)"), RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string matchString = match.ToString();
                Debug.Assert(_ExpName.EndsWith(matchString)); // we are assuming that the experiment name ends with "_(Read1|Index1|Index2|Read2)"
                Datasubfolder = matchString.Split('_')[1];
                _ExpName = _ExpName.Substring(0, _ExpName.Length - Datasubfolder.Length - 1); // -1 to account for the '_' before (Read1|Index1|Index2|Read2)
            }
            //Datasubfolder is one of Read1|Index1|Index2|Read2
            _NasRootFolder = RecipeRunConfig.GetRecipeRunBackupDir(_ExpName);
            _NasFolder = RecipeRunConfig.GetRecipeRunImageBackupDir(_ExpName, Datasubfolder); //root + "//Data//"
            if (!_NasFolderlist.Contains(_NasFolder)) { _NasFolderlist.Add(_NasFolder); }
            _RecipeRunImageDataDir = RecipeRunConfig.GetRecipeRunImageDataDir(_ExpName, Datasubfolder);
            _RecipeRunWorkingDir = RecipeRunConfig.GetRecipeRunWorkingDir(_ExpName);
            if (!_Folderlist.Contains(_RecipeRunImageDataDir)) { _Folderlist.Add(_RecipeRunImageDataDir); }

            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            FileVersionInfo fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            _ImageInfo.SoftwareVersion = fileVersionInfo.ProductVersion;
            _ImageInfo.InstrumentModel = SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName;
        }
        #endregion Constructor
        
        protected void _PDTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _PDValue = 0;
            _MainBoard.GetPDValue();
            _PDValue = (int)_MainBoard.PDValue;
            _ImageInfo.MixChannel.PDValue = _PDValue;
        }

        protected void CopytoNas(RecipeStepBase step, string filename, string destpath)
        {
            lock (_RecipeCopydatalock)
            {
                try
                {
                    File.Copy(filename, destpath, true);
                    FindQCFromImage QC = new FindQCFromImage(filename);
                }
                catch (Exception ex)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Failed to copy image file to Nas, retry: {0}, Exception:{1}", Path.GetFileName(filename), ex.ToString()), false);
                    try
                    {
                        File.Copy(filename, destpath, true);
                        FindQCFromImage QC = new FindQCFromImage(filename);
                    }
                    catch (Exception nex)
                    {
                        FailImageTranfer.Add((filename, destpath));
                        OnStepRunUpdatedInvoke(step, string.Format("Failed to copy image file to Nas, added to list : {0}, Exception:{1}", Path.GetFileName(filename), nex.ToString()), false);
                    }
                }
            }

        }

        protected void _InnerRecipeRunThread_OnLoopStepUpdated(StepsTree steptree)
        {
            OnLoopStepUpdatedInvoke(steptree);
        }

        protected void _InnerRecipeRunThread_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            _InnerRecipeRunThread.OnRecipeRunUpdated -= _InnerRecipeRunThread_OnRecipeRunUpdated;
            _InnerRecipeRunThread.OnStepRunUpdated -= _InnerRecipeRunThread_OnStepRunUpdated;
            _InnerRecipeRunThread.OnLoopStepUpdated -= _InnerRecipeRunThread_OnLoopStepUpdated;
            _InnerRecipeRunThread.Completed -= _InnerRecipeRunThread_Completed;
            _InnerRecipeRunThread = null;
        }
        protected void _InnerRecipeRunThread_OnRecipeRunUpdated(string message)=>OnRecipeRunUpdated?.Invoke(message);
        protected void _InnerRecipeRunThread_OnStepRunUpdated(RecipeStepBase step, string msg, bool isError)
        {
            OnStepRunUpdatedInvoke(step, msg, isError);
        }
        protected void RunStepProc(WaitingStep step)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var startTimeSpan = TimeSpan.FromMilliseconds(0);
            var periodTimeSpan = TimeSpan.FromSeconds(30);
            using (var timer = new System.Threading.Timer(e => OnStepRunUpdatedInvoke(step, String.Format("Thread:{1}: {0}", "Waiting", Thread.CurrentThread.Name), false), null, startTimeSpan, periodTimeSpan))
            {
                if (step.Time >= WaitTimeThreshold && FluidicsInterface.Pump.PumpAbsolutePos >= 50 * SettingsManager.ConfigSettings.PumpIncToVolFactor
                    && step.ResetPump)
                {
                    OnStepRunUpdatedInvoke(step, "Reset Pump", false);
                    PumpingSettings _PumpSetting = new PumpingSettings();
                    _PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
                    _PumpSetting.PumpingVolume = FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor;
                    _PumpSetting.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                    _PumpSetting.SelectedMode = ModeOptions.Push;
                    _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = FluidicsInterface.Valve.CurrentPos };
                    if (step.Time >= 15) { _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = 24 }; }
                    _PumpSetting.SelectedPushPath = PathOptions.Waste;
                    for (int i = 0; i < 4; i++)
                    {
                        _PumpSetting.PumpPushingPaths[i] = false;
                    }
                    _PumpSetting.SelectedPushValve2Pos = 6;
                    _PumpSetting.SelectedPushValve3Pos = 1;
                    FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, _PumpSetting, true, IsSimulationMode);
                }
                while (stopwatch.ElapsedMilliseconds < step.Time * 1000 && !IsSimulationMode)
                {
                    if (IsAbort)
                    {
                        OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                        break;
                    }
                    Thread.Sleep(1); // we are close now, so check frequently
                }
                stopwatch.Stop();
                startTimeSpan = TimeSpan.FromMilliseconds(-1);
                //Thread.Sleep(50); // KBH removed - not sure of the purpose of an extra 50 ms sleep
            }
        }

        protected void RunStepProc(CommentStep step)
        {

        }
        protected string CheckInnerRecipePath(RunRecipeStep step)
        {
            string recipePath = step.RecipePath;
            ////if (!File.Exists(recipePath))
            //{
            //    if (string.IsNullOrEmpty(Path.GetDirectoryName(recipePath)))
            //    {
            //        Logger.Log($"Inner recipe filename: {recipePath} doesn't have directory, try to use parent recipe location");
            //        //using parent recipe path
            //        bool findDir = false;
            //        if (!string.IsNullOrEmpty(this.Recipe?.RecipeFileLocation))
            //        {
            //            string dir = Path.GetDirectoryName(this.Recipe.RecipeFileLocation);
            //            {
            //                recipePath = Path.Combine(dir, recipePath);
            //                findDir = true;
            //            }
            //        }

            //        if (!findDir)
            //        {
            //            throw new Exception($"Not able find path for recipe {step.RecipePath}");
            //        }
            //    }
            //}
            return recipePath;
        }

        #region Online Image Analysis
        protected void RunOLAJobManager(int loopCount, ImagingStep step, bool isLastCycle)
        {
            if (_isEnableOLA || IsEnablePP)
                OLAJobs?.UpdateImagingCycle(loopCount, step, isLastCycle);
        }

        protected void UpdateTileOLAJobManager(ImagingStep step, int cycle, string tile, bool init)
        {
            if (_isEnableOLA || IsEnablePP)
            {
                if (init)
                    OLAJobs?.Start(RecipeRunConfig.GetRecipeRunWorkingDir(_ExpName));
                
                OLAJobs?.UpdateImagingCycleEx(step, cycle, tile);
            }
        }

        protected void WaitForOLAComplete()
        {
            try
            {
                if (OLAJobs != null && (NeedWaitForOLAComplete || IsAbort))
                {
                    if (!IsInnerRecipeRunning)
                    {
                        if (IsAbort)
                        {
                            Logger.LogMessage("Aborting OLA job from Recipe Run Thread");
                            OLAJobs.Stop();
                        }
                        OLAJobs.WaitForAllDone();
                    }
                }
                else
                {
                    if (OLAJobs != null && !IsAbort)
                    {
                        Logger.Log($"NeedWaitForOLAComplete = {NeedWaitForOLAComplete}, OLA completion will waited outside this recipe run.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("failed to wait for OLA job done with exception error: " + ex.Message);
            }
        }
        #endregion Online Image Analysis
        public bool ResetLoopStepCounts(StepsTree tree)
        {
            if (tree.Step is LoopStep)
            {
                ((LoopStep)tree.Step).LoopCounts = 0;
                OnLoopStepUpdatedInvoke(tree);
                foreach (var item in tree.Children)
                {
                    ResetLoopStepCounts(item);
                }
                return true;
            }
            else { return false; }
        }
        #region virtual function
        public virtual void RunStepProc(StopTemperStep step)
        {
            _MainBoard.SetChemiTemperCtrlStatus(false);
        }
        virtual protected void BackupImage(ImagingStep step, string imageFileName, string destfolder)
        {
            string destpath = System.IO.Path.Combine(destfolder, Path.GetFileName(imageFileName));
            _MovefileThread = new Thread(() => CopytoNas(step, imageFileName, destpath));
            _MovefileThread.Start();
        }

        virtual protected void WaitForBackingupImageComplete()
        {
            if (_MovefileThread != null && !IsInnerRecipeRunning && _IsBackUp) { if (_MovefileThread.IsAlive) { _MovefileThread.Join(); } }
        }
        #endregion virtual function


    }
}
