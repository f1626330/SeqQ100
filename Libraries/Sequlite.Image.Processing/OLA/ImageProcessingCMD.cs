using Sequlite.ALF.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Sequlite.Image.Processing.utils;

namespace Sequlite.Image.Processing
{
    /// <summary>
    /// Process Instrument images files all the way to FASTQ
    /// </summary>

#if false
    public class ImageProcessingExeInfo
    {
        Thread ExecutingThread;
        public enum enImageprocessingExeType
        {
            eBuildTmplt,
            eExtractInt,
            eJoinCycles,
            eBaseCall
        };
        public enImageprocessingExeType eImageprocessingExeType;
        public double AnticipatedMemoryUsage_GB;
    }

    public class ImageProcessingEventArgs : EventArgs
    {
        public ImageProcessingExeInfo Info { get; set; }
    }
#endif
    public class BaseCallRangeInfo
    {
        public int Start { get; set; } = -1;  // 0-based
        public int End { get; set; } = -1;    // 0-based
        public int Overlap { get; set; } = -1; // Number of overlapping cycles wrt previous range
        public int NextOverlap { get; set; } = -1; // Number of overlapping cycles wrt next range

        // From BaseCall help:
        // -g/--globalPhasing phasing parameters: [[0, 0, 0, 0, 1, 1, 0, 0, 0, -1, -1, 0]]
        // ...
        // 11    - n>0 - sparse map max(n, range width) cycles, n<=0 - load all cycles
        public int SparseCycleMappingFlag { get; set; } = 0; // by default - load all available cycles
        public bool UseYngrams { get; set; } = false; // is using y-ngrams configured for this range?

        public bool IsValid()
        {
            return (Start > -1 && End > -1 && Start < End && !(Start > 0 && Overlap < 0));
        }
    }

