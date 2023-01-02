using System;
using System.IO;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    static public class SettingsManager
    {
        //< expected version of calibration file. Used to check file compatibility
        private static readonly int s_calibrationFileVersion = 3;

        //< expected version of configuration file. Used to check file compatibility
        private static readonly int s_configurationFileVersion = 3;
        public static string ApplicationDataPath { get; private set; }

        public static string ApplicationMediaDataPath { get; private set; }

        public static string CalibarationDataPath { get; private set; }
        static public ConfigSettings ConfigSettings { get; private set; } = null;
        static public ISeqLog Logger { get; private set; }


        public async static Task OnStartup(ISeqLog logger, string applicationDataPath, string calibrationDataPath)
        {
            Logger = logger;
            //example (for EUI) --- C:\ProgramData\Sequlite Instruments\ALF EngineerGUI
            //example2 (for CUI) --- C:\ProgramData\Sequlite Instruments\Sequlite Software
            ApplicationDataPath = applicationDataPath;
            CalibarationDataPath = calibrationDataPath;
            ApplicationMediaDataPath = Path.Combine(applicationDataPath, "Media");
            
            if (!Directory.Exists(ApplicationMediaDataPath))
            {
                Directory.CreateDirectory(ApplicationMediaDataPath);
            }

            string configFileFullName = EnsureConfigFilesExists();
            Logger.Log($"Loading configuration file: {configFileFullName}...");
            ConfigSettings = await LoadConfigJsonFile(configFileFullName);

            string calibFileFullPathName = EnsureCalibFilesExists();
            Logger.Log($"Loading calibration file: {calibFileFullPathName}... ");
            CalibSettings calibSetting = await LoadCalibJsonFile(calibFileFullPathName);

            ConfigSettings.CalibrationSettings = calibSetting;
            string dir = EnsureDefaultRecipeDirExists();
            ConfigSettings.SystemConfig.RecipeBuildConfig.RecipeBaseDir = dir;

            EnsureDatabaseExists();
            EnsureDefaultSampleSheetDirExists();
        }

        private static async Task<bool> SaveConfigSettingToJsonFile(SystemConfigJson sc, string fileName2) //full path name
        {
            //save a back up settings-- - for testing.
            //string fileName2 = @"Config_Backup.json";
            //string backupConfigFilePath = Path.Combine(ApplicationDataPath, fileName2);
            bool b = false;
            try
            {
                SettingJsonManipulater ConfigManipulater = new SettingJsonManipulater();
                await ConfigManipulater.SaveSettingsToFile(sc, fileName2);
                b = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save a back up for config json settings to : " + fileName2 + " with exception: " + ex.Message);
            }
            return b;
        }

        public static void OnExit()
        {

        }

        #region Private Functions
        //return full path config file name

        /// <summary>
        /// Checks that a config file exists in the target directory.
        /// If no config exists, tries to copy the default config file to the target directory.
        /// Note: The config and calib files are stored in same directory
        /// </summary>
        /// <returns>The full path name of the config file</returns>
        private static string EnsureConfigFilesExists()
        {
            // the name of the default config file. This file is copied to the executable directory during the build process
            // the first time this application is run, this file will be copied to Environment.SpecialFolder.CommonApplicationData
            // and renamed Config.json
            const string DefaultConfigFileName = "Config_default.json";
            const string ConfigFileName = "Config.json";

            string sourceConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigFileName);
            string targetConfigFilePath = Path.Combine(CalibarationDataPath, ConfigFileName);

            if (!File.Exists(targetConfigFilePath))
            {
                if (!File.Exists(sourceConfigFilePath))
                {
                    // no calibration file could be found
                    string message = $"Configuration file not found";
                    Logger.LogError(message);
                    throw new FileNotFoundException(message);
                }
                else
                {
                    // copy the default file to the app data folder
                    File.Copy(sourceConfigFilePath, targetConfigFilePath);
                }
            }
            return targetConfigFilePath;
        }

        private static string GetFile(string filename, string targetDir = null, string sourceDir = null)
        {
            targetDir = (targetDir == null) ? ApplicationDataPath : targetDir;
            sourceDir = (sourceDir == null) ? AppDomain.CurrentDomain.BaseDirectory : sourceDir;
            string sourceConfigFilePath = Path.Combine(sourceDir, filename);
            string targetConfigFilePath = Path.Combine(targetDir, filename);
            if (!File.Exists(targetConfigFilePath))
            {
                if (!File.Exists(sourceConfigFilePath))
                {
                    string message = $"{sourceConfigFilePath} not found";
                    Logger.LogError(message);
                    throw new FileNotFoundException(message);
                }
                else
                {
                    // copy the default file to the app data folder
                    File.Copy(sourceConfigFilePath, targetConfigFilePath);
                }
            }
            return targetConfigFilePath;
        }

        /// <summary>
        /// Checks that a calibration (calib) file exists in the target directory.
        /// If no calib exists, tries to copy the default calib file to the target directory.
        /// Note: The config and calib files are stored in same directory 
        /// </summary>
        /// <returns>The full path name of the calib file</returns>
        private static string EnsureCalibFilesExists()
        {
            // the name of the default calibration file. This file is copied to the executable directory during the build process
            // the first time this application is run, this file will be copied to Environment.SpecialFolder.CommonApplicationData
            // and renamed Calib.json
            const string DefaultCalibFileName = "Calib_default.json";
            const string CalibFileName = "Calib.json";

            string sourceCalibFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultCalibFileName);
            string targetCalibFilePath = Path.Combine(CalibarationDataPath, CalibFileName);

            if (!File.Exists(targetCalibFilePath))
            {
                if (!File.Exists(sourceCalibFilePath))
                {
                    // no calibration file could be found
                    string message = $"Calibration file not found";
                    Logger.LogError(message);
                    throw new FileNotFoundException(message);
                }
                else
                {
                    // copy the default file to the app data folder
                    File.Copy(sourceCalibFilePath, targetCalibFilePath);
                }
            }
            return targetCalibFilePath;
        }

        private static string EnsureDefaultRecipeDirExists()
        {
            string defaultRecipePath = "";
            try
            {
                defaultRecipePath = Path.Combine(ApplicationDataPath, "Recipes");
                if (!Directory.Exists(defaultRecipePath))
                {
                    Directory.CreateDirectory(defaultRecipePath);
                }

                string sourceFileDir = $"{AppDomain.CurrentDomain.BaseDirectory}\\Data\\Recipes";
                DirectoryInfo folder = new DirectoryInfo(sourceFileDir);
                foreach (var file in folder.GetFiles("*.xml"))
                    GetFile(file.Name, defaultRecipePath, sourceFileDir);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to find or create default recipe path {defaultRecipePath}");
                throw ex;
            }
            return defaultRecipePath;
        }

        private static string EnsureDefaultSampleSheetDirExists()
        {
            string defaultSampleSheetPath = "";
            try
            {
                defaultSampleSheetPath = Path.Combine(ApplicationDataPath, "SampleSheets");
                if (!Directory.Exists(defaultSampleSheetPath))
                    Directory.CreateDirectory(defaultSampleSheetPath);

                string sourceFileDir = $"{AppDomain.CurrentDomain.BaseDirectory}\\Data\\SampleSheets";
                DirectoryInfo folder = new DirectoryInfo(sourceFileDir);
                foreach (var file in folder.GetFiles("*.csv"))
                    GetFile(file.Name, defaultSampleSheetPath, sourceFileDir);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to find or create directory {defaultSampleSheetPath}");
                throw ex;
            }
            return defaultSampleSheetPath;
        }

        private static void EnsureDatabaseExists()
        {
            string sourceFileDir = $"{AppDomain.CurrentDomain.BaseDirectory}\\Data\\Database";
            try
            {                
                DirectoryInfo folder = new DirectoryInfo(sourceFileDir);
                foreach (var file in folder.GetFiles("*.db"))
                    GetFile(file.Name, ApplicationDataPath, sourceFileDir);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to copy *.db file from {sourceFileDir}");
                throw ex;
            }
        }

        private static async Task<ConfigSettings> LoadConfigJsonFile(string configFilePath)
        {
            ConfigSettings cfs = null;
            try
            {

                if (!File.Exists(configFilePath))
                {
                    string message = $"Failed to load configuration file: {configFilePath}";
                    Logger.LogError(message);
                    throw new FileNotFoundException(message);
                }

                SystemConfigJson configJson = null;
                try
                {
                    SettingJsonManipulater ConfigManipulater = new SettingJsonManipulater();
                    configJson = await ConfigManipulater.ReadSettingsFromFile<SystemConfigJson>(configFilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error reading configuration file: {configFilePath}. Exception: {ex.Message}");
                    throw ex;
                }

                if (configJson == null)
                {
                    Logger.LogError($"Unhandled error reading configuration file: {configFilePath}");
                }
                else
                {
                    // warn if configuration version and software internal versions do not match
                    if (configJson.Version != s_configurationFileVersion)
                    {
                        string message = $"Configuration file is not compatible with this software version." +
                            $" Configuration file version: {configJson.Version} Configuration file version: {s_configurationFileVersion}";

                        throw new Exception(message);
                    }
                    cfs = new ConfigSettings();
                    cfs.SetLoadedSystemConfig(configJson, Logger);
                }

            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                throw e;
            }
            return cfs;
        }


        private static async Task<CalibSettings> LoadCalibJsonFile(string calibFilePath)
        {
            CalibSettings cfs = null;
            try
            {
                if (!File.Exists(calibFilePath))
                {
                    string message = $"Failed to load calibration file: {calibFilePath}";
                    Logger.LogError(message);
                    throw new FileNotFoundException(message);
                }

                SystemCalibJson calibJson = null;
                try
                {
                    SettingJsonManipulater ConfigManipulater = new SettingJsonManipulater();
                    calibJson = await ConfigManipulater.ReadSettingsFromFile<SystemCalibJson>(calibFilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error reading calibration file: {calibFilePath}. Exception: {ex.Message}");
                    throw ex;
                }
                if (calibJson == null)
                {
                    Logger.LogError($"Unhandled error reading calibration file: {calibFilePath}");
                }
                else
                {
                    // warn if calibration version and software internal versions do not match
                    if (calibJson.Version.ID != s_calibrationFileVersion)
                    {
                        string message = $"Calibration file is not compatible with this software version." +
                            $" Calibration file version: {calibJson.Version.ID} Compatibile file version: {s_calibrationFileVersion}";
                        
                        throw new FormatException(message);
                    }
                    //warn if calib pos exceed the config motion limit
                    double reagentpos = calibJson.FluidicsCalibSettings.ReagentCartPos;
                    double washpos = calibJson.FluidicsCalibSettings.WashCartPos;
                    double carlimitH = ConfigSettings.MotionSettings[MotionTypes.Cartridge].MotionRange.LimitHigh;
                    double carlimitL = ConfigSettings.MotionSettings[MotionTypes.Cartridge].MotionRange.LimitLow;
                    if (carlimitH < reagentpos || carlimitH < washpos || carlimitL > reagentpos || carlimitL > washpos)
                    {
                        string message = $"Calibration Reagent Cartridge Position {reagentpos}, Wash Cartridge Position {washpos}, exceed the limit [{carlimitL}, {carlimitH}], check config and calib json";
                        throw new Exception(message);
                    }
                    cfs = new CalibSettings();
                    cfs.SetLoadedCalibConfig(calibJson, Logger);
                }
            }
            catch (AggregateException ex)
            {
                foreach(var innerException in ex.InnerExceptions)
                {
                    Logger.LogError(innerException.Message);
                    throw ex;
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.Message);
                throw ex;
            }
            return cfs;
        }

        //static ConfigSettings LoadConfigFile(string configFilePath)
        //{
        //    //string configFilePath = Path.Combine(ApplicationDataPath, _ConfigFileName);

        //    if (!File.Exists(configFilePath))
        //    {
        //        Logger.LogError("Configuration file does not exists: " + configFilePath);
        //        throw new Exception("Configuration file does not exists: " + configFilePath);
        //    }

        //    ConfigSettings cfs = new ConfigSettings();

        //     XPathDocument xpathDoc = new XPathDocument(configFilePath);
        //    XPathNavigator xpathNav = xpathDoc.CreateNavigator();
        //    XPathNodeIterator iter = xpathNav.Select("/Config/BinFactors/BinFactor");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        BinningFactorType binningItem = new BinningFactorType();
        //        string str = nav.GetAttribute("Position", "");
        //        if (!string.IsNullOrEmpty(str))
        //        {
        //            binningItem.Position = int.Parse(str);
        //        }
        //        str = nav.GetAttribute("HorizontalBins", "");
        //        if (!string.IsNullOrEmpty(str))
        //        {
        //            binningItem.HorizontalBins = int.Parse(str);
        //        }
        //        str = nav.GetAttribute("VerticalBins", "");
        //        if (!string.IsNullOrEmpty(str))
        //        {
        //            binningItem.VerticalBins = int.Parse(str);
        //        }
        //        binningItem.DisplayName = nav.GetAttribute("DisplayName", "");
        //        cfs.BinFactors.Add(binningItem);
        //    }

        //    iter = xpathNav.Select("/Config/Gains/Gain");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        GainType gainItem = new GainType();
        //        string str = nav.GetAttribute("Position", "");
        //        if (!string.IsNullOrEmpty(str))
        //        {
        //            gainItem.Position = int.Parse(str);
        //        }
        //        str = nav.GetAttribute("Value", "");
        //        if (!string.IsNullOrEmpty(str))
        //        {
        //            gainItem.Value = int.Parse(str);
        //        }
        //        gainItem.DisplayName = nav.GetAttribute("DisplayName", "");
        //        cfs.Gains.Add(gainItem);
        //    }

        //    iter = xpathNav.Select("/Config/CameraDefaultSettings/CameraSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        string valStr = nav.GetAttribute("Value", "");
        //        if (name == "BinFactor")
        //        {
        //            int bin = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.BinFactor = bin;
        //        }
        //        else if (name == "Gain")
        //        {
        //            int gain = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.Gain = gain;
        //        }
        //        else if (name == "RoiLeft")
        //        {
        //            int left = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.RoiLeft = left;
        //        }
        //        else if (name == "RoiTop")
        //        {
        //            int top = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.RoiTop = top;
        //        }
        //        else if (name == "RoiWidth")
        //        {
        //            int width = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.RoiWidth = width;
        //        }
        //        else if (name == "RoiHeight")
        //        {
        //            int height = int.Parse(valStr);
        //            cfs.CameraDefaultSettings.RoiHeight = height;
        //        }
        //        else if (name == "ReadoutSpeed")
        //        {
        //            cfs.CameraDefaultSettings.ReadoutSpeed = valStr;
        //        }
        //        else if (name == "ExtraExposure")
        //        {
        //            cfs.CameraDefaultSettings.ExtraExposure = double.Parse(valStr);
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/MotionSettings/MotionSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        MotionRanges motionItem = new MotionRanges();
        //        MotionTypes type;
        //        Enum.TryParse(nav.GetAttribute("Type", ""), out type);
        //        motionItem.SpeedRange.LimitHigh = double.Parse(nav.GetAttribute("MaxSpeed", ""));
        //        motionItem.AccelRange.LimitHigh = double.Parse(nav.GetAttribute("MaxAccel", ""));
        //        motionItem.MotionRange.LimitLow = double.Parse(nav.GetAttribute("Min", ""));
        //        motionItem.MotionRange.LimitHigh = double.Parse(nav.GetAttribute("Max", ""));
        //        cfs.MotionSettings.Add(type, motionItem);
        //    }

        //    iter = xpathNav.Select("/Config/MotionStartupSettings/MotionStartupSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        MotionSettings motionItem = new MotionSettings();
        //        MotionTypes type;
        //        Enum.TryParse(nav.GetAttribute("Type", ""), out type);
        //        motionItem.Speed = double.Parse(nav.GetAttribute("Speed", ""));
        //        motionItem.Accel = double.Parse(nav.GetAttribute("Accel", ""));
        //        motionItem.Absolute = double.Parse(nav.GetAttribute("Absolute", ""));
        //        motionItem.Relative = double.Parse(nav.GetAttribute("Relative", ""));
        //        cfs.MotionStartupSettings.Add(type, motionItem);
        //    }

        //    iter = xpathNav.Select("/Config/MotionFactors/MotionFactor");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        MotionTypes type;
        //        Enum.TryParse(nav.GetAttribute("Type", ""), out type);
        //        double factor = double.Parse(nav.GetAttribute("Factor", ""));
        //        cfs.MotionFactors.Add(type, factor);
        //    }

        //    iter = xpathNav.Select("/Config/FluidicsSettings/PumpSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        double value = double.Parse(nav.GetAttribute("Value", ""));
        //        cfs.FluidicsSettings.PumpSetting.Add(name, value);
        //        switch (name)
        //        {
        //            case "PosToVolFactor":
        //                cfs.PumpIncToVolFactor = value;
        //                break;
        //            case "AspRateLimitLow":
        //                cfs.PumpAspRateRange.LimitLow = value;
        //                break;
        //            case "AspRateLimitHigh":
        //                cfs.PumpAspRateRange.LimitHigh = value;
        //                break;
        //            case "DispRateLimitLow":
        //                cfs.PumpDispRateRange.LimitLow = value;
        //                break;
        //            case "DispRateLimitHigh":
        //                cfs.PumpDispRateRange.LimitHigh = value;
        //                break;
        //            case "PumpVolLimitLow":
        //                cfs.PumpVolRange.LimitLow = value;
        //                break;
        //            case "PumpVolLimitHigh":
        //                cfs.PumpVolRange.LimitHigh = value;
        //                break;
        //            case "DelayTime":
        //                cfs.PumpDelayTime = value;
        //                break;
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/FluidicsSettings/ChemiSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        double value = double.Parse(nav.GetAttribute("Value", ""));
        //        cfs.FluidicsSettings.ChemiSetting.Add(name, value);
        //        switch (name)
        //        {
        //            case "TemperLimitLow":
        //                cfs.ChemiTemperRange.LimitLow = value;
        //                break;
        //            case "TemperLimitHigh":
        //                cfs.ChemiTemperRange.LimitHigh = value;
        //                break;
        //            case "TemperRampLimitLow":
        //                cfs.ChemiTemperRampRange.LimitLow = value;
        //                break;
        //            case "TemperRampLimitHigh":
        //                cfs.ChemiTemperRampRange.LimitHigh = value;
        //                break;
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/LEDSettings/LEDSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string type = nav.GetAttribute("Type", "");
        //        double max = double.Parse(nav.GetAttribute("Max", ""));
        //        double min = double.Parse(nav.GetAttribute("Min", ""));
        //        string maxOnStr = nav.GetAttribute("MaxOnTime", "");
        //        SettingRange range = new SettingRange();
        //        range.LimitHigh = max;
        //        range.LimitLow = min;
        //        int maxOnTime = 0;
        //        switch (type)
        //        {
        //            case "G":
        //                cfs.LEDIntensitiesRange.Add(LEDTypes.Green, range);
        //                if (!string.IsNullOrEmpty(maxOnStr))
        //                {
        //                    maxOnTime = int.Parse(maxOnStr);
        //                    cfs.LEDMaxOnTimes.Add(LEDTypes.Green, maxOnTime);
        //                }
        //                cfs.LEDSettings.Add(new LEDSetting() { Type = LEDTypes.Green, Range = range, MaxOnTime = maxOnTime });
        //                break;
        //            case "R":
        //                cfs.LEDIntensitiesRange.Add(LEDTypes.Red, range);
        //                if (!string.IsNullOrEmpty(maxOnStr))
        //                {
        //                    maxOnTime = int.Parse(maxOnStr);
        //                    cfs.LEDMaxOnTimes.Add(LEDTypes.Red, maxOnTime);
        //                }
        //                cfs.LEDSettings.Add(new LEDSetting() { Type = LEDTypes.Red, Range = range, MaxOnTime = maxOnTime });

        //                break;
        //            case "W":
        //                cfs.LEDIntensitiesRange.Add(LEDTypes.White, range);
        //                if (!string.IsNullOrEmpty(maxOnStr))
        //                {
        //                    maxOnTime = int.Parse(maxOnStr);
        //                    cfs.LEDMaxOnTimes.Add(LEDTypes.White, maxOnTime);
        //                }
        //                cfs.LEDSettings.Add(new LEDSetting() { Type = LEDTypes.White, Range = range, MaxOnTime = maxOnTime });

        //                break;
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/YStageRegions/Region");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        int pos = int.Parse(nav.GetAttribute("Position", ""));
        //        double ypos = double.Parse(nav.GetAttribute("Ypos", ""));
        //        double zpos = double.Parse(nav.GetAttribute("Zpos", ""));
        //        cfs.YStageRegionPositions.Add(pos, ypos);
        //        cfs.ZStageRegionPositions.Add(pos, zpos);
        //        cfs.YStageRegions.Add(new StageRegion() { Position = pos, Ypos = ypos, Zpos = zpos });
        //    }

        //    iter = xpathNav.Select("/Config/StageRegions/RegionsSetting");
        //    double lane = 0;
        //    double row = 0;
        //    double col = 0;
        //    double interval = 0;
        //    double[][] startpoint = new double[4][];
        //    startpoint[0] = new double[2];
        //    startpoint[1] = new double[2];
        //    startpoint[2] = new double[2];
        //    startpoint[3] = new double[2];
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        double value = double.Parse(nav.GetAttribute("Value", ""));
        //        cfs.StageRegions[name] = value;
        //        if (name == "Lane") { lane = value; }
        //        if (name == "Row") { row = value; }
        //        if (name == "Column") { col = value; }
        //        if (name == "Interval") { interval = value; }
        //        if (name == "StartPointX1") { startpoint[0][0] = value; }
        //        if (name == "StartPointY1") { startpoint[0][1] = value; }
        //        if (name == "StartPointX2") { startpoint[1][0] = value; }
        //        if (name == "StartPointY2") { startpoint[1][1] = value; }
        //        if (name == "StartPointX3") { startpoint[2][0] = value; }
        //        if (name == "StartPointY3") { startpoint[2][1] = value; }
        //        if (name == "StartPointX4") { startpoint[3][0] = value; }
        //        if (name == "StartPointY4") { startpoint[3][1] = value; }
        //    }
        //    cfs.FCColumn = (int)col;
        //    cfs.FCLane = (int)lane;
        //    cfs.FCRow = (int)row;
        //    double[] newpoint = new double[2];
        //    if (lane * col * row == 0) { throw new Exception("Wrong RegionSettings in Config file "); }
        //    for (int l = 1; l < lane + 1; l++)
        //    {
        //        for (int r = 1; r < row + 1; r++)
        //        {
        //            for (int c = 1; c < col + 1; c++)
        //            {
        //                int colx;
        //                if (r % 2 == 0) { colx = (int)col - c + 1; } else { colx = c; }
        //                int[] regionindex = new int[3] { l, colx, r };
        //                newpoint = new double[2] { (startpoint[(l - 1)][0] + (interval * (colx - 1))), (startpoint[(l - 1)][1] + (interval * (r - 1))) };
        //                cfs.StageRegionMaps.Add(new RegionIndex(regionindex), newpoint);
        //            }
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/FluidicsSettings/DefaultPumping");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        double value = double.Parse(nav.GetAttribute("Value", ""));
        //        cfs.FluidicsSettings.DefaultPumping.Add(name, value);
        //        if (name == "AspRate")
        //        {
        //            cfs.FluidicsStartupSettings.AspRate = value;
        //        }
        //        else if (name == "DisRate")
        //        {
        //            cfs.FluidicsStartupSettings.DisRate = value;
        //        }
        //        else if (name == "Volume")
        //        {
        //            cfs.FluidicsStartupSettings.Volume = value;
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/FluidicsSettings/DefaultPumpPath");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        //MotionSettings motionItem = new MotionSettings();
        //        PumpingSettings pumpItem = new PumpingSettings();
        //        PathOptions paths;
        //        Enum.TryParse(nav.GetAttribute("Type", ""), out paths);
        //        pumpItem.SelectedPullValve2Pos = int.Parse(nav.GetAttribute("valve2", ""));
        //        pumpItem.SelectedPullValve3Pos = int.Parse(nav.GetAttribute("valve3", ""));
        //        for (int i = 0; i < 4; i++)
        //        {
        //            if (nav.GetAttribute("pumpvalve", "").ToCharArray()[i] == '1') { pumpItem.PumpPullingPaths[i] = true; }
        //            else { pumpItem.PumpPullingPaths[i] = false; }

        //        }
        //        cfs.PumpPathDefault.Add(paths, pumpItem);
        //        cfs.FluidicsSettings.DefaultPumpPath.Add(paths, new PumpPathSettings()
        //        {
        //            valve2 = pumpItem.SelectedPullValve2Pos,
        //            valve3 = pumpItem.SelectedPullValve3Pos,
        //            pumpvalve = nav.GetAttribute("pumpvalve", "")
        //        });
        //    }

        //    iter = xpathNav.Select("/Config/FluidicsSettings/DefaultChemistry");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string name = nav.GetAttribute("Name", "");
        //        double value = double.Parse(nav.GetAttribute("Value", ""));
        //        cfs.FluidicsSettings.DefaultChemistry.Add(name, value);
        //        if (name == "Temper")
        //        {
        //            cfs.ChemistryStartupSettings.Temper = value;
        //        }
        //        else if (name == "Ramp")
        //        {
        //            cfs.ChemistryStartupSettings.Ramp = value;
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/FilterPositionSettings/FilterPositionSetting");
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string posStr = nav.GetAttribute("Pos", "");
        //        string valStr = nav.GetAttribute("Value", "");
        //        if (!string.IsNullOrEmpty(posStr))
        //        {
        //            if (!string.IsNullOrEmpty(valStr))
        //            {
        //                cfs.FilterPositionSettings.Add(int.Parse(posStr), double.Parse(valStr));
        //            }
        //        }
        //    }

        //    iter = xpathNav.Select("/Config/AutoFocusSettings/AutoFocusSetting");
        //    System.Windows.Int32Rect Roi = new System.Windows.Int32Rect();
        //    while (iter.MoveNext())
        //    {
        //        XPathNavigator nav = iter.Current;
        //        string nameStr = nav.GetAttribute("Name", "");
        //        string valStr = nav.GetAttribute("Value", "");
        //        if (!string.IsNullOrEmpty(nameStr))
        //        {
        //            if (!string.IsNullOrEmpty(valStr))
        //            {
        //                switch (nameStr)
        //                {
        //                    case "RoiLeft":
        //                        Roi.X = int.Parse(valStr);
        //                        break;
        //                    case "RoiTop":
        //                        Roi.Y = int.Parse(valStr);
        //                        break;
        //                    case "RoiWidth":
        //                        Roi.Width = int.Parse(valStr);
        //                        break;
        //                    case "RoiHeight":
        //                        Roi.Height = int.Parse(valStr);
        //                        break;
        //                    case "LED":
        //                        cfs.AutoFocusingSettings.LEDType = (LEDTypes)Enum.Parse(typeof(LEDTypes), valStr);
        //                        break;
        //                    case "Intensity":
        //                        cfs.AutoFocusingSettings.LEDIntensity = uint.Parse(valStr);
        //                        break;
        //                    case "Exposure":
        //                        cfs.AutoFocusingSettings.ExposureTime = double.Parse(valStr);
        //                        break;
        //                    case "Speed":
        //                        cfs.AutoFocusingSettings.ZstageSpeed = double.Parse(valStr);
        //                        break;
        //                    case "Accel":
        //                        cfs.AutoFocusingSettings.ZstageAccel = double.Parse(valStr);
        //                        break;
        //                    case "ZRange":
        //                        cfs.AutoFocusingSettings.ZRange = double.Parse(valStr);
        //                        break;
        //                    case "FilterIndex":
        //                        cfs.AutoFocusingSettings.FilterIndex = int.Parse(valStr);
        //                        break;
        //                    case "OffsetFilterIndex":
        //                        cfs.AutoFocusingSettings.OffsetFilterIndex = int.Parse(valStr);
        //                        break;
        //                    case "OffsetLED":
        //                        cfs.AutoFocusingSettings.OffsetLEDType = (LEDTypes)Enum.Parse(typeof(LEDTypes), valStr);
        //                        break;
        //                    case "OffsetIntensity":
        //                        cfs.AutoFocusingSettings.OffsetLEDIntensity = uint.Parse(valStr);
        //                        break;
        //                    case "OffsetExposure":
        //                        cfs.AutoFocusingSettings.OffsetExposureTime = double.Parse(valStr);
        //                        break;
        //                }
        //            }
        //        }
        //    }
        //    cfs.AutoFocusingSettings.ROI = Roi;
        //    return cfs;
        //}
        #endregion Private Functions
    }
}

