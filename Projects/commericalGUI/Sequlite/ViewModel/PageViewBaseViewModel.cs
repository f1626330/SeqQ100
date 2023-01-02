using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;

namespace Sequlite.UI.ViewModel
{
    public abstract class PageViewBaseViewModel : ViewModelBase
    {
        protected ISeqLog Logger = SeqLogFactory.GetSeqFileLog("CUI"); //commercial UI
        public ISeqApp SeqApp { get; protected set; }


        protected IPageNavigator PageNavigator { get; }
        protected IDialogService DialogService { get; }
        protected PageViewBaseViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null)
        {
            SeqApp = seqApp;
            DialogService = dialogs;
            PageNavigator = _PageNavigator;
            SubpageNames = new string[0];
            IsSubpageHeaderActive = false; //true -- button click-able
            CanSelectSubpage = false;
            CurrentSubpageIndex = -1;
        }


        string _FileLocation;
        public string FileLocation { get => _FileLocation; set => SetProperty(ref _FileLocation, value); }

        PageStateModel _CurrentPageState = new PageStateModel();
        public PageStateModel CurrentPageState { get => _CurrentPageState; }
        public void SetCurrentPageState(bool enter)
        {
            if (enter)
            {
                CurrentPageState.PageState = PageStateEnum.EnterPage;
                CurrentPageState.IsPlayingAnimation = true;
                CurrentPageState.IsStopPlayingAnimation = false;
            }
            else
            {
                CurrentPageState.PageState = PageStateEnum.ExitPage;
                CurrentPageState.IsStopPlayingAnimation = true;
                CurrentPageState.IsPlayingAnimation = false;
            }
        }

        ObservableCollection<PageStatusModel> _StatusList;
        public ObservableCollection<PageStatusModel> StatusList
        {
            get
            {
                return _StatusList;
            }
            set
            {
                SetProperty(ref _StatusList, value, nameof(StatusList));
            }
        }

        public abstract string Instruction { get; protected set; }

        public abstract string DisplayName { get; }

        string _Description;
        public string Description
        {
            get
            {
                return _Description;
            }
            protected set
            {
                if (_Description != value)
                {
                    _Description = value;
                    RaisePropertyChanged(nameof(Description));
                }
            }
        }
        bool _isCurrentPage;
        public bool IsCurrentPage
        {
            get { return _isCurrentPage; }
            set
            {
                if (value != _isCurrentPage)
                {
                    _isCurrentPage = value;
                    RaisePropertyChanged(nameof(IsCurrentPage));
                }
            }
        }


        public bool IsSimulation
        {
            get => (PageNavigator != null) ? PageNavigator.IsSimulation : false;
            set
            {
                if (PageNavigator != null && PageNavigator.IsSimulation != value)
                {
                    PageNavigator.IsSimulation = value;
                    RaisePropertyChanged(nameof(IsSimulation));

                }
            }
        }

        /// <summary>
        /// Returns true if the user has finish in this page properly
        /// and the wizard should allow the user to progress to the 
        /// next page in the workflow.
        /// </summary>
        internal abstract bool IsPageDone();
        // protected bool IsPageBusy { get; set; }
        //sub pages ----------------------------------------------------------------
        protected virtual void SetSubPageDiscription(int pageIndex) { }
        protected virtual void OnUpdateSubpageInderChanged(int newSubpageIndex) { }
        public virtual void OnUpdateCurrentPageChanged() { }

        int _SubpageCount;
        protected int SubpageCount
        {
            get => _SubpageCount;
            set
            {
                if (_SubpageCount != value)
                {
                    _SubpageCount = value;
                    //if (_SubpageCount > 0)
                    //{
                    //    for (int i = 0; i < _SubpageCount; i++)
                    //    {
                    //        _PageStates.Add(new PageStateModel());
                    //    }
                    //}
                    //else
                    //{
                    //    _PageStates.Add(new PageStateModel());
                    //}
                }
            }
        }

        public bool IsSubpageHeaderActive { get; set; }

        public virtual bool IsOnFirstSubpage
        {
            get { return HasSubpages && CurrentSubpageIndex == 0; }
        }
        public virtual bool IsOnLastSubpage
        {
            get { return HasSubpages && CurrentSubpageIndex == SubpageCount - 1; }
        }

