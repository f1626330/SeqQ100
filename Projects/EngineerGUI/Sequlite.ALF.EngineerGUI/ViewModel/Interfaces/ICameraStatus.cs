using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public interface ICameraStatus
    {
        DateTime CameraCaptureStartTime { get; set; }
        string CameraCapturingStatus{get; set;}
        double EstimatedCaptureTime { get; set; }
    }
}
