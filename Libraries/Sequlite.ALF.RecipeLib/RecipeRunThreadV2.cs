using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.Imaging;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using Sequlite.Image.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Sequlite.ALF.Common.SystemCalibJson;

namespace Sequlite.ALF.RecipeLib
{
    #region Function/Object maintain info and data that shared cross threads(inner recipes) 
    //Object carry data between different threads: recipe/camera
    public class AcquiredImageData
    {
        public ILucidCamera Camera { get; set; }
        public int CameraIndex { get; set; }
        public ImagingStep Step { get; set; }
        public byte[] ImageDataArray { get; set; }
        public Image.Processing.ImageInfo Imageinfo { get; set; }
        public int ImageId { get; set; }
        public string ImageName { get; set; }
        public string BackupPath { get; set; }
        public int TryCounts { get; set; }
        public int LoopCount { get; set; }

        public int LEDFailure { get; set; }
        public int BadImageCounts { get; set; }
        public int NullImageCounts { get; set; }
        public string RecipeRunImageDataDir { get; set; }
        public int PDValue { get; set; }
        public string Locationinfo { get; set; }
        public int RegionIndex { get; set; }//< the index of region (tile) where this image was acquired
        public AcquiredImageData()
        {
        }
    }
    //Help track the state of cameras
    public class ImagingStates
    {
        //public bool GotImage { get; set; }
        //public byte[] ImageDataArray { get; set; }
        public bool IsCameraExposureEnd { get; set; }
        public bool IsExposureSuccess { get; set; }
        public int QueueImageTaskCount { get; set; }

        //public int ImageId { get; set; } //for tracking purpose
        public Dictionary<int, byte[]> ImageListByID;
        public int CameraIndex { get; set; }
        public ILucidCamera Camera { get; set; }
        public ImagingStates()
        {
            //GotImage = true;
            ImageListByID = new Dictionary<int, byte[]>();
        }
    }
    //Object share loop count cross differnet thread
    public class LoopCounts
    {
        int _Counts;
        public int Counts
        {
            get
            {
                lock (this)
                {
                    return _Counts;
                }
            }
            set
            {
                lock (this)
                {
                    _Counts = value;
                }
            }
        }
        public LoopCounts()
        {
            _Counts = -1;
        }
    }
    //Event Args that pass info about 
    public class ImageSavedEventArgs : EventArgs
    {
        public RecipeStepBase Step { get; set; }
        public string Message { get; set; }
        public int ImageCurrentLoopCount { get; set; }
        public string ImageFile { get; set; }
    }
    //Object that maintain the threads for saving / backup image data and camera states
    public class Imaging
    {
        public event ImageSavedHandler OnImageSaved;
        public ImagingStates ImagingState1 { get; }
        public ImagingStates ImagingState2 { get; }
        public Thread ImageQProcessingThread { get; private set; }
        public AutoSetQueue<AcquiredImageData> ImageQ { get; private set; }

        private int _ImageId;
        public SequenceDataBackup ImageBackingup { get; set; }
        public bool NeedWaitForImageBackupComplete { get; set; }
        public ISeqLog Logger { get; set; }

        public Dictionary<RegionIndex, Dictionary<double, List<double>>> NFPatternWLocations = new Dictionary<RegionIndex, Dictionary<double, List<double>>>();

        //public double Threshold;
        public Imaging(ILucidCamera camera1, ILucidCamera camera2)
        {
            _ImageId = 0;
            ImagingState1 = new ImagingStates() { CameraIndex = 1, Camera = camera1 };
            ImagingState2 = new ImagingStates() { CameraIndex = 2, Camera = camera2 };

        }
        public int GetNextImageId()
        {
            return Interlocked.Increment(ref _ImageId);
        }

        public void StartImageQProcessingThread(Action threadFunction)
        {
            ImageQ = new AutoSetQueue<AcquiredImageData>();
            ImageQProcessingThread = new Thread(() => threadFunction());
            ImageQProcessingThread.Name = "ImageSaving";
            ImageQProcessingThread.IsBackground = true;
            ImageQProcessingThread.Start();
        }

        public void StartImageBackupQProcessingThread() =>
            ImageBackingup?.StartBackupQProcessingThread();

        public void WaitForSavingImagesComplete()
        {

            if (ImageQProcessingThread?.IsAlive == true)
            {
                ImageQProcessingThread.Join();
            }
            ImageQProcessingThread = null;
        }

        public void WaitForBackingupImageComplete()
        {
            if (NeedWaitForImageBackupComplete)
            {
                ImageBackingup?.WaitForBackingupComplete();
            }
            else
            {
                Logger.LogMessage($"NeedWaitForImageBackupComplete= {NeedWaitForImageBackupComplete}, Data backup will be waited to complete outside this recipe run");
            }
        }

        public void OnImageSavedInvoke(ImageSavedEventArgs args)
        {
            OnImageSaved?.Invoke(args);
        }
    }
    #endregion Function/Object maintain info and data that shared cross threads(inner recipes) 

    public delegate void ImageSavedHandler(ImageSavedEventArgs args);

    /// <summary>
    /// Contains methods used to execute a recipe in a seperate thread other than EUI base thread, 
    /// which create more threads running in parallel. Several threads are spawned that
    /// must share data with each other during the recipe process.
    /// </summary>
    public class RecipeRunThreadV2 : RecipeRunThreadBase
    {
        #region static fields
        //define all static fields here for easy  maintainers  -----------------------------------------------------------------
        //todo: need re-think if we really need "static ", static sometime causes problem
        private static bool _IsOneRef;
        //this may have problem because it is onetime assigned, if _IsOneRef later it won't get it new value,
        //unless _IsOneRef is readonly
        //so shall we change it to  something like this??   =>  !_IsOneRef
        private static bool _ScanEveryRegion = !_IsOneRef; // AF every region? 
        private static bool _IsAFing;
        private static int AFColumninterval = 5; // 4*38 regions in one lane
        private static int _ExpoControlFactor; //Dynamic change factor accroding to realtime intensity
        private static int _CycleTotalImageCount;
        private static double TopSharpness = SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL; //
        private static double BottomSharpness = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL; //Sharpness lower limit at bottom surface
        private static bool IsTopShpThresholdChange;
        private static bool IsBomShpThresholdChange;
        private static double SurfaceMulitplier = 1; //factor between top and bottom surface, increase intensity

        private static bool _processImageQueueBusy = false; //< a flag to track if the image processing queue is busy
        private static readonly object _processImageBusyLocker = new object(); //< a object to use for locking _processImageQueueBusy flag
        private static readonly object _imageQueueHasItemsLocker = new object(); //< a object to use for locking before checking if the ImageQ has items
        //--------------------------------------------------------------------------------------------------------------------
        #endregion static fields

        #region events
        public event ImageSavedHandler OnImageSaved
        {
            add
            {
                if (_Imaging != null)
                {
                    _Imaging.OnImageSaved += value;
                }
            }
            remove
            {
                if (_Imaging != null)
                {
                    _Imaging.OnImageSaved -= value;
                }
            }
        }
        public void OnImageSavedInvoke(ImageSavedEventArgs args) => _Imaging?.OnImageSavedInvoke(args);
        #endregion events

        #region Private fields
        private LEDController _LEDController;
        private ILucidCamera _camera1;
        private ILucidCamera _camera2;
        private double[] _IntThreshold = new double[2] { 1500, 3000 };
        readonly int MaxImageQSize = 50;
        readonly int MinPDValue = 100;
        //readonly double NFiducialTh = 30000;
        public LoopCounts CurrentImageLoopCount { get; set; }

        private AutoSetQueue<AcquiredImageData> ImageQ { get => _Imaging.ImageQ; }

        private TemperatureController _FCTemperControllerRev2;

        // Used to apply affine transformations to images before writing data to disk
        private ImageTransformer _imageTransformer;

        // A rectangle to hold the final width and height of the output (transformed) image
        // Note: the x and y coordinates are not used and set to 0,0
        private Int32Rect _transformedImageRect;


        private Task Camera1Task;
        private Task Camera2Task;
        private long Camera1TaskCount;
        private long Camera2TaskCount;

        private bool CheckAFByFluor = false;
        private string _TestAFImagePath;

        private ImagingStates ImagingState1 { get => _Imaging.ImagingState1; }
        private ImagingStates ImagingState2 { get => _Imaging.ImagingState2; }
        private int GetNextImageId() => _Imaging.GetNextImageId();
        private Imaging _Imaging;
        private Dictionary<string, bool> _StopOnFailureAnswers;
        private string _surface; // "t" for top surface or "b" for bottom surface

        private AutoFocusTiltFid _AutoFocusNFProcess;
        private Dictionary<double, List<double>> _FocusPattern = new Dictionary<double, List<double>>();
        private double _Threshold;
        private Dictionary<RegionIndex, Dictionary<double, List<double>>> NFPatternWLocations { get => _Imaging.NFPatternWLocations; }
        SequenceDataBackup DataBackup { get; }
        #endregion Private fields

