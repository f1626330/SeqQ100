using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public class HardwareCheckResults
    {
        public double FocusedBottomPos { get; set; }
        public  double FocusedTopPos { get; set; }
        public double DiskSpaceReq { get; set; }
        public long DiskSpaceEmp { get; set; }
        public double WasteLevel { get; set; }
        public HardwareCheckResults() { }
    }

    public interface ISystemCheck
    {
        bool DoorCheck();
        bool FluidicsCheck();
        bool TemperatureCheck();
        bool ImageSystemCheck();
        bool DiskSpaceCheck(int readlength);
        bool FlowCheck(int selectedsolution, List<PathOptions> testpath);
        bool FlowCheckAndPriming();
        bool CancelChecking();
        bool IsAbortCheck { get; set; }
        string SessionId { get; set; }

        HardwareCheckResults HardwareCheckResults { get; }
    }
}
