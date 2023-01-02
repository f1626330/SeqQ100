using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.UI.ViewModel
{
    public class SummaryPageViewModel : PageViewBaseViewModel
    {
        bool IsDataUploaded { get; set; }
        SequenceStatusModel SequenceStatus { get; }
        PostRunPageModel PostRunModel { get; }
        bool _DataVisualizationInitialized;
        RunSummaryViewModel _RunSummaryVM;
        public RunSummaryViewModel RunSummaryVM { get => _RunSummaryVM; set => SetProperty(ref _RunSummaryVM, value); }
        MultipleDataGraphViewModel _MultipleDataGraphVM;
        public MultipleDataGraphViewModel MultipleDataGraphVM { get => _MultipleDataGraphVM; set => SetProperty(ref _MultipleDataGraphVM, value); }
        SummarySampleSheetViewModel _SampleSheetVM;
        public SummarySampleSheetViewModel SampleSheetVM { get => _SampleSheetVM; set => SetProperty(ref _SampleSheetVM, value); }

        public SummaryPageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) : 
            base(seqApp,_PageNavigator, dialogs)
        {
            SequenceStatus = (SequenceStatusModel)_PageNavigator.GetPageModel(SequencePageTypeEnum.Sequence);
            PostRunModel = (PostRunPageModel)_PageNavigator.GetPageModel(SequencePageTypeEnum.PostRun);

            SubpageNames = new string[] {
                Strings.PageDisplayName_Summary_Charts_Graphs,
                };//Strings.PageDisplayName_Summary_Run,
                //Strings.PageDisplayName_Summary_SampleSheet };
            SubpageCount = SubpageNames.Length;
            CurrentSubpageIndex = 0;
            IsSubpageHeaderActive = true;
            CanSelectSubpage = true;
            RunSummaryVM = new RunSummaryViewModel();
        }

        public override void OnUpdateCurrentPageChanged()
        {
            //WizardView_Button_MovePrevious = Strings.SequenceWizardView_Button_MovePrevious_UploadData;
            WizardView_Button_MoveNext = Strings.SequenceWizardView_Button_MovePrevious_UploadData;
            //Show_WizardView_Button_Cancel = false;
            WizardView_Button_Cancel = Strings.SequenceWizardView_Button_Finish;
            if (CurrentSubpageIndex == 0 || CurrentSubpageIndex == 1)
            {
                OnUpdateSubpageInderChanged(CurrentSubpageIndex);
            }
        }

        protected override void OnUpdateSubpageInderChanged(int subpageIndex)
        {
            switch (CurrentSubpageIndex)
            {
                case 1:
                    if (RunSummaryVM.SummaryData == null)
                    {
                        List<SummaryDataItem> lst = PdfReportViewModel.GenerateReportItems(SequenceStatus, SequenceDataTypeEnum.Read1);
                        //RunSummaryVM = new RunSummaryViewModel(lst);
                        RunSummaryVM.SummaryData = new ObservableCollection<SummaryDataItem>(lst);
                    }
                    break;
                case  0:
                    {
                        if (SequenceStatus.IsOLAEnabled && SequenceStatus.SequenceInformation!= null && !_DataVisualizationInitialized)
                        {
                            MultipleDataGraphVM = new MultipleDataGraphViewModel(Logger, SequenceStatus, SequenceStatus.SequenceInformation);

                            List<SequenceDataTypeInfo> listSequenceDataTypeInfos = SequenceDataTypeInfo.GetListSequenceDataTypeInfos(SequenceStatus.SequenceInformation);
                            //if (InitDataVisualization(SequenceStatus.SequenceDataFeeder, SequenceStatus.SequenceInformation))
                            //{
                            //    FillOLAData(SequenceStatus.SequenceInformation);
                            //    _DataVisualizationInitialized = true;
                            //}
                            foreach (var it in listSequenceDataTypeInfos)
                            {
                               ISequenceDataFeeder sequenceDataFeeder = SequenceStatus.SequenceDataFeeder(it.SequenceDataType);// = new SequenceOLADataProcess(Logger, it.SequenceDataType);

                                if (MultipleDataGraphVM?.AddDataGraph(it.SequenceDataType, SequenceStatus.SequenceInformation, sequenceDataFeeder) == true)
                                {
                                    MultipleDataGraphVM?.FillOLAData(it.SequenceDataType, SequenceStatus.SequenceInformation.Cycles, SequenceStatus.SequenceInformation.Cycles);
                                }
                            }
                            _DataVisualizationInitialized = true;
                        }
                    }
                    break;
                case  2:
                    break;
            }
        }

        public override bool MoveToNextPage()
        {

            //MessageBox.Show("Coming soon: implementing sequence result upload.", "Data upload",
            //    MessageBoxButton.OK, MessageBoxImage.Information);
            DataUploadViewModel vm = new DataUploadViewModel(SequenceStatus.SequenceInformation.WorkingDir, SeqApp, PageNavigator, this.DialogService) ;
            vm.Show(this.DialogService.Dialogs);
            IsDataUploaded = true;
            return true;
        
        }

        

        //return true if don't want  wizard to handle cancel. --- used as finish
        public override bool CanCelPage()
        {
            //upload file here
            bool b = true;
            if (!IsDataUploaded)
            {
                MessageBoxViewModel msgVm = new MessageBoxViewModel()
                {
                    Message = "Cannot exit without uploading data first",
                    Caption = "Error",
                    Image = MessageBoxImage.Error,
                    Buttons = MessageBoxButton.OK,
                    IsModal = true
                };
                msgVm.Show(DialogService.Dialogs);
                //to do: move this flag set to button command handler
                IsDataUploaded = true;
            }
            else
            {

                if (SequenceStatus.IsDataBackupRunning)
                {
                    MessageBoxViewModel msgVm = new MessageBoxViewModel()
                    {
                        Message = "Cannot exit since image backup is still in progress, try go back sequence page and stop it.",
                        Caption = "Error",
                        Image = MessageBoxImage.Error,
                        Buttons = MessageBoxButton.OK,
                        IsModal = true
                    };
                    msgVm.Show(DialogService.Dialogs);
                }
                else if (PostRunModel.IsWashing)
                {
                    MessageBoxViewModel msgVm = new MessageBoxViewModel()
                    {
                        Message = "Cannot exit since post washing is still in progress.",
                        Caption = "Error",
                        Image = MessageBoxImage.Error,
                        Buttons = MessageBoxButton.OK,
                        IsModal = true
                    };
                    msgVm.Show(DialogService.Dialogs);
                }
                else if (!PostRunModel.IsWashDone)
                {
                    MessageBoxViewModel msgVm = new MessageBoxViewModel()
                    {
                        Message = "Washing was not completed, do you want to exit sequence?",
                        Caption = "Exit Sequence",
                        Image = MessageBoxImage.Question,
                        Buttons = MessageBoxButton.YesNo,
                        IsModal = true
                    };
                    if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
                    {
                        PageNavigator.CancelPage(false);

                    }

                }
                else
                {
                    b = false;//let wizard handle cancel
                }
            }
            
            return b;
        }

        public override string DisplayName => Strings.PageDisplayName_Summary;

        public override bool CanMoveOutSubpages
        {
            get
            {
                return true;
            }
        }

        public override bool CanMoveToPreviousSubpage
        {
            get { return true; }
        }

        public override bool IsOnFirstSubpage
        {
            get { return true; }
        }
        public override bool IsOnLastSubpage
        {
            get { return true; }
        }

        public override void MoveToPreviousSubpage()
        {
            if (CanMoveToPreviousSubpage)
            {
                //CurrentSubpage = Subpages[CurrentSubpageIndex - 1];
                CurrentSubpageIndex = 0;
            }
        }

        public override bool CanMoveToNextSubpage
        {
            get
            {
                return true;
            }
        }

        public override void MoveToNextSubpage()
        {
            if (CanMoveToNextSubpage)
            {
                CurrentSubpageIndex = SubpageCount - 1;
            }
        }

        
        internal override bool IsPageDone()
        {
            return true;
        }

        private string _Instruction = Instructions.SummaryPage_Instruction;

       
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

        string[] subPageDesps = new string[]
           {
                
               Descriptions.SummaryPage_Description_1 ,
               Descriptions.SummaryPage_Description_0 ,
               Descriptions.SummaryPage_Description_2 

           };
        protected override void SetSubPageDiscription(int pageIndex)
        {
            Description = subPageDesps[pageIndex];
        }

        //#region DATA_TABLE
        //DataInTableViewModel _DataInTableVM;
        //public DataInTableViewModel DataInTableVM { get => _DataInTableVM; set => SetProperty(ref _DataInTableVM, value); }
        //#endregion

        //#region LIN_GRAPHS
        //DataByCycleViewModel _DataByCycleVM;
        //public DataByCycleViewModel DataByCycleVM { get => _DataByCycleVM; set => SetProperty(ref _DataByCycleVM, value); }
        //#endregion //LINE_GRAPHS

        //#region HEATER_MAPS
        //DataByTileViewModel _DataByTileVM;
        //public DataByTileViewModel DataByTileVM { get => _DataByTileVM; set => SetProperty(ref _DataByTileVM, value); }
        //#endregion //HETAER_MAPS

        //bool InitDataVisualization(ISequenceDataFeeder sequenceDataFeeder, SequenceInfo sequenceInformation)
        //{
        //    if (sequenceDataFeeder == null || sequenceInformation == null)
        //    {
        //        return false;
        //    }
        //    DataByCycleVM = new DataByCycleViewModel(Logger, sequenceDataFeeder);
        //    DataByCycleVM.BuildLines();
        //    DataByCycleVM.SetTotalCycles(sequenceInformation.Cycles);
        //    int[] lanes = sequenceInformation.Lanes;
        //    DataByCycleVM.InitLanes(lanes);
        //    DataByCycleVM.MetricsDataItem2 = MetricsDataEnum.Intensity;

        //    DataInTableVM = new DataInTableViewModel(Logger, sequenceDataFeeder);

        //    DataByTileVM = new DataByTileViewModel(Logger, sequenceDataFeeder);
        //    DataByTileVM.BuildHeatMaps(sequenceInformation.Rows, sequenceInformation.Column,
        //        sequenceInformation.Lanes.Length); //   4, 45, 4);
        //    DataByTileVM.MetricsDataItem = MetricsDataEnum.Intensity;
        //    DataByTileVM.InitCycles();
        //    DataByTileVM.UpdateHeatMapOnCycles(sequenceInformation.Cycles, sequenceInformation.Cycles);
        //    return true;
        //}

        //void FillOLAData(SequenceInfo sequenceInformation)
        //{
        //    List<SequenceDataTableItems> tableItems = null;
        //    Task.Run(() =>
        //    {
        //        if (DataByCycleVM != null)
        //        {
        //            DataByCycleVM.WaitForLastLineDataFilled();
        //            DataByCycleVM.FillLineData();
        //        }
        //        tableItems = DataInTableVM?.FillDataTbale();
        //    }).ContinueWith((o) =>
        //    {
        //        DataByCycleVM?.SetLineDataFilled();
        //        Dispatch(() =>
        //        {
        //            //update line graphs 
        //            DataByCycleVM?.UpdateLineDataDisplayOnSlectedKye();
        //            //update table
        //            DataInTableVM?.UpdateDataTable(tableItems);
        //            //heat map update on cycle
        //            DataByTileVM?.UpdateHeatMapOnCycles(sequenceInformation.Cycles, sequenceInformation.Cycles);
        //        });
        //    });
        //}
    }
}

