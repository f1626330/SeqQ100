using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public enum DataTransferStatusEnum
    {
        None,
        Start,
        End,
        Cancel,
        Completed,
        Failed,
    }
    public class DataTransferEventArgs
    {
        public DataTransferStatusEnum DataTransferStatus { get; set; }
    }
    public class DataTransferDefaultViewModel : PageViewBaseViewModel
    {
        string _SelectedExp;
        public string SelectedExp { get=> _SelectedExp; set=>SetProperty(ref _SelectedExp, value); }
        private string ImageBaseDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.GetRecipeRunImagingBaseDir();
        ObservableCollection<string> _ExpOptions;
        public ObservableCollection<string> ExpOptions { get=> _ExpOptions; set=>SetProperty(ref _ExpOptions, value); }
        ObservableCollection<DriveInfo> _DriveOptions;
        public ObservableCollection<DriveInfo> DriveOptions { get=> _DriveOptions; set=>SetProperty(ref _DriveOptions, value); }
        DriveInfo _SelectedDrive;
        public DriveInfo SelectedDrive { get=> _SelectedDrive; set=>SetProperty(ref _SelectedDrive, value); }
        bool _IsSaveOrg;
        public bool IsSaveOrg { get=> _IsSaveOrg; set=>SetProperty(ref _IsSaveOrg, value); }
        bool _IsTransfering;
        public bool IsTransfering { get => _IsTransfering; set => SetProperty(ref _IsTransfering, value); }

        bool _IsUpload;
        public bool IsUpload { get => _IsUpload; set => SetProperty(ref _IsUpload, value); }


        bool _IncludeImageData = true;
        bool _IsOnlyFastqSummary = false;
        public bool IncludeImageData { get => _IncludeImageData; set => SetProperty(ref _IncludeImageData, value); }
        public bool IsOnlyFastqSummary { get => _IsOnlyFastqSummary; set => SetProperty(ref _IsOnlyFastqSummary, value); }
        bool IsAbort { get; set; }
        public event EventHandler<DataTransferEventArgs> OnTransfering;
        //public event EventHandler OnTransferCanceled;
        //public DataTransferStatusEnum DataTransferStatus { get; set; }
        public DataTransferDefaultViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            Show_WizardView_Button_MoverPrevious = false;
            Description = "Transfer Experiment Data";
            ExpOptions = new ObservableCollection<string>();
            DriveOptions = new ObservableCollection<DriveInfo>();
            string[] files = Directory.GetDirectories(ImageBaseDir);
            foreach (string filepath in files)
            {
                ExpOptions.Add(Path.GetFileName(filepath));
            }
            if (ExpOptions.Count > 0)
            {
                SelectedExp = ExpOptions[0];
            }
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!ImageBaseDir.Contains(drive.Name) && !drive.Name.Contains("C"))
                {
                    DriveOptions.Add(drive);
                }
            }
            if (DriveOptions.Count > 0)
            {
                SelectedDrive = DriveOptions[0];
            }
            else
            {
                SeqApp.UpdateAppMessage("Please add extra harddisk for tranferring", AppMessageTypeEnum.Warning);
            }
            IsSaveOrg = true;
            CanCancelPage = false;
        }

        string _Instruction = "Data Transfer Instruction";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }

        public override string DisplayName => "Data Transfer";

        internal override bool IsPageDone()
        {
            return true;
        }

        #region Transfer cmd
        private ICommand _TransferCmd = null;
        public ICommand TransferCmd
        {
            get
            {
                if (_TransferCmd == null)
                {
                    _TransferCmd = new RelayCommand(o => Transfer(o), o => CanTransfer);
                }
                return _TransferCmd;
            }
        }

        bool _CanTransfer = true;
        public bool CanTransfer
        {
            get
            {
                return _CanTransfer;
            }
            set
            {
                SetProperty(ref _CanTransfer, value, nameof(CanTransfer), true);
            }
        }


        private async void Transfer(object o)
        {
            DataTransferStatusEnum dataTransferStatus = DataTransferStatusEnum.None;
            //InvokeStatusEvent(dataTransferStatus);
            
            CanTransfer = false;
            PageNavigator.CanMoveToNextPage = false;
            IsSaveOrg = Convert.ToBoolean(o);
            IsAbort = false;
            IsTransfering = true;
             MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                //Message = string.Format("Transfer experiment data: {0} and {1} the source data ?", SelectedExp, IsSaveOrg ? "Save" : "Delete"),
                Caption = "Confirm Data Transferring",
                Image = MessageBoxImage.Question,
                Buttons = MessageBoxButton.YesNo,
                IsModal = true,
            };
            if (IsSaveOrg)
            {
                msgVm.Message = $"Copy experiment data from {SelectedExp} to {SelectedDrive}?";
            }
            else
            {
                msgVm.Message = $"Move experiment data from {SelectedExp} to {SelectedDrive}, and delete the source data?";
            }
            
            // var msgResult = MessageBox.Show(string.Format("Transfer experiment data: {0} and {1} the source data ?", SelectedExp, IsSaveOrg?"Save":"Delete"), "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            //if (msgResult == MessageBoxResult.OK)
            if (msgVm.Show(this.DialogService.Dialogs) == MessageBoxResult.Yes)
            {
                try
                {
                     dataTransferStatus = DataTransferStatusEnum.Start;
                    InvokeStatusEvent(dataTransferStatus);
                    bool doTransfer = true;
                    //string targetPath = SelectedDrive.Name + "\\" + SettingsManager.ConfigSettings.InstrumentInfo.InstrumentName + "\\";
                    string targetPath = SelectedDrive.Name;
                    string destFolder = Path.Combine(targetPath, SelectedExp);
                    if (Directory.Exists(destFolder))
                    {
                        msgVm = new MessageBoxViewModel()
                        {
                            Caption = "Overwrite Folder?",
                            Message = $"The following folder already exists: \n{destFolder}\nDo you want to overwrite it? ",
                            Image = MessageBoxImage.Question,
                            Buttons = MessageBoxButton.YesNo,
                            IsModal = true,
                        };

                        if (msgVm.Show(this.DialogService.Dialogs) != MessageBoxResult.Yes)
                        {
                            SeqApp.UpdateAppMessage("Transfer is canceled.", AppMessageTypeEnum.Warning);
                            dataTransferStatus = DataTransferStatusEnum.Cancel;
                            doTransfer = false;
                        }
                        
                    }

                    if (doTransfer)
                    {
                        string sourceFolder = Path.Combine(ImageBaseDir, SelectedExp);
                        CanCancelPage = true;
                        string strError = await Task<bool>.Run(() => { return DirectoryCopy(sourceFolder, destFolder, true, IncludeImageData, IsOnlyFastqSummary); });
                        if (!string.IsNullOrEmpty(strError))
                        {
                            dataTransferStatus = DataTransferStatusEnum.Failed;
                            SeqApp.UpdateAppMessage($"Failed transfer experiment with error: {strError}", AppMessageTypeEnum.Error);
                        }
                        else
                        {
                            bool hasError = false;
                            if (!IsSaveOrg && !IsAbort)
                            {
                                SeqApp.UpdateAppMessage($"Please wait while deleting source data folder: {sourceFolder}");

                                hasError = !await Task<bool>.Run(() =>
                                {
                                    try
                                    {
                                        Directory.Delete(sourceFolder, true);
                                        return true;
                                    }
                                    catch (Exception ex2)
                                    {

                                        SeqApp.UpdateAppMessage($"Experiment is transfered, but it failed to delete source experiment {sourceFolder} with error {ex2.Message}", AppMessageTypeEnum.Warning);
                                        return false;
                                    }

                                });

                                if (!hasError)
                                {
                                    bool b = ExpOptions.Remove(Path.GetFileName(sourceFolder));
                                    if (b && ExpOptions.Count > 0)
                                    {
                                        SelectedExp = ExpOptions[0];
                                    }
                                }
                            }

                            if (hasError)
                            {
                                dataTransferStatus = DataTransferStatusEnum.Failed;
                            }
                            else
                            {
                                //MessageBox.Show(string.Format("Experiment Transfered: {0}", SelectedExp));
                                if (IsAbort)
                                {
                                    dataTransferStatus = DataTransferStatusEnum.Cancel;
                                    SeqApp.UpdateAppMessage(string.Format("Experiment transferring was aborted: {0}", SelectedExp), AppMessageTypeEnum.Warning);
                                }
                                else
                                {
                                    dataTransferStatus = DataTransferStatusEnum.Completed;
                                    SeqApp.UpdateAppMessage(string.Format("Experiment Transfered: {0}", SelectedExp));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    dataTransferStatus = DataTransferStatusEnum.Failed;
                    SeqApp.UpdateAppMessage($"Failed to transfer experiment data with error: {ex.Message}", AppMessageTypeEnum.Error);
                }
                finally
                {
                    CanCancelPage = false;
                }
            }
            else
            {
                //MessageBox.Show("Transfer is canceled.");
                dataTransferStatus = DataTransferStatusEnum.Cancel;
                SeqApp.UpdateAppMessage("Transfer is canceled.", AppMessageTypeEnum.Warning);
                ///return;
            }
            IsTransfering = false;
            CanTransfer = true;
            PageNavigator.CanMoveToNextPage = true;
            if (dataTransferStatus == DataTransferStatusEnum.Start)
            {
                dataTransferStatus = DataTransferStatusEnum.End; //should never be here
            }
            InvokeStatusEvent(dataTransferStatus);
            //OnTransfering?.Invoke(this, new DataTransferEventArgs() { DataTransferStatus = DataTransferStatusEnum.End });
            //if (IsAbort)
            //{
            //    //OnTransferCanceled?.Invoke(this, null);
            //    InvokeStatusEvent(DataTransferStatusEnum.Cancel);
            //}
        }
        #endregion Transfer cmd
        private string DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, bool includeImages, bool onlyfastqSummary)
        {

            try
            {// Get the subdirectories for the specified directory.
                DirectoryInfo dir = new DirectoryInfo(sourceDirName);

                if (!dir.Exists)
                {
                    //throw new DirectoryNotFoundException(
                    //  "Source directory does not exist or could not be found: "
                    // + sourceDirName);
                    return $"Source directory does not exist or could not be found: {sourceDirName} ";

                }
                if (onlyfastqSummary) { IncludeImageData = false; }
                List<DirectoryInfo> dirs = dir.GetDirectories().ToList<DirectoryInfo>();
                if (!includeImages)
                {
                    dirs.RemoveAll(x => string.Compare(x.Name, "Data", false) == 0);
                }
                if (onlyfastqSummary)
                {
                    dirs.RemoveAll(x => string.Compare(x.Name, "fastq", false) == 1 );
                }
                // If the destination directory doesn't exist, create it.       
                Directory.CreateDirectory(destDirName);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    if (IsAbort)
                    {
                        break;
                    }
                    if(onlyfastqSummary && !file.Name.Contains("Report") && !destDirName.Contains("fastq"))
                    {
                        continue; // skip all other file other than Summary Report and file under fastq folder if onlyfastqSummary is true
                    }
                    string tempPath = Path.Combine(destDirName, file.Name);
                    SeqApp.UpdateAppMessage($"copy {file.FullName} to {tempPath}");
                    file.CopyTo(tempPath, true); // override false);
                }

                // If copying subdirectories, copy them and their contents to new location.
                if (copySubDirs)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        if (IsAbort)
                        {
                            break;
                        }
                        string tempPath = Path.Combine(destDirName, subdir.Name);
                        DirectoryCopy(subdir.FullName, tempPath, copySubDirs, includeImages, false); //only top level "Data" to be exclude if not IncludeImageData = false
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                return $"{ex.Message}";
            }
        }
        #region RefreshDriveInfo cmd
        private ICommand _RefreshDriCmd = null;
        public ICommand RefreshDriCmd
        {
            get
            {
                if (_RefreshDriCmd == null)
                {
                    _RefreshDriCmd = new RelayCommand(o => RefreshDri(o), o => CanRefreshDri);
                }
                return _RefreshDriCmd;
            }
        }

        bool _CanRefreshDri = true;
        public bool CanRefreshDri
        {
            get
            {
                return _CanRefreshDri;
            }
            set
            {
                SetProperty(ref _CanRefreshDri, value, nameof(CanRefreshDri), true);
            }
        }


        private void RefreshDri(object o)
        {
            CanRefreshDri = false;
            DriveOptions.Clear();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!ImageBaseDir.Contains(drive.Name) && !drive.Name.Contains("C"))
                {
                    DriveOptions.Add(drive);
                }
            }
            SelectedDrive = DriveOptions?[0];
            CanRefreshDri = true;
        }

        public  override  bool CanCelPage()
        {
            IsAbort = true;
            //to do : wait for transferring end
            CanCancelPage = false;
            return true; //true -- handled it.
        }

        void InvokeStatusEvent(DataTransferStatusEnum st)
        {
            
            OnTransfering?.Invoke(this, new DataTransferEventArgs() { DataTransferStatus = st});
        }

        #endregion RefreshDri cmd
    }
}
