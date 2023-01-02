using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Xml;

namespace Sequlite.ALF.RecipeLib
{
    public class Recipe
    {
        public string RecipeName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }
        public List<StepsTree> Steps { get; set; }
        public string ToolVersion { get; set; }
        public string RecipeFileLocation {get;set;}

        public Recipe( string name = "New Recipe")
        {
            RecipeName = name;
           
            Steps = new List<StepsTree>();
        }

        public static void SaveToXmlFile(Recipe recipe, string fileName)
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration declare = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            doc.AppendChild(declare);

            XmlElement root = doc.CreateElement("Recipe");
            root.SetAttribute("Name", recipe.RecipeName);
            root.SetAttribute("CreatedTime", recipe.CreatedTime.ToString());
            root.SetAttribute("UpdatedTime", recipe.UpdatedTime.ToString());
            root.SetAttribute("ToolVersion", recipe.ToolVersion);
            doc.AppendChild(root);

            XmlElement steps = doc.CreateElement("Steps");

            foreach (StepsTree subStep in recipe.Steps)
            {
                SetStepAttributes(doc, steps, subStep);
            }
            root.AppendChild(steps);
            doc.Save(fileName);
        }

        private static void SetStepAttributes(XmlDocument xmlDoc, XmlElement parentXmlElement, StepsTree stepTree)
        {
            XmlElement stepElement = xmlDoc.CreateElement("Step");
            stepElement.SetAttribute("StepName", RecipeStepBase.GetTypeName(stepTree.Step.StepType));

            switch (stepTree.Step.StepType)
            {
                case RecipeStepTypes.SetTemper:
                    stepElement.SetAttribute("TargetTemper", ((SetTemperStep)stepTree.Step).TargetTemper.ToString());
                    stepElement.SetAttribute("Tolerance", ((SetTemperStep)stepTree.Step).Tolerance.ToString());
                    stepElement.SetAttribute("Duration", ((SetTemperStep)stepTree.Step).Duration.ToString());
                    stepElement.SetAttribute("Wait", ((SetTemperStep)stepTree.Step).WaitForComplete.ToString());
                    stepElement.SetAttribute("CtrlP", ((SetTemperStep)stepTree.Step).CtrlP.ToString());
                    stepElement.SetAttribute("CtrlI", ((SetTemperStep)stepTree.Step).CtrlI.ToString());
                    stepElement.SetAttribute("CtrlD", ((SetTemperStep)stepTree.Step).CtrlD.ToString());
                    stepElement.SetAttribute("CtrlHeatGain", ((SetTemperStep)stepTree.Step).CtrlHeatGain.ToString());
                    stepElement.SetAttribute("CtrlCoolGain", ((SetTemperStep)stepTree.Step).CtrlCoolGain.ToString());

                    break;
                case RecipeStepTypes.SetPreHeatTemp:
                    stepElement.SetAttribute("TargetTemper", ((SetPreHeatTempStep)stepTree.Step).TargetTemper.ToString());
                    stepElement.SetAttribute("Tolerance", ((SetPreHeatTempStep)stepTree.Step).Tolerance.ToString());
                    //stepElement.SetAttribute("Duration", ((SetPreHeatTempStep)stepTree.Step).Duration.ToString());
                    stepElement.SetAttribute("Wait", ((SetPreHeatTempStep)stepTree.Step).WaitForComplete.ToString());
                    //stepElement.SetAttribute("CtrlP", ((SetPreHeatTempStep)stepTree.Step).CtrlP.ToString());
                    //stepElement.SetAttribute("CtrlI", ((SetPreHeatTempStep)stepTree.Step).CtrlI.ToString());
                    //stepElement.SetAttribute("CtrlD", ((SetPreHeatTempStep)stepTree.Step).CtrlD.ToString());
                    //stepElement.SetAttribute("CtrlHeatGain", ((SetPreHeatTempStep)stepTree.Step).CtrlHeatGain.ToString());
                    //stepElement.SetAttribute("CtrlCoolGain", ((SetPreHeatTempStep)stepTree.Step).CtrlCoolGain.ToString());

                    break;
                case RecipeStepTypes.StopPreHeating:
                    break;
                case RecipeStepTypes.StopTemper:
                    break;
                case RecipeStepTypes.Pumping:
                    stepElement.SetAttribute("PumpingType", ((PumpingStep)stepTree.Step).PumpingType.ToString());
                    stepElement.SetAttribute("Volume", ((PumpingStep)stepTree.Step).Volume.ToString());
                    if (((PumpingStep)stepTree.Step).PumpingType == ModeOptions.Pull)
                    {
                        stepElement.SetAttribute("PullingPath", ((PumpingStep)stepTree.Step).PullPath.ToString());
                        stepElement.SetAttribute("PullingRate", ((PumpingStep)stepTree.Step).PullRate.ToString());
                    }
                    else if (((PumpingStep)stepTree.Step).PumpingType == ModeOptions.Push)
                    {
                        stepElement.SetAttribute("PushingPath", ((PumpingStep)stepTree.Step).PushPath.ToString());
                        stepElement.SetAttribute("PushingRate", ((PumpingStep)stepTree.Step).PushRate.ToString());
                    }
                    else
                    {
                        stepElement.SetAttribute("PullingPath", ((PumpingStep)stepTree.Step).PullPath.ToString());
                        stepElement.SetAttribute("PullingRate", ((PumpingStep)stepTree.Step).PullRate.ToString());
                        stepElement.SetAttribute("PushingPath", ((PumpingStep)stepTree.Step).PushPath.ToString());
                        stepElement.SetAttribute("PushingRate", ((PumpingStep)stepTree.Step).PushRate.ToString());
                    }
                    stepElement.SetAttribute("Reagent", ((PumpingStep)stepTree.Step).Reagent.ToString());
                    break;
                case RecipeStepTypes.NewPumping:

                    string pullpaths = null;
                    string pushpaths = null;
                    if (((NewPumpingStep)stepTree.Step).PumpingType == ModeOptions.Pull)
                    {
                        stepElement.SetAttribute("PullingRate", ((NewPumpingStep)stepTree.Step).PullRate.ToString());
                        stepElement.SetAttribute("PullValve3", ((NewPumpingStep)stepTree.Step).SelectedPullValve3Pos.ToString());
                        stepElement.SetAttribute("PullValve2", ((NewPumpingStep)stepTree.Step).SelectedPullValve2Pos.ToString());
                        for (int i = 0; i < 4; i++)
                        {
                            if (((NewPumpingStep)stepTree.Step).PumpPullingPaths[i]){ pullpaths += "1"; }else { pullpaths += "0"; }
                        }
                        stepElement.SetAttribute("PumpPullingPaths", pullpaths);
                        stepElement.SetAttribute("PullingPath", ((NewPumpingStep)stepTree.Step).PullPath.ToString());

                    }
                    else if (((NewPumpingStep)stepTree.Step).PumpingType == ModeOptions.Push)
                    {
                        stepElement.SetAttribute("PushingRate", ((NewPumpingStep)stepTree.Step).PushRate.ToString());
                        stepElement.SetAttribute("PushValve3", ((NewPumpingStep)stepTree.Step).SelectedPushValve3Pos.ToString());
                        stepElement.SetAttribute("PushValve2", ((NewPumpingStep)stepTree.Step).SelectedPushValve2Pos.ToString());
                        for (int i = 0; i < 4; i++) {
                            if (((NewPumpingStep)stepTree.Step).PumpPushingPaths[i]){ pushpaths += "1"; }else { pushpaths += "0"; }
                        }
                        stepElement.SetAttribute("PumpPushingPaths", pushpaths);
                        stepElement.SetAttribute("PushingPath", ((NewPumpingStep)stepTree.Step).PushPath.ToString());
                    }
                    else
                    {   
                        stepElement.SetAttribute("PushingRate", ((NewPumpingStep)stepTree.Step).PushRate.ToString());
                        for (int i = 0; i < 4; i++) {
                            if (((NewPumpingStep)stepTree.Step).PumpPushingPaths[i]){ pushpaths += "1"; }else { pushpaths += "0"; }
                            if (((NewPumpingStep)stepTree.Step).PumpPullingPaths[i]){ pullpaths += "1"; }else { pullpaths += "0"; }
                        }
                        stepElement.SetAttribute("PushValve3", ((NewPumpingStep)stepTree.Step).SelectedPushValve3Pos.ToString());
                        stepElement.SetAttribute("PushValve2", ((NewPumpingStep)stepTree.Step).SelectedPushValve2Pos.ToString());
                        stepElement.SetAttribute("PumpPushingPaths", pushpaths);
                        stepElement.SetAttribute("PushingPath", ((NewPumpingStep)stepTree.Step).PushPath.ToString());

                        stepElement.SetAttribute("PullingRate", ((NewPumpingStep)stepTree.Step).PullRate.ToString());
                        stepElement.SetAttribute("PullValve3", ((NewPumpingStep)stepTree.Step).SelectedPullValve3Pos.ToString());
                        stepElement.SetAttribute("PullValve2", ((NewPumpingStep)stepTree.Step).SelectedPullValve2Pos.ToString());
                        stepElement.SetAttribute("PumpPullingPaths", pullpaths);
                        stepElement.SetAttribute("PullingPath", ((NewPumpingStep)stepTree.Step).PullPath.ToString());
                    }
                    stepElement.SetAttribute("Volume", ((NewPumpingStep)stepTree.Step).Volume.ToString());
                    stepElement.SetAttribute("Reagent", ((NewPumpingStep)stepTree.Step).Reagent.ToString());
                    stepElement.SetAttribute("PumpingType", ((NewPumpingStep)stepTree.Step).PumpingType.ToString());
                    break;
                case RecipeStepTypes.Imaging:
                    stepElement.SetAttribute("IsAutoFocusOn", ((ImagingStep)stepTree.Step).IsAutoFocusOn.ToString());
                    XmlElement regions = xmlDoc.CreateElement("Regions");
                    foreach (var region in ((ImagingStep)stepTree.Step).Regions)
                    {
                        XmlElement currentRegion = xmlDoc.CreateElement("Region");
                        currentRegion.SetAttribute("Index", region.RegionIndex.ToString());

                        // only put in file if we are using Lane, X, Y
                        if (region.Lane  > 0)
                        {
                            currentRegion.SetAttribute("Lane", region.Lane.ToString());
                            currentRegion.SetAttribute("Column", region.Column.ToString());
                            currentRegion.SetAttribute("Row", region.Row.ToString());
                        }

                        XmlElement imagings = xmlDoc.CreateElement("Imagings");
                        foreach (var imaging in region.Imagings)
                        {
                            XmlElement imagingElement = xmlDoc.CreateElement("Imaging");
                            string channelStr = string.Empty;
                            string exposureStr = string.Empty;
                            string intensityStr = string.Empty;
                            string filterStr = string.Empty;
                            switch (imaging.Channels)
                            {
                                case ImagingChannels.Red:
                                    channelStr = "[R]";
                                    exposureStr = string.Format("[{0}]", imaging.RedExposureTime);
                                    intensityStr = string.Format("[{0}]", imaging.RedIntensity);
                                    break;
                                case ImagingChannels.Green:
                                    channelStr = "[G]";
                                    exposureStr = string.Format("[{0}]", imaging.GreenExposureTime);
                                    intensityStr = string.Format("[{0}]", imaging.GreenIntensity);
                                    break;
                                case ImagingChannels.RedGreen:
                                    channelStr = "[R,G]";
                                    exposureStr = string.Format("[{0},{1}]", imaging.RedExposureTime, imaging.GreenExposureTime);
                                    intensityStr = string.Format("[{0},{1}]", imaging.RedIntensity, imaging.GreenIntensity);
                                    break;
                                case ImagingChannels.White:
                                    channelStr = "[W]";
                                    exposureStr = string.Format("[{0}]", imaging.WhiteExposureTime);
                                    intensityStr = string.Format("[{0}]", imaging.WhiteIntensity);
                                    break;
                            }
                            filterStr = imaging.Filter.ToString();
                            imagingElement.SetAttribute("Channel", channelStr);
                            imagingElement.SetAttribute("Exposure", exposureStr);
                            imagingElement.SetAttribute("Intensity", intensityStr);
                            imagingElement.SetAttribute("Filter", filterStr);

                            imagings.AppendChild(imagingElement);
                        }
                        currentRegion.AppendChild(imagings);

                        XmlElement focuses = xmlDoc.CreateElement("Focuses");
                        foreach (var focus in region.ReferenceFocuses)
                        {
                            XmlElement focusElement = xmlDoc.CreateElement("Focus");
                            focusElement.SetAttribute("Name", focus.Name);
                            focusElement.SetAttribute("Pos", focus.Position.ToString());
                            focuses.AppendChild(focusElement);
                        }
                        currentRegion.AppendChild(focuses);

                        regions.AppendChild(currentRegion);

                    }
                    stepElement.AppendChild(regions);
                    break;
                case RecipeStepTypes.MoveStage:
                    stepElement.SetAttribute("Region", ((MoveStageStep)stepTree.Step).Region.ToString());
                    break;
                case RecipeStepTypes.MoveStageRev2:
                    stepElement.SetAttribute("Lane", ((MoveStageStepRev2)stepTree.Step).Lane.ToString());
                    stepElement.SetAttribute("Row", ((MoveStageStepRev2)stepTree.Step).Row.ToString());
                    stepElement.SetAttribute("Column", ((MoveStageStepRev2)stepTree.Step).Column.ToString());
                    break;
                case RecipeStepTypes.RunRecipe:
                    stepElement.SetAttribute("FileName", ((RunRecipeStep)stepTree.Step).RecipePath);
                    break;
                case RecipeStepTypes.Waiting:
                    stepElement.SetAttribute("Time", ((WaitingStep)stepTree.Step).Time.ToString());
                    stepElement.SetAttribute("ResetPump", ((WaitingStep)stepTree.Step).ResetPump.ToString());
                    break;
                case RecipeStepTypes.Comment:
                    stepElement.SetAttribute("Content", ((CommentStep)stepTree.Step).Comment);
                    break;
                case RecipeStepTypes.Loop:
                    stepElement.SetAttribute("LoopName", ((LoopStep)stepTree.Step).LoopName);
                    stepElement.SetAttribute("Cycles", ((LoopStep)stepTree.Step).LoopCycles.ToString());
                    if (stepTree.Children.Count > 0)
                    {
                        foreach (StepsTree son in stepTree.Children)
                        {
                            SetStepAttributes(xmlDoc, stepElement, son);
                        }
                    }
                    break;
                case RecipeStepTypes.HomeMotion:
                    stepElement.SetAttribute("MotionType", ((HomeMotionStep)stepTree.Step).MotionType.ToString());
                    stepElement.SetAttribute("Speed", ((HomeMotionStep)stepTree.Step).Speed.ToString());
                    stepElement.SetAttribute("WaitComplete", ((HomeMotionStep)stepTree.Step).WaitForComplete.ToString());
                    break;
                case RecipeStepTypes.AbsoluteMove:
                    stepElement.SetAttribute("MotionType", ((AbsoluteMoveStep)stepTree.Step).MotionType.ToString());
                    stepElement.SetAttribute("Speed", ((AbsoluteMoveStep)stepTree.Step).Speed.ToString());
                    stepElement.SetAttribute("TargetPos", ((AbsoluteMoveStep)stepTree.Step).TargetPos.ToString());
                    stepElement.SetAttribute("WaitComplete", ((AbsoluteMoveStep)stepTree.Step).WaitForComplete.ToString());
                    break;
                case RecipeStepTypes.RelativeMove:
                    stepElement.SetAttribute("MotionType", ((RelativeMoveStep)stepTree.Step).MotionType.ToString());
                    stepElement.SetAttribute("Speed", ((RelativeMoveStep)stepTree.Step).Speed.ToString());
                    stepElement.SetAttribute("MoveStep", ((RelativeMoveStep)stepTree.Step).MoveStep.ToString());
                    stepElement.SetAttribute("WaitComplete", ((RelativeMoveStep)stepTree.Step).WaitForComplete.ToString());
                    break;
                case RecipeStepTypes.HywireImaging:
                    stepElement.SetAttribute("CameraSN", ((HywireImagingStep)stepTree.Step).CameraSN);
                    stepElement.SetAttribute("Exposure", ((HywireImagingStep)stepTree.Step).ExposureTime.ToString());
                    stepElement.SetAttribute("Gain", ((HywireImagingStep)stepTree.Step).Gain.ToString());
                    stepElement.SetAttribute("ADCBitDepth", ((HywireImagingStep)stepTree.Step).ADCBitDepth.ToString());
                    stepElement.SetAttribute("PixelBitDepth", ((HywireImagingStep)stepTree.Step).PixelBitDepth.ToString());
                    stepElement.SetAttribute("ROI", ((HywireImagingStep)stepTree.Step).ROI.ToString());
                    stepElement.SetAttribute("LED", ((HywireImagingStep)stepTree.Step).LED.ToString());
                    stepElement.SetAttribute("Intensity", ((HywireImagingStep)stepTree.Step).Intensity.ToString());
                    break;
                case RecipeStepTypes.LEDCtrl:
                    stepElement.SetAttribute("LED", ((LEDControlStep)stepTree.Step).LED.ToString());
                    stepElement.SetAttribute("Intensity", ((LEDControlStep)stepTree.Step).Intensity.ToString());
                    stepElement.SetAttribute("SetOn", ((LEDControlStep)stepTree.Step).SetOn.ToString());
                    break;
            }

            parentXmlElement.AppendChild(stepElement);
        }

        public static Recipe LoadFromXmlFile(string fileName)
        {
            Recipe loadedRecipe = new Recipe();//IsMachineRev2);
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            loadedRecipe.RecipeName = doc.SelectSingleNode("/Recipe").Attributes["Name"].Value;
            loadedRecipe.CreatedTime = DateTime.Parse(doc.SelectSingleNode("/Recipe").Attributes["CreatedTime"].Value);
            loadedRecipe.UpdatedTime = DateTime.Parse(doc.SelectSingleNode("/Recipe").Attributes["UpdatedTime"].Value);
            var toolVersionAttr = doc.SelectSingleNode("/Recipe").Attributes["ToolVersion"];
            if (toolVersionAttr != null)
            {
                loadedRecipe.ToolVersion = toolVersionAttr.Value;
            }
            XmlNodeList steps = doc.SelectNodes("/Recipe/Steps/Step");
            foreach (XmlNode step in steps)
            {
                GetStepAttributes(step, null, loadedRecipe.Steps);
            }
            loadedRecipe.RecipeFileLocation = fileName;
            return loadedRecipe;
        }

        private static void GetStepAttributes(XmlNode stepNode, StepsTree parentStep, List<StepsTree> root)
        {
            string stepName = stepNode.Attributes["StepName"].Value;
            RecipeStepTypes stepType = RecipeStepBase.GetStepType(stepName);
            if (stepType == RecipeStepTypes.SetTemper)
            {
                SetTemperStep subStep = new SetTemperStep();
                subStep.TargetTemper = double.Parse(stepNode.Attributes["TargetTemper"].Value);
                subStep.Tolerance = double.Parse(stepNode.Attributes["Tolerance"].Value);
                subStep.Duration = int.Parse(stepNode.Attributes["Duration"].Value);
                subStep.WaitForComplete = bool.Parse(stepNode.Attributes["Wait"].Value);
                if (stepNode.Attributes["CtrlP"] != null)
                {
                    subStep.CtrlP = double.Parse(stepNode.Attributes["CtrlP"].Value);
                }
                if (stepNode.Attributes["CtrlI"] != null)
                {
                    subStep.CtrlI = double.Parse(stepNode.Attributes["CtrlI"].Value);
                }
                if (stepNode.Attributes["CtrlD"] != null)
                {
                    subStep.CtrlD = double.Parse(stepNode.Attributes["CtrlD"].Value);
                }
                if (stepNode.Attributes["CtrlHeatGain"] != null)
                {
                    subStep.CtrlHeatGain = double.Parse(stepNode.Attributes["CtrlHeatGain"].Value);
                }
                if (stepNode.Attributes["CtrlCoolGain"] != null)
                {
                    subStep.CtrlCoolGain = double.Parse(stepNode.Attributes["CtrlCoolGain"].Value);
                }
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.SetPreHeatTemp)
            {
                SetPreHeatTempStep subStep = new SetPreHeatTempStep();
                subStep.TargetTemper = double.Parse(stepNode.Attributes["TargetTemper"].Value);
                subStep.Tolerance = double.Parse(stepNode.Attributes["Tolerance"].Value);
                subStep.WaitForComplete = bool.Parse(stepNode.Attributes["Wait"].Value);
                //if (stepNode.Attributes["CtrlP"] != null)
                //{
                //    subStep.CtrlP = double.Parse(stepNode.Attributes["CtrlP"].Value);
                //}
                //if (stepNode.Attributes["CtrlI"] != null)
                //{
                //    subStep.CtrlI = double.Parse(stepNode.Attributes["CtrlI"].Value);
                //}
                //if (stepNode.Attributes["CtrlD"] != null)
                //{
                //    subStep.CtrlD = double.Parse(stepNode.Attributes["CtrlD"].Value);
                //}
                //if (stepNode.Attributes["CtrlHeatGain"] != null)
                //{
                //    subStep.CtrlHeatGain = double.Parse(stepNode.Attributes["CtrlHeatGain"].Value);
                //}
                //if (stepNode.Attributes["CtrlCoolGain"] != null)
                //{
                //    subStep.CtrlCoolGain = double.Parse(stepNode.Attributes["CtrlCoolGain"].Value);
                //}
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.StopPreHeating)
            {
                StopPreHeatingStep subStep = new StopPreHeatingStep();
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.StopTemper)
            {
                StopTemperStep subStep = new StopTemperStep();
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.Imaging)
            {
                ImagingStep subStep = new ImagingStep();
                subStep.IsAutoFocusOn = bool.Parse(stepNode.Attributes["IsAutoFocusOn"].Value);

                var stepNav = stepNode.CreateNavigator();
                var regionIter = stepNav.Select("Regions/Region");
                while (regionIter.MoveNext())
                {
                    var regionNav = regionIter.Current;
                    ImagingRegion crntRegion = new ImagingRegion();
                    crntRegion.RegionIndex = int.Parse(regionNav.GetAttribute("Index", ""));
                    // use by  MachineRev2
                    {
                        int temp;
                        if (int.TryParse(regionNav.GetAttribute("Lane", ""), out temp))
                        {
                            crntRegion.Lane = temp;
                        }

                        if (int.TryParse(regionNav.GetAttribute("Column", ""), out temp))
                        {

                            crntRegion.Column = temp;
                        }

                        if (int.TryParse(regionNav.GetAttribute("Row", ""), out temp))
                        {
                            crntRegion.Row = temp;
                        }
                    }
                    var imagingIter = regionNav.Select("Imagings/Imaging");
                    while (imagingIter.MoveNext())
                    {
                        ImagingSetting newImaging = new ImagingSetting();
                        var imagingNav = imagingIter.Current;
                        string channelStr = imagingNav.GetAttribute("Channel", "");
                        string exposureStr = imagingNav.GetAttribute("Exposure", "");
                        string intensityStr = imagingNav.GetAttribute("Intensity", "");
                        string filterStr = imagingNav.GetAttribute("Filter", "");
                        if (channelStr == "[R]")
                        {
                            newImaging.Channels = ImagingChannels.Red;
                            newImaging.RedExposureTime = double.Parse(exposureStr.Substring(1, exposureStr.Length - 2));
                            newImaging.RedIntensity = uint.Parse(intensityStr.Substring(1, intensityStr.Length - 2));
                        }
                        else if (channelStr == "[G]")
                        {
                            newImaging.Channels = ImagingChannels.Green;
                            newImaging.GreenExposureTime = double.Parse(exposureStr.Substring(1, exposureStr.Length - 2));
                            newImaging.GreenIntensity = uint.Parse(intensityStr.Substring(1, intensityStr.Length - 2));
                        }
                        else if (channelStr == "[R,G]")
                        {
                            newImaging.Channels = ImagingChannels.RedGreen;
                            int dividerIndex = exposureStr.IndexOf(",");
                            newImaging.RedExposureTime = double.Parse(exposureStr.Substring(1, dividerIndex - 1));
                            newImaging.GreenExposureTime = double.Parse(exposureStr.Substring(dividerIndex + 1, exposureStr.Length - dividerIndex - 2));
                            dividerIndex = intensityStr.IndexOf(",");
                            newImaging.RedIntensity = uint.Parse(intensityStr.Substring(1, dividerIndex - 1));
                            newImaging.GreenIntensity = uint.Parse(intensityStr.Substring(dividerIndex + 1, intensityStr.Length - dividerIndex - 2));
                        }
                        else if (channelStr == "[W]")
                        {
                            newImaging.Channels = ImagingChannels.White;
                            newImaging.WhiteExposureTime = double.Parse(exposureStr.Substring(1, exposureStr.Length - 2));
                            newImaging.WhiteIntensity = uint.Parse(intensityStr.Substring(1, intensityStr.Length - 2));
                        }
                        FilterTypes filter = FilterTypes.None;
                        Enum.TryParse(filterStr, out filter);
                        newImaging.Filter = filter;

                        crntRegion.Imagings.Add(newImaging);
                    }

                    var focusIter = regionNav.Select("Focuses/Focus");
                    while (focusIter.MoveNext())
                    {
                        var focusNav = focusIter.Current;
                        string nameStr = focusNav.GetAttribute("Name", "");
                        string posStr = focusNav.GetAttribute("Pos", "");
                        FocusSetting newFocus = new FocusSetting();
                        newFocus.Name = nameStr;
                        newFocus.Position = double.Parse(posStr);
                        crntRegion.ReferenceFocuses.Add(newFocus);
                    }
                    subStep.Regions.Add(crntRegion);

                }
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.MoveStage)
            {
                MoveStageStep subStep = new MoveStageStep();
                subStep.Region = int.Parse(stepNode.Attributes["Region"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.MoveStageRev2)
            {
                MoveStageStepRev2 subStep = new MoveStageStepRev2();
                subStep.Lane = int.Parse(stepNode.Attributes["Lane"].Value);
                subStep.Row = int.Parse(stepNode.Attributes["Row"].Value);
                subStep.Column = int.Parse(stepNode.Attributes["Column"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.Pumping)
            {
                PumpingStep subStep = new PumpingStep();
                subStep.PumpingType = (ModeOptions)Enum.Parse(typeof(ModeOptions), stepNode.Attributes["PumpingType"].Value);
                subStep.Volume = int.Parse(stepNode.Attributes["Volume"].Value);
                var pullPathAttr = stepNode.Attributes["PullingPath"];
                var pullRateAttr = stepNode.Attributes["PullingRate"];
                var pushPathAttr = stepNode.Attributes["PushingPath"];
                var pushRateAttr = stepNode.Attributes["PushingRate"];
                if (pullPathAttr != null)
                {
                    subStep.PullPath = (PathOptions)Enum.Parse(typeof(PathOptions), pullPathAttr.Value);
                }
                if (pullRateAttr != null)
                {
                    subStep.PullRate = double.Parse(pullRateAttr.Value);
                }
                if (pushPathAttr != null)
                {
                    subStep.PushPath = (PathOptions)Enum.Parse(typeof(PathOptions), pushPathAttr.Value);
                }
                if (pushRateAttr != null)
                {
                    subStep.PushRate = double.Parse(pushRateAttr.Value);
                }
                subStep.Reagent = int.Parse(stepNode.Attributes["Reagent"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.NewPumping)
            {
                NewPumpingStep subStep = new NewPumpingStep();
                subStep.PumpingType = (ModeOptions)Enum.Parse(typeof(ModeOptions), stepNode.Attributes["PumpingType"].Value);
                subStep.Volume = int.Parse(stepNode.Attributes["Volume"].Value);
                var pullPathAttr = stepNode.Attributes["PullingPath"];
                var pullRateAttr = stepNode.Attributes["PullingRate"];
                var pushPathAttr = stepNode.Attributes["PushingPath"];
                var pushRateAttr = stepNode.Attributes["PushingRate"];
                var pullValve2Attr = stepNode.Attributes["PullValve2"];
                var pullValve3Attr = stepNode.Attributes["PullValve3"];
                var pushValve2Attr = stepNode.Attributes["PushValve2"];
                var pushValve3Attr = stepNode.Attributes["PushValve3"];
                var pumppullingpathsAttr = stepNode.Attributes["PumpPullingPaths"];
                var pumppushingpathsAttr = stepNode.Attributes["PumpPushingPaths"];
                if (pullPathAttr != null)
                {
                    subStep.PullPath = (PathOptions)Enum.Parse(typeof(PathOptions), pullPathAttr.Value);
                }
                if (pullRateAttr != null)
                {
                    subStep.PullRate = double.Parse(pullRateAttr.Value);
                }
                if (pushPathAttr != null)
                {
                    subStep.PushPath = (PathOptions)Enum.Parse(typeof(PathOptions), pushPathAttr.Value);
                }
                if (pushRateAttr != null)
                {
                    subStep.PushRate = double.Parse(pushRateAttr.Value);
                }
                if (pullValve2Attr != null)
                {
                    subStep.SelectedPullValve2Pos = int.Parse(pullValve2Attr.Value);
                }
                if (pullValve3Attr != null)
                {
                    subStep.SelectedPullValve3Pos = int.Parse(pullValve3Attr.Value);
                }
                if (pushValve2Attr != null)
                {
                    subStep.SelectedPushValve2Pos = int.Parse(pushValve2Attr.Value);
                }
                if (pushValve3Attr != null)
                {
                    subStep.SelectedPushValve3Pos =int.Parse(pushValve3Attr.Value);
                }
                if (pumppullingpathsAttr != null)
                {
                    for(int i = 0; i < 4; i++)
                    {
                        if(pumppullingpathsAttr.Value[i] == '1'){subStep.PumpPullingPaths[i] = true;}else{subStep.PumpPullingPaths[i] = false;}
                    }
                }
                if (pumppushingpathsAttr != null)
                {
                    for (int i = 0; i < 4; i++) { subStep.PumpPushingPaths[i] = (pumppushingpathsAttr.Value[i] == '1')? true : false; }
                }
                subStep.Reagent = int.Parse(stepNode.Attributes["Reagent"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.Loop)
            {
                LoopStep subStep = new LoopStep();
                subStep.LoopName = stepNode.Attributes["LoopName"].Value;
                subStep.LoopCycles = int.Parse(stepNode.Attributes["Cycles"].Value);

                StepsTree newStep = new StepsTree(parentStep, subStep);

                if (stepNode.HasChildNodes)
                {
                    var subStepNodes = stepNode.SelectNodes("Step");
                    foreach (XmlNode node in subStepNodes)
                    {
                        GetStepAttributes(node, newStep, root);
                    }
                }
                if (parentStep == null)
                {
                    root.Add(newStep);
                }
            }
            else if (stepType == RecipeStepTypes.RunRecipe)
            {
                RunRecipeStep subStep = new RunRecipeStep();
                subStep.RecipePath = stepNode.Attributes["FileName"].Value;
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.Waiting)
            {
                WaitingStep subStep = new WaitingStep();
                subStep.Time = double.Parse(stepNode.Attributes["Time"].Value);
                if(stepNode.Attributes["ResetPump"] != null)
                {
                    subStep.ResetPump = bool.Parse(stepNode.Attributes["ResetPump"].Value);
                }
                else { subStep.ResetPump = true; }
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if (stepType == RecipeStepTypes.Comment)
            {
                CommentStep subStep = new CommentStep();
                subStep.Comment = stepNode.Attributes["Content"].Value;
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if(stepType == RecipeStepTypes.HomeMotion)
            {
                HomeMotionStep subStep = new HomeMotionStep();
                subStep.MotionType = (MotionTypes)Enum.Parse(typeof(MotionTypes), stepNode.Attributes["MotionType"].Value);
                subStep.Speed = double.Parse(stepNode.Attributes["Speed"].Value);
                subStep.WaitForComplete = bool.Parse(stepNode.Attributes["WaitComplete"].Value);
                if(parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if(stepType == RecipeStepTypes.AbsoluteMove)
            {
                AbsoluteMoveStep subStep = new AbsoluteMoveStep();
                subStep.MotionType = (MotionTypes)Enum.Parse(typeof(MotionTypes), stepNode.Attributes["MotionType"].Value);
                subStep.Speed = double.Parse(stepNode.Attributes["Speed"].Value);
                subStep.TargetPos = double.Parse(stepNode.Attributes["TargetPos"].Value);
                subStep.WaitForComplete = bool.Parse(stepNode.Attributes["WaitComplete"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if(stepType == RecipeStepTypes.RelativeMove)
            {
                RelativeMoveStep subStep = new RelativeMoveStep();
                subStep.MotionType = (MotionTypes)Enum.Parse(typeof(MotionTypes), stepNode.Attributes["MotionType"].Value);
                subStep.Speed = double.Parse(stepNode.Attributes["Speed"].Value);
                subStep.MoveStep = double.Parse(stepNode.Attributes["MoveStep"].Value);
                subStep.WaitForComplete = bool.Parse(stepNode.Attributes["WaitComplete"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if(stepType == RecipeStepTypes.HywireImaging)
            {
                HywireImagingStep subStep = new HywireImagingStep();
                subStep.CameraSN = stepNode.Attributes["CameraSN"].Value;
                subStep.ExposureTime = double.Parse(stepNode.Attributes["Exposure"].Value);
                subStep.Gain = int.Parse(stepNode.Attributes["Gain"].Value);
                subStep.ADCBitDepth = int.Parse(stepNode.Attributes["ADCBitDepth"].Value);
                subStep.PixelBitDepth = int.Parse(stepNode.Attributes["PixelBitDepth"].Value);
                subStep.ROI = Int32Rect.Parse(stepNode.Attributes["ROI"].Value);
                subStep.LED = (LEDTypes)Enum.Parse(typeof(LEDTypes), stepNode.Attributes["LED"].Value);
                subStep.Intensity = int.Parse(stepNode.Attributes["Intensity"].Value);
                if (parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
            else if(stepType == RecipeStepTypes.LEDCtrl)
            {
                LEDControlStep subStep = new LEDControlStep();
                subStep.LED = (LEDTypes)Enum.Parse(typeof(LEDTypes), stepNode.Attributes["LED"].Value);
                subStep.Intensity = int.Parse(stepNode.Attributes["Intensity"].Value);
                subStep.SetOn = bool.Parse(stepNode.Attributes["SetOn"].Value);
                if(parentStep == null)
                {
                    root.Add(new StepsTree(null, subStep));
                }
                else
                {
                    parentStep.AppendSubStep(subStep);
                }
            }
        }
    }
}
