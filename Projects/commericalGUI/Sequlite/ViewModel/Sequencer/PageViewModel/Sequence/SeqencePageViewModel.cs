using InteractiveDataDisplay.WPF;
using Microsoft.VisualBasic.FileIO;
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Sequlite.UI.ViewModel
{
    public class SeqencePageViewModel : SeqencePageBaseViewModel
    {
        public RunSetupPageModel RunSetupModel { get; }
        public LoadPageModel LoadModel { get; }
        public CheckHardwarePageModel CheckHardwareModel { get; }
        ISequncePageNavigator _SeqPageNavigator;
        public SeqencePageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) :
            base(seqApp, _PageNavigator, dialogs)
        {
            _SeqPageNavigator = _PageNavigator;
            Description = Descriptions.SequencePage_Description;
            _PageNavigator.AddPageModel(SequencePageTypeEnum.Sequence, SequenceStatus);
            UserModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.User) as UserPageModel;
            RunSetupModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.RunSetup) as RunSetupPageModel;
            CheckHardwareModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.Check) as CheckHardwarePageModel;
            LoadModel = _PageNavigator.GetPageModel(SequencePageTypeEnum.Load) as LoadPageModel;
            SequenceStatus.IsOLAEnabled = RunSetupModel.IsEnableOLA;
        }

        public override string DisplayName => Strings.PageDisplayName_Seqence;

        protected override async void RunSeqence()
        {
           // LoadPageModel loadPageModel = _SeqPageNavigator.GetPageModel(SequencePageTypeEnum.Load) as LoadPageModel;
            SequenceStatus.UserName = UserModel?.UserName;
            SequenceStatus.RunDescription = RunSetupModel.Description;
            SequenceStatus.RunName = RunSetupModel.RunName;
            SequenceStatus.IsOLAEnabled = RunSetupModel.IsEnableOLA;
            //SequenceStatus.SampleID = "N/A";// RunSetupModel.SampleID;
            //SequenceStatus.FlowCellID = loadPageModel?.FCBarcodeId;
            //SequenceStatus.ReagentID = loadPageModel?.ReagentRFID;
            StopRunByUser = false;
            SequenceStatus.IsSequenceDone = false;
            await Task.Run(() =>
            {

                bool done = false;
                try
                {
                    done = SequenceApp.Sequence(
                        new RunSeqenceParameters()
                        {
                            Readlength = RunSetupModel.Read1Value,
                            Paired = RunSetupModel.EnableRead2,
                            Index1Enable = RunSetupModel.EnableIndex1,
                            Index1Number = RunSetupModel.Index1Value,
                            Index2Enable = RunSetupModel.EnableIndex2,
                            Index2Number = RunSetupModel.Index2Value,
                            IsSimulation = IsSimulation,
                            IsEnableOLA = RunSetupModel.IsEnableOLA,
                            IsCG = RunSetupModel.IsCG,
                            IsEnablePP = RunSetupModel.IsEnablePP,
                            FocusedBottomPos = CheckHardwareModel.HardwareCheckData.FocusedBottomPos,
                            FocusedTopPos = CheckHardwareModel.HardwareCheckData.FocusedTopPos,
                            UserEmail = UserModel.Email,
                            SessionId = UserModel.CurrentSessionId,
                            ExpName = RunSetupModel.RunName,
                            SeqTemp = RunSetupModel.SelectedTemplate,
                            SeqIndexTemp = RunSetupModel.SelectedIndTemplate,
                            SampleID= RunSetupModel.SampleID,
                            FlowCellID = LoadModel.FCBarcodeId,
                            ReagentID = LoadModel.ReagentRFID,
                            SampleSheetData = RunSetupModel.SampleSheetData,
                        }) ;

                }
                catch (Exception ex)
                {
                    done = false;
                    Logger.LogError($"Sequence run failed: {ex.StackTrace}");

                }
            }).ContinueWith((o) =>
            {
                SequenceStatus.IsSequenceDone = !SequenceApp.Sequencerunning;
                SequenceStatus.SequenceInformation = SequenceApp?.SequenceInformation;
               
            });

            SequenceStatus.IsSequenceDone = !SequenceApp.Sequencerunning;
            SequenceStatus.SequenceInformation = SequenceApp?.SequenceInformation;
            if (SequenceStatus.IsSequenceDone)
            {
                PageNavigator.CanMoveToNextPage = true;
            }

            
        }

        private string _Instruction = Instructions.SequencePage_Instruction;

        public override string Instruction
        {
            get
            {
                return HtmlDecorator.CSS1 + _Instruction;
            }
            protected set
            {
                _Instruction = value;
                RaisePropertyChanged(nameof(Instruction));
            }
        }

        protected override async void StopRun()
        {

            await Task<bool>.Run(() =>
            {
                SequenceApp.StopSequence();
            });

        }

        protected override bool ConfirmCancel()
        {
            return ConfirmCancel("Sequence is running, do you want to abort it?", "Stop Sequence");
        }
    }

}
