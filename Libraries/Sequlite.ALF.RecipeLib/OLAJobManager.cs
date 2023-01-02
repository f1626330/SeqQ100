using Sequlite.ALF.Common;
using Sequlite.Image.Processing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using System.Text.Json.Serialization;
using System.Collections;
//using System.Runtime.InteropServices;
using Sequlite.Image.Processing.utils;
using System.Text;
using System.Text.RegularExpressions;

namespace Sequlite.ALF.RecipeLib
{
    public class OLATile
    {
        public string Name { get; set; } = ""; // e.g. bL102A
        public string Surface { get; set; } = ""; // b or t
        public int Lane { get; set; } = -1; // 1,2,3, or 4
        public int Column { get; set; } = 0; // "00" - "44"
        public string Row { get; set; } = ""; // A,B,C, or D
        public int ID { get; set; } = -1; // an integer representing a combination of all properties
        public bool Valid { get; set; } = false;
        public bool Failed { get; set; } = false;

        public OLATile(string name)
        {
            Name = name;
            string pattern = @"^(?<surface>(b|t))L(?<lane>(1|2|3|4))(?<column>\d{2})(?<row>(A|B|C|D))";
            Match match = Regex.Match(Name, pattern);

            if (match.Success)
            {
                Surface = match.Groups["surface"].Value;
                Lane    = int.Parse(match.Groups["lane"].Value);
                Column  = int.Parse(match.Groups["column"].Value);
                Row     = match.Groups["row"].Value;

                int surface_id = -1;
                switch (Surface)
                {
                    case "b":
                        surface_id = 1;
                        break;
                    case "t":
                        surface_id = 2;
                        break;
                }

                int row_id = -1;
                switch (Row)
                {
                    case "A":
                        row_id = 1;
                        break;
                    case "B":
                        row_id = 2;
                        break;
                    case "C":
                        row_id = 3;
                        break;
                    case "D":
                        row_id = 4;
                        break;
                }

                Debug.Assert(surface_id > 0);
                Debug.Assert(Lane > 0);
                Debug.Assert(Column > 0);
                Debug.Assert(row_id > 0);

                ID = surface_id * 10000 + Lane * 1000 + Column * 10 + row_id;

                Valid = true;
            }
        }
    }
    
    public class OLATileStatus
    {
        public int RecipeCompletedCycles { get; set; } = 0;
        public int OLACompletedCycles { get; set; } = 0;
        public bool Failed { get; set; } = false;

        public OLATileStatus(int recipeCompletedCycles)
        {
            RecipeCompletedCycles = recipeCompletedCycles;
        }
    }

    public class OLASequenceRead
    {
        public int TotalCycles { get; set; } = 0;
        public int RecipeCurrentCycle { get; set; } = 0;
        public int RecipeCompletedCycles { get; set; } = 0;
        public int OLACompletedCycles { get; set; } = 0;
        public Dictionary<string, OLATileStatus> TileStatus = null;
        public Dictionary<string, ImageProcessingCMD.TileProcessingProgress> TileProgress = null;
        public static string TileProgressFileName => "TileProgress.json";

        public OLASequenceRead(int totalCycles, bool allAcquired)
        {
            TotalCycles = totalCycles;

            if (allAcquired) // true if processing data offline
            {
                RecipeCurrentCycle = totalCycles;
                RecipeCompletedCycles = totalCycles;
            }
        }

        public bool IsOLAFinished()
        {
            return TotalCycles == OLACompletedCycles;
        }
    }

    public class IndexCycles
    {
        public int Item1 { get; set; }
        public int Item2 { get; set; }
        public IndexCycles()
        {
            Item1 = 0;
            Item2 = 0;
        }

        public IndexCycles(Tuple<int, int> indexCycles)
        {
            Item1 = indexCycles.Item1;
            Item2 = indexCycles.Item2;
        }
    }

    public class OLAIndexInfo
    {
        public static readonly int INVALID_ID = -1;
        public int Id { get; set; } //a generic unique id for index
        public string Sequence { get; set; } = ""; //for example "CTGAAGCT"

        public static string NonIndexedReads = "NonIndexedReads";

        public OLAIndexInfo()
        {
            Id = INVALID_ID;
            Sequence = "";
        }

        public OLAIndexInfo(int id, string sequence)
        {
            Id = id;
            Sequence = sequence;
        }

        public OLAIndexInfo(OLAIndexInfo another)
        {
            Id = another.Id;
            Sequence = another.Sequence;
        }
    }

    public class OLASampleIndexInfo
    {
        public string Id { get; set; } //sample ID

        public string Name { get; set; } //sample name

        public List<OLAIndexInfo> IndexInfo { get; set; } = new List<OLAIndexInfo>();

        public OLASampleIndexInfo()
        {
            Id = "";
            Name = "";
        }

        public OLASampleIndexInfo(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public OLASampleIndexInfo(OLASampleIndexInfo another)
        {
            Id = another.Id;
            Name = another.Name;

            IndexInfo = another.IndexInfo.ConvertAll(info => new OLAIndexInfo(info));
        }

        public void AddIndexInfo(OLAIndexInfo info, bool autoId = false)
        {
            
            IndexInfo.Add(info);
            if (autoId)
            {
                if (info.Id == OLAIndexInfo.INVALID_ID)
                {
                    info.Id = IndexInfo.Count - 1;
                }
            }
        }
    }

    public class OLALaneSampleIndexInfo
    {
        public int Id { get; set; } //Lane# 1 --

        public List<OLASampleIndexInfo> SampleInfo { get; set; } = new List<OLASampleIndexInfo>();

        public OLALaneSampleIndexInfo() 
        {
            Id = OLAIndexInfo.INVALID_ID;
        }

        public OLALaneSampleIndexInfo(int id)
        {
            Id = id;
        }

        public OLALaneSampleIndexInfo(OLALaneSampleIndexInfo another)
        {
            Id = another.Id;
            SampleInfo = another.SampleInfo.ConvertAll(info => new OLASampleIndexInfo(info));
        }

        public void AddSampleIndexInfo(OLASampleIndexInfo info)
        {
            SampleInfo.Add(info);
        }

        public OLASampleIndexInfo GetOLASampleIndexInfo(string sampleId, string sampleName)
        =>SampleInfo.Where((o) => 
        (o.Id.ToLower() == sampleId.ToLower() && 
        o.Name.ToLower() == sampleName.ToLower())).FirstOrDefault();
            
    }

    //support json serialization
    public class OLASequenceInfo
    {
        public string ExpName { get; set; } = "";
        public string SessionId { get; set; } = "";
        public bool Paired { get; set; } = false;
        public int Cycles { get; set; } = 0;
        public IndexCycles IndexCycles { get; set; } = new IndexCycles();
        [JsonIgnore] public bool Index1Enabled => IndexCycles?.Item1 > 0;
        [JsonIgnore] public bool Index2Enabled => IndexCycles?.Item2 > 0;
        public List<int> Lanes { get; set; } = new List<int>();
        public int Rows { get; set; } = 0;
        public int Columns { get; set; } = 0;
        // [XmlElement("SelectedTemplate")]
        public TemplateOptions Template { get; set; } = TemplateOptions.ecoli;
        public TemplateOptions IndexTemplate { get; set; } = TemplateOptions.idx;
        public string Instrument { get; set; } = "";//model name
        public string InstrumentID { get; set; } = "";//library version
        public string SampleID { get; set; } = "";
        public string FlowCellID { get; set; } = "";
        public string ReagentID { get; set; } = "";

        //[JsonIgnore] public List<OLALaneSampleIndexInfo> LaneSampleIndexInfo { get; set; } = new List<OLALaneSampleIndexInfo>();
        public List<OLALaneSampleIndexInfo> LaneSampleIndexInfo { get; set; } = new List<OLALaneSampleIndexInfo>();
        public void AddLaneSampleIndexInfo(OLALaneSampleIndexInfo info)
        {
            LaneSampleIndexInfo.Add(info);
        }

        public OLASequenceInfo()
        {
        }

        public OLASequenceInfo(string expName, 
                               string sessionId, 
                               bool paired,
                               int cycles, 
                               Tuple<int,int> indexCycles, 
                               int[] lanes, 
                               int rows, 
                               int columns,
                               TemplateOptions template, 
                               TemplateOptions indexTemplate,
                               List<OLALaneSampleIndexInfo> olaLaneSampleIndexInfos = null
                               )
        {
            ExpName = expName;
            SessionId = sessionId;
            Paired = paired;
            Cycles = cycles;
            IndexCycles = new IndexCycles(indexCycles);
            Lanes = lanes.ToList();
            Rows = rows;
            Columns = columns;
            Template = template;
            IndexTemplate = indexTemplate;
            if (olaLaneSampleIndexInfos == null)
            {
                BuildLaneSampleIndexInfo();
            }
            else
            {
                LaneSampleIndexInfo = olaLaneSampleIndexInfos;
            }
        }

        // Generate an Illumina-like run id from the session id
        public bool CreateRunID(out long runId)
        {
            runId = -1;
            string numericSessionId = new String(SessionId.Where(Char.IsDigit).ToArray());
            return long.TryParse(numericSessionId, out runId);
        }

        private void BuildLaneSampleIndexInfo()
        {
            OLASampleIndexInfo sampleInfo = new OLASampleIndexInfo("MySampleId", "MySampleName");

            sampleInfo.AddIndexInfo(new OLAIndexInfo( 0, "ATTACTCG"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 1, "TCCGGAGA"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 2, "CGCTCATT"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 3, "GAGATTCC"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 4, "ATTCAGAA"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 5, "GAATTCGT"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 6, "CTGAAGCT"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 7, "TAATGCGC"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 8, "CGGCTATG"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo( 9, "TCCGCGAA"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo(10, "TCTCGCGC"));
            sampleInfo.AddIndexInfo(new OLAIndexInfo(11, "AGCGATAG"));

            OLALaneSampleIndexInfo laneInfo = new OLALaneSampleIndexInfo(1);
            laneInfo.AddSampleIndexInfo(sampleInfo);

            AddLaneSampleIndexInfo(laneInfo);
        }

        public OLASequenceInfo(OLASequenceInfo another)
        {
            ExpName = another.ExpName;
            SessionId = another.SessionId;
            Paired = another.Paired;
            Cycles = another.Cycles;
            IndexCycles = another.IndexCycles;
            //Index1Enabled = another.Index1Enabled;
            //Index2Enabled = another.Index2Enabled;
            LaneSampleIndexInfo = another.LaneSampleIndexInfo.ConvertAll(info => new OLALaneSampleIndexInfo(info));
            Lanes = another.Lanes.ToList();
            Rows = another.Rows;
            Columns = another.Columns;
            Template = another.Template;
            IndexTemplate = another.IndexTemplate;
            Instrument = String.IsNullOrEmpty(another.Instrument) ? "?" : another.Instrument;
            InstrumentID = String.IsNullOrEmpty(another.InstrumentID) ? "?" : another.InstrumentID;
            SampleID = another.SampleID;
            FlowCellID = String.IsNullOrEmpty(another.FlowCellID) ? "?" : another.FlowCellID;
            ReagentID = String.IsNullOrEmpty(another.ReagentID) ? "?" : another.ReagentID;
        }
    }

    public class OLARunningEventArgs : EventArgs
    {
        public enum OLARunningMessageTypeEnum
        {
           General,
           Cycle_Finished,
           One_Read_Finished,
           Exit,
        }

        public OLARunningMessageTypeEnum MessageType { get; set; } = OLARunningMessageTypeEnum.General;
        public string Message { get; set; }
        public bool IsError { get; set; } = false;
        public List<OLAResultsInfo> Results { get; set; }
    }

    public class OLAWorkingDirInfo
    {
        public string BaseWorkingDir { get; set; } //for example D:\Sequlite\ALF\Recipe\Images\test1_211009-104049
        public string Dir { get; set; } //for example test1_211009-104049  (ExpName + sessionId)
        public string ReadName {get;set;}
        public string OLAFolderName => "OLA";
        public string DataFolderName => "Data";
        public static string BcqcFolderName => "bcqc";
        public string FastQFolderName => "fastq";
        public string IndexMergeLogFileName => "IndexMerge.log";
        public string GoodTilesFileName => ImageProcessingCMD.GoodTilesFileName;
        public string OLAInfoFileName => "OLAInfo.txt";
        public string DirWithInstrumentName
        {
            get
            {
                if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.UseSubFolderForDataBackup)
                {
                    return Path.Combine(Dir, SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.RecipeRunBackupSubDir);
                }
                else
                {
                    return Dir;
                }
            }
        }
    }

    public class OLABaseCallRangeGenerator
    {
        private List<BaseCallRangeInfo> BaseCallRanges = new List<BaseCallRangeInfo>();

        private int CurrentRange = -1; // an index of the base calling range

        ImagingStep.SequenceRead SequenceRead { get; set; }

        private int CycleCount { get; set; } = 0;

        // MaxCyclesLoad has to do with a "sparse mapping" flag used by BaseCall exe. The BaseCall help says:
        // -g/--globalPhasing phasing parameters: [[0, 0, 0, 0, 1, 1, 0, 0, 0, -1, -1, 0]]
        // ...
        // 11    - n>0 - sparse map max(n, range width) cycles, n<=0 - load all cycles
        // When OLASparseMappingOption is set to 3 or 4, OLA behaves as follows. At first, OLA loads all available cycles for as long as the pool of PF clusters keeps changing.
        // The reason is: bcqc, which is based on PF clusters, has to be recalculated every time the PF clusters pool is updated, and it will be recalculated for all loaded cycles. 
        // For example, with Illumina's definition of PF clusters and using a sliding window SW5-5-7, all available cycles will be loaded (and bcqc recalculated)
        // from -x "0 5" through -x "23 30". After PF clusters have settled, OLA can load any number of cycles for every range as long that number of cycles is
        // no less than the range width.  The fewer cycles OLA loads, the faster it runs, although the sequence matching quality may get worse as the number of loaded cycles decreases. 
        // So the MaxCyclesLoad parameter keeps track of the growing number of loaded cycles while the sliding window is within the PF length.
        int MaxCyclesLoad { get; set; } = 0;

