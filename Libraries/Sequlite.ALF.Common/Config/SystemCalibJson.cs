using Sequlite.ALF.Common.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Sequlite.Image.Processing;

namespace Sequlite.ALF.Common
{
    public class SystemCalibJson
    {
        public struct ImageTransformConfig
        {
            public int WidthAdjust { get; set; } //< amount to scale the image on the x-axis
            public int HeightAdjust { get; set; } //< amount to scale the image on the y-axis
            public int XOffset { get; set; } //< amount to translate the image on the x-axis
            public int YOffset { get; set; } //< amount to translate the image on the y-axis
        }

        public CalibrationVersion Version { get; set; } = new CalibrationVersion();
        public AutoFocusSettings AutoFocusingSettings { get; set; } = new AutoFocusSettings();
        public InstrumentInfo InstrumentInfo { get; set; } = new InstrumentInfo();
        public CameraCalib CameraCalibSettings { get; set; } = new CameraCalib();
        public FluidicsCalib FluidicsCalibSettings { get; set; } = new FluidicsCalib();
        public Dictionary<string, double> StageRegions { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, ImageTransformConfig> ImageTransforms { get; set; }
        public string[] Errors { get; set; }
    }
}
