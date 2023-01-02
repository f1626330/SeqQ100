using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public class SampleSheetDataInfo
    {
        private List<SampleLaneIndexDataInfo> _SampleLaneDataInfos = new List<SampleLaneIndexDataInfo>();

        public int Reads { get; set; }
        public int Index { get; set; }
        public string ExpName { get; set; }
        public string Description { get; set; }

        public int GetIndexLengthFromSampleSheet()
        {

            if (_SampleLaneDataInfos?.Count > 0)
            {
                return _SampleLaneDataInfos[0].IndexSequnce.Length;
            }
            else
            {
                return 0;
            }

        }

        public void AddSampleLaneDataInfo(SampleLaneIndexDataInfo info)
        {
            _SampleLaneDataInfos.Add(info);
        }

        public List<SampleLaneIndexDataInfo> SampleLaneDataInfos => _SampleLaneDataInfos.Select(v => new SampleLaneIndexDataInfo(v)).ToList();
    }
}
