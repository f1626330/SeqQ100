using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public class StatusBase
    {
        public string Name { get; }
        public object Value { get; }

        public StatusBase(string _Name, object _Value)
        {
            Name = _Name;
            Value = _Value;
        }
    }

    public class TemperatureStatusBase
    {
    }

    public class TemperatureStatus : TemperatureStatusBase
    {
       
        public TemperatureStatus() { }
        public double ChemiTemper { get; set; }
        public double HeatSinkTemper { get; set; }
        public double PreHeatTemper { get; set; }
        public double CoolerTemper { get; set; }
        public double AmbientTemper { get; set; }
    }

    public class ChillerTemperatureStatus : TemperatureStatusBase
    {
        public double ChillerTemperature { get; set; }
        public bool IsChillerTempReady { get; set; }
        public bool HasError { get; set; }
    }
}
