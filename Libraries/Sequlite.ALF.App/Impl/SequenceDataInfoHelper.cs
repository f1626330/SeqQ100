using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    class SequenceDataInfoHelper
    {
        IAppMessage _IAppMessage;
        Dictionary<SequenceDataTypeEnum, Dictionary<string, TileOLAInfo>> OLATileList = new Dictionary<SequenceDataTypeEnum, Dictionary<string, TileOLAInfo>>();
        int[] _OLATileListLock = new int[0];
        public SequenceDataInfoHelper(IAppMessage seqAppMessage)
        {
            _IAppMessage = seqAppMessage;
        }

        public SequenceOLADataInfo SequenceOLAData(SequenceDataTypeEnum sequenceDataType)
        {

            SequenceOLADataInfo seqOLAData = null;

            lock (_OLATileListLock)
            {
                if (OLATileList.ContainsKey(sequenceDataType))
                {
                    seqOLAData = new SequenceOLADataInfo()
                    {
                        OLATileData = new List<TileOLAInfo>(OLATileList[sequenceDataType].Values),
                        SequenceDataType = sequenceDataType
                    };
                }
            }
            return seqOLAData;
        }

        public void SendSequnceOLAResults(List<OLAResultsInfo> results, SequenceDataTypeEnum sequenceDataType, bool isCycleFinished)
        {
            if (results != null)
            {
                lock (_OLATileListLock)
                {
                    Dictionary<string, TileOLAInfo> tileList;
                    if (!OLATileList.ContainsKey(sequenceDataType))
                    {
                        tileList = new Dictionary<string, TileOLAInfo>();
                        OLATileList.Add(sequenceDataType, tileList);
                    }
                    else
                    {
                        tileList = OLATileList[sequenceDataType];
                    }
                    foreach (var it in results)
                    {
                        if (tileList.ContainsKey(it.TileName))
                        {
                            TileOLAInfo info = tileList[it.TileName];
                            info.Cycle = it.Cycle;
                            info.DataFileName = it.DataFileName;
                            info.FileLocationPath = Path.Combine(it.FileLocationPath, it.TileName);
                            info.TileName = it.TileName;
                            info.SequenceDataType = sequenceDataType;
                            info.IsCycleFinished = isCycleFinished;
                        }
                        else
                        {
                            tileList.Add(it.TileName, new TileOLAInfo()
                            {
                                Cycle = it.Cycle,
                                DataFileName = it.DataFileName,
                                FileLocationPath = Path.Combine(it.FileLocationPath, it.TileName),
                                TileName = it.TileName,
                                SequenceDataType = sequenceDataType,
                                IsCycleFinished = isCycleFinished,
                            });
                        }
                    }
                }//lock
                
                string jsonString = System.Text.Json.JsonSerializer.Serialize(results);
                //string jsonString = System.Text.Json.JsonSerializer.Serialize(OLATileList);
                _IAppMessage.UpdateAppMessage(new AppSequenceStatusOLA() { Message = jsonString, OLAStatus = ProgressTypeEnum.InProgress, 
                    IsOLAResultsUpdated = true, SequenceDataType = sequenceDataType },
                AppMessageTypeEnum.Status);
            }
        }

        
    }
}
