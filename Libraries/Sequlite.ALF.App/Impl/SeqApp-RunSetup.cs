using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    class SeqAppRunSetup : IRunSetup
    {
        SeqApp SeqApp { get; }

        
        public SeqAppRunSetup(SeqApp seqApp)
        {
            SeqApp = seqApp;
        }
        public bool RunSetup()
        {
            throw new NotImplementedException();
        }

        public bool ValidateSampleSheetData(SampleSheetDataInfo sData, out string error)
        {
            error = "";
            List<OLALaneSampleIndexInfo> olaLaneSampleIndexInfo = PrepareOLALaneSampleIndexInfo(sData);
            bool b;
            if (olaLaneSampleIndexInfo != null)
            {
                b = ValidateOLALaneSampleIndexInfo(olaLaneSampleIndexInfo, out error);
            }
            else
            {
                error = "Empty Lane sample Index Info";
                b = false;
            }
            return b;
        }


        public static List<OLALaneSampleIndexInfo> PrepareOLALaneSampleIndexInfo(SampleSheetDataInfo sData)
        {
            List<OLALaneSampleIndexInfo> list = null;
            int IdForOLAIndexInfo = 0;
            if (sData?.SampleLaneDataInfos != null)
            {
                Dictionary<int, OLALaneSampleIndexInfo> laneSampleList = new Dictionary<int, OLALaneSampleIndexInfo>();
                foreach (var it in sData.SampleLaneDataInfos)
                {
                    OLALaneSampleIndexInfo laneInfo;
                    if (laneSampleList.ContainsKey(it.LaneNumber))
                    {
                        laneInfo = laneSampleList[it.LaneNumber];
                    }
                    else
                    {
                        laneInfo = new OLALaneSampleIndexInfo(it.LaneNumber);
                        laneSampleList.Add(it.LaneNumber, laneInfo);
                    }
                    //find if laneInfo contains an indexinfo with sample id and sample name
                    OLASampleIndexInfo sampleIndexInfo = laneInfo.GetOLASampleIndexInfo(it.SampleId, it.SampleName);
                    if (sampleIndexInfo == null)
                    {
                        sampleIndexInfo = new OLASampleIndexInfo(it.SampleId, it.SampleName);
                        laneInfo.AddSampleIndexInfo(sampleIndexInfo);
                    }
                    //sampleIndexInfo.AddIndexInfo(new OLAIndexInfo(OLAIndexInfo.INVALID_ID, it.IndexSequnce), true);
                    sampleIndexInfo.AddIndexInfo(new OLAIndexInfo(IdForOLAIndexInfo++, it.IndexSequnce), true);

                }
                list = new List<OLALaneSampleIndexInfo>(laneSampleList.Values);
            }

            return list;
        }

        public  static bool ValidateOLALaneSampleIndexInfo(List<OLALaneSampleIndexInfo> lanes, out string error)
        {
            error = "";
            List<string> indexSeqs = new List<string>();
            foreach (var lane in lanes)
            {
                foreach (var sample in lane.SampleInfo)
                {
                    foreach (var index in sample.IndexInfo)
                    {
                        if (!indexSeqs.Contains(index.Sequence))
                            indexSeqs.Add(index.Sequence);
                        else
                        {
                            error = $"Sequence \"{index.Sequence}\" is not unique.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
