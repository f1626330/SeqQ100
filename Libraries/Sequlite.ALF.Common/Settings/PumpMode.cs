using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class PumpMode
    {
        public string DisplayName { get; }
        public ModeOptions Mode { get; }
        public PumpMode() { }
        public PumpMode(string name, ModeOptions mode)
        {
            DisplayName = name;
            Mode = mode;
        }
    }
}
