using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common.Settings
{
    public class FluidicsCalib
    {
        public double FCTestPressureCalib { get; set; }
        public double ByPassPressureCalib { get; set; }
        public double PressureTole { get; set; }
        public double FlowRateTole { get; set; }
        public double FlowRateStdTole { get; set; }
        public double WashCartPos { get; set; }
        public double ReagentCartPos { get; set; }
        public double WasteCartridgeEmptyWeight { get; set; }

        public FluidicsCalib() { }
    }
}
