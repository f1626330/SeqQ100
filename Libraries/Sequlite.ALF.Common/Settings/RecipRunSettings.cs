using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Sequlite.ALF.Common
{
    //add description for UI display
    public enum TemplateOptions
    {
        [Description("Hg38")]
        hg38,

        [Description("Ecoli")]
        ecoli,

        [Description("E8739")]
        e8739,

        [Description("M13")]
        m13,

        [Description("Scere")]
        scere,

        [Description("PhiX")]
        PhiX,

        [Description("Idx")]
        idx,

        [Description("Brca")]
        Brca,

        [Description("NIPT")]
        NIPT,
    }

    public static class TemplateOptionsHelper
    {
        static public bool IsIndexTemplate(TemplateOptions tmp)
        {
            return tmp == TemplateOptions.idx;
        }

        static public void GetTemplates(out List<TemplateOptions> templateOptions, out List<TemplateOptions> indexTemplateOptions)
        {
            templateOptions = new List<TemplateOptions>();
            indexTemplateOptions = new List<TemplateOptions>();
            foreach (TemplateOptions template in Enum.GetValues(typeof(TemplateOptions)))
            {
                if (TemplateOptionsHelper.IsIndexTemplate(template))
                {
                    indexTemplateOptions.Add(template);
                }
                else
                {
                    templateOptions.Add(template);
                }
            }
        }
    }

    public class OLABaseCallRange
    {
        public string Type
        {
            get;
            set;
        }

        public int Width
        {
            get;
            set;
        }

        public int Overlap
        {
            get;
            set;
        }

        public int Repeat
        {
            get;
            set;
        }

        public OLABaseCallRange()
        {
        }

        // copy constructor
        public OLABaseCallRange(OLABaseCallRange range)
        {
            this.Type       = range.Type;
            this.Width      = range.Width;
            this.Overlap    = range.Overlap;
            this.Repeat     = range.Repeat;
        }

        public OLABaseCallRange(string type, int width, int overlap, int repeat)
        {
            this.Type       = type;
            this.Width      = width;
            this.Overlap    = overlap;
            this.Repeat     = repeat;
        }
    }

    [Flags]
    public enum DataBackupOptionsEnum
    {
        NoBackup = 0,
        ImageBackup = 1,
        OLABackup = 2,
    }

    //json serialization
    public class RecipeRunSettings
    {
        public RecipeRunSettings() 
        {
            OLAProcessSimulationImages = false;
            UsingTPL = true;
            UsingTPLForExtractIntensitiesByCell = false;
            OLASingleExtractMultipleImagesByCell = false;
            OLASingleExtractMultipleImagesByCellWithJC = false;
            OLAUseJoinCycles = true;
            OLAJoinCyclesAtRunEnd = false;
            OLAMinimumCyclesToCreateTemplates = 5;
            OLAMinimumCyclesToCallBases = 25;
            OLABaseCallEveryNthCycle = 4;
            OLASlidingWindowMain = true;
            OLASlidingWindowIndex = true;
            OLASlidingWindowOverlapCycles = 5;
            OLAMinimumOutput = false;
            OLAUseDLL = false;
            OLAIndexCLR = false;
            OLAUpdateCUIOnEveryTileBaseCall = false;
            OLAPFClusterMinimumLength = 25;
            OLABaseCallOnlyPFClusters = false;
            OLASparseMappingOption = 0;
            OLASparseMappingCycles = 0;
            OLAFirstCycleToUseYngrams = 30;
            OLAUseYngramsWithStepCycles = 5;
            OLACatchHardwareExceptionsInCpp = true;
            OLABackupOffline = false;
            OLAOutOfRangeBaseCallAllowed = true;
            OLAUseScoresFromPreviousRangeWhenDefiningPFClusters = true;
            OLASmoothBCQC = true;
            OLASmoothBCQCIncludeOutOfRange = true;
            OLAMinimumTotalThreadCount = 1;
        }

        public string ImagingBaseDirSelection { get; set; }
        public string AcquiredImageSubDir { get; set; }
        public string AcquiredImageBackupLocation { get; set; }
        public bool UseSubFolderForDataBackup { get; set; }
        public string AnalysisTaskLocation { get; set; }
        public DataBackupOptionsEnum DataBackupOptions { get; set; }
        public int MaximumReadlength { get; set; }
        public int OLADataSizeMBPerTile { get; set; }
        public bool SaveSimulationImages { get; set; }
        public string OLADir { get; set; }
        public bool OLAProcessSimulationImages { get; set; }
        public bool UsingTPL { get; set; }
        public bool UsingTPLForExtractIntensitiesByCell { get; set; }
        public bool OLASingleExtractMultipleImagesByCell { get; set; }
        public bool OLASingleExtractMultipleImagesByCellWithJC { get; set; }
        public bool OLAUseJoinCycles { get; set; }
        public bool OLAJoinCyclesAtRunEnd { get; set; }
        public int OLAMinimumCyclesToCreateTemplates { get; set; }
        public int OLAMinimumCyclesToCallBases { get; set; }
        public int OLABaseCallEveryNthCycle { get; set; }
        public bool OLASlidingWindowMain { get; set; }
        public bool OLASlidingWindowIndex { get; set; }
        public int OLASlidingWindowOverlapCycles { get; set; }
        public bool OLAMinimumOutput { get; set; }
        public bool OLAUseDLL { get; set; }
        public bool OLAIndexCLR { get; set; }
        public bool OLAUpdateCUIOnEveryTileBaseCall { get; set; }
        public int OLAPFClusterMinimumLength { get; set; }
        public bool OLABaseCallOnlyPFClusters { get; set; }
        public int OLASparseMappingOption { get; set; }
        public int OLASparseMappingCycles { get; set; }
        public int OLAFirstCycleToUseYngrams { get; set; }
        public int OLAUseYngramsWithStepCycles { get; set; }
        public bool OLAOutOfRangeBaseCallAllowed { get; set; }
        public bool OLAUseScoresFromPreviousRangeWhenDefiningPFClusters { get; set; }
        public bool OLASmoothBCQC { get; set; }
        public bool OLASmoothBCQCIncludeOutOfRange { get; set; }
        public bool OLACatchHardwareExceptionsInCpp { get; set; }
        public bool OLABackupOffline { get; set; }
        public string OLASimulationImageBaseDir { get; set; }
        public string OLAParams_Main_nonHG { get; set; }
        public string OLAParams_Main_HG { get; set; }
        public string OLAParams_Index { get; set; }
        public int OLAMinimumTotalThreadCount { get; set; }

        public string RecipeRunLogSubDir { get; set; }
        public string GetRecipeRunImagingBaseDir()
        {
            StringBuilder folderBuilder = new StringBuilder();
            if (string.Compare(ImagingBaseDirSelection, BaseDirSelectionConstant.MYDOC, true) == 0)
            {
                folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                folderBuilder.Append(AcquiredImageSubDir);
                folderBuilder.Append("\\");
            }
            else if (string.Compare(ImagingBaseDirSelection, BaseDirSelectionConstant.PROGRAMDATA, true) == 0)
            {
                folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
                folderBuilder.Append(AcquiredImageSubDir);
                folderBuilder.Append("\\");
            }
            else
            {
                folderBuilder.Append(ImagingBaseDirSelection);
                folderBuilder.Append("\\");
                folderBuilder.Append(AcquiredImageSubDir);
                folderBuilder.Append("\\");
            }
            return folderBuilder.ToString(); 
        }
        public string GetRecipeRunLogBaseDir()
        {
            StringBuilder folderBuilder = new StringBuilder();
            if (string.Compare(ImagingBaseDirSelection, BaseDirSelectionConstant.MYDOC, true) == 0)
            {
                folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                folderBuilder.Append(RecipeRunLogSubDir);
                folderBuilder.Append("\\");
            }
            else if (string.Compare(ImagingBaseDirSelection, BaseDirSelectionConstant.PROGRAMDATA, true) == 0)
            {
                folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
                folderBuilder.Append(RecipeRunLogSubDir);
                folderBuilder.Append("\\");
            }
            else
            {
                folderBuilder.Append(ImagingBaseDirSelection);
                folderBuilder.Append("\\");
                folderBuilder.Append(RecipeRunLogSubDir);
                folderBuilder.Append("\\");
            }
            return folderBuilder.ToString(); 
        }
        public string GetRecipeRunWorkingDir(string expName) => GetRecipeRunImagingBaseDir() + expName;


        //public string GetRecipeRunBackupRootDir => AcquiredImageBackupLocation + "\\"
        //   + SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName;
        public string RecipeRunBackupSubDir => "Instrument";
        public string RecipeRunBackupRootDir  => AcquiredImageBackupLocation; 
        public string GetRecipeRunImageDataDir(string expName, string seqReadName, bool forAquisition=true, string baseOfflineDataDir=null)
        {
            string imageDataDir;

            if (forAquisition) // Directory for run-time data (simulation or not simulation) 
            {
                imageDataDir =  Path.Combine(GetRecipeRunWorkingDir(expName), seqReadName, "Data");
            }
            else // Directory for data processing - online (OLA) or offline
            {
                if (!String.IsNullOrEmpty(baseOfflineDataDir)) // Directory for offline data processing
                {
                    imageDataDir = Path.Combine(baseOfflineDataDir, seqReadName, "Data");
                }
                else if (OLAProcessSimulationImages) // Directory with some existing data for OLA to process during sequencing in either simulation or non-simulation mode
                {
                    Debug.Assert(!string.IsNullOrEmpty(OLASimulationImageBaseDir));
                    imageDataDir = Path.Combine(OLASimulationImageBaseDir, seqReadName, "Data");
                }
                else // Directory with data to be processed online (OLA)
                {
                    imageDataDir = Path.Combine(GetRecipeRunWorkingDir(expName), seqReadName, "Data");
                }
            }

            return imageDataDir;
        }

        public string GetRecipeRunBackupDir(string expName)
        {
            if (UseSubFolderForDataBackup)
            {
                return AcquiredImageBackupLocation + "\\" + expName + "\\" + RecipeRunBackupSubDir + "\\";
            }
            else
            {
                return AcquiredImageBackupLocation + "\\" + expName + "\\" ;
            }
        }

        public string GetRecipeRunImageBackupDir(string expName, string subfolder) => 
            GetRecipeRunBackupDir(expName) + subfolder + "\\Data" + "\\";
        
        public string GetOLABinDir(string recipeName)
        {
            if (string.IsNullOrEmpty(OLADir))
            {
                return GetRecipeRunWorkingDir(recipeName) + "\\bin";
            }
            else
            {
                return OLADir;
            }
        }
        [JsonIgnore] public bool BackupOLAData => (DataBackupOptions & DataBackupOptionsEnum.OLABackup) == DataBackupOptionsEnum.OLABackup;
        [JsonIgnore] public bool BackupImage => (DataBackupOptions & DataBackupOptionsEnum.ImageBackup) == DataBackupOptionsEnum.ImageBackup;
        [JsonIgnore] public bool BackupData => DataBackupOptions != DataBackupOptionsEnum.NoBackup;
    }
}
