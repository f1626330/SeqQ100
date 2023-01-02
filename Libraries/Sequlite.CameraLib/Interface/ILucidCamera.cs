using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Sequlite.CameraLib
{
    public delegate void TriggerStartHandle();
    public delegate void ExposureEndHandle(bool isExpoSuccess);
    public class CameraNotifArgs
    {
        public int ImageId {get;}
        public byte[] ImageRef { get; }
        public CameraNotifArgs(int imgId, byte[] imgDataRef)
        {
            ImageId = imgId;
            ImageRef = imgDataRef;
        }
    }
    public interface ILucidCamera : ICamera
    {
        event TriggerStartHandle OnTriggerStartRequested;
        event ExposureEndHandle ExposureEndNotif;
        bool SwitchExpoEvent(bool OnOrOff);
        bool IsRecipeImaging { get; set; }
        
        bool EnableTriggerMode { get; set; }
        bool IsTriggerFromOutside { get; set; }
        bool ContinuousModeStarted { get; }
        bool TriggeredByOutside { get; set; }
        int ImageId { set; }
        bool WaitTriggerArmed();
        bool SetExposure(double expInMsec);
        string SerialNumber {get;}
        
    }
}
