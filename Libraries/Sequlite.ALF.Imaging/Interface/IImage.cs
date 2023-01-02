using Sequlite.ALF.Common;
using Sequlite.CameraLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Sequlite.ALF.Imaging
{
    public interface IImage
    {
        ICamera Camera1 { get; set; }
        ICamera Camera2 { get; set; }
        void Autofocus(Dispatcher callingDisptcher, AutoFocusSettings settings, bool isMachineRev2, ICamera camera);
        void AutofocusScan(Dispatcher callingDisptcher, AutoFocusSettings settings, bool isMachineRev2, ICamera camera);
        void CalculateOffset(Dispatcher callingDisptcher, AutoFocusSettings settings, double fiducialpos, bool isMachineRev2, ICamera camera);
        void CaptureImageAync(Dispatcher callingDisptcher, ImageChannelSettings imagechannel, Int32Rect roiRect, bool isMachineRev2, ICamera camera);
        void LiveStreaming(Dispatcher callingDisptcher, ImageChannelSettings imagechannel, Int32Rect roiRect, bool isMachineRev2, ICamera camera);
    }
}
