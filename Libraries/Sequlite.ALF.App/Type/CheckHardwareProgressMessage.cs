using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public enum CheckHardwareProgressMessageEnum
    {
        Start,
        End,
        End_Error,
        Progress,
    }

    public enum CheckHardwareEnum
    {
        Door,
        Fluidics,
        Temperature,
        Imaging,
        Sensor,
        Flow,
        Disk,
    }

    public class CheckHardwareProgressMessage
    {
        public CheckHardwareProgressMessageEnum MessageType { get; set; }
        public float ProgressPercentage { get; set; }
        public CheckHardwareEnum CheckType { get; set; }
        public override string ToString()
        {
            return $"{Enum.GetName(typeof(CheckHardwareEnum), CheckType)}:{Enum.GetName(typeof(CheckHardwareProgressMessageEnum), MessageType)}: {ProgressPercentage}% Progresses";
        }
    }

}
