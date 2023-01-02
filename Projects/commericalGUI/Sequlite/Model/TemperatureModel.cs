using Sequlite.ALF.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class TemperatureModel : ModelBase
    {

        
        double _ChemiTemper;
        public double ChemiTemper { get => _ChemiTemper; set => SetProperty(ref _ChemiTemper, value); }
        double _HeatSinkTemper;
        public double HeatSinkTemper { get => _HeatSinkTemper; set => SetProperty(ref _HeatSinkTemper, value); }
        double _CoolerTemper;
        public double CoolerTemper { get => _CoolerTemper; set => SetProperty(ref _CoolerTemper, value); }
        double _AmbientTemper;
        public double AmbientTemper { get => _AmbientTemper; set => SetProperty(ref _AmbientTemper, value); }

        public void UpdateTemperarures(TemperatureStatus t)
        {
            if (t != null)
            {

                ChemiTemper = t.ChemiTemper;
                HeatSinkTemper = t.HeatSinkTemper;
                CoolerTemper = t.CoolerTemper;
                AmbientTemper = t.AmbientTemper;
            }
        }
    }
}
