using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class FluidControllerStatus
    {
        public double AmbientTemper { get;  set; }
        /// <summary>
        /// unit in mbar
        /// </summary>
        public double Pressure { get;  set; }
        /// <summary>
        /// unit in mL/min
        /// </summary>
        public double FlowRate { get;  set; }
        public uint BufferLevel { get;  set; }
        public uint WasteLevel { get;  set; }
        public bool BubbleDetected { get;  set; }
        public uint Bubble { get;  set; }
        public bool BufferTrayIn { get;  set; }
        public bool SipperDown { get;  set; }
        public bool WasteIn { get;  set; }
    }
}