        private int EndOfFirstRangeToUseYngrams { get; set; } = -1;

        public OLABaseCallRangeGenerator(ImagingStep.SequenceRead read, int cycleCount)
        {
            SequenceRead = read;
            CycleCount = cycleCount;

            BaseCallRanges.Clear();

            if (read == ImagingStep.SequenceRead.Read1 || read == ImagingStep.SequenceRead.Read2)
            {
                if (SettingsManager.ConfigSettings.SystemConfig.OLABaseCallRanges.Count > 0)
                    GenerateCustomBaseCallRanges();
                else
                    GenerateSlidingWindowBaseCallRanges();
            }
            else
            {
                Debug.Assert(read == ImagingStep.SequenceRead.Index1 || read == ImagingStep.SequenceRead.Index2);

                GenerateBaseCallRangesForIndexRead();
            }
        }

        public void GenerateSlidingWindowBaseCallRanges()
        {
            // Add the first base call range
            int firstBaseCallCycles = Math.Min(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAMinimumCyclesToCallBases, CycleCount);
            BaseCallRanges.Add(new BaseCallRangeInfo()
            {
                Start = 0,
                End = firstBaseCallCycles,
            });
 
            if (firstBaseCallCycles >= CycleCount)
                return;

            int step    = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLABaseCallEveryNthCycle;
            int overlap = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLASlidingWindowOverlapCycles;

            // Add the sliding window ranges
            int beg;
            int end = firstBaseCallCycles;
            int additional_overlap = 0;
            while(true)
            {
                beg = Math.Max(0, end - overlap);
                end = beg + overlap + step;
                
                // Make sure the last range is within the run length and has the same width as the previous ranges
                if (end > CycleCount)
                {
                    additional_overlap = end - CycleCount;
                    end = CycleCount;
                    beg -= additional_overlap;
                    beg = Math.Max(0, beg);
                }

                BaseCallRanges.Add(new BaseCallRangeInfo() 
                { 
                    Start = beg, 
                    End = end, 
                    Overlap = overlap + additional_overlap
                });

                if (BaseCallRanges.Count > 1)
                    BaseCallRanges[BaseCallRanges.Count - 2].NextOverlap = BaseCallRanges[BaseCallRanges.Count - 1].Overlap;

                if (end == CycleCount)
                    break;
            }
        }

        public void GenerateBaseCallRangesForIndexRead()
        {
            // Add the first base calling range
            int firstBaseCallCycles = CycleCount; // Since Index read is only 8 cycles, we run BaseCall on the whole Index read
            BaseCallRanges.Add(new BaseCallRangeInfo()
            {
                Start = 0,
                End = firstBaseCallCycles,
                Overlap = 0
            });
        }

        private void GenerateCustomBaseCallRanges()
        {
            for (int i = 0; i < SettingsManager.ConfigSettings.SystemConfig.OLABaseCallRanges.Count; i++)
            {
                OLABaseCallRange configRange = SettingsManager.ConfigSettings.SystemConfig.OLABaseCallRanges[i];
                for (int j = 0; j < configRange.Repeat; j++)
                {
                    int beg = 0;
                    int end = 0;
                    if (configRange.Type == "AllCycle")
                    {
                        beg = 0;
                    }
                    else if (configRange.Type == "SlidingWindow")
                    {
                        if (BaseCallRanges.Count > 0)
                            beg = BaseCallRanges[BaseCallRanges.Count - 1].End - configRange.Overlap;
                    }

                    end = beg + configRange.Width;

                    BaseCallRanges.Add(new BaseCallRangeInfo()
                    {
                        Start = beg,
                        End = end,
                        Overlap = configRange.Overlap
                    });
                }
            }
        }

        // Fill in BaseCall range info and return true if base calling is required. Return false otherwise.
        public bool GetCurrentBaseCallRange(int maxAvailableCycle, int maxProcessedCycle, out BaseCallRangeInfo range)
        {
            BaseCallRangeInfo defRange = CurrentRange < 0 ? new BaseCallRangeInfo() : BaseCallRanges[CurrentRange];

            if (maxAvailableCycle >= BaseCallRanges[CurrentRange + 1].End) // base calling is required
            {
                while (true)
                {
                    CurrentRange++;
                    range = BaseCallRanges[CurrentRange];
                    if (range.End > maxProcessedCycle)
                        break;
                }

                range.SparseCycleMappingFlag = CalculateSparseMappingFlag(range.Start, range.End);
                range.UseYngrams = IsConfiguredToUseYngrams(range.Start, range.End);

                return true;
            }
            else // no base calling required
            {
                range = defRange;
                return false;
            }
        }

        // Calculate sparse mapping flag depending on the range limits
        private int CalculateSparseMappingFlag(int start, int end)
        {
            switch (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLASparseMappingOption)
            {
                case 0:
                    return 0; // load all available cycles
                case 1:
                    return (end - start); // load the number of cycles equal to the range width
                case 2:
                    return Math.Max(end - start, SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLASparseMappingCycles);
                case 3:
                    if (start < SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAPFClusterMinimumLength)
                    {
                        MaxCyclesLoad = end; // keep track of the growing number of loaded cycles
                        return 0;  // load all available cycles
                    }
                    else
                        return MaxCyclesLoad; // after PF clusters have settled, keep loading the maximum number loaded so far
                case 4:
                    if (start < SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAPFClusterMinimumLength)
                    {
                        MaxCyclesLoad = end; // keep track of the growing number of loaded cycles
                        return 0;  // load all available cycles
                    }
                    else
                        return Math.Max(end - start, SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLASparseMappingCycles); // after PF clusters have settled, keep loading a constant number of cycles
            }

            Debug.Assert(false); // unknown SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLASparseMappingOption
            return 0;
        }

        // Return true if the [start,end) range is configured to use yngrams
        public bool IsConfiguredToUseYngrams(int start, int end)
        {
            if (end == CycleCount) // always allowed to use y-ngrams on the last range of the sliding window or on the whole run, if post-processing
                return true;

            if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAFirstCycleToUseYngrams <= 0)
                return false; // y-ngrams can be used _only_ on the last range of cycles

            if (EndOfFirstRangeToUseYngrams < 0) // determine the first range to use y-ngrams
            {
                if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAFirstCycleToUseYngrams >= start &&
                    SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAFirstCycleToUseYngrams <= end)
                {
                    EndOfFirstRangeToUseYngrams = end;
                    return true;
                }
            }
            else // if the first range is already determined, go by the step
            {
                if (SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAUseYngramsWithStepCycles > 0 && 
                    (end - EndOfFirstRangeToUseYngrams) % SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.OLAUseYngramsWithStepCycles == 0)
                    return true;
            }

            return false;
        }
    }

    public class OLAJob
    {
        public string tile { get; set; } = "";
        public int latestCycle { get; set; } = 0;
    }

