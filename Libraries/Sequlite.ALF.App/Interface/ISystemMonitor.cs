using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface ISystemMonitor
    {
        IObservable<TemperatureStatusBase> TemperatureMonitor { get; }
        IObservable<StatusBase> DeviceStatusMonitor { get; }
        void CheckTemperature();
        //bool IsChillerTempReady { get;}
        TemperatureStatus TemperatureData { get; }
    }
}
