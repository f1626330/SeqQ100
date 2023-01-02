using Sequlite.WPF.Framework;
using Sequlite.ALF.RecipeLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class Workspace : ViewModelBase
    {
        static public Workspace This { get; } = new Workspace();
        MainWindow Owner { get; set; }

        public Workspace()
        {
            RecipeToolRecipeVM = new RecipeToolRecipeViewModel();
            NewStepVM = new RecipeStepViewModel();
            StepManipulationVM = new StepManipulationViewModel();
        }

        #region Public properties
        public RecipeToolRecipeViewModel RecipeToolRecipeVM { get; }
        public RecipeStepViewModel NewStepVM { get; }
        public StepManipulationViewModel StepManipulationVM { get; }
        public Recipe NewRecipe { get; set; } = new Recipe();
        #endregion Public properties

        public void SetStepParameterToViewModel(RecipeStepBase step, StepsTreeViewModel viewModel)
        {
            switch (step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    ((SetTemperStepViewModel)viewModel).SetTemperature = ((SetTemperStep)step).TargetTemper;
                    ((SetTemperStepViewModel)viewModel).TemperTolerance = ((SetTemperStep)step).Tolerance;
                    ((SetTemperStepViewModel)viewModel).TemperDuration = ((SetTemperStep)step).Duration;
                    ((SetTemperStepViewModel)viewModel).WaitForTemperCtrlFinished = ((SetTemperStep)step).WaitForComplete;
                    break;
                case RecipeStepTypes.Imaging:
                    ((ImagingStepViewModel)viewModel).IsAutoFocusOn = ((ImagingStep)step).IsAutoFocusOn;
                    ((ImagingStepViewModel)viewModel).AddedRegions = new ObservableCollection<ImagingRegionViewModel>();
                    foreach(var region in ((ImagingStep)step).Regions)
                    {
                        ImagingRegionViewModel regionVm = new ImagingRegionViewModel();
                        regionVm.Index = region.RegionIndex;
                        regionVm.Lane = region.Lane;
                        regionVm.X = region.X;
                        regionVm.Y = region.Y;

                        foreach(var imaging in region.Imagings)
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

                        foreach(var focus in region.ReferenceFocuses)
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
                case RecipeStepTypes.Pumping:
                    ((PumpingStepViewModel)viewModel).SelectedPumpingType = ((PumpingStepViewModel)viewModel).PumpingTypeOptions.Find(p => p.Mode == ((PumpingStep)step).PumpingType);
                    ((PumpingStepViewModel)viewModel).PumpPullingRate = ((PumpingStep)step).PullRate;
                    ((PumpingStepViewModel)viewModel).PumpPushingRate = ((PumpingStep)step).PushRate;
                    ((PumpingStepViewModel)viewModel).SelectedPullingPath = ((PumpingStepViewModel)viewModel).PumpingPathOptions.Find(p => p == ((PumpingStep)step).PullPath);
                    ((PumpingStepViewModel)viewModel).SelectedPushingPath = ((PumpingStepViewModel)viewModel).PumpingPathOptions.Find(p => p == ((PumpingStep)step).PushPath);
                    ((PumpingStepViewModel)viewModel).SelectedReagent = ((PumpingStepViewModel)viewModel).ReagentOptions.Find(p => p == ((PumpingStep)step).Reagent);
                    ((PumpingStepViewModel)viewModel).PumpingVol = ((PumpingStep)step).Volume;
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
                    break;
                case RecipeStepTypes.Comment:
                    ((CommentStepViewModel)viewModel).Comments = ((CommentStep)step).Comment;
                    break;

            }
        }

        public void GetStepParameterFromViewModel(RecipeStepBase step, StepsTreeViewModel viewModel)
        {
            switch (step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    ((SetTemperStep)step).TargetTemper = ((SetTemperStepViewModel)viewModel).SetTemperature;
                    ((SetTemperStep)step).Tolerance = ((SetTemperStepViewModel)viewModel).TemperTolerance;
                    ((SetTemperStep)step).Duration = ((SetTemperStepViewModel)viewModel).TemperDuration;
                    ((SetTemperStep)step).WaitForComplete = ((SetTemperStepViewModel)viewModel).WaitForTemperCtrlFinished;
                    break;
                case RecipeStepTypes.Imaging:
                    ((ImagingStep)step).IsAutoFocusOn = ((ImagingStepViewModel)viewModel).IsAutoFocusOn;
                    ((ImagingStep)step).Regions = new List<ImagingRegion>();
                    foreach(var regionVm in ((ImagingStepViewModel)viewModel).AddedRegions)
                    {
                        ImagingRegion region = new ImagingRegion();
                        region.RegionIndex = regionVm.Index;
                        region.Lane = regionVm.Lane;
                        region.X = regionVm.X;
                        region.Y = regionVm.Y;
                        foreach(var imagingVm in regionVm.Imagings)
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
                        foreach(var focusVm in regionVm.RefFocuses)
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
                case RecipeStepTypes.Pumping:
                    ((PumpingStep)step).PumpingType = ((PumpingStepViewModel)viewModel).SelectedPumpingType.Mode;

                    ((PumpingStep)step).PullRate = ((PumpingStepViewModel)viewModel).PumpPullingRate;
                    ((PumpingStep)step).PushRate = ((PumpingStepViewModel)viewModel).PumpPushingRate;
                    ((PumpingStep)step).PullPath = ((PumpingStepViewModel)viewModel).SelectedPullingPath;
                    ((PumpingStep)step).PushPath = ((PumpingStepViewModel)viewModel).SelectedPushingPath;
                    ((PumpingStep)step).Reagent = ((PumpingStepViewModel)viewModel).SelectedReagent;
                    ((PumpingStep)step).Volume = ((PumpingStepViewModel)viewModel).PumpingVol;
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
                    break;
                case RecipeStepTypes.Comment:
                    ((CommentStep)step).Comment = ((CommentStepViewModel)viewModel).Comments;
                    break;

            }
        }
    }
}
