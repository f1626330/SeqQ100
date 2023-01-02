//using Sequlite.ALF.Imaging;
//using Sequlite.ALF.MainBoard;
using Sequlite.ALF.Common.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Sequlite.ALF.Common
{
    public class ConfigSettings
    {
        //from config file one-one ma, read only shall be never changed after reading from config file------------------------------
        public SystemConfigJson _SystemConfig;
        public CalibSettings _CalibSettings;
        public CalibSettings CalibrationSettings { get => _CalibSettings; set => _CalibSettings = value;  }
        public SystemConfigJson SystemConfig { get { return _SystemConfig; } set { _SystemConfig = value; } }
        //----end

        public ObservableCollection<BinningFactorType> BinFactors { get { return _SystemConfig.BinFactors; } }
        public ObservableCollection<GainType> Gains { get { return _SystemConfig.Gains; } }
        public CameraSettings CameraDefaultSettings { get { return _SystemConfig.CameraDefaultSettings; } }
        public Dictionary<MotionTypes, MotionRanges> MotionSettings { get { return _SystemConfig.MotionSettings; } }
        public Dictionary<MotionTypes, MotionSettings> MotionStartupSettings { get { return _SystemConfig.MotionStartupSettings; } }
        public Dictionary<MotionTypes, MotionSettings> MotionHomeSettings { get { return _SystemConfig.MotionHomeSettings; } }
        public Dictionary<MotionTypes, double> MotionFactors { get { return _SystemConfig.MotionFactors; } }
        public Dictionary<MotionTypes, double> MotionEncoderFactors { get { return _SystemConfig.MotionEncoderFactors; } }
        public List<StageRegion> YStageRegions { get { return _SystemConfig.YStageRegions; } }
        public Dictionary<string, double> StageRegions { get { return _SystemConfig.StageRegions; } }
        public Dictionary<int, double> FilterPositionSettings { get { return _SystemConfig.FilterPositionSettings; } }
        public List<LEDSetting> LEDSettings { get { return _SystemConfig.LEDSettings; } }
        public FluidicsFlowSettings FluidicsSettings { get { return _SystemConfig.FluidicsSettings; } }
        //public AutoFocusSettings AutoFocusingSettings { get { return _SystemConfig.AutoFocusingSettings; } }
        public AutoFocusSettings AutoFocusingSettings { get { return CalibrationSettings?.AutoFocusingSettings; } }
        public InstrumentInfo InstrumentInfo { get { return CalibrationSettings?.InstrumentInfo; } }
        public CameraCalib CameraCalibSettings { get { return CalibrationSettings?.CameraCalibSettings; } }
        public FluidicsCalib FluidicsCalibSettings { get { return CalibrationSettings?.FluidicsCalibSettings; } }
        public Dictionary<RegionIndex, double[]> StageRegionMaps { get { return CalibrationSettings?.StageRegionMaps; } }
        public int FCLane { get { return (int)CalibrationSettings?.FCLane; } }
        public int FCRow { get { return (int)CalibrationSettings?.FCRow; } }
        public int FCColumn { get { return (int)CalibrationSettings?.FCColumn; } }
        public Dictionary<SerialDeviceTypes, SerialCommSettings> SerialCommDeviceSettings { get { return _SystemConfig.SerialCommDeviceSettings; } }
        public FCTemperCtrlSettings FCTemperCtrlSettings { get { return _SystemConfig.FCTemperCtrlSettings; } }
        //end ----------------------------------------------------------------------------------------------------

        //following properties shall be consolidated with above settings as much as possible -------------------
        public Dictionary<PathOptions, PumpingSettings> PumpPathDefault { get; } = new Dictionary<PathOptions, PumpingSettings>();
        public double PumpIncToVolFactor { get; set; }
        public SettingRange PumpAspRateRange { get; } = new SettingRange();
        public SettingRange PumpDispRateRange { get; } = new SettingRange();
        public SettingRange PumpVolRange { get; } = new SettingRange();
        public double PumpDelayTime { get; set; }
        public SettingRange ChemiTemperRange { get; } = new SettingRange();
        public SettingRange ChemiTemperRampRange { get; } = new SettingRange();

        public Dictionary<LEDTypes, SettingRange> LEDIntensitiesRange { get; } = new Dictionary<LEDTypes, SettingRange>();
        public Dictionary<LEDTypes, int> LEDMaxOnTimes { get; } = new Dictionary<LEDTypes, int>();
        public Dictionary<int, double> YStageRegionPositions { get; } = new Dictionary<int, double>();
        public Dictionary<int, double> ZStageRegionPositions { get; } = new Dictionary<int, double>();


        public FluidicsValueSettings FluidicsStartupSettings { get; } = new FluidicsValueSettings();
        public ChemistrySettings ChemistryStartupSettings { get; } = new ChemistrySettings();
        //end -------------------------------------------------------------------------------------------

        public  ConfigSettings()
        {
            _SystemConfig = new SystemConfigJson();
        }

        public void SetLoadedSystemConfig(SystemConfigJson systemConfigJson, ISeqLog logger)
        {
            _SystemConfig = systemConfigJson;
            foreach (var it in FluidicsSettings.PumpSetting)
            {
                switch (it.Key)
                {
                    case "PosToVolFactor":
                        PumpIncToVolFactor = it.Value;
                        break;
                    case "AspRateLimitLow":
                        PumpAspRateRange.LimitLow = it.Value;
                        break;
                    case "AspRateLimitHigh":
                        PumpAspRateRange.LimitHigh = it.Value;
                        break;
                    case "DispRateLimitLow":
                        PumpDispRateRange.LimitLow = it.Value;
                        break;
                    case "DispRateLimitHigh":
                        PumpDispRateRange.LimitHigh = it.Value;
                        break;
                    case "PumpVolLimitLow":
                        PumpVolRange.LimitLow = it.Value;
                        break;
                    case "PumpVolLimitHigh":
                        PumpVolRange.LimitHigh = it.Value;
                        break;
                    case "DelayTime":
                        PumpDelayTime = it.Value;
                        break;
                }
            } //end foreach

            foreach (var it in FluidicsSettings.ChemiSetting)
            {
                switch (it.Key)
                {
                    case "TemperLimitLow":
                        ChemiTemperRange.LimitLow = it.Value;
                        break;
                    case "TemperLimitHigh":
                        ChemiTemperRange.LimitHigh = it.Value;
                        break;
                    case "TemperRampLimitLow":
                        ChemiTemperRampRange.LimitLow = it.Value;
                        break;
                    case "TemperRampLimitHigh":
                        ChemiTemperRampRange.LimitHigh = it.Value;
                        break;
                }
            }//end foreach

            foreach (var it in LEDSettings)
            {
                LEDIntensitiesRange.Add(it.Type, it.Range);
                LEDMaxOnTimes.Add(it.Type, it.MaxOnTime);
            }//end foreach

            foreach (var it in YStageRegions)
            {
                YStageRegionPositions.Add(it.Position, it.Ypos);
                ZStageRegionPositions.Add(it.Position, it.Zpos);
            }//end foreach

            //{//start block
            //    double lane = 0;
            //    double row = 0;
            //    double col = 0;
            //    double vinterval = 0;
            //    double hinterval = 0;
            //    double[][] startpoint = new double[4][];
            //    startpoint[0] = new double[2];
            //    startpoint[1] = new double[2];
            //    startpoint[2] = new double[2];
            //    startpoint[3] = new double[2];
            //    foreach (var it in StageRegions)
            //    {
            //        string name = it.Key;
            //        double value = it.Value;
            //        if (name == "Lane") { lane = value; }
            //        if (name == "Row") { row = value; }
            //        if (name == "Column") { col = value; }
            //        if (name == "HInterval") { hinterval = value; }
            //        if (name == "VInterval") { vinterval = value; }
            //        if (name == "StartPointX1") { startpoint[0][0] = value; }
            //        if (name == "StartPointY1") { startpoint[0][1] = value; }
            //        if (name == "StartPointX2") { startpoint[1][0] = value; }
            //        if (name == "StartPointY2") { startpoint[1][1] = value; }
            //        if (name == "StartPointX3") { startpoint[2][0] = value; }
            //        if (name == "StartPointY3") { startpoint[2][1] = value; }
            //        if (name == "StartPointX4") { startpoint[3][0] = value; }
            //        if (name == "StartPointY4") { startpoint[3][1] = value; }
            //    }
            //    FCColumn = (int)col;
            //    FCLane = (int)lane;
            //    FCRow = (int)row;
            //    double[] newpoint = new double[2];
            //    if (lane * col * row == 0)
            //    {
            //        logger.LogError("Wrong RegionSettings in Config file");
            //        throw new Exception("Wrong RegionSettings in Config file ");
            //    }

            //    for (int l = 1; l < lane + 1; l++)
            //    {
            //        for (int r = 1; r < row + 1; r++)
            //        {
            //            for (int c = 1; c < col + 1; c++)
            //            {

            //                int colx;
            //                if(r % 2 == 0) { colx = (int)col - c + 1; } else { colx = c; }
            //                int[] regionindex = new int[3] { l, colx, r };
            //                newpoint = new double[2] { (startpoint[(l - 1)][0] + (hinterval * (colx - 1))), (startpoint[(l - 1)][1] + (vinterval * (r - 1))) };
            //                StageRegionMaps.Add(new RegionIndex(regionindex), newpoint);
            //            }
            //        }
            //    }
            //} //end block

            foreach (var it in FluidicsSettings.DefaultPumping)
            {
                string name = it.Key;
                double value = it.Value;
                if (name == "AspRate")
                {
                    FluidicsStartupSettings.AspRate = value;
                }
                else if (name == "DisRate")
                {
                    FluidicsStartupSettings.DisRate = value;
                }
                else if (name == "Volume")
                {
                    FluidicsStartupSettings.Volume = value;
                }
                else if(name == "Buffer1Pos")
                {
                    FluidicsStartupSettings.Buffer1Pos = (int)value;
                }
                else if (name == "Buffer2Pos")
                {
                    FluidicsStartupSettings.Buffer2Pos = (int)value;
                }
                else if (name == "Buffer3Pos")
                {
                    FluidicsStartupSettings.Buffer3Pos = (int)value;
                }
            }//end for each

            foreach (var it in FluidicsSettings.DefaultPumpPath)
            {
                PumpingSettings pumpItem = new PumpingSettings();
                PathOptions paths = it.Key;

                pumpItem.SelectedPullValve2Pos = it.Value.valve2;
                pumpItem.SelectedPullValve3Pos = it.Value.valve3;
                for (int i = 0; i < 4; i++)
                {
                    if (it.Value.pumpvalve.ToCharArray()[i] == '1') { pumpItem.PumpPullingPaths[i] = true; }
                    else { pumpItem.PumpPullingPaths[i] = false; }

                }
                PumpPathDefault.Add(paths, pumpItem);
            } //end for each

            foreach (var it in FluidicsSettings.DefaultChemistry)
            {
                if (it.Key == "Temper")
                {
                    ChemistryStartupSettings.Temper = it.Value;
                }
                else if (it.Key == "Ramp")
                {
                    ChemistryStartupSettings.Ramp = it.Value;
                }
            }//end foreach
        }
    }

}
