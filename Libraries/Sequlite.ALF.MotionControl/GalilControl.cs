using System.Collections.Generic;

namespace Sequlite.ALF.MotionControl
{
    public enum GalilCommandSet
    {
        HaltThread,
        Stop,
        ServoHere,
        Speed,
        Home,
        Begin,
        Acceleration,
        Deceleration,
        PositionAbsolute,
        PositionRelative,
        SwitchDeceleration,
    }
    public class GalilControl
    {
        #region Private Fields
        //public Galil.Galil GalilInterface { get; } = new Galil.Galil();
        //private bool _IsConnected = false;
        #endregion Private Fields

        #region Galil Commands defination
        public static Dictionary<GalilCommandSet, string> Commands { get; }
        static GalilControl()
        {
            Commands = new Dictionary<GalilCommandSet, string>();
            Commands.Add(GalilCommandSet.HaltThread, "HX");
            Commands.Add(GalilCommandSet.Stop, "ST");
            Commands.Add(GalilCommandSet.ServoHere, "SH");
            Commands.Add(GalilCommandSet.Speed, "SP");
            Commands.Add(GalilCommandSet.Home, "HM");
            Commands.Add(GalilCommandSet.Begin, "BG");
            Commands.Add(GalilCommandSet.Acceleration, "AC");
            Commands.Add(GalilCommandSet.Deceleration, "DC");
            Commands.Add(GalilCommandSet.PositionAbsolute, "PA");
            Commands.Add(GalilCommandSet.PositionRelative, "PR");
            Commands.Add(GalilCommandSet.SwitchDeceleration, "SD");
        }
        #endregion Galil Commands defination

        //#region Public Functions
        //public bool Connect()
        //{
        //    if(_IsConnected == true)
        //    {
        //        return _IsConnected;
        //    }
        //    var portList = GalilInterface.addresses();
        //    if (portList != null)
        //    {
        //        for (int i = 0; i < portList.Length; i++)
        //        {
        //            try
        //            {
        //                GalilInterface.address = portList[i] + " " + "115200";   // this would trigger the api to connect the control board
        //                //GalilInterface.address="COM5" + " " + "115200";   // this would trigger the api to connect the control board
        //                _IsConnected = true;
        //                break;
        //            }
        //            catch   // it throws exception if failed to connect to the board
        //            {

        //            }
        //        }
        //    }
        //    return _IsConnected;
        //}

        //public bool SendCommand(string cmd)
        //{
        //    try
        //    {
        //        GalilInterface.command(cmd);
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}
        //#endregion Public Functions
    }
}
