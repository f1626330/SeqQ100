using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //json
    public class LoggerSettings
    {
        public LoggerSettings()
        {
            FilterOutFlags = SeqLogFlagEnum.NONE;
        }
        public SeqLogFlagEnum FilterOutFlags { get; set; }

        public void AddFilterOutFlag(SeqLogFlagEnum flags)
        {
            FilterOutFlags |= flags;
        }
        public void RemoveFilterOutFlag(SeqLogFlagEnum flags)
        {
            FilterOutFlags &= ~flags;
        }
        public SeqLogFlagEnum OLAFilterOutFlags { get; set; }
    }
}
