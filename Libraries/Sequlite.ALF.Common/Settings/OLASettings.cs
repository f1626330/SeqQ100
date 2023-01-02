using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //json serialization
    public class OLASettings
    {
        public OLASettings() { }
        public bool IsSimulation { get; set; }
        public string SimulationImagePath { get; set; }

    }
}
