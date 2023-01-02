using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public class FCTemperCtrlSettings
    {
        public double CtrlP { get; set; }
        public double CtrlI { get; set; }
        public double CtrlD { get; set; }
        public double HeatGain { get; set; }
        public double CoolGain { get; set; }
    }
}
