using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public class FluidicsFlowSettings
    {
        public Dictionary<string, double> PumpSetting { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> ChemiSetting { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> DefaultPumping { get; set; } = new Dictionary<string, double>();
        public Dictionary<PathOptions, PumpPathSettings> DefaultPumpPath { get; set; } = new Dictionary<PathOptions, PumpPathSettings>();
        public Dictionary<string, double> DefaultChemistry { get; set; } = new Dictionary<string, double>();
        public FluidicsFlowSettings() { }
    }
}
