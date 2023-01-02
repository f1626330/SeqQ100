using Sequlite.ALF.Common;
using Sequlite.ALF.EngineerGUI.View;
using Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.RecipeLib;
using Sequlite.ALF.SerialPeripherals;
using Sequlite.ALF.Fluidics;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;
using Sequlite.ALF.App;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    //this class implements a main window view model
    public class Workspace : ViewModelBase, 
        IViewModelStatus, ICameraStatus, IStepParameters
    {
        
        public ISeqFileLog Logger { get; private set; }
        public bool IsMainDevicesConnected { get; private set; }
        public bool IsOtherDevicesConnected { get; private set; }
        // public MainWindow Owner { get; set; }
        public ObservableCollection<IDialogBoxViewModel> Dialogs { get; private set; }
        public CameraViewModel CameraVM { get; private set; }
        public MotionViewModel MotionVM { get; private set; }
        public DetectorParametersViewModel DetectorParametersVM { get; private set; }
        public MainBoardViewModel MainBoardVM { get; private set; }
        public ImageGalleryViewModel ImageGalleryVM { get; private set; }
        
        FluidicsViewModel _FluidicsVM = null;
        public FluidicsViewModel FluidicsVM
        {
            get
            {
                return _FluidicsVM;
            }
            set
            {
                if (_FluidicsVM != value)
                {
                    _FluidicsVM = value;
                    RaisePropertyChanged("FluidicsVM");
                }
            }
        }

        public ChemistryViewModel ChemistryVM { get; private set; }
        public RecipeViewModel RecipeVM { get; private set; }
        public AutoFocusViewModel AutoFocusVM { get; private set; }
        public CustomerViewModel CustomerVM { get; private set; }
        public BarCodeReaderViewModel BarCodeReaderVm { get; private set; }

        public RecipeToolRecipeViewModel RecipeToolRecipeVM { get; private set; }
        public RecipeStepViewModel NewStepVM { get; private set; }
        public StepManipulationViewModel StepManipulationVM { get; private set; }
        public LedPdCalibrationViewModel LedPdCalibrationVm { get; private set; }
        public HardwareVerifyViewModel HwVerifyVm { get; private set; }
        //public Recipe NewRecipe { get; set; }
        
        ISeqApp SeqApp { get; set; }
        ISystemInit SystemInitApp { get; set; }
        public Workspace(ISeqFileLog logger)
        {
            Logger = logger;
            Dialogs = new ObservableCollection<IDialogBoxViewModel>();
            BindingOperations.EnableCollectionSynchronization(Dialogs, new object());
            FluidicsVMList = new Dictionary<FluidicsVersion, FluidicsViewModel>();
        }

        public Workspace(ISeqFileLog logger, ObservableCollection<IDialogBoxViewModel> dialog)
        {
            Logger = logger;
            Dialogs = dialog;
            FluidicsVMList = new Dictionary<FluidicsVersion, FluidicsViewModel>();
        }

        public bool CanClose
        {
            get
            {
                bool _canClose = true;
                //test
                //var option1 = MessageBox.Show("Image unsaved, are you sure you want to close this application.", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                //if (option1 == MessageBoxResult.Cancel)
                //{
                //    _canClose = false;
                //}

                foreach (var file in ImageGalleryVM.Files)
                {
                    if (file.IsDirty)
                    {
                        var option = MessageBox.Show("Image unsaved, are you sure you want to close this application.", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                        if (option == MessageBoxResult.Cancel)
                        {
                            _canClose = false;
                            break;
                        }
                        break;
                    }
                }
                return _canClose;
            }
           
        }
        private ICommand _WindowClosing = null;
        public ICommand WindowClosing
        {
            get
            {
                if (_WindowClosing == null)
                {
                    _WindowClosing = new RelayCommand(o => Closeing(o), o=> CanClose);
                }
                return _WindowClosing;
            }
        }

        private ICommand _WindowClosed = null;
        public ICommand WindowClosed
        {
            get
            {
                if (_WindowClosed == null)
                {
                    _WindowClosed = new RelayCommand(o => Close());
                }
                return _WindowClosed;
            }
        }

        //can be called before main window initializecomponents
        public void  CreatViewModels(ISeqApp seqApp)
        {
            SeqApp = seqApp;
            SystemInitApp = seqApp.GetSystemInitInterface();
            IsMachineRev2 = SystemInitApp.IsMachineRev2;
            IsMachineRev2P4 = MainBoardController.GetInstance().IsMachineRev2P4;

            //ConnectToMainBoardDevices();
            CustomerVM = new CustomerViewModel();
            ImageGalleryVM = new ImageGalleryViewModel();
            BarCodeReaderVm = new BarCodeReaderViewModel();
            ChemistryVM = new ChemistryViewModel(IsMachineRev2, this);
            FluidicsVM = FindOrCreateFluidicsVM(IsMachineRev2 ? FluidicsVersion.V2 : FluidicsVersion.V1, ChemistryVM);
            MotionVM = new MotionViewModel(FluidicsVM, IsMachineRev2, IsMachineRev2P4);
            MainBoardVM = new MainBoardViewModel(IsMachineRev2, ChemistryVM, FluidicsVM);
            DetectorParametersVM = new DetectorParametersViewModel(IsMachineRev2, MotionVM, MainBoardVM);
            CameraVM = new CameraViewModel(IsMachineRev2, ProductVersion, this, MainBoardVM, ImageGalleryVM, MotionVM);
            RecipeVM = new RecipeViewModel(SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig,
                IsMachineRev2, this, MotionVM, CameraVM, ChemistryVM, FluidicsVM, MainBoardVM);
            RecipeVM.IsSimulationMode = SeqApp?.IsSimulation == true;
            AutoFocusVM = new AutoFocusViewModel(CameraVM, RecipeVM, MotionVM, MainBoardVM);
            //NewRecipe = new Recipe(IsMachineRev2);
            RecipeToolRecipeVM = new RecipeToolRecipeViewModel(IsMachineRev2, this);
            NewStepVM = new RecipeStepViewModel(IsMachineRev2, MainBoardVM.MainBoardController.IsProtocolRev2);
            StepManipulationVM = new StepManipulationViewModel(this, RecipeToolRecipeVM, NewStepVM);
            LedPdCalibrationVm = new LedPdCalibrationViewModel(MainBoardVM.LEDController);
            HwVerifyVm = new HardwareVerifyViewModel(CameraVM, ImageGalleryVM);

            CameraVM.Initialize();
            ChemistryVM.Initialize();
           // FluidicsVM.Initialize();

            MotionVM.FMotionSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Speed;
            MotionVM.FMotionAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Accel;
            MotionVM.FMotionAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Absolute;
            MotionVM.FMotionPosShift = (int)SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Filter].Relative;
            MotionVM.YMotionSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed;
            MotionVM.YMotionAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel;
            MotionVM.YMotionAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Absolute;
            MotionVM.YMotionPosShift = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Relative;
            MotionVM.ZMotionSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Speed;
            MotionVM.ZMotionAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Accel;
            MotionVM.ZMotionAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Absolute;
            MotionVM.ZMotionPosShift = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.ZStage].Relative;
            MotionVM.XMotionSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed;
            MotionVM.XMotionAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel;
            MotionVM.XMotionAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Absolute;
            MotionVM.XMotionPosShift = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Relative;
            MotionVM.CMotionSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed;
            MotionVM.CMotionAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel;
            MotionVM.CMotionAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Absolute;
            MotionVM.CMotionPosShift = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Relative;
            MotionVM.FCDoorSpeed = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Speed;
            MotionVM.FCDoorAccel = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Accel;
            MotionVM.FCDoorAbsolutePos = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Absolute;
            MotionVM.FCDoorPosShift = SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.FCDoor].Relative;

            MotionVM.FMotionLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].MotionRange.LimitHigh;
            MotionVM.FMotionLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].MotionRange.LimitLow;
            MotionVM.YMotionLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].MotionRange.LimitHigh;
            MotionVM.YMotionLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].MotionRange.LimitLow;
            MotionVM.ZMotionLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].MotionRange.LimitHigh;
            MotionVM.ZMotionLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].MotionRange.LimitLow;
            MotionVM.XMotionLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.XStage].MotionRange.LimitHigh;
            MotionVM.XMotionLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.XStage].MotionRange.LimitLow;
            MotionVM.CMotionLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Cartridge].MotionRange.LimitLow;
            MotionVM.CMotionLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Cartridge].MotionRange.LimitHigh;
            MotionVM.FCDoorLimitL = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitLow;
            MotionVM.FCDoorLimitH = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.FCDoor].MotionRange.LimitHigh;

            MotionVM.CMotionCoeff = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge];
            MotionVM.YMotionCoeff = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage];
            MotionVM.FMotionCoeff = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Filter];
            MotionVM.XMotionCoeff = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage];
            MotionVM.FCDoorCoeff = SettingsManager.ConfigSettings.MotionFactors[MotionTypes.FCDoor];

            int greenMaxOnTime = SettingsManager.ConfigSettings.LEDMaxOnTimes[LEDTypes.Green];
            int redMaxOnTime = SettingsManager.ConfigSettings.LEDMaxOnTimes[LEDTypes.Red];
            int whiteMaxOnTime = SettingsManager.ConfigSettings.LEDMaxOnTimes[LEDTypes.White];
            if (greenMaxOnTime > 0)
            {
                MainBoardVM.MainBoard.GLEDMaxOnTime = greenMaxOnTime;
            }
            if (redMaxOnTime > 0)
            {
                MainBoardVM.MainBoard.RLEDMaxOnTime = redMaxOnTime;
            }
            if (whiteMaxOnTime > 0)
            {
                MainBoardVM.MainBoard.WLEDMaxOnTime = whiteMaxOnTime;
            }
            AutoFocusVM.Initialize();

            InitializeViewModels(SystemInitApp);
        }
        

        private void _DispatcherTimer_Tick(object sender, EventArgs e)
        {
            //double estTimeRemain = 0;
            //double percentCompete = 0;
            //double estimatedCaptureTimeInSec = 0;

            _EstimatedRemainingTime = string.Empty;

            if (CameraVM.WorkingStatus == CameraStatusEnums.Capture)
            {
                TimeSpan elapsedTime = DateTime.Now - _CaptureStartTime;
            }
        }
        #region RecipeTool Initialization
        public void SetStepParameterToViewModel(RecipeStepBase step, RecipetoolVM.StepsTreeViewModel viewModel)
        {
            switch (step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    ((SetTemperStepViewModel)viewModel).SetTemperature = ((SetTemperStep)step).TargetTemper;
                    ((SetTemperStepViewModel)viewModel).TemperTolerance = ((SetTemperStep)step).Tolerance;
                    ((SetTemperStepViewModel)viewModel).TemperDuration = ((SetTemperStep)step).Duration;
                    ((SetTemperStepViewModel)viewModel).WaitForTemperCtrlFinished = ((SetTemperStep)step).WaitForComplete;
                    ((SetTemperStepViewModel)viewModel).TemperCtrlP = ((SetTemperStep)step).CtrlP;
                    ((SetTemperStepViewModel)viewModel).TemperCtrlI = ((SetTemperStep)step).CtrlI;
                    ((SetTemperStepViewModel)viewModel).TemperCtrlD = ((SetTemperStep)step).CtrlD;
                    ((SetTemperStepViewModel)viewModel).TemperCtrlHeatGain = ((SetTemperStep)step).CtrlHeatGain;
                    ((SetTemperStepViewModel)viewModel).TemperCtrlCoolGain = ((SetTemperStep)step).CtrlCoolGain;
                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    ((SetPreHeatTemperStepViewModel)viewModel).SetTemperature = ((SetPreHeatTempStep)step).TargetTemper;
                    ((SetPreHeatTemperStepViewModel)viewModel).TemperTolerance = ((SetPreHeatTempStep)step).Tolerance;
                    ((SetPreHeatTemperStepViewModel)viewModel).WaitForTemperCtrlFinished = ((SetPreHeatTempStep)step).WaitForComplete;
                    break;
                case RecipeStepTypes.Imaging:
                    ((ImagingStepViewModel)viewModel).IsAutoFocusOn = ((ImagingStep)step).IsAutoFocusOn;
                    ((ImagingStepViewModel)viewModel).AddedRegions = new ObservableCollection<ImagingRegionViewModel>();
                    foreach (var region in ((ImagingStep)step).Regions)
                    {
                        ImagingRegionViewModel regionVm = new ImagingRegionViewModel(IsMachineRev2);
                        regionVm.Index = region.RegionIndex;
                        regionVm.Lane = region.Lane;
                        regionVm.Column = region.Column;
                        regionVm.Row = region.Row;

                        foreach (var imaging in region.Imagings)
                        {
                            ImageSettingsViewModel imagingVm = new ImageSettingsViewModel();
                            switch (imaging.Channels)
                            {
                                case ImagingChannels.Green:
                                    imagingVm.SelectedChannel = imagingVm.ChannelOptions.Find(p => p == "[G]");
                                    imagingVm.GreenExposure = imaging.GreenExposureTime;
                                    imagingVm.GreenIntensity = imaging.GreenIntensity;
                                    break;
                                case ImagingChannels.Red:
                                    imagingVm.SelectedChannel = imagingVm.ChannelOptions.Find(p => p == "[R]");
                                    imagingVm.RedExposure = imaging.RedExposureTime;
                                    imagingVm.RedIntensity = imaging.RedIntensity;
                                    break;
                                case ImagingChannels.RedGreen:
                                    imagingVm.SelectedChannel = imagingVm.ChannelOptions.Find(p => p == "[R,G]");
                                    imagingVm.RedExposure = imaging.RedExposureTime;
                                    imagingVm.RedIntensity = imaging.RedIntensity;
                                    imagingVm.GreenExposure = imaging.GreenExposureTime;
                                    imagingVm.GreenIntensity = imaging.GreenIntensity;
                                    break;
                            }
                            imagingVm.SelectedFilter = imaging.Filter;
                            regionVm.Imagings.Add(imagingVm);
                        }

                        foreach (var focus in region.ReferenceFocuses)
                        {
                            FocusViewModel focusVm = new FocusViewModel();
                            focusVm.FocusName = focus.Name;
                            focusVm.FocusPos = focus.Position;
                            regionVm.RefFocuses.Add(focusVm);
                        }
                        ((ImagingStepViewModel)viewModel).AddedRegions.Add(regionVm);
                    }
                    break;
                case RecipeStepTypes.MoveStage:
                    ((MoveStageStepViewModel)viewModel).MoveStageRegion = ((MoveStageStepViewModel)viewModel).RegionOptions.Find(val => val == ((MoveStageStep)step).Region);
                    break;
                case RecipeStepTypes.MoveStageRev2:
                    ((MoveStageStepVMRev2)viewModel).SelectedLane = ((MoveStageStepVMRev2)viewModel).LaneOptions.Find(val => val == ((MoveStageStepRev2)step).Lane);
                    ((MoveStageStepVMRev2)viewModel).SelectedRow = ((MoveStageStepVMRev2)viewModel).RowOptions.Find(val => val == ((MoveStageStepRev2)step).Row);
                    ((MoveStageStepVMRev2)viewModel).SelectedColumn = ((MoveStageStepVMRev2)viewModel).ColumnOptions.Find(val => val == ((MoveStageStepRev2)step).Column);
                    break;
                case RecipeStepTypes.Pumping:
                    ((PumpingStepViewModel)viewModel).SelectedPumpingType = ((PumpingStepViewModel)viewModel).PumpingTypeOptions.Find(p => p.Mode == ((PumpingStep)step).PumpingType);
                    ((PumpingStepViewModel)viewModel).PumpPullingRate = ((PumpingStep)step).PullRate;
                    ((PumpingStepViewModel)viewModel).PumpPushingRate = ((PumpingStep)step).PushRate;
                    ((PumpingStepViewModel)viewModel).SelectedPullingPath = ((PumpingStepViewModel)viewModel).PumpingPathOptions.Find(p => p == ((PumpingStep)step).PullPath);
                    ((PumpingStepViewModel)viewModel).SelectedPushingPath = ((PumpingStepViewModel)viewModel).PumpingPathOptions.Find(p => p == ((PumpingStep)step).PushPath);
                    ((PumpingStepViewModel)viewModel).SelectedReagent = ((PumpingStepViewModel)viewModel).ReagentOptions.Find(p => p == ((PumpingStep)step).Reagent);
                    ((PumpingStepViewModel)viewModel).PumpingVol = ((PumpingStep)step).Volume;
                    break;
                case RecipeStepTypes.NewPumping:
                    ((PumpingStepVMRev2)viewModel).SelectedPumpingType = ((PumpingStepVMRev2)viewModel).PumpingTypeOptions.Find(p => p.Mode == ((NewPumpingStep)step).PumpingType);
                    ((PumpingStepVMRev2)viewModel).PumpPullingRate = ((NewPumpingStep)step).PullRate;
                    ((PumpingStepVMRev2)viewModel).PumpPushingRate = ((NewPumpingStep)step).PushRate;
                    ((PumpingStepVMRev2)viewModel).SelectedPullingPath = ((NewPumpingStep)step).PullPath;
                    ((PumpingStepVMRev2)viewModel).SelectedPushingPath = ((NewPumpingStep)step).PushPath;
                    ((PumpingStepVMRev2)viewModel).SelectedReagent = ((NewPumpingStep)step).Reagent;
                    ((PumpingStepVMRev2)viewModel).PumpingVol = ((NewPumpingStep)step).Volume;
                    ((PumpingStepVMRev2)viewModel).SelectedPullValve2Pos = ((NewPumpingStep)step).SelectedPullValve2Pos;
                    ((PumpingStepVMRev2)viewModel).SelectedPullValve3Pos = ((NewPumpingStep)step).SelectedPullValve3Pos;
                    ((PumpingStepVMRev2)viewModel).SelectedPushValve2Pos = ((NewPumpingStep)step).SelectedPushValve2Pos;
                    ((PumpingStepVMRev2)viewModel).SelectedPushValve3Pos = ((NewPumpingStep)step).SelectedPushValve3Pos;
                    for (int i = 0; i < 4; i++)
                    {
                        ((PumpingStepVMRev2)viewModel).PullSyringeSelectFC[i] = ((NewPumpingStep)step).PumpPullingPaths[i];
                        ((PumpingStepVMRev2)viewModel).PushSyringeSelectFC[i] = ((NewPumpingStep)step).PumpPushingPaths[i];
                    }
                    break;
                case RecipeStepTypes.Loop:
                    ((LoopStepViewModel)viewModel).LoopCycles = ((LoopStep)step).LoopCycles;
                    ((LoopStepViewModel)viewModel).LoopName = ((LoopStep)step).LoopName;
                    break;
                case RecipeStepTypes.RunRecipe:
                    ((RunRecipeStepViewModel)viewModel).RunRecipePath = ((RunRecipeStep)step).RecipePath;
                    break;
                case RecipeStepTypes.Waiting:
                    ((WaitingStepViewModel)viewModel).WaitingTime = ((WaitingStep)step).Time;
                    ((WaitingStepViewModel)viewModel).ResetPump = ((WaitingStep)step).ResetPump;
                    break;
                case RecipeStepTypes.Comment:
                    ((CommentStepViewModel)viewModel).Comments = ((CommentStep)step).Comment;
                    break;

            }
        }

        public void GetStepParameterFromViewModel(RecipeStepBase step, RecipetoolVM.StepsTreeViewModel viewModel)
        {
            switch (step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    ((SetTemperStep)step).TargetTemper = ((SetTemperStepViewModel)viewModel).SetTemperature;
                    ((SetTemperStep)step).Tolerance = ((SetTemperStepViewModel)viewModel).TemperTolerance;
                    ((SetTemperStep)step).Duration = ((SetTemperStepViewModel)viewModel).TemperDuration;
                    ((SetTemperStep)step).WaitForComplete = ((SetTemperStepViewModel)viewModel).WaitForTemperCtrlFinished;
                    ((SetTemperStep)step).CtrlP = ((SetTemperStepViewModel)viewModel).TemperCtrlP;
                    ((SetTemperStep)step).CtrlI = ((SetTemperStepViewModel)viewModel).TemperCtrlI;
                    ((SetTemperStep)step).CtrlD = ((SetTemperStepViewModel)viewModel).TemperCtrlD;
                    ((SetTemperStep)step).CtrlHeatGain = ((SetTemperStepViewModel)viewModel).TemperCtrlHeatGain;
                    ((SetTemperStep)step).CtrlCoolGain = ((SetTemperStepViewModel)viewModel).TemperCtrlCoolGain;
                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    ((SetPreHeatTempStep)step).TargetTemper = ((SetPreHeatTemperStepViewModel)viewModel).SetTemperature;
                    ((SetPreHeatTempStep)step).Tolerance = ((SetPreHeatTemperStepViewModel)viewModel).TemperTolerance;
                    ((SetPreHeatTempStep)step).WaitForComplete = ((SetPreHeatTemperStepViewModel)viewModel).WaitForTemperCtrlFinished;
                    break;
                case RecipeStepTypes.Imaging:
                    ((ImagingStep)step).IsAutoFocusOn = ((ImagingStepViewModel)viewModel).IsAutoFocusOn;
                    ((ImagingStep)step).Regions = new List<ImagingRegion>();
                    foreach (var regionVm in ((ImagingStepViewModel)viewModel).AddedRegions)
                    {
                        ImagingRegion region = new ImagingRegion();
                        region.RegionIndex = regionVm.Index;
                        region.Lane = regionVm.Lane;
                        region.Column = regionVm.Column;
                        region.Row = regionVm.Row;
                        foreach (var imagingVm in regionVm.Imagings)
                        {
                            ImagingSetting imaging = new ImagingSetting();
                            switch (imagingVm.SelectedChannel)
                            {
                                case "[R]":
                                    imaging.Channels = ImagingChannels.Red;
                                    imaging.RedExposureTime = imagingVm.RedExposure;
                                    imaging.RedIntensity = imagingVm.RedIntensity;
                                    break;
                                case "[G]":
                                    imaging.Channels = ImagingChannels.Green;
                                    imaging.GreenExposureTime = imagingVm.GreenExposure;
                                    imaging.GreenIntensity = imagingVm.GreenIntensity;
                                    break;
                                case "[R,G]":
                                    imaging.Channels = ImagingChannels.RedGreen;
                                    imaging.RedExposureTime = imagingVm.RedExposure;
                                    imaging.RedIntensity = imagingVm.RedIntensity;
                                    imaging.GreenExposureTime = imagingVm.GreenExposure;
                                    imaging.GreenIntensity = imagingVm.GreenIntensity;
                                    break;
                            }
                            imaging.Filter = imagingVm.SelectedFilter;
                            region.Imagings.Add(imaging);
                        }
                        foreach (var focusVm in regionVm.RefFocuses)
                        {
                            FocusSetting focus = new FocusSetting();
                            focus.Name = focusVm.FocusName;
                            focus.Position = focusVm.FocusPos;
                            region.ReferenceFocuses.Add(focus);
                        }
                        ((ImagingStep)step).Regions.Add(region);
                    }
                    break;
                case RecipeStepTypes.MoveStage:
                    ((MoveStageStep)step).Region = ((MoveStageStepViewModel)viewModel).MoveStageRegion;
                    break;
                case RecipeStepTypes.MoveStageRev2:
                    ((MoveStageStepRev2)step).Lane = ((MoveStageStepVMRev2)viewModel).SelectedLane;
                    ((MoveStageStepRev2)step).Row = ((MoveStageStepVMRev2)viewModel).SelectedRow;
                    ((MoveStageStepRev2)step).Column = ((MoveStageStepVMRev2)viewModel).SelectedColumn;
                    break;
                case RecipeStepTypes.Pumping:
                    ((PumpingStep)step).PumpingType = ((PumpingStepViewModel)viewModel).SelectedPumpingType.Mode;
                    ((PumpingStep)step).PullRate = ((PumpingStepViewModel)viewModel).PumpPullingRate;
                    ((PumpingStep)step).PushRate = ((PumpingStepViewModel)viewModel).PumpPushingRate;
                    ((PumpingStep)step).PullPath = ((PumpingStepViewModel)viewModel).SelectedPullingPath;
                    ((PumpingStep)step).PushPath = ((PumpingStepViewModel)viewModel).SelectedPushingPath;
                    ((PumpingStep)step).Reagent = ((PumpingStepViewModel)viewModel).SelectedReagent;
                    ((PumpingStep)step).Volume = ((PumpingStepViewModel)viewModel).PumpingVol;
                    break;
                case RecipeStepTypes.NewPumping:
                    ((NewPumpingStep)step).PumpingType = ((PumpingStepVMRev2)viewModel).SelectedPumpingType.Mode;
                    ((NewPumpingStep)step).PullRate = ((PumpingStepVMRev2)viewModel).PumpPullingRate;
                    ((NewPumpingStep)step).PushRate = ((PumpingStepVMRev2)viewModel).PumpPushingRate;
                    ((NewPumpingStep)step).PullPath = ((PumpingStepVMRev2)viewModel).SelectedPullingPath;
                    ((NewPumpingStep)step).PushPath = ((PumpingStepVMRev2)viewModel).SelectedPushingPath;
                    ((NewPumpingStep)step).Reagent = ((PumpingStepVMRev2)viewModel).SelectedReagent;
                    ((NewPumpingStep)step).Volume = ((PumpingStepVMRev2)viewModel).PumpingVol;
                    ((NewPumpingStep)step).SelectedPullValve2Pos = ((PumpingStepVMRev2)viewModel).SelectedPullValve2Pos;
                    ((NewPumpingStep)step).SelectedPullValve3Pos = ((PumpingStepVMRev2)viewModel).SelectedPullValve3Pos;
                    ((NewPumpingStep)step).SelectedPushValve2Pos = ((PumpingStepVMRev2)viewModel).SelectedPushValve2Pos;
                    ((NewPumpingStep)step).SelectedPushValve3Pos = ((PumpingStepVMRev2)viewModel).SelectedPushValve3Pos;
                    for (int i = 0; i < 4; i++)
                    {
                        ((NewPumpingStep)step).PumpPullingPaths[i] = ((PumpingStepVMRev2)viewModel).PullSyringeSelectFC[i];
                        ((NewPumpingStep)step).PumpPushingPaths[i] = ((PumpingStepVMRev2)viewModel).PushSyringeSelectFC[i];
                    }
                    break;
                case RecipeStepTypes.Loop:
                    ((LoopStep)step).LoopCycles = ((LoopStepViewModel)viewModel).LoopCycles;
                    ((LoopStep)step).LoopName = ((LoopStepViewModel)viewModel).LoopName;
                    break;
                case RecipeStepTypes.RunRecipe:
                    ((RunRecipeStep)step).RecipePath = ((RunRecipeStepViewModel)viewModel).RunRecipePath;
                    break;
                case RecipeStepTypes.Waiting:
                    ((WaitingStep)step).Time = ((WaitingStepViewModel)viewModel).WaitingTime;
                    ((WaitingStep)step).ResetPump = ((WaitingStepViewModel)viewModel).ResetPump;
                    break;
                case RecipeStepTypes.Comment:
                    ((CommentStep)step).Comment = ((CommentStepViewModel)viewModel).Comments;
                    break;

            }
        }
        #endregion RecipeTool Initialization

        #region Private Fields
        //private DispatcherTimer _DispatcherTimer = new DispatcherTimer();
        private DateTime _CaptureStartTime;
        private string _CapturingStatus = string.Empty;
        private string _EstimatedRemainingTime;
        private double _EstimatedCaptureTime;
        private double _PercentCompleted;
        //private string _LaunchRecord;
        private bool _IsBusy;
        private string _StatusInfo;
        private bool _IsMachineRev2 = false;
        private bool _IsMachineRev2P4 = false;
        Dictionary<FluidicsVersion, FluidicsViewModel> FluidicsVMList;
        private LogWindowViewModel _LogWindowVM = null;
        #endregion Private Fields

        #region Public Properties
        public LogWindowViewModel LogWindowVM
        {
            get { return _LogWindowVM; }
            set { _LogWindowVM = value; RaisePropertyChanged("LogWindowVM"); }
        }
        public ICommand ShowLogWindowCmd
        {
            get
            {
                return new RelayCommand(e => ShowLog());
            }
        }

       // for camera
        public DateTime CameraCaptureStartTime
        {
            get { return _CaptureStartTime; }
            set
            {
                if (_CaptureStartTime != value)
                {
                    _CaptureStartTime = value;
                    RaisePropertyChanged(nameof(CameraCaptureStartTime));
                }
            }
        }
        public string CameraCapturingStatus
        {
            get { return _CapturingStatus; }
            set
            {
                if (_CapturingStatus != value)
                {
                    _CapturingStatus = value;
                    RaisePropertyChanged(nameof(CameraCapturingStatus));
                }
            }
        }
        public double EstimatedCaptureTime
        {
            get { return _EstimatedCaptureTime; }
            set
            {
                if (_EstimatedCaptureTime != value)
                {
                    _EstimatedCaptureTime = value;
                    RaisePropertyChanged(nameof(EstimatedCaptureTime));
                }
            }
        }

        public string EstimatedRemainingTime
        {
            get { return _EstimatedRemainingTime; }
            set
            {
                if (_EstimatedRemainingTime != value)
                {
                    _EstimatedRemainingTime = value;
                    RaisePropertyChanged(nameof(EstimatedRemainingTime));
                }
            }
        }
        public double PercentCompleted
        {
            get { return _PercentCompleted; }
            set
            {
                if (_PercentCompleted != value)
                {
                    _PercentCompleted = value;
                    RaisePropertyChanged(nameof(PercentCompleted));
                }
            }
        }
        //public DispatcherTimer CaptureCountdownTimer
        //{
        //    get { return _DispatcherTimer; }
        //}
        //public void AddLaunchRecord(string msg)
        //{
        //    LaunchRecord += msg;
        //}

        //string LaunchRecord
        //{
        //    get { return _LaunchRecord; }
        //    set
        //    {
        //        if (_LaunchRecord != value)
        //        {
        //            _LaunchRecord = value;
                    
                    
        //            RaisePropertyChanged(nameof(LaunchRecord));
        //        }
        //    }
        //}
        
        string ProductVersion
        {
            get { return string.Format(" V{0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version); }
        }
        public bool IsMachineRev2
        {
            get { return _IsMachineRev2; }
            set
            {
                if (_IsMachineRev2 != value)
                {
                    _IsMachineRev2 = value;
                    //FluidicsVM = FindOrCreateFluidicsVM(IsMachineRev2 ? FluidicsVersion.V2 : FluidicsVersion.V1, ChemistryVM);
                    RaisePropertyChanged(nameof(IsMachineRev2));
                }
            }
        }
        public bool IsMachineRev2P4
        {
            get { return _IsMachineRev2P4; }
            set
            {
                if (_IsMachineRev2P4 != value)
                {
                    _IsMachineRev2P4 = value;
                    RaisePropertyChanged(nameof(IsMachineRev2P4));
                }
            }
        }
        public bool IsBusy
        {
            get { return _IsBusy; }
            set
            {
                if (_IsBusy != value)
                {
                    _IsBusy = value;
                   
                    RaisePropertyChanged(nameof(IsBusy));
                }
            }
        }
        public string StatusInfo
        {
            get { return _StatusInfo; }
            set
            {
                if (_StatusInfo != value)
                {
                    _StatusInfo = value;
                    RaisePropertyChanged(nameof(StatusInfo));
                }
            }
        }

       

        #endregion Public Properties

        #region Other Settings Command
        private RelayCommand _OtherSettingsCmd;

        

        public ICommand OtherSettingsCmd
        {
            get
            {
                if (_OtherSettingsCmd == null)
                {
                    _OtherSettingsCmd = new RelayCommand(ExecuteOtherSettingsCmd, CanExecuteOtherSettingsCmd);
                }
                return _OtherSettingsCmd;
            }
        }

        private void ExecuteOtherSettingsCmd(object obj)
        {
            DetectorParametersVM.FilterMotionSetup.MaxSpeed = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].SpeedRange.LimitHigh;
            DetectorParametersVM.FilterMotionSetup.MaxAccel = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].AccelRange.LimitHigh;
            DetectorParametersVM.FilterMotionSetup.RangeHigh = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].MotionRange.LimitHigh;
            DetectorParametersVM.FilterMotionSetup.RangeLow = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.Filter].MotionRange.LimitLow;

            DetectorParametersVM.YStageMotionSetup.MaxSpeed = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].SpeedRange.LimitHigh;
            DetectorParametersVM.YStageMotionSetup.MaxAccel = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].AccelRange.LimitHigh;
            DetectorParametersVM.YStageMotionSetup.RangeHigh = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].MotionRange.LimitHigh;
            DetectorParametersVM.YStageMotionSetup.RangeLow = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].MotionRange.LimitLow;

            DetectorParametersVM.ZStageMotionSetup.MaxSpeed = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].SpeedRange.LimitHigh;
            DetectorParametersVM.ZStageMotionSetup.MaxAccel = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].AccelRange.LimitHigh;
            DetectorParametersVM.ZStageMotionSetup.RangeHigh = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].MotionRange.LimitHigh;
            DetectorParametersVM.ZStageMotionSetup.RangeLow = SettingsManager.ConfigSettings.MotionSettings[MotionTypes.ZStage].MotionRange.LimitLow;

            MainBoardVM.MainBoard.GetCartridgeMotorStatus();
            DetectorParametersVM.IsCartridgeEnabled = MainBoardVM.MainBoard.IsCartridgeEnabled;

            DetectorParameterSetupWind otherSettingsWindow = new DetectorParameterSetupWind();
            otherSettingsWindow.DataContext = DetectorParametersVM;
            otherSettingsWindow.ShowDialog();
        }

        private bool CanExecuteOtherSettingsCmd(object obj)
        {
            return true;
        }

        public void InitialLogWindowVM()
        {
            if (LogWindowVM == null)
            {
                LogWindowVM = new LogWindowViewModel(false) { LogViewerVM = new LogViewerViewModel(Logger), DispalyDebugMessage=false };
                LogWindowVM.Title =  "Log Viewer";
            }
        }

        private void ShowLog(bool addUIDisplayFilter = false)
        {

            Dispatch(() =>
            {
                InitialLogWindowVM();
                if (addUIDisplayFilter)
                {
                    LogWindowVM?.LogDisplayFilter?.AddSubSystemDisplayFilter("UI");
                }

                if (!LogWindowVM.Contains(this.Dialogs))
                {
                    LogWindowVM.Show(this.Dialogs);
                }
            });
        }
        private FluidicsViewModel FindOrCreateFluidicsVM (FluidicsVersion v, ChemistryViewModel chemistryViewModel)
        {
            FluidicsViewModel vm = null;
            if (FluidicsVMList.ContainsKey(v))
            {
                vm=  FluidicsVMList[v];
            }
            else
            {
                
                switch(v)
                {
                    case FluidicsVersion.V1:
                        vm = new FluidicsViewModelV1(chemistryViewModel);
                        break;
                    case FluidicsVersion.V2:
                        vm = new FluidicsViewModelV2(chemistryViewModel);
                        vm.IsSimulation = SeqApp?.IsSimulation == true;
                        break;
                }
                if (vm != null)
                {
                    FluidicsVMList.Add(v, vm);
                }
            }
            return vm;
        }
        //no view model involved
        //to do: this code shall be move out
        //public bool ConnectToMainBoardDevices(ISeqApp seqApp)
        //{
        //    bool isAllDeviceConnected = true;
        //    IsMachineRev2 = seqApp.IsMachineRev2;
        //    //LaunchRecord = string.Empty;
        //    //AddLaunchRecordMessage("Free disk space check...\n");
        //    //DriveInfo[] allDrives = DriveInfo.GetDrives();
        //    //AddLaunchRecordMessage(string.Format("Disk: {0} has available space: {1:F2}GB \n", allDrives[0].Name, (allDrives[0].AvailableFreeSpace / 1024 / 1024 / 1024)));

        //    //Mainboard mainboardDevice = Mainboard.GetInstance();
        //    //AddLaunchRecordMessage("Main Board Connection...");
        //    //mainboardDevice.Connect();
        //    //if (mainboardDevice.IsConnected)
        //    //{
        //    //    AddLaunchRecordMessage(string.Format("Succeeded, Hardware Version:{0}\n", mainboardDevice.HWVersion));
        //    //    if (mainboardDevice.HWVersion.Substring(0, 1) == "1")    // ALF 1.x machine
        //    //    {
        //    //        AddLaunchRecordMessage("This is a 1.x machine\n");
        //    //        IsMachineRev2 = false;
        //    //    }
        //    //    else if (mainboardDevice.HWVersion.Substring(0, 1) == "2")   // ALF2.x machine
        //    //    {
        //    //        AddLaunchRecordMessage("This is a 2.x machine\n");
        //    //        IsMachineRev2 = true;
        //    //        mainboardDevice.Disconnect();

        //    //        AddLaunchRecordMessage("Mainboard Revision 2 connecting...");
        //    //        MainBoardController mainBoardControllerDevice = MainBoardController.GetInstance();
        //    //        if (mainBoardControllerDevice.Connect("COM16"))
        //    //        {
        //    //            AddLaunchRecordMessage("Succeeded.\n");
        //    //        }
        //    //        else if (mainBoardControllerDevice.Connect())
        //    //        {
        //    //            AddLaunchRecordMessage("Succeeded.\n");
        //    //        }
        //    //        else
        //    //        {
        //    //            AddLaunchRecordMessage("Failed.\n", false);
        //    //            isAllDeviceConnected = false;
        //    //        }
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    //MessageBox.Show("Main board is not found, the program is closing, please verify the main board connection before relaunching this program.");
        //    //    //Environment.Exit(0);
        //    //    AddLaunchRecordMessage("Failed.\n", false);
        //    //    isAllDeviceConnected = false;
        //    //}
        //    isAllDeviceConnected = seqApp.Initialized;
        //    IsMainDevicesConnected = isAllDeviceConnected;
        //    return isAllDeviceConnected;
        //}

        //to do: this code shall be moved out
        public void InitializeViewModels(ISystemInit seqApp)
        {
            bool isAllDeviceConnected = true;
            //AddLaunchRecordMessage("Z stage connecting...");
            for (int i = 0; i < 2; i++)
            {
                //MotionVM.MotionController.ZStageConnect();
                MotionVM.IsZStageAlive = seqApp.MotionController.IsZStageConnected;
                if (MotionVM.IsZStageAlive)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
            //AddLaunchRecordMessage(string.Format("{0}\n", MotionVM.IsZStageAlive ? "Succeeded" : "Failed"), MotionVM.IsZStageAlive);

            if (!IsMachineRev2)
            {
                //AddLaunchRecordMessage("Camera connecting...");
                TheDispatcher.Invoke(new Action(() =>
                {
                    if (CameraVM.InitializeFromCamera(seqApp.PhotometricsCamera))
                    {
                        // AddLaunchRecordMessage("Succeeded.\n");
                    }
                    else
                    {
                        //AddLaunchRecordMessage("Failed.\n", false);
                        isAllDeviceConnected = false;
                    }
                }));
            }
            else
            {
                // to do: connecting to ethernet cameras
                //AddLaunchRecordMessage("Ethernet Cameras connecting...");
                if (CameraVM.InitializeFromEthernetCameras(seqApp.EthernetCameraA, seqApp.EthernetCameraB))
                {
                    //AddLaunchRecordMessage("Succeeded.\n");
                }
                else
                {
                    //AddLaunchRecordMessage("Failed.\n", false);
                    isAllDeviceConnected = false;
                }
            }

            //AddLaunchRecordMessage("Motion controller connecting...");
            //MotionVM.MotionController.UsingHywireController = IsMachineRev2;
            //MotionVM.MotionController.OtherStagesConnect();
            MotionVM.IsGalilAlive = seqApp.MotionController.IsMotionConnected;
            //AddLaunchRecordMessage(string.Format("{0}\n", MotionVM.IsGalilAlive ? "Succeeded" : "Failed"), MotionVM.IsGalilAlive);
            if (!MotionVM.IsGalilAlive)
            {
                isAllDeviceConnected = false;
            }

            if (IsMachineRev2)
            {
                MainBoardVM.MotionFWVersion = MotionVM.MotionController.HywireMotionController.ControllerVersion;
            }

            //FluidicsVM.FluidicsInterface.OnConnectionUpdated += FluidicsInterface_OnConnectionUpdated;
            FluidicsVM.Initialize(seqApp.FluidicsInterface, seqApp.MotionController);
            if (!seqApp.FluidicsInterface.IsConnected)
            {
                isAllDeviceConnected = false;
            }
            //FluidicsVM.FluidicsInterface.OnConnectionUpdated -= FluidicsInterface_OnConnectionUpdated;
            FluidicsVM.ValvePos = FluidicsVM.Valve.CurrentPos;
            if (IsMachineRev2)
            {
                ((FluidicsViewModelV2)FluidicsVM).CurrentValve2 = FluidicsVM.SmartValve2.CurrentPos;
                ((FluidicsViewModelV2)FluidicsVM).CurrentValve3 = FluidicsVM.SmartValve3.CurrentPos;
                // to do: connecting to RFID
                //AddLaunchRecordMessage("Chiller controller connecting...");
                //if (MainBoardVM.Chiller.Connect("COM17"))
                //{
                //    AddLaunchRecordMessage("Succeeded.\n");
                //}
                //else if (MainBoardVM.Chiller.Connect())
                //{
                //    AddLaunchRecordMessage("Succeeded.\n");
                //}
                //else
                //{
                //    AddLaunchRecordMessage("Failed.\n", false);
                //    isAllDeviceConnected = false;
                //}

                //if (MainBoardVM.LEDController.IsConnected)
                //{
                //    MainBoardVM.LEDController.SetLEDControlledByCamera(LEDTypes.Green, false);
                //    MainBoardVM.LEDController.SetLEDControlledByCamera(LEDTypes.Red, false);
                //    MainBoardVM.LEDController.SetLEDControlledByCamera(LEDTypes.White, false);
                //    MainBoardVM.LEDController.SetLEDIntensity(LEDTypes.Green, (int)MainBoardVM.GLEDIntensitySet);
                //    MainBoardVM.LEDController.SetLEDIntensity(LEDTypes.Red, (int)MainBoardVM.RLEDIntensitySet);
                //    MainBoardVM.LEDController.SetLEDIntensity(LEDTypes.White, (int)MainBoardVM.WLEDIntensitySet);
                //}

                //AddLaunchRecordMessage("Barcode reader connecting...");
                //if (BarCodeReaderVm.Connect("COM19"))
                //{
                //    AddLaunchRecordMessage("Succeeded.\n");
                //}
                //else if (BarCodeReaderVm.Connect())
                //{
                //    AddLaunchRecordMessage("Succeeded.\n");
                //}
                //else
                //{
                //    AddLaunchRecordMessage("Failed.\n", false);
                //    isAllDeviceConnected = false;
                //}

                //AddLaunchRecordMessage("FC Temperature controller connecting...");
                //if (TemperatureController.GetInstance().Connect())
                //{
                //    AddLaunchRecordMessage("Succeeded.\n");
                //}
                //else
                //{
                //    AddLaunchRecordMessage("Failed.\n", false);
                //    isAllDeviceConnected = false;
                //}
            }
            //Logger.Log("SUMMARY:\n" + LaunchRecord);
            //Thread.Sleep(2500); //give some times to display the last log message above
            IsOtherDevicesConnected = isAllDeviceConnected;
        }

        //private void FluidicsInterface_OnConnectionUpdated(object sender, ComponentConnectionEventArgs e)
        //{
        //    //throw new NotImplementedException();
        //    Dispatch(()=>
        //    AddLaunchRecordMessage(e.Message, e.IsErrorMessage)
        //    );
        //}

        //public void AddLaunchRecordMessage(string str, bool success = true)
        //{
        //    if (Logger != null)
        //    {
        //        if (!success)
        //        {
        //            Logger.LogError(str);
        //        }
        //        else
        //        {
        //            Logger.Log(str);
        //        }
        //    }
        //    LaunchRecord += str;
        //}
        #endregion Other Settings Command

        #region Public Functions
        #endregion Public Functions

        private void Closeing(object obj)
        {
            if (CameraVM.IsConnected)
            {
                if (CameraVM.WorkingStatus != CameraStatusEnums.Idle)
                {
                    CameraVM.CancelCmd.Execute(null);
                }
                //CameraVM.CloseCamera();
            }

            //if (MainBoardVM.MainBoard.IsConnected && !IsMachineRev2)
            //{
            //    MainBoardVM.MainBoard.SetLEDStatus(LEDTypes.Green, false);
            //    MainBoardVM.MainBoard.SetLEDStatus(LEDTypes.Red, false);
            //    MainBoardVM.MainBoard.SetLEDStatus(LEDTypes.White, false);
            //}
            //else if (MainBoardVM.LEDController.IsConnected)
            //{
            //    MainBoardVM.LEDController.SetLEDStatus(LEDTypes.Green, false);
            //    MainBoardVM.LEDController.SetLEDStatus(LEDTypes.Red, false);
            //    MainBoardVM.LEDController.SetLEDStatus(LEDTypes.White, false);
            //}
            SystemInitApp?.Unintialize();
        }

        private void Close()
        {
            Environment.Exit(0);
        }
    }
}
