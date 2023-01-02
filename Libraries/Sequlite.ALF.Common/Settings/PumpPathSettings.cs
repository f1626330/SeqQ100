using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public class PumpPathSettings
    {
        
        public int valve2 { get; set; }
        public int valve3 { get; set; }
        public string pumpvalve { get; set; }
        public PumpPathSettings() { }
    }
}
