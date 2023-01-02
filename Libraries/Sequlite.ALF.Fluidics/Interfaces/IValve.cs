using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Fluidics
{
    public delegate void ValvePosUpdateHandle();
    public interface IValve
    {
        event ValvePosUpdateHandle OnPositionUpdated;
        bool IsConnected { get; }
        int ValvePos { get; }
        int CurrentPos { get; }
        bool Connect(string portName = "", int baudrate = 19200);
        bool SetToNewPos(int pos,bool waitForExecution);
        bool SetToNewPos(int pos, bool moveCCW, bool waitForExecution);
        bool GetCurrentPos();
        bool ResetValve();
        bool Initialize(bool isCCW = false, int initialPort = 1);
    }
}