        string[] _SubpageNames = new string[0];
        public string[] SubpageNames
        {
            get { return _SubpageNames; }
            set {
                _SubpageNames = value;
                RaisePropertyChanged(nameof(SubpageNames));
            }
        }

        public virtual bool CanMoveOutSubpages
        {
            get
            {
                if (IsOnFirstSubpage || IsOnLastSubpage)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        int _CurrentSubpageIndex = -1;
        public int CurrentSubpageIndex
        {
            get
            {

                return _CurrentSubpageIndex;
            }
            set
            {
                if (_CurrentSubpageIndex != value)
                {
                    SetCurrentPageState(false);
                    _CurrentSubpageIndex = value;
                    SetCurrentPageState(true);
                    RaisePropertyChanged(nameof(CurrentSubpageIndex));
                    RaisePropertyChanged(nameof(IsOnLastSubpage));
                    RaisePropertyChanged(nameof(IsOnFirstSubpage));
                    SetSubPageDiscription(_CurrentSubpageIndex);
                    OnUpdateSubpageInderChanged(_CurrentSubpageIndex);
                }
            }
        }

        public bool HasSubpages => SubpageCount > 0;

        public virtual bool CanMoveToPreviousSubpage
        {
            get { return 0 < CurrentSubpageIndex; }
        }

        public virtual void MoveToPreviousSubpage()
        {
            if (CanMoveToPreviousSubpage)
            {
                //CurrentSubpage = Subpages[CurrentSubpageIndex - 1];
                CurrentSubpageIndex--;
            }
        }

        public virtual bool CanMoveToNextSubpage
        {
            get
            {
                return HasSubpages && IsPageDone() && CurrentSubpageIndex < SubpageCount - 1;
            }
        }

        public virtual void MoveToNextSubpage()
        {
            if (CanMoveToNextSubpage)
            {
                if (CurrentSubpageIndex < SubpageCount - 1)
                    CurrentSubpageIndex++;
            }
        }


        ICommand _SelectSubpage;
        public ICommand SelectSubpage
        {
            get
            {
                if (_SelectSubpage == null)
                    _SelectSubpage = new RelayCommand(
                        (o) => this.SelectSubpageCmd(o),
                        (o) => this.CanSelectSubpage);

                return _SelectSubpage;
            }
        }





        protected bool CanSelectSubpage { get; set; }

        void SelectSubpageCmd(Object o)
        {

            CurrentSubpageIndex = (int)o;

        }

        ICommand _StopPalyCommand;
        public ICommand StopPalyCommand
        {
            get
            {
                if (_StopPalyCommand == null)
                    _StopPalyCommand = new RelayCommand(
                        (o) => this.StopPalyCmd(o),
                        (o) => this.CanStopPalyCommand);

                return _StopPalyCommand;
            }
        }

        protected bool CanStopPalyCommand { get => CurrentPageState.IsPlayingAnimation; }

        void StopPalyCmd(Object o)
        {

            CurrentPageState.IsPlayingAnimation = false;

        }

        ICommand _StartPalyCommand;
        public ICommand StartPalyCommand
        {
            get
            {
                if (_StartPalyCommand == null)
                    _StartPalyCommand = new RelayCommand(
                        (o) => this.StartPalyCmd(o),
                        (o) => this.CanStartPalyCommand);

                return _StartPalyCommand;
            }
        }

        protected bool CanStartPalyCommand { get => !CurrentPageState.IsPlayingAnimation; }

        void StartPalyCmd(Object o)
        {

            CurrentPageState.IsPlayingAnimation = true;

        }

        //end sub pages ------------------------------------------------------------------------------

        public string WizardView_Button_Finish { get => Strings.SequenceWizardView_Button_Finish; set => throw new NotImplementedException(); }
        private string _MovePreviousText = Strings.SequenceWizardView_Button_MovePrevious;
        public string WizardView_Button_MovePrevious
        {
            get
            {
                return _MovePreviousText;
            }
            set
            {
                SetProperty(ref _MovePreviousText, value, nameof(WizardView_Button_MovePrevious));
            }
        }

        private string _MoveNextText = Strings.SequenceWizardView_Button_MoveNext;
        public string WizardView_Button_MoveNext
        {
            get
            {
                return _MoveNextText;
            }
            set
            {
                SetProperty(ref _MoveNextText, value, nameof(WizardView_Button_MoveNext));
            }
        }

        private string _CancelText = Strings.SeqenceWizardView_Button_Cancel;
        public string WizardView_Button_Cancel
        {
            get
            {
                return _CancelText;
            }
            set
            {
                SetProperty(ref _CancelText, value, nameof(WizardView_Button_Cancel));
            }
        }


        private bool _ShowMovePrevious = true;
        public bool Show_WizardView_Button_MoverPrevious
        {
            get
            {
                return _ShowMovePrevious;
            }
            set
            {
                SetProperty(ref _ShowMovePrevious, value, nameof(Show_WizardView_Button_MoverPrevious));
            }
        }

        private bool _ShowMoveNext = true;
        public bool Show_WizardView_Button_MoverNext
        {
            get
            {
                return _ShowMoveNext;
            }
            set
            {
                SetProperty(ref _ShowMoveNext, value, nameof(Show_WizardView_Button_MoverNext));
            }
        }

        private bool _ShowCancel = true;
        public bool Show_WizardView_Button_Cancel
        {
            get
            {
                return _ShowCancel;
            }
            set
            {
                SetProperty(ref _ShowCancel, value, nameof(Show_WizardView_Button_Cancel));
            }
        }

        //return true if don't want  wizard to handle cancel.
        public virtual bool CanCelPage()
        {
            return false;
        }

        bool _CanCancelPage = true;
        public bool CanCancelPage
        {
            get
            {
                return _CanCancelPage;
            }
            set
            {
                _CanCancelPage = value;
                RaisePropertyChanged(nameof(CanCancelPage));
                CommandManager.InvalidateRequerySuggested();
            }
        }


        //return true if don't want wizard to handle move to next.
        public virtual bool MoveToPreviousPage()
        {
            return false;
        }

        //return true if don't want wizard to handle move to next.
        public virtual bool MoveToNextPage()
        {
            return false;
        }
        protected virtual bool UpdateCurrentPageStatus(string statusName, PageStatusEnum status, string msg = "")
        {
            bool updated = false;
            PageStatusModel st = StatusList.Where(x => x.Name == statusName).FirstOrDefault();
            if (st != default(PageStatusModel))
            {
                st.Status = status;
                st.Message = msg;

                updated = true;
            }
            return updated;
        }
        protected void ShowPdfReport( SequenceStatusModel seqInfo, string initialOutputFilePath,
            string title)
        {
            CommonSaveFileDialog dlg = new CommonSaveFileDialog("Select where to save export Pdf");
            dlg.OverwritePrompt = true;
            if (!string.IsNullOrEmpty(initialOutputFilePath))
            {
                dlg.InitialDirectory = initialOutputFilePath;
            }
            else if (!string.IsNullOrEmpty(seqInfo?.SequenceInformation.WorkingDir))
            {
                dlg.InitialDirectory = seqInfo?.SequenceInformation.WorkingDir;
            }

            dlg.DefaultFileName = "Report.pdf";
            dlg.DefaultExtension = "pdf";
            dlg.AlwaysAppendDefaultExtension = true;
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                RunPdfReport(seqInfo, dlg.FileName, title, true, true);
            }
        }

        protected void RunPdfReport(SequenceStatusModel seqInfo,
            string pdfFileName, string title, bool useModalDialog = true, bool autoClose = false)
        {
            //List<SequenceDataTypeInfo> seqDataTypeList =  SequenceDataTypeInfo.GetListSequenceDataTypeInfos(seqInfo.SequenceInformation);
            //foreach (var it in seqDataTypeList)
            {
                //ISequenceDataFeeder dataFeeder = seqInfo.SequenceDataFeeder(it.SequenceDataType);
                PdfReportViewModel vm = new PdfReportViewModel(Logger, seqInfo, useModalDialog, autoClose)
                {
                    Title = title,
                    DataOutputPath = pdfFileName, //Path.ChangeExtension(pdfFileName, null) + "." + Path.GetExtension(pdfFileName),
                };
                vm.Show(this.DialogService.Dialogs);
            }
        }
    
    }
}