    public class OLAJobManager
    {
        public static string InfoFileName => "info.json";
        public static string OLAInfoFileName => "OLAInfo.txt";
        public string LastUserErrorMessage { get; set; } = string.Empty;
        private static Object LockInstanceCreation = new int[0];
        private static OLAJobManager _OLAJobManager = null;
        //note: for V1 recipe, use this method to get a singleton OLAJobManager
        //for V2 recipe, no need to use a singleton, because imageQ knows when it's on the last cycle.
        public static OLAJobManager GetOLAJobManager(bool isV2 = false, OLASequenceInfo seqInfo = null)
        {
            if (_OLAJobManager == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_OLAJobManager == null)
                    {
                        _OLAJobManager = new OLAJobManager(isV2, seqInfo);
                    }
                }
            }
            return _OLAJobManager;
        }

        public OLAJobManager(bool isV2 = false, OLASequenceInfo seqInfo = null)
        {
            IsV2 = isV2;

            if (seqInfo != null)
            {
                SeqInfo = new OLASequenceInfo(seqInfo);
                OLALogger = SeqLogFactory.GetSeqFileLog($"OLALog-{SeqInfo.ExpName}_{SeqInfo.SessionId}", "OLA");
            }
            else
                OLALogger = SeqLogFactory.GetSeqFileLog($"OLALog", "OLA");
        }

        private bool IsV2 { set; get; } = false;
        private OLASequenceInfo SeqInfo { set; get; } = null;

        public delegate void OLAInfoHandler(OLAWorkingDirInfo e);
        public event OLAInfoHandler OLAInfoUpdated;
        public void OLAUpdatedInvoke(OLAWorkingDirInfo e)
        {
            OLAInfoUpdated?.Invoke(e);
        }
        public event EventHandler<OLARunningEventArgs> OnOLAStatusUpdated;
        public void OnOLAStatusUpdatedInvoke(OLARunningEventArgs e)
        {
            OnOLAStatusUpdated?.Invoke(this, e);
        }
        List<OLATile> TileList { get; set; } = new List<OLATile>();
        HashSet<string> FailedTiles { get; set; } = new HashSet<string>(); // Hash set is used because duplicate items are not allowed

        public bool IsStarted { get; set; } = false;

        public static readonly string LoggerSubSystemName = "OLAJob";
        protected ISeqLog Logger = SeqLogFactory.GetSeqFileLog(LoggerSubSystemName);
        protected ISeqFileLog OLALogger = null;

        public ImagingStep.SequenceRead SequenceRead { get; private set; } = ImagingStep.SequenceRead.Read1;

        public string ModeName { get; set; } = "OLA";

        RecipeRunSettings RecipeRunConfig => SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig;

        public string BaseOfflineProcessingImageDataDir { get; set; } = "";
        public string BaseWorkingDir { get; set; } = "";
        public string WorkingDir { get; set; } = "";

        public bool UseSlidingWindowForOfflineProcessing { get; set; } = false;

        ImageProcessingCMD ImageProc { get; set; } = null;
        Thread OLAJobsThread { get; set; } = null;

        private bool _IsLastCycle { get; set; } = false;
        private bool IsLastCycle(int cycle)
        {
            if (IsV2)
                return _IsLastCycle;

            if (ImageProc != null)
                return ImageProc.NS == cycle;

            return false;
        }

        int[] RunLock { get; } = new int[0];
        public bool IsAbort { get; private set; } = false;

        private float RAM_BuildTmplt_GB { get; set; } = 0f;
        private List<(int maxCycle, int rangeLength, bool useYngrams, float RAM)> RAM_BaseCall_history_GB = new List<(int, int, bool, float)>();
        private SortedDictionary<int, float> RAM_ExtractInt_history_GB = new SortedDictionary<int, float>();
        float estimatedExtractInt_RAM_perSingleImage_GB = 1.5f; // a conservative estimate of the RAM per single image exe
        float estimatedExtractInt_RAM_slope_GBperImage = 0.5f;

        private float AvailableRAM_GB { get; set; }
        private int MaxThreads { get; set; }
        private ulong OLAProcessorAffinityMask { get; set; }

        private long ProcessingTimeThroughFirstBaseCall_ms { get; set; } = 0;
        private long ProcessingTimeAfterFirstBaseCall_ms { get; set; } = 0;
        private long ProcessingTimeOnLastCycle_ms { get; set; } = 0;
        private long ProcessingTimeTotal_ms { get; set; } = 0;

        private OLABaseCallRangeGenerator BaseCallRangeGenerator = null;

        private Dictionary<ImagingStep.SequenceRead, OLASequenceRead> SequenceReads = new Dictionary<ImagingStep.SequenceRead, OLASequenceRead>();

        private ConcurrentQueue<OLAJob> Jobs = new ConcurrentQueue<OLAJob>();

        private Dictionary<(ImagingStep.SequenceRead, int), (double, double)> OLATiming;

        public void Start(string recipeWorkingDir, string offlineDataDir = null, bool useExistingWorkingDir = false, bool useSlidingWindowForOfflineProcessing = false, List<OLATile> selectedTiles = null)
        {
            if (IsStarted)
                return;

            // If the OLAJobs thread is still running - stop it.          
            Stop();
            WaitForAllDone();

            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = "" });

            LogConfiguration();

            // How much RAM is available?
            var performance = new PerformanceCounter("Memory", "Available MBytes");
            var systemAvailableRAM_GB = performance.NextValue() / 1024f;

            // How much RAM can OLA use? 
            AvailableRAM_GB = systemAvailableRAM_GB - SettingsManager.ConfigSettings.SystemConfig.MaxMemoryUsage_GB;

            if (AvailableRAM_GB < 0)
            {
                OLALogger.Log($"Not enough RAM available for OLA. System available RAM {systemAvailableRAM_GB}GB; Anticipated non-OLA max memory usage: {SettingsManager.ConfigSettings.SystemConfig.MaxMemoryUsage_GB}GB", SeqLogFlagEnum.OLAERROR);
                AvailableRAM_GB = 10.0f; // TODO: return an error; probably wrong configuration SettingsManager.ConfigSettings.SystemConfig.MaxMemoryUsage_GB
            }

            OLALogger.Log($"Maximum RAM available to OLA: {AvailableRAM_GB}GB");

            // How many CPU processing cores does the GUI use?
            ulong guiProcessorAffinityMask = SettingsManager.ConfigSettings.SystemConfig.GetProcessorAffinityValue();
            byte[] guiBitArray = BitConverter.GetBytes(guiProcessorAffinityMask);
            int cuiThreadsCount = 0;
            foreach (byte b in guiBitArray)
            {
                // Present each byte as a binary string and count the number of 1s
                string s = Convert.ToString((int)b, 2);
                cuiThreadsCount += s.Count(f => f == '1');
            }

            // How many processing cores do we have in total?
            int totalSystemThreadsCount = Environment.ProcessorCount;

            // How many processing cores can be used by OLA
            MaxThreads = totalSystemThreadsCount - Math.Max(2, cuiThreadsCount); // assuming 2 hyperthreads per core, make sure at least 2 hyperthreads are left to the GUI
            MaxThreads = Math.Max(1, MaxThreads); // sanity check

            // If we have > 64 hyperthreads under Windows 10, Environment.ProcessorCount is only 64, because every process by default gets assigned only one group of cores.
            // So we use RecipeRunConfig.OLAMinimumTotalThreadCount to correct the number of total threads to be used by OLA.
            MaxThreads = Math.Max(MaxThreads, SettingsManager.ConfigSettings.SystemConfig.OLAMinimumTotalThreadCount);

            // Build an affinity mask for up to 64 processors. The reason: System.Diagnostics.Process.ProcessorAffinity, which we set to Vitaly's exes, is 64-bit.
            // Assuming guiProcessorAffinityMask has no more than 64 bits set.
            ulong fullProcessorAffinityMask = BitArrayToU64(new BitArray(Math.Min(64, totalSystemThreadsCount), true));
            if (guiProcessorAffinityMask < fullProcessorAffinityMask)
                OLAProcessorAffinityMask = fullProcessorAffinityMask & ~guiProcessorAffinityMask;
            else
                OLAProcessorAffinityMask = guiProcessorAffinityMask;

            int affinityMaskHexPositions = (int)Math.Ceiling(Environment.ProcessorCount / (float)4); // dividing by 4, because each F in hexadecimal format corresponds to 1111 in binary format (i.e. a mask for 4 processing cores)
            OLALogger.Log($"CUI processor affinity mask: 0x" + SettingsManager.ConfigSettings.SystemConfig.GetProcessorAffinityValue().ToString($"X{affinityMaskHexPositions}"));
            OLALogger.Log("OLA processor affinity mask: 0x" + OLAProcessorAffinityMask.ToString($"X{affinityMaskHexPositions}"));
            OLALogger.Log($"OLA maximum threads count: {MaxThreads}");

            // Set a "sliding window" flag for offline processing
            UseSlidingWindowForOfflineProcessing = useSlidingWindowForOfflineProcessing;

            // Set base working and data directories
            BaseWorkingDir = recipeWorkingDir;
            BaseOfflineProcessingImageDataDir = offlineDataDir;

            CreateBcqcInfoFolder();
            CopyRunInfoFile();

            // Build a tile list
            if (selectedTiles != null)
                TileList = selectedTiles;
            else
            {
                if (!String.IsNullOrEmpty(BaseOfflineProcessingImageDataDir)) // off-line processing
                    TileList = BuildTileList(BaseOfflineProcessingImageDataDir, FailedTiles.ToList());
                else
                    BuildTileList(); // real experiment 
            }

            InitializeSequenceReads();
            
            if (useExistingWorkingDir)
                UpdateFromPreviousTileProgress();

            //// Generally, when an experiment is just started, we want to clean the working directory.
            //// But if an experiment is restarted (loopcount > 1), we can re-use the results obtained before the restart
            //if (loopCount <= 1)
            //{
            //    CleanProcessingResults(WorkingDir);
            //}

            // Create OLA main jobs thread
            OLAJobsThread = new Thread(() => JobsRun());
            OLAJobsThread.IsBackground = true;
            OLAJobsThread.Name = ModeName;
            OLAJobsThread.Priority = ThreadPriority.BelowNormal;

            IsAbort = false;
            OLAJobsThread.Start();

            string msg = $"{ModeName} starts";
            Logger.Log(msg);
            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = msg });

            IsStarted = true;
        }

        // In a real experiment, the run information info.json file is created by Recipe under its working dir.
        // In the offline processing, the run information file must exist under a previously created Recipe working dir, which must also contain a Data dir.
        // The method below copies info.json file to the bcqc directory. In the offline mode, the method also copies the file from the previous Recipe working dir to the offline working dir.
        private void CopyRunInfoFile()
        {
            bool offlineProcessing = !String.IsNullOrEmpty(BaseOfflineProcessingImageDataDir);

            string srcInfoFileDir;
            if (!offlineProcessing)
            {
                srcInfoFileDir = BaseWorkingDir;
            }
            else
            {
                srcInfoFileDir = BaseOfflineProcessingImageDataDir;
            }
            FileInfo srcInfoFilePath = new FileInfo(Path.Combine(srcInfoFileDir, InfoFileName));

            if (!srcInfoFilePath.Exists)
            {
                OLALogger.LogWarning($"Run information file not found: {srcInfoFilePath}", SeqLogFlagEnum.OLAWARNING);
            }
            else
            {
                DirectoryInfo srcInfoFileDirInfo = new DirectoryInfo(srcInfoFileDir);
                FileInfo bcqcTargetInfoFilePath = new FileInfo(Path.Combine(BaseWorkingDir, "bcqc", InfoFileName));
                if (!bcqcTargetInfoFilePath.Exists)
                {
                    FileManipulator.MoveFile(InfoFileName, bcqcTargetInfoFilePath.DirectoryName, srcInfoFileDirInfo, false/*copy file disallowing overwriting of an existing file*/);
                }

                if (offlineProcessing)
                {
                    FileInfo workingDirTargetInfoFilePath = new FileInfo(Path.Combine(BaseWorkingDir, InfoFileName));
                    if (!workingDirTargetInfoFilePath.Exists)
                    {
                        FileManipulator.MoveFile(InfoFileName, workingDirTargetInfoFilePath.DirectoryName, srcInfoFileDirInfo, false/*copy file disallowing overwriting of an existing file*/);
                    }
                }
            }
        }

        private void UpdateFromPreviousTileProgress()
        {
            if (BaseWorkingDir == null)
                return;

            foreach (var read in SequenceReads)
            {
                FileInfo tileProgressFileInfo = new FileInfo(Path.Combine(GetOLAWorkingDirectory(read.Key.ToString()), OLASequenceRead.TileProgressFileName));
                if (!tileProgressFileInfo.Exists)
                {
                    Logger.LogError($"File {tileProgressFileInfo.FullName} does not exist", SeqLogFlagEnum.OLAERROR);
                    continue;
                }

                read.Value.TileProgress = DeserializeTileProgress(tileProgressFileInfo.FullName);

                List<int> olaCompletedCyclesOnTiles = new List<int>();
                foreach (var t in TileList)
                {
                    if (read.Value.TileProgress.ContainsKey(t.Name))
                    {
                        if (!read.Value.TileProgress[t.Name].FailedTile)
                        {
                            int olaCompletedCyclesOnTile = Math.Min(read.Value.TileProgress[t.Name].LastCycleExtracted,
                                                                    read.Value.TileProgress[t.Name].LastCycleCalled);

                            SequenceReads[read.Key].TileStatus[t.Name].OLACompletedCycles = olaCompletedCyclesOnTile;
                            olaCompletedCyclesOnTiles.Add(olaCompletedCyclesOnTile);
                        }
                        else
                        {
                            t.Failed = true;
                        }
                    }
                }

                if (olaCompletedCyclesOnTiles.Count() == 0)
                    Logger.LogError("No tiles are referenced in TileProgress or all tiles are marked as failed", SeqLogFlagEnum.OLAERROR);

                SequenceReads[read.Key].OLACompletedCycles = olaCompletedCyclesOnTiles.Min();
            }

            List<OLATile> newTileList = new List<OLATile>();
            foreach (var t in TileList)
            {
                if (!t.Failed)
                    newTileList.Add(t);
            }

            TileList = newTileList;
        }

        public static Dictionary<string, ImageProcessingCMD.TileProcessingProgress> DeserializeTileProgress(string progressFilePath, ISeqFileLog logger = null)
        {
            Dictionary<string, ImageProcessingCMD.TileProcessingProgress> tileProgress = new Dictionary<string, ImageProcessingCMD.TileProcessingProgress>();

            try
            {
                SettingJsonManipulater jsonManipulator = new SettingJsonManipulater();
                Task<Dictionary<string, ImageProcessingCMD.TileProcessingProgress>> task = jsonManipulator.ReadSettingsFromFile<Dictionary<string, ImageProcessingCMD.TileProcessingProgress>>(progressFilePath);
                Dictionary<string, ImageProcessingCMD.TileProcessingProgress> dict = task.Result;
                foreach (var entry in dict)
                    tileProgress[entry.Key] = new ImageProcessingCMD.TileProcessingProgress(entry.Value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return null;
            }

            return tileProgress;
        }

        private void LogConfiguration()
        {
            OLALogger.Log($"OLA CONFIGURATION");
            OLALogger.Log($"OLASimulationImageBaseDir: {RecipeRunConfig.OLASimulationImageBaseDir}");
            OLALogger.Log($"OLAProcessSimulationImages: {RecipeRunConfig.OLAProcessSimulationImages}");
            OLALogger.Log($"UsingTPL: {RecipeRunConfig.UsingTPL}");
            OLALogger.Log($"UsingTPLForExtractIntensitiesByCell: {RecipeRunConfig.UsingTPLForExtractIntensitiesByCell}");
            OLALogger.Log($"OLASingleExtractMultipleImagesByCell: {RecipeRunConfig.OLASingleExtractMultipleImagesByCell}");
            OLALogger.Log($"OLASingleExtractMultipleImagesByCellWithJC: {RecipeRunConfig.OLASingleExtractMultipleImagesByCellWithJC}");
            OLALogger.Log($"OLAUseJoinCycles: {RecipeRunConfig.OLAUseJoinCycles}");
            OLALogger.Log($"OLAJoinCyclesAtRunEnd: {RecipeRunConfig.OLAJoinCyclesAtRunEnd}");
            OLALogger.Log($"OLAMinimumCyclesToCreateTemplates: {RecipeRunConfig.OLAMinimumCyclesToCreateTemplates}");
            OLALogger.Log($"OLAMinimumCyclesToCallBases: {RecipeRunConfig.OLAMinimumCyclesToCallBases}");
            OLALogger.Log($"OLABaseCallEveryNthCycle: {RecipeRunConfig.OLABaseCallEveryNthCycle}");
            OLALogger.Log($"OLASlidingWindowMain: {RecipeRunConfig.OLASlidingWindowMain}");
            OLALogger.Log($"OLASlidingWindowIndex: {RecipeRunConfig.OLASlidingWindowIndex}");
            OLALogger.Log($"OLASlidingWindowOverlapCycles: {RecipeRunConfig.OLASlidingWindowOverlapCycles}");
            OLALogger.Log($"OLAMinimumOutput: {RecipeRunConfig.OLAMinimumOutput}");
            OLALogger.Log($"OLAUseDLL: {RecipeRunConfig.OLAUseDLL}");
            OLALogger.Log($"OLAIndexCLR: {RecipeRunConfig.OLAIndexCLR}");
            OLALogger.Log($"OLAUpdateCUIOnEveryTileBaseCall: {RecipeRunConfig.OLAUpdateCUIOnEveryTileBaseCall}");
            OLALogger.Log($"OLAPFClusterMinimumLength: {RecipeRunConfig.OLAPFClusterMinimumLength}");
            OLALogger.Log($"OLABaseCallOnlyPFClusters: {RecipeRunConfig.OLABaseCallOnlyPFClusters}");
            OLALogger.Log($"OLASparseMappingOption: {RecipeRunConfig.OLASparseMappingOption}");
            OLALogger.Log($"OLASparseMappingCycles: {RecipeRunConfig.OLASparseMappingCycles}");
            OLALogger.Log($"OLAFirstCycleToUseYngrams: {RecipeRunConfig.OLAFirstCycleToUseYngrams}");
            OLALogger.Log($"OLAUseYngramsWithStepCycles: {RecipeRunConfig.OLAUseYngramsWithStepCycles}");
            OLALogger.Log($"OLAOutOfRangeBaseCallAllowed: {RecipeRunConfig.OLAOutOfRangeBaseCallAllowed}");
            OLALogger.Log($"OLAUseScoresFromPreviousRangeWhenDefiningPFClusters: {RecipeRunConfig.OLAUseScoresFromPreviousRangeWhenDefiningPFClusters}");
            OLALogger.Log($"OLASmoothBCQC: {RecipeRunConfig.OLASmoothBCQC}");
            OLALogger.Log($"OLASmoothBCQCIncludeOutOfRange: {RecipeRunConfig.OLASmoothBCQCIncludeOutOfRange}");
            OLALogger.Log($"OLACatchHardwareExceptionsInCpp: {RecipeRunConfig.OLACatchHardwareExceptionsInCpp}");
            //OLALogger.Log($"OLABackupOffline: {RecipeRunConfig.OLABackupOffline}");
            OLALogger.Log($"OLAMinimumTotalThreadCount: {RecipeRunConfig.OLAMinimumTotalThreadCount}");
        }

        public void InitializeSequenceReads()
        {
            Debug.Assert(SeqInfo != null);

            bool allCyclesAcquired = !String.IsNullOrEmpty(BaseOfflineProcessingImageDataDir); // A developer can manually set allCyclesAcquired to true here, when they are using a sequence simulation, but pretending that the recipe has already acquired all cycles

            SequenceReads.Add(ImagingStep.SequenceRead.Read1, new OLASequenceRead(SeqInfo.Cycles, allCyclesAcquired));
            if (SeqInfo.IndexCycles.Item1 > 0)
                SequenceReads.Add(ImagingStep.SequenceRead.Index1, new OLASequenceRead(SeqInfo.IndexCycles.Item1, allCyclesAcquired));
            if (SeqInfo.IndexCycles.Item2 > 0)
                SequenceReads.Add(ImagingStep.SequenceRead.Index2, new OLASequenceRead(SeqInfo.IndexCycles.Item2, allCyclesAcquired));
            if (SeqInfo.Paired)
                SequenceReads.Add(ImagingStep.SequenceRead.Read2, new OLASequenceRead(SeqInfo.Cycles, allCyclesAcquired));

            foreach (var read in SequenceReads)
            {
                SequenceReads[read.Key].TileStatus = new Dictionary<string, OLATileStatus>();
                foreach (var t in TileList)
                {
                    SequenceReads[read.Key].TileStatus[t.Name] = new OLATileStatus(allCyclesAcquired ? SequenceReads[read.Key].TotalCycles : 0);
                }
            }
        }

        public void InitializeSequenceRead()
        {
            Debug.Assert(SeqInfo != null);

            // Get number of cycles
            int cycleCount = 0;
            switch (SequenceRead)
            {
                case ImagingStep.SequenceRead.Read1:
                case ImagingStep.SequenceRead.Read2:
                    cycleCount = SeqInfo.Cycles;
                    break;
                case ImagingStep.SequenceRead.Index1:
                    cycleCount = SeqInfo.IndexCycles.Item1;
                    break;
                case ImagingStep.SequenceRead.Index2:
                    cycleCount = SeqInfo.IndexCycles.Item2;
                    break;
            }

            // Generate base calling ranges
            BaseCallRangeGenerator = new OLABaseCallRangeGenerator(SequenceRead, cycleCount);

            // Get template
            TemplateOptions template = TemplateOptions.ecoli;
            switch (SequenceRead)
            {
                case ImagingStep.SequenceRead.Read1:
                case ImagingStep.SequenceRead.Read2:
                    template = SeqInfo.Template;
                    break;
                case ImagingStep.SequenceRead.Index1:
                case ImagingStep.SequenceRead.Index2:
                    template = SeqInfo.IndexTemplate;
                    break;
            }

            // Get working directory
            WorkingDir = GetOLAWorkingDirectory(SequenceRead.ToString());
            if (!string.IsNullOrEmpty(WorkingDir))
            {
                DirectoryInfo info = new DirectoryInfo(WorkingDir);
                if (!info.Exists)
                    info.Create(); // Otherwise can't write to trackingcmd.txt
            }
            OLAUpdatedInvoke(new OLAWorkingDirInfo()  //  new DirectoryInfo(BaseWorkingDir));
            { 
                BaseWorkingDir = BaseWorkingDir,
                Dir = Path.GetFileName(BaseWorkingDir.TrimEnd(Path.DirectorySeparatorChar)),
                ReadName = SequenceRead.ToString(),
            });
            // Get data and other directories
            string imageDataDir = RecipeRunConfig.GetRecipeRunImageDataDir(SeqInfo.ExpName + '_' + SeqInfo.SessionId, SequenceRead.ToString(), false /*not for acquisition*/, BaseOfflineProcessingImageDataDir);
            string imageTemplateBaseDir = GetOLAWorkingDirectory(ImagingStep.SequenceRead.Read1.ToString()); // Templates are built only on Read1; other reads must use Read1 templates
            string qualityDir = Path.Combine(BaseWorkingDir, "bcqc", SequenceRead.ToString());

            //// Initialize tile list
            //ResetTileList();
            ////GenerateTileList(imageDataDir);
            //GenerateTileList();

            // Create ImageProc
            bool mustBuildTemplate = (SequenceRead == ImagingStep.SequenceRead.Read1);
            ImageProc = new ImageProcessingCMD(WorkingDir, imageDataDir, imageTemplateBaseDir, qualityDir, template, IsV2, cycleCount, mustBuildTemplate)
            {
                UseSlidingWindow = IsSlidingWindowConfiguration(),
                OLAProcessorAffinityMask = OLAProcessorAffinityMask,
                Logger = OLALogger,
            };
            if (SequenceReads[SequenceRead].TileProgress != null)
                ImageProc.TileProgress = SequenceReads[SequenceRead].TileProgress;

            OLALogger.Log($"Number of tiles: {TileList.Count}");
            OLALogger.Log($"Number of cycles: {cycleCount}");

            if (ImageProc.UseSlidingWindow)
                OLALogger.Log($"Base calling using sliding window; Step: {RecipeRunConfig.OLABaseCallEveryNthCycle}; Overlap: {RecipeRunConfig.OLASlidingWindowOverlapCycles}");
            else
                OLALogger.Log($"Base calling on all available cycles");
            OLALogger.Log($"Data directory: {imageDataDir}");
            OLALogger.Log($"Working directory: {WorkingDir}");

            ImageProc.LoadProcessingParameters();
            
            if (IsIndexRead())
            {
                // Use lane-sample-index information to create a bngram file to be used by BaseCall on index run
                LaneSampleIndexInfo2IndexBngram();
            }

            ProcessingTimeThroughFirstBaseCall_ms = 0;
            ProcessingTimeAfterFirstBaseCall_ms = 0;
            ProcessingTimeOnLastCycle_ms = 0;
            ProcessingTimeTotal_ms = 0;
        }

        // Create an ASCII fasta file based on LaneSampleIndexInfo and convert it to a bngram
        private void LaneSampleIndexInfo2IndexBngram()
        {
            string indexFastaPath = Path.Combine(WorkingDir, "ngram", "index.fasta");

            if (File.Exists(indexFastaPath))
                File.Delete(indexFastaPath);

            Directory.CreateDirectory(Path.GetDirectoryName(indexFastaPath));
            FileStream fsIndexFasta = File.Create(indexFastaPath);

            using (var writer = new StreamWriter(fsIndexFasta))
            {
                foreach (var lane in SeqInfo.LaneSampleIndexInfo)
                {
                    foreach(var sample in lane.SampleInfo)
                    {
                        foreach (var index in sample.IndexInfo)
                        {
                            string indexInfo = $">{index.Id}\n{index.Sequence}\n";
                            writer.Write(indexInfo);
                        }
                    }
                }
            }

            ImageProc.Fasta2Bngram(indexFastaPath);
        }

        private bool IsIndexRead()
        {
            return SequenceRead == ImagingStep.SequenceRead.Index1 || SequenceRead == ImagingStep.SequenceRead.Index2;
        }

        public void UpdateImagingCycle(int cycle, ImagingStep step, bool isLastCycle)
        {
            if (!IsStarted)
                return;

            lock (RunLock)
            {
                OLALogger.Log($"RECIPE TO OLA: cycle {cycle}");

                Debug.Assert(SequenceReads.ContainsKey(step.Read));
                SequenceReads[step.Read].RecipeCompletedCycles = cycle;
                _IsLastCycle = isLastCycle;

            }
        }

        public void UpdateImagingCycleEx(ImagingStep step, int cycle, string tile)
        {
            if (!IsStarted)
                return;

            lock (RunLock)
            {
                //OLALogger.Log($"RECIPE TO OLA: cycle {cycle}; tile {tile}");

                // When the image acquisition just starts, it is possible OLAJobManager has not been initialized yet
                if (SequenceReads.ContainsKey(step.Read) && SequenceReads[step.Read].TileStatus != null && SequenceReads[step.Read].TileStatus.ContainsKey(tile))
                {
                    SequenceReads[step.Read].RecipeCurrentCycle = cycle;
                    SequenceReads[step.Read].TileStatus[tile].RecipeCompletedCycles = cycle;
                }
            }
        }

        private void UpdateJobQueue(string tile, int cycle)
        {
            Jobs.Enqueue(new OLAJob()
            {
                tile = tile,
                latestCycle = cycle
            });             
        }

        public void Stop()
        {
            IsAbort = true;
            if (ImageProc != null)
                ImageProc.IsAbort = true;

            IsStarted = false;
        }

        // Note, for abort/reset we need to call Stop() before calling WaitForAllDone().
        // Stop() aborts the underlying command processor, and, therefore, the OLA thread will exit.
        public void WaitForAllDone()
        {
            Logger.Log("Waiting for OLA jobs thread exit");
            bool alive = false;
            if (OLAJobsThread != null)
            {
                alive = OLAJobsThread.IsAlive;
                if (alive)
                {
                    OLAJobsThread.Join();
                }
                
            }
            if (alive)
                Logger.Log("OLA jobs thread has exited");
            else
                Logger.Log("OLA jobs thread is not running");
 
            // Force a reset next time Start() is called on OLAJobManager
            IsStarted = false;
        }

        //[DllImport("kernel32.dll")]
        //static extern uint GetCurrentThreadId();

        // Note, this method is executed on the OLAJobManager thread, which is different from the recipe run thread.
        // Therefore, the incorporation cycle, which this thread is processing, may be lagging behind the current recipe cycle. 
        private void JobsRun()
        {
            SequenceRead = ImagingStep.SequenceRead.Read1;
            string msg = "";

            while (!IsAbort) // a loop over all sequence reads (Read1, Index1, etc.)
            {
                if (SequenceReads[SequenceRead].IsOLAFinished())
                {
                    // Get the next sequence read
                    ImagingStep.SequenceRead? nextRead = NextRead(SequenceRead);
                    if (nextRead.HasValue && SequenceReads.ContainsKey(nextRead.Value) && SequenceReads[nextRead.Value].TotalCycles > 0)
                        SequenceRead = nextRead.Value;
                    else
                        break;
                }

                int recipeCompletedCycles = 0;
                int recipeCurrentCycle = 0;
                lock (RunLock)
                {
                    recipeCompletedCycles = SequenceReads[SequenceRead].RecipeCompletedCycles;
                    recipeCurrentCycle = SequenceReads[SequenceRead].RecipeCurrentCycle;
                }

                if (recipeCurrentCycle >= 1)
                    InitializeSequenceRead();
                else
                {
                    Thread.Sleep(2000);
                    continue;
                }
 
                while (!IsAbort) // a loop over all cycles of the current sequence read
                {
                    lock (RunLock)
                    {
                        recipeCompletedCycles = SequenceReads[SequenceRead].RecipeCompletedCycles;
                        recipeCurrentCycle = SequenceReads[SequenceRead].RecipeCurrentCycle;
                    }

                    if (SequenceReads[SequenceRead].OLACompletedCycles == recipeCurrentCycle || 
                        recipeCurrentCycle < ImageProc.MinTemplateCycle)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    bool hasError = false;
                    try
                    {
                        bool runBaseCall = false;
                        BaseCallRangeInfo baseCallRange;
                        
                        int procCycle = CalculateNextProcessingCycle(recipeCurrentCycle, SequenceReads[SequenceRead].OLACompletedCycles, out runBaseCall, out baseCallRange);                        
                        
                        msg = $"{ModeName} starts for cycle {procCycle}";
                        Logger.Log(msg);
                        OnOLAStatusUpdatedInvoke(new OLARunningEventArgs(){Message = msg});

                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        RunOnlineImageAnalysis(procCycle, runBaseCall, baseCallRange);
                        sw.Stop();

                        long elapsed_ms = sw.ElapsedMilliseconds;
                        TimeSpan time_span = new TimeSpan(0, 0, 0, 0, (int)elapsed_ms);

                        if (procCycle <= ImageProc.BaseCallMinCycle)
                            ProcessingTimeThroughFirstBaseCall_ms += elapsed_ms;
                        else if (procCycle < ImageProc.NS)
                            ProcessingTimeAfterFirstBaseCall_ms += elapsed_ms;
                        else
                            ProcessingTimeOnLastCycle_ms += elapsed_ms;

                        ProcessingTimeTotal_ms += (long)time_span.TotalMilliseconds;

                        SequenceReads[SequenceRead].OLACompletedCycles = procCycle;

                        OLALogger.Log($"RunOnlineImageAnalysis on cycle: {procCycle} took {time_span:c}");

                        // Update the GUI
                        if (runBaseCall)
                        {
                            List<OLAResultsInfo> results = BuildResultsInfoList(procCycle);
                            if (results.Count > 0)
                            {
                                OnOLAStatusUpdatedInvoke(new OLARunningEventArgs()
                                {
                                    MessageType = OLARunningEventArgs.OLARunningMessageTypeEnum.Cycle_Finished,
                                    Message = $"Finished {ModeName} on {SequenceRead} through cycle {procCycle}",
                                    Results = results
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        Logger.LogError($"Failed to run {ModeName} for cycle {recipeCurrentCycle} with exception: {ex.Message}\n{ex.StackTrace}", SeqLogFlagEnum.OLAERROR);
                    }

                    if (SequenceReads[SequenceRead].IsOLAFinished())
                    {
                        try
                        {
                            CreateOlaInfoFile();
                        }
                        catch (Exception ex) // in case we are running out of disk space
                        {
                            hasError = true;
                            Logger.LogError($"Failed to create OLA info for {ModeName} with exception: {ex.Message}\n{ex.StackTrace}", SeqLogFlagEnum.OLAERROR);
                        }

                        OnOLAStatusUpdatedInvoke(new OLARunningEventArgs()
                        {
                            Message = $"{ModeName} Ends on {SequenceRead}",
                            MessageType = OLARunningEventArgs.OLARunningMessageTypeEnum.One_Read_Finished,
                            IsError = hasError,
                        });

                        OLALogger.Log($"Processing time through first basecall: {ProcessingTimeThroughFirstBaseCall_ms} ms; Processing time after first basecall: {ProcessingTimeAfterFirstBaseCall_ms} ms; Processing time on last cycle: {ProcessingTimeOnLastCycle_ms} ms");

                        BackupOLAResults();

                        break;
                    }
                }
            }

            bool hasError2 = false;
            List<string> goodTiles = TileList.Where(o => o.Failed != true && !FailedTiles.Contains(o.Name)).Select(o => o.Name).ToList();
            if (goodTiles.Count == 0)
            {
                OLALogger.LogError("No good tiles for index binning available", SeqLogFlagEnum.OLAERROR);
                hasError2 = true;
            }

            if (!hasError2)
            {
                if (!IsAbort)
                {
                    // Create an Illumina-like numeric run id
                    long runId = -1;
                    if (!SeqInfo.CreateRunID(out runId))
                    {
                        Logger.LogError($"Failed to create a run id", SeqLogFlagEnum.OLAERROR);
                    }

                    OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = "Indexing and merging fastq files..." });
                    if (SequenceReads.ContainsKey(ImagingStep.SequenceRead.Index1) || SequenceReads.ContainsKey(ImagingStep.SequenceRead.Index2))
                    {
                        ImageProc.IndexFormatMergeFastqFiles(true, BaseWorkingDir, goodTiles, runId, SeqInfo.Instrument, SeqInfo.InstrumentID, SeqInfo.FlowCellID);
                    }
                    else
                    {
                        ImageProc.IndexFormatMergeFastqFiles(false, BaseWorkingDir, goodTiles, runId, SeqInfo.Instrument, SeqInfo.InstrumentID, SeqInfo.FlowCellID);
                    }
                }
            }

            msg = $"{ModeName} exits";
            Logger.Log(msg);
            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs()
            {
                Message = msg,
                MessageType = OLARunningEventArgs.OLARunningMessageTypeEnum.Exit,
                IsError = hasError2
            });

            if (!IsAbort)
                BackupOLAResults(true/*Final*/);
        }

        // Back up OLA results using a stand-alone executable
        private void BackupOLAResults(bool final=false)
        {
            if (/*!RecipeRunConfig.OLABackupOffline || */!String.IsNullOrEmpty(BaseOfflineProcessingImageDataDir)) // do not back up OLA results when processing data offline
                return;
            
            string backupBaseDir = RecipeRunConfig.AcquiredImageBackupLocation;
            backupBaseDir = Path.Combine(backupBaseDir, SeqInfo.ExpName + '_' + SeqInfo.SessionId);
            if (RecipeRunConfig.UseSubFolderForDataBackup)
                backupBaseDir = Path.Combine(backupBaseDir, RecipeRunConfig.RecipeRunBackupSubDir);

            if (!final)
                ImageProc.BackupOLAResults(BaseWorkingDir, backupBaseDir, RecipeRunConfig.AnalysisTaskLocation, SequenceRead.ToString());
            else
                ImageProc.BackupOLAResults(BaseWorkingDir, backupBaseDir, RecipeRunConfig.AnalysisTaskLocation, SequenceRead == ImagingStep.SequenceRead.Index1 ? "Post_Index1" : "Post_Read1");
        }

        private void CreateOlaInfoFile()
        {
            FileInfo olaInfoFilePath = new FileInfo(Path.Combine(BaseWorkingDir, SequenceRead.ToString(), OLAInfoFileName));

            TemplateOptions template = IsIndexRead() ? SeqInfo.IndexTemplate : SeqInfo.Template;

            DateTime firstImageTime;
            DateTime lastImageTime;
            GetImageCreationFirstAndLastTime(out firstImageTime, out lastImageTime);

            using (StreamWriter sw = File.CreateText(olaInfoFilePath.FullName))
            {
                sw.WriteLine("Date: " + firstImageTime.ToString("yyyy-MM-dd"));
                sw.WriteLine($"Experiment: {SeqInfo.ExpName + '_' + SeqInfo.SessionId}/{SequenceRead.ToString()}");
                sw.WriteLine($"Reference: {template}");
                sw.WriteLine("Method: bc");
                sw.WriteLine($"Parameter: {ImageProcessingCMD.GetParamsName(template, IsV2)}");
                sw.WriteLine($"Instrument: {SeqInfo.Instrument}");
                List<string> goodTiles = TileList.Where(o => o.Failed != true && !FailedTiles.Contains(o.Name)).Select(o => o.Name).ToList();
                sw.WriteLine($"Tile: {goodTiles.Count} ({string.Join(" ", goodTiles)})");
                IEnumerable<int> cycleRange = Enumerable.Range(1, SequenceReads[SequenceRead].OLACompletedCycles);
                sw.WriteLine($"Cycle: {cycleRange.Count()} ({string.Join(" ", cycleRange)})");
                TimeSpan t1 = lastImageTime - firstImageTime;
                sw.WriteLine($"Runtime: {t1.ToString(@"hh\:mm\:ss")}");
                TimeSpan t2 = TimeSpan.FromSeconds(ProcessingTimeTotal_ms / 1000);
                sw.WriteLine($"ProcessTime: {t2.ToString(@"hh\:mm\:ss")}");
            }
        }

        private void GetImageCreationFirstAndLastTime(out DateTime firstTime, out DateTime lastTime)
        {
            firstTime = new DateTime();
            lastTime = new DateTime();

            bool init = false;

            DirectoryInfo imageDir = new DirectoryInfo(RecipeRunConfig.GetRecipeRunImageDataDir(SeqInfo.ExpName + '_' + SeqInfo.SessionId, SequenceRead.ToString(), false /*not for acquisition*/, BaseOfflineProcessingImageDataDir));
            if (!imageDir.Exists)
                return;

            foreach (FileInfo fi in imageDir.GetFiles("*.tif", SearchOption.TopDirectoryOnly))
            {
                if (!init)
                {
                    firstTime = fi.CreationTime;
                    lastTime = fi.CreationTime;

                    init = true;
                }

                if (fi.CreationTime < firstTime)
                    firstTime = fi.CreationTime;

                if (fi.CreationTime > lastTime)
                    lastTime = fi.CreationTime;
            }
        }

        protected int CalculateNextProcessingCycle(int maxAvailableCycle, int maxProcessedCycle, out bool runBaseCall, out BaseCallRangeInfo baseCallRange)
        {
            runBaseCall = false;
            baseCallRange = new BaseCallRangeInfo();

            Debug.Assert(maxAvailableCycle >= ImageProc.MinTemplateCycle);

            // Unless the sliding window is used, the next processing cycle is always the same as the maximum available cycle.
            // Whether or not we should run BaseCall depends on that maximum available cycle and configured frequency of base calling.
            // TODO: Use BaseCallRangeGenerator to fill in the BaseCallRangeInfo object in both sliding window and non-sliding window cases
            if (!IsSlidingWindowConfiguration())
            {
                if (maxAvailableCycle == ImageProc.NS) // Always run BaseCall on the last cycle
                {
                    runBaseCall = true;
                }
                else if (maxAvailableCycle == ImageProc.BaseCallMinCycle)
                {
                    runBaseCall = true;
                }
                else if (maxAvailableCycle > ImageProc.BaseCallMinCycle)
                {
                    // Check if the maximum available cycle contains at least one multiple of BaseCallEveryNthCycle, starting with BaseCallMinCycle. 
                    for (int c = maxAvailableCycle; c >= SequenceReads[SequenceRead].OLACompletedCycles + 1; c--)
                    {
                        if ((c - ImageProc.BaseCallMinCycle) % ImageProc.BaseCallEveryNthCycle == 0)
                        {
                            runBaseCall = true;
                            break;
                        }
                    }
                }

                if (runBaseCall)
                {
                    bool configuredToUseYngrams = BaseCallRangeGenerator.IsConfiguredToUseYngrams(0, maxAvailableCycle);
                    bool paramsHaveYngrams = !String.IsNullOrEmpty(ImageProc.YG);
                    bool isIndexRead = IsIndexRead();
                    baseCallRange = new BaseCallRangeInfo()
                    {
                        Start = 0,
                        End = maxAvailableCycle,
                        Overlap = 0,
                        UseYngrams = configuredToUseYngrams && paramsHaveYngrams && !isIndexRead,
                    };
                }

                return maxAvailableCycle;
            }
            else // sliding window configuration
            {
                runBaseCall = BaseCallRangeGenerator.GetCurrentBaseCallRange(maxAvailableCycle, maxProcessedCycle, out baseCallRange);

                // Make sure sparse mapping is not used on index reads
                if (baseCallRange.SparseCycleMappingFlag != 0 && IsIndexRead())
                    baseCallRange.SparseCycleMappingFlag = 0;

                // Make sure yngrams are not used if run parameters do not specify yngrams. Also, yngrams are not used on index reads.
                if (baseCallRange.UseYngrams && (String.IsNullOrEmpty(ImageProc.YG) || IsIndexRead()))
                    baseCallRange.UseYngrams = false;

                if (runBaseCall)
                    return baseCallRange.End;

                // For timing/debugging purposes it may be useful to process the first MinTemplateCycle cycles separately
                if (SettingsManager.ConfigSettings.SystemConfig.SimulationConfig.IsSimulation && SequenceReads[ImagingStep.SequenceRead.Read1].OLACompletedCycles == 0)
                    return ImageProc.MinTemplateCycle;
            }

            return maxAvailableCycle;
        }

        private bool IsSlidingWindowConfiguration()
        {
            // Normally for offline processing there is no need to use sliding window, because all cycles are available.
            // However, there are 2 special cases: (1) testing sliding window offline and (2) resuming sliding window processing, which was previously interrupted during experiment
            if (!String.IsNullOrEmpty(BaseOfflineProcessingImageDataDir) && !UseSlidingWindowForOfflineProcessing)
                return false;

            return (IsIndexRead() && RecipeRunConfig.OLASlidingWindowIndex) || (!IsIndexRead() && RecipeRunConfig.OLASlidingWindowMain);
        }

        protected void GetRAMRestrictedParallelCount(int loopcount, out int testedTileCount/*how many tiles have been processed for testing purposes*/,
                                                     out int allowedParallelTileCount_BuildTmplt, out int allowedParallelImageCount_BuildTmplt,
                                                     out int allowedParallelTileCount_ExtractInt, out int allowedParallelImageCount_ExtractInt,
                                                     out int allowedParallelTileCount_BaseCall, out int allowedParallelImageCount_BaseCall,
                                                     bool runBuildTmplt,
                                                     bool runBaseCall, BaseCallRangeInfo baseCallRange)
        {
            Debug.Assert(TileList.Count > 0);

            // Initialize parallel counts to some "safe" values: TODO
            allowedParallelTileCount_BuildTmplt = 11;
            allowedParallelImageCount_BuildTmplt = 8;

            allowedParallelTileCount_BaseCall = 11;
            allowedParallelImageCount_BaseCall = 8;

            allowedParallelTileCount_ExtractInt = 11;
            allowedParallelImageCount_ExtractInt = 8;

            // How many cycles do we have to process?
            int cyclesCount = loopcount - SequenceReads[SequenceRead].OLACompletedCycles;
            
            // How many tiles are we testing?
            int tileCountPerTest = AvailableRAM_GB > 20 ? 2 : 1; // Test 2 tiles on ALF2 and 1 tile on ALF1

            List<string> untestedTiles = TileList.Select(o=>o.Name).ToList();
            
            while (true)
            {
                if (untestedTiles.Count == 0)
                    break; // we have run out of tiles to test

                // Do we have to test RAM usage? Or we can predict it (i.e. extrapolate or interpolate) based on the previous tests?

                // For BuildTmplt the RAM usage doesn't grow with the loopcount, so a RAM test is only needed, when the recorded RAM usage is zero.
                bool testBuildTmplt_RAM = RAM_BuildTmplt_GB == 0;
                
                // For ExtractInt a RAM test is needed if we do not have an estimate for an ExtractInt run on a single image
                bool testExtractInt_RAM = RAM_ExtractInt_history_GB.Count == 0 || (RAM_ExtractInt_history_GB.Count > 0 && RAM_ExtractInt_history_GB[1] == 0);
 
                // For BaseCall RAM usage generally grows with the loopcount, so we may have to measure it repeatedly during the experiment 
                bool testBaseCall_RAM = false;
                if (runBaseCall)
                {
                    // Do we already have a RAM measurement for the current conditions: maximum cycle and range length?
                    int bc = RAM_BaseCall_history_GB.Count;
                    bool current_BaseCall_RAM_measured = bc > 0 &&
                                                         RAM_BaseCall_history_GB.Last().maxCycle == baseCallRange.End &&
                                                         RAM_BaseCall_history_GB.Last().rangeLength == (baseCallRange.End - baseCallRange.Start) &&
                                                         RAM_BaseCall_history_GB.Last().RAM > 0;
                    
                    // Can we extrapolate the RAM usage from the previous RAM measurements?
                    bool can_Extrapolate_BaseCall_RAM = CanExtrapolateBaseCallRAM(baseCallRange);
                    
                    // So do we have to test RAM usage for BaseCall?
                    testBaseCall_RAM = !(current_BaseCall_RAM_measured || can_Extrapolate_BaseCall_RAM);
                }

                // So do we have to test RAM usage?
                bool needToRunTest = (runBuildTmplt && testBuildTmplt_RAM) || (runBaseCall && testBaseCall_RAM) || (!runBuildTmplt && !runBaseCall && testExtractInt_RAM);
                if (!needToRunTest)
                    break;

                // Make sure the number of tiles to be tested does not exceed the remaining number of untested tiles
                tileCountPerTest = Math.Min(tileCountPerTest, untestedTiles.Count);

                // Create a list of tiles to be tested and update the list of remaining, or unprocessed, tiles
                int countOfTilesToTest = Math.Min(tileCountPerTest, untestedTiles.Count);
                List<string> tilesToTest = untestedTiles.GetRange(0, countOfTilesToTest).ToList(); // TODO: the test tiles don't necessarily have to be the first tiles
                int countOfUnprocessedTiles = untestedTiles.Count - tilesToTest.Count;
                if (countOfUnprocessedTiles > 0)
                    untestedTiles = untestedTiles.GetRange(countOfTilesToTest, countOfUnprocessedTiles).ToList();
                else
                    untestedTiles.Clear();

                // Run the test - RAM usage will be recorded. ExtractInt and BaseCall RAM usage will be recorded to the ExtractInt and BaseCall usage history respectively.
                OLALogger.Log($"Testing OLA RAM Usage.", SeqLogFlagEnum.DEBUG);

                var exeTypes = new List<ImageProcessingCMD.enImageprocessingExeType>();
                if (runBuildTmplt)
                {
                    exeTypes.Add(ImageProcessingCMD.enImageprocessingExeType.eBuildTmplt);
                    RunCyclesOnTiles(tilesToTest, loopcount, exeTypes, tileCountPerTest, -1, false, baseCallRange, CalculateThreadsPerTile(tileCountPerTest));
                }

                if (RAM_ExtractInt_history_GB.Count == 0) // The history will be empty if the processing has just started or if it was aborted and then resumed
                {
                    exeTypes.Clear();
                    exeTypes.Add(ImageProcessingCMD.enImageprocessingExeType.eExtractInt);
                    
                    // First RAM test is one ExtractInt exe per image.
                    // If "RAM per single image" value is available in TileProgress, there is no need to run a new test
                    bool ok = UpdateSingleImageExtractIntRAMFromPreviousTileProgress();
                    if (!ok && SequenceReads[SequenceRead].OLACompletedCycles == 0) 
                        RunCyclesOnTiles(tilesToTest,         1, exeTypes, tileCountPerTest, CalculateImageConcurrencyPerTile(tileCountPerTest, true, 4), true, baseCallRange);
                    
                    // Next RAM test is one Extractint exe per multiple images processed in parallel
                    if (loopcount > 1)
                        RunCyclesOnTiles(tilesToTest, loopcount, exeTypes, tileCountPerTest, CalculateImageConcurrencyPerTile(tileCountPerTest, RAM_ExtractInt_history_GB.Count == 0, (loopcount - 1) * 4), false, baseCallRange);
                }

                if (runBaseCall)
                {
                    exeTypes.Clear();
                    exeTypes.Add(ImageProcessingCMD.enImageprocessingExeType.eBaseCall);
                    exeTypes.Add(ImageProcessingCMD.enImageprocessingExeType.eExtractInt);  
                    RunCyclesOnTiles(tilesToTest, loopcount, exeTypes, tileCountPerTest, CalculateImageConcurrencyPerTile(tileCountPerTest, RAM_ExtractInt_history_GB.Count == 0, cyclesCount * 4), false, baseCallRange, CalculateThreadsPerTile(tileCountPerTest));
                }
            }

            // How many tiles have we tested in total?
            testedTileCount = TileList.Count - untestedTiles.Count;

            // If all tiles are processed during the testing, there is nothing left to do
            if (testedTileCount == TileList.Count)
                return;

            // For the unprocessed tiles, estimate how many tiles/images can be run in parallel in all cases

            // BuildTmplt & ExtractInt case
            if (runBuildTmplt)
            {
                if (RAM_BuildTmplt_GB > 0)
                {
                    allowedParallelTileCount_BuildTmplt = CalculateOptimalTileConcurrency(RAM_BuildTmplt_GB, TileList.Count - testedTileCount);
                    allowedParallelImageCount_BuildTmplt = CalculateImageConcurrencyPerTile(allowedParallelTileCount_BuildTmplt, RAM_ExtractInt_history_GB.Count == 0, cyclesCount * 4);
                }
                else
                {
                    allowedParallelTileCount_BuildTmplt = Math.Min(allowedParallelTileCount_BuildTmplt, TileList.Count - testedTileCount);
                    allowedParallelImageCount_BuildTmplt = CalculateImageConcurrencyPerTile(allowedParallelTileCount_BuildTmplt, RAM_ExtractInt_history_GB.Count == 0, cyclesCount * 4);
                }
            }

            // ExtractInt & BaseCall case
            if (runBaseCall)
            {
                // Predict BaseCall RAM usage
                float RAM_BaseCall_predicted_GB = 0;
                int bc = RAM_BaseCall_history_GB.Count;
                OLALogger.Log($"History of the BaseCall RAM Usage vs cycle number has {bc} cycle measurements.", SeqLogFlagEnum.DEBUG);

                // Do we have a RAM measurement for exactly the same conditions: maximum cycle and range length?
                bool current_BaseCall_RAM_measured = bc > 0 &&
                                                     RAM_BaseCall_history_GB.Last().maxCycle == baseCallRange.End &&
                                                     RAM_BaseCall_history_GB.Last().rangeLength == (baseCallRange.End - baseCallRange.Start) &&
                                                     RAM_BaseCall_history_GB.Last().RAM > 0;

                // Can we extrapolate RAM usage from the previous RAM measurements?
                bool can_Extrapolate_BaseCall_RAM = CanExtrapolateBaseCallRAM(baseCallRange);

                if (current_BaseCall_RAM_measured)
                {
                    RAM_BaseCall_predicted_GB = RAM_BaseCall_history_GB.Last().RAM;
                    OLALogger.Log($"BaseCall RAM usage (maximum over tiles) is already available for this range: {RAM_BaseCall_predicted_GB} GB", SeqLogFlagEnum.DEBUG);
                }
                else if (can_Extrapolate_BaseCall_RAM)
                {
                    var x1 = RAM_BaseCall_history_GB.ElementAt(bc - 2).maxCycle;
                    var y1 = RAM_BaseCall_history_GB.ElementAt(bc - 2).RAM;

                    var x2 = RAM_BaseCall_history_GB.ElementAt(bc - 1).maxCycle;
                    var y2 = RAM_BaseCall_history_GB.ElementAt(bc - 1).RAM;

                    if (y1 > 0 && y2 > 0 && x1 != x2)
                    {
                        RAM_BaseCall_predicted_GB = ((y1 - y2) * loopcount + (y2 * x1 - y1 * x2)) / (x1 - x2);
                        OLALogger.Log($"Using an extrapolation from the last two measurements (maximum over tiles) of the BaseCall RAM usage: {RAM_BaseCall_predicted_GB} GB", SeqLogFlagEnum.DEBUG);
                    }
                }

                if (RAM_BaseCall_predicted_GB > 0 && RAM_ExtractInt_history_GB.Count > 0)
                {
                    if (!IsSlidingWindowConfiguration() && loopcount == ImageProc.NS)
                    {
                        // Normally we use a smaller -D during the run and then use the most accurate/largest -D on the whole run, after the last cycle is acquired.
                        // Therefore, we anticipate RAM usage increase on the last BaseCall. To account for that, we use a fudge factor of 1.5, which was determined
                        // experimentally by using -D 4 during the run and -D 20 on the whole run.
                        allowedParallelTileCount_BaseCall = CalculateOptimalTileConcurrency(RAM_BaseCall_predicted_GB * 1.5f, TileList.Count - testedTileCount);
                    }
                    else
                        allowedParallelTileCount_BaseCall = CalculateOptimalTileConcurrency(RAM_BaseCall_predicted_GB, TileList.Count - testedTileCount);

                    allowedParallelImageCount_BaseCall = CalculateImageConcurrencyPerTile(allowedParallelTileCount_BaseCall, false, cyclesCount*4);
                }
                else
                {
                    // Check against the number of available tiles
                    allowedParallelTileCount_BaseCall = Math.Min(allowedParallelTileCount_BaseCall, TileList.Count - testedTileCount);

                    // How many images can we extract in parallel?
                    allowedParallelImageCount_BaseCall = CalculateImageConcurrencyPerTile(allowedParallelTileCount_BaseCall, RAM_ExtractInt_history_GB.Count == 0, cyclesCount*4);
                 }
            }

            // ExtractInt-only case. Try to have one tile thread process all images for that tile in the given cycle range.
            if (!runBuildTmplt && !runBaseCall)
            {
                if (RAM_ExtractInt_history_GB.Count > 0)
                {
                    float total_RAM_ExtractInt_per_tile_GB = CalculateExtractIntRAMFromImageCount(4 * cyclesCount, false);
                    allowedParallelTileCount_ExtractInt = CalculateOptimalTileConcurrency(total_RAM_ExtractInt_per_tile_GB, TileList.Count - testedTileCount);
                    allowedParallelImageCount_ExtractInt = CalculateImageConcurrencyPerTile(allowedParallelTileCount_ExtractInt, false, cyclesCount*4);
                }
                else // first test of ExtractInt RAM
                {
                    allowedParallelTileCount_ExtractInt = Math.Min(allowedParallelTileCount_ExtractInt, TileList.Count - testedTileCount);
                    allowedParallelImageCount_ExtractInt = CalculateImageConcurrencyPerTile(allowedParallelTileCount_ExtractInt, true, cyclesCount*4);
                }
            }
        }

        private bool UpdateSingleImageExtractIntRAMFromPreviousTileProgress()
        {
            if (SequenceReads[SequenceRead].TileProgress == null)
                return false;

            List<float> vals = new List<float>();
            foreach (var tile in SequenceReads[SequenceRead].TileProgress)
            {
                if (tile.Value.FailedTile)
                    continue;

                if (tile.Value.LastExtractInt_SingleImage_RAM_GB > 0)
                    vals.Add(tile.Value.LastExtractInt_SingleImage_RAM_GB);
            }

            if (vals.Count == 0)
                return false;

            estimatedExtractInt_RAM_perSingleImage_GB = vals.Average();
            RAM_ExtractInt_history_GB[1] = estimatedExtractInt_RAM_perSingleImage_GB;

            return true;
        }

        private int CalculateThreadsPerTile(int tileCount)
        {
            decimal ret;
            ret = decimal.Round((decimal)MaxThreads / tileCount, 0);

            return Math.Max(1, (int)ret);
        }

        private int CalculateOptimalTileConcurrency(float tileRAM_GB, int tileCount)
        {
            int limit = (int)Math.Max(1, Math.Floor(AvailableRAM_GB / tileRAM_GB));
            limit = Math.Min(limit, MaxThreads);
            limit = Math.Min(limit, tileCount);

            // Partition the tile list as evenly as possible
            int divider = 0;
            while (true)
            {
                divider++;
                if (tileCount / divider <= limit)
                    break;
            }

            return tileCount / divider;
        }

        // This leads to underesimation of RAM, because RAM curve is not a straight line. TODO: maybe try least-squares.
        private void UpdateMultithreadedExtractIntRAMSlope()
        {
            // A crude way to determine the slope from all available RAM measurements
            float RAM_slope = 0f;
            if (RAM_ExtractInt_history_GB.Count > 1 || !(RAM_ExtractInt_history_GB.Count==1 && RAM_ExtractInt_history_GB.ContainsKey(1)))
            {
                foreach (var key in RAM_ExtractInt_history_GB.Keys)
                {
                    if (key == 1)
                        continue;
                    else
                    {
                        RAM_slope += (RAM_ExtractInt_history_GB[key] - estimatedExtractInt_RAM_perSingleImage_GB) / (key - 1);
                    }
                }

                RAM_slope /= (RAM_ExtractInt_history_GB.Count - 1);
            }

            if (RAM_slope > 0) // sanity check
                estimatedExtractInt_RAM_slope_GBperImage = RAM_slope;
        }

        // The return value of this method tells how many images can be processed in parallel when we use one or more ExtractInt exes.
        // If we use multiple per-image ExtractInt exes, the return value of this method tells how many ExtractInt exes can be run in parallel.
        // If we use a single multi-image ExtractInt exe, the return value of this method tells how many threads can be used by that exe, i.e. the -n parameter of the ExtractInt command line.
        private int CalculateImageConcurrencyPerTile(int tileCount, bool usingMultipleExtractIntExes, int totalImages)
        {
            float ret;

            float RAM = AvailableRAM_GB / tileCount;
            
            if (usingMultipleExtractIntExes)
                ret = RAM / estimatedExtractInt_RAM_perSingleImage_GB;
            else
                ret = (RAM - estimatedExtractInt_RAM_perSingleImage_GB) / estimatedExtractInt_RAM_slope_GBperImage + 1;
            
            ret = Math.Min(totalImages, ret);

            // Sanity check: the number of images processed in parallel cannot exceed the number of threads allowed per tile.
            ret = Math.Min((float)CalculateThreadsPerTile(tileCount), ret);

            // Another sanity check: the return value must be 1 or greater.
            return Math.Max(1, (int)ret);
        }

        private float CalculateExtractIntRAMFromImageCount(int imageCount, bool usingMultipleExtractIntExes)
        {
            if (usingMultipleExtractIntExes)
                return estimatedExtractInt_RAM_perSingleImage_GB * imageCount;
            else
                return estimatedExtractInt_RAM_perSingleImage_GB + estimatedExtractInt_RAM_slope_GBperImage * (imageCount - 1);
        }

        private bool CanExtrapolateBaseCallRAM(BaseCallRangeInfo range)
        {
            bool can;
            int bc = RAM_BaseCall_history_GB.Count;

            if (!IsSlidingWindowConfiguration())
            {
                can = bc >= 2 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).RAM > 0 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 2).RAM > 0 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).useYngrams == RAM_BaseCall_history_GB.ElementAt(bc - 2).useYngrams &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).useYngrams == range.UseYngrams;
            }
            else // sliding window
            {
                can = bc >= 2 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).RAM > 0 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 2).RAM > 0 &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).rangeLength == RAM_BaseCall_history_GB.ElementAt(bc - 2).rangeLength &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).rangeLength == (range.End - range.Start) &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).useYngrams == RAM_BaseCall_history_GB.ElementAt(bc - 2).useYngrams &&
                        RAM_BaseCall_history_GB.ElementAt(bc - 1).useYngrams == range.UseYngrams;
            }

            return can;
        }

        public void Update_Maximum_RAM_Usage(List<ImageProcessingCMD.enImageprocessingExeType> exeTypes, int imageConcurrency, bool usingMultipleExtractIntExes, int loopcount, BaseCallRangeInfo baseCallRange)
        {
            // Update RAM usage
            float RAM_max_BuildTmplt_GB = 0;
            float RAM_max_ExtractInt_GB = 0;
            float RAM_max_BaseCall_GB = 0;
            ImageProc.GetMaximum_RAM_UsagePerTile(exeTypes, loopcount, out RAM_max_BuildTmplt_GB, out RAM_max_ExtractInt_GB, out RAM_max_BaseCall_GB);

            if (exeTypes.Contains(ImageProcessingCMD.enImageprocessingExeType.eBuildTmplt))
                RAM_BuildTmplt_GB = RAM_max_BuildTmplt_GB;

            if (exeTypes.Contains(ImageProcessingCMD.enImageprocessingExeType.eExtractInt))
            {
                if (RAM_max_ExtractInt_GB > 0)
                {
                    if (usingMultipleExtractIntExes)
                    {
                        RAM_ExtractInt_history_GB[1] = RAM_max_ExtractInt_GB; // 1 means: RAM usage per 1 image
                        estimatedExtractInt_RAM_perSingleImage_GB = RAM_max_ExtractInt_GB;
                    }
                    //else // using a multi-threaded ExtractInt exe, one per tile 
                    //{
                    //    RAM_ExtractInt_history_GB[imageConcurrency] = RAM_max_ExtractInt_GB;
                    //    if (imageConcurrency == 1)
                    //        estimatedExtractInt_RAM_perSingleImage_GB = RAM_max_ExtractInt_GB;
                        
                    //    // For high concurrencies it underestimates the slope
                    //    //UpdateMultithreadedExtractIntRAMSlope();
                    //}
                }
            }

            if (exeTypes.Contains(ImageProcessingCMD.enImageprocessingExeType.eBaseCall))
            {
                int rangeLength = baseCallRange.End - baseCallRange.Start;
                
                if (RAM_BaseCall_history_GB.Count > 0 &&
                        RAM_BaseCall_history_GB.Last().maxCycle == loopcount &&
                        RAM_BaseCall_history_GB.Last().rangeLength == rangeLength &&
                        RAM_BaseCall_history_GB.Last().useYngrams == baseCallRange.UseYngrams)
                    RAM_BaseCall_history_GB[RAM_BaseCall_history_GB.Count - 1] = (loopcount, rangeLength, baseCallRange.UseYngrams, RAM_max_BaseCall_GB);
                else if (RAM_max_BaseCall_GB > 0)
                    RAM_BaseCall_history_GB.Add((loopcount, rangeLength, baseCallRange.UseYngrams, RAM_max_BaseCall_GB));
            }
        }

        // Run image analysis on the current Read through the loopcount. If runBaseCall is true, the baseCallRange contains information on the base calling range.
        public void RunOnlineImageAnalysis(int loopcount, bool runBaseCall, BaseCallRangeInfo baseCallRange)
        {
            bool hasError = false;

            bool runBuildTmplt = SequenceReads[SequenceRead].OLACompletedCycles == 0 && SequenceRead == ImagingStep.SequenceRead.Read1;

            if (TileList.Count > 0)
            {
                //ImageProc.DeserializeTileProcessingProgress();

                LogMessage($"{ModeName} starts");
                bool usingTPL = RecipeRunConfig.UsingTPL;// true;
                if (usingTPL)
                {
                    OLALogger.Log($"Using TPL to run {ModeName}");

                    // Predict the RAM-allowed number of parallel tiles and parallel images per tile.
                    // We may have to test a few tiles for the RAM usage first.
                    int testedTileCount = 0;

                    int parallelTileCount_BuildTmplt = 0;
                    int parallelImageCount_BuildTmplt = 0;

                    int parallelTileCount_BaseCall = 0;
                    int parallelImageCount_BaseCall = 0;

                    int parallelTileCount_ExtractInt = 0;
                    int parallelImageCount_ExtractInt = 0;

                    GetRAMRestrictedParallelCount(loopcount, out testedTileCount,
                                                  out parallelTileCount_BuildTmplt, out parallelImageCount_BuildTmplt,
                                                  out parallelTileCount_ExtractInt, out parallelImageCount_ExtractInt,
                                                  out parallelTileCount_BaseCall, out parallelImageCount_BaseCall,
                                                  runBuildTmplt,
                                                  runBaseCall, baseCallRange);

                    int unprocessedTilesCount = TileList.Count - testedTileCount;
                    if (unprocessedTilesCount > 0) // if all tiles have been processed during RAM testing, there is nothing left to do here
                    {
                        List<string> tileSubList = TileList.GetRange(testedTileCount, unprocessedTilesCount).Select(o=>o.Name).ToList();

                        // RAM usage of BuildTmplt and BaseCall may allow different numbers of tiles to be processed in parallel, so we run these exes separately  
                        if (runBuildTmplt)
                        {
                            var exeTypes = new List<ImageProcessingCMD.enImageprocessingExeType>() { ImageProcessingCMD.enImageprocessingExeType.eBuildTmplt, ImageProcessingCMD.enImageprocessingExeType.eExtractInt };
                            RunCyclesOnTiles(tileSubList, loopcount, exeTypes, parallelTileCount_BuildTmplt, parallelImageCount_BuildTmplt, RAM_ExtractInt_history_GB.Count == 0, baseCallRange, CalculateThreadsPerTile(parallelTileCount_BuildTmplt));
                        }

                        if (runBaseCall)
                        {
                            var exeTypes = new List<ImageProcessingCMD.enImageprocessingExeType>() { ImageProcessingCMD.enImageprocessingExeType.eExtractInt, ImageProcessingCMD.enImageprocessingExeType.eBaseCall };
                            RunCyclesOnTiles(tileSubList, loopcount, exeTypes, parallelTileCount_BaseCall, parallelImageCount_BaseCall, RAM_ExtractInt_history_GB.Count == 0, baseCallRange, CalculateThreadsPerTile(parallelTileCount_BaseCall));
                            float RAM_GB = RAM_BaseCall_history_GB.Count > 0 ? RAM_BaseCall_history_GB.Last().RAM : 0;
                            int rangeLength = baseCallRange.End - baseCallRange.Start;
                            OLALogger.Log($"Loopcount:{loopcount},Range length:{rangeLength},BaseCall_Maximum_RAM_GB:{RAM_GB}", SeqLogFlagEnum.DEBUG);
                        }

                        if (!runBuildTmplt && !runBaseCall)
                        {
                            var exeTypes = new List<ImageProcessingCMD.enImageprocessingExeType>() { ImageProcessingCMD.enImageprocessingExeType.eExtractInt };
                            RunCyclesOnTiles(tileSubList, loopcount, exeTypes, parallelTileCount_ExtractInt, parallelImageCount_ExtractInt, RAM_ExtractInt_history_GB.Count == 0, baseCallRange);
                        }
                    }
                }
                else
                {
                    //Thread[] newThread = new Thread[ImageProc.TileList.Count];
                    //for (int i = 0; i < ImageProc.TileList.Count; i++)
                    //{
                    //    DirectoryNumber dn = new DirectoryNumber(ImageProc.FCList[i], new DirectoryInfo(ImageProc.WorkingDir.ToString()), loopcount);
                    //    newThread[i] = new Thread(() => ImageProc.RunCycles(dn, 40));
                    //    newThread[i].Name = "Prep" + ImageProc.FCList[i];
                    //    newThread[i].Start();
                    //    newThread[i].Join();
                    //    Thread.Sleep(10);
                    //    ImageProc.SerializeTileProcessingProgress();
                    //}
                }
            }
            else
            {
                hasError = true;
                OLALogger.LogError($"FCList is empty in {ModeName} at cycle {loopcount}, the job has no images to be processed", SeqLogFlagEnum.OLAERROR);
            }

            string msg = hasError ? $"{ModeName} failed on {SequenceRead} cycle {loopcount}" : $"{ModeName} finished on {SequenceRead} cycle {loopcount}";
            LogMessage(msg, hasError ? SeqLogMessageTypeEnum.ERROR : SeqLogMessageTypeEnum.INFO);
            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs()
            {
                IsError = hasError,
                Message = msg,
            }); 
        }

        // Parallel-process a list of tiles starting with the first unprocessed cycle and through the current total number of cycles, i.e. loopcount
        protected void RunCyclesOnTiles(List<string> tiles, 
                                        int loopcount, 
                                        List<ImageProcessingCMD.enImageprocessingExeType> exeTypes, 
                                        int parallelTileCount, 
                                        int imageConcurrency,
                                        bool usingMultipleExtractIntExes,
                                        BaseCallRangeInfo baseCallRange,
                                        int tileThreads=-1)
        {
            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs() { Message = $"Running {ModeName} on {SequenceRead} through cycle {loopcount}" });

            string exeTypeDesc = String.Join(",", exeTypes.ConvertAll(f => f.ToString()));

            ImageProc.FCList = tiles.ToList();
            Debug.Assert(tiles.Count > 0 && parallelTileCount > 0 && parallelTileCount <= tiles.Count);
            OLALogger.Log($"Loopcount: {loopcount}, Executing: {exeTypeDesc}, Parallel tiles: {parallelTileCount}; images per tile: {imageConcurrency}; threads per tile: {tileThreads}");

            var Options = new ParallelOptions();
            Options.MaxDegreeOfParallelism = parallelTileCount;
            List<List<int>> chunks = BuildChunkList(tiles.Count, parallelTileCount);
            Parallel.ForEach(chunks, Options, chunk =>
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    RunCyclesOnTile(tiles[chunk[i]], loopcount, exeTypes, imageConcurrency, usingMultipleExtractIntExes, baseCallRange, tileThreads);
                }
            });

            // Update RAM usage
            Update_Maximum_RAM_Usage(exeTypes, imageConcurrency, usingMultipleExtractIntExes, loopcount, baseCallRange);
        }

        protected void RunCyclesOnTile(string tile, 
                                        int loopcount,
                                        List<ImageProcessingCMD.enImageprocessingExeType> exeTypes,
                                        int imageConcurrency,
                                        bool usingMultipleExtractIntExes,
                                        BaseCallRangeInfo baseCallRange,
                                        int tileThreads = -1)
        {
            if (FailedTiles.Contains(tile))
                return;

            // The while loop below is for a real (not simulated) experiment, where we may have to wait for a tile being acquired.
            // If the cycle is acquired without this tile, the tile is considered "bad".
            bool tileAcquiredByRecipe = false;
            bool cycleCompletedByRecipe = false;
            while (!IsAbort)
            {
                bool waitForTile = true;
                lock (RunLock)
                {
                    tileAcquiredByRecipe = SequenceReads[SequenceRead].TileStatus[tile].RecipeCompletedCycles >= loopcount;
                    cycleCompletedByRecipe = SequenceReads[SequenceRead].RecipeCompletedCycles >= loopcount;
                    waitForTile = !(tileAcquiredByRecipe || cycleCompletedByRecipe);
                }

                if (!waitForTile)
                    break;
                else
                    Thread.Sleep(2000);
            }

            if (tileAcquiredByRecipe || RecipeRunConfig.OLAProcessSimulationImages) // in a simulation mode we may not require that the recipe acquires _all_ tiles per cycle, so it's ok for the tileAcquiredByRecipe to be false
            {
                DirectoryNumber dn = new DirectoryNumber(tile, new DirectoryInfo(WorkingDir), loopcount);
                ImageProc.RunCycles(exeTypes, dn, imageConcurrency, usingMultipleExtractIntExes, baseCallRange, tileThreads);

                if (RecipeRunConfig.OLAUpdateCUIOnEveryTileBaseCall)
                {
                    if (exeTypes.Contains(ImageProcessingCMD.enImageprocessingExeType.eBaseCall) && ImageProc.BaseCallSucceededOnTile(tile, loopcount))
                    {
                        List<OLAResultsInfo> results = BuildResultsInfoList(tile, loopcount);
                        if (results.Count > 0)
                        {
                            OnOLAStatusUpdatedInvoke(new OLARunningEventArgs()
                            {
                                MessageType = OLARunningEventArgs.OLARunningMessageTypeEnum.General,
                                Message = $"Running {ModeName} on {SequenceRead} through cycle {loopcount}",
                                Results = results
                            });
                        }
                    }
                }
            }
            else if (cycleCompletedByRecipe) // if the whole cycle is acquired, but this tile isn't, it must be a bad tile
            {
                ImageProc.SetFailedTile(tile);
                Logger.LogError($"Cycle {loopcount} is acquired, but tile {tile} is not acquired", SeqLogFlagEnum.OLAERROR);
            }

            // Save progress
            ImageProc.SerializeTileProcessingProgress();

            if (ImageProc.IsBadTile(tile))
            {
                lock (FailedTiles)
                {
                    FailedTiles.Add(tile);
                }
            }
        }

        protected List<OLAResultsInfo> BuildResultsInfoList(List<string> tiles, int loopcount)
        {
            List<string> all_base_called_tiles = ImageProc.GetAllTilesWithBaseCallExecuted(loopcount);
            List<string> current_base_called_tiles = all_base_called_tiles.Intersect(tiles).ToList();

            List<OLAResultsInfo> results = new List<OLAResultsInfo>();
            string bcqcPath = Path.Combine(BaseWorkingDir, "bcqc", SequenceRead.ToString());
            foreach (string fc in current_base_called_tiles)
            {
                OLAResultsInfo info = new OLAResultsInfo()
                {
                    Read = SequenceRead.ToString(),
                    FileLocationPath = bcqcPath,
                    TileName = fc,
                    Cycle = loopcount,
                    DataFileName = $"1_{loopcount}_proc-int-bcqc.csv",
                };

                results.Add(info);
            }

            return results;
        }

        protected List<OLAResultsInfo> BuildResultsInfoList(string tile, int loopcount)
        {
            string bcqcPath = Path.Combine(BaseWorkingDir, "bcqc", SequenceRead.ToString());

            List<OLAResultsInfo> results = new List<OLAResultsInfo>();

            OLAResultsInfo info = new OLAResultsInfo()
            {
                Read = SequenceRead.ToString(),
                FileLocationPath = bcqcPath,
                TileName = tile,
                Cycle = loopcount,
                DataFileName = $"1_{loopcount}_proc-int-bcqc.csv",
            };

            results.Add(info);

            return results;
        }

        protected List<OLAResultsInfo> BuildResultsInfoList(int loopcount)
        {
            List<string> tiles = TileList.Select(o => o.Name).ToList();

            return BuildResultsInfoList(tiles, loopcount);
        }

        protected void ResetTileList()
        {
            TileList.Clear();
        }

        // Build tile list from the sequence info
        protected void BuildTileList()
        {
            //List<char> surfaces = new List<char>();
            //surfaces.Add('t');
            //surfaces.Add('b');

            //string tile;
            //foreach (int lane in SeqInfo.Lanes)
            //{
            //    for (int row = 0; row < SeqInfo.Rows; row++)
            //    {
            //        char rowLetter = (char)((int)'A' + row);

            //        if (row % 2 == 0)
            //        {
            //            for (int col = 1; col <= SeqInfo.Columns; col++)
            //            {
            //                foreach (char surface in surfaces)
            //                {
            //                    tile = $"{surface}L{lane}{col.ToString("D2")}{rowLetter}";

            //                    TileList.Add(new OLATile(tile));
            //                }
            //            }
            //        }
            //        else  // reverse the order of columns
            //        {
            //            for (int col = SeqInfo.Columns; col > 0; col--)
            //            {
            //                foreach (char surface in surfaces)
            //                {
            //                    tile = $"{surface}L{lane}{col.ToString("D2")}{rowLetter}";

            //                    TileList.Add(new OLATile(tile));
            //                }
            //            }
            //        }
            //    }
            //}

            //List<string> temp = TileList.Select(o => o.Name).ToList();
            TileList= BuildTileList(SeqInfo);
        }

        public static List<OLATile> BuildTileList(OLASequenceInfo seqInfo)
        {
            List<char> surfaces = new List<char>();
            surfaces.Add('t');
            surfaces.Add('b');
            List<OLATile> tileList = new List<OLATile>();
            string tile;
            foreach (int lane in seqInfo.Lanes)
            {
                for (int row = 0; row < seqInfo.Rows; row++)
                {
                    char rowLetter = (char)((int)'A' + row);

                    if (row % 2 == 0)
                    {
                        for (int col = 1; col <= seqInfo.Columns; col++)
                        {
                            foreach (char surface in surfaces)
                            {
                                tile = $"{surface}L{lane}{col.ToString("D2")}{rowLetter}";

                                tileList.Add(new OLATile(tile));
                            }
                        }
                    }
                    else  // reverse the order of columns
                    {
                        for (int col = seqInfo.Columns; col > 0; col--)
                        {
                            foreach (char surface in surfaces)
                            {
                                tile = $"{surface}L{lane}{col.ToString("D2")}{rowLetter}";

                                tileList.Add(new OLATile(tile));
                            }
                        }
                    }
                }
            }

            return tileList;
        }
        
        // Build tile list from the image data directory
        public static List<OLATile> BuildTileList(string baseDataDir, List<string> skipTiles=null, string seqType= "Read1")
        {
            DirectoryInfo diImageDataDir = new DirectoryInfo(Path.Combine(baseDataDir, seqType, "Data"));
            if (!diImageDataDir.Exists)
            {
                return new List<OLATile>();
            }

            List<string> tileItems = new List<string>();
            string[] imageNameItems;

            var colorList = new List<string> { "G1", "G2", "R3", "R4" };
            foreach (FileInfo fi in diImageDataDir.GetFiles("*.tif", SearchOption.TopDirectoryOnly))
            {
                imageNameItems = fi.Name.Split('_');
                for (int i = 0; i < imageNameItems.Length; i++)
                {
                    // The tile name item must follow the image color item (G1,G2,R3 or R4). So if the previous item was a color, the current item must be a tile name 
                    if (i > 0 && colorList.Contains(imageNameItems[i-1]))
                    {
                        string ti = imageNameItems[i];
                        if (ti.EndsWith("mm"))
                            ti = ti.Remove(ti.Length - 2);
                        
                        if (!tileItems.Contains(ti) && (skipTiles == null || !skipTiles.Contains(ti)))
                            tileItems.Add(ti);
                        
                        break;
                    }
                }
            }

            return tileItems.Select(o=>new OLATile(o)).ToList();
        }

        protected int CalculateTotalNumberOfCycles(string imageDataDir)
        {
            int total_cycles = 0;
            DirectoryInfo diImageDataDir = new DirectoryInfo(imageDataDir);
            var colorList = new List<string> { "G1", "G2", "R3", "R4" };
            foreach (FileInfo fi in diImageDataDir.GetFiles("*.tif", SearchOption.TopDirectoryOnly))
            {
                string[] imageNameItems = fi.Name.Split('_');
                for (int i = 0; i < imageNameItems.Length; i++)
                {
                    if (imageNameItems[i].StartsWith("Inc") && (i + 1) < imageNameItems.Length && -1 != colorList.IndexOf(imageNameItems[i + 1])) // cycle tag precedes image color (G1,G2,R3 or R4)
                    {
                        string cycle_tag = imageNameItems[i];
                        cycle_tag = cycle_tag.Substring(3); // skip "Inc"
                        int cycle = int.Parse(cycle_tag);
                        if (cycle > total_cycles)
                            total_cycles = cycle;
                        break;
                    }
                }
            }

            return total_cycles;
        }

        //private string CurrentThreadName
        //{
        //    get
        //    {
        //        string name = Thread.CurrentThread.Name;
        //        if (string.IsNullOrEmpty(name))
        //        {
        //            name = "ID" + Thread.CurrentThread.ManagedThreadId;
        //        }
        //        return name;
        //    }
        //}

        private void LogMessage(string msg, SeqLogMessageTypeEnum msgType = SeqLogMessageTypeEnum.INFO) => Logger.LogMessage(msg, msgType);

        void CleanProcessingResults(string recipeWorkingDir)
        {
            // Delete all proc directories
            DirectoryInfo di = new DirectoryInfo(recipeWorkingDir);
            foreach (DirectoryInfo fi in di.GetDirectories("proc-*", SearchOption.TopDirectoryOnly))
                Directory.Delete(fi.FullName, true);

            // Delete bcqc directory
            di = new DirectoryInfo(Path.Combine(recipeWorkingDir, "bcqc"));
            if (di.Exists)
                di.Delete(true);

            // Delete fastq directory
            di = new DirectoryInfo(Path.Combine(recipeWorkingDir, "fastq"));
            if (di.Exists)
                di.Delete(true);

            // Delete progress file
            FileInfo progressFile = new FileInfo(System.IO.Path.Combine(recipeWorkingDir, ImageProcessingCMD.ProgressFileName));
            if (progressFile.Exists)
                progressFile.Delete();

            // Delete log file
            FileInfo logFile = new FileInfo(System.IO.Path.Combine(recipeWorkingDir, ImageProcessingCMD.LogFileName));
            if (logFile.Exists)
                logFile.Delete();
        }

        public static void SerializeSequenceInfo(OLASequenceInfo sequenceInfo, ISeqFileLog logger = null, string destDir = "")
        {
            try
            {
                DirectoryInfo workingDirectoryInfo;
                if (!string.IsNullOrEmpty(destDir))
                {
                    workingDirectoryInfo = new DirectoryInfo(destDir);
                    if (!workingDirectoryInfo.Exists)
                    {
                        workingDirectoryInfo.Create();
                    }
                }
                else
                {
                    string workingDirectoryName = sequenceInfo.ExpName + '_' + sequenceInfo.SessionId;
                    string workingDirectoryPath = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.GetRecipeRunWorkingDir(workingDirectoryName);
                    workingDirectoryInfo = new DirectoryInfo(workingDirectoryPath);
                    workingDirectoryInfo.Create();
                }

                string infoFilePath = System.IO.Path.Combine(workingDirectoryInfo.FullName, InfoFileName);

                SettingJsonManipulater jsonManipulator = new SettingJsonManipulater();
                Task task = jsonManipulator.SaveSettingsToFile(sequenceInfo, infoFilePath);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Failed to SerializeSequenceInfo with error: {ex.Message}");
            }
        }
        public static OLASequenceInfo DeserializeSequenceInfo(string infoFilePath)
        {
            OLASequenceInfo seqInfo = new OLASequenceInfo();
            SettingJsonManipulater jsonManipulator = new SettingJsonManipulater();
            Task<OLASequenceInfo> task = jsonManipulator.ReadSettingsFromFile<OLASequenceInfo>(infoFilePath);
            seqInfo = task.Result;
            return seqInfo;
        }

        public void CreateBcqcInfoFolder()
        {
            string bcqcDir = Path.Combine(BaseWorkingDir, "bcqc");
            if (!Directory.Exists(bcqcDir))
            {
                DirectoryInfo bcqcDirInfo = new DirectoryInfo(bcqcDir);
                bcqcDirInfo.Create();
            }
        }

        public static ImagingStep.SequenceRead? NextRead(ImagingStep.SequenceRead read)
        {
            switch (read)
            {
                case ImagingStep.SequenceRead.Read1:
                    return ImagingStep.SequenceRead.Index1;
                case ImagingStep.SequenceRead.Index1:
                    return ImagingStep.SequenceRead.Index2;
                case ImagingStep.SequenceRead.Index2:
                    return ImagingStep.SequenceRead.Read2;
                default:
                    return null;
            }
        }

        public bool IsLastRead(ImagingStep.SequenceRead read)
        {
            ImagingStep.SequenceRead? next = NextRead(read);

            return !(next.HasValue && SequenceReads.ContainsKey(next.Value));
        }

        public static ulong BitArrayToU64(BitArray ba)
        {
            var len = Math.Min(64, ba.Count);
            ulong n = 0;
            for (int i = 0; i < len; i++)
            {
                if (ba.Get(i))
                    n |= 1UL << i;
            }
            return n;
        }

        public static List<List<int>> SplitList(List<int> src, int chunkSize)
        {
            var list = new List<List<int>>();

            for (int i = 0; i < src.Count; i += chunkSize)
            {
                list.Add(src.GetRange(i, Math.Min(chunkSize, src.Count - i)));
            }

            return list;
        }

        public static List<List<int>> BuildChunkList(int total, int chunkCount)
        {
            List<List<int>> chunks = new List<List<int>>();

            int beg = 0;
            for (int i = 0; i < chunkCount; i++)
            {
                List<int> chunk = new List<int>();

                int j = beg;
                while (true)
                {
                    if (j >= total)
                        break;
                    chunk.Add(j);
                    j += chunkCount;
                }

                chunks.Add(chunk);
                beg += 1;
            }

            return chunks;
        }

        string GetOLAWorkingDirectory(string readName)
        {
            OLAWorkingDirInfo helper = new OLAWorkingDirInfo();
            return Path.Combine(BaseWorkingDir, readName, helper.OLAFolderName);
        }
    }

    /*
    public class RecipeHelper
    {
        public RecipeRunThreadBase Recipe { get; set; } = null;
        public RecipeHelper(RecipeRunThreadBase recipe)
        {
            Recipe = recipe;
        }

        // Note, this method works only for the imaging type of recipe.
        public int GetTotalImagingCycles()
        {
            // How many imaging cycles does this recipe have?
            int this_recipe_imaging_cycles = GetTotalImagingCycles(Recipe.Recipe.Steps);

            // How many times is this recipe called by a parent recipe?
            int this_recipe_runs = Recipe.OutterRecipeThread != null ? GetTotalRunRecipeCycles(Recipe.OutterRecipeThread.Recipe.Steps, Recipe.Recipe.RecipeFileLocation) : 0;

            if (this_recipe_runs > 0)
                return this_recipe_imaging_cycles * this_recipe_runs;
            else
                return this_recipe_imaging_cycles;
        }

        public static int GetTotalImagingCycles(List<StepsTree> steps)
        {
            int cycles = 0;
            foreach (StepsTree step in steps)
            {
                if (step.Step.StepType == RecipeStepTypes.Imaging)
                    cycles += 1;
                else if (step.Step.StepType == RecipeStepTypes.Loop)
                {
                    LoopStep loopStep = step.Step as LoopStep;
                    cycles += loopStep.LoopCycles * GetTotalImagingCycles(step.Children);
                }
            }
            return cycles;
        }

        public static int GetTotalRunRecipeCycles(List<StepsTree> steps, string recipePath)
        {
            int cycles = 0;
            foreach (StepsTree step in steps)
            {
                if (step.Step.StepType == RecipeStepTypes.RunRecipe)
                {
                    RunRecipeStep runRecipeStep = step.Step as RunRecipeStep;
                    if (runRecipeStep.RecipePath == recipePath)
                        cycles += 1;
                }
                else if (step.Step.StepType == RecipeStepTypes.Loop)
                {
                    LoopStep loopStep = step.Step as LoopStep;
                    cycles += loopStep.LoopCycles * GetTotalRunRecipeCycles(step.Children, recipePath);
                }
            }
            return cycles;
        }
    }
    */

    public class OLAResultsInfo
    {
        public string Read { get; set; }
        public string FileLocationPath { get; set; }
        public string TileName { get; set; }
        public int Cycle { get; set; }
        public string DataFileName { get; set; }
    }

    class TileNameComparer
    {
        public TileNameComparer()
        {
        }

        public int Compare(string x, string y)
        {
            OLATile t1 = new OLATile(x);
            OLATile t2 = new OLATile(y);

            if (t1.Surface == "b")
                return -1;
            else if (t2.Surface == "b")
                return 1;

            if (t1.Lane < t2.Lane)
                return -1;
            else if (t1.Lane > t2.Lane)
                return 1;

            if (t1.Column < t2.Column)
                return -1;
            else if (t1.Column > t2.Column)
                return 1;

            return String.Compare(t1.Row, t2.Row);
        }
    }
}
