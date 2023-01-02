using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.SerialPeripherals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Sequlite.ALF.App
{
    class SeqAppPostRun : IPostRun
    {
        SeqApp SeqApp { get; }

        RecipeBuildSettings _RecipeBuildSettings;
        RecipeRunThreadV2 _RecipeRunThreadV2_PostWash;
        bool _IsPostWashSuccess;
        private bool IsSimulationMode { get; }
        private List<double>[] _PressureList = new List<double>[23];
        private List<double>[] _FlowrateList = new List<double>[23];
        //private bool _IsLastSetPulling;
        //private bool _IsCurrentSetPulling;
        private System.Timers.Timer _LogTimer;
        private int _CurrentSelectorPos;
        private List<int> _ErrorSoultionList = new List<int>();
        public SeqAppPostRun(SeqApp seqApp)
        {
            SeqApp = seqApp;
            IsSimulationMode = seqApp.IsSimulation;
            _RecipeBuildSettings = SettingsManager.ConfigSettings.SystemConfig.RecipeBuildConfig;
        }

        private void FluidicsInterface_OnPumpStatusUpdated(bool _isOn, int solution)
        {
            if (_isOn) { _LogTimer.Start(); _CurrentSelectorPos = solution; }
            else { _LogTimer.Stop(); }
        }

        public bool UnloadCart()
        {
            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            if (IsSimulationMode)
            {
                Thread.Sleep(2000);
                SeqApp.UpdateAppMessage("Sim: Move Cartridge position to 0");
            }
            else
            {
                if (!SeqApp.MotionController.IsCartridgeAvailable)
                {
                    var _Chiler = Chiller.GetInstance();
                    if (_Chiler.ChillerMotorControl(false) == false)
                    {
                        SeqApp.UpdateAppErrorMessage("Move Cartridge failed, retry");
                        if (_Chiler.ChillerMotorControl(false) == false)
                        {
                            SeqApp.UpdateAppErrorMessage("Move Cartridge failed");
                            return false;
                        }
                    }
                    _Chiler.GetChillerMotorStatus();
                    int retry = 0;
                    while (_Chiler.CartridgeMotorStatus != Chiller.CartridgeStatusTypes.Unloaded)
                    {
                        if (++retry > 140)
                        {
                            SeqApp.UpdateAppErrorMessage("Move Cartridge timeout");
                            return false;
                        }
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, 0, speed, accel, true))
                    {
                        //Thread.Sleep(100);
                        //SeqApp.UpdateAppErrorMessage("Move Cartridge failed, retry");
                        //if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, 0, speed, accel, true))
                        //{
                        SeqApp.UpdateAppErrorMessage("Move Cartridge failed");
                        return false;
                        //}
                    }
                }
            }
            return true;
        }

        public bool LoadWash()
        {
            //check Reagent sensor
            if (IsSimulationMode)
            {
                SeqApp.UpdateAppMessage("Sim: check if cartridge door is closed and presented");
            }
            else
            {
                Chiller _Chiller = Chiller.GetInstance();
                _Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
                if (!_Chiller.CartridgeDoor && MotionControl.MotionController.GetInstance().IsReagentDoorEnabled) // is closed
                {
#if !DisableReagentDoor
                    SeqApp.NotifyError("Please close the reagent door");
                    return false;
#endif
                }
                if (!_Chiller.CartridgePresent)
                {
                    SeqApp.NotifyError("Please push back the reagent more");
                    return false;
                }
            }


            //Lower Sipper
            int pos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));

            if (IsSimulationMode)
            {
                SeqApp.UpdateAppMessage("Sim: Load Cartridge");
                Thread.Sleep(2000);
            }
            else
            {
                if (!SeqApp.MotionController.IsCartridgeAvailable)
                {
                    var _Chiller = Chiller.GetInstance();
                    if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos) == false)
                    {
                        SeqApp.UpdateAppErrorMessage("Load Cartridge failed, retry");
                        if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos) == false)
                        {
                            SeqApp.UpdateAppErrorMessage("Load Cartridge failed.");
                            return false;
                        }
                    }
                    _Chiller.GetChillerMotorPos();
                    int retry = 0;
                    while (_Chiller.CartridgeMotorPos != SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos)
                    {
                        if (++retry > 140)
                        {
                            SeqApp.UpdateAppErrorMessage("Load Cartridge timeout.");
                            return false;
                        }
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                    {
                        //Thread.Sleep(100);
                        //SeqApp.UpdateAppErrorMessage("Load Cartridge failed, retry");
                        //if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                        //{
                        SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
                        return false;
                        //}
                    }
                }
            }

            if (IsSimulationMode)
            {
                SeqApp.UpdateAppMessage("Sim: Check wash if buffer is loaded and snipper is lowered");
            }
            else
            {
                //Load buffer
                FluidController _FluidController = FluidController.GetInstance();
                _FluidController.ReadRegisters(FluidController.Registers.OnoffInputs, 1);
                bool bufferin = _FluidController.BufferTrayIn;
                bool sipperdown = _FluidController.SipperDown;
                if (bufferin)
                {
                    SeqApp.NotifyError("Please load the buffer");
                    return false;
                }
                if (sipperdown)
                {
                    SeqApp.NotifyError("Please lower the sipper");
                    return false;
                }
            }
            return true;
        }

        public bool RunWash(RunWashParams parametrs)
        {
            SeqApp.UpdateAppMessage(new AppRunWashStatus() { Message = "Running Wash", WashStatus = ProgressTypeEnum.InProgress },
             AppMessageTypeEnum.Status);
            WashOption washmode = parametrs.SelectedWashOption;
            switch (washmode)
            {
                case WashOption.Prerun:
                case WashOption.PostWash:
                    PostWash(parametrs);
                    break;

                case WashOption.Maintenance: // Post Wash 3 times
                    //for(int i = 0; i<3; i++)
                    //{
                    //    PostWash(parametrs);
                    //    // Load new cartridge
                    //    if (i < 2)
                    //    {
                    //        bool issucload = ReLoadCart();
                    //        if (!issucload) { return false; }
                    //    }
                    //}
                    string washRecipeDir = _RecipeBuildSettings.MaintenanceWashRecipePath;
                    string str = parametrs.SessionId;// DateTime.Now.ToString("yyMMdd-HHmmss");
                    string recipeDir = SeqApp.CreateTempRecipeLocation(str);
                    string originalRecipeDir = _RecipeBuildSettings.RecipeBaseDir;
                    string newRecipeFullFilePath = SeqApp.SaveRecipeToNewPath(washRecipeDir, recipeDir, "MaintenanceWash.xml", originalRecipeDir);
                    Recipe MaintenanceRecipe = Recipe.LoadFromXmlFile(newRecipeFullFilePath);

                    bool loadCartridge = false;
                    _IsPostWashSuccess = false;
                    RecipeThreadParameters _RecipeParam = new RecipeThreadParameters()
                    {

                        SelectedTemplate = TemplateOptions.ecoli,
                        IsSimulation = IsSimulationMode,
                        LoadCartridge = loadCartridge
                    };
                    _RecipeRunThreadV2_PostWash = new RecipeRunThreadV2(
                                SeqApp.TheDispatcher,
                                SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                                MaintenanceRecipe,
                                SeqApp.EthernetCameraA,
                                SeqApp.EthernetCameraB,
                                SeqApp.MotionController,
                                SeqApp.MainboardDevice,
                                SeqApp.LEDController,
                                SeqApp.FluidicsInterface,
                                _RecipeParam, null,
                                null, //no OLA
                                false, //no wait for OLA completion
                                null, //no image backup
                                false
                                );
                    _RecipeRunThreadV2_PostWash.Completed += _RecipeRunThread_PostWash_Completed;
                    _RecipeRunThreadV2_PostWash.InProgress += _RecipeRunThread_PostWash_InProgress;
                    _RecipeRunThreadV2_PostWash.Name = "MaintenanceWash";
                    _RecipeRunThreadV2_PostWash.IsSimulationMode = IsSimulationMode;
                    _RecipeRunThreadV2_PostWash.Start();
                    _RecipeRunThreadV2_PostWash.WaitForCompleted();
                    _RecipeRunThreadV2_PostWash = null;
                    break;
                case WashOption.ManualPostWash: //Manual Seq + Post
                    //Seq Wash
                    washRecipeDir = _RecipeBuildSettings.SeqWashRecipePath;
                    str = parametrs.SessionId;// DateTime.Now.ToString("yyMMdd-HHmmss");
                    recipeDir = SeqApp.CreateTempRecipeLocation(str);
                    originalRecipeDir = _RecipeBuildSettings.RecipeBaseDir;
                    newRecipeFullFilePath = SeqApp.SaveRecipeToNewPath(washRecipeDir, recipeDir, "ManualPostWash_Seq.xml", originalRecipeDir);
                    Recipe ManualPostWashRecipe_Seq = Recipe.LoadFromXmlFile(newRecipeFullFilePath);

                    loadCartridge = false;
                    _IsPostWashSuccess = false;
                    _RecipeParam = new RecipeThreadParameters()
                    {

                        SelectedTemplate = TemplateOptions.ecoli,
                        IsSimulation = IsSimulationMode,
                        LoadCartridge = loadCartridge
                    };
                    _RecipeRunThreadV2_PostWash = new RecipeRunThreadV2(
                                SeqApp.TheDispatcher,
                                SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                                ManualPostWashRecipe_Seq,
                                SeqApp.EthernetCameraA,
                                SeqApp.EthernetCameraB,
                                SeqApp.MotionController,
                                SeqApp.MainboardDevice,
                                SeqApp.LEDController,
                                SeqApp.FluidicsInterface,
                                _RecipeParam, null,
                                null, //no OLA
                                false, //no wait for OLA completion
                                null, //no image backup
                                false
                                );
                    _RecipeRunThreadV2_PostWash.Completed += _RecipeRunThread_PostWash_Completed;
                    _RecipeRunThreadV2_PostWash.InProgress += _RecipeRunThread_PostWash_InProgress;
                    _RecipeRunThreadV2_PostWash.Name = "ManualPostWashRecipe_Seq";
                    _RecipeRunThreadV2_PostWash.IsSimulationMode = IsSimulationMode;
                    _RecipeRunThreadV2_PostWash.Start();
                    _RecipeRunThreadV2_PostWash.WaitForCompleted();
                    _RecipeRunThreadV2_PostWash = null;

                    //Load wash cart
                    if (!ReLoadCart()) { return false; }
                    //Post Wash
                    PostWash(parametrs);
                    break;
            }

            bool b = _IsPostWashSuccess;
            return b;
        }
        private void PostWash(RunWashParams parametrs)
        {
            //Raise and lower sipper 3x 
            //for (int i = 0; i < 3; i++)
            //{
            //    UnloadCart();
            //    Thread.Sleep(100);
            //    //Lower Sipper
            //    int pos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            //    int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            //    int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));

            //    if (IsSimulationMode)
            //    {
            //        SeqApp.UpdateAppMessage("Sim: Load Cartridge");
            //        Thread.Sleep(2000);
            //    }
            //    else
            //    {
            //        if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
            //        {
            //            Thread.Sleep(100);
            //            SeqApp.UpdateAppErrorMessage("Load Cartridge failed, retry");
            //            if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
            //            {
            //                SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
            //                return false;
            //            }
            //        }
            //    }
            //}
            //SeqApp.FluidicsInterface.OnPumpStatusUpdated += FluidicsInterface_OnPumpStatusUpdated;
            _PressureList = new List<double>[23];
            _FlowrateList = new List<double>[23];
            for (int i = 0; i < _PressureList.Length; i++)
            {
                _PressureList[i] = new List<double>();
                _FlowrateList[i] = new List<double>();
            }
            _ErrorSoultionList = new List<int>();
            string _WashRecipeDir = _RecipeBuildSettings.PostWashRecipePath;
            //Recipe SeprePrimingRecipe = Recipe.LoadFromXmlFile(primingRecipeDir);
            string str = parametrs.SessionId;// DateTime.Now.ToString("yyMMdd-HHmmss");
            string recipeDir = SeqApp.CreateTempRecipeLocation(str);
            string originalRecipeDir = _RecipeBuildSettings.RecipeBaseDir;
            string recipename = parametrs.SelectedWashOption.ToString() + ".xml";
            string newRecipeFullFilePath = SeqApp.SaveRecipeToNewPath(_WashRecipeDir, recipeDir, recipename, originalRecipeDir);
            Recipe PostWashRecipe = Recipe.LoadFromXmlFile(newRecipeFullFilePath);

            bool loadCartridge = false;
            _IsPostWashSuccess = false;
            RecipeThreadParameters _RecipeParam = new RecipeThreadParameters()
            {

                SelectedTemplate = TemplateOptions.ecoli,
                IsSimulation = IsSimulationMode,
                LoadCartridge = loadCartridge
            };
            _RecipeRunThreadV2_PostWash = new RecipeRunThreadV2(
                        SeqApp.TheDispatcher,
                        SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                        PostWashRecipe,
                        SeqApp.EthernetCameraA,
                        SeqApp.EthernetCameraB,
                        SeqApp.MotionController,
                        SeqApp.MainboardDevice,
                        SeqApp.LEDController,
                        SeqApp.FluidicsInterface,
                        _RecipeParam, null,
                        null, //no OLA
                        false, //no wait for OLA completion
                        null, //no image backup
                        false
                        );
            _RecipeRunThreadV2_PostWash.Completed += _RecipeRunThread_PostWash_Completed;
            _RecipeRunThreadV2_PostWash.InProgress += _RecipeRunThread_PostWash_InProgress;
            _RecipeRunThreadV2_PostWash.Name = "PostWashRecipe";
            _RecipeRunThreadV2_PostWash.IsSimulationMode = IsSimulationMode;
            _LogTimer = new System.Timers.Timer(1000);
            _LogTimer.Elapsed += _LogTimer_Elapsed;
            _LogTimer.AutoReset = true;

            //_RecipeRunThreadV2_PostWash.PumpLogStart += _RecipeRunThreadV2_PostWash_PumpLogStart;
            _RecipeRunThreadV2_PostWash.Start();
            _RecipeRunThreadV2_PostWash.WaitForCompleted();
            _RecipeRunThreadV2_PostWash = null;
            _LogTimer?.Stop();
            _LogTimer?.Dispose();
            //SeqApp.FluidicsInterface.OnPumpStatusUpdated -= FluidicsInterface_OnPumpStatusUpdated;
            if (!IsSimulationMode)
            {
                //SaveTestData();
                //FullFlowTest();
            }
        }
        private bool ReLoadCart()
        {
            Chiller _Chiller = Chiller.GetInstance();
            UnloadCart();
            SeqApp.NotifyError("Please reload new cartridge, then click OK to load cartridge");
            bool isloadsuccess;
            int trycount = 0;
            do
            {
                _Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
                if (!_Chiller.CartridgeDoor && !IsSimulationMode) // is closed
                {
                    SeqApp.NotifyError("Please close the reagent door");
                    isloadsuccess = false;
                }
                else { isloadsuccess = true; }
                if (!_Chiller.CartridgePresent && !IsSimulationMode)
                {
                    SeqApp.NotifyError("Please push back the reagent more");
                    isloadsuccess = false;
                }
                else { isloadsuccess = true; }
                trycount++;
                if (trycount > 5)
                {
                    SeqApp.NotifyError("Unable to load new Maintenance Wash cartridge");
                    _IsPostWashSuccess = false;
                    return false;
                }
            }
            while (!isloadsuccess);
            //Lower Sipper
            int pos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));

            if (IsSimulationMode)
            {
                SeqApp.UpdateAppMessage("Sim: Load Cartridge");
                Thread.Sleep(2000);
            }
            else
            {
                if (!SeqApp.MotionController.IsCartridgeAvailable)
                {
                    if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos) == false)
                    {
                        SeqApp.UpdateAppErrorMessage("Load Cartridge failed, retry");
                        if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos) == false)
                        {
                            SeqApp.UpdateAppErrorMessage("Load Cartridge failed.");
                            return false;
                        }
                    }
                    _Chiller.GetChillerMotorPos();
                    int retry = 0;
                    while (_Chiller.CartridgeMotorPos != SettingsManager.ConfigSettings.FluidicsCalibSettings.WashCartPos)
                    {
                        if (++retry > 140)
                        {
                            SeqApp.UpdateAppErrorMessage("Load Cartridge timeout");
                            return false;
                        }
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                    {
                        //Thread.Sleep(100);
                        //SeqApp.UpdateAppErrorMessage("Load Cartridge failed, retry");
                        //if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                        //{
                        SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
                        return false;
                        //}
                    }
                }
            }
            return true;
        }
        private void _RecipeRunThread_PostWash_InProgress(object sender, EventArgs e)
        {
            //int progressDelta = 1;

            //UpdateCheckHardwareProgress(CheckHardwareEnum.Fluidics, CheckHardwareProgressMessageEnum.Progress, progressDelta);
        }

        private void _RecipeRunThread_PostWash_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            string str = "";
            ProgressTypeEnum washStatus = ProgressTypeEnum.None;
            if (_RecipeRunThreadV2_PostWash.ExitStat == ThreadBase.ThreadExitStat.None)
            {
                _IsPostWashSuccess = true;
                str = "SeprePriming Finished";
                washStatus = ProgressTypeEnum.Completed;
            }
            else if (_RecipeRunThreadV2_PostWash.ExitStat == ThreadBase.ThreadExitStat.Error)
            {
                _IsPostWashSuccess = false;
                str = "SeprePriming Failed";
                washStatus = ProgressTypeEnum.Failed;
            }
            if (_RecipeRunThreadV2_PostWash.IsAbort)
            {
                str = "SeprePriming Aborted";
                _IsPostWashSuccess = false;
                washStatus = ProgressTypeEnum.Aborted;
            }
            SeqApp.UpdateAppMessage(new AppRunWashStatus() { Message = str, WashStatus = washStatus },
              AppMessageTypeEnum.Status);
            _RecipeRunThreadV2_PostWash.Completed -= _RecipeRunThread_PostWash_Completed;
            // _RecipeRunThreadV2_PostWash.PumpLogStart -= _RecipeRunThreadV2_PostWash_PumpLogStart;
        }

        private void _LogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SeqApp.FluidicsInterface.GetFluidControllerStatus();
            for (int i = 0; i < 10; i++)
            {
                _PressureList[_CurrentSelectorPos - 1].Add(Math.Round(FluidController.GetInstance().PressureArray[i], 2));
                _FlowrateList[_CurrentSelectorPos - 1].Add(Math.Round(FluidController.GetInstance().FlowArray[i], 2));
            }
        }
        private void SaveTestData()
        {
#region save as csv
            //if (_PressureList[0]?.Count > 0)
            //{
            //    try
            //    {
            //        string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            //        string logDir = Path.Combine(commonAppData, "Sequlite\\FlowTestLog"); //to do read it from config file
            //        if (!Directory.Exists(logDir))
            //        {
            //            DirectoryInfo di = Directory.CreateDirectory(logDir);
            //            if (Directory.Exists(logDir))
            //            {
            //                logDir = di.FullName;
            //            }
            //            else
            //            {
            //                logDir = string.Empty;
            //            }
            //        }
            //        string path = string.Empty;


            //        string logFilePrefix = "FlowFullTest" + "-";
            //        ulong index = 0;
            //        ulong tempIndex;

            //        string fileName;
            //        foreach (var it in Directory.EnumerateFiles(logDir, "*.csv", SearchOption.TopDirectoryOnly))
            //        {
            //            fileName = Path.GetFileNameWithoutExtension(it);
            //            if (fileName.StartsWith(logFilePrefix))
            //            {
            //                if (UInt64.TryParse(fileName.Substring(logFilePrefix.Length), out tempIndex))
            //                {
            //                    if (index < tempIndex)
            //                    {
            //                        index = tempIndex;
            //                    }
            //                }
            //            }
            //        }
            //        index++;
            //        path = logFilePrefix + index + ".csv";
            //        path = Path.Combine(logDir, path);
            //        FileStream aFile = new FileStream(path, FileMode.Create);
            //        StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
            //        sw.WriteLine(string.Format("Sample Time/Sec,Pressure,FlowRate"));

            //        for(int k = 0; k < _PressureList.Length; k++)
            //        {
            //            List<double> pressureList = _PressureList[k];
            //            List<double> flowrateList = _FlowrateList[k];
            //            sw.WriteLine(string.Format("Solution: {0}", k + 1));
            //            for (int i = 0; i < pressureList.Count; i++)
            //            {
            //                sw.WriteLine(string.Format("{0},{1}", pressureList[i], flowrateList[i]));
            //            }
            //        }

            //        sw.Close();
            //    }
            //    catch (Exception e)
            //    {
            //        SeqApp.UpdateAppErrorMessage(e.ToString());
            //    }
            //}
#endregion save as csv

#region save in sys log
            if (_PressureList[0]?.Count > 0)
            {
                for (int i = 0; i < _PressureList.Count(); i++)
                {
                    SeqApp.UpdateAppMessage(string.Format("Solution {0}:", i + 1));
                    string pressuredata = string.Join(", ", _PressureList[i]);
                    string flowdata = string.Join(", ", _FlowrateList[i]);
                    SeqApp.UpdateAppMessage(string.Format("Pressure data:{0}", pressuredata));
                    SeqApp.UpdateAppMessage(string.Format("Flowrate data:{0}", flowdata));
                }
            }
#endregion save in sys log
        }
        private bool FullFlowTest()
        {
            // Calculate the avg pressure and compare baseline
            for (int i = 0; i < _PressureList.Length; i++)
            {
                double avgpressure = _PressureList[i].Count > 0 ? _PressureList[i].Average() : 0.0;
                double avgflow = _FlowrateList[i].Count > 0 ? _FlowrateList[i].Average() : 0.0;
                // Perform the Sum of (value-avg)_2_2.      
                double sum = _FlowrateList[i].Sum(d => Math.Pow(d - avgflow, 2));
                // Put it all together.      
                double stdflow = Math.Sqrt((sum) / (_FlowrateList.Count() - 1));
                double _CalibPressure = SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FCTestPressureCalib;
                SeqApp.UpdateAppMessage(string.Format("Solution {0}: Avg Pressure: {1:00.00}, Avg Flowrate: {2:00.00}, STDV Flowrate: {3:00.00}", i + 1, avgpressure, avgflow, stdflow));
                if (avgpressure != 0 && avgflow != 0 && (Math.Abs(avgpressure - _CalibPressure) > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.PressureTole
                    || Math.Abs(avgflow - 1) > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FlowRateTole
                    || stdflow > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.FlowRateStdTole))
                {
                    _ErrorSoultionList.Add(i + 1);
                    SeqApp.UpdateAppMessage(string.Format("Lane {0} has abnormal pressure/flowrate", i + 1));
                }
            }
            //Push back if in error list
            foreach (int errorsolution in _ErrorSoultionList)
            {
                //PULLING
                PumpingSettings PumpSettings = new PumpingSettings();
                PumpSettings.SelectedPushPath = PathOptions.Bypass;
                PumpSettings.SelectedMode = ModeOptions.Pull;
                PumpSettings.PullRate = 2000;
                PumpSettings.SelectedSolution = new ValveSolution() { ValveNumber = 23 };
                PumpSettings.PumpingVolume = 500;
                for (int k = 0; k < 4; k++)
                {
                    PumpSettings.PumpPullingPaths[k] = false;
                }
                PumpSettings.SelectedPullValve2Pos = 6;
                PumpSettings.SelectedPullValve3Pos = 3;
                //SeqApp.FluidicsInterface.OnPumpingInProgress += FlowCheck_FluidicsInterface_OnPumpingInProgress;
                SeqApp.FluidicsInterface.OnPumpingCompleted += FlowCheck_RunPuming_Completed;
                SeqApp.FluidicsInterface.RunPumping(null, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, true, IsSimulationMode);
                SeqApp.FluidicsInterface.WaitForPumpingCompleted();
                //PUSHING
                PumpSettings = new PumpingSettings();
                PumpSettings.SelectedPushPath = PathOptions.Test1;
                PumpSettings.SelectedMode = ModeOptions.Push;
                PumpSettings.PushRate = 1000;
                PumpSettings.SelectedSolution = new ValveSolution() { ValveNumber = errorsolution };
                PumpSettings.PumpingVolume = SeqApp.FluidicsInterface.Pump.PumpAbsolutePos / SettingsManager.ConfigSettings.PumpIncToVolFactor;
                for (int k = 0; k < 4; k++)
                {
                    PumpSettings.PumpPushingPaths[k] = false;
                    if (k == 0)
                    {
                        PumpSettings.PumpPushingPaths[k] = true;
                    }
                }
                PumpSettings.SelectedPushValve2Pos = 1;
                PumpSettings.SelectedPushValve3Pos = 2;
                //SeqApp.FluidicsInterface.OnPumpingInProgress += FlowCheck_FluidicsInterface_OnPumpingInProgress;
                SeqApp.FluidicsInterface.OnPumpingCompleted += FlowCheck_RunPuming_Completed;
                SeqApp.FluidicsInterface.RunPumping(null, SettingsManager.ConfigSettings.PumpIncToVolFactor, PumpSettings, true, IsSimulationMode);
                SeqApp.FluidicsInterface.WaitForPumpingCompleted();
            }
            return true;
        }
        private void FlowCheck_RunPuming_Completed(ThreadBase sender, ThreadBase.ThreadExitStat exitState)
        {
            SeqApp.FluidicsInterface.OnPumpingCompleted -= FlowCheck_RunPuming_Completed;
            //SeqApp.FluidicsInterface.OnPumpingInProgress -= FlowCheck_FluidicsInterface_OnPumpingInProgress;
            //Task.Run(() =>
            //{
            //    // _TecanPump.GetPumpPos();
            //    if (exitState == ThreadBase.ThreadExitStat.Error)
            //    {
            //        _FlowCheck_RunPuming_OK = false;
            //        SeqApp.NotifyNormalError("Error occurred during pumping thread, valve failure");
            //    }
            //    else if (exitState == ThreadBase.ThreadExitStat.Abort)
            //    {
            //        _FlowCheck_RunPuming_OK = false;
            //        SeqApp.NotifyNormalError("Pumping thread aborted");
            //    }
            //    else if (exitState == ThreadBase.ThreadExitStat.None)
            //    {
            //        _FlowCheck_RunPuming_OK = true;
            //    }
            //    //_RunTecanPumingThread.Completed -= _RunTecanPumingThread_Completed;
            //    //_RunTecanPumingThread = null;
            //});
        }
        public bool CancelWashing()
        {
            _RecipeRunThreadV2_PostWash?.AbortWork();
            _RecipeRunThreadV2_PostWash?.WaitForCompleted();
            return true;
        }
    }
}