    public static class MyExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems)
        {
            return items.Select((item, inx) => new { item, inx })
                        .GroupBy(x => x.inx / maxItems)
                        .Select(g => g.Select(x => x.item));
        }
    }

    public class ImageProcessingCMD
    {
        public class TileProcessingProgress
        {
            public bool Prepared { get; set; } = false;
            public bool FailedTile { get; set; } = false;

            public Dictionary<int, bool> ImageExtracted = new Dictionary<int, bool>();
            public int LastCycleTemplateCreated { get; set; } = 0;
            public int LastCycleExtracted { get; set; } = 0;
            public int LastCycleJoined { get; set; } = 0;
            public int LastCycleCalled { get; set; } = 0;
            public float LastBuildTmplt_RAM_GB { get; set; } = 0f;
            public float LastExtractInt_SingleImage_RAM_GB { get; set; } = 0f;
            public float LastExtractInt_RAM_GB { get; set; } = 0f;
            public int LastExtractInt_Concurrency { get; set; } = 0;
            public float LastBaseCall_RAM_GB { get; set; } = 0f;

            // These parameters are needed to decide if we could re-use a previously-generated "base-clust" file in a sliding window scenario
            public string LastCyclePhasingParams { get; set; } = "";
            public int LastCycleStageWidthParam { get; set; } = 0;
            //

            public TileProcessingProgress() { }
            public TileProcessingProgress(TileProcessingProgress another)
            {
                Prepared = another.Prepared;
                FailedTile = another.FailedTile;
                LastCycleTemplateCreated = another.LastCycleTemplateCreated;
                LastCycleExtracted = another.LastCycleExtracted;
                LastCycleJoined = another.LastCycleJoined;
                LastCycleCalled = another.LastCycleCalled;
                LastBuildTmplt_RAM_GB = another.LastBuildTmplt_RAM_GB;
                LastExtractInt_SingleImage_RAM_GB = another.LastExtractInt_SingleImage_RAM_GB;
                LastExtractInt_RAM_GB = another.LastExtractInt_RAM_GB;
                LastExtractInt_Concurrency = another.LastExtractInt_Concurrency;
                LastBaseCall_RAM_GB = another.LastBaseCall_RAM_GB;
                LastCyclePhasingParams = another.LastCyclePhasingParams;
                LastCycleStageWidthParam = another.LastCycleStageWidthParam;
            }
        }

        public class CommandOptions
        {
            protected Dictionary<string, string> options { get; set; } = new Dictionary<string, string>();
            public CommandOptions(string command)
            {
                if (!string.IsNullOrEmpty(command))
                {
                    if (command.EndsWith(" proc.txt"))
                        command = command.Substring(0, command.LastIndexOf(" proc.txt"));

                    MatchCollection matches = Regex.Matches(command, "-{1}[a-zA-Z] ");
                    int[] ar = matches.Cast<Match>().Select(m => m.Index).ToArray();
                    for (int i = 0; i < ar.Count(); i++)
                    {
                        string key = command.Substring(ar[i] + 1, 1);
                        string value = "";
                        if (i < ar.Count() - 1)
                            value = command.Substring(ar[i] + 3, ar[i + 1] - ar[i] - 4);
                        else
                            value = command.Substring(ar[i] + 3, command.Length - (ar[i] + 3));

                        options[key] = value;
                    }
                }
            }
        }

        public class GeneralCommandOptions : CommandOptions
        {
            public int log { get; set; } = 1;               // -l
            public int numOfThreads { get; set; } = 0;      // -n
            public int appendOutput { get; set; } = 0;      // -O
            public int saveBinary { get; set; } = 0;        // -s
            public int saveCSV { get; set; } = 0;           // -S

            public GeneralCommandOptions(string command) : base(command)
            {
                if (options.ContainsKey("l"))
                    log = int.Parse(options["l"]);
                if (options.ContainsKey("n"))
                    numOfThreads = int.Parse(options["n"]);
                if (options.ContainsKey("O"))
                    appendOutput = int.Parse(options["O"]);
                if (options.ContainsKey("s"))
                    saveBinary = int.Parse(options["s"]);
                if (options.ContainsKey("S"))
                    saveCSV = int.Parse(options["S"]);
            }
        }

        public class FindBlobsOptions : GeneralCommandOptions
        {
            // FindBlobs
            public int useCorners { get; set; } = 0;                    // -C Join corner connected blobs
            public float[] filter { get; set; } = { 0, 1, 0, 0 };       // -f Use Fourier space filtering
            public int[] tileGeom { get; set; } = { 0, 0, 1 };          // -G Tile geometry; may be different for BuildTmplt vs ExtractInt
            public int pixInt { get; set; } = 0;                        // -I Type of sub-pixel interpolation
            public int saveMask { get; set; } = 0;                      // -K Save binary mask
            public int imageNum { get; set; } = -1;                     // -N Image number in a stack
            public int[] pixelLimits { get; set; } = { -1, -1 };        // -p Set blob pixel limits "min max"
            public int savePNG { get; set; } = 0;                        // -P Save view file

            public float[] imageQuality { get; set; } = { 2.5f, 0.6f };     // -Q save QC CSV files with (1) SNR threshold, (2) chastity theshold: [2.5,0.6]

            public float resolution { get; set; } = 0;                   // -r
            public float threshDiv { get; set; } = 2;                    // -t Threshold divider 

            // BuildTmplt
            public float[] allowedShifts { get; set; } = { 0, 0 };      // -a
            public string loadAT { get; set; } = "";                    // -A Load transform
            public float mergeFactor { get; set; } = 1;                 // -F Additional merge when building or serching template
            public int[] cropLocs { get; set; } = { -1, -1, -1, -1 };   // -j Crop locations[L R T B]
            public int meanNorm { get; set; } = 0;                      // -m Normalize stack by mean when merging blobs
            public int mergePerImage { get; set; } = 0;                 // -M Merge in each image when building template
            public int[] buildTmplt { get; set; } = { 1, 0, 1 };        // -T Build template using N images from a stack

            // ExtractInt
            public float[] extract { get; set; } = { -1, 21, 0, 0.75f, 0, 0, 5 };   // -e extract intensities/background and noise using masks
            public string loadLocs { get; set; } = "";                      // -L Load blob locations
            public string loadRefImage { get; set; } = "";                  // -R load reference image
            public int idAtError { get; set; } = 0;                         // -X Output identity transform if stage error is detected

            // BaseCall
            public string baseOrder { get; set; } = "CATG";                     // -b Base order
            public float minClustBallRad { get; set; } = 1e-15f;                // -B smallest ball radius for clustering
            public int callByScore { get; set; } = -1;                          // -c Use consensus or score to make call
            public int debugClust { get; set; } = 0;                            // -d Output cluster debug data
            public int clusterDim { get; set; } = 2;                            // -D Dimensions of stage used for clustering
            public float clusterEps { get; set; } = 0.5f;                       // -E clustering accuracy    
            public float[] globalPhasing { get; set; } = { 0, 0, 0, 0, 1, 1, 0, 0, 0, -1, -1, 0 };     // -g Phasing parameters
            public int numColors { get; set; } = 2;                             // -H Number of colors for base calling
            public int basePerColor { get; set; } = 1;                          // -k Number of bases per color
            public string loadIntensities { get; set; } = "";                   // -i Load intensities from binary file
            public int seqWidth { get; set; } = -1;                             // -W output sequences with more than n bases
            public float[] clusterNorm { get; set; } = { 0, -1, -1 };           // -q Cluster normalization type and range
            public int clusterField { get; set; } = 0;                          // -o Intensity type to use for clustering
            public string clusterFile { get; set; } = "";                       // -u Name of base cluster file
            public string[] weightBaseFiles { get; set; } = { "", "", "" };       // -w Names of base matrix weight/eq/digram files: [,]
            public int[] stagesRange { get; set; } = { -1, -1 };                // -x Process only a range of stages
            public string qMapFile { get; set; } = "";                          // -U Name of q-score map file

            public FindBlobsOptions(string command) : base(command)
            {
                // FindBlobs
                if (options.ContainsKey("C"))
                    useCorners = int.Parse(options["C"]);
                if (options.ContainsKey("f"))
                    StringHelper.StringToInitializedArray<float>(options["f"], filter);
                if (options.ContainsKey("G"))
                    StringHelper.StringToInitializedArray<int>(options["G"], tileGeom);
                if (options.ContainsKey("I"))
                    pixInt = int.Parse(options["I"]);
                if (options.ContainsKey("K"))
                    saveMask = int.Parse(options["K"]);
                if (options.ContainsKey("N"))
                    imageNum = int.Parse(options["N"]);
                if (options.ContainsKey("p"))
                    StringHelper.StringToInitializedArray<int>(options["p"], pixelLimits);
                if (options.ContainsKey("P"))
                    savePNG = int.Parse(options["P"]);
                if (options.ContainsKey("Q"))
                    StringHelper.StringToInitializedArray<float>(options["Q"], imageQuality);
                if (options.ContainsKey("r"))
                    resolution = float.Parse(options["r"]);
                if (options.ContainsKey("t"))
                    threshDiv = float.Parse(options["t"]);

                // BuildTmplt
                if (options.ContainsKey("a"))
                    StringHelper.StringToInitializedArray<float>(options["a"], allowedShifts);
                if (options.ContainsKey("A"))
                    loadAT = options["A"];
                if (options.ContainsKey("F"))
                    mergeFactor = float.Parse(options["F"]);
                if (options.ContainsKey("j"))
                    StringHelper.StringToInitializedArray<int>(options["j"], cropLocs);
                if (options.ContainsKey("m"))
                    meanNorm = int.Parse(options["m"]);
                if (options.ContainsKey("M"))
                    mergePerImage = int.Parse(options["M"]);
                if (options.ContainsKey("T"))
                    StringHelper.StringToInitializedArray<int>(options["T"], buildTmplt);

                // ExtractInt
                if (options.ContainsKey("e"))
                    StringHelper.StringToInitializedArray<float>(options["e"], extract);
                if (options.ContainsKey("L"))
                    loadLocs = options["L"];
                if (options.ContainsKey("R"))
                    loadRefImage = options["R"];
                if (options.ContainsKey("X"))
                    idAtError = int.Parse(options["X"]);

                // BaseCall
                if (options.ContainsKey("b"))
                    baseOrder = options["b"].Trim('\"');
                if (options.ContainsKey("B"))
                    minClustBallRad = float.Parse(options["B"]);
                if (options.ContainsKey("c"))
                    callByScore = int.Parse(options["c"]);
                if (options.ContainsKey("d"))
                    debugClust = int.Parse(options["d"]);
                if (options.ContainsKey("D"))
                    clusterDim = int.Parse(options["D"]);
                if (options.ContainsKey("E"))
                    clusterEps = float.Parse(options["E"]);
                if (options.ContainsKey("g"))
                    StringHelper.StringToInitializedArray<float>(options["g"], globalPhasing);
                if (options.ContainsKey("H"))
                    numColors = int.Parse(options["H"]);
                if (options.ContainsKey("k"))
                    basePerColor = int.Parse(options["k"]);
                if (options.ContainsKey("i"))
                    loadIntensities = options["i"];
                if (options.ContainsKey("W"))
                    seqWidth = int.Parse(options["W"]);
                if (options.ContainsKey("q"))
                    StringHelper.StringToInitializedArray<float>(options["q"], clusterNorm);
                if (options.ContainsKey("o"))
                    clusterField = int.Parse(options["o"]);
                if (options.ContainsKey("u"))
                    clusterFile = options["u"];
                if (options.ContainsKey("w"))
                    weightBaseFiles = options["w"].Trim('\"').Split(' ');
                if (options.ContainsKey("x"))
                    StringHelper.StringToInitializedArray<int>(options["x"], stagesRange);
                if (options.ContainsKey("U"))
                    qMapFile = options["U"];
            }
        }

        public static readonly string LogFileName = "trackingCMD.txt";
        public static readonly string ProgressFileName = "TileProgress.json";

        static readonly bool _LogCMDLineOutput = false;
        string LogFile = "";
        public static readonly string LoggerSubSystemName = "ImageProc";
        public static readonly string GoodTilesFileName = "GoodTiles.txt";
        public ISeqLog Logger = null;

        DateTime SeqPSoftwareDate;

        public bool LogToMainLogFile { get; set; } = true;
        public bool IsAbort { get; set; } = false;

        int _MinTemplateCycle = 4;
        public int MinTemplateCycle { get => _MinTemplateCycle; set => _MinTemplateCycle = value; }

        int _baseCallMinCycle = 25;
        public int BaseCallMinCycle { get => _baseCallMinCycle; set => _baseCallMinCycle = value; }

        int _baseCallEveryNthCycle = 5;
        public int BaseCallEveryNthCycle { get => _baseCallEveryNthCycle; set => _baseCallEveryNthCycle = value; }

        // key == tile name, 
        public Dictionary<string, TileProcessingProgress> TileProgress = new Dictionary<string, TileProcessingProgress>();

        //--- following parameters are from run setting config file  (run.sh)--------
        string _RN = "";
        public string RN { get => _RN; set => _RN = value; }
        int _NS = -1;
        public int NS { get => _NS; set => _NS = value; }
        string _BO = "";
        public string BO { get => _BO; set => _BO = value; }
        string _CE = "";
        public string CE { get => _CE; set => _CE = value; }
        string _TP = "";
        public string TP { get => _TP; set => _TP = value; }
        int _NC = -1;
        public int NC { get => _NC; set => _NC = value; }
        int _CD = 8;
        public int CD { get => _CD; set => _CD = value; }
        float[] PP { get; set; } = { 0, 0, 0, 0, 1, 1, 0, 0, 0, -1, -1, 0 };     // -g Phasing parameters
        string _CN = "1 0 1";                                       // cluster-norm  -q
        string _FT = "0.1 1 1 0";                                   // filter-tmplt  -f
        public string FT { get => _FT; set => _FT = value; }
        string _FE = "0.1 1 1 0";                                   // filter-extr   -f
        public string FE { get => _FE; set => _FE = value; }
        public string CN { get => _CN; set => _CN = value; }
        string _EO = "-S 2 -n 1 -s 1 -r 1 -t 0.5 -M 0 -T 1 -O 0 -Q 0 -F 2";
        public string EO { get => _EO; set => _EO = value; }
        string _TO = "-S 2 -n 0 -s 0 -r 1 -t 0.5 -e 0 -M 0 -F 2 -P 0 -K 0 -Q 0 -m 0";
        public string TO { get => _TO; set => _TO = value; }
        string _CO = "-S 1 -n 0 -s 0 -t 0 -c 1 -W 1 -Q 0 -k 1 -o 0 -d 1 -B 0.01 -E 0.5";
        public string CO { get => _CO; set => _CO = value; }
        string _JO = "-S 1";
        public string JO { get => _JO; set => _JO = value; }
        int _RI = 0;
        public int RI { get => _RI; set => _RI = value; }
        int _BC = 1;
        public int BC { get => _BC; set => _BC = value; }

        string _BW = "";                                            // base-weight
        public string BW { get => _BW; set => _BW = value; }

        // base-ngram
        string _BE = "";
        public string BE { get => _BE; set => _BE = value; }

        string _BD = "";
        public string BD { get => _BD; set => _BD = value; }

        string _BT = "";
        public string BT { get => _BT; set => _BT = value; }

        string _B4 = "";
        public string B4 { get => _B4; set => _B4 = value; }

        string _B5 = "";
        public string B5 { get => _B5; set => _B5 = value; }

        // base-bngram
        string _BB = "";
        public string BB { get => _BB; set => _BB = value; }

        // base-yngram
        string _YG = "";
        public string YG { get => _YG; set => _YG = value; }

        string _EP = "1 11 0.1 0.5";
        public string EP { get => _EP; set => _EP = value; }

        // q-map
        string _QM = "";
        public string QM { get => _QM; set => _QM = value; }

        // base call q parameter
        string _CQ = "";
        public string CQ { get => _CQ; set => _CQ = value; }

        string _IN = "0";
        public string IN { get => _IN; set => _IN = value; }

        // Distortion maps for a single-image ExtractInt
        string _EDS = "";
        public string EDS { get => _EDS; set => _EDS = value; }

        // Distortion maps for a multiple-image ExtractInt
        string _EDM = "";
        public string EDM { get => _EDM; set => _EDM = value; }

        //----end run parameters -------------------------------------------------------

        public int CycleFrames { get => NS * NC; }

        //Constants ---------------------------------------
        const string _SEQP = "pwd";
        const string _ARCH = "uname";

        string _ST = "350 0"; //shift-tmplt          -a
        string _TT = "3 3 1"; //tile-tmplt           -G

        string _SE = "350 4"; //shift-extr           -a
        string _TE = "0 0 1"; //tile-extr            -G

        public string SEQP => _SEQP;
        public string ARCH => _ARCH;
        public string ST => _ST;
        public string SE => _SE;
        public string TT => _TT;
        public string TE => _TE;
        //end Constants ------------------------------------

        bool _DoNotExe = false;
        public bool DoNotExe { get => _DoNotExe; set => _DoNotExe = value; }

        List<string> _FCList = new List<string>();
        public List<string> FCList
        {
            get
            {
                return _FCList;
            }
            set
            {
                _FCList = value;
                foreach (string tileName in _FCList)
                {
                    if (!TileProgress.ContainsKey(tileName))
                        TileProgress[tileName] = new TileProcessingProgress();
                }
            }
        }

        bool _enableBaseStatistics = true;
        public bool EnableBaseStatistics { get => _enableBaseStatistics; set => _enableBaseStatistics = value; }

        public ulong OLAProcessorAffinityMask { get; set; }

        public DirectoryInfo OLABinDir { get; }

        DirectoryInfo _WorkingDir = null;
        public DirectoryInfo WorkingDir { get { return _WorkingDir; } set => _WorkingDir = value; }

        DirectoryInfo _QualityDir = null;
        public DirectoryInfo QualityDir { get { return _QualityDir; } set => _QualityDir = value; }

        DirectoryInfo _ImageTemplateBaseDir = null;
        public DirectoryInfo ImageTemplateBaseDir { get { return _ImageTemplateBaseDir; } set => _ImageTemplateBaseDir = value; }

        DirectoryInfo _ImageDataDir = null;
        public DirectoryInfo ImageDataDir { get { return _ImageDataDir; } set => _ImageDataDir = value; }

        string IndexBinPath { get; set; } = "";

        public RecipeRunSettings RecipeRunConfig { get; set; } = null;
        public bool UseSlidingWindow { get; set; } = false;

        //public bool IsPostProcessing { get; set; } = false;

        TemplateOptions Template { get; set; } = TemplateOptions.ecoli;
        bool IsV2 { get; set; } = false;

        public enum enImageprocessingExeType
        {
            eAll,
            eFindBlobs,
            eBuildTmplt,
            eExtractInt,
            eJoinCycles,
            eBaseCall
        };

        private static PerformanceCounter _RAMCounter = new PerformanceCounter("Memory", "Available MBytes");

        private bool MustBuildTemplate { get; set; } = false;

        //Thread DispatcherThread { get; set; }
        //Dictionary<string, Queue<ImageProcessingExeInfo>> DispatcherQueue;

        //public event EventHandler<ImageProcessingEventArgs> OnDispatcherEvent;
        //public void OnDispatcherEventInvoke(ImageProcessingEventArgs e)
        //{
        //    OnDispatcherEvent?.Invoke(this, e);
        //}

        public ImageProcessingCMD(string workingDir,
                                  string imageDataDir,
                                  string imageTemplateBaseDir,
                                  string qualityDir,
                                  TemplateOptions template,
                                  bool isV2,
                                  int totalCycles,
                                  bool mustBuildTemplate)
        {
            WorkingDir = new DirectoryInfo(workingDir);
            ImageDataDir = new DirectoryInfo(imageDataDir);
            ImageTemplateBaseDir = new DirectoryInfo(imageTemplateBaseDir);
            QualityDir = new DirectoryInfo(qualityDir);
            Template = template;
            IsV2 = isV2;
            NS = totalCycles;
            MustBuildTemplate = mustBuildTemplate;

            LogFile = Path.Combine(WorkingDir.FullName, LogFileName);
            RecipeRunConfig = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig;
            OLABinDir = new DirectoryInfo(RecipeRunConfig.GetOLABinDir(""));

            //DispatcherQueue["BuildTmplt"] = new Queue<ImageProcessingExeInfo>();
            //DispatcherQueue["ExtractInt"] = new Queue<ImageProcessingExeInfo>();
            //DispatcherQueue["JoinCycles"] = new Queue<ImageProcessingExeInfo>();
            //DispatcherQueue["BaseCalll"] = new Queue<ImageProcessingExeInfo>();
            //DispatcherThread = new Thread(() => DispatchExecution());
            //DispatcherThread.IsBackground = true;
            //DispatcherThread.Name = "ImageProcessingExeDispatcher";
            //DispatcherThread.Start();
        }

        //private void DispatchExecution()
        //{
        //    while (true)
        //    {
        //        if (IsAbort)
        //        {
        //            break;
        //        }
        //        lock (DispatcherQueue)
        //        {
        //        }

        //        if (false)
        //        {
        //            Thread.Sleep(2000);
        //            continue;
        //        }

        //        // Process all unprocessed cycles
        //        if (false)
        //        {
        //            try
        //            {
        //                //Logger.Log(String.Format("OLA job starts for cycle{0}", newRunningCycle));
        //            }
        //            catch (Exception ex)
        //            {
        //                //Logger.LogError(string.Format("Failed to run OLA job for cycle with exception: {1} \n{2]", newRunningCycle, ex.Message, ex.StackTrace));
        //            }
        //        }
        //        else
        //        {
        //            // The OLA job manager is done with the last cycle, but another cycle may still be coming from the recipe (CompletedCycle==Cycle and IsRecipeWaitingForOLACompletion==false)
        //            Thread.Sleep(2000);
        //        }
        //    }
        //    Logger.Log("ImageProcessingExeDispatcher exits.");
        //}

        public void Log(string msg, SeqLogFlagEnum flag = SeqLogFlagEnum.NORMAL, DirectoryInfo di = null)
        {
            LogToFile(msg, flag, di);
        }

        private string CalcPhasingParameters(BaseCallRangeInfo range)
        {
            // Per Vitaly, we should use -g [0] -1 for "short" ranges. "This means a single pass basecalling without any iterations of
            // dynamic/global phasing estimation and re-estimation after each new basecalling pass." Vitaly suggested 8 cycles as
            // the shortness criterion. However, that is too restrictie for our purposes as we would like to use -g [0] > -1. Therefore,
            // we are using 5 cycles as the shortness criterion.
            if (range.End - range.Start < 5) 
                PP[0] = -1.0f;

            // RB: commenting out the code below for -g [3], b/c it is not clear whether it improves the OLA matching rate.

            // From Vitaly: "...the multiple iterations are not supposed to work when the calling window is small. (RB: "small calling window" is the same as "short range")
            // You should at the very least turn off updating the intensity at each iteration and use matrix only updates (i.e. to set -g [3] to 0)."
            //if (range.End - range.Start < 8 && int.Parse(strPhasingParams[0]) > -1)
            //{
            //    if (strPhasingParams.Length > 3)
            //        strPhasingParams[3] = "0";
            //}

            // Regarding phasing parameters P[4] and P[5], which are explained in the help as:
            // 4 - n > 0 - apply stage n-1 inverse to all stages
            // 5 - n > 0 - apply cycle n-1 inverse to all cycles,
            // the original instruction from Vitaly was: these parameters should be set to 1, if the lower limit of -x is 0, and to 0 otherwise.
            // But with the original sliding window strategy (no "Early base calling", "Sparse mapping", "PF-only" features enabled), out tests showed that
            // this handling of phasing parameters 4 and 5 improves the matching rate only when yngrams are used. Otherwise, we can keep them at 1 for all -x ranges.
            // Additional comments from Vitaly came when we were implmenting support for the "Early base calling" + "PF-only" features. Vitaly wrote:
            // "When using updated -cpf.fastq and - cpf.blb it would be good to turn on sparse intensity loading to avoid reading in intensities from cycles with incorrect number of clusters.
            // Also,  in -g parameter fields 4 and 5 ... should be set to 0, or alternatively, cycle n(1 - based) and / or stage n(again 1 based) should be re - extracted as well."
            // Testing the sliding window strategy with all the "shortcuts" enabled ("Early base calling", "Sparse mapping", "PF-only") showed that unless we keep
            // P[4] and P[5] at 1 for all -x ranges the Base% graph shows significant A-T separation, which violates the Chargaff's rules. Thefore we have to keep P[4] and P[5} at 1
            // for all -x ranges, which also means we have to keep re-running ExtractInt on the first few cycles (also depending on the stage width -D) when the pool of PF clusters changes.

            //if (!String.IsNullOrEmpty(YG) || RecipeRunConfig.OLABaseCallOnlyPFClusters)
            //{
            //    PP[4] = range.Start == 0 ? 1.0f : 0.0f;
            //    PP[5] = range.Start == 0 ? 1.0f : 0.0f;
            //}

            PP[4] = 1.0f;
            PP[5] = 1.0f;

            PP[11] = range.SparseCycleMappingFlag;

            return String.Join(" ", PP);
        }

        // Calculate the required width of the base calling stage (measured in images: 4 images = 1 cycle)
        // In general, use the -D value from run.sh. Or use a smaller -D, for example, if there is not enough cycles available.
        int CalcBaseCallingStageWidth(BaseCallRangeInfo range)
        {
            Debug.Assert(range.IsValid());

            if (UseSlidingWindow)
            {
                return Math.Min((range.End - range.Start) * 4, _CD);
            }
            else if (range.End < NS)
            {
                return 4; // To speed up OLA in the regular OLA mode (no sliding window) we use a smaller stage size: 1 cycle, or 4 images
            }
            else // At the end of the regular OLA mode (no sliding window) use the -D from run.sh
            {
                return Math.Min(range.End * 4, _CD);
            }
        }

        private void BaseCallByCell(string tileName, DirectoryInfo di, BaseCallRangeInfo range, int threadsCount, out bool pfBlobsCountChanged)
        {
            Debug.Assert(range.IsValid());

            pfBlobsCountChanged = false;
            UInt64 prevPfBlobCount = 0;
            string masterBLB = Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc.blb");
            string cpfBLB = Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc-cpf.blb");
            if (UseSlidingWindow && MustBuildTemplate && RecipeRunConfig.OLABaseCallOnlyPFClusters && range.Start < SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAPFClusterMinimumLength)
            {
                if (!File.Exists(cpfBLB))
                    prevPfBlobCount = GetBlobCountFromFile(masterBLB);
                else
                    prevPfBlobCount = GetBlobCountFromFile(cpfBLB);
            }

            DirectoryInfo paramsDir = new DirectoryInfo(Path.Combine(di.FullName, "params"));
            DirectoryInfo callsDir = new DirectoryInfo(Path.Combine(di.FullName, "calls"));

            StringBuilder sb = new StringBuilder();

            string exePath = Path.Combine(OLABinDir.FullName, "BaseCall-static.exe");

            string newCO = CO.Replace(" -n 0 ", " -n " + threadsCount + " ");

            sb.Append(exePath);
            sb.Append(" " + newCO);

            if (RecipeRunConfig.OLABaseCallOnlyPFClusters)
                sb.Replace(" -S 1", " -S 3"); // output qc files and cpf/npf fastq files
            else if (RecipeRunConfig.OLAMinimumOutput)
                sb.Replace(" -S 1", " -S 2"); // output qc files, cpf/npf/clr fastq files

            if (RecipeRunConfig.OLAUseJoinCycles)
                sb.Append($" -i proc-int.bli");
            else
                sb.Append(" -i ..\\extr\\");

            if (!File.Exists(cpfBLB))
                sb.Append(" -L " + masterBLB);
            else
                sb.Append(" -L " + cpfBLB);

            string phasingParams = CalcPhasingParameters(range);
            sb.Append($" -g \"{phasingParams}\"");

            sb.Append(" -q \"" + CN + "\"");
            sb.Append(" -b \"" + BO + "\"");
            sb.Append(" -H " + NC);

            int stageWidth = CalcBaseCallingStageWidth(range);
            sb.Append($" -D {stageWidth}");

            sb.Append(" -w \"..\\params\\base-weight ..\\params\\base-ngram ..\\params\\base-bngram ..\\params\\base-index");

            if (range.UseYngrams)
            {
                Debug.Assert(!String.IsNullOrEmpty(YG));
                sb.Append(" ..\\params\\base-yngram\"");
            }
            else
                sb.Append("\"");

            // From Vitaly: "The base-clust file has cycle crosstalk (1st line) and stage crosstalk/phasing (2nd line) parameters.
            // They can be set manually  before run and/or they are saved after processing for possible reuse in partial stage procession."
            sb.Append(" -u \"..\\params\\base-clust\"");

            // We will re-use the base-clust file only if the previous stage width and phasing parameters are the same as the current ones 
            //if (!(TileProgress[tileName].LastCyclePhasingParams == phasingParams && TileProgress[tileName].LastCycleStageWidthParam == stageWidth))
            //{
            //    WriteParams(paramsDir, "base-clust", "");
            //}

            if (TileProgress[tileName].LastCycleStageWidthParam != stageWidth)
            {
                WriteParams(paramsDir, "base-clust", "");
            }

            if (!CO.Contains("-Q"))
                sb.Append(" -Q \"" + CQ + "\"");

            if (!String.IsNullOrEmpty(QM))
            {
                if (SeqPSoftwareDate < new DateTime(2022,5,12) )
                {
                    sb.Append(" -U " + QM);
                }
                else
                {
                    sb.Append(" -U \"" + QM + " 1\"");
                }
            }

            // From Vitaly:
            //       -x/--stagesRange        process only a range of stages: [[]]
            //last odd element: [1]
            //        0 - reject updates for out of range cycles,                                
            //       &1 - permit updates for out of range cycles (default),                      
            //       &2 - permit updates and use PFs for out of range cycles,                
            //       &4 - smooth sliding window cycles only,              
            //       &8 - smooth sliding window and out of range cycles
            //-x "x y 2" sets the use of scores from the previous sliding window when defining new PF clusters where as -x "x y 1" or -x "x y 0" use only the current sliding window.
            ulong loiFlag = 0;
            if (RecipeRunConfig.OLAUseScoresFromPreviousRangeWhenDefiningPFClusters)
                loiFlag += 2;
            else if (RecipeRunConfig.OLAOutOfRangeBaseCallAllowed)
                loiFlag += 1;

            if (RecipeRunConfig.OLASmoothBCQCIncludeOutOfRange)
                loiFlag += 8;
            else if (RecipeRunConfig.OLASmoothBCQC)
                loiFlag += 4;

            sb.Append(" -x " + $"\"{range.Start} {range.End} {loiFlag}\"");

            Log($"Base calling window: {range.Start}-{range.End}; Overlap: {range.Overlap}; Next Overlap: {range.NextOverlap}; Sparse Mapping Flag: {range.SparseCycleMappingFlag}; Use Yngrams: {range.UseYngrams}");

            // From Vitaly: "...set -J/--nPF flag to '25 1'  when doing a sliding window base calling and it will save a new proc-loc-cpf.blb file
            // that contains only PF clusters. After that you should be able to use this proc-loc-cpf.blb together with proc-int-cpf.fastq for subsequent
            // sliding window base calling and only PF clusters will then be used. This new proc-loc-cpf.blb should also be used instead of the original
            // proc-loc.blb for intensity extraction, and it is important to remember that for any cycles overlapping between two sliding windows you will
            // need to rerun the intensity extraction."
            if (UseSlidingWindow && MustBuildTemplate && RecipeRunConfig.OLABaseCallOnlyPFClusters && range.Start < SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAPFClusterMinimumLength)
                sb.Append($" -J \"{RecipeRunConfig.OLAPFClusterMinimumLength} 1\"");
            else
                sb.Append($" -J {RecipeRunConfig.OLAPFClusterMinimumLength}");

            if (RecipeRunConfig.OLACatchHardwareExceptionsInCpp)
                sb.Append(" -y 1");

            // This has to be the last entry on the command line
            sb.Append(" proc.txt");

            // Update base-bngram and base-index files specified by the -w option
            UpdateBaseBngramFile(di, range);
            UpdateBaseIndexFile(di, tileName, range);

            bool ok = false;
            float maxRAM_GB = 0f;
            if (RecipeRunConfig.OLAUseDLL)
                ok = ExecuteCommandDLL(enImageprocessingExeType.eBaseCall, sb.ToString(), tileName, di, out maxRAM_GB);
            else
                ok = ExecuteCommandSync(sb.ToString(), callsDir, out maxRAM_GB);

            if (ok)
                LogToFile($"Cycle:{range.End},LastBaseCall_RAM_GB:{maxRAM_GB}", SeqLogFlagEnum.DEBUG);

            if (IsAbort)
                return;

            if (ok)
                ok = ManageFilesAfterBaseCall(tileName, di, range);

            if (ok)
            {
                if (UseSlidingWindow && MustBuildTemplate && RecipeRunConfig.OLABaseCallOnlyPFClusters && range.Start < SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAPFClusterMinimumLength)
                {
                    if (File.Exists(cpfBLB))
                    {
                        UInt64 curPfBlobCount = GetBlobCountFromFile(cpfBLB);
                        if (prevPfBlobCount != curPfBlobCount)
                        {
                            Debug.Assert(prevPfBlobCount >= curPfBlobCount); // as the sliding window progresses, the number of PF clusters can only decrease
                            pfBlobsCountChanged = true;
                        }
                    }
                }
            }


            if (ok)
            {
                lock (TileProgress)
                {
                    TileProgress[tileName].LastCycleCalled = range.End;
                    TileProgress[tileName].LastBaseCall_RAM_GB = maxRAM_GB;

                    // Memorize these parameters so that the next BaseCall could decide if it can re-use the base-clust file
                    TileProgress[tileName].LastCyclePhasingParams = phasingParams;
                    TileProgress[tileName].LastCycleStageWidthParam = stageWidth;
                }
            }
            else
            {
                lock (TileProgress)
                {
                    // Reset these parameters so that the next BaseCall could not re-use the base-clust file
                    TileProgress[tileName].LastCyclePhasingParams = "";
                    TileProgress[tileName].LastCycleStageWidthParam = 0;
                }
            }
        }

        public void Fasta2Bngram(string indexFastaPath)
        {
            DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(indexFastaPath));
            IndexBinPath = Path.Combine(di.FullName, $"index-b{NS}.bin");

            string exePath = Path.Combine(OLABinDir.FullName, "binIndex-static.exe");

            StringBuilder sb = new StringBuilder();
            sb.Append(exePath);
            sb.Append($" {indexFastaPath} {IndexBinPath} {BO} {NS}");

            float ram = 0f;
            if (!ExecuteCommandSync(sb.ToString(), new DirectoryInfo(Path.GetDirectoryName(indexFastaPath)), out ram))
            {
                LogToFile("Failed to generate a binary index file " + IndexBinPath, SeqLogFlagEnum.OLAERROR);
                IndexBinPath = "";
            }
        }

        private void UpdateBaseBngramFile(DirectoryInfo di, BaseCallRangeInfo range)
        {
            string ngramDir = Path.Combine(OLABinDir.FullName, "ngrams", Template.ToString());
            string BB_path = string.IsNullOrEmpty(BB) ? "" : Path.Combine(ngramDir, BB);

            // Optionally input the previous fastq file, either clr or cpf
            string prev_FASTQ_path = "";
            string prev_clr_FASTQ_path = Path.Combine(di.FullName, "calls", "save", "proc-int-clr.fastq");
            string prev_cpf_FASTQ_path = Path.Combine(di.FullName, "calls", "save", "proc-int-cpf.fastq");
            if (UseSlidingWindow && range.Start > 0)
            {
                if (RecipeRunConfig.OLABaseCallOnlyPFClusters)
                    prev_FASTQ_path = prev_cpf_FASTQ_path;
                else
                    prev_FASTQ_path = prev_clr_FASTQ_path;
            }

            DirectoryInfo paramDir = new DirectoryInfo(Path.Combine(di.FullName, "params"));

            string bngramPath = String.IsNullOrEmpty(IndexBinPath) ? BB_path : IndexBinPath;

            if (!string.IsNullOrEmpty(bngramPath) && !string.IsNullOrEmpty(prev_FASTQ_path))
                WriteParams(paramDir, "base-bngram", bngramPath + Environment.NewLine + prev_FASTQ_path);
            else if (!string.IsNullOrEmpty(bngramPath))
                WriteParams(paramDir, "base-bngram", bngramPath);
            else if (!string.IsNullOrEmpty(prev_FASTQ_path))
                WriteParams(paramDir, "base-bngram", Environment.NewLine + prev_FASTQ_path);
            else
                WriteParams(paramDir, "base-bngram", "");
        }

        private void UpdateBaseIndexFile(DirectoryInfo di, string tileName, BaseCallRangeInfo range)
        {
            // Currently we support up to two reads: Read1 + Index1, so if MustBuildTemplate is false, it must be an Index1 read.
            // IN == "0" would mean no indexing is required
            if (!MustBuildTemplate && IN != "0")
            {
                string FASTQ_to_be_indexed;
                
                if (RecipeRunConfig.OLABaseCallOnlyPFClusters)
                    FASTQ_to_be_indexed = Path.Combine(ImageTemplateBaseDir.FullName, "proc-" + tileName, "calls", "proc-int-cpf.fastq");
                else
                    FASTQ_to_be_indexed = Path.Combine(ImageTemplateBaseDir.FullName, "proc-" + tileName, "calls", "proc-int-clr.fastq");

                DirectoryInfo paramDir = new DirectoryInfo(Path.Combine(di.FullName, "params"));

                if (range.Start == 0)
                    WriteParams(paramDir, "base-index", IN + Environment.NewLine + FASTQ_to_be_indexed);
                else
                    WriteParams(paramDir, "base-index", IN);
            }
        }

        static public string GetParamsName(TemplateOptions template, bool isV2)
        {
            string paramsName = "";
            switch (template)
            {
                case TemplateOptions.ecoli:
                case TemplateOptions.e8739:
                case TemplateOptions.m13:
                case TemplateOptions.scere:
                case TemplateOptions.PhiX:
                case TemplateOptions.Brca:
                    paramsName = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAParams_Main_nonHG;
                    break;
                case TemplateOptions.hg38:
                case TemplateOptions.NIPT:
                    paramsName = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAParams_Main_HG;
                    break;
                case TemplateOptions.idx:
                    paramsName = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAParams_Index;
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            return paramsName;
        }

        FileInfo GetParamFile()
        {
            FileInfo fi = new FileInfo(Path.Combine(WorkingDir.FullName, "run.sh"));
            if (fi.Exists)
                return fi;

            return new FileInfo(Path.Combine(OLABinDir.FullName, "params", GetParamsName(Template, IsV2), Template.ToString(), "run.sh"));
        }

        public void LoadProcessingParameters()
        {
            string baseCallExePath = Path.Combine(OLABinDir.FullName, "BaseCall-static.exe");
            if (File.Exists(baseCallExePath))
            {
                SeqPSoftwareDate = File.GetLastWriteTime(baseCallExePath);
                LogToFile($"seqP software modification date: {SeqPSoftwareDate}");
            }
            else
            {
                LogToFile("File not found: " + baseCallExePath, SeqLogFlagEnum.OLAERROR);
                return;
            }

            FileInfo paramFile = GetParamFile();
            Log("Using parameter file: " + paramFile.FullName);

            if (!paramFile.Exists)
            {
                LogToFile("File not found: " + paramFile.FullName, SeqLogFlagEnum.OLAERROR);
                return;
            }

            using (StreamReader sr = new StreamReader(paramFile.FullName))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (line.Contains("export RN="))
                        _RN = line.Substring(line.IndexOf("=") + 1);
                    //else if (line.Contains("export NS="))
                    //    _NS = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1));
                    else if (line.Contains("export BO="))
                        _BO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CE="))
                        _CE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TP="))
                        _TP = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export NC="))
                        _NC = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export CD="))
                        _CD = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export PP="))
                    {
                        string input = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                        StringHelper.StringToInitializedArray<float>(input, PP);
                    }
                    else if (line.Contains("export CN="))
                        _CN = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export FT="))
                        _FT = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export FE="))
                        _FE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export EO="))
                        _EO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TO="))
                        _TO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export ST="))
                        _ST = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TT="))
                        _TT = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export SE="))
                        _SE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TE="))
                        _TE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CO="))
                        _CO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export JO="))
                        _JO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export RI="))
                        _RI = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export EP="))
                        _EP = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export BC="))
                        _BC = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export BW="))
                    {
                        _BW = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                        _BW = _BW.Replace("\\n", Environment.NewLine);
                    }
                    else if (line.Contains("export BE="))
                        _BE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export BD="))
                        _BD = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export BT="))
                        _BT = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export B4="))
                        _B4 = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export B5="))
                        _B5 = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export BB="))
                        _BB = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export YG="))
                        _YG = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export QM="))
                        _QM = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CQ="))
                        _CQ = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export IN="))
                        _IN = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export EDS="))
                        _EDS = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export EDM="))
                        _EDM = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                }
            }

            // Determine the minimum number of cycles required to build a template
            if (MustBuildTemplate)
            {
                Debug.Assert(String.Compare(ImageTemplateBaseDir.FullName, WorkingDir.FullName) == 0);

                int bt_image_count;
                int bt_image_start;
                int bt_image_step;
                // Generally, we need 9 cycles to build templates.
                // Alternatively the config may enforce a certain minimum number of cycles for building templates.
                // For example, when testing the software, the total number of cycles may be less than 9, in which case the config should give a valid minimum number of cycles.
                if (RecipeRunConfig.OLAMinimumCyclesToCreateTemplates > 0) // explicit minimum number of cycles for building templates 
                {
                    bt_image_count = RecipeRunConfig.OLAMinimumCyclesToCreateTemplates * 4;
                    bt_image_start = 0;
                    bt_image_step = 1;
                }
                else if (NS < 9) // 9 cycles is the minimum number of cycles required by -T 24 12 1"
                {
                    bt_image_count = NS * 4;
                    bt_image_start = 0;
                    bt_image_step = 1;
                }
                //else // hard-wire BuildTmplt -T parameter to "24 12 1"
                //{
                //    bt_image_count = 24;
                //    bt_image_start = 12;
                //    bt_image_step = 1;
                //}
                else // hard-wire BuildTmplt -T parameter to "36 0 1"
                {
                    bt_image_count = 36;
                    bt_image_start = 0;
                    bt_image_step = 1;
                }
                _TP = $"{bt_image_count} {bt_image_start} {bt_image_step}";
                MinTemplateCycle = (bt_image_start + bt_image_count * bt_image_step) / 4;
            }
            else
            {
                MinTemplateCycle = 0; // we will use a pre-built template
            }

            LogToFile($"Minimum number of cycles to build template: {MinTemplateCycle}");

            // Determine the minimum number of cycles required to call BaseCall
            if (RecipeRunConfig.OLAMinimumCyclesToCallBases > 0) // A config option to enforce a certain minimum cycle for calling BaseCall
            {
                // Cannot call BaseCall before building a template  
                BaseCallMinCycle = Math.Max(MinTemplateCycle, RecipeRunConfig.OLAMinimumCyclesToCallBases);
            }
            else // by default base calling starts after cycle 25 
                BaseCallMinCycle = 25;

            // Make sure the minimum number of cycles for base calling doesn't exceed the run length
            BaseCallMinCycle = Math.Min(BaseCallMinCycle, NS);

            LogToFile($"Minimum number of cycles to call bases: {BaseCallMinCycle}");

            // Set the frequency of calling BaseCall
            BaseCallEveryNthCycle = RecipeRunConfig.OLABaseCallEveryNthCycle;

            // Prepend -QM with path
            string qmapDir = Path.Combine(OLABinDir.FullName, "qmaps", Template.ToString());
            QM = string.IsNullOrEmpty(QM) ? "" : Path.Combine(qmapDir, QM);
        }

        private void JoinCyclesByCell(string tileName, DirectoryInfo di, int cycle)
        {
            bool useNewMethod = true;
            bool outputCSV = false;

            DirectoryInfo extr = new DirectoryInfo(Path.Combine(di.FullName, "extr"));

            if (useNewMethod)
            {
                IntensityBLI curBLI = new IntensityBLI(Path.Combine(di.FullName, "calls", "proc-int"), outputCSV);
                AffineFile curAF = new AffineFile(Path.Combine(di.FullName, "at", "proc-at"), outputCSV);

                int start = (int)curBLI.Fov;  // number of images already in proc-int file, which is the same as the index of the first image we should add to proc-int file

                if (curBLI.Fov == 0) // if proc-int.bli doesn't exist, or has no FOVs, start building it by loading the proc-int from initial image
                {
                    string initFileName = Path.Combine(di.FullName, "extr", "proc_000000-int");
                    if (!File.Exists(initFileName + ".bli")) // must find initial file
                    {
                        LogToFile("JoinCycles failed: proc_000000-int.bli doesn't exist.", SeqLogFlagEnum.OLAERROR);
                        return;
                    }

                    curBLI = new IntensityBLI(initFileName, outputCSV);
                    if (curBLI.NumClusters < 1) // must have some extracted intensities
                    {
                        LogToFile("JoinCycles failed: proc_000000-int.bli doesn't have any clusters.", SeqLogFlagEnum.OLAERROR);
                        return;
                    }

                    curBLI.WriteFile(Path.Combine(di.FullName, "calls", "proc-int"));
                    start = 1; // already loaded proc_000000-int, so the next one will be proc_000001-int
                }

                if (curAF.N == 0) // if proc-at file doesn't exist, or has no FOVs, start building it by loading the proc-at from initial image
                {
                    curAF = new AffineFile(Path.Combine(di.FullName, "extr", "proc_000000-at"), outputCSV);
                    curAF.WriteFile(Path.Combine(di.FullName, "at", "proc-at"));
                }

                int stop = extr.GetFiles("proc_*-int.bli").Length;
                for (int i = start; i < stop; i++)
                {
                    if (IsAbort)
                        break;

                    string flname = Path.Combine(di.FullName, "extr", String.Format("proc_{0:D6}-int", i));
                    if (!File.Exists(flname + ".bli"))
                    {
                        LogToFile($"JoinCycles failed: {flname}.bli doesn't exist.", SeqLogFlagEnum.OLAERROR);
                        return;
                    }
                    IntensityBLI fl1 = new IntensityBLI(flname);
                    curBLI.AddFOV(fl1);

                    string atflname = Path.Combine(di.FullName, "extr", String.Format("proc_{0:D6}-at", i));
                    if (!File.Exists(atflname + ".bla"))
                    {
                        LogToFile($"JoinCycles failed: {atflname}.bla doesn't exist.", SeqLogFlagEnum.OLAERROR);
                        return;
                    }
                    AffineFile aff = new AffineFile(atflname);
                    curAF.add(aff);
                }
            }
            else
            {
                string exePath = Path.Combine(OLABinDir.FullName, "JoinCycles-static.exe");
                StringBuilder sb = new StringBuilder();
                sb.Append(exePath);
                sb.Append(" " + JO);
                sb.Append(" -H \"" + NC + "\"");
                sb.Append(" -b \"" + BO + "\"");
                if (RecipeRunConfig.OLACatchHardwareExceptionsInCpp)
                    sb.Append(" -y 1");
                // This has to be the last entry on the command line
                sb.Append(" proc.txt");

                bool ok = false;
                float maxRAM_GB = 0f;
                ok = ExecuteCommandSync(sb.ToString(), extr, out maxRAM_GB);

                if (ok)
                    LogToFile("JoinCycles succeeded.", SeqLogFlagEnum.DEBUG);
                else
                    LogToFile("JoinCycles failed.", SeqLogFlagEnum.OLAERROR);

                if (!ok || IsAbort)
                    return;

                if (extr.GetFiles("proc-int.*").Length < 2)
                    ok = false;
                else if (extr.GetFiles("proc-at.*").Length < 2)
                    ok = false;

                if (!ok)
                {
                    LogToFile("JoinCycles failed to create target file(s)", SeqLogFlagEnum.OLAERROR);
                    return;
                }

                if (1 > FileManipulator.MoveFile("proc-int.*", "../calls", extr))
                    ok = false;

                if (1 > FileManipulator.MoveFile("proc-at.*", "../at", extr))
                    ok = false;

                if (!ok)
                {
                    LogToFile("JoinCycles failed to move target file(s)", SeqLogFlagEnum.OLAERROR);
                    return;
                }
            }

            TileProgress[tileName].LastCycleJoined = cycle;

            // for short time RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".baseCall");
        }

        public void WriteToParams(DirectoryInfo target = null)
        {
            if (target != null)
                Log("Write Param " + target.FullName, SeqLogFlagEnum.DEBUG);
            else
                Log("Write Param all", SeqLogFlagEnum.DEBUG);

            DirectoryInfo di = WorkingDir;
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (target != null)
                {
                    if (sd.Name != target.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {
                    DirectoryInfo paramDir = new DirectoryInfo(Path.Combine(sd.FullName, "params"));
                    if (!paramDir.Exists)
                        paramDir.Create();
                    string contents = sd.Name.Substring(sd.Name.IndexOf('-') + 1);
                    WriteParams(paramDir, "flow-cell", contents);
                    WriteParams(paramDir, "base-order", BO);
                    WriteParams(paramDir, "color-ext", CE);
                    WriteParams(paramDir, "num-stages", NS.ToString());
                    WriteParams(paramDir, "num-colors", NC.ToString());
                    WriteParams(paramDir, "run-name", RN);
                    WriteParams(paramDir, "tmplt-par", TP);
                    WriteParams(paramDir, "filter-tmplt", FT);
                    WriteParams(paramDir, "filter-extr", FE);
                    WriteParams(paramDir, "shift-tmplt", ST);
                    WriteParams(paramDir, "shift-extr", SE);
                    WriteParams(paramDir, "tile-tmplt", TT);
                    WriteParams(paramDir, "tile-extr", TE);
                    WriteParams(paramDir, "extr-par", EP);
                    WriteParams(paramDir, "extr-opts", EO);
                    WriteParams(paramDir, "rm-int", RI.ToString());
                    WriteParams(paramDir, "tmplt-opts", TO);
                    WriteParams(paramDir, "jc-opts", JO);
                    WriteParams(paramDir, "cluster-dim", CD.ToString());
                    WriteParams(paramDir, "phasing-par", String.Join(" ", PP));
                    WriteParams(paramDir, "cluster-norm", CN);
                    WriteParams(paramDir, "call-opts", CO);
                    WriteParams(paramDir, "base-call", BC.ToString());
                    WriteParams(paramDir, "call-qc", CQ);
                    WriteParams(paramDir, "base-index", IN);

                    // base-weight
                    WriteParams(paramDir, "base-weight", BW);

                    // base-ngram
                    string ngramDir = Path.Combine(OLABinDir.FullName, "ngrams", Template.ToString());

                    string BE_path = string.IsNullOrEmpty(BE) ? "" : Path.Combine(ngramDir, BE);
                    string BD_path = string.IsNullOrEmpty(BD) ? "" : Path.Combine(ngramDir, BD);
                    string BT_path = string.IsNullOrEmpty(BT) ? "" : Path.Combine(ngramDir, BT);
                    string B4_path = string.IsNullOrEmpty(B4) ? "" : Path.Combine(ngramDir, B4);
                    string B5_path = string.IsNullOrEmpty(B5) ? "" : Path.Combine(ngramDir, B5);

                    string temp = "";
                    if (!string.IsNullOrEmpty(BE_path))
                        temp += (BE_path + "\n");
                    if (!string.IsNullOrEmpty(BD_path))
                        temp += (BD_path + "\n");
                    if (!string.IsNullOrEmpty(BT_path))
                        temp += (BT_path + "\n");
                    if (!string.IsNullOrEmpty(B4_path))
                        temp += (B4_path + "\n");
                    if (!string.IsNullOrEmpty(B5_path))
                        temp += (B5_path + "\n");
                    WriteParams(paramDir, "base-ngram", temp);

                    // base-bngram
                    string BB_path = string.IsNullOrEmpty(BB) ? "" : Path.Combine(ngramDir, BB);
                    temp = "";
                    if (!string.IsNullOrEmpty(BB_path))
                        temp = BB_path;
                    WriteParams(paramDir, "base-bngram", temp);

                    // base-yngram
                    //export YG="YGPATH\\all_0\nYGPATH\\all_1\nYGPATH\\all_2\nYGPATH\\all_3\nYGPATH\\all_4"
                    temp = "";
                    if (!string.IsNullOrEmpty(YG))
                    {
                        string ygPath = Path.Combine(OLABinDir.FullName, "yngrams", Template.ToString());
                        temp = YG.Replace("YGPATH", ygPath);
                        temp = temp.Replace(@"\n", Environment.NewLine);
                    }
                    WriteParams(paramDir, "base-yngram", temp);

                    // q-map
                    WriteParams(paramDir, "qscore-map", QM);
                }
            }
        }

        private void ExtractIntensitiesByCell(string tileName, DirectoryInfo di, int cycleStart, int cycleStop, int parallelImageCount, bool usingMultipleExtractIntExes)
        {
            int start = (cycleStart - 1) * 4;
            int stop = cycleStop * 4;

            bool ok;

            if (usingMultipleExtractIntExes && RecipeRunConfig.UsingTPLForExtractIntensitiesByCell && parallelImageCount > 1)
            {
                CreateProcTxt(1, cycleStop, di);

                var Options = new ParallelOptions();
                Options.MaxDegreeOfParallelism = parallelImageCount;

                int imagesPerTask = (int)Math.Ceiling((stop - start) / (float)parallelImageCount);

                Parallel.ForEach(Partitioner.Create(start, stop, imagesPerTask), Options, range =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        if (IsAbort)
                            break;

                        DirectoryNumber dn = new DirectoryNumber(tileName, di, i);
                        ExtractSingleIntensity(dn, tileName);
                    }
                });

                ok = ManageFilesAfterExtractIntensities(di, cycleStart, cycleStop);
            }
            else if (!usingMultipleExtractIntExes)
            {
                DirectoryNumber dn = new DirectoryNumber(tileName, di);
                CreateProcTxt(cycleStart, cycleStop, di);
                ok = ExtractMultipleImages(dn, tileName, new Tuple<int, int>((cycleStart - 1) * 4, cycleStop * 4), parallelImageCount);
                if (ok)
                    ok = ManageFilesAfterExtractIntensitiesEx(di, cycleStart, cycleStop);
            }
            else // not using TPL; serial processing
            {
                CreateProcTxt(1, cycleStop, di);

                int counts = stop - start;
                Thread[] newThread = new Thread[counts];
                for (int num = start; num < stop; num++)
                {
                    if (IsAbort)
                        break;

                    DirectoryNumber dn = new DirectoryNumber(tileName, di, num);
                    newThread[num - start] = new Thread(() => ExtractSingleIntensity(dn, tileName));
                    newThread[num - start].Name = di.Name + ":" + num.ToString();
                    newThread[num - start].Start();// dn);
                    newThread[num - start].Join();

                    Thread.Sleep(100); // start process
                }

                for (int num = 0; num < counts; num++)
                {
                    if (newThread[num] != null)
                    {
                        newThread[num].Join();
                        newThread[num] = null; // no longer needed, mark as unused
                    }
                }

                ok = ManageFilesAfterExtractIntensities(di, cycleStart, cycleStop);
            }

            // Check if all per-image target files are there
            if (ok)
            {
                if (!usingMultipleExtractIntExes)
                {
                    if (!RecipeRunConfig.OLASingleExtractMultipleImagesByCellWithJC)
                    {
                        for (int ii = start; ii < stop; ii++)
                        {
                            string targetFile = Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-int.bli", ii));
                            if (!File.Exists(targetFile))
                            {
                                LogToFile("ExtractInt-static.exe target file \"" + targetFile + "\" not found.", SeqLogFlagEnum.OLAERROR);
                                ok = false;
                            }

                            string targetFile_bla = Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-at.bla", ii));
                            if (!File.Exists(targetFile_bla))
                            {
                                LogToFile("ExtractInt-static.exe target file \"" + targetFile_bla + "\" not found.", SeqLogFlagEnum.OLAERROR);
                                ok = false;
                            }
                        }
                    }
                    else
                    {
                        // Check the combined bli and bla files
                    }

                    if (ok && !IsAbort)
                    {
                        lock (TileProgress)
                        {
                            for (int ii = start; ii < stop; ii++)
                                TileProgress[tileName].ImageExtracted[ii] = true;
                        }
                    }
                }

                // Tally up all extracted images information and update the progresss
                Dictionary<int, bool> imagesExtracted = TileProgress[tileName].ImageExtracted;
                int lastCycleExtracted = start / 4;
                for (int i = start; i < stop; i++)
                {
                    if (!imagesExtracted.ContainsKey(i) || imagesExtracted[i] != true)
                        break;
                    // Every 4 images extracted - a cycle is extracted
                    if ((i - start + 1) % 4 == 0)
                        lastCycleExtracted++;
                }

                TileProgress[tileName].LastCycleExtracted = lastCycleExtracted;
                TileProgress[tileName].LastExtractInt_Concurrency = parallelImageCount;
            }
        }

        private void ManageIndexedFilesAfterExtractIntensities(DirectoryInfo di, int offset, string suffix, string dest, string subdir="")
        {
            string destDir;
            if (string.IsNullOrEmpty(subdir))
            {
                destDir = Path.Combine(di.FullName, dest);
            }
            else
            {
                destDir = Path.Combine(di.FullName, dest, subdir);
                Directory.CreateDirectory(destDir);
            }

            foreach (string filePath in Directory.EnumerateFiles(di.FullName, suffix, SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                string pattern = @"\d{6}";
                Match match = Regex.Match(fileName, pattern);
                if (match.Success)
                {
                    string fileNewName = fileName;
                    if (offset > 0)
                    {
                        string indexString = match.ToString();
                        int indexWithOffset = int.Parse(indexString) + offset;
                        string indexWithOffsetString = string.Format("{0:D6}", indexWithOffset);
                        fileNewName = fileName.Replace(indexString, indexWithOffsetString);
                    }

                    string destPath = Path.Combine(destDir, fileNewName);
                    if (File.Exists(destPath))
                        File.Delete(destPath);
                    File.Move(Path.Combine(di.FullName, fileName), destPath);
                }
            }
        }

        private bool ManageFilesAfterExtractIntensitiesEx(DirectoryInfo di, int cycleStart, int cycleStop)
        {
            bool ok = true;
            try
            {
                // Move (after merging, if necessary) proc.txt file to the extr and calls directories  
                string extrProcPath = Path.Combine(di.FullName, "extr", "proc.txt");
                string callsProcPath = Path.Combine(di.FullName, "calls", "proc.txt");
                string thisProcPath = Path.Combine(di.FullName, "proc.txt");

                List<string> previousProcTxt = new List<string>();
                if (File.Exists(extrProcPath))
                    previousProcTxt = File.ReadAllLines(extrProcPath).ToList();
                List<string> currentProcTxt = File.ReadAllLines(thisProcPath).ToList();
                List<string> combinedList = previousProcTxt.Union(currentProcTxt).ToList(); // Union guarantees the combined list (a) does not have duplicates (b) is sorted as original lists

                File.WriteAllLines(extrProcPath, combinedList);
                File.Copy(extrProcPath, thisProcPath, true /*overwrite*/); // this may be needed for BaseCall
                File.Copy(extrProcPath, callsProcPath, true /*overwrite*/); // this may be needed for BaseCall

                // Build a unique subdirectory name for the log file(s)
                string logSubDir_0 = $"extr_{cycleStart}_{cycleStop}";
                string logSubDir = logSubDir_0;
                int index = 1;
                while (Directory.Exists(Path.Combine(di.FullName, "logs", logSubDir)))
                {
                    logSubDir = logSubDir_0 + $" ({index})";
                    index++;
                }
                Directory.CreateDirectory(Path.Combine(di.FullName, "logs", logSubDir));

                // When ExtractInt is run on multiple images _and_ with -n 1, it creates (1) a single multi-image log file and (2) multiple per-image log files.
                // All per-image log files will be moved to the log directory by ManageIndexedFilesAfterExtractIntensities below. But the single multi-image
                // log file has to be moved separately. Here we give it a special name, which indicates its range of cycles, and move it to the target log subdirectory.
                string multiImageLog = Path.Combine(di.FullName, "proc.log");
                if (File.Exists(multiImageLog))
                {
                    string start_index = string.Format("{0:D6}", NC * (cycleStart - 1));
                    string stop_index = string.Format("{0:D6}", NC * cycleStop - 1);
                    string destPath = Path.Combine(di.FullName, "logs", logSubDir, "proc_" + start_index + "-" + stop_index + ".log");
                    if (File.Exists(destPath))
                        File.Delete(destPath);
                    File.Move(multiImageLog, destPath);
                }

                // For all indexed-name files, offset their indexed names and move them to the respective directories
                int offset = (cycleStart - 1) * NC;

                ManageIndexedFilesAfterExtractIntensities(di, offset, "*.log", "logs", logSubDir);

                ManageIndexedFilesAfterExtractIntensities(di, offset, "*-int.*", "extr");
                ManageIndexedFilesAfterExtractIntensities(di, offset, "*-at.*", "extr");
                ManageIndexedFilesAfterExtractIntensities(di, offset, "*-loc.*", "locs");
                ManageIndexedFilesAfterExtractIntensities(di, offset, "*-qc.*", "qc");

                if (RecipeRunConfig.OLASingleExtractMultipleImagesByCellWithJC)
                {
                    // Merge *-int.bli, *-int.csv, *-at.bla, *-at.csv files to calls dir.
                    //bool outputCSV = (cycle == NS); // joined files in CSV format are needed only for convenience of the downstream analysis and only after the last cycle 
                    bool outputCSV = true; // TODO: output csv after the last cycle only; currently it's not possible in the sliding window mode, because of the CSV file header 
                    string jcBLIPath = Path.Combine(di.FullName, "calls", "proc-int.bli");
                    string thisBLIPath = Path.Combine(di.FullName, "proc-int.bli");
                    if (cycleStart == 1)
                    {
                        File.Move(thisBLIPath, jcBLIPath);
                        if (outputCSV)
                        {
                            IntensityBLI jcBLI = new IntensityBLI(Path.Combine(di.FullName, "calls", "proc-int"), outputCSV);
                            jcBLI.WriteCSVFile();
                        }
                    }
                    else
                    {
                        IntensityBLI jcBLI = new IntensityBLI(Path.Combine(di.FullName, "calls", "proc-int"), outputCSV);
                        IntensityBLI thisBLI = new IntensityBLI(Path.Combine(di.FullName, "proc-int"), outputCSV);
                        jcBLI.AppendBLI(thisBLI);
                        File.Delete(thisBLIPath);
                    }

                    string jcBLAPath = Path.Combine(di.FullName, "at", "proc-at.bla");
                    string thisBLAPath = Path.Combine(di.FullName, "proc-at.bla");
                    if (cycleStart == 1)
                    {
                        File.Move(thisBLAPath, jcBLAPath);
                        if (outputCSV)
                        {
                            AffineFile jcBLA = new AffineFile(Path.Combine(di.FullName, "at", "proc-at"), outputCSV);
                            jcBLA.WriteCSVFile();
                        }
                    }
                    else
                    {
                        AffineFile jcBLA = new AffineFile(Path.Combine(di.FullName, "at", "proc-at"), outputCSV);
                        AffineFile thisBLA = new AffineFile(Path.Combine(di.FullName, "proc-at"), outputCSV);
                        jcBLA.AppendBLA(thisBLA);
                        File.Delete(thisBLAPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile("Exception in ManageFilesAfterExtractIntensitiesEx: " + ex.ToString(), SeqLogFlagEnum.OLAERROR);
                ok = false;
            }

            return ok;
        }

        private bool ManageFilesAfterExtractIntensities(DirectoryInfo sd, int cycleStart, int cycleStop)
        {
            bool ok = true;
            try
            {
                // Build a unique subdirectory name for the log file(s)
                string logSubDir_0 = $"extr_{cycleStart}_{cycleStop}";
                string logSubDir = logSubDir_0;
                int index = 1;
                while (Directory.Exists(Path.Combine(sd.FullName, "logs", logSubDir)))
                {
                    logSubDir = logSubDir_0 + $" ({index})";
                    index++;
                }

                // Create the log directory if it does not exist and move the log file(s) there
                string logDirPath = Path.Combine(sd.FullName, "logs", logSubDir);
                Directory.CreateDirectory(logDirPath);
                FileManipulator.MoveFile("*.log", logDirPath, sd);

                FileManipulator.MoveFile("*-loc.*", "locs", sd);
                FileManipulator.MoveFile("*-int.*", "extr", sd);
                FileManipulator.MoveFile("*-at.*", "extr", sd);
                FileManipulator.MoveFile("proc.txt", "extr", sd, false);
                FileManipulator.MoveFile("proc.txt", "calls", sd, false);
                FileManipulator.MoveFile("*-qc.*", "qc", sd);
            }
            catch (Exception ex)
            {
                LogToFile("Exception in ManageFilesAfterExtractIntensities: " + ex.ToString(), SeqLogFlagEnum.OLAERROR);
                ok = false;
            }

            return ok;
        }

        private bool ManageFilesAfterBaseCall(string tileName, DirectoryInfo sd, BaseCallRangeInfo range)
        {
            bool ok = true;

            if (range.Start > 0)
            {
                Debug.Assert(UseSlidingWindow);

                if (!MergeBCQCFiles(sd, TileProgress[tileName].LastCycleCalled, range))
                    ok = false;
            }

            FileInfo log_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int.log"));

            try
            {
                if (range.End == NS)
                {
                    FileInfo fastq_cpf_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-cpf.fastq"));
                    if (!fastq_cpf_File.Exists)
                    {
                        LogToFile($"Could not find file {fastq_cpf_File.FullName}", SeqLogFlagEnum.OLAERROR);
                        ok = false;
                    }

                    if (!RecipeRunConfig.OLABaseCallOnlyPFClusters)
                    {
                        FileInfo fastq_clr_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-clr.fastq"));
                        if (!fastq_clr_File.Exists)
                        {
                            LogToFile($"Could not find file {fastq_clr_File.FullName}", SeqLogFlagEnum.OLAERROR);
                            ok = false;
                        }
                    }
                    
                    // Move "at" files from "extr" to "at" directory
                    DirectoryInfo di = new DirectoryInfo(Path.Combine(sd.FullName, "extr"));
                    foreach (FileInfo fi in di.GetFiles("*-at.csv"))
                    {
                        fi.MoveTo(Path.Combine(sd.FullName, "at", fi.Name));
                    }

                    if (RecipeRunConfig.OLAMinimumOutput)
                    {
                        // Delete "locs" and "extr" directories, unless we use JoinCycles, in which case those directories are deleted after JoinCycles. 
                        if (!RecipeRunConfig.OLAUseJoinCycles)
                        {
                            DirectoryInfo locsDir = new DirectoryInfo(Path.Combine(sd.FullName, "locs"));
                            Directory.Delete(locsDir.FullName, true);

                            DirectoryInfo extrDir = new DirectoryInfo(Path.Combine(sd.FullName, "extr"));
                            Directory.Delete(extrDir.FullName, true);
                        }

                        // Delete save directory
                        string savePath = Path.Combine(sd.FullName, "calls", "save");
                        if (Directory.Exists(savePath))
                            Directory.Delete(savePath, true);

                        string cscPath = Path.Combine(sd.FullName, "calls", "proc-int-csc.csv");
                        if (File.Exists(cscPath))
                            File.Delete(cscPath);
                    }
                }
                // If using sliding window, save the intermediate log file. Also stash the fastq and bcqc files to be used by the next BaseCall
                else if (UseSlidingWindow)
                {
                    // save the log file
                    if (log_File.Exists)
                    {
                        string logPath = Path.Combine(sd.FullName, "logs");
                        log_File.CopyTo(Path.Combine(logPath, $"proc-int_{range.End}.log"), true);
                    }
                    else
                    {
                        LogToFile($"Could not find file {log_File.FullName}", SeqLogFlagEnum.OLAWARNING);
                        ok = false;
                    }

                    // stash clr file
                    if (!RecipeRunConfig.OLABaseCallOnlyPFClusters)
                    {
                        FileInfo fastq_clr_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-clr.fastq"));
                        if (fastq_clr_File.Exists)
                        {
                            string savePath = Path.Combine(sd.FullName, "calls", "save");
                            CreateDirectory(savePath);
                            fastq_clr_File.CopyTo(Path.Combine(savePath, "proc-int-clr.fastq"), true);
                        }
                        else
                        {
                            LogToFile($"Could not find file {fastq_clr_File.FullName}", SeqLogFlagEnum.OLAERROR);
                            ok = false;
                        }
                    }

                    // stash cpf file
                    FileInfo fastq_cpf_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-cpf.fastq"));
                    if (fastq_cpf_File.Exists)
                    {
                        string savePath = Path.Combine(sd.FullName, "calls", "save");
                        CreateDirectory(savePath);
                        fastq_cpf_File.CopyTo(Path.Combine(savePath, "proc-int-cpf.fastq"), true);
                    }
                    else
                    {
                        LogToFile($"Could not find file {fastq_cpf_File.FullName}", SeqLogFlagEnum.OLAWARNING);
                        ok = false;
                    }

                    // stash bcqc file
                    FileInfo bcqc_File = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-bcqc.csv"));
                    if (bcqc_File.Exists)
                    {
                        string savePath = Path.Combine(sd.FullName, "calls", "save");
                        CreateDirectory(savePath);
                        bcqc_File.CopyTo(Path.Combine(savePath, "proc-int-bcqc.csv"), true);
                    }
                    else
                    {
                        LogToFile($"Could not find file {bcqc_File.FullName}", SeqLogFlagEnum.OLAWARNING);
                        ok = false;
                    }

                    FileInfo cpf_cpf_File = new FileInfo(Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc-cpf-cpf.blb"));
                    if (cpf_cpf_File.Exists)
                    {
                        cpf_cpf_File.CopyTo(Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName,tileName), "proc-loc-cpf.blb"), true);
                    }
                }

                // Move the bcqc file to a subdirectory under the experiment-level bcqc subdirectory, where it will be picked up by graphing
                string bcqcTilePath = Path.Combine(QualityDir.FullName, tileName);
                CreateDirectory(bcqcTilePath);
                FileInfo bcqcFile = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-bcqc.csv"));

                if (range.End == NS)
                    bcqcFile.CopyTo(Path.Combine(bcqcTilePath, $"1_{range.End}_proc-int-bcqc.csv"));
                else
                    bcqcFile.MoveTo(Path.Combine(bcqcTilePath, $"1_{range.End}_proc-int-bcqc.csv"));

                // Move the log file to the logs directory
                // TODO: handle a case with logging disabled
                DirectoryInfo calls_Dir = new DirectoryInfo(Path.Combine(sd.FullName, "calls"));
                if (1 > FileManipulator.MoveFile("proc-int.log", "../logs", calls_Dir))
                    ok = false;
            }
            catch (Exception ex)
            {
                LogToFile("Exception in ManageFilesAfterBaseCall: " + ex.ToString(), SeqLogFlagEnum.OLAERROR);
                ok = false;
            }

            return ok;
        }

        // Obsolete function: the fastq files are now combined in the BaseCall exe itself
        private bool StitchFastqFiles(DirectoryInfo sd, string fileType, int lastCycleCalled)
        {
            bool ok = true;

            // Read current file
            FileInfo curFile = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!curFile.Exists)
            {
                LogToFile($"Could not find file {curFile.FullName}", SeqLogFlagEnum.OLAERROR);
                return false;
            }
            List<string> curText = new List<string>();
            using (StreamReader sr = new StreamReader(curFile.FullName))
            {
                while (sr.Peek() >= 0)
                    curText.Add(sr.ReadLine());
            }

            // Read previous file
            FileInfo prevFile = new FileInfo(Path.Combine(sd.FullName, "calls", "save", "proc-int-" + fileType + ".fastq"));
            if (!prevFile.Exists)
            {
                LogToFile($"Could not find file {curFile.FullName}", SeqLogFlagEnum.OLAERROR);
                return false;
            }
            List<string> prevText = new List<string>();
            using (StreamReader sr = new StreamReader(prevFile.FullName))
            {
                while (sr.Peek() >= 0)
                    prevText.Add(sr.ReadLine());
            }

            // Current and previous files should be based on the same number of clusters
            //Debug.Assert(curText.Count == prevText.Count);
            if (curText.Count != prevText.Count)
            {
                LogToFile($"Files \"{prevFile.FullName}\" and \"{curFile.FullName}\" have different number of lines ({prevText.Count} vs {curText.Count})", SeqLogFlagEnum.OLAERROR);
                return false;
            }

            int replaceCycles = lastCycleCalled;

            for (int i = 1; i < curText.Count; i += 2)
                curText[i] = prevText[i].Substring(0, replaceCycles) + curText[i].Substring(replaceCycles);

            curFile.Delete();
            FileHelper.WriteAllLinesWithSeparator(curFile.FullName, curText, "\n");

            return ok;
        }

        // Merge intensity fields of the previous and current sliding window ranges
        private bool MergeBCQCFiles(DirectoryInfo sd, int prevCycleCalled, BaseCallRangeInfo curCycleRange)
        {
            // Per Vitaly, loading all available cycles guarantees that the bcqc statistics will be calculated for every cycle,
            // not just for the current cycle range. Therefore, no merging with the previous bcqc file is required.
            // However, our testing shows that BaseCall.exe may calculat Base% incorrectly, so it is safer to do the merging with our own code 
            //if (curCycleRange.SparseCycleMappingFlag == 0) // 0 means: all available cycles are loaded
            //    return true;

            bool ok = true;

            // Read the current file
            FileInfo curFile = new FileInfo(Path.Combine(sd.FullName, "calls", "proc-int-bcqc.csv"));
            if (!curFile.Exists)
            {
                LogToFile($"Could not find file {curFile.FullName}", SeqLogFlagEnum.OLAERROR);
                return false;
            }
            List<string> curText = new List<string>();
            using (StreamReader sr = new StreamReader(curFile.FullName))
            {
                while (sr.Peek() >= 0)
                    curText.Add(sr.ReadLine());
            }

            // Read the previous file
            FileInfo prevFile = new FileInfo(Path.Combine(sd.FullName, "calls", "save", "proc-int-bcqc.csv"));
            if (!prevFile.Exists)
            {
                LogToFile($"Could not find file {curFile.FullName}", SeqLogFlagEnum.OLAERROR);
                return false;
            }
            List<string> prevText = new List<string>();
            using (StreamReader sr = new StreamReader(prevFile.FullName))
            {
                while (sr.Peek() >= 0)
                    prevText.Add(sr.ReadLine());
            }

            // Overwrite the current file with the merged file
            using (StreamWriter sr = new StreamWriter(curFile.FullName, false))
            {
                string[] colNames = curText[0].Split(',');

                sr.WriteLine(curText[0]); // write header
                // TODO: the summary row has to be recalculated based on the merged results
                sr.WriteLine(curText[1]); // write summary row

                // Copy all previous, including the overlap, cycles from the previous file to the merged file
                for (int i = 2; i < prevCycleCalled + 2; ++i) // +2 to account for the header and summary rows
                {
                    string[] prevValues = prevText[i].Split(',');
                    string[] curValues = curText[i].Split(',');

                    Debug.Assert(prevValues.Length == curValues.Length);

                    string mergedValues = "";
                    for (int j = 0; j < prevValues.Length; ++j)
                    {
                        if (colNames[j].Contains("Intensity") || colNames[j].Contains("Base"))
                            mergedValues += prevValues[j];
                        else
                            mergedValues += curValues[j];

                        if (j < prevValues.Length - 1)
                            mergedValues += ',';
                    }

                    sr.WriteLine(mergedValues);
                }

                // Copy all current, excluding the overlap, cycles from the current file to the merged file
                for (int i = prevCycleCalled + 2; i < curCycleRange.End + 2; ++i) // +2 to account for the header and summary rows
                    sr.WriteLine(curText[i]);
            }

            return ok;
        }

        // Given two CSV strings, average the corresponding values and return an averaged CSV string
        private string AverageCSV(string csvString1, string csvString2)
        {
            string csvStringAverage = "";

            string[] csv1 = csvString1.Split(',');
            string[] csv2 = csvString2.Split(',');

            Debug.Assert(csv1.Length == csv2.Length);

            double val1 = 0.0;
            double val2 = 0.0;
            for (int j = 0; j < csv1.Length; j++)
            {
                bool ok_1 = double.TryParse(csv1[j], out val1);
                bool ok_2 = double.TryParse(csv2[j], out val2);

                if (ok_1 && ok_2)
                {
                    double average = (val1 + val2) / 2;
                    csvStringAverage += average.ToString();
                }
                else // For example, a string value could be "inf"
                    csvStringAverage += csv1[j];

                if (j != csv1.Length - 1)
                    csvStringAverage += ',';
            }

            return csvStringAverage;
        }

        private bool ExtractMultipleImages(DirectoryNumber dn, string tileName, Tuple<int, int> imageRange, int parallelImageCount) // imageRange is 0-based
        {
            LogToFile($"ExtractMultipleImages starts in {dn.BaseDirectory} for 0-based image range: {imageRange.Item1}-{imageRange.Item2}", SeqLogFlagEnum.DEBUG);

            int totalImages = imageRange.Item2 - imageRange.Item1;

            string exePath = Path.Combine(OLABinDir.FullName, "ExtractInt-static.exe");

            StringBuilder sb = new StringBuilder();
            sb.Append(exePath);

            // safety check
            parallelImageCount = Math.Min(totalImages, parallelImageCount);
            string newEO = EO.Replace(" -n 1 ", $" -n {parallelImageCount} ");
            sb.Append(" " + newEO);

            sb.Append(" -e \"" + EP + "\"");
            sb.Append(" -a \"" + SE + "\"");
            sb.Append(" -f \"" + FE + "\"");
            sb.Append(" -G \"" + TE + "\"");
            if (!string.IsNullOrEmpty(EDM))
            {
                string dmPath = Path.Combine(OLABinDir.FullName, "dmaps");
                string temp = EDM.Replace("DMPATH", dmPath);
                sb.Append($" -Y \"{temp}\"");
            }

            string masterBLB = Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc.blb");
            string cpfBLB = Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc-cpf.blb");
            if (UseSlidingWindow && RecipeRunConfig.OLABaseCallOnlyPFClusters && File.Exists(cpfBLB))
                sb.Append(" -L " + cpfBLB);
            else
                sb.Append(" -L " + masterBLB);

            sb.Append(" -R " + Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-tmplt.tif"));
            if (RecipeRunConfig.OLASingleExtractMultipleImagesByCellWithJC)
                sb.Append(" -O 1"); // combine all results into single bla and bli files
            if (RecipeRunConfig.OLACatchHardwareExceptionsInCpp)
                sb.Append(" -y 1");
            // This has to be the last entry on the command line
            sb.Append(" proc.txt");

            bool ok;
            float maxRAM_GB;
            if (RecipeRunConfig.OLAUseDLL)
                ok = ExecuteCommandDLL(enImageprocessingExeType.eExtractInt, sb.ToString(), dn.Tile, dn.BaseDirectory, out maxRAM_GB);
            else
                ok = ExecuteCommandSync(sb.ToString(), dn.BaseDirectory, out maxRAM_GB);

            if (ok)
            {
                TileProgress[tileName].LastExtractInt_RAM_GB = maxRAM_GB;
            }
            else
            {
                TileProgress[tileName].LastExtractInt_RAM_GB = -1.0f;
                SetFailedTile(tileName);
            }

            TileProgress[tileName].LastExtractInt_Concurrency = parallelImageCount;

            if (ok)
                LogToFile($"ExtractMultipleImages ended in: {dn.BaseDirectory} Tile: {dn.Tile} 0-based range: {imageRange.Item1}-{imageRange.Item2}", SeqLogFlagEnum.DEBUG);
            else
                LogToFile($"ExtractMultipleImages failed in: {dn.BaseDirectory} Tile: {dn.Tile} 0-based image range: {imageRange.Item1}-{imageRange.Item2}", SeqLogFlagEnum.OLAERROR);

            return ok;
        }

        // Note, dn.Cycle is used here to store not a cycle number, but a zero-based index of an image of the tile dn.Tile
        private void ExtractSingleIntensity(DirectoryNumber dn, string tileName)
        {
            LogToFile($"Extracting thread starts in {dn.BaseDirectory} for image {dn.Cycle}", SeqLogFlagEnum.DEBUG);

            string exePath = Path.Combine(OLABinDir.FullName, "ExtractInt-static.exe");

            StringBuilder sb = new StringBuilder();
            sb.Append(exePath);
            sb.Append(" " + EO);
            sb.Append(" -e \"" + EP + "\"");
            sb.Append(" -a \"" + SE + "\"");
            sb.Append(" -f \"" + FE + "\"");
            sb.Append(" -G \"" + TE + "\"");
            if (!string.IsNullOrEmpty(EDS))
            {
                string dmPath = Path.Combine(OLABinDir.FullName, "dmaps");
                string temp = EDS.Replace("DMPATH", dmPath);
                sb.Append($" -Y \"{temp}\"");
            }
            sb.Append(" -L " + Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-loc.blb"));
            sb.Append(" -R " + Path.Combine(GetTmpltDirPath(ImageTemplateBaseDir.FullName, tileName), "proc-tmplt.tif"));
            sb.Append(" -N " + dn.Cycle);
            if (RecipeRunConfig.OLACatchHardwareExceptionsInCpp)
                sb.Append(" -y 1");

            // This has to be the last entry on the command line
            sb.Append(" proc.txt");

            bool ok = false;
            float maxRAM_GB = 0f;
            if (RecipeRunConfig.OLAUseDLL)
                ok = ExecuteCommandDLL(enImageprocessingExeType.eExtractInt, sb.ToString(), dn.Tile, dn.BaseDirectory, out maxRAM_GB);
            else
                ok = ExecuteCommandSync(sb.ToString(), dn.BaseDirectory, out maxRAM_GB);

            if (!ok)
            {
                LogToFile($"ExtractInt-static.exe failed: {dn.BaseDirectory} Tile: {dn.Tile} N: {dn.Cycle}", SeqLogFlagEnum.OLAERROR);
            }
            else // ok is true, but still check if the target files are there
            {
                string targetFile = Path.Combine(dn.BaseDirectory.FullName, string.Format("proc_{0:D6}-int.bli", dn.Cycle));
                if (!File.Exists(targetFile))
                {
                    LogToFile("ExtractInt-static.exe target file \"" + targetFile + "\" not found.", SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }

                string targetFile_bla = Path.Combine(dn.BaseDirectory.FullName, string.Format("proc_{0:D6}-at.bla", dn.Cycle));
                if (!File.Exists(targetFile_bla))
                {
                    LogToFile("ExtractInt-static.exe target file \"" + targetFile_bla + "\" not found.", SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }
            }

            // Mark progress
            if (!IsAbort)
            {
                if (ok)
                {
                    lock (TileProgress)
                    {
                        TileProgress[dn.Tile].ImageExtracted[dn.Cycle] = true; // here dn.Cycle is a zero-based index of an image of the tile dn.Tile
                        TileProgress[dn.Tile].LastExtractInt_SingleImage_RAM_GB = Math.Max(TileProgress[dn.Tile].LastExtractInt_SingleImage_RAM_GB, maxRAM_GB);
                        TileProgress[dn.Tile].LastExtractInt_RAM_GB = Math.Max(TileProgress[dn.Tile].LastExtractInt_RAM_GB, maxRAM_GB);
                    }
                }
                else
                {
                    lock (TileProgress)
                    {
                        TileProgress[tileName].LastExtractInt_SingleImage_RAM_GB = -1.0f;
                        TileProgress[tileName].LastExtractInt_RAM_GB = -1.0f;
                        TileProgress[tileName].FailedTile = true;
                    }
                }
            }

            LogToFile($"Extracting thread ends in {dn.BaseDirectory} for image {dn.Cycle}", SeqLogFlagEnum.DEBUG);
        }

        public void CallMatchStats(ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff, int cycle = -1)
        {
            foreach (string tileName in FCList)
            {
                CallMatchStats(tileName, ref perfectMatch, ref OneOff, cycle);
            }
        }
        public void CallMatchStats(string tileName, ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff, int cycle = -1)
        {
            Dictionary<string, string> reference = new Dictionary<string, string>();
            string refFile = Path.Combine(WorkingDir.FullName, "ref.fasta");
            if (!File.Exists(refFile))
                return;
            using (StreamReader refSeq = new StreamReader(refFile))
            {
                while (!refSeq.EndOfStream)
                {
                    string name = refSeq.ReadLine();
                    string value = refSeq.ReadLine();
                    reference[name] = value;
                }

            }

            Dictionary<string, int> perfect = new Dictionary<string, int>();
            Dictionary<string, int> oneOff = new Dictionary<string, int>();
            Dictionary<string, float> DLDist = new Dictionary<string, float>();
            Dictionary<string, float> DLDist3 = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                perfect[key] = 0;
                oneOff[key] = 0;
                DLDist[key] = 0;
                DLDist3[key] = 0;
            }
            int numClusters = 0;

            string curCalls = Path.Combine(WorkingDir.FullName, "proc-" + tileName, "calls", "proc-int-clr.fastq");
            if (!File.Exists(curCalls))
                return;

            int curCycle = cycle;
            using (StreamReader calls = new StreamReader(curCalls))
            {
                while (!calls.EndOfStream)
                {
                    string name = calls.ReadLine();
                    string call = calls.ReadLine();
                    string comment = calls.ReadLine();
                    string quality = calls.ReadLine();
                    numClusters++;
                    if (curCycle < 1)
                        curCycle = call.Length;
                    foreach (string key in reference.Keys)
                    {
                        int errCount = 0;
                        for (int i = 0; i < curCycle; i++)
                        {
                            if (call[i] != reference[key][i])
                                errCount++;
                        }
                        if (errCount == 0)
                            perfect[key]++;
                        if (errCount == 1)
                            oneOff[key]++;
                        int dlDist = GetDamerauLevenshteinDistance(reference[key].Substring(0, curCycle), call.Substring(0, curCycle));
                        if (dlDist < 3)
                            DLDist[key]++;
                        if (dlDist < 4)
                            DLDist3[key]++;
                    }
                }
            }

            //Dictionary<string, float> stats = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                perfectMatch[key] = 100.0f * (float)perfect[key] / (float)numClusters;
            }

            //Dictionary<string, float> stats1Error = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                OneOff[key] = 100.0f * ((float)oneOff[key] + (float)perfect[key]) / (float)numClusters;
            }

            foreach (string key in reference.Keys)
            {
                DLDist[key] = 100.0f * (float)DLDist[key] / (float)numClusters;
                DLDist3[key] = 100.0f * (float)DLDist3[key] / (float)numClusters;
            }

            float totPerfect = 0;
            foreach (string key in reference.Keys)
            {
                totPerfect += perfectMatch[key];
            }

            float totOneOff = 0;
            foreach (string key in reference.Keys)
            {
                totOneOff += OneOff[key];
            }

            float totDLDist = 0;
            float totDLDist3 = 0;
            foreach (string key in reference.Keys)
            {
                totDLDist += DLDist[key];
                totDLDist3 += DLDist3[key];
            }

            string statFile = Path.Combine(WorkingDir.FullName, "proc-" + tileName, "Stat" + curCycle + ".txt");

            using (StreamWriter sr = new StreamWriter(statFile, false))
            {

                sr.WriteLine("Name\tPerfect%\t1 edit%\t2 edit%\t3 edit%");
                foreach (string key in reference.Keys)
                {
                    sr.WriteLine(String.Format("{0}\t{1:F1}\t{2:F1}\t{3:F1}\t{4:f1}", key, perfectMatch[key], OneOff[key], DLDist[key], DLDist3[key]));
                }
                sr.WriteLine();
                sr.WriteLine(String.Format("Total\t{0:F1}\t{1:F1}\t{2:F1}\t{3:f1}", totPerfect, totOneOff, totDLDist, totDLDist3));
            }
        }

        public static int GetDamerauLevenshteinDistance(string s, string t)
        {
            var bounds = new { Height = s.Length + 1, Width = t.Length + 1 };

            int[,] matrix = new int[bounds.Height, bounds.Width];

            for (int height = 0; height < bounds.Height; height++) { matrix[height, 0] = height; };
            for (int width = 0; width < bounds.Width; width++) { matrix[0, width] = width; };

            for (int height = 1; height < bounds.Height; height++)
            {
                for (int width = 1; width < bounds.Width; width++)
                {
                    int cost = (s[height - 1] == t[width - 1]) ? 0 : 1;
                    int insertion = matrix[height, width - 1] + 1;
                    int deletion = matrix[height - 1, width] + 1;
                    int substitution = matrix[height - 1, width - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (height > 1 && width > 1 && s[height - 1] == t[width - 2] && s[height - 2] == t[width - 1])
                    {
                        distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);
                    }

                    matrix[height, width] = distance;
                }
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
        }

        string CurrentThreadName
        {
            get
            {
                string name = Thread.CurrentThread.Name;
                if (string.IsNullOrEmpty(name))
                {
                    name = "ID" + Thread.CurrentThread.ManagedThreadId;
                }
                return name;
            }
        }

        string PrepareLogMessageEx(string msg, string dirname) => $"AvailableRAM:{(int)(_RAMCounter.NextValue() / 1000f)}GB ProcessThreads:{Process.GetCurrentProcess().Threads.Count}" +
                            ": [" + CurrentThreadName + "] (" + dirname + "): " + msg;

        string PrepareLogMessage(string msg, string dirname) => "[" + CurrentThreadName + "] (" + dirname + "): " + msg;

        private void LogToFile(string msg, SeqLogFlagEnum flag = SeqLogFlagEnum.NORMAL, DirectoryInfo di = null)
        {
            string logMsg = PrepareLogMessage(msg, (di != null) ? di.Name : "basic");
            switch (flag)
            {
                case SeqLogFlagEnum.OLAERROR:
                    Logger.LogError(logMsg, flag);
                    break;
                case SeqLogFlagEnum.OLAWARNING:
                    Logger.LogWarning(logMsg, flag);
                    break;
                default:
                    Logger.Log(logMsg, flag);
                    break;
            }
            //}

            //if ((flag & SettingsManager.ConfigSettings.SystemConfig.LoggerConfig.OLAFilterOutFlags) == flag)
            //    return;

            //string dirname = "basic";
            //if (di != null)
            //    dirname = di.Name;

            // Temporarily disable logging to the main log, b/c it might be blocking non-OLA threads
#if false
            string msgOut = $"AvailableRAM:{(int)(_RAMCounter.NextValue()/1000f)}GB ProcessThreads:{Process.GetCurrentProcess().Threads.Count}" + 
                            ": [" + CurrentThreadName + "] (" + dirname + "): " + msg;
            
            if (flag == SeqLogFlagEnum.NORMAL)
            {
                Logger.Log(msgOut);
            }
            else if(flag == SeqLogFlagEnum.OLAWARNING)
            {
                Logger.LogWarning(msgOut);
            }
            else if (flag == SeqLogFlagEnum.OLAERROR)
            {
                Logger.LogError(msgOut);
            }
#endif
            //string msgType = "";
            //switch(flag)
            //{
            //    case SeqLogFlagEnum.NORMAL:
            //        msgType = "INFO";
            //        break;
            //    case SeqLogFlagEnum.OLAERROR:
            //        msgType = "ERROR";
            //        break;
            //    case SeqLogFlagEnum.OLAWARNING:
            //        msgType = "WARNING";
            //        break;
            //    case SeqLogFlagEnum.DEBUG:
            //        msgType = "DEBUG";
            //        break;
            //}

            //if (string.IsNullOrEmpty(logMsg))
            //{
            //    logMsg = PrepareLogMessage(msg, (di != null) ? di.Name : "basic");
            //}
            //string txtOut = DateTime.Now.ToLongTimeString() + " |" + msgType + "| " + logMsg;
            //    //$"AvailableRAM:{(int)(_RAMCounter.NextValue() / 1000f)}GB ProcessThreads:{Process.GetCurrentProcess().Threads.Count}" +
            //    //           ": [" + CurrentThreadName + "] (" + dirname + "): " + msg;
            //lock (LogFile)
            //{
            //    File.AppendAllText(LogFile, txtOut + "\r\n");
            //}
        }

        private void FindBlobsForCell(string tileName, DirectoryInfo sd)
        {
        }

        private void BuildTemplateForCell(string tileName, DirectoryInfo sd, int cellThreads)
        {
            Log("Building template in " + sd.FullName, SeqLogFlagEnum.DEBUG);

#if false
            ImageProcessingExeInfo eventInfo = new ImageProcessingExeInfo();
            eventInfo.eImageprocessingExeType = ImageProcessingExeInfo.enImageprocessingExeType.eBuildTmplt;
            eventInfo.AnticipatedMemoryUsage_GB = 2.0;
            ImageProcessingEventArgs eventArgs = new ImageProcessingEventArgs();
            eventArgs.Info = eventInfo;
            OnDispatcherEventInvoke(eventArgs);
#endif
            bool ok = false;
            float maxRAM_GB = 0f;

            // Build command line
            StringBuilder sb = new StringBuilder();
            string exePath = Path.Combine(OLABinDir.FullName, "BuildTmplt-static.exe");
            sb.Append(exePath);
            string newTO = TO.Replace(" -n 0 ", " -n " + cellThreads + " ");
            sb.Append(" " + newTO);
            sb.Append(" -T \"" + TP + "\"");
            sb.Append(" -f \"" + FT + "\"");
            sb.Append(" -G \"" + TT + "\"");
            sb.Append(" -a \"" + ST + "\"");
            if (RecipeRunConfig.OLACatchHardwareExceptionsInCpp)
                sb.Append(" -y 1");
            // This has to be the last entry on the command line
            sb.Append(" proc.txt");

            if (RecipeRunConfig.OLAUseDLL)
                ok = ExecuteCommandDLL(enImageprocessingExeType.eBuildTmplt, sb.ToString(), tileName, sd, out maxRAM_GB);
                //ok = ExecuteCommandDLLNew(enImageprocessingExeType.eBuildTmplt, sb.ToString(), tileName, sd, out maxRAM_GB);
            else
                ok = ExecuteCommandSync(sb.ToString(), sd, out maxRAM_GB);

            if (ok && !IsAbort)
            {
                int numFiles = sd.GetFiles("proc-loc.blb").Length;
                if (numFiles > 0)
                    Log("Template built in " + sd.FullName, SeqLogFlagEnum.DEBUG);
                else
                {
                    Log("File proc-loc.blb not found in " + sd.FullName, SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }
            }

            if (ok && !IsAbort)
            {
                int filesMoved = 0;
                filesMoved = FileManipulator.MoveFile("proc.log", "logs/proc-tmplt.log", sd);
                if (filesMoved < 1)
                {
                    LogToFile("Build template: no log files found", SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }

                filesMoved = FileManipulator.MoveFile("proc-tmplt-at.*", "tmplt", sd);
                if (filesMoved < 1)
                {
                    LogToFile("Build template: no proc-tmplt-at.* files found", SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }

                filesMoved = FileManipulator.MoveFile("proc-tmplt*.*", "tmplt", sd);
                if (filesMoved < 1)
                {
                    LogToFile("Build template: no proc-tmplt*.* files found");
                    ok = false;
                }

                filesMoved = FileManipulator.MoveFile("proc-loc.blb", "tmplt", sd);
                if (filesMoved < 1)
                {
                    LogToFile("Build template: no proc-loc.blb files found", SeqLogFlagEnum.OLAERROR);
                    ok = false;
                }
                //hold off RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".extractInt");
            }

            if (ok && !IsAbort)
            {
                TileProgress[tileName].LastCycleTemplateCreated = MinTemplateCycle;
                TileProgress[tileName].LastBuildTmplt_RAM_GB = maxRAM_GB;
            }

            if (IsAbort)
                LogToFile("Template build is aborted.", SeqLogFlagEnum.OLAWARNING);
        }

        private void WriteParams(DirectoryInfo paramDir, string name, string contents)
        {
            FileInfo target = new FileInfo(Path.Combine(paramDir.FullName, name));
            if (target.Exists)
                target.Delete();

            if (!contents.EndsWith(Environment.NewLine))
                contents += Environment.NewLine;

            File.WriteAllText(target.FullName, contents);
        }

        //public struct IO_COUNTERS
        //{
        //    public ulong ReadOperationCount;
        //    public ulong WriteOperationCount;
        //    public ulong OtherOperationCount;
        //    public ulong ReadTransferCount;
        //    public ulong WriteTransferCount;
        //    public ulong OtherTransferCount;
        //}

        //[DllImport("kernel32.dll")]
        //static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

        private const string BooDLLFilePath = @"C:\bin\libblobs.dll";

        [DllImport(BooDLLFilePath, CallingConvention = CallingConvention.Cdecl)]
        unsafe extern static void* BOO_StartSessionEmpty();

        [DllImport(BooDLLFilePath, CallingConvention = CallingConvention.Cdecl)]
        unsafe extern static void BOO_EndSession(void* sessionHandle);

        [DllImport(BooDLLFilePath, CallingConvention = CallingConvention.Cdecl)]
        unsafe extern static void BOO_Process(void* sessionHandle);

        [DllImport(BooDLLFilePath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        unsafe extern static void BOO_SetParameters(void* sessionHandle,        // seqP session handle
                                                    string TIFName,             // name of a tiff file or a list file with multiple tiffs
                                                    int subPix,                 // type of subpixel resolution
                                                    float threshDiv,            // threshold divisor
                                                    float resolution,           // spot resolution
                                                    int useCorners,             // 4 (0) or 9 (1) selection of neighbors
                                                    int[] tileGeom,             // array of tile geometry parameters
                                                    float[] filter,             // array of FFT filter parameters
                                                    string loadLocs,            // name of file with locations to load
                                                    int imageNum,               // number of image from image stack
                                                    int[] buildTmplt,           // array of parameters for building template
                                                    float[] extract,            // array of parameters for extracting intensities
                                                    int meanNorm,               // normalize stack by mean when merging blobs      
                                                    int mergePerImage,          // merge in each image when building template      
                                                    int saveBinary,             // save binary files (ints, locs, calls, etc.)     
                                                    int saveCSV,                // save CSV files (ints,locs,calls,etc.)           
                                                    int saveMask,               // save binary mask                                
                                                    int numofThreads,           // number of threads to use                        
                                                    string loadAT,              // name of transform file to load                  
                                                    int log,                    // use processing logging                          
                                                    float[] allowShift,         // array of allowed shifts                         
                                                    string loadRefImage,        // name of reference image to load                 
                                                    int savePNG,                // save image with blobs marked
                                                    float imageQuality,         // save image QC CSV file with SNR threshold
                                                    int appendOutput,           // append (1) or overwrite (0) output files
                                                    float mergeFactor,          // additional merge factor for template
                                                    int idAtError               // output identity transform if stage error is detected
                                                    );

        [DllImport(BooDLLFilePath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        unsafe extern static void BOO_SetBaseCallPar(void* sessionHandle,       // seqP session handle
                                                string TIFName,                 // name of a tiff file or a list file with multiple tiffs
                                                string loadIntensities,         // name of file with intensities to load
                                                string loadLocs,                // name of file with locations to load
                                                string baseOrder,               // base order
                                                int callByScore,                // use consensus (0) or score (1,2,3...) to make call           
                                                int clusterDim,                 // stage dimensions used for clustering                         
                                                int debugClust,                 // output cluster debug data:                                   
                                                float minClusterR,              // smallest ball radius for clustering:                         
                                                float clusterEps,               // clustering accuracy                                          
                                                float[] globalPhasing,          // phasing parameters                                           
                                                int seqWidth,                   // output sequences with more than n bases                      
                                                int saveBinary,                 // save binary files (ints, locs, calls, etc.)                  
                                                int saveCSV,                    // save CSV files (ints,locs,calls,etc.)                        
                                                int numofThreads,               // number of threads to use                                     
                                                int log,                        // use processing logging                                       
                                                float imageQuality,             // save image QC CSV file with SNR threshold
                                                float threshDiv,                // threshold divisor
                                                int appendOutput,               // append (1) or overwrite (0) output files
                                                int numColors,                  // number of colors for base calling
                                                int basePerColor,               // number of bases per color 
                                                float[] clusterNorm,            // cluster normalization type and range
                                                int clusterField,               // intensity type to use for clustering
                                                string baseWeightFile,          // name of base weight file
                                                string baseNGramFile,           // name of base ngram file
                                                string baseBNGramFile,          // name of base bngram file
                                                float qcThresh,                 // chastity threshold
                                                string qMapFile                 // q-score map file
                                                );


        [DllImport(@"D:\temp\SeqBlobs\x64\Debug\SeqBlobs.dll", CallingConvention = CallingConvention.StdCall)]
        static extern void SEQ_SetParameters(   string TIFName,             // name of a tiff file or a list file with multiple tiffs               
                                            int subPix,                 // type of subpixel resolution
                                            float threshDiv,            // threshold divisor
                                            float resolution,           // spot resolution
                                            int useCorners,             // 4 (0) or 9 (1) selection of neighbors
                                            int[] tileGeom,             // array of tile geometry parameters
                                            float[] filter,             // array of FFT filter parameters
                                            string loadLocs,            // name of file with locations to load
                                            int imageNum,               // number of image from image stack
                                            int[] buildTmplt,           // array of parameters for building template
                                            float[] extract,            // array of parameters for extracting intensities
                                            int meanNorm,               // normalize stack by mean when merging blobs      
                                            int mergePerImage,          // merge in each image when building template      
                                            int saveBinary,             // save binary files (ints, locs, calls, etc.)     
                                            int saveCSV,                // save CSV files (ints,locs,calls,etc.)           
                                            int saveMask,               // save binary mask                                
                                            int numofThreads,           // number of threads to use                        
                                            string loadAT,              // name of transform file to load                  
                                            int log,                    // use processing logging                          
                                            float[] allowShift,         // array of allowed shifts                         
                                            string loadRefImage,        // name of reference image to load                 
                                            int savePNG,                // save image with blobs marked
                                            float imageQuality,         // save image QC CSV file with SNR threshold
                                            int appendOutput,           // append (1) or overwrite (0) output files
                                            float mergeFactor,          // additional merge factor for template
                                            int idAtError               // output identity transform if stage error is detected
                                            );

        [DllImport(@"D:\temp\SeqBlobs\x64\Debug\SeqBlobs.dll", CallingConvention = CallingConvention.StdCall)]
        static extern void SEQ_Process();

        string BuildFullFilePath(string dirPath, string fileSubPath)
        {
            if (String.IsNullOrEmpty(fileSubPath))
                return String.Empty;

            fileSubPath = fileSubPath.Trim(new char[] { '.', '\\', '/' });

            return Path.Combine(dirPath, fileSubPath);
        }

        unsafe bool ExecuteCommandDLL(enImageprocessingExeType exeType, string command, string tileName, DirectoryInfo workingDir, out float maxRAM_GB)
        {
            maxRAM_GB = 0;

            // These RAM values are for testing only! TODO: get RAM from DLL
            switch (exeType)
            {
                case enImageprocessingExeType.eBuildTmplt:
                    maxRAM_GB = 5;
                    break;
                case enImageprocessingExeType.eExtractInt:
                    maxRAM_GB = 0.73f;
                    break;
                case enImageprocessingExeType.eBaseCall:
                    maxRAM_GB = 2f;
                    break;
            }

            FindBlobsOptions options = new FindBlobsOptions(command);
            void* sessionHandle = BOO_StartSessionEmpty();

            if (exeType == enImageprocessingExeType.eExtractInt)
                LogToFile($"IMAGE NUMBER: {options.imageNum} BOO_StartSessionEmpty SESSION HANDLE: {(int)sessionHandle}", SeqLogFlagEnum.DEBUG);

            //options.log = 0;

            string tileDirPath = Path.Combine(WorkingDir.FullName, "proc-" + tileName);
            string tiffPath = BuildFullFilePath(tileDirPath, "proc.txt");
            string locsPath = BuildFullFilePath(tileDirPath, options.loadLocs);
            string atPath = BuildFullFilePath(tileDirPath, options.loadAT);
            string refImagePath = BuildFullFilePath(tileDirPath, options.loadRefImage);

            try
            {
                switch (exeType)
                {
                    case enImageprocessingExeType.eBuildTmplt:
                    case enImageprocessingExeType.eExtractInt:
                        BOO_SetParameters(sessionHandle,
                                          tiffPath,
                                          options.pixInt,
                                          options.threshDiv,
                                          options.resolution,
                                          options.useCorners,
                                          options.tileGeom,
                                          options.filter,
                                          locsPath,
                                          options.imageNum,
                                          options.buildTmplt,
                                          options.extract,
                                          options.meanNorm,
                                          options.mergePerImage,
                                          options.saveBinary,
                                          options.saveCSV,
                                          options.saveMask,
                                          options.numOfThreads,
                                          atPath,
                                          options.log,
                                          options.allowedShifts,
                                          refImagePath,
                                          options.savePNG,
                                          options.imageQuality[0],
                                          options.appendOutput,
                                          options.mergeFactor,
                                          options.idAtError);
                        if (exeType == enImageprocessingExeType.eExtractInt)
                            LogToFile($"IMAGE NUMBER: {options.imageNum} BOO_SetParameters SESSION HANDLE: {(int)sessionHandle}", SeqLogFlagEnum.DEBUG);
                        break;
                    case enImageprocessingExeType.eBaseCall:
                        tiffPath = "";

                        string bliPath = BuildFullFilePath(Path.Combine(tileDirPath, "calls"), options.loadIntensities); ;

                        string weightPath = options.weightBaseFiles.Count() > 0 ? options.weightBaseFiles[0] : "";
                        weightPath = BuildFullFilePath(tileDirPath, weightPath);

                        string ngramPath = options.weightBaseFiles.Count() > 1 ? options.weightBaseFiles[1] : "";
                        ngramPath = BuildFullFilePath(tileDirPath, ngramPath);

                        string bngramPath = options.weightBaseFiles.Count() > 2 ? options.weightBaseFiles[2] : "";
                        bngramPath = BuildFullFilePath(tileDirPath, bngramPath);

                        BOO_SetBaseCallPar(sessionHandle,
                                           tiffPath,
                                           bliPath,
                                           locsPath,
                                           options.baseOrder,
                                           options.callByScore,
                                           options.clusterDim,
                                           options.debugClust,
                                           options.minClustBallRad,
                                           options.clusterEps,
                                           options.globalPhasing,
                                           options.seqWidth,
                                           options.saveBinary,
                                           options.saveCSV,
                                           options.numOfThreads,
                                           options.log,
                                           options.imageQuality[0],
                                           options.threshDiv,
                                           options.appendOutput,
                                           options.numColors,
                                           options.basePerColor,
                                           options.clusterNorm,
                                           options.clusterField,
                                           weightPath,
                                           ngramPath,
                                           bngramPath,
                                           options.imageQuality[1],
                                           options.qMapFile
                                         );
                        break;
                }
                if (exeType == enImageprocessingExeType.eExtractInt)
                    LogToFile($"IMAGE NUMBER: {options.imageNum} BOO_Process SESSION HANDLE: {(int)sessionHandle}", SeqLogFlagEnum.DEBUG);
                BOO_Process(sessionHandle);
                if (exeType == enImageprocessingExeType.eExtractInt)
                    LogToFile($"IMAGE NUMBER: {options.imageNum} BOO_EndSession SESSION HANDLE: {(int)sessionHandle}", SeqLogFlagEnum.DEBUG);
                BOO_EndSession(sessionHandle);
            }
            catch (Exception objException)
            {
                string errResult = " Exception: " + objException.ToString();
                LogToFile("Exception in ExecuteCommandSync: " + command + "\'" + objException.ToString() + "\'", SeqLogFlagEnum.OLAERROR, workingDir);
            }

            return true;
        }

        unsafe bool ExecuteCommandDLLNew(enImageprocessingExeType exeType, string command, string tileName, DirectoryInfo workingDir, out float maxRAM_GB)
        {
            maxRAM_GB = 0;

            // These RAM values are for testing only! TODO: get RAM from DLL
            switch (exeType)
            {
                case enImageprocessingExeType.eBuildTmplt:
                    maxRAM_GB = 5;
                    break;
                case enImageprocessingExeType.eExtractInt:
                    maxRAM_GB = 0.73f;
                    break;
                case enImageprocessingExeType.eBaseCall:
                    maxRAM_GB = 2f;
                    break;
            }

            FindBlobsOptions options = new FindBlobsOptions(command);

            string tileDirPath = Path.Combine(WorkingDir.FullName, "proc-" + tileName);
            string tiffPath = BuildFullFilePath(tileDirPath, "proc.txt");
            string locsPath = BuildFullFilePath(tileDirPath, options.loadLocs);
            string atPath = BuildFullFilePath(tileDirPath, options.loadAT);
            string refImagePath = BuildFullFilePath(tileDirPath, options.loadRefImage);

            try
            {
                switch (exeType)
                {
                    case enImageprocessingExeType.eBuildTmplt:
                    case enImageprocessingExeType.eExtractInt:
                        SEQ_SetParameters(    tiffPath,
                                          options.pixInt,
                                          options.threshDiv,
                                          options.resolution,
                                          options.useCorners,
                                          options.tileGeom,
                                          options.filter,
                                          locsPath,
                                          options.imageNum,
                                          options.buildTmplt,
                                          options.extract,
                                          options.meanNorm,
                                          options.mergePerImage,
                                          options.saveBinary,
                                          options.saveCSV,
                                          options.saveMask,
                                          options.numOfThreads,
                                          atPath,
                                          options.log,
                                          options.allowedShifts,
                                          refImagePath,
                                          options.savePNG,
                                          options.imageQuality[0],
                                          options.appendOutput,
                                          options.mergeFactor,
                                          options.idAtError);
                        if (exeType == enImageprocessingExeType.eExtractInt)
                            LogToFile($"IMAGE NUMBER: {options.imageNum} SetParameters", SeqLogFlagEnum.DEBUG);
                        SEQ_Process();
                        break;
                }
            }
            catch (Exception objException)
            {
                string errResult = " Exception: " + objException.ToString();
                LogToFile("Exception in ExecuteCommandSync: " + command + "\'" + objException.ToString() + "\'", SeqLogFlagEnum.OLAERROR, workingDir);
            }

            return true;
        }

        public bool ExecuteCommandSync(string command, DirectoryInfo workingDir, out float maxRAM_GB)
        {
            command = command.Replace("'", "\"");

            maxRAM_GB = 0f;
            OLACommandExecutor executor = new OLACommandExecutor(this);
            return executor.ExecuteSync(command, workingDir, out maxRAM_GB);
        }
        public bool ExecuteCommandAsync(string command, DirectoryInfo workingDir)
        {
            command = command.Replace("'", "\"");

            OLACommandExecutor executor = new OLACommandExecutor(this);
            return executor.ExecuteAsync(command, workingDir);
        }

        public class OLACommandExecutor
        {
            private ImageProcessingCMD ImageProc;
            bool InProcess = true;
            Process Proc = new Process();
            private string ProcCmd = "";
            private int ProcId = 0;
            private string Error = "";
            public OLACommandExecutor(ImageProcessingCMD imageProc)
            {
                ImageProc = imageProc;
            }
            private void LogToFile(string msg, SeqLogFlagEnum flag = SeqLogFlagEnum.NORMAL, DirectoryInfo di = null) => ImageProc.LogToFile(msg, flag, di);
            private bool IsAbort() => ImageProc.IsAbort;

            private void ProcessExited(object sender, System.EventArgs e)
            {                
                try
                {
                    InProcess = false;
                    LogToFile($"Process {ProcId}:  {ProcCmd} ends with exit code: {Proc.ExitCode}", Proc.ExitCode == 0 ? SeqLogFlagEnum.DEBUG : SeqLogFlagEnum.OLAERROR);

                    if (Proc.ExitCode != 0)
                    {
                        lock (Error)
                        {
                            Error += "Process:" + ProcCmd + "(" + ProcId + ") -- abnormal exit status.";
                        }
                    }

                    // Get the output into a string
                    if (_LogCMDLineOutput)
                    {
                        string result = Proc.StandardOutput.ReadToEnd();
                        string errResult = Proc.StandardError.ReadToEnd();
                    }

                }
                catch (Exception objException)
                {
                    Error += " Exception: " + objException.ToString();
                    LogToFile("Exception in ExecuteCommandSync: " + ProcCmd + "\'" + objException.ToString() + "\'", SeqLogFlagEnum.OLAERROR);
                }                
            }

            public bool ExecuteSync(string command, DirectoryInfo workingDir, out float maxRAM_GB) //workingDir -- proc dir
            {
                LogToFile(command.ToString());

                maxRAM_GB = 0f;
                try
                {
                    // Create the ProcessStartInfo using "cmd" as the program to be run, and "/c " as the parameters.
                    // Incidentally, /c tells cmd that we want it to execute the command that follows, and then exit.

                    //if (DoNotExe)
                    //    return true;

                    long peakWorkingSet = 0L;
                    //IO_COUNTERS io_counters = new IO_COUNTERS() {ReadOperationCount=0UL,
                    //                                             WriteOperationCount=0UL,
                    //                                             OtherOperationCount=0UL,
                    //                                             ReadTransferCount=0UL,
                    //                                             WriteTransferCount=0UL,
                    //                                             OtherTransferCount=0UL};

                    string[] items = command.Split(' ');
                    ProcCmd = items[0];
                    string args = command.Substring(ProcCmd.Length + 1);
                    //args = args.Replace(" -n 0 ", " -n " + ImageProc.MaxThreads + " ");

                    ProcessStartInfo procStartInfo = new ProcessStartInfo(ProcCmd, args);

                    // The following commands are needed to redirect the standard output.
                    // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    if (_LogCMDLineOutput)
                    {
                        procStartInfo.RedirectStandardOutput = true;
                        procStartInfo.RedirectStandardError = true;
                    }

                    procStartInfo.UseShellExecute = false;
                    procStartInfo.ErrorDialog = false;
                    // Do not create the black window.
                    procStartInfo.CreateNoWindow = true;
                    procStartInfo.WorkingDirectory = workingDir.FullName;

                    //var parent = Process.GetCurrentProcess();
                    //var original = parent.PriorityClass;

                    // Now we create a process, assign its ProcessStartInfo and start it
                    using (Proc)
                    {
                        Proc.StartInfo = procStartInfo;

                        Proc.EnableRaisingEvents = true;
                        Proc.Exited += new EventHandler(ProcessExited);

                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        // START PROCESS
                        if (!Proc.Start())
                            throw new Exception("Process " + ProcCmd + " does not start.");
                        else
                            LogToFile(ProcCmd + " starts", SeqLogFlagEnum.DEBUG, workingDir);

                        try
                        {
                            Proc.ProcessorAffinity = (IntPtr)ImageProc.OLAProcessorAffinityMask;
                            ProcId = Proc.Id;
                            LogToFile("Process:" + Proc.ProcessName + "(" + ProcId + ") is running.", SeqLogFlagEnum.DEBUG);
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Process \"{ProcCmd}\" may have already exited. This (possibly benign) exception: {ex.Message}", SeqLogFlagEnum.OLAWARNING);
                        }

                        int sleep_ms = 200;
                        while (InProcess)
                        {
                            Proc?.Refresh();

                            if (IsAbort())
                            {
                                try
                                {
                                    if (Process.GetProcessById(ProcId)?.Id == ProcId)
                                    {
                                        LogToFile("Process:" + ProcCmd + "(" + ProcId + ") is aborting.", SeqLogFlagEnum.OLAWARNING);
                                        lock (Error)
                                        {
                                            Error += "Process:" + ProcCmd + "(" + ProcId + ") is aborted.";
                                        }

                                        Proc?.Kill();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogToFile($"Process \"{ProcCmd}\" may have already exited. This exception: {ex.Message}", SeqLogFlagEnum.OLAWARNING);
                                    break;
                                }
                            }

                            long? _PeakWorkingSet64 = 0;
                            try
                            {
                                if (!(Proc?.HasExited).GetValueOrDefault(true))
                                    _PeakWorkingSet64 = Proc?.PeakWorkingSet64;                                
                                //GetProcessIoCounters(proc.Handle, out io_counters);
                            }
                            catch (Exception objException)
                            {                                
                                LogToFile($"Profiling Process({ProcId}) Exception: {objException.ToString()}", SeqLogFlagEnum.DEBUG, workingDir);
                            }

                            if (_PeakWorkingSet64.HasValue)
                                peakWorkingSet = Math.Max(peakWorkingSet, _PeakWorkingSet64.Value);

                            if (_LogCMDLineOutput)
                            {
                                string result = Proc.StandardOutput.ReadToEnd();
                                string errResult = Proc.StandardError.ReadToEnd();
                            }

                            Thread.Sleep(sleep_ms);
                        }

                        sw.Stop();

                        if (Error.Length > 0)
                            LogToFile(Error, SeqLogFlagEnum.OLAERROR);

                        LogToFile(ProcCmd + " took " + sw.ElapsedMilliseconds + " ms to exec shell command", SeqLogFlagEnum.DEBUG, workingDir);
                        LogToFile(ProcCmd + " peak RAM: " + String.Format("{0:0.0}", peakWorkingSet / 1024f / 1024f / 1024f) + " GB of memory", SeqLogFlagEnum.DEBUG, workingDir);
                    }

                    // Display the command output.
                    //if (_LogCMDLineOutput)
                    //    LogToFile(result, SeqLogFlagEnum.DEBUG, workingDir);
                    //if (Error.Length > 0)
                    //    LogToFile("cmd - " + Error, SeqLogFlagEnum.OLAERROR, workingDir);

                    // Fill in the maximum RAM usage
                    maxRAM_GB = peakWorkingSet / 1024f / 1024f / 1024f;
                }
                catch (Exception objException)
                {
                    Error += " Exception: " + objException.ToString();
                    LogToFile("Exception in ExecuteCommandSync: " + command + "\'" + objException.ToString() + "\'", SeqLogFlagEnum.OLAERROR, workingDir);
                }

                return Error.Length == 0;
            }

            public bool ExecuteAsync(string command, DirectoryInfo workingDir) //workingDir -- proc dir
            {
                try
                {
                    // Create the ProcessStartInfo using "cmd" as the program to be run, and "/c " as the parameters.
                    // Incidentally, /c tells cmd that we want it to execute the command that follows, and then exit.

                    string[] items = command.Split(' ');
                    ProcCmd = items[0];
                    string args = command.Substring(ProcCmd.Length + 1);

                    ProcessStartInfo procStartInfo = new ProcessStartInfo(ProcCmd, args);

                    // The following commands are needed to redirect the standard output.
                    // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    if (_LogCMDLineOutput)
                    {
                        procStartInfo.RedirectStandardOutput = true;
                        procStartInfo.RedirectStandardError = true;
                    }

                    procStartInfo.UseShellExecute = false;
                    procStartInfo.ErrorDialog = false;
 
                    // Do not create the black window.
                    procStartInfo.CreateNoWindow = true;
                    
                    procStartInfo.WorkingDirectory = workingDir.FullName;

                    // Now we create a process, assign its ProcessStartInfo and start it
                    using (Proc)
                    {
                        Proc.StartInfo = procStartInfo;

                        //Proc.EnableRaisingEvents = true;
                        //Proc.Exited += new EventHandler(ProcessExited);

                        // START PROCESS
                        if (!Proc.Start())
                            throw new Exception("Process " + ProcCmd + " does not start.");
                        else
                            LogToFile(ProcCmd + " starts", SeqLogFlagEnum.DEBUG, workingDir);

                        try
                        {
                            Proc.ProcessorAffinity = (IntPtr)ImageProc.OLAProcessorAffinityMask;
                            ProcId = Proc.Id;
                            LogToFile("Process:" + Proc.ProcessName + "(" + ProcId + ") is running.", SeqLogFlagEnum.DEBUG);
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Process \"{ProcCmd}\" may have already exited. This (possibly benign) exception: {ex.Message}", SeqLogFlagEnum.OLAWARNING);
                        }
                    }
                }
                catch (Exception objException)
                {
                    Error += " Exception: " + objException.ToString();
                    LogToFile("Exception in ExecuteCommandAsync: " + command + "\'" + objException.ToString() + "\'", SeqLogFlagEnum.OLAERROR, workingDir);
                }

                return Error.Length == 0;
            }
        }

        private void RemoveFiles(string directory, string filter)
        {
            DirectoryInfo di = new DirectoryInfo(directory);
            foreach (FileInfo fl in di.GetFiles(filter))
            {
                fl.Delete();
            }
        }

        public void Prep(DirectoryInfo target = null)
        {
            if (target != null)
                Log("Prepping " + target.FullName, SeqLogFlagEnum.DEBUG);
            else
                Log("Prepping all", SeqLogFlagEnum.DEBUG);
            DirectoryInfo di = WorkingDir;
            if (target == null)
            {
                foreach (string i in FCList)
                {
                    Directory.CreateDirectory(Path.Combine(WorkingDir.FullName, "proc-" + i));//  FCList[i]));
                }
            }
            else
            {
                target.Create();
            }

            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (target != null)
                {
                    if (sd.Name != target.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {
                    CleanDirectory(sd.FullName);
                    CleanDirectory(Path.Combine(sd.FullName, "at"));
                    CleanDirectory(Path.Combine(sd.FullName, "calls"));
                    CleanDirectory(Path.Combine(sd.FullName, "logs"));
                    CleanDirectory(Path.Combine(sd.FullName, "locs"));
                    CleanDirectory(Path.Combine(sd.FullName, "extr"));
                    CleanDirectory(Path.Combine(sd.FullName, "qc"));
                    CleanDirectory(Path.Combine(sd.FullName, "tmplt"));
                }
            }
        }

        private void CleanDirectory(string targetDir)
        {
            CreateDirectory(targetDir);
            /* too destructive to remove files - disable until we understand the commercial product
            DirectoryInfo di = new DirectoryInfo(targetDir);
            foreach (FileInfo fi in di.GetFiles())
            {
                fi.Delete();
            }
            */
        }

        private void CreateDirectory(DirectoryInfo sd, string subdir)
        {
            DirectoryInfo newDir = new DirectoryInfo(Path.Combine(sd.FullName, subdir));
            newDir.Create();
        }

        private void CreateDirectory(string dirPath)
        {
            DirectoryInfo newDir = new DirectoryInfo(dirPath);
            newDir.Create();
        }

        private void CreateProcTxt(int startCycle, int stopCycle, DirectoryInfo subDir = null, bool subDirCreate = true)
        {
            DirectoryInfo di = WorkingDir;

            if (!subDirCreate && subDir != null)
            {
                // get tile, assume subDir is proc-tile# dir
                string fcPositionString = subDir.Parent.Name.Substring(subDir.Parent.Name.IndexOf("proc-") + 5);
                FileCreateProcTxt(startCycle, stopCycle, fcPositionString, ImageDataDir, subDir);
                return;
            }

            //subDirCreate == true or subDir == null
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (subDir != null)
                {
                    if (sd.Name != subDir.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {
                    string fcPositionString = sd.Name.Substring(sd.Name.IndexOf("proc-") + 5);
                    FileCreateProcTxt(startCycle, stopCycle, fcPositionString, ImageDataDir, sd);
                }
            }
        }

        private void FileCreateProcTxt(int startCycle, int stopCycle, string cellPosition, DirectoryInfo data, DirectoryInfo sd)
        {
            List<Tuple<string, DateTime>> selectedFiles = new List<Tuple<string, DateTime>>();

            for (int cycle = startCycle; cycle <= stopCycle; cycle++)
            {
                string cellTag = "_" + cellPosition;
                string cycleTag = "_Inc" + cycle + "_";
                Regex reg = new Regex(string.Format(@"_Inc{0}_(G1|G2|R3|R4)_{1}", cycle, cellPosition));
                var matchingFiles = data.GetFiles("*" + cycleTag + "*" + cellTag + "*.tif", SearchOption.TopDirectoryOnly)
                                     .Where(path => reg.IsMatch(path.ToString()))
                                     .ToList();

                foreach (FileInfo fi in matchingFiles.OrderBy(f => f.Name)) // Order by G1 < G2 < R3 < R4
                {
                    // Check if the list already has file(s) with this combination of cell, cycle and color tags and choose the most recent file
                    Match checkPattern = reg.Match(fi.Name);
                    DateTime lastModified = fi.LastWriteTime;
                    bool moreRecentFileAlreadySelected = false;
                    List<Tuple<string, DateTime>> obsoleteFiles = new List<Tuple<string, DateTime>>();
                    for (var i = selectedFiles.Count - 1; i >= 0; i--)
                    {
                        var f = selectedFiles[i];
                        if (f.Item1.Contains(checkPattern.Value))
                        {
                            if (f.Item2 <= lastModified)
                                obsoleteFiles.Add(f);
                            else if (f.Item2 > lastModified)
                            {
                                moreRecentFileAlreadySelected = true;
                                break;
                            }
                        }
                        else
                            break; // since the original and selected file lists are sorted by cycle and color and since we are looping backwards, no more matches are possible
                    }
                    selectedFiles = selectedFiles.Except(obsoleteFiles).ToList();
                    if (!moreRecentFileAlreadySelected)
                        selectedFiles.Add(Tuple.Create(fi.Name, lastModified));
                }
            }

            StringBuilder outFiles = new StringBuilder();
            for (var i = 0; i < selectedFiles.Count; i++)
                outFiles.AppendLine(Path.Combine(data.FullName, selectedFiles[i].Item1));

            if (selectedFiles.Count < (stopCycle - startCycle + 1) * NC)
            {
                throw new Exception($"Number of tif files in {data.FullName} in the {startCycle}-{stopCycle} range is less than expected.");
            }

            string procPath = Path.Combine(sd.FullName, "proc.txt");
            File.WriteAllText(procPath, outFiles.ToString());
        }

        // Processing a single tile
        public bool RunCycles(List<enImageprocessingExeType> exeTypes,
                              DirectoryNumber dn,
                              int parallelImageCount,
                              bool usingMultipleExtractIntExes,
                              BaseCallRangeInfo baseCallRange,
                              int cellThreads)
        {
            string tileName = dn.Tile;
            DirectoryInfo di = new DirectoryInfo(Path.Combine(dn.BaseDirectory.FullName, "proc-" + tileName));

            string logMsg = $"Type: {String.Join(",", exeTypes.ConvertAll(f => f.ToString()))}, Cycles: {dn.Cycle}, Tile: {dn.Tile}, Dir: {dn.BaseDirectory.Name}";
            LogToFile("Start Runcycles " + logMsg);

            try
            {
                if (!TileProgress[tileName].Prepared)
                {
                    Prep(di);
                    WriteToParams(di);
                    TileProgress[tileName].Prepared = true;
                }

                // If previously there was a problem processing this tile (e.g. failure to build a template, failure to register an image) - just exit.
                // Note, unless we're using a sliding window, a failure to make an _intermediate_ base call doesn't make a tile "failed". Base call may still succeed for a larger number of cycles. 
                if (TileProgress[tileName].FailedTile)
                    return false;

                // Make sure there are no left-overs in the tile directory
                foreach (var file in di.EnumerateFiles("proc_*"))
                {
                    file.Delete();
                }

                if (exeTypes.Contains(enImageprocessingExeType.eFindBlobs))
                {
                    Log("Cycle " + dn.Cycle + " start FindBlobs clause", SeqLogFlagEnum.DEBUG);
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    CreateProcTxt(1, MinTemplateCycle, di);
                    FindBlobsForCell(tileName, di);
                    sw.Stop();
                    LogToFile(tileName + " FindBlobs clause took " + sw.ElapsedMilliseconds + " ms");
                }

                if (IsAbort)
                    return false;

                if (exeTypes.Contains(enImageprocessingExeType.eBuildTmplt))
                {
                    bool isTemplateCreated = TileProgress[tileName].LastCycleTemplateCreated > 0;
                    if (!isTemplateCreated)
                    {
                        // Build the template
                        Log("Cycle " + dn.Cycle + " start BuildTmplt clause", SeqLogFlagEnum.DEBUG);
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        CreateProcTxt(1, MinTemplateCycle, di);
                        BuildTemplateForCell(tileName, di, cellThreads);
                        sw.Stop();
                        LogToFile(tileName + " BuildTmplt clause took " + sw.ElapsedMilliseconds + " ms");

                        isTemplateCreated = TileProgress[tileName].LastCycleTemplateCreated > 0;
                        if (isTemplateCreated)
                        {
                            Debug.Assert(ImageTemplateExists(ImageTemplateBaseDir.FullName, tileName));
                        }
                        else
                        {
                            SetFailedTile(tileName);
                            return false;
                        }
                    }
                }

                if (IsAbort)
                    return false;

                if (exeTypes.Contains(enImageprocessingExeType.eExtractInt))
                {
                    if (!ImageTemplateExists(ImageTemplateBaseDir.FullName, tileName))
                    {
                        LogToFile("End Runcycles " + logMsg + " Cannot run ExtractInt, because image template does not exist", SeqLogFlagEnum.OLAERROR);

                        SetFailedTile(tileName);

                        return false;
                    }

                    int lastCycleExtracted = TileProgress[tileName].LastCycleExtracted;
                    if (lastCycleExtracted < dn.Cycle)
                    {
                        Log("Cycle " + dn.Cycle + " start ExtractInt clause", SeqLogFlagEnum.DEBUG);

                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        // might have multiple threads inside
                        ExtractIntensitiesByCell(tileName, di, lastCycleExtracted + 1, dn.Cycle, parallelImageCount, usingMultipleExtractIntExes);

                        sw.Stop();
                        LogToFile(tileName + " ExtractInt clause took " + sw.ElapsedMilliseconds + " ms");

                        lastCycleExtracted = TileProgress[tileName].LastCycleExtracted;
                        if (lastCycleExtracted < dn.Cycle)
                        {
                            SetFailedTile(tileName);
                            return false;
                        }
                    }
                }

                if (IsAbort)
                    return false;

                if (exeTypes.Contains(enImageprocessingExeType.eBaseCall))
                {
                    int lastCycleExtracted = TileProgress[tileName].LastCycleExtracted;
                    if (lastCycleExtracted < dn.Cycle)
                    {
                        LogToFile("End Runcycles " + logMsg + " Cannot run BaseCall, because intensities are not extracted up to the current cycle", SeqLogFlagEnum.OLAERROR);
                        SetFailedTile(tileName);
                        return false;
                    }

                    // JOIN CYCLES
                    if ((RecipeRunConfig.OLAUseJoinCycles && !(!usingMultipleExtractIntExes && RecipeRunConfig.OLASingleExtractMultipleImagesByCellWithJC)) || 
                        (!RecipeRunConfig.OLABaseCallOnlyPFClusters && RecipeRunConfig.OLAJoinCyclesAtRunEnd && baseCallRange.End == NS)) 
                    {
                        int lastCycleJoined = TileProgress[tileName].LastCycleJoined;
                        if (lastCycleJoined < dn.Cycle)
                        {
                            Log("Cycle " + dn.Cycle + " start JoinCycles clause", SeqLogFlagEnum.DEBUG);

                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            CreateProcTxt(1, dn.Cycle, di); // update to this cycle

                            JoinCyclesByCell(tileName, di, dn.Cycle);

                            sw.Stop();
                            LogToFile(tileName + " JoinCycles clause took " + sw.ElapsedMilliseconds + " ms");

                            lastCycleJoined = TileProgress[tileName].LastCycleJoined;
                            if (lastCycleJoined < dn.Cycle)
                            {
                                SetFailedTile(tileName);
                                return false;
                            }
                        }

                        if (IsAbort)
                            return false;
                    }

                    // BASE CALLING
                    int lastCycleCalled = TileProgress[tileName].LastCycleCalled;
                    TileProgress[tileName].LastBaseCall_RAM_GB = 0;

                    if (lastCycleCalled < dn.Cycle)
                    {
                        Log("Cycle " + dn.Cycle + " start BaseCall clause", SeqLogFlagEnum.DEBUG);

                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        bool pfBlobsCountChanged;
                        BaseCallByCell(tileName, di, baseCallRange, cellThreads, out pfBlobsCountChanged);

                        sw.Stop();
                        LogToFile(tileName + " BaseCall clause took " + sw.ElapsedMilliseconds + " ms");

                        lastCycleCalled = TileProgress[tileName].LastCycleCalled;
                        if (lastCycleCalled < dn.Cycle)
                        {
                            // A BaseCall failure is always terminal for the sliding window. Otherwise, the failure matters only for a BaseCall on the whole run length.
                            if (UseSlidingWindow || dn.Cycle == NS)
                            {
                                LogToFile("End Runcycles " + logMsg + " Failed to call bases", SeqLogFlagEnum.OLAERROR);
                                SetFailedTile(tileName);
                                return false;
                            }
                        }
                        else // See if we need to re-run ExtractInt on some cycles
                        {
                            if (UseSlidingWindow && MustBuildTemplate && RecipeRunConfig.OLABaseCallOnlyPFClusters && pfBlobsCountChanged && baseCallRange.End < NS)
                            {
                                bool ok = true;

                                ResetExtractIntStatus(tileName, di, baseCallRange); // If re-run of ExtractInt is necessary, this will reset TileProgress[tileName].LastCycleExtracted
                                
                                lastCycleExtracted = TileProgress[tileName].LastCycleExtracted;
 
                                if (lastCycleExtracted < dn.Cycle) // if true, we need to re-run ExtractInt
                                {
                                    if (PP[4] == 1.0f || PP[5] == 1.0f)
                                    {
                                        int lastBootStrappingCycle = _CD / 4;
                                        Log($"Re-running ExtractInt clause on tile {tileName}, cycle range [1,{lastBootStrappingCycle}] (1-based)", SeqLogFlagEnum.DEBUG);
                                        sw = new Stopwatch();
                                        sw.Start();
                                        // might have multiple threads inside
                                        ExtractIntensitiesByCell(tileName, di, 1, lastBootStrappingCycle, parallelImageCount, usingMultipleExtractIntExes);
                                        sw.Stop();
                                        LogToFile(tileName + " ExtractInt clause took " + sw.ElapsedMilliseconds + " ms");
                                        if (TileProgress[tileName].LastCycleExtracted < lastBootStrappingCycle)
                                            ok = false;
                                    }

                                    if (ok)
                                    {
                                        Log($"Re-running ExtractInt clause on tile {tileName}, cycle range [{lastCycleExtracted + 1},{dn.Cycle}] (1-based)", SeqLogFlagEnum.DEBUG);
                                        sw = new Stopwatch();
                                        sw.Start();
                                        // might have multiple threads inside
                                        ExtractIntensitiesByCell(tileName, di, lastCycleExtracted + 1, dn.Cycle, parallelImageCount, usingMultipleExtractIntExes);
                                        sw.Stop();
                                        LogToFile(tileName + " ExtractInt clause took " + sw.ElapsedMilliseconds + " ms");
                                    }

                                    if (!ok || TileProgress[tileName].LastCycleExtracted < dn.Cycle)
                                    {
                                        SetFailedTile(tileName);
                                        LogToFile("End Runcycles " + logMsg + " Failed to re-run ExtractInt", SeqLogFlagEnum.OLAERROR);
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }

                //if (_enableBaseStatistics && !IsAbort)
                //{
                //    Dictionary<string, float> perfect = new Dictionary<string, float>();
                //    Dictionary<string, float> oneOff = new Dictionary<string, float>();
                //    sw.Start();
                //    CallMatchStats(tileName, ref perfect, ref oneOff);
                //    sw.Stop();
                //    LogToFile("Realtime stats took " + sw.ElapsedMilliseconds);
                //}
            }
            catch (Exception ex)
            {
                SetFailedTile(tileName);
                LogToFile("End Runcycles " + ex.Message + " " + logMsg, SeqLogFlagEnum.OLAERROR);
            }

            LogToFile("End Runcycles " + logMsg);

            return !IsAbort;
        }

        private UInt64 GetBlobCountFromFile(string blbPath)
        {
            if (!File.Exists(blbPath))
                return 0;

            Int32 version;
            float error;
            UInt64 numClusters = 0;

            try
            {
                using (BinaryReader sr = new BinaryReader(File.Open(blbPath, FileMode.Open)))
                {
                    version = sr.ReadInt32();
                    error = sr.ReadSingle();
                    numClusters = sr.ReadUInt64();
                }
            }

            catch (Exception ex)
            {
                LogToFile("Exception in GetBlobCountFromFile: " + ex.ToString(), SeqLogFlagEnum.OLAERROR);
            }

            return numClusters;
        }

        // If processing PF clusters only, ExtractInt may need to be re-run on the overlapping cycles and on the first few cycles of the whole run - see github Issue 200
        private void ResetExtractIntStatus(string tileName, DirectoryInfo di, BaseCallRangeInfo range)
        {
            Debug.Assert(UseSlidingWindow);
            Debug.Assert(MustBuildTemplate);
            Debug.Assert(RecipeRunConfig.OLABaseCallOnlyPFClusters);
            Debug.Assert(range.IsValid());
            
            if (range.NextOverlap == 0 || range.End == NS)
                return; // no need to re-run ExtractInt in this case

            int lastCycleExtracted = range.End - range.NextOverlap; //lastCycleExtracted must be 1-based

            int start;
            int stop;
            // Reset per-image status on the first few cycles depending on PP[4] and PP[5]
            if (PP[4] == 1.0f || PP[5] == 1.0f)
            {
                start = 0;
                stop = _CD / 4 * 4;
                for (int ii = start; ii < stop; ii++)
                {
                    // reset the status
                    TileProgress[tileName].ImageExtracted[ii] = false;

                    // delete csv, bli, etc. files
                    File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-int.bli", ii)));
                    File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-int.csv", ii)));
                    File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-at.bla", ii)));
                    File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-at.csv", ii)));
                    File.Delete(Path.Combine(di.FullName, "locs", string.Format("proc_{0:D6}-loc.blr", ii)));
                    File.Delete(Path.Combine(di.FullName, "locs", string.Format("proc_{0:D6}-loc.csv", ii)));
                    File.Delete(Path.Combine(di.FullName, "qc", string.Format("proc_{0:D6}-qc.csv", ii)));
                }

                TileProgress[tileName].LastCycleExtracted = 0;
            }
            else
            {
                TileProgress[tileName].LastCycleExtracted = lastCycleExtracted;
            }

            TileProgress[tileName].LastCycleExtracted = lastCycleExtracted;

            // Make sure the cycles we have just reset will be re-joined, if we do use JoinCycles
            TileProgress[tileName].LastCycleJoined = Math.Min(TileProgress[tileName].LastCycleJoined, TileProgress[tileName].LastCycleExtracted);
            
            // Reset per-image status on the overlapping cycles
            start = lastCycleExtracted * 4;
            stop = range.End * 4;
            for (int ii = start; ii < stop; ii++)
            {
                // reset the status
                TileProgress[tileName].ImageExtracted[ii] = false;

                // delete csv, bli, etc. files
                File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-int.bli", ii)));
                File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-int.csv", ii)));
                File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-at.bla", ii)));
                File.Delete(Path.Combine(di.FullName, "extr", string.Format("proc_{0:D6}-at.csv", ii)));
                File.Delete(Path.Combine(di.FullName, "locs", string.Format("proc_{0:D6}-loc.blr", ii)));
                File.Delete(Path.Combine(di.FullName, "locs", string.Format("proc_{0:D6}-loc.csv", ii)));
                File.Delete(Path.Combine(di.FullName, "qc", string.Format("proc_{0:D6}-qc.csv", ii)));
            }
        }

        public void SerializeTileProcessingProgress()
        {
            string progressFilePath = Path.Combine(WorkingDir.FullName, ProgressFileName);

            lock (TileProgress)
            {
                try
                {
                    SettingJsonManipulater jsonManipulator = new SettingJsonManipulater();
                    Task task = jsonManipulator.SaveSettingsToFile(TileProgress, progressFilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }
        }
        public bool IsBadTile(string tileName)
        {
            return TileProgress[tileName].FailedTile;
        }

        public void SetFailedTile(string tileName)
        {
            if (IsAbort) // do not mark a tile as failed if OLA was canceled
                return;

            lock (TileProgress)
            {
                if (!TileProgress.ContainsKey(tileName))
                    TileProgress[tileName] = new TileProcessingProgress();

                TileProgress[tileName].FailedTile = true;
            }
        }

        public void GetAverage_RAM_UsagePerTile(List<ImageProcessingCMD.enImageprocessingExeType> exeTypes,
                                                 int loopcount,
                                                 out float RAM_average_BuildTmplt_GB,
                                                 out float RAM_average_ExtractInt_GB,
                                                 out float RAM_average_BaseCall_GB)
        {
            List<float> RAM_BuiltTmpltUsage = new List<float>();
            List<float> RAM_ExtractIntUsage = new List<float>();
            List<float> RAM_BaseCallUsage = new List<float>();

            foreach (var entry in TileProgress)
            {
                if (entry.Value.FailedTile)
                    continue;

                if (exeTypes.Contains(enImageprocessingExeType.eBuildTmplt) && entry.Value.LastCycleTemplateCreated == loopcount && entry.Value.LastBuildTmplt_RAM_GB > 0)
                    RAM_BuiltTmpltUsage.Add(entry.Value.LastBuildTmplt_RAM_GB);

                if (exeTypes.Contains(enImageprocessingExeType.eExtractInt) && entry.Value.LastCycleExtracted == loopcount && entry.Value.LastExtractInt_RAM_GB > 0)
                    RAM_ExtractIntUsage.Add(entry.Value.LastExtractInt_RAM_GB);

                if (exeTypes.Contains(enImageprocessingExeType.eBaseCall) && entry.Value.LastCycleCalled == loopcount && entry.Value.LastBaseCall_RAM_GB > 0)
                    RAM_BaseCallUsage.Add(entry.Value.LastBaseCall_RAM_GB);
            }

            RAM_average_BuildTmplt_GB = RAM_BuiltTmpltUsage.Count > 0 ? RAM_BuiltTmpltUsage.Average() : 0;
            RAM_average_ExtractInt_GB = RAM_ExtractIntUsage.Count > 0 ? RAM_ExtractIntUsage.Average() : 0;
            RAM_average_BaseCall_GB = RAM_BaseCallUsage.Count > 0 ? RAM_BaseCallUsage.Average() : 0;
        }

        public void GetMaximum_RAM_UsagePerTile(List<ImageProcessingCMD.enImageprocessingExeType> exeTypes,
                                                 int loopcount,
                                                 out float RAM_max_BuildTmplt_GB,
                                                 out float RAM_max_ExtractInt_GB,
                                                 out float RAM_max_BaseCall_GB)
        {
            List<float> RAM_BuiltTmpltUsage = new List<float>();
            List<float> RAM_ExtractIntUsage = new List<float>();
            List<float> RAM_BaseCallUsage = new List<float>();

            foreach (var entry in TileProgress)
            {
                if (entry.Value.FailedTile)
                    continue;

                if (exeTypes.Contains(enImageprocessingExeType.eBuildTmplt) && entry.Value.LastCycleTemplateCreated == MinTemplateCycle && entry.Value.LastBuildTmplt_RAM_GB > 0)
                    RAM_BuiltTmpltUsage.Add(entry.Value.LastBuildTmplt_RAM_GB);

                if (exeTypes.Contains(enImageprocessingExeType.eExtractInt) && entry.Value.LastCycleExtracted == loopcount && entry.Value.LastExtractInt_RAM_GB > 0)
                    RAM_ExtractIntUsage.Add(entry.Value.LastExtractInt_RAM_GB);

                if (exeTypes.Contains(enImageprocessingExeType.eBaseCall) && entry.Value.LastCycleCalled == loopcount && entry.Value.LastBaseCall_RAM_GB > 0)
                    RAM_BaseCallUsage.Add(entry.Value.LastBaseCall_RAM_GB);
            }

            RAM_max_BuildTmplt_GB = RAM_BuiltTmpltUsage.Count > 0 ? RAM_BuiltTmpltUsage.Max() : 0;
            RAM_max_ExtractInt_GB = RAM_ExtractIntUsage.Count > 0 ? RAM_ExtractIntUsage.Max() : 0;
            RAM_max_BaseCall_GB = RAM_BaseCallUsage.Count > 0 ? RAM_BaseCallUsage.Max() : 0;
        }

        public List<string> GetAllTilesWithBaseCallExecuted(int loopcount)
        {
            List<string> tiles = new List<string>();
            foreach (var entry in TileProgress)
            {
                if (entry.Value.LastCycleCalled == loopcount)
                    tiles.Add(entry.Key);
            }

            return tiles;
        }

        public bool BaseCallSucceededOnTile(string tile, int loopcount)
        {
            return TileProgress.ContainsKey(tile) && TileProgress[tile].LastCycleCalled == loopcount;
        }

        //public bool MustRunBaseCall(int cycle, int lastCycleExtracted)
        //{
        //    // If the mode is post-processing, i.e. we process the whole experiment, we run BaseCall on the last cycle and only on the last cycle
        //    if (IsPostProcessing)
        //        return cycle == NS;

        //    // Always run BaseCall on the last cycle
        //    if (cycle == NS) 
        //        return true;

        //    if (cycle >= BaseCallMinCycle)
        //    {
        //        // Check if the current range of cycles contains at least one multiple of BaseCallEveryNthCycle, starting with BaseCallMinCycle. 
        //        // If it does, run BaseCall on the last cycle of the current range.
        //        for (int c = cycle; c >= lastCycleExtracted + 1; c--)
        //        {
        //            if ((c - BaseCallMinCycle) % BaseCallEveryNthCycle == 0)
        //            {
        //                return true;
        //            }
        //        }
        //    }

        //    return false;
        //}

        private static string GetTmpltDirPath(string baseDir, string tileName)
        {
            Debug.Assert(!String.IsNullOrEmpty(baseDir));
            return Path.Combine(baseDir, "proc-" + tileName, "tmplt");
        }

        private static bool ImageTemplateExists(string baseDir, string tileName)
        {
            FileInfo file = new FileInfo(Path.Combine(GetTmpltDirPath(baseDir, tileName), "proc-loc.blb"));
            if (!file.Exists)
                return false;

            file = new FileInfo(Path.Combine(GetTmpltDirPath(baseDir, tileName), "proc-tmplt.tif"));
            if (!file.Exists)
                return false;

            return true;
        }

        public void IndexFormatMergeFastqFiles(bool index, string baseWorkingDir, List<string> goodTiles, long runId, string instrument, string instrumentId, string flowCellId)
        {
            File.WriteAllLines(Path.Combine(baseWorkingDir, GoodTilesFileName), goodTiles); //"GoodTiles.txt"

            string exePath = @"C:\bin\IndexMerge\IndexMerge.exe";

            StringBuilder sb = new StringBuilder();
            sb.Append(exePath);
            sb.Append($" --index {index} --baseWorkingDir {baseWorkingDir} --runId {runId} --instrument {instrument} --instrumentId {instrumentId} --flowCellId {flowCellId}");

            LogToFile(sb.ToString());

            float ram = 0f;
            if (!ExecuteCommandSync(sb.ToString(), new DirectoryInfo(baseWorkingDir), out ram))
                LogToFile("Failed to index/format/merge fastq files", SeqLogFlagEnum.OLAERROR);
        }

        public void BackupOLAResults(string srcDir, string backupDir, string taskDir, string backupStep)
        {
            string exePath = @"C:\bin\OLABackup\OLABackup.exe";

            StringBuilder sb = new StringBuilder();
            sb.Append(exePath);
            sb.Append($" --srcDir {srcDir} --backupDir {backupDir} --taskDir {taskDir} --backupStep {backupStep}");

            LogToFile(sb.ToString());

            if (!ExecuteCommandAsync(sb.ToString(), new DirectoryInfo(srcDir)))
                LogToFile("Failed to back up OLA results", SeqLogFlagEnum.OLAERROR);
        }
    }
}
