using Sequlite.ALF.App;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class DataUploadViewModel : DialogViewModelBase
    {

        DataTransferDefaultViewModel _DataTransferVM;

        public DataTransferDefaultViewModel DataTransferVM { get => _DataTransferVM; set => SetProperty(ref _DataTransferVM, value); }
        public DataUploadViewModel(string experimentFolder, ISeqApp seqApp, IPageNavigator pageNavigator, IDialogService dialogService)
        {
            IsModal = true;
            DataTransferVM = new DataTransferDefaultViewModel(seqApp, pageNavigator, dialogService);
            DataTransferVM.SelectedExp = Path.GetFileName(experimentFolder);
            DataTransferVM.IsUpload = true;
            DataTransferVM.IncludeImageData = false;
            DataTransferVM.IsOnlyFastqSummary = false;
           // DataTransferVM.OnTransferCanceled += DataTransferVM_OnTransferCanceled;
            DataTransferVM.OnTransfering += DataTransferVM_OnTransfering;
            //this.DialogBoxClosing += DataUploadViewModel_DialogBoxClosing;       

        }

        WindowStyle _WinStyle = WindowStyle.ToolWindow;
        public WindowStyle WinStyle { get => _WinStyle; set => SetProperty(ref _WinStyle, value); }
        //private void DataUploadViewModel_DialogBoxClosing(object sender, EventArgs e)
        //{

        //}

        string _DataUploadError;
        public string DataUploadError { get => _DataUploadError; set => SetProperty(ref _DataUploadError, value); }

        private void DataTransferVM_OnTransfering(object sender, DataTransferEventArgs e)
        {
            switch (e.DataTransferStatus)
            {
                case DataTransferStatusEnum.None:
                    CanClose = false;
                    break;
                case DataTransferStatusEnum.Start:
                    DataUploadError = "";
                    CanOKCommand = true;
                    CanClose = false;
                    WinStyle = WindowStyle.None;
                    break;
                case DataTransferStatusEnum.End:
                   
                    //CanOKCommand = true;
                    break;
                case DataTransferStatusEnum.Cancel:
                    CanOKCommand = true;
                    Exit();
                    break;
                case DataTransferStatusEnum.Completed:
                    CanOKCommand = true;
                    Exit();
                    break;
                case DataTransferStatusEnum.Failed:
                    WinStyle = WindowStyle.ToolWindow;
                    DataUploadError = "Failed to upload data";
                    CanOKCommand = true;
                    CanClose = true;
                    break;
            }
        }

       

        protected override void RunOKCommand(object o)
        {
            CanOKCommand = false;
            if (DataTransferVM?.IsTransfering == true)
            {
                DataTransferVM?.CanCelPage();
            }
            else
            {
                Exit();
            }
        }

        void Exit()
        {
            CanClose = true;
            RequestClose();
            
        }

        private ICommand _WindowClosing = null;
        public ICommand WindowClosing
        {
            get
            {
                if (_WindowClosing == null)
                {
                    _WindowClosing = new RelayCommand(o => Closeing(o), o => CanClose);
                }
                return _WindowClosing;
            }
        }

        bool _canClose = true;
        public bool CanClose
        {
            get
            {
                return _canClose && DataTransferVM?.IsTransfering == false;
            }
            set
            {
                _canClose = value;
                RaisePropertyChanged(nameof(CanClose));
            }

        }

        private void Closeing(object obj)
        {

        }

        
    }
}
