using Sequlite.ALF.Common;
using Sequlite.ALF.Fluidics;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.MotionControl;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.CameraLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.ALF.App
{
    partial class SeqApp 
    {
        
          
        public bool Initialized { get; private set; }
        
        public bool Initialize(bool startMonitorService)
        {
            bool b1 = ConnectToMainBoardDevices();
            bool b2 = ConnectToOtherDevices();

            Initialized = b1 & b2;
            Initialized = b1 & b2;
            if (startMonitorService)
            {
                StartAppMonitor();
            }
            return Initialized;
        }

        

        public bool Unintialize()
        {
            StopAppMinitor();
            if (!IsMachineRev2)
            {
                if (PhotometricsCamera != null)
                {
                    PhotometricsCamera.Close();
                }
            }
            else
            {
                LucidCameraManager.CloseCameras();
                LucidCameraManager.OnCameraUpdated -= LucidCameraManager_OnCameraUpdated;
            }

            if (MainboardDevice != null)
            {
                if (MainboardDevice.IsConnected && !IsMachineRev2)
                {
                    MainboardDevice.SetLEDStatus(LEDTypes.Green, false);
                    MainboardDevice.SetLEDStatus(LEDTypes.Red, false);
                    MainboardDevice.SetLEDStatus(LEDTypes.White, false);
                }
                else if (LEDController != null && LEDController.IsConnected)
                {
                    LEDController.SetLEDStatus(LEDTypes.Green, false);
                    LEDController.SetLEDStatus(LEDTypes.Red, false);
                    LEDController.SetLEDStatus(LEDTypes.White, false);

                }
            }
            if (!IsMachineRev2)
            {
                MainboardDevice.SetChemiTemperCtrlStatus(false);
            }
            else
            {
                TemperatureController.GetInstance().SetControlSwitch(false);
            }
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                MainBoardController.GetInstance().SetFluidPreHeatingEnable(false);
            }
            else
            {
                Chiller.GetInstance().SetFluidHeatingEnable(false);
            }
            Initialized = false;
            return true;
        }

        private bool InitializeCamera(PhotometricsCamera _ActiveCamera)
        {
            bool IsConnected = false;
            try
            {
                if (_ActiveCamera.Open())
                {
                    IsConnected = true;

                }
                else
                {
                    IsConnected = false;
                }
            }
            catch
            {
                IsConnected = false;
            }
            return IsConnected;
        }
        //size in GB
        public bool  GetImageDriveDiskSpace(out long space, out string driveName )
        {
            bool b = true;
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            string RecipeImageDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.GetRecipeRunImagingBaseDir();
            DriveInfo RecipeImageDrive = null;
            foreach(DriveInfo drive in allDrives)
            {
                if (RecipeImageDir.Contains(drive.Name)) { RecipeImageDrive = drive; }
            }
            if (RecipeImageDrive == null)
            {
                space = 0;
                driveName = null;
                return false;
            }
            space = RecipeImageDrive.AvailableFreeSpace / 1024 / 1024 / 1024;
            driveName = RecipeImageDrive.Name;
            return b;
        }

        private bool ConnectToMainBoardDevices()
        {
            bool isAllDeviceConnected = true;
            //LaunchRecord = string.Empty;
            AddLaunchRecordMessage("Free disk space check...\n");
            long diskSpace;
            string driveName;
            GetImageDriveDiskSpace(out diskSpace, out driveName);
            AddLaunchRecordMessage(string.Format("Disk: {0} has available space: {1:F2}GB \n", driveName, diskSpace));
            //DriveInfo[] allDrives = DriveInfo.GetDrives();
            //AddLaunchRecordMessage(string.Format("Disk: {0} has available space: {1:F2}GB \n", allDrives[0].Name, (allDrives[0].AvailableFreeSpace / 1024 / 1024 / 1024)));
            //if((allDrives[0].AvailableFreeSpace / 1024 / 1024 / 1024) < 100)
            if (diskSpace < 300)
            {
                //MessageBox.Show("Please clean up disk space");
               
                SeqApp.Send(ObservableAppMessage, new AppMessage()
                {
                    MessageObject = new LowDiskSpaceMessage()
                    {
                        Mesage = "Please clean up disk space",
                        Title = "Low Disk Space"
                    },
                    MessageType = AppMessageTypeEnum.Warning
                });
            }
            Mainboard mainboardDevice = Mainboard.GetInstance();
            MainboardDevice = mainboardDevice;
            AddLaunchRecordMessage("Main Board Connection...");
            var presetMBPortName = SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.MainboardController].PortName;
            if (MainboardDevice.Connect(presetMBPortName) == false)
            {
                mainboardDevice.Connect();
            }
            if (mainboardDevice.IsConnected)
            {
                AddLaunchRecordMessage(string.Format("Succeeded, Hardware Version:{0}\n", mainboardDevice.HWVersion));
                if (mainboardDevice.HWVersion.Substring(0, 1) == "1")    // ALF 1.x machine
                {
                    AddLaunchRecordMessage("This is a 1.x machine\n");
                    IsMachineRev2 = false;
                }
                else if (mainboardDevice.HWVersion.Substring(0, 1) == "2")   // ALF2.x machine
                {
                    AddLaunchRecordMessage("This is a 2.x machine\n");
                    IsMachineRev2 = true;
                    mainboardDevice.Disconnect();

                    // for alf2.0 machine, cartridge motor motion factor should be doubled; alf2.5 or later will apply new factor value
                    if(mainboardDevice.HWVersion=="2.0.0.0" || mainboardDevice.HWVersion == "2.0.0.1")
                    {
                        SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge] *= 2;
                    }

                    AddLaunchRecordMessage("Mainboard Revision 2 connecting...");
                    MainBoardController mainBoardControllerDevice = MainBoardController.GetInstance();
                    MainBoardController = mainBoardControllerDevice;
                    if (mainBoardControllerDevice.Connect(presetMBPortName))
                    {
                        AddLaunchRecordMessage("Succeeded.\n");
                    }
                    else if (mainBoardControllerDevice.Connect())
                    {
                        AddLaunchRecordMessage("Succeeded.\n");
                    }
                    else
                    {
                        AddLaunchRecordMessage("Failed.\n", false);
                        isAllDeviceConnected = false;
                    }
                    LEDController.GetInstance().IsProtocolRev2 = mainBoardControllerDevice.IsProtocolRev2;
                    Chiller.GetInstance().IsProtocolRev2 = mainBoardControllerDevice.IsProtocolRev2;
                }
            }
            else
            {
                AddLaunchRecordMessage("Failed.\n", false);
                isAllDeviceConnected = false;
            }

            SimulationSettings simConfig = SettingsManager.ConfigSettings.SystemConfig.SimulationConfig;
            //if (simConfig?.IsSimulation == true)
            if (IsSimulation)
            {
                //IsSimulation = true;
                if (simConfig.IsMachineV2)
                {
                    IsMachineRev2 = true;
                    MainBoardController mainBoardControllerDevice = MainBoardController.GetInstance();
                    MainBoardController = mainBoardControllerDevice;
                }
                Logger?.Log($"Running simulation IsMachineRev2={IsMachineRev2}");
            }
            IsMainDevicesConnected = isAllDeviceConnected;
            return isAllDeviceConnected;
        }
        private bool ConnectEthernetCameras()
        {
            try
            {
                bool IsConnected = false;
                LucidCameraManager.OnCameraUpdated += LucidCameraManager_OnCameraUpdated;
                if (LucidCameraManager.OpenCameras())
                {
                    EthernetCameraA = LucidCameraManager.GetCamera(0);
                    EthernetCameraB = LucidCameraManager.GetCamera(1);
                    IsConnected = true;
                }
                else
                {
                    IsConnected = false;
                }
                return IsConnected;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
                return false;
            }
        }

        private void LucidCameraManager_OnCameraUpdated()
        {
            EthernetCameraA = LucidCameraManager.GetCamera(0);
            EthernetCameraB = LucidCameraManager.GetCamera(1);
        }

        private bool ConnectToOtherDevices()
        {
            IsOtherDevicesConnected = false;
            bool isAllDeviceConnected = true;
            AddLaunchRecordMessage("Z stage connecting...");
            MotionController = MotionController.GetInstance();
            if (MainBoardController.GetInstance().IsProtocolRev2)
            {
                MotionController.OnDoorCtrlRequested += MotionController.SetFCDoorStatus;
            }
            else
            {
                MotionController.OnDoorCtrlRequested += MainBoardController.GetInstance().SetDoorStatus;
            }
            bool IsZStageAlive = false;
            for (int i = 0; i < 2; i++)
            {
                MotionController.ZStageConnect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.ZStage].PortName);
                IsZStageAlive = MotionController.IsZStageConnected;
                if (IsZStageAlive)
                {
                    break;
                }
                else
                {
                    MotionController.ZStageConnect();
                    IsZStageAlive = MotionController.IsZStageConnected;
                    if (IsZStageAlive)
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            isAllDeviceConnected = IsZStageAlive;
            AddLaunchRecordMessage(string.Format("{0}\n", IsZStageAlive ? "Succeeded" : "Failed"), IsZStageAlive);

            //ICamera Camera;
            if (!IsMachineRev2)
            {
                AddLaunchRecordMessage("Camera connecting...");
                PhotometricsCamera _ActiveCamera = new PhotometricsCamera();
                _ActiveCamera.CCDCoolerSetPoint = -20;
                PhotometricsCamera = _ActiveCamera;
                //Camera = _ActiveCamera;
                if (InitializeCamera(_ActiveCamera))
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else
                {
                    AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
            }
            else
            {
                //LucidCamera _EthernetCameraA;
                //LucidCamera _EthernetCameraB;
                AddLaunchRecordMessage("Ethernet Cameras connecting...");
                if (ConnectEthernetCameras())
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else
                {
                    AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
            }

            AddLaunchRecordMessage("Motion controller connecting...");
            MotionController.UsingControllerV2 = IsMachineRev2;
            if (IsMachineRev2)
            {
                if (MainBoardController.GetInstance().HWVersion != "2.0.0.0" && MainBoardController.GetInstance().HWVersion != "2.0.0.2")
                {
                    MotionController.IsFCDoorAvailable = true;
                }
                else
                {
                    MotionController.IsFCDoorAvailable = false;
                }
                if (MainBoardController.GetInstance().IsMachineRev2P4)
                {
                    MotionController.IsCartridgeAvailable = false;
                }
                else
                {
                    MotionController.IsCartridgeAvailable = true;
                }
                MotionController.OtherStagesConnect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.MotionController].PortName);
                if (!MotionController.IsMotionConnected)
                {
                    MotionController.OtherStagesConnect();
                }
            }
            else
            {
                MotionController.OtherStagesConnect();
            }
            bool IsGalilAlive = MotionController.IsMotionConnected;
            AddLaunchRecordMessage(string.Format("{0}\n", IsGalilAlive ? "Succeeded" : "Failed"), IsGalilAlive);
            if (!IsGalilAlive)
            {
                isAllDeviceConnected = false;
            }

            FluidicsInterface = FluidicsManager.GetFluidicsInterface(IsMachineRev2 ? FluidicsVersion.V2 : FluidicsVersion.V1);
            FluidicsInterface.OnConnectionUpdated += FluidicsInterface_OnConnectionUpdated;
            if (!FluidicsInterface.Connect())
            {
                isAllDeviceConnected = false;
            }
            FluidicsInterface.OnConnectionUpdated -= FluidicsInterface_OnConnectionUpdated;

            if (IsMachineRev2)
            {
                Chiller Chiller = Chiller.GetInstance();
                // to do: connecting to RFID
                AddLaunchRecordMessage("Chiller controller connecting...");
                if (Chiller.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.Chiller].PortName))
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else if (Chiller.Connect())
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else
                {
                    AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
                if(Chiller.IsConnected && MainBoardController.IsMachineRev2P4)
                {
                    Chiller.ChillerMotorControl(false);
                }
                if (MainBoardController.GetInstance().IsProtocolRev2)
                {
                    MainBoardController.GetInstance().SetFluidPreHeatingEnable(false);
                }
                else
                {
                    Chiller.GetInstance().SetFluidHeatingEnable(false);
                }

                LEDController = LEDController.GetInstance();
                AddLaunchRecordMessage("LED Controller connecting...");
                if (LEDController.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.LEDController].PortName))
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else if (LEDController.Connect())
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                }
                else
                {
                    AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
                if (LEDController.IsConnected)
                {
                    LEDController.SetLEDControlledByCamera(LEDTypes.Green, false);
                    LEDController.SetLEDControlledByCamera(LEDTypes.Red, false);
                    LEDController.SetLEDControlledByCamera(LEDTypes.White, false);
                    LEDController.SetLEDIntensity(LEDTypes.Green, 50);
                    LEDController.SetLEDIntensity(LEDTypes.Red, 50);
                    LEDController.SetLEDIntensity(LEDTypes.White, 50);

                    // identify the cameras of each channel(G1/R3 and G2/R4)
                    LEDController.GetCameraMap();
                    foreach(var camera in LucidCameraManager.GetAllCameras())
                    {
                        if (camera.SerialNumber == LEDController.G1R3CameraSN.ToString())
                        {
                            camera.Channels = "G1/R3";
                        }
                        else if (camera.SerialNumber == LEDController.G2R4CameraSN.ToString())
                        {
                            camera.Channels = "G2/R4";
                        }
                        else
                        {
                            camera.Channels = "NA";
                        }
                    }
                }
#if  !DisableBarCodeReader
                if (MotionControl.MotionController.GetInstance().IsBarcodeReaderEnabled)
                {
                    BarCodeReader BarCodeReader = BarCodeReader.GetInstance();
                    AddLaunchRecordMessage("Barcode reader connecting...");
                    if (BarCodeReader.Connect(SettingsManager.ConfigSettings.SerialCommDeviceSettings[SerialDeviceTypes.BarCodeReader].PortName))
                    {
                        AddLaunchRecordMessage("Succeeded.\n");
                    }
                    else if (BarCodeReader.Connect())
                    {
                        AddLaunchRecordMessage("Succeeded.\n");
                    }
                    else
                    {
                        AddLaunchRecordMessage("Failed.\n", false);
                        isAllDeviceConnected = false;
                    }
                }
#endif
                AddLaunchRecordMessage("FC Temperature controller connecting...");
                if (TemperatureController.GetInstance().Connect())
                {
                    AddLaunchRecordMessage("Succeeded.\n");
                    if (MainBoardController.IsProtocolRev2)
                    {
                        MainBoardController.SetTemperCtrlParameters(SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP,
                            SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI,
                            SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD,
                            SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain,
                            SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain);
                    }
                    else
                    {
                        TemperatureController.GetInstance().SetCtrlP(SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlP);
                        TemperatureController.GetInstance().SetCtrlI(SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlI);
                        TemperatureController.GetInstance().SetCtrlD(SettingsManager.ConfigSettings.FCTemperCtrlSettings.CtrlD);
                        TemperatureController.GetInstance().SetHeaterGain(SettingsManager.ConfigSettings.FCTemperCtrlSettings.HeatGain);
                        TemperatureController.GetInstance().SetCoolerGain(SettingsManager.ConfigSettings.FCTemperCtrlSettings.CoolGain);
                        TemperatureController.GetInstance().SetControlSwitch(false);
                    }
                }
                else
                {
                    AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
            }
            Logger.Log("SUMMARY:\n" + LaunchRecord, SeqLogFlagEnum.STARTUP);
            IsOtherDevicesConnected = isAllDeviceConnected;
            return IsOtherDevicesConnected;
        }

        private void FluidicsInterface_OnConnectionUpdated(object sender, ComponentConnectionEventArgs e)
        {
            AddLaunchRecordMessage(e.Message, e.IsErrorMessage);
        }

        bool IsMainDevicesConnected { get; set; }
        bool IsOtherDevicesConnected { get; set; }
        string _LaunchRecord;
        private string LaunchRecord
        {
            get { return _LaunchRecord; }
            set
            {
                if (_LaunchRecord != value)
                {
                    _LaunchRecord = value;
                }
            }
        }

        private void AddLaunchRecordMessage(string str, bool success = true)
        {
            if (Logger != null)
            {
                if (!success)
                {
                    Logger.LogError(str, SeqLogFlagEnum.STARTUP);
                }
                else
                {
                    Logger.Log(str, SeqLogFlagEnum.STARTUP);
                }
            }
            LaunchRecord += str;
        }

        private bool _IsMachineRev2;
        public bool IsMachineRev2
        {
            get { return _IsMachineRev2; }
            private set
            {
                if (_IsMachineRev2 != value)
                {
                    _IsMachineRev2 = value;
                }
            }
        }

        //public bool IsSimulation { get; private set; }


        public Mainboard MainboardDevice { get; private set; }
        public MotionController MotionController { get; private set; }

        public PhotometricsCamera PhotometricsCamera { get; private set; }

        public ILucidCamera EthernetCameraA { get; private set; }

        public ILucidCamera EthernetCameraB { get; private set; }

        public IFluidics FluidicsInterface { get; private set; }
        public LEDController LEDController { get; private set; }
        public BarCodeReader BarCodeReader { get; private set; }

        public MainBoardController MainBoardController { get; private set; }
    }
}