        #region Constructor
        public RecipeRunThreadV2(
            Dispatcher callingDispatcher,
            RecipeRunSettings _recipeRunConfig,
            Recipe recipe,
            ILucidCamera camera1,
            ILucidCamera camera2,
            MotionController motionController,
            Mainboard mainBoard,
            LEDController ledcontroller,
            IFluidics fluidics,
            RecipeThreadParameters recipeparam,
            RecipeRunThreadV2 outterThread,
            OLAJobManager olaJob,
            bool waitForOLAComplete,  //wait OLA done inside recipe thread -- if true
            SequenceDataBackup dataBackup,
            bool waitForImageBackupComplete
            ) :
            base(callingDispatcher, _recipeRunConfig, recipe, motionController, mainBoard, fluidics, recipeparam, outterThread, olaJob, waitForOLAComplete)
        {

            DataBackup = dataBackup;
            _camera1 = camera1;
            _camera2 = camera2;
            _LEDController = ledcontroller;
            _LoadCartridge = recipeparam.LoadCartridge;
            _isEnableOLA = recipeparam.IsEnableOLA;
            _Expoinc = recipeparam.Expoinc;
            _RLEDinc = recipeparam.RLEDinc;
            _GLEDinc = recipeparam.GLEDinc;
            B_Offset = recipeparam.Bottom_Offset;
            T_Offset = recipeparam.Top_Offset;
            _StartInc = recipeparam.StartInc;
            _UserEmail = recipeparam.UserEmail;
            _IsBC = recipeparam.IsBC;
            _SelectedTemplate = recipeparam.SelectedTemplate;
            _IsOneRef = recipeparam.OneRef;
            _IsBackUp = recipeparam.BackUpData;
            _ScanEveryRegion = !_IsOneRef;
            WaitTimeThreshold = 5;
            _IsIndex = recipeparam.IsIndex;
            _IsReCalculateOffset = recipeparam.IsCalculateOffset;
            _FCTemperControllerRev2 = TemperatureController.GetInstance();
            NullImageCounts = 0; // Allow 30 incomplete data pre cycle at maximum
            #region image transform
            bool isAllZeros = true;
            foreach (string key in SettingsManager.ConfigSettings.CalibrationSettings.ImageTransforms.Keys)
            {
                ImageTransformConfig c = SettingsManager.ConfigSettings.CalibrationSettings.ImageTransforms[key];
                if (c.WidthAdjust != 0 || c.HeightAdjust != 0 || c.XOffset != 0 || c.YOffset != 0)
                {
                    isAllZeros = false;
                }
            }
            if (!isAllZeros)
            {
                // access the image transformation via a singleton pattern
                _imageTransformer = ImageTransformer.GetImageTransformer();

                // the initial width and height of the images (units = [px])
                int w, h;
                // check the config file to see if CameraDefaultSettings is cropping the image: 
                if (SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth > 0 && SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight > 0)
                {
                    w = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth;
                    h = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight;
                }
                else
                {
                    // default to the full sensor resolution
                    w = 4416;
                    h = 4428;
                }

                // stupid dictionary copying because the type definitions are causing circular dependencies...
                Dictionary<string, ImageTransformConfig> configParameters = SettingsManager.ConfigSettings.CalibrationSettings.ImageTransforms;
                Dictionary<string, ImageTransformParameters> parameters = new Dictionary<string, ImageTransformParameters>();
                foreach (string key in configParameters.Keys)
                {
                    ImageTransformConfig c = configParameters[key];
                    ImageTransformParameters p = new ImageTransformParameters();
                    p.WidthAdjust = c.WidthAdjust;
                    p.HeightAdjust = c.HeightAdjust;
                    p.XOffset = c.XOffset;
                    p.YOffset = c.YOffset;
                    parameters.Add(key, p);
                }

                // the image transformer returns an empty rectangle if parameters are invalid
                Int32Rect transformedImageRect = _imageTransformer.Initialize(w, h, parameters);
                if (transformedImageRect.IsEmpty)
                {
                    string msg = $"Failed to initialize image transformer. Invalid parameters";
                    Logger.LogError(msg);
                }
                else
                {
                    string msg = $"Image transformer initialized";
                    Logger.Log(msg);
                }
                _transformedImageRect = new Int32Rect(0, 0, transformedImageRect.Width, transformedImageRect.Height);
            }
            #endregion image transform

            //shared object among inner recipes -------------------------------------------------------
            if (outterThread?.CurrentImageLoopCount == null)
            {
                CurrentImageLoopCount = new LoopCounts();
            }
            else
            {
                CurrentImageLoopCount = outterThread?.CurrentImageLoopCount;
            }

            if (outterThread?._Imaging == null)
            {
                _Imaging = new Imaging(_camera1, _camera2)
                {
                    ImageBackingup = dataBackup,
                    NeedWaitForImageBackupComplete = waitForImageBackupComplete,
                    Logger = this.Logger,
                };
            }
            else
            {
                _Imaging = outterThread?._Imaging;
            }

            if (outterThread?._StopOnFailureAnswers == null)
            {
                _StopOnFailureAnswers = new Dictionary<string, bool>();
            }
            else
            {
                _StopOnFailureAnswers = outterThread._StopOnFailureAnswers;
            }
            if (!IsInnerRecipeRunning)
            {
                LucidCameraManager.OnCameraUpdated += LucidCameraManager_OnCameraUpdated;
            }
            //---------------------------------------------------------------------------------------------

            Logger.LogMessage("Recipe Thread Created");
            Logger.LogMessage(string.Format("ExpoInc:{0}, GLED:{1}, RLED:{2}, B_Offset:{3}, T_Offset:{4}, BC:{5}, Template:{6}, OneRef:{7},IsEnableOLA:{8}, IsEnablePP:{9}",
                _Expoinc, _GLEDinc, _RLEDinc, B_Offset,
                T_Offset, _IsBC, _SelectedTemplate.ToString(), _IsOneRef, _isEnableOLA, _IsEnablePP));

        }
        #endregion Constructor
        public override void ThreadFunction()
        {
            try
            {
                if (_LoadCartridge)
                {
                    if (!_MotionController.IsCartridgeAvailable)
                    {
                        if (!IsSimulationMode)
                        {
                            //Chiller.GetInstance().ChillerMotorControl(true);
                            Chiller.GetInstance().SetChillerMotorAbsMove(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos);
                            int trycount = 0;
                            do
                            {
                                if (trycount++ > 140)
                                {
                                    Logger.LogError("Failed to Move Cartridge, waiting expire.");
                                    throw new Exception("Cartridge movement failed");
                                }
                                Thread.Sleep(1000);
                                Chiller.GetInstance().GetChillerMotorPos();
                            }
                            while (!Chiller.GetInstance().CheckCartridgeSippersReagentPos());
                        }
                    }
                    else
                    {
                        int tgtPos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                        if (_MotionController.CCurrentPos != tgtPos)
                        {
                            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                            // do not do absolute moves if in simulation
                            if (!IsSimulationMode)
                                _MotionController.AbsoluteMove(MotionTypes.Cartridge, tgtPos, speed, accel, true);
                        }

                    }
                }
                //Run Steps
                foreach (var item in Recipe.Steps)
                {
                    if (IsAbort)
                    {
                        OnStepRunUpdatedInvoke(item.Step, "Recipe Abort", true);
                        return;
                    }
                    RunStepTree(item);
                }
                //Add last null object to image queue after outter recipe finished //TODO: document: why?
                if (!IsInnerRecipeRunning)
                {
                    //Reset control factor
                    _ExpoControlFactor = 0;
                    //add last null image to q
                    do
                    {

                        lock (ImagingState1)
                        {
                            if (ImagingState1.QueueImageTaskCount <= 0)
                            {
                                lock (ImagingState2)
                                {
                                    if (ImagingState2.QueueImageTaskCount <= 0)
                                    {
                                        Logger.LogMessage($"Added last null image to the image queue. Current QSize={ImageQ?.QueueCount}");
                                        ImageQ?.Enqueue(null);
                                        break;
                                    }
                                }
                            }
                        }
                        Thread.Sleep(50);
                    }
                    while (true);
                }
                //Wait all image saved
                if (!IsInnerRecipeRunning)
                {
                    WaitForSavingImagesComplete();
                    Logger.LogMessage($"All image data in image queue has been saved");
                }
                //Makes sure camera task is empty
                long camera1TaskCount = Interlocked.Read(ref Camera1TaskCount);
                if (camera1TaskCount > 0)
                {
                    Logger.LogWarning($"Not all Camera1Tasks have been completed. Task count:{camera1TaskCount}");
                }
                long camera2TaskCount = Interlocked.Read(ref Camera2TaskCount);
                if (camera2TaskCount > 0)
                {
                    Logger.LogWarning($"Not all Camera2Tasks have been completed. Task count:{camera2TaskCount}");
                }

                //Transfer Txt file
                if (!IsInnerRecipeRunning && !IsSimulationMode && _IsBackUp)
                {
                    try //should not stop exp or OLA if error in tranfering files
                    {
                        Logger.LogMessage("Transferring file: list.txt and info.json...");
                        for (int i = 0; i < _Folderlist.Count; i++)
                        {
                            if (File.Exists(Path.Combine(_Folderlist[i], "list.txt")))
                            {
                                string destdir = Path.Combine(Directory.GetParent(_NasFolderlist[i].TrimEnd(Path.DirectorySeparatorChar)).FullName, "list.txt");
                                File.Copy(Path.Combine(_Folderlist[i], "list.txt"), destdir, true);
                                Logger.LogMessage($"Finished transfering list.txt file: {_Folderlist[i]} to {destdir}");
                                
                                //Copy to Nas Task folder
                                DirectoryInfo dir = new DirectoryInfo(_Folderlist[i]);
                                string readname = dir.Parent.Name;
                                string expname = dir.Parent.Parent.Name;
                                string taskfilename = expname + "_" + readname + "_" + "Data" + ".txt";
                                DirectoryInfo Nasdir = new DirectoryInfo(_NasFolderlist[i]);
                                string nasrootfolder = Nasdir.Root.ToString();
                                string[] folders = nasrootfolder.Split('\\');

                                string taskpath = "\\\\" + folders[2] + "\\" + "Tasks";

                                taskpath = Path.Combine(taskpath, taskfilename);
                                File.Copy(Path.Combine(_Folderlist[i], "list.txt"), taskpath, true);
                                Logger.LogMessage($"Finished transfering list.txt file: {_Folderlist[i]} to {taskpath}");
                                //make a copy to Image\expname\
                                destdir = RecipeRunConfig.GetRecipeRunWorkingDir(expname);
                                destdir = Path.Combine(destdir, taskfilename);
                                File.Copy(Path.Combine(_Folderlist[i], "list.txt"), destdir, true);
                                Logger.LogMessage($"Finished transfering list.txt file: {_Folderlist[i]} to {destdir}");
                            }
                            string infojsonfolder = Directory.GetParent(Directory.GetParent(_Folderlist[i].TrimEnd(Path.DirectorySeparatorChar)).FullName).FullName;
                            string nasinfofolder = Directory.GetParent(Directory.GetParent(_NasFolderlist[i].TrimEnd(Path.DirectorySeparatorChar)).FullName).FullName;
                            if (File.Exists(Path.Combine(infojsonfolder, "info.json")))
                            {
                                File.Copy(Path.Combine(infojsonfolder, "info.json"), Path.Combine(nasinfofolder, "info.json"), true);
                                Logger.LogMessage($"Finished transferring info.json file:{ _Folderlist[i]}");
                            }
                        }
                        _NasFolderlist.Clear();
                        _Folderlist.Clear();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Logger.LogWarning("Recipe Thread Aborted");
                OLAJobs?.Stop();
            }
            catch (Exception ex)
            {
                ExMessage = ex.ToString();

                Logger.LogError(ExMessage);

                InformError(ExMessage);

                ExitStat = ThreadExitStat.Error;
                OLAJobs?.Stop();
            }
            finally
            {
                WaitForOLAComplete();

                if (!IsInnerRecipeRunning)
                {
                    Logger.Log("Turning off temperature control");
                    _FCTemperControllerRev2.SetControlSwitch(false);
                    Logger.Log("Temperature control is turned off");
                    SetFluidPreHeatingEnable(false);
                    Logger.Log("Turn off PreHeating");
                    TopSharpness = SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL;
                    BottomSharpness = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL;
                    IsTopShpThresholdChange = false;
                    IsBomShpThresholdChange = false;
                    Logger.Log("Reset sharpness threshold");
                    LucidCameraManager.OnCameraUpdated -= LucidCameraManager_OnCameraUpdated;
                }
            }

            Logger.Log($"Run thread:{this.Name} exits");
            Logger.Log($"The recipe has completed with {Logger.WarningCount} warnings and {Logger.ErrorCount} errors");
        }
        int currentRecipeRunedSetepCount = 0;
        private void RunStepTree(StepsTree tree)
        {
            Stopwatch sw = Stopwatch.StartNew();
            OnStepRunUpdatedInvoke(tree.Step, StartRunningMessage, false);
            _CurrentTree = tree;
            switch (tree.Step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    RunStepProc((SetTemperStep)tree.Step);
                    Logger.Log($"Set chemistry temperature recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    break;
                case RecipeStepTypes.StopTemper:
                    RunStepProc((StopTemperStep)tree.Step);
                    Logger.Log($"Disable temperature recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    RunStepProc((SetPreHeatTempStep)tree.Step);
                    Logger.Log($"Set pre heat temperature recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    break;
                case RecipeStepTypes.StopPreHeating:
                    RunStepProc((StopPreHeatingStep)tree.Step);
                    Logger.Log($"Disable pre heat temperature recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    break;
                case RecipeStepTypes.Imaging:
                    RunStepProc((ImagingStep)tree.Step);
                    Logger.Log($"Imaging recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    break;
                case RecipeStepTypes.Loop:
                    ((LoopStep)tree.Step).LoopCounts = 1;
                    OnLoopStepUpdatedInvoke(tree);
                    for (int i = 0; i < ((LoopStep)tree.Step).LoopCycles; i++)
                    {
                        OnStepRunUpdatedInvoke(tree.Step, string.Format("Loop: {0}", i + 1), false);
                        foreach (var item in tree.Children)
                        {
                            if (IsAbort)
                            {
                                OnStepRunUpdatedInvoke(item.Step, "Recipe Abort", true);
                                return;
                            }
                            RunStepTree(item);
                        }
                        if (i < ((LoopStep)tree.Step).LoopCycles - 1)
                        {
                            ((LoopStep)tree.Step).LoopCounts++;
                        }
                        OnLoopStepUpdatedInvoke(tree);
                    }
                    ((LoopStep)tree.Step).LoopCounts = 0;
                    OnLoopStepUpdatedInvoke(tree);
                    break;
                case RecipeStepTypes.RunRecipe:
                    RunStepProc((RunRecipeStep)tree.Step);
                    break;
                case RecipeStepTypes.Waiting:
                    RunStepProc((WaitingStep)tree.Step);
                    Logger.Log($"Waiting recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.NewPumping:
                    RunStepProc((NewPumpingStep)tree.Step);
                    Logger.Log($"New pumping recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.HomeMotion:
                    RunStepProc((HomeMotionStep)tree.Step);
                    Logger.Log($"Home motion recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.AbsoluteMove:
                    RunStepProc((AbsoluteMoveStep)tree.Step);
                    Logger.Log($"Absolute move recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.RelativeMove:
                    RunStepProc((RelativeMoveStep)tree.Step);
                    Logger.Log($"Relative move recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.HywireImaging:
                    RunStepProc((HywireImagingStep)tree.Step);
                    Logger.Log($"Hywire Imaging recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
                case RecipeStepTypes.LEDCtrl:
                    RunStepProc((LEDControlStep)tree.Step);
                    Logger.Log($"LED control recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);

                    break;
            }
            Logger.Log($"Recipe step elapsed time [ms]|{sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
            OnStepRunUpdatedInvoke(tree.Step, $"Recipe step {tree.Step.ToString()} completed ", false);
            if (tree.Step.StepType == RecipeStepTypes.RunRecipe)
            {
                currentRecipeRunedSetepCount = 0;
            }
            else
            {
                currentRecipeRunedSetepCount++;
                var recipeRunUpdate = new { Recipe = Recipe.RecipeName, CurrentStep = tree.Step.StepName, CurrentStepIdx = currentRecipeRunedSetepCount, TotalStepCount = Recipe.Steps.Count() };
                OnRecipeRunUpdatedInvoke(System.Text.Json.JsonSerializer.Serialize(recipeRunUpdate));
            }
        }
        public override void AbortWork()
        {
            IsAbort = true;
            Logger.Log($"Aborting recipe run thread:{Name}");
            if (_InnerRecipeRunThread != null)
            {
                Logger.Log("Aborting inner recipe");
                _InnerRecipeRunThread.IsAbort = true;
                _InnerRecipeRunThread.Abort();
                while (_InnerRecipeRunThread != null && _InnerRecipeRunThread.IsAlive)
                {
                    Thread.Sleep(200);
                }
                Logger.Log("Inner recipe aborted");
            }

            Logger.Log("Aborting pumping");
            FluidicsInterface.StopPumping();
            FluidicsInterface.WaitForPumpingCompleted();
            Logger.Log("Pumping aborted");

            if (_AutoFocusProcess != null)
            {
                Logger.Log("Aborting auto focus");
                _AutoFocusProcess.Abort();
                while (_AutoFocusProcess != null)
                {
                    Thread.Sleep(10);
                }
                Logger.Log("Auto focus aborted");
            }

            Logger.Log("Halting all motions");
            _MotionController.HaltAllMotions();
            Logger.Log("All motions halted");

            Logger.Log("Turning off all LEDs");
            if (!IsSimulationMode)
            {
                _LEDController.SetLEDStatus(LEDTypes.Green, false);
                _LEDController.SetLEDStatus(LEDTypes.Red, false);
                _LEDController.SetLEDStatus(LEDTypes.White, false);
            }
            Logger.Log("All LEDs are turned off");

            Logger.Log("Turning off temperature control");
            _FCTemperControllerRev2.SetControlSwitch(false);
            Logger.Log("Temperature control is turned off");
            Logger.Log("Turn off PreHeating");
            SetFluidPreHeatingEnable(false);
            Logger.Log("Resetting all loop step counts.");
            foreach (var item in Recipe.Steps)
            {
                ResetLoopStepCounts(item);
            }
            _NasFolderlist.Clear();
            _Folderlist.Clear();
            Logger.Log($"Recipe run thread:{Name} aborted");
        }
        //---------------------------------------------------------------------------------------------------
        #region Run Image Step
        private void RunStepProc(ImagingStep step)
        {
            // access the tile watcher via singleton pattern
            TileWatcher tileWatcher = TileWatcher.GetTileWatcher();

            // reset the tile watcher at the beginning of each cycle
            tileWatcher.Reset();

            ImageListKeeper listKeeper = ImageListKeeper.GetImageListKeeper();

            // clear out the image record to make room for the tiles this cycle
            listKeeper.Reset();

            // Determine Read1/Index1/Read2/Index2. Defaults to Read1
            if (Recipe.RecipeName.ToLower().Contains("read1"))
            {
                step.Read = ImagingStep.SequenceRead.Read1;
            }
            else if (Recipe.RecipeName.ToLower().Contains("read2"))
            {
                step.Read = ImagingStep.SequenceRead.Read2;
            }
            else if (Recipe.RecipeName.ToLower().Contains("index1"))
            {
                step.Read = ImagingStep.SequenceRead.Index1;
            }
            else if (Recipe.RecipeName.ToLower().Contains("index2"))
            {
                step.Read = ImagingStep.SequenceRead.Index2;
            }

            // Skip if no image need to take and no AF required when testing
            if (!_ScanEveryRegion && _loopCount != 1 && _loopCount % 10 != 0 && step.Regions[0].Imagings.Count < 1)
            {
                Logger.Log($"Recipe run thread:{Name} is skipping imaging step...", SeqLogFlagEnum.DEBUG);
                return;
            }

            // Image Intensity check.
            ImageStatistics ImageStat = new ImageStatistics();

            if (_Imaging.ImageQProcessingThread == null)
            {
                _Imaging.StartImageQProcessingThread(() => ProcessImageQ());
            }

            if (_IsBackUp && _Imaging.ImageBackingup != null && !_Imaging.ImageBackingup.IsBackupStarted)
            {
                _Imaging.StartImageBackupQProcessingThread();// () => ImageBackupQProcessing());
            }


            // 1. create save folder
            if (!Directory.Exists(_RecipeRunImageDataDir))
            {
                Directory.CreateDirectory(_RecipeRunImageDataDir);
                using (StreamWriter sw = File.AppendText(Path.Combine(_RecipeRunImageDataDir, "list.txt")))
                {
                    if (_IsBC && !Recipe.RecipeName.Contains("_CL"))
                    {
                        sw.WriteLine("bc");
                    }
                    else
                    {
                        sw.WriteLine("qc");
                    }

                    sw.WriteLine(Path.GetFileName(_UserEmail));

                    if (Recipe.RecipeName.Contains("Index"))
                    {
                        sw.WriteLine(_RecipeParameters.SelectedIndTemplate.ToString());
                    }
                    else
                    {
                        sw.WriteLine(_SelectedTemplate.ToString());
                    }

                    if (!_IsIndex)
                    {
                        sw.WriteLine("1");
                    }
                    else
                    {
                        sw.WriteLine("0");
                    }

                    sw.WriteLine(SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName);
                }
            }

            if (CheckAFByFluor)
            {
                string testimagefolder = Directory.GetParent(_RecipeRunImageDataDir).FullName + "\\" + "TestImage" + "\\";
                if (!Directory.Exists(testimagefolder))
                {
                    Directory.CreateDirectory(testimagefolder);
                }
            }

            if (!Directory.Exists(_NasFolder) && _IsBackUp)
            {
                try
                {
                    Directory.CreateDirectory(_NasFolder);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex.ToString());
                    OnStepRunUpdatedInvoke(step, "Failed to create Nas folder", true);
                }
            }

            //Set loop count
            string loopInfo = string.Empty;
            _loopCount = 0;
            if (_CurrentTree.Parent != null)
            {
                if (_CurrentTree.Parent.Step.StepType == RecipeStepTypes.Loop)
                {
                    _loopCount = ((LoopStep)_CurrentTree.Parent.Step).LoopCounts + _StartInc - 1;
                    loopInfo = string.Format("Inc{0}", _loopCount);
                }
            }
            else
            {
                _loopCount = _StartInc - 1;
                loopInfo = string.Format("Inc{0}", _loopCount);
            }

            #region One reference method add regions according to region map in calib.json
            // when IsOneRef is enabled, imaging regions will be automatically added from the calibration file

            // display a warning on the first cycle if the recipe file already contains more than one imaging region
            if (_IsOneRef && _loopCount == 1 && step.Regions.Count() != 1)
            {
                if (StopOnFailure("Image recipe contains more than one region and will mapping again, stop?", IsSimulationMode))
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Image recipe contains more than one region when OneRef is on, recipe stop."), true);
                    AbortWork();
                    ExitStat = ThreadExitStat.Error;
                    return;
                }
                else
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Image recipe contains more than one region and will mapping again, recipe continue."), true);
                    Logger.LogWarning("Image recipe contains more than one region and will mapping again, recipe continue.");
                }
            }

            // 
            if (_IsOneRef && step.Regions.Count() != SettingsManager.ConfigSettings.StageRegionMaps.Count())
            {
                bool startpoint = false;
                int regionindexnum = 0;
                foreach (KeyValuePair<RegionIndex, double[]> regionMap in SettingsManager.ConfigSettings.StageRegionMaps)
                {
                    ImagingRegion newregion = new ImagingRegion();
                    RegionIndex regionindex = regionMap.Key; // contains a tuple with lane, column, row
                    newregion.Lane = regionindex.Index.Item1;
                    newregion.Column = regionindex.Index.Item2;
                    newregion.Row = regionindex.Index.Item3;

                    if (startpoint)
                    {
                        for (int j = 0; j < step.Regions[0].Imagings.Count; j++)
                        {
                            newregion.Imagings.Add(step.Regions[0].Imagings[j]);
                        }
                        for (int k = 0; k < step.Regions[0].ReferenceFocuses.Count; k++)
                        {
                            FocusSetting focussetting = new FocusSetting();
                            focussetting.Position = step.Regions[0].ReferenceFocuses[k].Position;
                            newregion.ReferenceFocuses.Add(focussetting);
                        }
                        newregion.RegionIndex = regionindexnum;
                        step.Regions.Add(newregion);
                    }
                    if ((newregion.Lane == step.Regions[0].Lane && newregion.Column == step.Regions[0].Column && newregion.Row == step.Regions[0].Row))
                    {
                        startpoint = true;
                        step.Regions[0].RegionIndex = regionindexnum;
                    }
                    regionindexnum++;
                }
            }
            #endregion One reference method

            //Change AF skip interval and determine whehter AF every single region depends on imaging how many regions
            int _tileCountpreLane = SettingsManager.ConfigSettings.FCRow * SettingsManager.ConfigSettings.FCColumn; //Tile count pre Lane, determine AF scheme
            if (_tileCountpreLane >= 2 * 19 && _tileCountpreLane < 4 * 38)
            {
                AFColumninterval = 3;
            }
            else if (_tileCountpreLane < 2 * 19)
            {
                _ScanEveryRegion = true;
            } // AF at every region if tile no. is lowe }

            //Create list of region sequence, as when AF whole FC, only AF every two regions
            List<int> regionorder = new List<int>();
            List<int> AFregionorder = new List<int>();
            AFregionorder.Add(0);
            //normal order
            for (int i = 0; i < step.Regions.Count; i++)
            {
                regionorder.Add(i);
            }
            //AF order
            for (int i = 1; i < step.Regions.Count; i++)
            {
                if (i % 2 == 0)
                {
                    AFregionorder.Add(i);
                    AFregionorder.Add(i - 1);
                }
            }
            if (step.Regions.Count % 2 == 0)
            {
                AFregionorder.Add(step.Regions.Count - 1);
            }

            //Calculate number of tile
            _CycleTotalImageCount = step.Regions.Count * step.Regions[0].ReferenceFocuses.Count * step.Regions[0].Imagings.Count * 2;

            //In case of recipe without AF, calculate initial exposure time
            double initialexpo = 0.1;
            if (step.Regions[0].Imagings.Count > 0)
            {
                initialexpo = Math.Round((step.Regions[0].Imagings[0].GreenExposureTime * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3);
            }

            // 2. capture images for each region
            int skipcounts = 0;
            //int regioncounts = 0;
            int Ystartpos = 0;
            int Ystartspeed = 0;
            //< the difference between the focus position of this cycle and the previous cycle for two surface.
            // [top surface Z change cross cycles, bottom surface Z change cross cycles]
            double[] cyclediff = new double[2] { 0, 0 };
            int Xstartpos = 0;
            int Xstartspeed = 0;

            // Determine whether this is the cycle need to AF full region for both surface in order to map the curvature of FC in Z direction for both surface
            // This map will be used to calculate focus in all other cycle that AF less regions with only top surface.
            // _ScanEveryRegion is a boolean set to true when exp with much less tile, only useful for internal test
            //cycle 1 and 2 will have some temp profile difference if run HYB and rest of exp seperatly so that need to remap,
            //will not be issue if run CG + Hyb and rest of exp all together, which mean only need to run AF for full region cycle == 1.
            // remap every 45 cycle for re-calibration.

            List<int> currentcycleorder = regionorder;
            bool isAFcycle = _ScanEveryRegion || _loopCount == 1 || _loopCount == 2 || _loopCount % 100 == 0; //==> test requested by yy
            //Change movement order to AF every 2 regions first.
            if (_IsOneRef && step.IsAutoFocusOn && isAFcycle && _tileCountpreLane >= 2 * 19) // skip one region for AF if imaging full region
            {
                currentcycleorder = AFregionorder;
            }

            bool isHConly = false;
            bool isReconnectedForNullImage = false; // boolean shows whether had reconnected before because of too many incomplete data(null image)
            foreach (int regionNum in currentcycleorder)
            {
                if (IsAbort)
                {
                    break;
                }
                Logger.LogMessage($"Capturing region: {regionNum}");

                int YtargetPos = 0;
                int XtargetPos = 0;
                int Yspeed = 1;
                int Xspeed = 1;

                moveToRegion(regionNum, step, ref Ystartpos, ref Xstartpos, ref Ystartspeed, ref Xstartspeed, ref YtargetPos, ref XtargetPos, ref Yspeed, ref Xspeed);

                // sort focuses
                ImagingRegion region = step.Regions[regionNum];
                RegionIndex FClocation = new RegionIndex(new int[3] { region.Lane, region.Column, region.Row });

                // put focal planes in decending order (top first)
                List<int> focusorder = new List<int>();
                SortFocuses(focusorder, region);

                int focusnum = 0; //image top surface first
                for (; focusnum < focusorder.Count(); focusnum++)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    CaptureTile(focusnum, focusorder, regionNum, FClocation, step, currentcycleorder, regionorder, AFregionorder,
                        ref isAFcycle, ref isHConly, ref cyclediff, ref initialexpo, ref skipcounts, loopInfo, ref YtargetPos, ref XtargetPos, ref Yspeed, ref Xspeed, ref isReconnectedForNullImage);
                }//for focusIndex
            }//for region

            if (!IsSimulationMode)
            {
                Logger.Log($"Waiting for Image Processing to finish...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                // at the end of the cycle, wait until all image tasks are done before attempting re-capture
                // 1: wait until camera have sent all data
                WaitForImagesReadout();

                // make sure the Queue is empty and ImageProcessing is not busy
                while (true)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    lock (_processImageBusyLocker)
                    {
                        if (!_processImageQueueBusy)
                        {
                            lock (_imageQueueHasItemsLocker)
                            {
                                if (!ImageQ.HasItems)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    Thread.Sleep(20);
                }
                stopwatch.Stop();
                Logger.LogMessage($"Image Processing has finished. Waited: {stopwatch.ElapsedMilliseconds} ms. (Cycle#: {CurrentImageLoopCount.Counts})");
                Logger.Log($"Post-acquisition image saving delay [ms]|{stopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
            }

            // check if tiles with missing images were found
            int droppedTiles = tileWatcher.LostTileCount();
            Logger.Log($"Tile Watcher detected {droppedTiles} incomplete tiles.", SeqLogFlagEnum.DEBUG);

            // attempt to re-capture dropped tiles
            Stopwatch recaptureStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < droppedTiles; ++i)
            {
                if (IsAbort)
                {
                    break;
                }

                // get the tile location from the tile watcher
                int regionNum = 0;
                int focusNum = 0;
                tileWatcher.LostTileInfo(i, ref regionNum, ref focusNum);
                Logger.Log($"Re-imaging tile: {regionNum} focus:{focusNum}", SeqLogFlagEnum.DEBUG);

                int YtargetPos = 0;
                int XtargetPos = 0;
                int Yspeed = 1;
                int Xspeed = 1;

                moveToRegion(regionNum, step, ref Ystartpos, ref Xstartpos, ref Ystartspeed, ref Xstartspeed, ref YtargetPos, ref XtargetPos, ref Yspeed, ref Xspeed);

                // sort focuses
                ImagingRegion region = step.Regions[regionNum];
                RegionIndex FClocation = new RegionIndex(new int[3] { region.Lane, region.Column, region.Row });

                // put focal planes in decending order (top first)
                List<int> focusorder = new List<int>();
                SortFocuses(focusorder, region);

                bool forceAF = true; // set currentcycleorder to regionorder and isAFcycle to true to force AF to run for this tile
                CaptureTile(focusNum, focusorder, regionNum, FClocation, step, regionorder/*currentcycleorder*/, regionorder, AFregionorder,
                        ref forceAF/*isAFcycle*/, ref isHConly, ref cyclediff, ref initialexpo, ref skipcounts, loopInfo, ref YtargetPos, ref XtargetPos, ref Yspeed, ref Xspeed, ref isReconnectedForNullImage);
            }
            Logger.Log($"Image re-capture elapsed time [ms]|{recaptureStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);


            if (!IsSimulationMode)
            {
                _MotionController.AbsoluteMove(MotionTypes.YStage, Ystartpos, Ystartspeed,
                        (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), false, true);
                _MotionController.AbsoluteMove(MotionTypes.XStage, Xstartpos, Xstartspeed,
                        (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), false, true);

            }
            else
            {
                Thread.Sleep(200);
            }
            UnInitializeCameras(step, _camera1, _camera2);
        }
        #region Imaging step helper function
        /// <summary>
        /// Puts the focuses in focusorder in decending order (top of FC is imaged first)
        /// </summary>
        /// <param name="focusorder">The list of focus indices to be sorted</param>
        /// <param name="region">The region from which reference focuses are retrieved</param>
        private void SortFocuses(List<int> focusorder, in ImagingRegion region)
        {
            focusorder.Add(0);
            int focusInd = 1;
            for (; focusInd < region.ReferenceFocuses.Count; focusInd++)
            {
                if (region.ReferenceFocuses[focusInd].Position > region.ReferenceFocuses[focusInd - 1].Position) //bottom surface was added before top
                {
                    focusorder.Insert(0, focusInd);
                }
                else
                {
                    focusorder.Add(focusInd);
                }
            }
        }
        /// <summary>
        /// Moves the stage to a regino (x,y) to prepare for image capture
        /// </summary>
        /// <param name="regionNum"></param>
        /// <param name="step"></param>
        /// <param name="Ystartpos"></param>
        /// <param name="Xstartpos"></param>
        /// <param name="Ystartspeed"></param>
        /// <param name="Xstartspeed"></param>
        /// <param name="YtargetPos"></param>
        /// <param name="XtargetPos"></param>
        /// <param name="Yspeed"></param>
        /// <param name="Xspeed"></param>
        private void moveToRegion(in int regionNum, ImagingStep step, ref int Ystartpos, ref int Xstartpos, ref int Ystartspeed, ref int Xstartspeed, ref int YtargetPos, ref int XtargetPos, ref int Yspeed, ref int Xspeed)
        {
            ImagingRegion region = step.Regions[regionNum];
            RegionIndex FClocation = new RegionIndex(new int[3] { region.Lane, region.Column, region.Row });



            if (!IsSimulationMode)
            {
                // 1. move Y stage to the region -----------------------------------------------------------------------------------------------------------------------------------------------------
                YtargetPos = (int)Math.Round((SettingsManager.ConfigSettings.StageRegionMaps[FClocation][1] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                XtargetPos = (int)Math.Round((SettingsManager.ConfigSettings.StageRegionMaps[FClocation][0] * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));
                Yspeed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]));
                Xspeed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]));

                int waitTime = 0;
                bool xBusy = true;
                bool yBusy = true; ;
                do
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    if (++waitTime > 1200)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                            $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, retry."), true);
                        //retry one last time with wait=true
                        if (_MotionController.AbsoluteMove(MotionTypes.YStage, Ystartpos, Yspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel *
                            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true, true) ||
                            // move X stage
                            _MotionController.AbsoluteMove(MotionTypes.XStage, Xstartpos, Xspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel *
                            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), true, true))
                        {
                            OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                            $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, recipe stop."), true);
                            AbortWork();
                            ExitStat = ThreadExitStat.Error;
                            throw new System.InvalidOperationException("Waiting stage stable Failure");
                        }
                    }
                    Thread.Sleep(10);
                    _MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);
                    xBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy;
                    yBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy;
                    _MotionController.FCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_X];
                    _MotionController.YCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_Y];
                }
                while (xBusy || yBusy);
                if (!_MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, Yspeed,
                    (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), false, true) ||
                    // move X stage
                    !_MotionController.AbsoluteMove(MotionTypes.XStage, XtargetPos, Xspeed,
                    (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), false, true))
                {
                    OnStepRunUpdatedInvoke(step, "Failed to move X/Y stage, Recipe stop", true);
                    AbortWork();
                    ExitStat = ThreadExitStat.Error;
                    throw new System.InvalidOperationException("X/Y Stage Movement Failure");
                }


                if (regionNum == 0) { Xstartpos = XtargetPos; Xstartspeed = Xspeed; Ystartpos = YtargetPos; Ystartspeed = Yspeed; }
            }
            else
            {
                if (regionNum == 0) { Ystartpos = YtargetPos; Ystartspeed = Yspeed; Xstartpos = XtargetPos; Xstartspeed = Xspeed; }
                _ImageInfo.MixChannel.YPosition = Math.Round(YtargetPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);
                _ImageInfo.MixChannel.XPosition = Math.Round(XtargetPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2);
                Thread.Sleep(200);
            }
            //-----------------------------------------------------------------------------------------------------------------------
        }
        /// <summary>
        /// Attempts to capture images of a tile from a region (x,y) and focus (z).
        /// Will auto-focus if necessary.
        /// </summary>
        /// <param name="focusnum"></param>
        /// <param name="focusorder"></param>
        /// <param name="regionNum"></param>
        /// <param name="FClocation"></param>
        /// <param name="step"></param>
        /// <param name="currentcycleorder"></param>
        /// <param name="regionorder"></param>
        /// <param name="AFregionorder"></param>
        /// <param name="isAFcycle"></param>
        /// <param name="isHConly"></param>
        /// <param name="cyclediff"></param>
        /// <param name="initialexpo"></param>
        /// <param name="skipcounts"></param>
        /// <param name="loopInfo"></param>
        /// <param name="YtargetPos"></param>
        /// <param name="XtargetPos"></param>
        /// <param name="Yspeed"></param>
        /// <param name="Xspeed"></param>
        private void CaptureTile(int focusnum, List<int> focusorder, int regionNum, RegionIndex FClocation, ImagingStep step, List<int> currentcycleorder, List<int> regionorder, List<int> AFregionorder,
            ref bool isAFcycle, ref bool isHConly, ref double[] cyclediff, ref double initialexpo, ref int skipcounts, string loopInfo, ref int YtargetPos, ref int XtargetPos, ref int Yspeed, ref int Xspeed, ref bool isReconnectedForNullImage)
        {
            ImagingRegion region = step.Regions[regionNum];

            // the first focus is the z position of the top surface
            _surface = (focusnum == 0) ? "t" : "b";

            // get a z position using an index (focusnum) into a list of focus indices (focusorder)
            // TODO: simplify this
            double refFocus0 = region.ReferenceFocuses[focusorder[focusnum]].Position;

            // initialze an array - fluor check passes
            bool[] isPassCheckByFluor = new bool[2] { true, true };

            //adjust Z position no camera image to be saved ----------------------------------------------------------------------------------------------------------

            #region 2. do auto focus if necessary, or else just move z stage to refFocus0

            // isAFregion is true if the region number is evenly divisible by AFColumnInterval
            bool isAFregion = regionNum % AFColumninterval == 0;

            // ?
            bool isAForder = (currentcycleorder == AFregionorder && regionNum % 2 == 0) || currentcycleorder == regionorder;

            OnStepRunUpdatedInvoke(step, $"{Thread.CurrentThread.Name} [AF] Capturing tile: {regionNum}", false);

            // run auto-focus if necessary
            if (step.IsAutoFocusOn && (isAFcycle || isAFregion) && isAForder)
            {
                Logger.Log($"AF is enabled. Running AutoFocus. Reference0: {refFocus0}", SeqLogFlagEnum.DEBUG);
                Stopwatch afStopwatch = Stopwatch.StartNew();

                if (IsAbort)
                {
                    return;
                }
                #region AF with first ROI
                UnInitializeCameras(step, _camera1, _camera2);

                //When isHConly set to true, no scan through with larger step size, but only HillClimb, if reference focus given is very close to focus position, scan through
                // is unnecessary. This will save more time by taking less image and move to less Z pos
                // Two condtion we need scan through:
                // 1. isAFcycle, scan whole FC map the curvature.: Reference may far away from true focus, avoid local maximum.
                // 2. the cycle just after isAFcycle only first few regions.: On the 3rd and 46th loop, "scan through" the first 11 regions
                // 3rd and 46th loop is the cycle that just after the cycle 2, and 45 which AF full regions, because different AF scheme, the time spent on first few regions
                // will change, as a result, temperature profile will be different and focus position will have minor shift, in case of local maximum we run scan through given those shifts.
                // 11 region is a arbitrary number that approximatlly time that temp recovered from overshot and heatsink temp stablized, which means
                // Z pos do not shift that much after certain time(approximatlly 11 regions).
                if (isAFcycle || (_loopCount == 3 && regionNum < 11) || (_loopCount == 46 && regionNum < 11))
                {
                    isHConly = false; // "scan through"
                }
                else
                {
                    isHConly = true;  // hill climb only
                }
                _IsAFing = true;

                ILucidCamera AFcamera = _camera1;
                if (!IsSimulationMode)
                {
                    AFcamera = _camera1.Channels.Contains("2") ? _camera1 : _camera2;
                }
                // Wait stage stable before AF
                int waitTime = 0;
                bool xBusy = true;
                bool yBusy = true;
                do
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    if (++waitTime > 1200)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                            $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, retry."), true);
                        //retry one last time with wait=true
                        if (_MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, Yspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel *
                            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true, true) ||
                            // move X stage
                            _MotionController.AbsoluteMove(MotionTypes.XStage, XtargetPos, Xspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel *
                            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), true, true))
                        {
                            OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                            $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, recipe stop."), true);
                            AbortWork();
                            ExitStat = ThreadExitStat.Error;
                            throw new System.InvalidOperationException("Waiting stage stable Failure");
                        }
                    }
                    Thread.Sleep(10);
                    // update motion states from the controller
                    _MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);
                    // check if x and y axes are still busy
                    xBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy;
                    yBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy;
                    // TODO: the motion controller position information should be updated internally, not here...
                    _MotionController.FCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_X];
                    _MotionController.YCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_Y];
                }
                while (xBusy || yBusy);

