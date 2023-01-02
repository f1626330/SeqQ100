using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class SystemConfigJson
    {
        public bool EnableSaveAFImage { get; set; }
        public int Version { get; set; } = 0;
        public LoggerSettings LoggerConfig { get; set; } = new LoggerSettings();
        public SimulationSettings SimulationConfig { get; set; } = new SimulationSettings();
        public string ProcessorAffinity { get; set; } = string.Empty;
        public int MaxMemoryUsage_GB { get; set; } = 8;
        public int OLAMinimumTotalThreadCount { get; set; } = 1;
        public RecipeRunSettings RecipeRunConfig { get; set; } = new RecipeRunSettings();
        public ObservableCollection<BinningFactorType> BinFactors { get; set; } = new ObservableCollection<BinningFactorType>();
        public ObservableCollection<GainType> Gains { get; set; } = new ObservableCollection<GainType>();
        public CameraSettings CameraDefaultSettings { get; set; } = new CameraSettings();
        public Dictionary<MotionTypes, MotionRanges> MotionSettings { get; set; } = new Dictionary<MotionTypes, MotionRanges>();
        public Dictionary<MotionTypes, MotionSettings> MotionStartupSettings { get; set; } = new Dictionary<MotionTypes, MotionSettings>();
        public Dictionary<MotionTypes, MotionSettings> MotionHomeSettings { get; set; } = new Dictionary<MotionTypes, MotionSettings>();
        public Dictionary<MotionTypes, double> MotionFactors { get; set; } = new Dictionary<MotionTypes, double>();
        public Dictionary<MotionTypes, double> MotionEncoderFactors { get; set; } = new Dictionary<MotionTypes, double>();
        public List<StageRegion> YStageRegions { get; set; } = new List<StageRegion>();
        public Dictionary<string, double> StageRegions { get; set; } = new Dictionary<string, double>();
        public Dictionary<int, double> FilterPositionSettings { get; set; } = new Dictionary<int, double>();
        public List<LEDSetting> LEDSettings { get; set; } = new List<LEDSetting>();
        public FluidicsFlowSettings FluidicsSettings { get; set; } = new FluidicsFlowSettings();
        //public AutoFocusSettings AutoFocusingSettings { get; set; } = new AutoFocusSettings();
        public Dictionary<SerialDeviceTypes, SerialCommSettings> SerialCommDeviceSettings { get; set; } = new Dictionary<SerialDeviceTypes, SerialCommSettings>();
        public FCTemperCtrlSettings FCTemperCtrlSettings { get; set; } = new FCTemperCtrlSettings();
        public RecipeBuildSettings RecipeBuildConfig { get; set; } = new RecipeBuildSettings();
        public List<OLABaseCallRange> OLABaseCallRanges { get; set; } = new List<OLABaseCallRange>();      
        public SystemConfigJson()
        {

        }

        //public SystemConfigJson(ConfigSettings cfs)
        //{


        //}
        public ulong GetProcessorAffinityValue()
        {
            ulong processorAffinity;
            char[] trimhex = new char[] { '0', 'x' };

            if (!ulong.TryParse(ProcessorAffinity.TrimStart(trimhex), NumberStyles.HexNumber, CultureInfo.CurrentCulture,
                out processorAffinity))
            {
                processorAffinity = 0;
            }
            return processorAffinity;
        }
    }
}
