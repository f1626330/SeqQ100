using Sequlite.ALF.SerialPeripherals;
using System.Collections.Generic;
using Sequlite.ALF.Common;


namespace Sequlite.ALF.Fluidics
{
    public static class FluidicsManager
    {

        private static Dictionary<FluidicsVersion, IFluidics> FluidicsInterfaces = new Dictionary<FluidicsVersion, IFluidics>();
        public static IFluidics GetFluidicsInterface(FluidicsVersion version)
        {
            IFluidics fluidicsInterface = null;
            lock (FluidicsInterfaces)
            {
                if (FluidicsInterfaces.ContainsKey(version))
                {
                    fluidicsInterface = FluidicsInterfaces[version];
                }
                else
                {
                    switch (version)
                    {
                        case FluidicsVersion.V1:
                            fluidicsInterface = new FluidicsV1();
                            FluidicsInterfaces[version] = fluidicsInterface;
                            break;
                        case FluidicsVersion.V2:
                            fluidicsInterface = new FluidicsV2();
                            FluidicsInterfaces[version] = fluidicsInterface;
                            break;
                    }
                }
                return fluidicsInterface;
            }

            //    #region Public Properties
            //    public static List<ValveSolution> Solutions { get; }
            //    public static List<PumpMode> Modes { get; }
            //    public static List<PathOptions> Paths { get; }

            //    public static PumpController Pump { get; }
            //    public static ValveController Valve { get; }

            //    public static TecanXMP6000Pump XMP6000Pump { get; }
            //    /// <summary>
            //    /// valve 2 has 6 positions.
            //    /// </summary>
            //    public static TecanSmartValve SmartValve2 { get; }
            //    /// <summary>
            //    /// valve 3 has 3 positions.
            //    /// </summary>
            //    public static TecanSmartValve SmartValve3 { get; }
            //    public static FluidController FluidController { get; }
            //    #endregion Public Properties

            //    #region Constructor
            //    static FluidicsManager()
            //    {
            //        Solutions = new List<ValveSolution>();
            //        for (int i = 0; i < 24; i++)
            //        {
            //            Solutions.Add(new ValveSolution() { DisplayName = string.Format("Solution {0}", i + 1), ValveNumber = i + 1, SolutionVol = 0 });
            //        }

            //        Modes = new List<PumpMode>();
            //        Modes.Add(new PumpMode("Asp.&Disp.", ModeOptions.AspirateDispense));
            //        Modes.Add(new PumpMode("Aspirate", ModeOptions.Aspirate));
            //        Modes.Add(new PumpMode("Dispense", ModeOptions.Dispense));
            //        Modes.Add(new PumpMode("Pull", ModeOptions.Pull));
            //        Modes.Add(new PumpMode("Push", ModeOptions.Push));
            //        Modes.Add(new PumpMode("Pull&Push", ModeOptions.PullPush));

            //        Paths = new List<PathOptions>();
            //        Paths.Add(PathOptions.FC);
            //        Paths.Add(PathOptions.Waste);
            //        Paths.Add(PathOptions.Bypass);

            //        Pump = new PumpController();
            //        Valve = new ValveController();

            //        XMP6000Pump = new TecanXMP6000Pump();
            //        SmartValve2 = new TecanSmartValve();
            //        SmartValve3 = new TecanSmartValve();
            //        FluidController = new FluidController();
            //    }
            //    #endregion Constructor
            //}
        }
    }
}
