using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //Json serialization
    public class SerialCommSettings
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public SerialCommSettings() { }
    }

    //Json serialization
    public enum SerialDeviceTypes
    {
        None,
        ZStage,
        MotionController,
        FCTemperController,
        MainboardController,
        Chiller,
        LEDController,
        FluidController,
        BarCodeReader,
        RFIDReader,
        ValveSelector,
        SmartValve2,
        SmartValve3,
        Pump,
    }
}
