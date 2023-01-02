using Sequlite.ALF.Fluidics;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface ISystemInit
    {
        bool Initialize(bool startMonitorService);
        bool Unintialize();
        bool Initialized { get; }
        Mainboard MainboardDevice { get; }
        MainBoardController MainBoardController { get; }
        MotionController MotionController { get; }
        PhotometricsCamera PhotometricsCamera { get; }
        ILucidCamera EthernetCameraA { get; }
        ILucidCamera EthernetCameraB { get; }
        IFluidics FluidicsInterface { get; }
        LEDController LEDController { get; }
        bool IsMachineRev2 { get; }
       
    }
}
