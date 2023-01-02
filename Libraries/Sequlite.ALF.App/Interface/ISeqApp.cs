using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface ISeqApp : IAppMessage
    {
        string AppLogName { get; }


        ISystemInit GetSystemInitInterface();
        ISystemMonitor GetSystemMonitorInterface();
        ILoad CreateLoadInterface();
        ISystemCheck CreateSystemCheckInterface();
        IRunSetup CreateRunSetupInterface();
        ISequence CreateSequenceInterface(bool checkHardwareInitialized = true);
        IPostRun CreatePostRunInterface();
        bool IsSimulation { get; }
        IIDHistory IDHistory { get; set; }

    }
}