                _ImageInfo.MixChannel.YPosition = Math.Round(_MotionController.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);
                _ImageInfo.MixChannel.XPosition = Math.Round(_MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2);

                ////////////////////////////////
                // Retry AF 4x before attempting to push a bubble
                // Expand the search range each time by rangeIncrement
                // Pump scanning solution after 3 failed tries
                ////////////////////////////////
                const int maxRetries = 4; //< retry limit for this region and ROI
                int retryCount = 0; //< the number of times AF has been attempted on this region and ROI
                double searchRange = SettingsManager.ConfigSettings.AutoFocusingSettings.ZRange; //< the current focus search range (unit = [um])
                const double searchRangeIncrement = 20.0; //< the amount to increase the the focus search range each retry attempt (units = [um])

                AutoFocusSettings _AutoFocusSetting = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                _AutoFocusSetting.ZstageLimitH = refFocus0 + (searchRange / 2);
                _AutoFocusSetting.ZstageLimitL = refFocus0 - (searchRange / 2);
                _AutoFocusSetting.Reference0 = refFocus0;
                _AutoFocusSetting.IsHConly = isHConly;
                _IsAutoFocusingSucceeded = false;

                while (retryCount < maxRetries && !_IsAutoFocusingSucceeded)
                {
                    retryCount++;
                    // run new AF routine if set to fiducial 2.1
                    if (SettingsManager.ConfigSettings.AutoFocusingSettings.FiducialVersion == 2.1 && isAFregion && !isAFcycle)
                    {
                        //new fiducial
                        _AutoFocusSetting.ScanInterval = 0.5;

                        Dictionary<double, List<double>> nfmetricpatterns = new Dictionary<double, List<double>>();
                        if ((_loopCount - 1) % 45 == 0 && regionNum == 0 && focusnum == 0)
                        {
                            NFPatternWLocations.Clear(); // Re-mapping to fit the change of light reflection
                        }
                        if (NFPatternWLocations.ContainsKey(FClocation))
                        {
                            nfmetricpatterns = NFPatternWLocations[FClocation];
                        }
                        _AutoFocusNFProcess = new AutoFocusTiltFid(_CallingDispatcher, _MotionController, AFcamera, _LEDController, _AutoFocusSetting, nfmetricpatterns);
                        _AutoFocusNFProcess.IsSimulationMode = IsSimulationMode;
                        _AutoFocusNFProcess.Completed += AutoFocusNFProcess_Completed;
                        _AutoFocusNFProcess.Name = "AutofocusNF";
                        _AutoFocusNFProcess.Start();
                        _AutoFocusNFProcess.Join();

                    }
                    else
                    {
                        _AutoFocusSetting.ScanInterval = 3;
                        _AutoFocusProcess = new AutoFocusCommand2(_CallingDispatcher, _MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess.Completed += AutoFocusProcess_Completed;
                        _AutoFocusProcess.Name = "Autofocus";
                        _AutoFocusProcess.Start();
                        _AutoFocusProcess?.Join(); // wait for process to finish
                    }

                    // make AF always succeed in simulation
                    if (_IsAutoFocusingSucceeded || IsSimulationMode)
                    {
                        if (IsSimulationMode)
                        {
                            _IsAutoFocusingSucceeded = true;
                        }
                        // reference0 gets set to the current z position
                        refFocus0 = _MotionController.ZCurrentPos;

                        Logger.Log($"Thread: {Thread.CurrentThread.Name} [AF] Autofocus succeeded after {retryCount} attempt. " +
                            $"ROI:1. X:{_ImageInfo.MixChannel.XPosition:F}, Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                            $" Sharpness: {_FocusedSharpness}", SeqLogFlagEnum.DEBUG);

                        Logger.Log($"Thread: {Thread.CurrentThread.Name} [AF] reference0 is now: {refFocus0}", SeqLogFlagEnum.DEBUG);


                        if (SettingsManager.ConfigSettings.AutoFocusingSettings.FiducialVersion == 2.1 && isAFregion && !isAFcycle && !NFPatternWLocations.ContainsKey(FClocation))
                        {
                            NFPatternWLocations.Add(FClocation, _FocusPattern);
                        }
                    }
                    else // AF was not successful
                    {
                        // retry
                        if (IsAbort)
                        {
                            return;
                        }
                        if (_AutoFocusErrorMessage != null)
                        {
                            if (_AutoFocusErrorMessage.Contains("Range")) //shift range if out of range
                            {
                                refFocus0 = _MotionController.ZCurrentPos;
                            }
                        }

                        // increament search range and try again
                        searchRange += searchRangeIncrement;

                        _AutoFocusSetting.ZstageLimitH = refFocus0 + (searchRange / 2);
                        _AutoFocusSetting.ZstageLimitL = refFocus0 - (searchRange / 2);
                        _AutoFocusSetting.Reference0 = refFocus0;
                        _AutoFocusSetting.IsHConly = false;

                        OnStepRunUpdatedInvoke(step, ($"[{Thread.CurrentThread.Name}] [AF]: Autofocus failed. ROI:1 Attempt#: {retryCount}. " +
                            $"Location: X:{_ImageInfo.MixChannel.XPosition:F}, Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                            $" Error message: {_AutoFocusErrorMessage} Retrying with a wider range. Reference0: {refFocus0} " +
                            $" SearchRange : [{_AutoFocusSetting.ZstageLimitL},{_AutoFocusSetting.ZstageLimitH}]"), false);

                        if (IsAbort)
                        {
                            return;
                        }

                        // if AF hasn't succeed, try to push a bubble and try to focus one last time on this ROI
                        if (retryCount == (maxRetries - 1))
                        {
                            OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                            FlushBubble(region.Lane);
                        }
                    } // AF not successful
                }
                if (!_IsAutoFocusingSucceeded)
                {
                    OnStepRunUpdatedInvoke(step, ($"[{Thread.CurrentThread.Name}] [AF]: Autofocus failed after {maxRetries} attempts at X:{_ImageInfo.MixChannel.XPosition:F}, Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                            $" Error message: {_AutoFocusErrorMessage} User attention, retry another ROI. Reference0: {refFocus0} " +
                            $" Previous SearchRange: [{_AutoFocusSetting.ZstageLimitL},{_AutoFocusSetting.ZstageLimitH}]"), true);

                    /*OnStepRunUpdatedInvoke(step, string.Format("[{4}]: Autofocus failed at X{0:00.00}_Y{1:00.00}, Z:{2:F2}, Std{3:F3}, ErrorMessage:{5}, user attention, retry another ROI", _ImageInfo.MixChannel.XPosition,
                    _ImageInfo.MixChannel.YPosition, _MotionController.ZCurrentPos, _FocusedSharpness, Thread.CurrentThread.Name, _AutoFocusErrorMessage), true);*/
                    isPassCheckByFluor[0] = false; // I guess the first element of this array is used to remember if AF passed on the first ROI. Why?
                }
                else
                {
                    OnStepRunUpdatedInvoke(step, $"{Thread.CurrentThread.Name} [AF] Autofocus succeeded after {retryCount} try. ROI:1. " +
                                $"X:{_ImageInfo.MixChannel.XPosition:F} Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                            $"Sharpness: {_FocusedSharpness:F}", false);
                }
                #endregion AF with first ROI

                #region check the image focus by fluorscent signal
                if (IsAbort)
                {
                    return;
                }
                if (_IsAutoFocusingSucceeded && CheckAFByFluor && regionNum == 0 && focusnum == 0)
                {
                    //first check

                    OnStepRunUpdatedInvoke(step, "Check by fluor", false);
                    UnInitializeCameras(step, _camera1, _camera2);
                    isPassCheckByFluor = CheckImagebyFluro(step, LEDTypes.Red, (int)region.Imagings[1].RedIntensity,
                        Math.Round((region.Imagings[1].RedExposureTime * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3)
                        , focusorder[focusnum], regionNum, _IntThreshold, true);

                    //ispassCheck[1] will be false only if (Relative Intensity < 600 && _LoopCount == 1 && regioncount == 1) first cycle first region bottom surface
                    // if false, double the exposure time
                    if (!isPassCheckByFluor[1])
                    {
                        OnStepRunUpdatedInvoke(step, "Intensity too low at first cycle first region, double exposure", true);
                        for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
                        {
                            region.Imagings[imagingIndex].GreenExposureTime *= 2;
                            region.Imagings[imagingIndex].RedExposureTime *= 2;
                        }
                        for (int i = 1; i < step.Regions.Count; i++)
                        {
                            for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
                            {
                                step.Regions[i].Imagings[imagingIndex].GreenExposureTime *= 2;
                                step.Regions[i].Imagings[imagingIndex].RedExposureTime *= 2;
                            }
                        }
                        isPassCheckByFluor = CheckImagebyFluro(step, LEDTypes.Red, (int)region.Imagings[1].RedIntensity,
                        Math.Round((region.Imagings[1].RedExposureTime * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3),
                        focusorder[focusnum], regionNum, _IntThreshold, true);
                        // after double check intensity again
                        if (!isPassCheckByFluor[1])
                        {
                            OnStepRunUpdatedInvoke(step, "Intensity too low at first cycle first region, double exposure again", true);
                            for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
                            {
                                region.Imagings[imagingIndex].GreenExposureTime *= 2;
                                region.Imagings[imagingIndex].RedExposureTime *= 2;
                            }
                            for (int i = 1; i < step.Regions.Count; i++)
                            {
                                for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
                                {
                                    step.Regions[i].Imagings[imagingIndex].GreenExposureTime *= 2;
                                    step.Regions[i].Imagings[imagingIndex].RedExposureTime *= 2;
                                }
                            }
                            // check again
                            isPassCheckByFluor = CheckImagebyFluro(step, LEDTypes.Red, (int)region.Imagings[1].RedIntensity,
                                Math.Round((region.Imagings[1].RedExposureTime * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3),
                                focusorder[focusnum], regionNum, _IntThreshold, true);
                            if (!isPassCheckByFluor[1])
                            {
                                // if still failed after times 4*, recipe stopped.
                                OnStepRunUpdatedInvoke(step, "Unable to recalibrate exposure time", true);
                                if (StopOnFailure("Unable to recalibrate exposure time, stop?", IsSimulationMode))
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format("Unable to recalibrate exposure time, recipe stop."), true);
                                    AbortWork();
                                    ExitStat = ThreadExitStat.Error;
                                    throw new System.InvalidOperationException("Recalibrate exposure time Failure");
                                }
                                else
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format("Unable to recalibrate exposure time, recipe continue."), true);
                                    Logger.LogWarning("Unable to recalibrate exposure time, recipe continue.");
                                    return;
                                }
                            }
                        }
                    }
                    initialexpo = Math.Round((region.Imagings[0].GreenExposureTime * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3);
                }
                #endregion check image quality
                #region Re-focus with Secondary ROI if failed to pass the image quality check
                if (IsAbort)
                {
                    return;
                }
                if (!isPassCheckByFluor[0] || ((_loopCount > 2) && Math.Abs(cyclediff[focusnum]) >= 3)) //|| (_LoopCount <=2 && regionnum > 2){
                {
                    OnStepRunUpdatedInvoke(step, $"Reference0 CycleDiff: {Math.Abs(cyclediff[focusnum])}", false);
                    OnStepRunUpdatedInvoke(step, "Failed to pass focus check with fluor or failed to AF with ROI 1, trying with ROI 2", false);
                    // reset try count and search range for the second ROI
                    retryCount = 0;
                    searchRange = SettingsManager.ConfigSettings.AutoFocusingSettings.ZRange;
                    while (retryCount < maxRetries && !_IsAutoFocusingSucceeded)
                    {
                        retryCount++;                        
                        refFocus0 = region.ReferenceFocuses[focusorder[focusnum]].Position;
                        _AutoFocusSetting.ZRange = searchRange;
                        _AutoFocusSetting.ZstageLimitH = refFocus0 + (searchRange / 2);
                        _AutoFocusSetting.ZstageLimitL = refFocus0 - (searchRange / 2);
                        _AutoFocusSetting.Reference0 = refFocus0;
                        _AutoFocusSetting.ROI = _AutoFocusSetting.ROI2;
                        _AutoFocusSetting.IsHConly = isHConly;
                        _AutoFocusProcess = new AutoFocusCommand2(_CallingDispatcher, _MotionController, AFcamera, _LEDController, _AutoFocusSetting);
                        _AutoFocusProcess.IsSimulationMode = IsSimulationMode;
                        _AutoFocusProcess.Completed += AutoFocusProcess_Completed;
                        _AutoFocusProcess.Name = "Autofocus";
                        _AutoFocusProcess.Start();
                        _AutoFocusProcess?.Join();
                        if (_IsAutoFocusingSucceeded)
                        {
                            refFocus0 = _MotionController.ZCurrentPos;
                            OnStepRunUpdatedInvoke(step, $"{Thread.CurrentThread.Name} [AF] Autofocus succeeded after {retryCount} try. ROI:2. " +
                                $"X:{_ImageInfo.MixChannel.XPosition:F} Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                            $"Sharpness: {_FocusedSharpness:F}", false);
                            OnStepRunUpdatedInvoke(step, $"{Thread.CurrentThread.Name} [AF] reference0 is now: {refFocus0}", false);
                        }
                        else
                        {
                            searchRange += searchRangeIncrement;
                            _AutoFocusSetting.ZstageLimitH = refFocus0 + (searchRange / 2);
                            _AutoFocusSetting.ZstageLimitL = refFocus0 - (searchRange / 2);
                            _AutoFocusSetting.Reference0 = refFocus0;
                            _AutoFocusSetting.IsHConly = false;

                            OnStepRunUpdatedInvoke(step, ($"[{Thread.CurrentThread.Name}] [AF]: Autofocus failed. ROI:2 (Attempt: {retryCount}) at X:{_ImageInfo.MixChannel.XPosition:F}, Y:{_ImageInfo.MixChannel.YPosition:F}, Z:{_MotionController.ZCurrentPos:F}" +
                                    $" Error message: {_AutoFocusErrorMessage}  Retrying with a wider range. Reference0: {refFocus0} " +
                                    $" SearchRange: Z=[{_AutoFocusSetting.ZstageLimitL},{_AutoFocusSetting.ZstageLimitH}]"), false);


                            if (retryCount == (maxRetries - 1))
                            {
                                OnStepRunUpdatedInvoke(step, "Try push potential bubble", false);
                                FlushBubble(region.Lane);
                            }
                        }

                    }
                }

                #endregion Re-focus with 2nd ROI
                if (_IsAutoFocusingSucceeded && !IsSimulationMode) //Update Cycle difference once after AF succeeded
                {
                    cyclediff[focusnum] = Math.Round((refFocus0 - region.ReferenceFocuses[focusorder[focusnum]].Position), 2);
                    region.ReferenceFocuses[focusorder[focusnum]].Position = refFocus0;
                    Logger.Log($"[{Thread.CurrentThread.Name}] [AF]: Autofocus Succeed at X:{_ImageInfo.MixChannel.XPosition}, Y:{_ImageInfo.MixChannel.YPosition}, Z:{_MotionController.ZCurrentPos}" +
                        $"Sharpness: {_FocusedSharpness:F}. Cycle Diff: {cyclediff[focusnum]:F}", SeqLogFlagEnum.DEBUG);
                    //Calculate Offset at first cycle fist region for both surface, if this feature is turned on
                    if (regionNum == 0 && loopInfo == "Inc1" && _IsReCalculateOffset)
                    {
                        if (_surface == "t")
                        {
                            double toffset = RecalculateFiducialOffset(T_Offset, refFocus0, step);
                            if (Math.Abs(toffset - T_Offset) < 5)
                            {
                                T_Offset = toffset;
                                _RecipeParameters.Top_Offset = T_Offset;
                                Logger.Log("Use new top offset");
                            }
                            RecalculateChannelOffset(step);
                        }
                        else
                        {
                            double boffset = RecalculateFiducialOffset(B_Offset, refFocus0, step);
                            if (Math.Abs(boffset - B_Offset) < 5)
                            {
                                B_Offset = boffset;
                                _RecipeParameters.Bottom_Offset = B_Offset;
                                Logger.Log("Use new bottom offset");
                            }
                        }
                    }
                }
                else
                {
                    double referenceFocusedPos = region.ReferenceFocuses[focusorder[focusnum]].Position;                    
                    if (Math.Abs(_MotionController.ZCurrentPos - referenceFocusedPos) >= 3)
                    {
                        skipcounts += 1;
                    }
                    if (_loopCount > 1)
                    {
                        refFocus0 = referenceFocusedPos + cyclediff[focusnum];
                    }
                    if (skipcounts > 5)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format($"Autofocus has failed {skipcounts} times. The Recipe will stop."), true);
                        AbortWork();
                        ExitStat = ThreadExitStat.Error;
                        throw new System.InvalidOperationException("Autofocus Failure");
                    }

                    OnStepRunUpdatedInvoke(step, ($"[{Thread.CurrentThread.Name}] [AF]: Autofocus failed at X:{_ImageInfo.MixChannel.XPosition}, Y:{_ImageInfo.MixChannel.YPosition}, Z:{_MotionController.ZCurrentPos}, ReferenceFocusedPos:{referenceFocusedPos}" +
                        $" AF message: {_AutoFocusErrorMessage}  Skipcounts: {skipcounts}. User Attention!"), true);
                }
                Logger.Log($"Autofocus elapsed time [ms]|{afStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
            }
            //Calculate the focus pos of regions that do not AF during the full scan cycle by average the adjcent.
            else if (currentcycleorder == AFregionorder && regionNum % 2 != 0)
            {
                OnStepRunUpdatedInvoke(step, ($"[{Thread.CurrentThread.Name}] [AF]: Capturing tile: {regionNum}. AF is using focus interpolation"), false);
                //Calculate the row number for each region that need calculate focus
                int previousregionrow = (step.Regions[regionNum - 1].Lane - 1) * SettingsManager.ConfigSettings.FCRow + step.Regions[regionNum - 1].Row; ;
                int nextregionrow = 0;
                int currentregionrow = (step.Regions[regionNum].Lane - 1) * SettingsManager.ConfigSettings.FCRow + step.Regions[regionNum].Row; ;
                if (regionNum < step.Regions.Count - 1)
                {
                    nextregionrow = (step.Regions[regionNum + 1].Lane - 1) * SettingsManager.ConfigSettings.FCRow + step.Regions[regionNum + 1].Row;
                }

                //TODO: put interpolation in its own method

                // The previous region and the next region are the same row as this region
                if (regionNum < step.Regions.Count - 1 && previousregionrow == nextregionrow)
                {
                    double interpolatedPosition = (step.Regions[regionNum - 1].ReferenceFocuses[focusorder[focusnum]].Position + step.Regions[regionNum + 1].ReferenceFocuses[focusorder[focusnum]].Position) / 2.0;
                    region.ReferenceFocuses[focusorder[focusnum]].Position = interpolatedPosition;
                }
                // Edge condition: region is located at first region of current row
                else if (regionNum < step.Regions.Count - 1 && previousregionrow < currentregionrow)
                {
                    // take the difference between the last two regions in the previous lane and add that to the focus of the next region
                    double temp = step.Regions[regionNum - 1].ReferenceFocuses[focusorder[focusnum]].Position - step.Regions[regionNum - 2].ReferenceFocuses[focusorder[focusnum]].Position
                        + step.Regions[regionNum + 1].ReferenceFocuses[focusorder[focusnum]].Position;
                    region.ReferenceFocuses[focusorder[focusnum]].Position = temp;
                }
                // Edge condtion, region is located at last region of current row
                else
                {
                    // double the previous position (r1) and subtract the position from two regions ago (r2) (equal to r1-r2+r1)
                    double temp = 2.0 * step.Regions[regionNum - 1].ReferenceFocuses[focusorder[focusnum]].Position - step.Regions[regionNum - 2].ReferenceFocuses[focusorder[focusnum]].Position;
                    region.ReferenceFocuses[focusorder[focusnum]].Position = temp;
                }
                refFocus0 = region.ReferenceFocuses[focusorder[focusnum]].Position;
            }
            else
            {
                Logger.Log($"Capturing tile: {regionNum}. Surface: {_surface}. Using reference focus: {refFocus0} + Cycle diff: {cyclediff[focusnum]}", SeqLogFlagEnum.DEBUG);
                refFocus0 = region.ReferenceFocuses[focusorder[focusnum]].Position + cyclediff[focusnum];
                region.ReferenceFocuses[focusorder[focusnum]].Position = refFocus0;
            }
            // ^end autofocus
            _IsAFing = false;
            int zshiftlimit = 25;
            if (region.Row == 1 && region.Column == 1)
            {
                zshiftlimit = 25;
            }
            // Update rest regions' focus reference
            if (_IsOneRef && _loopCount == 1 && regionNum + 1 < step.Regions.Count && Math.Abs(cyclediff[focusnum]) < zshiftlimit
                && ((currentcycleorder == AFregionorder && regionNum % 2 == 0) || currentcycleorder == regionorder))
            {
                for (int i = regionNum + 1; i < step.Regions.Count; i++)
                {
                    step.Regions[i].ReferenceFocuses[focusorder[focusnum]].Position = refFocus0;
                }
            }
            #endregion AF
            // ^end autofocus region
            //---------------------------------------------------------------------------------------------------------

            #region Surface intensity multiplier
            //if (_LoopCount == 1 && regionnum == 0 && focusnum == 1) // first cycle first bottom surface
            //{
            //    _IntThreshold[0] = 1500;
            //    isPassCheckByFluor = CheckImagebyFluro(step, LEDTypes.Red, (int)region.Imagings[1].RedIntensity ,
            //            Math.Round((region.Imagings[1].RedExposureTime * Math.Pow(_Expoinc, _LoopCount - 1 + _ExpoControlFactor)), 3)
            //            , focusorder[focusnum], regionnum, _IntThreshold, true);
            //    if (!isPassCheckByFluor[1])
            //    {
            //        OnStepRunUpdatedInvoke(step, "Intensity too low at bottom suface, double exposure", true);
            //        SurfaceMulitplier = 2;
            //        isPassCheckByFluor = CheckImagebyFluro(step, LEDTypes.Red, (int)region.Imagings[1].RedIntensity,
            //            Math.Round((region.Imagings[1].RedExposureTime * Math.Pow(_Expoinc, _LoopCount - 1 + _ExpoControlFactor)), 3)
            //            , focusorder[focusnum], regionnum, _IntThreshold, true);
            //        if (!isPassCheckByFluor[1])
            //        {
            //            OnStepRunUpdatedInvoke(step, "Intensity too low at bottom suface, triple exposure", true);
            //            SurfaceMulitplier = 3;
            //        }
            //    }
            //}
            #endregion Surface intensity multiplier
            #region 3. config Image settings (ROI, binning, gain, Pixel Format, Depth)
            _ImageSetting = new ImageChannelSettings();
            _ImageSetting.AdGain = 10;
            _ImageSetting.BinningMode = SettingsManager.ConfigSettings.CameraDefaultSettings.BinFactor;
            _ImageSetting.IsCaptureFullRoi = false;
            _ImageSetting.IsEnableBadImageCheck = true;
            DateTime dateTime = DateTime.Now;
            _ImageInfo.DateTime = System.String.Format("{0:G}", dateTime.ToString());
            _ImageInfo.BinFactor = _ImageSetting.BinningMode;
            _ImageInfo.GainValue = _ImageSetting.AdGain;
            #endregion Config camera setting
            double zPos = refFocus0;
            //1.move z stage to the reference position
            if (focusnum == 0)
            {
                zPos = refFocus0 - T_Offset;
            }
            else if (focusnum == 1)
            {
                zPos = refFocus0 - B_Offset;
            }
            double zPosc = zPos;

            //////// Capture Tile Images
            // 2. capture all images in the region at the same focus
            for (int imagingIndex = 0; imagingIndex < region.Imagings.Count; imagingIndex++)
            {
                if (IsAbort)
                {
                    break;
                }
                var imaging = region.Imagings[imagingIndex];
                if (imaging.Channels == ImagingChannels.Red)
                {
                    zPosc = zPos + SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset;
                }
                else
                {
                    zPosc = zPos;
                }
                if (!IsSimulationMode)
                {
                    _MotionController.AbsoluteMoveZStage(zPosc,
                    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed,
                    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel,
                    false);
                }
                InitializeCameras(step, _camera1, _camera2, initialexpo);
                //Thread.Sleep(10);   // wait for stage stop moving
                // 1. Select Filter
                int ChannelIndex = 0;
                switch (imaging.Filter)
                {
                    case FilterTypes.Filter1:
                        ChannelIndex = 1;
                        break;
                    case FilterTypes.Filter2:
                        ChannelIndex = 2;
                        break;
                    case FilterTypes.Filter3:
                        ChannelIndex = 3;
                        break;
                    case FilterTypes.Filter4:
                        ChannelIndex = 4;
                        break;
                }
                // 2. Set Capturing parameters and image metadata
                if (imaging.Channels != ImagingChannels.RedGreen)
                {
                    // 1. set led and exposure
                    switch (imaging.Channels)       // ignore RedGreen Channel
                    {
                        case ImagingChannels.Green:
                            _ImageSetting.LED = LEDTypes.Green;
                            _ImageSetting.LedIntensity = imaging.GreenIntensity;
                            _OffsetGreenLEDInt = imaging.GreenIntensity;
                            _ImageSetting.Exposure = imaging.GreenExposureTime;
                            break;
                        case ImagingChannels.Red:
                            _ImageSetting.LED = LEDTypes.Red;
                            _ImageSetting.LedIntensity = imaging.RedIntensity;
                            _OffsetRedLEDInt = imaging.RedIntensity;
                            _ImageSetting.Exposure = imaging.RedExposureTime;
                            break;
                        case ImagingChannels.White:
                            _ImageSetting.LED = LEDTypes.White;
                            _ImageSetting.LedIntensity = imaging.WhiteIntensity;
                            _ImageSetting.Exposure = imaging.WhiteExposureTime;
                            break;
                    }
                    // increase intensity and expo For long cycle
                    if (_loopCount != 0)
                    {
                        _ImageSetting.Exposure = Math.Round((_ImageSetting.Exposure * Math.Pow(_Expoinc, _loopCount - 1 + _ExpoControlFactor)), 3);
                        if (_ImageSetting.LED == LEDTypes.Green)
                        {
                            _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * Math.Pow(_GLEDinc, _loopCount - 1)));
                        }
                        if (_ImageSetting.LED == LEDTypes.Red)
                        {
                            _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * Math.Pow(_RLEDinc, _loopCount - 1)));
                        }
                    }
                    else
                    {
                        _ImageSetting.Exposure = Math.Round(_ImageSetting.Exposure * _Expoinc, 3);
                        if (_ImageSetting.LED == LEDTypes.Green)
                        {
                            _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * _GLEDinc));
                        }
                        if (_ImageSetting.LED == LEDTypes.Red)
                        {
                            _ImageSetting.LedIntensity = (uint)Math.Round((_ImageSetting.LedIntensity * _RLEDinc));
                        }
                    }
                    // increase exposure time for bottom surface 
                    if (_surface == "b") { _ImageSetting.LedIntensity *= (uint)SurfaceMulitplier; }
                    bool bStabilized = true;
                    const int MaxRetyPDValueCounts = 5;
                    if (!IsSimulationMode)
                    {
                        _LEDController.SetLEDControlledByCamera(_ImageSetting.LED, true);
                        _LEDController.SetLEDIntensity(_ImageSetting.LED, (int)_ImageSetting.LedIntensity);
                        #region wait stage stablized
                        int waitTime = 0;
                        bool xBusy = true;
                        bool yBusy = true;
                        bool zbusy = true;
                        //bool bStabilized = true;
                        //const int MaxRetyPDValueCounts = 5;
                        Stopwatch stageStableStopwatch = Stopwatch.StartNew();
                        do
                        {
                            if (IsAbort)
                            {
                                break;
                            }
                            if (++waitTime > 1200)
                            {
                                OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                        $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, " +
                                        $"Z current pos:{_MotionController.ZCurrentPos} status:{zbusy}, retry."), true);
                                //retry one last time with wait=true
                                if (_MotionController.AbsoluteMoveZStage(zPosc, SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed,
                                    SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel, false) ||
                                    //move Y stage
                                    _MotionController.AbsoluteMove(MotionTypes.YStage, YtargetPos, Yspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel *
                                    SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]), true, true) ||
                                    // move X stage
                                    _MotionController.AbsoluteMove(MotionTypes.XStage, XtargetPos, Xspeed, (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel *
                                    SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]), true, true))
                                {
                                    OnStepRunUpdatedInvoke(step, string.Format($"Waiting stage stable failed X current pos:{_MotionController.FCurrentPos}, status:{xBusy}," +
                                        $" Y current pos:{_MotionController.YCurrentPos} status:{yBusy}, " +
                                        $"Z current pos:{_MotionController.ZCurrentPos} status:{zbusy}, recipe stop."), true);
                                    AbortWork();
                                    ExitStat = ThreadExitStat.Error;
                                    throw new System.InvalidOperationException("Waiting stage stable Failure");
                                }
                            }
                            _MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y);
                            xBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy;
                            yBusy = _MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy;
                            _MotionController.FCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_X];
                            _MotionController.YCurrentPos = _MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_Y];
                            if (Math.Abs(_MotionController.ZCurrentPos - zPosc) < 0.1) { zbusy = false; }

                            if (IsSimulationMode)
                            {
                                Thread.Sleep(500);
                                break;
                            }
                            else
                            {
                                Thread.Sleep(10);
                            }
                        }
                        while (xBusy || yBusy || zbusy);
                        Logger.Log($"Stage stabilization wait time [ms]|{stageStableStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                    }

                    if (IsSimulationMode)
                    {
                        Logger.LogMessage(bStabilized ? "Stages are stable (in simulation mode)" : "Stages are not stable (in simulation mode)");
                    }
                    else
                    {
                        Logger.LogMessage(bStabilized ? "Stages are stable" : "Stages are not stable");
                    }
                    #endregion wait stages stablized

                    #region Captured Image Info
                    _ImageInfo.MixChannel.YPosition = Math.Round(_MotionController.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 2);
                    _ImageInfo.MixChannel.XPosition = Math.Round(_MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 2);
                    _ImageInfo.MixChannel.LightIntensity = (int)_ImageSetting.LedIntensity;
                    _ImageInfo.MixChannel.Exposure = _ImageSetting.Exposure;
                    _ImageInfo.MixChannel.LightSource = _ImageSetting.LED.ToString();
                    Int32Rect roiRect;
                    if (IsSimulationMode && _camera1 == null)
                    {
                        roiRect = new Int32Rect(0, 0, 4300, 4200);
                    }
                    else
                    {
                        roiRect = new Int32Rect(_camera1.RoiStartX, _camera1.RoiStartY, _camera1.RoiWidth, _camera1.RoiHeight);
                    }
                    _ImageInfo.MixChannel.ROI = roiRect;
                    _ImageInfo.MixChannel.FilterPosition = ChannelIndex;
                    _ImageInfo.MixChannel.IsAutoFocus = step.IsAutoFocusOn;
                    _ImageInfo.MixChannel.FocusPosition = _MotionController.ZCurrentPos;
                    string ledn = (_ImageSetting.LED == LEDTypes.Green) ? "G" : "R";
                    #endregion Captured Image Info
                    // 2. Capture image-----------------------------------------------------------------
                    #region capture image
                    int PDtryCounts = 0;
                    int newImageId = -1;
                    int ExpotryCounts = 0;
                    if (!IsSimulationMode)
                    {
                        do //for _PDValue < 100
                        {
                            Stopwatch imageReadoutStopwatch = Stopwatch.StartNew();
                            // Wait Last round readout finish
                            WaitForImagesReadout();
                            Logger.Log($"Image readout wait time [ms]|{imageReadoutStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                            if (IsAbort)
                            {
                                break;
                            }
                            //Reconnect cameras if null image count equal 15 
                            if (NullImageCounts >= 15 && !isReconnectedForNullImage)
                            {
                                Logger.LogWarning($"Null image count equal to 15, Reconnect Cameras");
                                isReconnectedForNullImage = true;
                                double exp = ((LucidCamera)_camera1).Exposure;
                                UnInitializeCameras(step, _camera1, _camera2);
                                ReOpenCamera(_camera1);
                                ReOpenCamera(_camera2);
                                Logger.LogMessage("Reinitialize cameras");
                                InitializeCameras(step, _camera1, _camera2, exp);
                            }
                            newImageId = GetNextImageId();
                            Logger.LogMessage($"Camera finish readout ready to exposure next with new Image(ID:{newImageId})");

                            lock (ImagingState1)
                            {
                                ImagingState1.IsCameraExposureEnd = false;
                                ImagingState1.IsExposureSuccess = false;
                            }
                            lock (ImagingState2)
                            {
                                ImagingState2.IsCameraExposureEnd = false;
                                ImagingState2.IsExposureSuccess = false;
                            }
                            //prepare for next images
                            //Change Exposure time accordingly
                            _camera1.SetExposure(_ImageSetting.Exposure * 1000);
                            _camera2.SetExposure(_ImageSetting.Exposure * 1000);
                            if (!_camera1.WaitTriggerArmed() || !_camera2.WaitTriggerArmed())
                            {
                                OnStepRunUpdatedInvoke(step, "Failed to arm trigger", true);
                                AbortWork();
                            }
                            //while (_LEDController.SendCameraTrigger() == false)
                            //{
                            //    //apply exposure changes and trig imaging ?
                            //    Thread.Sleep(1);
                            //    if (IsAbort)
                            //    {
                            //        break;
                            //    }
                            //}
                            if (_LEDController.SendCameraTrigger() == false)
                            {
                                Thread.Sleep(10);
                                if (_LEDController.SendCameraTrigger() == false)
                                {
                                    OnStepRunUpdatedInvoke(step, "SendCameraTrigger Failed, Recipe stop", true);
                                    AbortWork();
                                    ExitStat = ThreadExitStat.Error;
                                    throw new System.InvalidOperationException("SendCameraTrigger Failure");
                                }
                            }

                            //trig to camera to set exposure and  grab a image from camera continuous mode 
                            _camera1.ImageId = newImageId;
                            _camera2.ImageId = newImageId;
                            _camera1.TriggeredByOutside = true;
                            _camera2.TriggeredByOutside = true;
                            //Wait Exposure end
                            WaitforCameraExposureEnd();

                            _LEDController.GetPDSampledValue();
                            _PDValue = (int)_LEDController.PDSampleValue;
                            Logger.LogMessage($"Read PD = {_PDValue}, Image(ID:{newImageId})");
                            _ImageInfo.MixChannel.PDValue = _PDValue;
                            if (_PDValue < MinPDValue)
                            {
                                #region wait readout without saving
                                bool isreadout1 = false;
                                bool isreadout2 = false;
                                do
                                {
                                    if (IsAbort)
                                    {
                                        Logger.LogMessage($"Abort Queue Image: Camera-{newImageId}");
                                        break;
                                    }
                                    lock (ImagingState1)
                                    {
                                        if (ImagingState1.ImageListByID.ContainsKey(newImageId) && !isreadout1)
                                        {
                                            OnStepRunUpdatedInvoke(step, "Low PD Reading, Camera1 readout done, retry", false);
                                            isreadout1 = true;
                                            string errimgname = $"Camera1_PD0.tif";
                                            string filename = _RecipeRunImageDataDir + "\\" + errimgname;
                                            int imagenum = 0;
                                            string imageFileName = filename;
                                            while (File.Exists(imageFileName))
                                            {
                                                imagenum += 1;
                                                imageFileName = filename.Replace(filename.Substring(filename.Length - 4), string.Format("_{0}{1}",
                                                    imagenum, filename.Substring(filename.Length - 4)));
                                            }
                                            _ImageInfo.MixChannel.BitDepth = 16;
                                            ImageProcessing.Save(imageFileName, ImagingState1.ImageListByID[newImageId], _ImageInfo, false);
                                        }
                                    }
                                    Thread.Sleep(1);
                                    lock (ImagingState2)
                                    {
                                        if (ImagingState2.ImageListByID.ContainsKey(newImageId) && !isreadout2)
                                        {
                                            OnStepRunUpdatedInvoke(step, "Low PD Reading, Camera2 readout done, retry", false);
                                            isreadout2 = true;
                                            string errimgname = $"Camera2_PD0.tif";
                                            string filename = _RecipeRunImageDataDir + "\\" + errimgname;
                                            int imagenum = 0;
                                            string imageFileName = filename;
                                            while (File.Exists(imageFileName))
                                            {
                                                imagenum += 1;
                                                imageFileName = filename.Replace(filename.Substring(filename.Length - 4), string.Format("_{0}{1}",
                                                    imagenum, filename.Substring(filename.Length - 4)));
                                            }
                                            _ImageInfo.MixChannel.BitDepth = 16;
                                            ImageProcessing.Save(imageFileName, ImagingState2.ImageListByID[newImageId], _ImageInfo, false);
                                        }
                                    }
                                    Thread.Sleep(1);
                                    if (isreadout1 && isreadout2)
                                    {
                                        ImagingState1.ImageListByID.Clear();
                                        ImagingState2.ImageListByID.Clear();
                                        break;
                                    }
                                } while (true);
                                #endregion wait readout without saving
                                PDtryCounts++;

                            }
                            if (PDtryCounts > 2)
                            {
                                OnStepRunUpdatedInvoke(step, "PD Abnoraml more than 2 times. reconnect LED Contorller.", false);
                                _LEDController.DisConnect();
                                OnStepRunUpdatedInvoke(step, "LED Controller disconnected.", false);
                                Thread.Sleep(1000);
                                OnStepRunUpdatedInvoke(step, "LED Controller Reconnecting...", false);
                                if (_LEDController.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.LEDController].PortName))
                                {
                                    OnStepRunUpdatedInvoke(step, "LED Reconnect Succeeded.", false);
                                }
                                else if (_LEDController.Connect())
                                {
                                    OnStepRunUpdatedInvoke(step, "LED Reconnect Succeeded.", false);
                                }
                                else
                                {
                                    OnStepRunUpdatedInvoke(step, "LED Reconnect Uusucceeded.", true);
                                    OnStepRunUpdatedInvoke(step, "Failed to reconnect LED, Recipe stop", true);
                                    AbortWork();
                                    ExitStat = ThreadExitStat.Error;
                                    throw new System.InvalidOperationException("LED Failure");
                                }
                            }
                            if (PDtryCounts > MaxRetyPDValueCounts)
                            {
                                OnStepRunUpdatedInvoke(step, "Failed to Turn on LED, Recipe stop", true);
                                AbortWork();
                                ExitStat = ThreadExitStat.Error;
                                throw new System.InvalidOperationException("LED Failure");
                            }
                            // wait for exposure failed
                            if (!ImagingState2.IsExposureSuccess || !ImagingState1.IsExposureSuccess)
                            {
                                OnStepRunUpdatedInvoke(step, "Failed to wait for exposure, restart camera", true);
                                ExpotryCounts++;
                                UnInitializeCameras(step, _camera1, _camera2);
                                Thread.Sleep(1000);
                                InitializeCameras(step, _camera1, _camera2, _ImageSetting.Exposure);
                            }
                            if (ExpotryCounts > 5)
                            {
                                OnStepRunUpdatedInvoke(step, "Failed to Wait exposure, Recipe stop", true);
                                AbortWork();
                                ExitStat = ThreadExitStat.Error;
                                throw new System.InvalidOperationException("Camera Failure");
                            }
                        }
                        while (_PDValue < MinPDValue || !ImagingState2.IsExposureSuccess || !ImagingState1.IsExposureSuccess);
                    }
                    else //simulation mode
                    {
                        var rand = new Random();
                        do //for _PDValue < 100
                        {
                            // Wait Last round readout finish
                            WaitForImagesReadout();
                            if (IsAbort)
                            {
                                break;
                            }
                            newImageId = GetNextImageId();
                            Logger.LogMessage($"Simulation: Camera finish readout ready to exposure next with new Image(ID:{newImageId})");

                            lock (ImagingState1)
                            {
                                ImagingState1.IsCameraExposureEnd = false;
                                ImagingState1.IsExposureSuccess = false;
                            }
                            lock (ImagingState2)
                            {
                                ImagingState2.IsCameraExposureEnd = false;
                                ImagingState2.IsExposureSuccess = false;
                            }

                            //creating two tasks to simulate exposure setting and imaging
                            int n = _ImageInfo.MixChannel.ROI.Width * _ImageInfo.MixChannel.ROI.Height * 2 - 1;
                            int newImageIdCopy = newImageId; //must make a copy because task may start later
                            Task.Factory.StartNew(() =>
                            {
                                SimulateCameraExposureAndImaging(ImagingState1, _ImageSetting.Exposure, newImageIdCopy, n);
                            }).ContinueWith(
                               (o) => { Logger.LogMessage($"Simulation: Camera-1 exposure and image notification completed"); });

                            Task.Factory.StartNew(() =>
                            {
                                SimulateCameraExposureAndImaging(ImagingState2, _ImageSetting.Exposure, newImageIdCopy, n);
                            }).ContinueWith(
                            (o) => { Logger.LogMessage($"Simulation: Camera-2 exposure and image notification completed"); });

                            WaitforCameraExposureEnd();

                            _PDValue = rand.Next(88, 150);
                            _ImageInfo.MixChannel.PDValue = _PDValue;
                            Logger.LogMessage($"Simulation: Read PD = {_PDValue}, Image(ID:{newImageId})");
                            if (_PDValue < MinPDValue)
                            {
                                //if need retake image, have to wait the read out finish
                                PDtryCounts++;

                            }
                            if (PDtryCounts > MaxRetyPDValueCounts)
                            {
                                _PDValue = MinPDValue;
                            }
                        } while (_PDValue < MinPDValue && !ImagingState2.IsExposureSuccess && !ImagingState1.IsExposureSuccess);
                    }//end simulation else
                    #endregion Capture image
                    //----------------------------------------------------------------------------------------------------
                    if (!IsAbort && newImageId > 0)
                    {
                        string locationinfo = _surface + "L" + region.Lane + string.Format("{0:00}", region.Column) + Convert.ToChar(64 + region.Row).ToString();
                        double zCurrentPos = (IsSimulationMode ? zPosc : _MotionController.ZCurrentPos);
                        AcquiredImageData[] imageDataArray = new AcquiredImageData[2];
                        for (int i = 0; i < 2; i++)
                        {
                            ILucidCamera camera = (i == 0) ? _camera1 : _camera2;
                            Image.Processing.ImageInfo imageinfo = (Image.Processing.ImageInfo)_ImageInfo.Clone();
                            imageinfo.CameraSerialNumber = IsSimulationMode ? "Simulation" : ((ILucidCamera)camera).SerialNumber;
                            imageinfo.ImagingChannel = IsSimulationMode ? "SIM" : ((ILucidCamera)camera).Channels;
                            int channelinfo = ChannelIndex; // is a copy of channelIndex necessary?

                            // One camera has G1&R3, the other has G2&R4.
                            // Assign channel info (1,2,3,4):
                            // G on G1/R3 does contain 1 --> stays G1
                            // G on G2/R4 does not contain 1 --> G2
                            // R on G1/R3 does contain 3 --> stays R3
                            // R on G2/R4 does not contain 3 --> R4
                            if ((camera != null && !camera.Channels.Contains(channelinfo.ToString())))
                            {
                                if (ledn == "G" && channelinfo == 1)
                                {
                                    channelinfo = 2;
                                }
                                else if (ledn == "G" && channelinfo == 2)
                                {
                                    channelinfo = 1;
                                }

                                if (ledn == "R" && channelinfo == 3)
                                {
                                    channelinfo = 4;
                                }
                                else if (ledn == "R" && channelinfo == 4)
                                {
                                    channelinfo = 3;
                                }

                                imageinfo.MixChannel.FilterPosition = channelinfo;
                            }
                            // For simulation mode, all images will arrive as G1 or R3
                            // They will also have the same newImageId (starts at 1 and increments once for each set of two images)
                            // So, use the loop counter to make one image G2 and one R4
                            else if (IsSimulationMode)
                            {
                                int[] channels = { 1, 2, 3, 4 }; // for G1, G2, R3, R4
                                int channelIndex = i; // i goes from [0 to 1]
                                if (ledn == "R")
                                {
                                    channelIndex += 2;
                                }
                                channelinfo = channels[channelIndex];
                                imageinfo.MixChannel.FilterPosition = channelinfo;
                            }
                            string imagename = string.Format("{0}_{1}_{2}{3}_{4}_X{5:00.00}Y{6:00.00}mm_{7:F2}um_{8:F3}s_{9}Int_PD{10}.tif",
                                        Recipe.RecipeName, loopInfo, ledn, channelinfo, locationinfo,
                                        imageinfo.MixChannel.XPosition, imageinfo.MixChannel.YPosition,
                                        zCurrentPos, _ImageSetting.Exposure,
                                        _ImageSetting.LedIntensity, _PDValue);
                            imageDataArray[i] = new AcquiredImageData()
                            {
                                Camera = camera,
                                CameraIndex = i + 1,
                                Step = step,
                                ImageDataArray = null,
                                Imageinfo = imageinfo,
                                ImageName = imagename,
                                TryCounts = PDtryCounts,
                                LoopCount = _loopCount,
                                LEDFailure = _LEDFailure,
                                BadImageCounts = BadImageCounts,
                                NullImageCounts = NullImageCounts,
                                RecipeRunImageDataDir = _RecipeRunImageDataDir,
                                PDValue = _PDValue,
                                ImageId = newImageId,
                                BackupPath = _NasFolder,
                                Locationinfo = locationinfo,
                                RegionIndex = regionNum
                            };

                        }
                        lock (ImagingState1)
                        {
                            ImagingState1.QueueImageTaskCount = ImagingState1.QueueImageTaskCount + 1;
                        }
                        lock (ImagingState2)
                        {
                            ImagingState2.QueueImageTaskCount = ImagingState2.QueueImageTaskCount + 1;
                        }
                        Task.Factory.StartNew(() => QuequeImage(ImagingState1, imageDataArray[0])).ContinueWith(
                                (o) =>
                                {
                                    int count;
                                    lock (ImagingState1)
                                    {
                                        ImagingState1.QueueImageTaskCount = ImagingState1.QueueImageTaskCount - 1;
                                        count = ImagingState1.QueueImageTaskCount;
                                    }
                                    Logger.LogMessage($"Loop{loopInfo}: Camera-1 image is queued, total imaging queue task count= {count}");
                                });

                        Task.Factory.StartNew(() => QuequeImage(ImagingState2, imageDataArray[1])).ContinueWith(
                                (o) =>
                                {
                                    int count;
                                    lock (ImagingState2)
                                    {
                                        ImagingState2.QueueImageTaskCount = ImagingState2.QueueImageTaskCount - 1;
                                        count = ImagingState2.QueueImageTaskCount;
                                    }
                                    Logger.LogMessage($"Loop{loopInfo}: Camera-2 image is queued, total imaging queue task count= {count}");
                                });
                    }
                    if (!IsSimulationMode)
                        _LEDController.SetLEDControlledByCamera(_ImageSetting.LED, false);
                    CheckForImageQSize();
                } //if
            }//for  imagingIndex
            ////////
        }
        private double RecalculateFiducialOffset(double offset, double fiducialfocus, ImagingStep step)
        {
            try
            {
                ILucidCamera AFcamera = _camera1;
                if (!IsSimulationMode)
                {
                    AFcamera = _camera1.Channels.Contains("1") ? _camera1 : _camera2;
                }
                double previouoffset = offset;
                AutoFocusSettings OffsetAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                Int32Rect offsetroi = new Int32Rect();
                offsetroi.X = 1800;
                offsetroi.Y = 1800;
                offsetroi.Width = 800;
                offsetroi.Height = 800;
                OffsetAFSettings.ROI = offsetroi;
                OffsetAFSettings.ZstageLimitH = fiducialfocus + 6 - previouoffset;
                OffsetAFSettings.ZstageLimitL = fiducialfocus - 6 - previouoffset;
                OffsetAFSettings.LEDType = OffsetAFSettings.OffsetLEDType;
                OffsetAFSettings.Reference0 = fiducialfocus - offset;
                if (OffsetAFSettings.LEDType == LEDTypes.Green)
                {
                    OffsetAFSettings.LEDIntensity = step.Regions[0].Imagings[0].GreenIntensity;
                    OffsetAFSettings.ExposureTime = step.Regions[0].Imagings[0].GreenExposureTime;

                }
                else if (OffsetAFSettings.LEDType == LEDTypes.Red)
                {
                    OffsetAFSettings.LEDIntensity = step.Regions[0].Imagings[0].RedIntensity;
                    OffsetAFSettings.ExposureTime = step.Regions[0].Imagings[0].RedExposureTime;
                }
                _AutoFocusFluoProcess = new AutofocusOnFluoV2(_CallingDispatcher, _MotionController, AFcamera, _LEDController, OffsetAFSettings, fiducialfocus);
                _AutoFocusFluoProcess.IsSimulationMode = IsSimulationMode;
                _AutoFocusFluoProcess.Completed += _AutoFocusFluoProcess_Completed;
                _AutoFocusFluoProcess.Start();
                _AutoFocusFluoProcess.Join();
                if (_IsOffsetCalSucc)
                {
                    OnStepRunUpdatedInvoke(step, $"Recalculate offset succeed, use calculated offset {_CalculatedOffset:F}", false);
                    return _CalculatedOffset;
                }
                else
                {
                    OnStepRunUpdatedInvoke(step, $"Recalculate offset failed, use pervious offset {offset:F}", true);
                    return previouoffset;
                }
            }
            catch (Exception ex)
            {
                OnStepRunUpdatedInvoke(step, ex.ToString(), true);
                return offset;
            }

        }
        private void RecalculateChannelOffset(ImagingStep step)
        {
            try
            {
                ILucidCamera AFcamera = _camera1;
                if (!IsSimulationMode)
                {
                    AFcamera = _camera1.Channels.Contains("1") ? _camera1 : _camera2;
                }
                AutoFocusSettings OffsetAFSettings = new AutoFocusSettings(SettingsManager.ConfigSettings.AutoFocusingSettings);
                Int32Rect offsetroi = new Int32Rect();
                offsetroi.X = 1800;
                offsetroi.Y = 1800;
                offsetroi.Width = 800;
                offsetroi.Height = 800;
                OffsetAFSettings.ROI = offsetroi;
                OffsetAFSettings.ZstageLimitH = _MotionController.ZCurrentPos + 6;
                OffsetAFSettings.ZstageLimitL = _MotionController.ZCurrentPos - 6;
                OffsetAFSettings.Reference0 = _MotionController.ZCurrentPos;
                OffsetAFSettings.LEDIntensity = step.Regions[0].Imagings[0].GreenIntensity;
                OffsetAFSettings.ExposureTime = step.Regions[0].Imagings[0].GreenExposureTime;
                _AutoFocusFluoProcess = new AutoFocusChannelOffset(_CallingDispatcher, _MotionController, AFcamera, _LEDController, OffsetAFSettings);
                _AutoFocusFluoProcess.IsSimulationMode = IsSimulationMode;
                _AutoFocusFluoProcess.Completed += _ChannelOffsetProcess_Completed;
                _AutoFocusFluoProcess.Start();
                _AutoFocusFluoProcess.Join();
                if (_IsOffsetCalSucc)
                {
                    OnStepRunUpdatedInvoke(step, $"Recalculate channel offset succeed, use calculated offset {SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset:F}", false);
                    //return _CalculatedOffset;
                }
                else
                {
                    OnStepRunUpdatedInvoke(step, $"Recalculate channel offset failed, use pervious offset {SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset:F}", true);
                    //return SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset;
                }
            }
            catch (Exception ex)
            {
                OnStepRunUpdatedInvoke(step, ex.ToString(), true);
            }
        }
        /// <summary>
        /// Attempts to remove bubbles from the FC. Configures the valves and pumps 80 uL
        /// </summary>
        /// <param name="lane"> The FC lane to flush [1,4] </param>
        private void FlushBubble(in int lane)
        {
            Stopwatch bubbleFlushStopwatch = Stopwatch.StartNew();
            PumpingSettings pumpSettings = new PumpingSettings();
            pumpSettings.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
            pumpSettings.PullRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.AspRate;
            pumpSettings.PumpingVolume = 80;
            pumpSettings.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
            pumpSettings.SelectedMode = ModeOptions.AspirateDispense;
            pumpSettings.SelectedSolution = new ValveSolution() { ValveNumber = 18 };
            pumpSettings.SelectedPullPath = PathOptions.FC;
            pumpSettings.SelectedPushPath = PathOptions.Waste;
            pumpSettings.SelectedPullValve2Pos = 6;
            pumpSettings.SelectedPullValve3Pos = 1;
            pumpSettings.SelectedPushValve2Pos = 6;
            pumpSettings.SelectedPushValve3Pos = 1;
            for (int i = 0; i < 4; i++)
            {
                if (i + 1 == lane) { pumpSettings.PumpPullingPaths[i] = true; } else { pumpSettings.PumpPullingPaths[i] = false; }
                pumpSettings.PumpPushingPaths[i] = false;
            }
            FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, pumpSettings, true, IsSimulationMode);
            if (!IsSimulationMode)
            {
                FluidicsInterface.WaitForPumpingCompleted();
            }
            Logger.Log($"Bubble flush elapsed time [ms]|{bubbleFlushStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
        }
        #endregion Imaging step helper function
        #region AF cmd thread complete event
        private void AutoFocusProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            //_IsAutoFocusing = false;
            if (exitState == ThreadExitStat.None)
            {
                _IsAutoFocusingSucceeded = true;
                _FocusedSharpness = _AutoFocusProcess.FoucsedSharpness;
                if (!IsSimulationMode)
                {
                    double shpTH;
                    double shpTL;
                    bool IsShpThresholdChange;
                    string errordetail = string.Empty;
                    double cfgTopStdLmtH = SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH;
                    double cfgTopStdLmtL = SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL;
                    double cfgBottomStdLmtL = SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL;
                    if (_surface == "t")
                    {                                                
                        shpTL = TopSharpness * 0.45; shpTH = TopSharpness * 2; IsShpThresholdChange = IsTopShpThresholdChange;
                        if (IsTopShpThresholdChange && (_FocusedSharpness < shpTL || _FocusedSharpness > shpTH))
                        {
                            errordetail = $"Surface:{_surface} FocusedSharpness:{_FocusedSharpness} <> [{shpTL}, {shpTH}]";
                        }
                        else if (_FocusedSharpness > cfgTopStdLmtH || _FocusedSharpness < cfgTopStdLmtL)
                        {
                            errordetail = $"Surface:{_surface} FocusedSharpness:{_FocusedSharpness} <> [{cfgTopStdLmtL}, {cfgTopStdLmtH}]";
                        }

                        if (errordetail != string.Empty)
                        {
                            _IsAutoFocusingSucceeded = false;
                            _AutoFocusErrorMessage = $"Sharpness OOB({errordetail}).";
                        }
                        else
                        {
                            if (!IsTopShpThresholdChange) 
                            { 
                                TopSharpness = _FocusedSharpness;
                                IsTopShpThresholdChange = true;
                            }
                        }
                    }
                    else
                    {
                        if (_FocusedSharpness > cfgTopStdLmtH || _FocusedSharpness < cfgBottomStdLmtL)
                        {
                            _IsAutoFocusingSucceeded = false;
                            _AutoFocusErrorMessage = $"Sharpness OOB(Surface:{_surface} FocusedSharpness:{_FocusedSharpness} <> [{cfgBottomStdLmtL}, {cfgTopStdLmtH}]).";
                        }
                    }

                    #region old_logic
                    //if (_surface == "t")
                    //{
                    //    shpTL = TopSharpness * 0.45; shpTH = TopSharpness * 2; IsShpThresholdChange = IsTopShpThresholdChange;
                    //    //if sharpness of top surface OOB
                    //    if (_FocusedSharpness < shpTL || (_FocusedSharpness > shpTH && IsShpThresholdChange) ||
                    //        _FocusedSharpness > SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH || _FocusedSharpness < SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL)
                    //    {
                    //        _IsAutoFocusingSucceeded = false;
                    //        _AutoFocusErrorMessage = string.Format("Sharpness OOB. Sharpness:{7} Surface:{0}, TopSharpness:{1}, BottomSharpness:{2}, sharpness range[{3}, {4}]," +
                    //            "IsTopShpThresholdChange:{5}, IsBomShpThresholdChange:{6}", _surface, TopSharpness, BottomSharpness, shpTL, shpTH,
                    //            IsTopShpThresholdChange, IsBomShpThresholdChange, _FocusedSharpness);
                    //    }
                    //    else
                    //    {
                    //        if (_surface == "t" && !IsTopShpThresholdChange) { TopSharpness = _FocusedSharpness; }
                    //        //else if (_surface == "b" && !IsBomShpThresholdChange) { BottomSharpness = _FocusedSharpness; }
                    //        if (TopSharpness != SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtL) { IsTopShpThresholdChange = true; }
                    //        //if (BottomSharpness != SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL) { IsBomShpThresholdChange = true; }
                    //    }
                    //}
                    //else
                    //{
                    //    if (_FocusedSharpness > SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH ||
                    //        _FocusedSharpness < SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL)
                    //    {
                    //        _IsAutoFocusingSucceeded = false;
                    //        _AutoFocusErrorMessage = string.Format("Sharpness OOB. Sharpness:{0} Surface:{1} Sharpness range[{2}, {3}]", _FocusedSharpness,
                    //            _surface, SettingsManager.ConfigSettings.AutoFocusingSettings.BottomStdLmtL, SettingsManager.ConfigSettings.AutoFocusingSettings.TopStdLmtH);
                    //    }
                    //}
                    ////else { shpTL = BottomSharpness * 0.45; shpTH = BottomSharpness * 2; IsShpThresholdChange = IsBomShpThresholdChange; }
                    #endregion old_logic

                }
                _AutoFocustrycount = _AutoFocusProcess.TryCounts;

            }
            else
            {
                _IsAutoFocusingSucceeded = false;
                _IsFailedCaptureAImage = _AutoFocusProcess.IsFailedCaptureImage;
                _FocusedSharpness = _AutoFocusProcess.FoucsedSharpness;
                _IsFailedtoSetALED = _AutoFocusProcess.IsFailedToSetLED;
                _AutoFocustrycount = _AutoFocusProcess.TryCounts;
                _AutoFocusErrorMessage = _AutoFocusProcess.ExceptionMessage;
            }
            _AutoFocusProcess.Completed -= AutoFocusProcess_Completed;
            _AutoFocusProcess = null;
        }
        private void AutoFocusNFProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            //_IsAutoFocusing = false;
            if (exitState == ThreadExitStat.None)
            {
                _IsAutoFocusingSucceeded = true;
                _FocusPattern = _AutoFocusNFProcess.NFMetricPatterns;
                _FocusedSharpness = _AutoFocusNFProcess.FoucsedSharpness;
                _AutoFocustrycount = _AutoFocusNFProcess.TryCounts;
                _Threshold = _AutoFocusNFProcess.Threshold;
            }
            else
            {
                _IsAutoFocusingSucceeded = false;
                _IsFailedCaptureAImage = _AutoFocusNFProcess.IsFailedCaptureImage;
                _IsFailedtoSetALED = _AutoFocusNFProcess.IsFailedToSetLED;
                _AutoFocustrycount = _AutoFocusNFProcess.TryCounts;
                _AutoFocusErrorMessage = _AutoFocusNFProcess.ExceptionMessage;
            }
            _AutoFocusNFProcess.Completed -= AutoFocusNFProcess_Completed;
            _AutoFocusNFProcess = null;
        }

        private void _AutoFocusFluoProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                Logger.LogMessage("AF on Fluo to recalculate fiducial offset Successed");
                _IsOffsetCalSucc = true;
                _CalculatedOffset = _AutoFocusFluoProcess.Offset;
            }
            else if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _IsOffsetCalSucc = false;
                Logger.LogMessage("Recalcualte fiducial offset aborted.", SeqLogMessageTypeEnum.WARNING);
            }
            else
            {
                _IsOffsetCalSucc = false;
                Logger.LogMessage(string.Format("Recalcualte fiducial offset failed. Exception:{0}", _AutoFocusFluoProcess.ExceptionMessage), SeqLogMessageTypeEnum.ERROR);
            }
            _AutoFocusFluoProcess.Completed -= _AutoFocusFluoProcess_Completed;
            _AutoFocusFluoProcess = null;
        }
        private void _ChannelOffsetProcess_Completed(ThreadBase sender, ThreadExitStat exitState)
        {
            if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                Logger.LogMessage("Recalculate channel offset Successed");
                _IsOffsetCalSucc = true;
                if (Math.Abs(SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset - _AutoFocusFluoProcess.Offset) < 5)
                {
                    SettingsManager.ConfigSettings.AutoFocusingSettings.ChannelOffset = _AutoFocusFluoProcess.Offset;
                    Logger.Log("Use recalculated channel offset");
                }
            }
            else if (_AutoFocusFluoProcess.ExitStat == ThreadBase.ThreadExitStat.Abort)
            {
                _IsOffsetCalSucc = false;
                Logger.LogMessage("Auto focusing on Fluo aborted.", SeqLogMessageTypeEnum.WARNING);
            }
            else
            {
                _IsOffsetCalSucc = false;
                Logger.LogMessage(string.Format("Recalculate channel offset failed. Exception:{0}", _AutoFocusFluoProcess.ExceptionMessage), SeqLogMessageTypeEnum.ERROR);
            }
            _AutoFocusFluoProcess.Completed -= _ChannelOffsetProcess_Completed;
            _AutoFocusFluoProcess = null;
        }
        #endregion AF cmd thread complete event
        #region Camera helper function and events
        private void PresetCameras(ImagingStep step, ILucidCamera camera)
        {
            camera.SwitchExpoEvent(true);
            camera.ImageId = -1;
            camera.IsRecipeImaging = true;
            Int32Rect roi = new Int32Rect();
            if (SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth > 0 && SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight > 0)
            {
                roi.X = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiLeft;
                roi.Width = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiWidth;
                roi.Y = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiTop;
                roi.Height = SettingsManager.ConfigSettings.CameraDefaultSettings.RoiHeight;
            }
            else
            {
                roi.X = 0;
                roi.Y = 0;

                // do not set here in simulation because unintialized
                if (!IsSimulationMode)
                {
                    roi.Width = camera.ImagingColumns / camera.HBin;
                    roi.Height = camera.ImagingRows / camera.VBin;
                }
            }
            if (roi.Width > 0 && roi.Height > 0)
            {
                camera.RoiStartX = (ushort)roi.X;
                camera.RoiWidth = (ushort)(roi.Width);
                camera.RoiStartY = (ushort)roi.Y;
                camera.RoiHeight = (ushort)(roi.Height);
            }
            else
            {
                camera.RoiStartX = 0;
                camera.RoiStartY = 0;
                camera.RoiWidth = camera.ImagingColumns / camera.HBin;
                camera.RoiHeight = camera.ImagingRows / camera.VBin;
            }
            //Set binning mode
            camera.HBin = SettingsManager.ConfigSettings.CameraDefaultSettings.BinFactor;
            camera.VBin = SettingsManager.ConfigSettings.CameraDefaultSettings.BinFactor;
            //Set gain
            camera.Gain = 10;
            camera.ADCBitDepth = 12;
            camera.PixelFormatBitDepth = 16;
            camera.EnableTriggerMode = true;
            camera.IsTriggerFromOutside = true;

        }
        private void ReOpenCamera(ILucidCamera camera)
        {
            camera.ExposureEndNotif -= _Camera1_ExposureEndNotif;
            camera.ExposureEndNotif -= _Camera2_ExposureEndNotif;
            camera.CameraNotif -= _Camera1_CameraNotif;
            camera.CameraNotif -= _Camera2_CameraNotif;
            string serialNumber = camera.SerialNumber;
            LucidCameraManager.ReConnectCamera(serialNumber);
            for (int i = 0; i < LucidCameraManager.GetAllCameras().Count; i++)
            {
                if (LucidCameraManager.GetCamera(i).SerialNumber == serialNumber)
                {
                    //if (i == 0) { _camera1 = LucidCameraManager.GetCamera(i); }
                    //if (i == 1) { _camera2 = LucidCameraManager.GetCamera(i); }
                    if (serialNumber == _LEDController.G1R3CameraSN.ToString())
                    {
                        LucidCameraManager.GetCamera(i).Channels = "G1/R3";
                    }
                    else if (serialNumber == _LEDController.G2R4CameraSN.ToString())
                    {
                        LucidCameraManager.GetCamera(i).Channels = "G2/R4";
                    }
                }

            }
        }
        //Assign the reference to camera after AF thread restart
        private void LucidCameraManager_OnCameraUpdated()
        {
            _camera1 = LucidCameraManager.GetCamera(0);
            _camera2 = LucidCameraManager.GetCamera(1);
        }
        private void InitializeCameras(ImagingStep step, ILucidCamera camera1, ILucidCamera camera2, double expo)
        {
            if (Camera1Task != null && Camera2Task != null)
            {
                Logger.Log("Camera already Initialized", SeqLogFlagEnum.DEBUG);
                return;
            }
            if (Camera1Task != null)
            {
                Camera1Task.Wait();
                Camera1Task.Dispose();
                Camera1Task = null;
                Logger.Log("Delete previous camera1 init task before init it again", SeqLogFlagEnum.DEBUG);
            }
            if (Camera2Task != null)
            {
                Camera2Task.Wait();
                Camera2Task.Dispose();
                Camera2Task = null;
                Logger.Log("Delete previous camera2 init task before init it again", SeqLogFlagEnum.DEBUG);
            }
            if (IsSimulationMode && (camera1 == null || camera2 == null))
            {
                OnStepRunUpdatedInvoke(step, $"Simulation: Initialize Cameras with exposure({expo} sec)", false);
                bool camera1ContinuousModeStarted = false;
                bool camera2ContinuousModeStarted = false;

                Interlocked.Increment(ref Camera1TaskCount);
                Interlocked.Increment(ref Camera2TaskCount);
                Camera1Task = Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(55);
                    camera1ContinuousModeStarted = true;
                }).ContinueWith((o) => { Interlocked.Decrement(ref Camera1TaskCount); Logger.LogMessage($"Simulation: Camera-1 ContinuousMode Finished"); });
                Camera2Task = Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(50);
                    camera2ContinuousModeStarted = true;
                }).ContinueWith((o) => { Interlocked.Decrement(ref Camera2TaskCount); Logger.LogMessage($"Simulation: Camera-2 ContinuousMode Finished"); });


                while (!camera1ContinuousModeStarted || !camera2ContinuousModeStarted)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    Thread.Sleep(1);
                }
            }//simulation mode
            else
            {
                OnStepRunUpdatedInvoke(step, "Initialize Camera", false);
                PresetCameras(step, camera1);
                PresetCameras(step, camera2);
                camera1.ExposureEndNotif += _Camera1_ExposureEndNotif;
                camera2.ExposureEndNotif += _Camera2_ExposureEndNotif;
                Interlocked.Increment(ref Camera1TaskCount);
                Interlocked.Increment(ref Camera2TaskCount);
                Camera1Task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        camera1.CameraNotif += _Camera1_CameraNotif;
                        camera1.StartContinuousMode(expo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        try
                        {
                            ReOpenCamera(camera1);
                            camera1 = _camera1;
                            PresetCameras(step, camera1);
                            camera1.ExposureEndNotif += _Camera1_ExposureEndNotif;
                            camera1.CameraNotif += _Camera1_CameraNotif;
                            camera1.StartContinuousMode(expo);
                        }
                        catch (Exception ex1)
                        {
                            Logger.LogError(ex1.ToString());
                            throw ex1;
                        }
                    }

                }, TaskCreationOptions.LongRunning).ContinueWith((o) => { Interlocked.Decrement(ref Camera1TaskCount); Logger.LogMessage($" Camera-1 ContinuousMode Finished"); });
                Camera2Task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        camera2.CameraNotif += _Camera2_CameraNotif;
                        camera2.StartContinuousMode(expo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        try
                        {
                            ReOpenCamera(camera2);
                            camera2 = _camera2;
                            PresetCameras(step, camera2);
                            camera2.CameraNotif += _Camera1_CameraNotif;
                            camera2.ExposureEndNotif += _Camera2_ExposureEndNotif;
                            camera2.StartContinuousMode(expo);
                        }
                        catch (Exception ex2)
                        {
                            Logger.LogError(ex2.ToString());
                            throw ex2;
                        }
                    }
                }, TaskCreationOptions.LongRunning).ContinueWith((o) => { Interlocked.Decrement(ref Camera2TaskCount); Logger.LogMessage($"Camera-2 ContinuousMode Finished"); });
                while (!camera1.ContinuousModeStarted || !camera2.ContinuousModeStarted)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    Thread.Sleep(1);
                }
            }
        }
        private void UnInitializeCameras(ImagingStep step, ILucidCamera camera1, ILucidCamera camera2)
        {
            if (Camera1Task == null && Camera2Task == null) { return; }
            if (!IsSimulationMode || (camera1 != null && camera2 != null))
            {
                OnStepRunUpdatedInvoke(step, "Uninitialize Camera", false);
                WaitForImagesReadout();

                camera1.StopAcquisition();
                camera2.StopAcquisition();
                camera1.CameraNotif -= _Camera1_CameraNotif;
                camera2.CameraNotif -= _Camera2_CameraNotif;
                camera1.ExposureEndNotif -= _Camera1_ExposureEndNotif;
                camera2.ExposureEndNotif -= _Camera2_ExposureEndNotif;
                if (Camera1Task != null)
                {
                    Camera1Task.Wait();
                    Camera1Task.Dispose();
                    Camera1Task = null;
                    Logger.LogMessage("Delete camera1 init task through uninit");
                }
                if (Camera2Task != null)
                {
                    Camera2Task.Wait();
                    Camera2Task.Dispose();
                    Camera2Task = null;
                    Logger.LogMessage("Delete camera2 init task through uninit");
                }
                camera1.IsRecipeImaging = false;
                camera2.IsRecipeImaging = false;
                camera1.SwitchExpoEvent(false);
                camera2.SwitchExpoEvent(false);
                camera1.ImageId = -1;
                camera2.ImageId = -1;
            }
            else
            {
                OnStepRunUpdatedInvoke(step, $"Simulation: Uninitialize Cameras", false);
                WaitForImagesReadout();

                if (Camera1Task != null)
                {
                    Camera1Task.Wait();
                    Camera1Task.Dispose();
                    Camera1Task = null;
                    Logger.LogMessage("Delete camera1 init task through uninit");
                }
                if (Camera2Task != null)
                {
                    Camera2Task.Wait();
                    Camera2Task.Dispose();
                    Camera2Task = null;
                    Logger.LogMessage("Delete camera2 init task through uninit");
                }
            }
        }
        //cameraIndex -- 1 camera 1, 2 -- camera 2
        /// <summary>
        /// Waits for an image to appear in imagingStates
        /// </summary>
        /// <param name="imagingStates"></param>
        /// <param name="imgData"></param>
        void QuequeImage(ImagingStates imagingStates, AcquiredImageData imgData)
        {
            //wait for camera image arrives
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                if (IsAbort)
                {
                    Logger.LogMessage($"Abort Queue Image: Camera-{imagingStates.CameraIndex}: {imgData.ImageName}");
                    break;
                }
                lock (imagingStates)
                {
                    if (imagingStates.ImageListByID.ContainsKey(imgData.ImageId))
                    {

                        imgData.ImageDataArray = imagingStates.ImageListByID[imgData.ImageId];
                        Logger.LogMessage($"Added to Queue: Camera-{imagingStates.CameraIndex} Image(ID:{imgData.ImageId}): {imgData.ImageName}");
                        ImageQ.Enqueue(imgData);
                        break;
                    }
                }
                Thread.Sleep(10);
                int timeout = IsSimulationMode ? 600 : 20; // allow up to 600 seconds to que image for debugging with breakpoints
                if (stopwatch.ElapsedMilliseconds > 1000 * timeout) // timeout TK
                {
                    Logger.LogMessage($"Queue Image Timeout, Add null data to Queue: Camera-{imagingStates.CameraIndex} Image(ID:{imgData.ImageId}): {imgData.ImageName}", SeqLogMessageTypeEnum.WARNING);
                    stopwatch.Stop();
                    //AcquiredImageData emptyData = new AcquiredImageData();
                    //emptyData.ImageName = imgData.ImageName;
                    ImageQ.Enqueue(imgData);
                    // Restart camera
                    //Logger.LogWarning($"Reconnect camera - {imgData.Camera.SerialNumber}");
                    //ReOpenCamera(imgData.Camera);
                    //Logger.LogMessage("Reinitialize cameras");
                    //UnInitializeCameras(imgData.Step, _camera1, _camera2);
                    //InitializeCameras(imgData.Step, _camera1, _camera2, imgData.Imageinfo.MixChannel.Exposure);
                    break;
                }


            } while (true);
        }
        private void ReceivedImage(CameraNotifArgs args, ImagingStates imagingState)
        {
            if (!_IsAFing)
            {
                Logger.Log("Receiving, before lock");
                lock (imagingState)
                {
                    byte[] imageDataArray;
                    if (args.ImageRef?.Length > 0)
                    {
                        imageDataArray = args.ImageRef;
                        Logger.LogMessage($"Image{imagingState.CameraIndex} DataArrive Image(ID:{args.ImageId})");
                    }
                    else
                    {
                        if (args.ImageRef == null)
                        {
                            imageDataArray = null;
                            Interlocked.Increment(ref NullImageCounts);
                            Logger.LogMessage($"Received null  image data from camera-{imagingState.CameraIndex}: Image(ID:{args.ImageId}) NullImageCount-{NullImageCounts}", SeqLogMessageTypeEnum.WARNING);
                            if (NullImageCounts > 30)
                            {
                                AbortWork();
                                Logger.LogMessage($"Null image count larger than 20, abort recipe-{imagingState.CameraIndex}: Image(ID:{args.ImageId})", SeqLogMessageTypeEnum.WARNING);
                                ExitStat = ThreadExitStat.Error;
                                throw new System.InvalidOperationException("Camera Failure");
                            }
                        }
                        else
                        {
                            Logger.LogMessage($"Received empty image data from camera-{imagingState.CameraIndex}: Image(ID:{args.ImageId})", SeqLogMessageTypeEnum.ERROR);
                            imageDataArray = new byte[0];
                        }
                    }
                    if (args.ImageId > 0)
                    {
                        if (imagingState.ImageListByID.ContainsKey(args.ImageId))
                        {
                            Logger.LogMessage($"Received image with duplicate id Image(ID:{args.ImageId})", SeqLogMessageTypeEnum.WARNING);
                        }
                        else
                        {
                            imagingState.ImageListByID.Add(args.ImageId, imageDataArray);
                        }
                    }
                    else
                    {
                        Logger.LogMessage($"Received image with non-positive id Image(ID:{args.ImageId})", SeqLogMessageTypeEnum.WARNING);
                    }
                }
            }
            else
            {

            }
        }
        private void _Camera1_CameraNotif(object sender) =>
           ReceivedImage(sender as CameraNotifArgs, ImagingState1);
        private void _Camera2_CameraNotif(object sender) =>
            ReceivedImage(sender as CameraNotifArgs, ImagingState2);
        private void _Camera1_ExposureEndNotif(bool isExpoSuccess) =>
            ReceivedCameraExposureEndNotif(ImagingState1, isExpoSuccess);
        private void _Camera2_ExposureEndNotif(bool isExpoSuccess) =>
            ReceivedCameraExposureEndNotif(ImagingState2, isExpoSuccess);
        private void ReceivedCameraExposureEndNotif(ImagingStates imagingState, bool isExpoSuccess)
        {
            lock (imagingState)
            {
                imagingState.IsCameraExposureEnd = true;
                imagingState.IsExposureSuccess = isExpoSuccess;
            }
        }
        void SimulateCameraExposureAndImaging(ImagingStates imagingState, double exposureSec, int imageId, int imageSize)
        {
            const int exposureWaitMS = 100;
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < exposureSec * 1000)
            {
                if (IsAbort)
                {
                    break;
                }
                Thread.Sleep(exposureWaitMS);
            }
            sw.Stop(); //simulation exposure ends
            ReceivedCameraExposureEndNotif(imagingState, true);
            var rand2 = new Random();
            double waitSec = rand2.NextDouble(); //between 0.0 to 1.0
            if (waitSec > 0)
            {
                Thread.Sleep((int)(waitSec * 1000));
            }
            byte v = (byte)((imagingState.CameraIndex == 1) ? 0x20 : 0x40);

            byte[] image = Enumerable.Repeat<byte>(v, imageSize).ToArray();
            ReceivedImage(new CameraNotifArgs(imageId, image), imagingState);
        }

        #endregion Camera helper function and events
        #endregion run Image Step
        //--------------------------------------------------------------------------------------------------------
        #region Imaging process

        /// <summary>
        /// check image quality with intensity and FOG.
        /// </summary>
        private bool[] CheckImagebyFluro(ImagingStep step, LEDTypes led, int LEDintensity, double expo, int focusIndex, int regioncount, double[] intThreshold, bool increaseFactor = false)
        {
            // config camera
            bool[] isPassCheckByFluor = new bool[2] { true, true };
            string _Surface = (focusIndex == 0) ? "t" : "b";
            ILucidCamera FCamera = _camera1.Channels.Contains("3") ? _camera1 : _camera2;
            FCamera.Gain = 10;
            FCamera.ADCBitDepth = 12;
            FCamera.PixelFormatBitDepth = 16;
            FCamera.EnableTriggerMode = false;
            FCamera.RoiStartX = 0;
            FCamera.RoiStartY = 0;
            FCamera.RoiWidth = FCamera.ImagingColumns / FCamera.HBin;
            FCamera.RoiHeight = FCamera.ImagingRows / FCamera.VBin;
            // config led
            _LEDController.SetLEDIntensity(led, LEDintensity);
            _LEDController.SetLEDControlledByCamera(led, true);

            WriteableBitmap imageA = null;
            double testexpo = expo;
            FCamera.GrabImage(testexpo, CaptureFrameType.Normal, ref imageA);
            string imagename = string.Format("Testimage_{0}_Inc{1}_R3_{2}X{3:00.00}Y{4:00.00}mm_{5:F2}um_{6:F3}s_{7}Int.tif",
                            Recipe.RecipeName, _loopCount, _Surface,
                            _ImageInfo.MixChannel.XPosition, _ImageInfo.MixChannel.YPosition,
                            _ImageInfo.MixChannel.FocusPosition, testexpo,
                            LEDintensity);
            _TestAFImagePath = Directory.GetParent(_RecipeRunImageDataDir).FullName + "\\" + "TestImage" + "\\" + imagename;
            ImageProcessing.Save(_TestAFImagePath, imageA, _ImageInfo, false);
            if (imageA != null)
            {
                FindQCFromImage QC = new FindQCFromImage(_TestAFImagePath);
                double FOGscore = QC.QcValues[1];
                if (FOGscore < 0.1)
                {
                    isPassCheckByFluor[0] = false;
                    OnStepRunUpdatedInvoke(step, "Failed to pass fluor check", true);
                }
                else
                {
                    OnStepRunUpdatedInvoke(step, "Passed fluor check", false);
                    if (focusIndex == 0)
                    {
                        double imgint = QC.QcValues[7];
                        OnStepRunUpdatedInvoke(step, string.Format("Calibrating exposure time, R3 Relative Intensity{0}", imgint), false);
                        if (imgint > intThreshold[1] && increaseFactor)
                        {
                            _ExpoControlFactor -= 1;
                        }
                        else if (imgint < intThreshold[0] && _loopCount == 1 && regioncount == 0)
                        {
                            isPassCheckByFluor[1] = false;
                        }
                        else if (imgint < intThreshold[1] && increaseFactor) { _ExpoControlFactor += 1; }
                    }
                }
            }
            else { OnStepRunUpdatedInvoke(step, "Failed to take image to check focus with Fluor", true); }
            _LEDController.SetLEDControlledByCamera(led, false);
            return isPassCheckByFluor;
        }
        override protected void BackupImage(ImagingStep step, string imageFileName, string destPath) =>
            _Imaging.ImageBackingup?.AddABackupRequest(step, imageFileName, destPath);

        /// <summary>
        /// Blocks until the Image Queue has space for additional images
        /// </summary>
        void CheckForImageQSize()
        {
            if (ImageQ != null)
            {
                int qSize;
                while ((qSize = ImageQ.QueueCount) > MaxImageQSize)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    Logger.LogMessage($"The imageQ current size is {qSize} , the maximum allowed size is {MaxImageQSize}");
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// Looks for images in a FIFO queue. Saves them and notifies OLA
        /// when a tile is complete. This method runs in its own thread.
        /// 
        /// Special case: A NULL imgData object is added to the end of the queue to signify the end of the recipe???
        /// </summary>
        private void ProcessImageQ()
        {
            bool isProcessing = true; // flag used to stop the image processing thread
            bool saveImage = true; //< mirrors the image save settings from RecipeRunConfig.SaveSimulationImages
            int lastImageLoopCount = -1; //< the loop count (Incorporation Cycle) of the last image that was received
            int _ImageCountSameCycle = -1; //< the total number of images processed for the current Incorportaion Cycle
            int _BadImageCountPcycle = 0;
            int _FailedSaveCount = 0;
            int _NullImgPreviousCycles = 0;
            int _NullImgThisCycle = 0;
            ImagingStep lastImagingStep = null; //< ? only used when a null image comes through the queue
            //int missingImageOffset = 0; //< track the total number of missing images detected each cycle

            ImageListKeeper _listKeeper = ImageListKeeper.GetImageListKeeper(); //< keeps track of the image files saved each cycle
            TileWatcher _tileWatcher = TileWatcher.GetTileWatcher(); //< monitors save directory for complete image sets

            if (IsSimulationMode)
            {
                saveImage = RecipeRunConfig?.SaveSimulationImages == true;
            }
            while (isProcessing)
            {
                if (IsAbort)
                {
                    break;
                }

                // clear busy flag if the queue is empty
                if (!ImageQ.HasItems)
                {
                    lock (_processImageBusyLocker)
                    {
                        _processImageQueueBusy = false;
                    }
                }
                AcquiredImageData imgData = ImageQ.WaitForNextItem(); // blocks until there is an item

                // set busy flag, processing an image
                lock (_processImageBusyLocker)
                {
                    _processImageQueueBusy = true;
                }

                if (imgData == null)
                {
                    if (CanUpdateOLALoopcount && lastImageLoopCount > 0)
                    {
                        SetOLANewLoopcount(lastImageLoopCount, lastImagingStep, true);
                    }
                    CurrentImageLoopCount.Counts = lastImageLoopCount;
                    //processing done
                    Logger.LogMessage("Image data processing thread is going to stop.");
                    break;
                }
                else
                {
                    string imageFileName = "";
                    if (imgData.ImageId == 0)
                    {
                        // the first image in the recipe will have id == 1
                        // if id == 0, imgData was not initialized
                        Logger.LogWarning("Received image with NULL ID");
                    }
                    // Save image
                    try
                    {
                        byte[] ImageDataArray = imgData.ImageDataArray; //< TODO: does ImageDataArray need to be locked?
                        Logger.LogMessage(string.Format("Dequeue Camera-{0} Image(ID:{1}) imagedata {2}, PDValue:{3}, LED Failed:{4}, trycounts{5}, Badimage{6}, Nullimage{7}, Low Intensity Image{8}",
                            imgData.CameraIndex, imgData.ImageId, imgData.ImageName, imgData.PDValue, imgData.LEDFailure, imgData.TryCounts, imgData.BadImageCounts, imgData.NullImageCounts, 0));

                        lastImageLoopCount = imgData.LoopCount;
                        lastImagingStep = imgData.Step;
                        if (CurrentImageLoopCount.Counts == imgData.LoopCount)
                        {
                            _ImageCountSameCycle += 1;
                        }
                        else
                        {
                            _ImageCountSameCycle = 1;
                            _BadImageCountPcycle = 0;
                            //missingImageOffset = 0;
                        }

                        if (ImageDataArray?.Length > 0)
                        {


                            // Save Image
                            //WriteableBitmap capturedImage;
                            //if (IsSimulationMode)
                            //{
                            //    capturedImage = LucidCamera.ToWriteableBitmap(imgData.Imageinfo.MixChannel.ROI.Width, imgData.Imageinfo.MixChannel.ROI.Height,
                            //        ImageDataArray, 16);// imgData.Camera.PixelFormatBitDepth);
                            //}
                            //else
                            //{
                            //    capturedImage = LucidCamera.ToWriteableBitmap(imgData.Imageinfo.MixChannel.ROI.Width, imgData.Imageinfo.MixChannel.ROI.Height,
                            //        ImageDataArray, imgData.Camera.PixelFormatBitDepth);
                            //}

                            //LogMessage(string.Format("Finish convert{0} to writable bitmap ", imgData.ImageName));
                            //double dMean = 0;
                            //double dStdDev = 0;
                            //ImageProcessing.MeanStdDev(capturedImage, ref dMean, ref dStdDev);
                            string filename = imgData.RecipeRunImageDataDir + "\\" + imgData.ImageName;
                            int imagenum = 0;

                            // make sure the imageFileName is unique
                            imageFileName = filename;
                            while (File.Exists(imageFileName))
                            {
                                imagenum += 1;
                                imageFileName = filename.Replace(filename.Substring(filename.Length - 4),
                                    string.Format("_{0}{1}", imagenum, filename.Substring(filename.Length - 4)));
                            }

                            Exception saveex = null;
                            bool saveOriginalImage = false;
                            byte[] transformed;
                            // apply affine transformations if successfully initialized
                            if (!_transformedImageRect.IsEmpty && saveImage)
                            {
                                Stopwatch imgTransformStopwatch = Stopwatch.StartNew();

                                _imageTransformer.FastTransform(ImageDataArray, out transformed, imageFileName);

                                Logger.Log($"Image transformation elapsed time [ms]|{ imgTransformStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                                try
                                {
                                    if (saveImage)
                                    {
                                        Stopwatch imgSaveStopwatch = Stopwatch.StartNew();
                                        imgData.Imageinfo.MixChannel.BitDepth = 16;
                                        if (saveOriginalImage)
                                        {
                                            ImageProcessing.Save(imageFileName.Substring(0, imageFileName.Length - 4) + "_original.tif", ImageDataArray, imgData.Imageinfo, false);
                                        }

                                        AcquiredImageData transformedImageData = imgData;
                                        transformedImageData.Imageinfo.MixChannel.ROI = _transformedImageRect;
                                        ImageProcessing.Save(imageFileName, transformed, transformedImageData.Imageinfo, false);

                                        Logger.Log($"SaveNET image saving elaped time [ms]|{imgSaveStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                                    }
                                }
                                catch (Exception saex)
                                {
                                    _FailedSaveCount++;
                                    saveex = saex;
                                }
                                if (_FailedSaveCount > 10)
                                {
                                    throw saveex;
                                }
                            }
                            else
                            {
                                try
                                {
                                    if (saveImage)
                                    {
                                        imgData.Imageinfo.MixChannel.BitDepth = 16;
                                        Stopwatch imgSaveStopwatch = Stopwatch.StartNew();
                                        ImageProcessing.Save(imageFileName, ImageDataArray, imgData.Imageinfo, false);
                                        Logger.Log($"SaveNET image saving elaped time [ms]|{imgSaveStopwatch.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
                                    }
                                }
                                catch (Exception saex)
                                {
                                    _FailedSaveCount++;
                                    saveex = saex;
                                }
                                if (_FailedSaveCount > 10)
                                {
                                    throw saveex;
                                }
                            }

                            //Bad Image check (apply to original, un-transformed image)
                            if (!IsSimulationMode && BadImageIdentifier.IsBadImage(ImageDataArray, imgData.Imageinfo.MixChannel.ROI.Width, imgData.Camera.PixelFormatBitDepth))
                            {
                                _BadImageCountPcycle++;
                                OnImageSavedInvoke(new ImageSavedEventArgs()
                                {
                                    Step = imgData.Step,
                                    Message = string.Format("Camera-{0}: {1}, Bad Image",
                                        imgData.CameraIndex, imgData.ImageName),
                                    ImageCurrentLoopCount = imgData.LoopCount,
                                    ImageFile = imageFileName
                                });
                                if (_BadImageCountPcycle > 3)
                                {

                                    Logger.LogError("Bad Image Count larger than 3");
                                    AbortWork();
                                    ExitStat = ThreadExitStat.Error;
                                    throw new System.InvalidOperationException("Bad Image Count larger than 3, Camera Failure");
                                }
                            }

                            OnImageSavedInvoke(new ImageSavedEventArgs()
                            {
                                Step = imgData.Step,
                                Message = string.Format("[{0}]: Saved Camera-{1} Image(ID:{2}): {3}",
                                Thread.CurrentThread.Name, imgData.CameraIndex, imgData.ImageId, imgData.ImageName),
                                ImageCurrentLoopCount = imgData.LoopCount,
                                ImageFile = imageFileName
                            });

                            CurrentImageLoopCount.Counts = imgData.LoopCount;

                            if (_IsBackUp)
                            {
                                string despath = Path.Combine(imgData.BackupPath, imgData.ImageName);
                                BackupImage(imgData.Step, imageFileName, despath);
                            }
                            //capturedImage = null;
                            ImageDataArray = null;
                            imgData.ImageDataArray = null;
                        }
                        else
                        {
                            if (ImageDataArray == null)
                            {
                                Logger.LogWarning($"Camera-{imgData.CameraIndex} Image(ID:{imgData.ImageId}) has null image data reference, image {imgData.ImageName} won't be saved");
                            }
                            else
                            {
                                Logger.LogWarning($"Camera-{imgData.CameraIndex} Image(ID:{imgData.ImageId}) has Zero length in image data , image {imgData.ImageName} won't be saved");
                            }
                            _NullImgThisCycle++;
                            OnImageSavedInvoke(new ImageSavedEventArgs()
                            {
                                Step = imgData.Step,
                                Message = string.Format("[{0}]: Error in saving Camera-{1} Image(ID:{2}): {3}",
                                Thread.CurrentThread.Name, imgData.CameraIndex, imgData.ImageId, imgData.ImageName),
                                ImageCurrentLoopCount = imgData.LoopCount,
                                ImageFile = imgData.ImageName
                            });
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to save image {imgData.ImageName} with error {ex.Message}");
                        AbortWork();
                        ExitStat = ThreadExitStat.Error;
                    }

                    //Maintain Image list
                    try
                    {
                        // image saving should be complete
                        //int loopCount = CurrentImageLoopCount.Counts;
                        int cycleNumber = imgData.LoopCount;
                        string tileName = imgData.Locationinfo;
                        string imagesPath = imgData.RecipeRunImageDataDir;

                        if (CurrentImageLoopCount.Counts != cycleNumber)
                        {
                            // sanity check. warning cycle num  != loop count!
                            Logger.Log("Warning! cycle num  != loop count", SeqLogFlagEnum.DEBUG);
                        }

                        // add the image to the dictionary
                        // this method must never fail since the total image count is used to determine cycle end/setOLALoop
                        _listKeeper.Insert(tileName, imgData.ImageName); //use ImageFileName for full path
                                                                         // TODO: images should be added to the dictionary in the acquisition method to simplify handling of missing images

                        // print info to log to track process/debug
                        Logger.Log($"Image Processing Progress: [{_listKeeper.TotalImageCount() / (double)_CycleTotalImageCount * 100.0:F}%]" +
                            $" Tile {tileName} image count: [{_listKeeper.TileImageCount(tileName)} / 4]" +
                            $" Cycle {cycleNumber} image count:  [{_listKeeper.TotalImageCount()} / {_CycleTotalImageCount}]");


                        // check if this image completes a tile or a cycle
                        if (_listKeeper.TileCountComplete(tileName))
                        {
                            // verify all the files exist on disk before adding image names to list.txt
                            if (_tileWatcher.VerifyTile(imagesPath, cycleNumber, tileName))
                            {
                                // the tile is complete, set in OLA and update list.txt file
                                Logger.Log($"Tile complete. Calling SetOLANewTile. Region: {imgData.Locationinfo} Cycle: {cycleNumber}", SeqLogFlagEnum.DEBUG);
                                // notify OLA imaging of one tile finished
                                SetOLANewTile(imgData.Step, imgData.Locationinfo, cycleNumber);
                            }
                            else if (IsSimulationMode)
                            {
                                // pay no attention to lost tiles in "simulation mode"
                                // the tile is complete, set in OLA and update list.txt file
                                Logger.Log($"Tile complete. Calling SetOLANewTile. Region: {imgData.Locationinfo} Cycle: {cycleNumber}", SeqLogFlagEnum.DEBUG);
                                // notify OLA imaging of one tile finished
                                SetOLANewTile(imgData.Step, imgData.Locationinfo, cycleNumber);
                            }
                            else
                            {
                                int regionIndex = imgData.RegionIndex;
                                int focusNum = tileName[0] == 't' ? 0 : 1; //following the convention used to set _Surface
                                Tuple<int, int> tileSite = new Tuple<int, int>(regionIndex, focusNum);
                                if (_tileWatcher.MarkTileLost(tileSite))
                                {
                                    // verification failed and tile was successfully marked for re-imaging
                                    // remove the old image names from the list to stop verification until four new images arrive
                                    _listKeeper.ClearTile(tileName);
                                }
                                else
                                {
                                    // tile was previously marked for re-acquisition
                                    // image capture has failed again
                                    // drop the tile and do not add images to the list file
                                    // send the incomplete tile to OLA anyway to print error messages
                                    Logger.Log($"Dropped incomplete tile. Calling SetOLANewTile. Region: {imgData.Locationinfo} Cycle: {cycleNumber}", SeqLogFlagEnum.DEBUG);
                                    SetOLANewTile(imgData.Step, imgData.Locationinfo, cycleNumber);
                                }
                            }
                        }
                        else
                        {
                            // cycle #, tile, images/tile, images/cycle
                            Logger.Log($"Imaging acquired. Cycle: {cycleNumber}", SeqLogFlagEnum.DEBUG);
                            // tile not yet complete, keep going
                        }

                        // check if this image completes the cycle
                        if (_listKeeper.TotalImageCount() == _CycleTotalImageCount)
                        {
                            //all images up to _CurrentImageLoopCount are saved
                            _NullImgPreviousCycles = _NullImgThisCycle;
                            if (CanUpdateOLALoopcount)
                            {
                                Logger.Log($"Loop complete. Calling SetOLANewLoop. Cycle: {cycleNumber}", SeqLogFlagEnum.DEBUG);
                                SetOLANewLoopcount(cycleNumber, imgData.Step, false);
                            }

                            // update list.txt with saved file names
                            //so changed  to OnImageSavedInvoke, 
                            _listKeeper.SaveFilePath = Path.Combine(imgData.RecipeRunImageDataDir, "list.txt");
                            _listKeeper.DumpCycleToFile();
                        }
                    }

                    catch (Exception imglistex)
                    {
                        Logger.LogError(imglistex.ToString());
                    }


                }// (imgData != null)

                Thread.Sleep(10);
            }
            Logger.LogMessage($"Image data processing thread has exited.");
        }

        #region On-line Image Analysis
        private bool CanUpdateOLALoopcount => !this.Recipe.RecipeName.Contains("_CL");

        private void SetOLANewLoopcount(int count, ImagingStep step, bool isLastOne)
        {
            Task.Run(() =>
            {
                if (isLastOne)
                {
                    Logger.LogMessage($"Update the last LOOPCOUNT({count}) to OLAJobManager");
                }
                else
                {
                    Logger.LogMessage($"Update LOOPCOUNT({count}) to OLAJobManager");
                }
                RunOLAJobManager(count, step, isLastOne);
            });

        }
        private void SetOLANewTile(ImagingStep step, string tilename, int count)
        {
            Task.Run(() =>
            {
                bool initOLA = false;
                if (count == 1 && tilename == "tL101A") // TODO: a better "if" condition?
                {
                    initOLA = true;
                    Logger.LogMessage($"Update first tile LOOPCOUNT({count}) to OLAJobManager");
                }
                UpdateTileOLAJobManager(step, count, tilename, initOLA);
            });
        }
        #endregion On-line Image Analysis

        private void WaitforCameraExposureEnd()
        {
            do //for waiting Camera 1 and  2 Exposure End
            {
                if (IsAbort)
                {
                    break;
                }

                lock (ImagingState1)
                {
                    if (ImagingState1.IsCameraExposureEnd)
                    {
                        lock (ImagingState2)
                        {
                            if (ImagingState2.IsCameraExposureEnd)
                            {
                                break;
                            }
                        }
                    }
                }
                Thread.Sleep(1);
            } while (true);
        }
        private void WaitForSavingImagesComplete() => _Imaging.WaitForSavingImagesComplete();
        private void WaitForImagesReadout()
        {
            //while (true)
            //{
            //    if (IsAbort)
            //    {
            //        break;
            //    }
            //    lock (ImagingState1)
            //    {
            //        if (ImagingState1.QueueImageTaskCount <= 0)
            //        {
            //            lock (ImagingState2)
            //            {
            //                if (ImagingState2.QueueImageTaskCount <= 0)
            //                {
            //                    ImagingState1.ImageListByID.Clear();
            //                    ImagingState2.ImageListByID.Clear();
            //                    break;
            //                }
            //            }
            //        }
            //    }
            //    Thread.Sleep(1);
            //}

            while (ImagingState1.QueueImageTaskCount > 0 || ImagingState2.QueueImageTaskCount > 0)
            {
                if (IsAbort)
                    break;
                Thread.Sleep(1);
            }

            lock (ImagingState1)
                ImagingState1.ImageListByID.Clear();
            lock (ImagingState2)
                ImagingState2.ImageListByID.Clear();

        }
        override protected void WaitForBackingupImageComplete() => _Imaging.WaitForBackingupImageComplete();
        #endregion Image process
        //---------------------------------------------------------------------------------------------
        #region Run other Step (other than imaging) Functions

        /// <summary>
        /// Runs a set temperature step. This method sets the chemistry module (flow cell) temperature.
        /// If the step WaitForComplete parameter is enabled, this method will block the recipe thread
        /// until the temperature setting step is complete or times out. The default threshold for a
        /// timeout error is set at 2x the step duration or 5 minutes for steps with no duration.
        /// 
        /// Temperature ramping decisions are made in the TemperatureController class based on
        /// the step duration and the change in temperature.
        /// </summary>
        /// <param name="step">The recipe step containing instructions for setting the chemistry module temperature</param>
        private void RunStepProc(SetTemperStep step)
        {
            Logger.LogMessage($"Beginning Recipe Step: Set Temperature. Target Temprature:{step.TargetTemper} °C.");

            if (IsSimulationMode)
            {
                Thread.Sleep(1000);
                return;
            }

            // retry attempts are done in the recipe thread to avoid occupying the thread doing MainBoard communication
            const int maxTryCount = 3; //< attempt to set temperature up to 3 times
            const int retryDelayMs = 1000; //< wait 1 second in-between attempts

            // set the threshold for a timeout error at 2x the step duration or 5 minutes
            int timeoutMs = (step.Duration > 0) ? (step.Duration * 1000 * 2) : (5 * 60 * 1000);

            // get the current temperature
            for (int tryCount = 0; tryCount < maxTryCount; ++tryCount)
            {
                if (_FCTemperControllerRev2.GetTemper())
                {
                    // temperature read successfully
                    break;
                }
                else
                {
                    // delay
                    if (tryCount < maxTryCount - 1)
                    {
                        Logger.LogMessage($"Failed to read chemistry temperature. Attempt: {tryCount + 1}", SeqLogMessageTypeEnum.WARNING);
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }
            double currentTemperature = _FCTemperControllerRev2.CurrentTemper;
            double startTemperature = currentTemperature;

            // set pid and gain for the FC peltier (only 2.5+)
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                Logger.Log($"Setting FC TE Parameters P:{step.CtrlP} I:{step.CtrlI} D:{step.CtrlD} " +
                    $"HeatGain:{step.CtrlHeatGain} CoolGain:{step.CtrlCoolGain}", SeqLogFlagEnum.DEBUG);

                for (int tryCount = 0; tryCount < maxTryCount; ++tryCount)
                {
                    if (MainBoardController.GetInstance().SetTemperCtrlParameters(step.CtrlP, step.CtrlI, step.CtrlD, step.CtrlHeatGain, step.CtrlCoolGain))
                    {
                        // parameters set successfully
                        break;
                    }
                    else
                    {
                        // delay and retry
                        Logger.LogMessage($"Failed to set temperature control parameters. Attempt: {tryCount + 1}", SeqLogMessageTypeEnum.WARNING);
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }

            // set target temperature
            for (int tryCount = 0; tryCount < maxTryCount; ++tryCount)
            {
                if (_FCTemperControllerRev2.SetTemperature(step.TargetTemper, step.Duration, step.Tolerance))
                {
                    // target temperature set successfully
                    break;
                }
                else
                {
                    // retry. throw and quit after maxTryCount failures
                    if (tryCount < maxTryCount - 1)
                    {
                        Logger.LogMessage($"Failed to set chemistry target temperature. Attempt: {tryCount + 1}", SeqLogMessageTypeEnum.WARNING);
                        Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        Logger.LogMessage($"Error: Could not set chemistry target temperature after {tryCount + 1} attempts", SeqLogMessageTypeEnum.ERROR);
                        OnStepRunUpdatedInvoke(step, $"Fatal Error in step: [{step.DisplayName}]. Stopping recipe...", true);
                        AbortWork();
                        ExitStat = ThreadExitStat.Error;
                        throw new System.InvalidOperationException("FC Temp Control Failure");
                    }
                }
            }

            // do the waiting in this thread to avoid blocking MainBoard communications
            if (step.WaitForComplete)
            {
                // block the recipe thread from continuing until the temperature
                // is within the tolerance of the setpoint or a timeout error occurs

                // setup a timer to update the GUI every updatePeriodMs milliseconds
                const int period = 5000; //< 5 second update interval
                string msg = $"Setting chemistry temperature to {step.TargetTemper} °C...";
                DateTime starTime = DateTime.Now;
                using (var t = new Timer(e => OnStepRunUpdatedInvoke(step, msg, isError: false), null, 0, period))
                {
                    while (Math.Abs(_FCTemperControllerRev2.GetProcessDifference()) > step.Tolerance || _FCTemperControllerRev2.IsRamping
                        /*_FCTemperControllerRev2.GetRampTimeRemaining() > 0*/)
                    {
                        // temperature has already been polled by GetProcessDifference()
                        currentTemperature = _FCTemperControllerRev2.CurrentTemper;
                        if (RecipeHelpers.HasOvershot(startTemperature, currentTemperature, step.TargetTemper))
                        {
                            break;
                        }
                        if (IsAbort)
                        {
                            OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                            break;
                        }
                        // generate an updated status message
                        double elapsedTime = (DateTime.Now - starTime).TotalMilliseconds;
                        double rampPercent = Math.Abs(step.Duration - _FCTemperControllerRev2.GetRampTimeRemaining() / 1000.0) / (step.Duration * 100.0);
                        double changePercent = 100.0 - (Math.Abs((currentTemperature - step.TargetTemper) / (startTemperature - step.TargetTemper))) * 100.0;
                        msg = $"Process difference: {_FCTemperControllerRev2.GetProcessDifference():F} [°C]. " +
                            $"Elapsed time: {elapsedTime / 1000.0:F} [s]. " +
                            $"Time remaining: { _FCTemperControllerRev2.GetRampTimeRemaining() / 1000.0:F} [s]. " +
                            $"Progress: {Math.Max(rampPercent, changePercent):F} %";

                        // if this step has exceeded the maximum allowed time, warn and proceed with the recipe
                        if (elapsedTime > timeoutMs)
                        {
                            string errorMessage = $"Timeout error occured in the step: Set temperature";
                            Logger.LogMessage(errorMessage, SeqLogMessageTypeEnum.ERROR);
                            OnStepRunUpdatedInvoke(step, errorMessage, true);
                            AbortWork();
                            ExitStat = ThreadExitStat.Error;
                            throw new System.InvalidOperationException("FC Temp Control Failure");
                        }
                        // sleep this thread for 1 second before checking again
                        Thread.Sleep(1000);
                    }
                    t.Dispose();
                }
            }
        }
        public override void RunStepProc(StopTemperStep step)
        {
            if (!IsSimulationMode)
            {
                if (!_FCTemperControllerRev2.SetControlSwitch(false))
                {
                    Thread.Sleep(100);
                    if (!_FCTemperControllerRev2.SetControlSwitch(false))
                    {
                        OnStepRunUpdatedInvoke(step, "Failed to off temp", true);
                        AbortWork();
                    }
                }
                else { OnStepRunUpdatedInvoke(step, "Temp off", false); }
            }
        }

        private void RunStepProc(SetPreHeatTempStep step)
        {
            Logger.LogMessage($"Beginning Recipe Step: Set Preheating Temperature. Target Temprature:{step.TargetTemper} °C.");

            if (IsSimulationMode)
            {
                Thread.Sleep(step.WaitForComplete ? (2000) : 100);
                return;
            }

            // retry attempts are done in the recipe thread to avoid occupying the thread doing MainBoard communication
            const int maxTryCount = 3; //< attempt to set temperature up to 3 times
            const int retryDelayMs = 1000; //< wait 1 second in-between attempts

            // set the threshold for a timeout error at 5 minutes
            int timeoutMs = (5 * 60 * 1000);

            // get the current temperature
            double currentTemperature = -1;
            for (int tryCount = 0; tryCount < maxTryCount; ++tryCount)
            {
                if (GetCurrentPreHeatingTemp(ref currentTemperature))
                {
                    // temperature read successfully
                    break;
                }
                else
                {
                    // delay
                    if (tryCount < maxTryCount - 1)
                    {
                        Logger.LogMessage($"Failed to read preheating temperature. Attempt: {tryCount + 1}", SeqLogMessageTypeEnum.WARNING);
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }

            double startTemperature = currentTemperature;

            // set target temperature
            for (int tryCount = 0; tryCount < maxTryCount; ++tryCount)
            {
                if (SetFluidPreHeatingTemp(step.TargetTemper))
                {
                    // temperature set successfully
                    break;
                }
                else
                {
                    // delay
                    if (tryCount < maxTryCount - 1)
                    {
                        Logger.LogMessage($"Failed to set preheating temperature. Attempt: {tryCount + 1}", SeqLogMessageTypeEnum.WARNING);
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }

            // do the waiting in this thread to avoid blocking MainBoard communications
            if (step.WaitForComplete)
            {
                // block the recipe thread from continuing until the temperature
                // is within the tolerance of the setpoint or a timeout error occurs

                // setup a timer to update the GUI every updatePeriodMs milliseconds
                const int period = 5000; //< 5 second update interval
                string msg = $"Setting preheating temperature to {step.TargetTemper} °C...";
                DateTime starTime = DateTime.Now;
                using (var t = new Timer(e => OnStepRunUpdatedInvoke(step, msg, isError: false), null, 0, period))
                {
                    do
                    {
                        // poll for current preheating temperature
                        GetCurrentPreHeatingTemp(ref currentTemperature);
                        if (RecipeHelpers.HasOvershot(startTemperature, currentTemperature, step.TargetTemper))
                        {
                            break;
                        }
                        if (IsAbort)
                        {
                            OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                            break;
                        }
                        // generate an updated status message
                        double elapsedTime = (DateTime.Now - starTime).TotalMilliseconds;
                        double changePercent = 100.0 - Math.Abs((currentTemperature - step.TargetTemper) / (startTemperature - step.TargetTemper)) * 100.0;
                        msg = $"Process difference: {(step.TargetTemper - currentTemperature):F} [°C]. " +
                            $"Elapsed time: {elapsedTime / 1000.0:F} [s]. " +
                            $"Progress: {changePercent:F} %";

                        // if this step has exceeded the maximum allowed time, warn and proceed with the recipe
                        if (elapsedTime > timeoutMs)
                        {
                            string errorMessage = $"Timeout error occured in the step: Set preheating temperature";
                            Logger.LogMessage(errorMessage, SeqLogMessageTypeEnum.ERROR);
                            OnStepRunUpdatedInvoke(step, errorMessage, true);
                            break;
                        }
                        // sleep this thread for 1 second before checking again
                        Thread.Sleep(1000);
                    }
                    while (Math.Abs(step.TargetTemper - currentTemperature) > step.Tolerance);

                    t.Dispose();
                }

            }
            Logger.LogMessage("End Setting Preheating temperature");
        }

        public void RunStepProc(StopPreHeatingStep step)
        {
            if (!IsSimulationMode)
            {
                if (!SetFluidPreHeatingEnable(false))
                {
                    OnStepRunUpdatedInvoke(step, "Failed to off preheating temp", true);
                    AbortWork();
                }
            }
            else { OnStepRunUpdatedInvoke(step, "PreHeating Temp off", false); }
        }
        #region heating control helper function

        private bool SetFluidPreHeatingEnable(bool enable)
        {
            if (!MainBoardController.GetInstance().IsFluidPreheatAvailable) { Logger.LogMessage("PreHeating not available."); return true; }
            bool issuccess = true;
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                //MainBoardController.GetInstance().SetFluidPreHeatCtrlParameters(FluidPreheatCtrlKp, FluidPreheatCtrlKi, FluidPreheatCtrlKd, FluidPreheatCtrlGain);
                if (!MainBoardController.GetInstance().SetFluidPreHeatingEnable(enable) && !IsSimulationMode)
                {
                    Thread.Sleep(100);
                    if (!MainBoardController.GetInstance().SetFluidPreHeatingEnable(enable) && !IsSimulationMode)
                    {
                        Logger.LogError(string.Format("PreHeating Temp Control failed, recipe stop."));
                        issuccess = false;
                    }
                }
            }
            else
            {
                if (!Chiller.GetInstance().SetFluidHeatingEnable(enable) && !IsSimulationMode)
                {
                    Thread.Sleep(100);
                    if (!Chiller.GetInstance().SetFluidHeatingEnable(enable) && !IsSimulationMode)
                    {
                        Logger.LogError(string.Format("PreHeating Temp Control failed, recipe stop."));
                        issuccess = false;
                    }
                }
            }
            return issuccess;
        }
        private bool SetFluidPreHeatingTemp(double temp)
        {
            if (!MainBoardController.GetInstance().IsFluidPreheatAvailable) { Logger.LogMessage("PreHeating not available."); return true; }
            bool issuccess = true;
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                if (!MainBoardController.GetInstance().FluidPreHeatingEnabled && !IsSimulationMode)
                {
                    if (!SetFluidPreHeatingEnable(true)) { Logger.LogError("Failed to turn on preheating"); return false; }
                }
                //MainBoardController.GetInstance().SetFluidPreHeatCtrlParameters(FluidPreheatCtrlKp, FluidPreheatCtrlKi, FluidPreheatCtrlKd, FluidPreheatCtrlGain);
                if (!MainBoardController.GetInstance().SetFluidPreHeatingTemp(temp) && !IsSimulationMode)
                {
                    Thread.Sleep(100);
                    if (!MainBoardController.GetInstance().SetFluidPreHeatingTemp(temp) && !IsSimulationMode)
                    {
                        Logger.LogError(string.Format("PreHeating Temp Control failed, recipe stop."));
                        issuccess = false;
                    }
                }
            }
            else
            {
                if (!Chiller.GetInstance().IsFluidHeatingEnabled && !IsSimulationMode)
                {
                    if (!SetFluidPreHeatingEnable(true)) { Logger.LogError("Failed to turn on preheating"); return false; }
                }
                if (!Chiller.GetInstance().SetFluidHeatingTemper(temp) && !IsSimulationMode)
                {
                    Thread.Sleep(100);
                    if (!Chiller.GetInstance().SetFluidHeatingTemper(temp) && !IsSimulationMode)
                    {
                        Logger.LogError(string.Format("PreHeating Temp Control failed, recipe stop."));
                        issuccess = false;
                    }
                }
            }
            return issuccess;
        }
        private bool GetCurrentPreHeatingTemp(ref double currtemp)
        {
            if (!MainBoardController.GetInstance().IsFluidPreheatAvailable) { Logger.LogMessage("PreHeating not available."); return true; }
            bool issuccess = true;
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                if (!MainBoardController.GetInstance().GetFluidPreHeatingTemp())
                {
                    Thread.Sleep(500);
                    if (!MainBoardController.GetInstance().GetFluidPreHeatingTemp()) { issuccess = false; Logger.LogError("Failed to read preheating temp"); }
                }
                currtemp = MainBoardController.GetInstance().FluidPreHeatingCrntTemper;
            }
            else
            {
                if (!Chiller.GetInstance().GetFluidHeatingTemper())
                {
                    Thread.Sleep(500);
                    if (!Chiller.GetInstance().GetFluidHeatingTemper()) { issuccess = false; Logger.LogError("Failed to read preheating temp"); }
                }
                currtemp = Chiller.GetInstance().FluidHeatingTemper;
            }
            return issuccess;
        }
        #endregion pre heating control helper function

        private void RunStepProc(NewPumpingStep step)
        {
            Logger.LogMessage(string.Format("Start Pumping for {0}.", Enum.GetName(typeof(ModeOptions), step.PumpingType)));
            #region New pumping settings
            PumpingSettings _PumpSetting = new PumpingSettings();
            _PumpSetting.PullRate = step.PullRate;
            _PumpSetting.PushRate = step.PushRate;
            _PumpSetting.SelectedPullPath = step.PullPath;
            _PumpSetting.SelectedPushPath = step.PushPath;
            _PumpSetting.PumpingVolume = step.Volume;
            _PumpSetting.SelectedMode = step.PumpingType;
            _PumpSetting.SelectedSolution = new ValveSolution() { ValveNumber = step.Reagent };
            _PumpSetting.PullDelayTime = SettingsManager.ConfigSettings.PumpDelayTime;
            _PumpSetting.SelectedPullValve2Pos = step.SelectedPullValve2Pos;
            _PumpSetting.SelectedPullValve3Pos = step.SelectedPullValve3Pos;
            _PumpSetting.SelectedPushValve2Pos = step.SelectedPushValve2Pos;
            _PumpSetting.SelectedPushValve3Pos = step.SelectedPushValve3Pos;
            _PumpSetting.PumpPullingPaths = step.PumpPullingPaths;
            _PumpSetting.PumpPushingPaths = step.PumpPushingPaths;

            int trycounts = 0;
            int startvol = FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol;
            int predvol = startvol + step.Volume * 4;
            // Mode: Pull vol check
            if (_PumpSetting.SelectedMode == ModeOptions.Pull)
            {
                if ((500 - FluidicsInterface.Pump.PumpAbsolutePos / 12) < step.Volume)
                {
                    PumpingSettings PumpSettings = new PumpingSettings();
                    PumpSettings.SelectedPushPath = PathOptions.Waste;
                    PumpSettings.SelectedMode = ModeOptions.Push;
                    PumpSettings.PushRate = SettingsManager.ConfigSettings.FluidicsStartupSettings.DisRate;
                    PumpSettings.PumpingVolume = FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor;
                    for (int i = 0; i < 4; i++)
                    {
                        PumpSettings.PumpPushingPaths[i] = false;
                    }
                    PumpSettings.SelectedPushValve2Pos = 6;
                    PumpSettings.SelectedPushValve3Pos = 1;
                    FluidicsInterface.RunPumping(_CallingDispatcher, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, true, IsSimulationMode);
                }
                Thread.Sleep(50);
            }
            //Mode: Push vol check
            if (_PumpSetting.SelectedMode == ModeOptions.Push)
            {
                if (FluidicsInterface.Pump.PumpAbsolutePos / 12 < step.Volume)
                {
                    _PumpSetting.PumpingVolume = Math.Round(FluidicsInterface.Pump.PumpAbsolutePos / 12.0);
                    //step.Volume = (int)Math.Round(FluidicsInterface.Pump.PumpAbsolutePos / 12.0);
                }
                predvol = startvol - (int)_PumpSetting.PumpingVolume * 4;
                if (_PumpSetting.SelectedPushPath == PathOptions.Waste) { predvol = startvol; }
            }
            #endregion New pumping settings
            //FluidicsInterface.RunPumping(_CallingDispatcher, 12, _PumpSetting, true, IsSimulationMode);
            //FluidicsInterface.OnPumpingCompleted += RunPuming_Completed;
            //bool pumping_step_failed = false;
            do
            {
                if (IsAbort)
                {
                    OnStepRunUpdatedInvoke(step, "Recipe Abort", true);
                    return;
                }
                int voldif = Math.Abs(FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol);
                if (trycounts > 0)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Solution{1} pump step failed, try counts: [{0}]", trycounts, _PumpSetting.SelectedSolution.ValveNumber), false);
                }
                if (voldif < step.Volume)
                {
                    if (trycounts > 0)
                    {
                        OnStepRunUpdatedInvoke(step, string.Format("Pumped solution{2} {0}uL less than target volume {1}uL, retry.",
                            FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol, step.Volume, _PumpSetting.SelectedSolution.ValveNumber), true);
                    }
                    _PumpSetting.PumpingVolume = step.Volume - voldif;
                }
                else if (voldif > step.Volume)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Pumped solution{2} {0}uL more than target volume {1}uL, recipe continue.",
                        FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - startvol, step.Volume, _PumpSetting.SelectedSolution.ValveNumber), true);
                    break;
                }
                OnStepRunUpdatedInvoke(step, string.Format("[Before pumping: Pump position: {0}, Valve current position: {1},  Selected Solution {3} Solution Volume:{2}]", FluidicsInterface.Pump.PumpAbsolutePos, _Valve.CurrentPos,
                    FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol, _PumpSetting.SelectedSolution.ValveNumber), false);

                //pump thread
                FluidicsInterface.OnPumpingCompleted += RunPuming_Completed;
                FluidicsInterface.RunPumping(_CallingDispatcher, 12, _PumpSetting, true, IsSimulationMode);


                if (!IsSimulationMode)
                {
                    FluidicsInterface.WaitForPumpingCompleted();
                }

                OnStepRunUpdatedInvoke(step, string.Format("[After pumping: Pump position: {0}, Valve current position: {1},  Selected Solution {3} Solution Volume:{2}]", FluidicsInterface.Pump.PumpAbsolutePos, _Valve.CurrentPos,
                FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol, _PumpSetting.SelectedSolution.ValveNumber), false);
                // do not check retrys if in simulation
                trycounts++;
                if (trycounts > 5 && !IsSimulationMode)
                {
                    OnStepRunUpdatedInvoke(step, string.Format("Pumping step tried 5 times, failed."), true);
                    OnStepRunUpdatedInvoke(step, string.Format("Pumping step failed, recipe stop."), true);
                    AbortWork();
                    ExitStat = ThreadExitStat.Error;
                    throw new System.InvalidOperationException("Pumping Failure");
                }
            }
            // do not check difference from setpoint in simulations
            while (!IsSimulationMode
                    && Math.Abs(FluidicsInterface.Solutions[_PumpSetting.SelectedSolution.ValveNumber - 1].SolutionVol - predvol) > 3 * 4
                    && _PumpSetting.SelectedPushPath != PathOptions.Manual
                    && _PumpSetting.SelectedPullPath != PathOptions.Manual);

            Logger.LogMessage($"End Pumping for {Enum.GetName(typeof(ModeOptions), step.PumpingType)}.");
        }

        private void RunPuming_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            FluidicsInterface.OnPumpingCompleted -= RunPuming_Completed;
            if (exitState == ThreadBase.ThreadExitStat.Error)
                Logger.LogMessage("Error occurred during pumping thread, valve failure", SeqLogMessageTypeEnum.ERROR);

            //if (_CallingDispatcher != null)
            //{
            //    _CallingDispatcher.Invoke(() =>
            //    {
            //        // _TecanPump.GetPumpPos();
            //        if (exitState == ThreadBase.ThreadExitStat.Error)
            //        {
            //            if (!IsSimulationMode)
            //            {
            //                MessageBox.Show("Error occurred during pumping thread, valve failure");
            //            }
            //            else
            //            {
            //                Logger.LogMessage("Error occurred during pumping thread, valve failure", SeqLogMessageTypeEnum.ERROR);
            //            }
            //        }
            //        //_RunTecanPumingThread.Completed -= _RunTecanPumingThread_Completed;
            //        //_RunTecanPumingThread = null;
            //    });
            //}
            //else
            //{
            //    Logger.LogMessage("Error occurred during pumping thread, valve failure", SeqLogMessageTypeEnum.ERROR);
            //}
        }

        private void RunStepProc(RunRecipeStep step)
        {
            int startInc = _StartInc;
            if (_CurrentTree.Parent != null)
            {
                if (_CurrentTree.Parent.Step.StepType == RecipeStepTypes.Loop)
                {
                    startInc = ((LoopStep)_CurrentTree.Parent.Step).LoopCounts + _StartInc;
                }
            }
            else
            {
                startInc = _StartInc;
            }
            _RecipeParameters.StartInc = startInc;
            string recipePath = CheckInnerRecipePath(step);//.RecipePath;
            Logger.Log($"Loading inner recipe from {recipePath}");

            Recipe recipe = Recipe.LoadFromXmlFile(recipePath);
            Stopwatch sw = Stopwatch.StartNew();
            step.RecipeName = recipe.RecipeName;
            _InnerRecipeRunThread = new RecipeRunThreadV2(_CallingDispatcher, RecipeRunConfig, recipe,
                _camera1, _camera2, _MotionController, _MainBoard, _LEDController, FluidicsInterface,
                _RecipeParameters, this, OLAJobs, false, null, false);
            _InnerRecipeRunThread.OnRecipeRunUpdated += _InnerRecipeRunThread_OnRecipeRunUpdated;
            _InnerRecipeRunThread.OnStepRunUpdated += _InnerRecipeRunThread_OnStepRunUpdated;
            _InnerRecipeRunThread.OnLoopStepUpdated += _InnerRecipeRunThread_OnLoopStepUpdated;
            _InnerRecipeRunThread.Completed += _InnerRecipeRunThread_Completed;
            _InnerRecipeRunThread.Name = "Inner Recipe";
            _InnerRecipeRunThread.IsSimulationMode = IsSimulationMode;
            _InnerRecipeRunThread.IsEnablePP = IsEnablePP;
            Logger.LogMessage($"Inner Recipe: {recipe.RecipeName} starts, startInc={startInc}");
            _InnerRecipeRunThread.Start();
            _InnerRecipeRunThread.Join();
            OnStepRunUpdatedInvoke(step, string.Format("[{0}]: Update Recipe", Thread.CurrentThread.Name), false);
            //why need ave recipe here ??
            Logger.Log($"Save inner recipe {recipePath}");
            Recipe.SaveToXmlFile(recipe, recipePath);// step.RecipePath);
            while (_InnerRecipeRunThread != null)
            {
                Thread.Sleep(100);
            }
            string innerRecipeName = Path.GetFileName(recipePath);
            Logger.Log($"Inner recipe {innerRecipeName} elapsed time [ms] | {sw.ElapsedMilliseconds}", SeqLogFlagEnum.BENCHMARK);
            Logger.LogMessage($"Inner Recipe: {recipe.RecipeName} ends");
        }
        #endregion Run other Step (other than imaging) Functions

        #region Hywire test recipe steps
        private void RunStepProc(HomeMotionStep step)
        {
            try
            {
                Logger.LogMessage(step.ToString());
                int speed = (int)(step.Speed * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                int accel = (int)(SettingsManager.ConfigSettings.MotionHomeSettings[step.MotionType].Accel * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                _MotionController.HomeMotion(step.MotionType, speed, accel, step.WaitForComplete);
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
        private void RunStepProc(AbsoluteMoveStep step)
        {
            try
            {
                Logger.LogMessage(step.ToString());
                int speed = (int)(step.Speed * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                int accel = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[step.MotionType].Accel * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                int pos = (int)(step.TargetPos * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                bool result = false;
                int tryCnts = 0;
                do
                {
                    result = _MotionController.AbsoluteMove(step.MotionType, pos, speed, accel, step.WaitForComplete);
                    tryCnts++;
                    if (tryCnts > 5)
                    {
                        throw new Exception(string.Format("Failed to Move {0} in AbsoluteMoveStep;", step.MotionType));
                    }
                }
                while (result == false);
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
        private void RunStepProc(RelativeMoveStep step)
        {
            try
            {
                Logger.LogMessage(step.ToString());
                int speed = (int)(step.Speed * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                int accel = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[step.MotionType].Accel * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]);
                int crntPos = 0;
                while (_MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_X | Hywire.MotionControl.MotorTypes.Motor_Y) == false) ;
                switch (step.MotionType)
                {
                    case MotionTypes.XStage:
                        crntPos = _MotionController.FCurrentPos;
                        break;
                    case MotionTypes.YStage:
                        crntPos = _MotionController.YCurrentPos;
                        break;
                    default:
                        Logger.LogWarning("Invalid motion for RelativeMoveStep.");
                        return;
                }
                int pos = (int)(step.MoveStep * SettingsManager.ConfigSettings.MotionFactors[step.MotionType]) + crntPos;
                bool result = false;
                int tryCnts = 0;
                do
                {
                    result = _MotionController.AbsoluteMove(step.MotionType, pos, speed, accel, step.WaitForComplete);
                    tryCnts++;
                    if (tryCnts > 5)
                    {
                        throw new Exception(string.Format("Failed to Move {0} in AbsoluteMoveStep;", step.MotionType));
                    }
                }
                while (result == false);
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
        private void RunStepProc(HywireImagingStep step)
        {
            try
            {
                Logger.LogMessage(step.ToString());
                // select camera
                LucidCamera selectedCamera = CameraLib.LucidCameraManager.GetAllCameras().Find((p) =>
                {
                    if (p.SerialNumber == step.CameraSN)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
                if (selectedCamera == null)
                {
                    Logger.LogWarning("Invalid Camera.");
                    return;
                }

                // config camera
                selectedCamera.Exposure = step.ExposureTime * 1000;
                selectedCamera.Gain = step.Gain;
                selectedCamera.ADCBitDepth = step.ADCBitDepth;
                selectedCamera.PixelFormatBitDepth = step.PixelBitDepth;
                selectedCamera.RoiStartX = step.ROI.X;
                selectedCamera.RoiStartY = step.ROI.Y;
                selectedCamera.RoiWidth = step.ROI.Width;
                selectedCamera.RoiHeight = step.ROI.Height;
                selectedCamera.UsingContinuousMode = false;
                selectedCamera.EnableTriggerMode = false;

                // config led
                _LEDController.SetLEDIntensity(step.LED, step.Intensity);
                _LEDController.SetLEDControlledByCamera(step.LED, true);

                // grab image
                WriteableBitmap img = null;
                selectedCamera.GrabImage(step.ExposureTime, CaptureFrameType.Normal, ref img);
                if (img == null)
                {
                    Logger.LogWarning("Failed to grab image from camera.");
                    return;
                }
                // get PD value
                _LEDController.GetPDSampledValue();
                uint pdVal = _LEDController.PDSampleValue;

                // save image
                double crntPosX = Math.Round(_MotionController.FCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage], 4);
                double encPosX = Math.Round(_MotionController.XEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.XStage], 4);
                double crntPosY = Math.Round(_MotionController.YCurrentPos / SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage], 4);
                double encPosY = Math.Round(_MotionController.YEncoderPos / SettingsManager.ConfigSettings.MotionEncoderFactors[MotionTypes.YStage], 4);
                string imageName = string.Format("{4}_CrntX{0}_EncX{1}_CrntY{2}_EncY{3}_{4}_PD{5}.tif", crntPosX, encPosX, crntPosY, encPosY, DateTime.Now.ToString("MMddhhmmsss"), pdVal);
                using (FileStream fs = new FileStream(imageName, FileMode.Create))
                {
                    TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(fs);
                }
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
            finally
            {
                _LEDController.SetLEDControlledByCamera(step.LED, false);
            }
        }
        private void RunStepProc(LEDControlStep step)
        {
            try
            {
                // set intensity if intensity > 0
                if (step.Intensity > 0)
                {
                    _LEDController.SetLEDIntensity(step.LED, step.Intensity);
                }

                if (step.SetOn)
                {
                    _LEDController.TurnOnLEDWhileTurnOffOthers(step.LED);
                }
                else
                {
                    _LEDController.SetLEDStatus(step.LED, false);
                }
            }
            catch (ThreadAbortException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
            finally
            {

            }
        }
        #endregion Hywire test recipe steps
        //-------------------------------------------------------------------------------------------------
        //to do: need better design for not using MessageBox in any library dll, messagebox should only go to UI code such as view model.
        bool StopOnFailure(string message, bool useLastAnswer = false)
        {

            if (_CallingDispatcher != null)
            {
                bool b;
                if (useLastAnswer && _StopOnFailureAnswers.ContainsKey(message))
                {
                    b = _StopOnFailureAnswers[message];
                }
                else
                {
                    MessageBoxResult msgResult = MessageBoxResult.None;
                    _CallingDispatcher.Invoke(() =>
                    {
                        msgResult = MessageBox.Show(message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    });
                    b = msgResult == MessageBoxResult.Yes;
                    if (!_StopOnFailureAnswers.ContainsKey(message))
                    {
                        _StopOnFailureAnswers.Add(message, b);
                    }
                }
                return b;
            }
            else
            {
                return true; //stop
            }
        }

        //to do : should remove message box, same reason as previous method
        void InformError(string message)
        {
            _CallingDispatcher?.Invoke(() =>
            {
                MessageBox.Show(ExMessage);
            });
        }
    }
}
