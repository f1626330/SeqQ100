using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public class LEDSetting
    {
        public LEDTypes Type { get; set; }
        public SettingRange Range { get; set; }
        public int MaxOnTime { get; set; }
        public LEDSetting() { }
    }
}
