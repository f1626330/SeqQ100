using Fasterflect;
using Omu.ValueInjecter;
using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{

    public class SampleLaneIndexDataInfo
    {
        public int LaneNumber { get; set; }
        public string SampleId { get; set; }
        public string SampleName { get; set; }

        public string IndexSequnce { get; set; }
        public string IndexId { get; set; }
        public SampleLaneIndexDataInfo() { }
        public SampleLaneIndexDataInfo(SampleLaneIndexDataInfo info)
        {
            this.InjectFrom(info.DeepClone());
        }

    }

    

    public class RunSeqenceParameters
    {
        public int Readlength { get; set; }
        public bool Paired { get; set; } //same as enabling read2
        public bool Index1Enable { get; set; }
        public int Index1Number { get; set; }
        public bool Index2Enable { get; set; }
        public int Index2Number { get; set; }
        public bool IsSimulation { get; set; }
        public bool IsEnablePP { get; set; }
        public bool IsEnableOLA { get; set; }
        public bool IsCG { get; set; }
        public double FocusedBottomPos { get; set; }
        public double FocusedTopPos { get; set; }
        public string UserEmail { get; set; }
        public string SessionId { get; set; }
        public string ExpName { get; set; }
        public TemplateOptions SeqTemp { get; set; }
        public TemplateOptions SeqIndexTemp { get; set; } = TemplateOptions.idx;
        public string SampleID { get; set; }
        public string FlowCellID { get; set; }
        public string ReagentID { get; set; }

        public SampleSheetDataInfo SampleSheetData { get; set; }
    }

    public class SequenceInfo : ICloneable
    {
        public SequenceInfo()
        {
            
        }
        public bool Paired { get; set; }
        public int Cycles { get; set; }
        public Tuple<int,int> IndexCycles  => new Tuple<int,int>(Index1Enabled?Index1Cycle:0, Index2Enabled?Index2Cycle:0);
        public int Index1Cycle {get;set;}
        public int Index2Cycle { get; set; }
        public int Read2Cycle => Paired ? Cycles : 0;
        public bool Index1Enabled { get; set; }
        public bool Index2Enabled { get; set; }
        public int Channels { get; set; }
        public int[] Lanes { get; set; }
        public int Rows { get; set; }
        public int Column { get; set; }
        public TemplateOptions Template { get; set; }
        public TemplateOptions IndexTemplate { get; set; } = TemplateOptions.idx;
        public string WorkingDir { get; set; }
       
        public string Instrument { get; set; } //model name
        public string InstrumentID { get; set; } //library version

        public string SampleID { get; set; }
        public string FlowCellID { get; set; }
        public string ReagentID { get; set; }
        public object Clone()
        {
            var clone = (SequenceInfo)this.MemberwiseClone();
            if (Lanes != null)
            {
                clone.Lanes = (int[])(Lanes.Clone());
            }
            return clone;
        }

       
    }

    public class SequenceDataTypeInfo
    {
        public SequenceDataTypeEnum SequenceDataType { get; set; }
        public string DataFolderName { get; set; }
        public int Cycles { get; set; }

        public static List<SequenceDataTypeInfo> GetListSequenceDataTypeInfos(SequenceInfo segInfo)
        {
            List<SequenceDataTypeInfo> listSequenceDataTypeInfos = new List<SequenceDataTypeInfo>();
            listSequenceDataTypeInfos.Add(new SequenceDataTypeInfo()
            {
                SequenceDataType = SequenceDataTypeEnum.Read1,
                DataFolderName = "Read1",
                Cycles = segInfo.Cycles
            });


            if (segInfo.Index1Enabled)
            {
                listSequenceDataTypeInfos.Add(new SequenceDataTypeInfo()
                {
                    SequenceDataType = SequenceDataTypeEnum.Index1,
                    DataFolderName = "Index1",
                    Cycles = segInfo.Index1Cycle
                });
            }

            if (segInfo.Index2Enabled)
            {
                listSequenceDataTypeInfos.Add(new SequenceDataTypeInfo()
                {
                    SequenceDataType = SequenceDataTypeEnum.Index2,
                    DataFolderName = "Index2",
                    Cycles = segInfo.Index2Cycle
                });
            }

            if (segInfo.Paired)
            {
                listSequenceDataTypeInfos.Add(new SequenceDataTypeInfo()
                {
                    SequenceDataType = SequenceDataTypeEnum.Read2,
                    DataFolderName = "Read2",
                    Cycles = segInfo.Cycles
                });
            }
            return listSequenceDataTypeInfos;
        }
    }

    public class TileOLAInfo
    {
        public string FileLocationPath { get; set; }
        public string TileName { get; set; }
        public int Cycle { get; set; }
        public string DataFileName { get; set; }
        public SequenceDataTypeEnum SequenceDataType { get; set; }
        public bool IsCycleFinished {get;set;}
    }

    public class SeqTile
    {
        public string Name { get; } = ""; // e.g. bL102A
        public string Surface { get; } = ""; // b or t
        public int Lane { get; } = -1; // 1,2,3, or 4
        public int Column { get;  } = 0; // "00" - "44"
        public string Row { get; } = ""; // A,B,C, or D
        public SeqTile(string name)
        {
            //string _tilePattern = @"((b|t)L(1|2|3|4)(\d{2})(A|B|C|D))";
            string tilePattern = @"^(?<Surface>[bt])L(?<Lane>\d)(?<Column>\d{2})(?<Row>[ABCD])$";
            Match m = Regex.Match(name, tilePattern);
            if (m.Success)
            {
                string temp = m.Groups["Surface"].Value;
                Surface = temp;

                temp = m.Groups["Lane"].Value;
                int n;
                if (int.TryParse(temp, out n))
                {
                    Lane = n;
                }

                temp = m.Groups["Column"].Value;
                if (int.TryParse(temp, out n))
                {
                    Column = n;
                }

                temp = m.Groups["Row"].Value;
                Row = temp;
            }
            Name = name;
        }
        public override bool Equals(object obj) => this.Equals(obj as SeqTile);

        public bool Equals(SeqTile p)
        {
            if (p is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != p.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return (Name == p.Name);
        }

        public override int GetHashCode() => Name.GetHashCode();

        public static bool operator ==(SeqTile lhs, SeqTile rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator !=(SeqTile lhs, SeqTile rhs) => !(lhs == rhs);
    }


        public class SequenceOLADataInfo
    {
        public SequenceDataTypeEnum SequenceDataType { get; set; }
        public List<TileOLAInfo> OLATileData { get; set; }
    }

    public class RunOfflineDataSeqenceParameters
    {
        public string DataInfoFile { get; set; }
    }

    public class RunOfflineImageDataSeqenceParameters
    {
        public string ExpName { get; set; }  //post processing
        public SequenceInfo SeqInfo { get; set; }
        public string SessionId { get; set; }
        public string WorkingDir { get; set; } //output root dir
        public string ImageDataDir { get; set; }

        public string DataInfoFilePath { get; set; }
        public bool UsingPreviousWorkingDir { get; set; }
        public bool UseSlidingWindow { get; set; }
        public List<string> Tiles { get; set; }
    }

    public enum SequenceDataTypeEnum
    {

        [Display(Name = "None", Description = "None")]
        None,

        [Display(Name = "Read1", Description = "Read1")]
        Read1,

        [Display(Name = "Read2", Description = "Read2")]
        Read2,

        [Display(Name = "Index1", Description = "Index1")]
        Index1,

        [Display(Name = "Index2", Description = "Index2")]
        Index2,
    }
    public interface ISequence
    {
        bool IsAbort { get; set; }
        bool Sequence(RunSeqenceParameters seqParams);
        bool OfflineImageDataSequence(RunOfflineImageDataSeqenceParameters seqParams);
        bool StopSequence();
        bool Sequencerunning { get; }
        bool IsOLADone { get; }
        SequenceInfo SequenceInformation { get; }
        SequenceOLADataInfo SequenceOLAData(SequenceDataTypeEnum sequenceDataType);

        bool OfflineDataDisplaySequence(RunOfflineDataSeqenceParameters seqParams);
        SequenceInfo GetOfflineDataSequenceInfo(string dataInfoFile);
        Dictionary<SequenceDataTypeEnum, List<SeqTile>> GetOfflineTileList(string baseDataDir, SequenceInfo segInfo);
        List<SeqTile> GetTileList(SequenceInfo seqInfo);
    }

   
}
