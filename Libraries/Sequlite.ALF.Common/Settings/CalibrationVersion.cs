using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common.Settings
{
     /// <summary>
    /// Calibration Version
    /// </summary>
    public class CalibrationVersion
    {
        public string Date { get; set; }
        public int ID { get; set; }
        public string Product { get; set; }
        public CalibrationVersion() { }

    }
}