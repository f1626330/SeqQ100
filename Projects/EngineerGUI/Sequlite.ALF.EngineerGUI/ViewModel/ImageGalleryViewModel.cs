using Microsoft.Win32;
using Sequlite.WPF.Framework;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Sequlite.Image.Processing;
using Sequlite.ALF.Common;
using System.Windows.Forms;
using System.IO;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class ImageGalleryViewModel : ViewModelBase
    {
        #region Constructor
        public ImageGalleryViewModel()
        {
        }
        #endregion Constructor

        #region Public Properties
        public ObservableCollection<FileViewModel> Files { get; } = new ObservableCollection<FileViewModel>();
        public ObservableCollection<PaneViewModel> Panes { get; } = new ObservableCollection<PaneViewModel>();

        private FileViewModel _ActiveFile;
        public FileViewModel ActiveFile
        {
            get { return _ActiveFile; }
            set
            {
                if (_ActiveFile != value)
                {
                    _ActiveFile = value;
                    RaisePropertyChanged(nameof(ActiveFile));
                }
            }
        }

        private PaneViewModel _ActivePane;
        public PaneViewModel ActivePane
        {
            get
            {
                return _ActivePane;
            }
            set
            {
                if (_ActivePane == value)
                {
                    return;
                }

                _ActivePane = value;

                RaisePropertyChanged(nameof(ActivePane));
            }
        }

        public int FileNameCount { get; set; }
        #endregion Public Properties

        #region Open Command
        private RelayCommand _OpenCmd;
        public ICommand OpenCmd
        {
            get
            {
                if (_OpenCmd == null)
                {
                    _OpenCmd = new RelayCommand(p => ExecuteOpenCmd(p));
                }
                return _OpenCmd;
            }
        }

        private void ExecuteOpenCmd(object p)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "TIF Files(.TIFF)|*.tif|JPG Files(.JPG)|*.jpg|BMP Files(.BMP)|*.bmp|All Files|*.*";
            dlg.Title = "Open File";
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            if (dlg.ShowDialog().GetValueOrDefault())
            {
                try
                {
                    var fileViewModel = new FileViewModel( dlg.FileName);
                    fileViewModel.OnClosingFile += FileViewModel_OnClosingFile;
                    Files.Add(fileViewModel);
                    ActiveFile = fileViewModel;
                }
                catch (Exception )
                {
                    System.Windows.MessageBox.Show("Can not open the image.");
                }
            }
        }

        private void FileViewModel_OnClosingFile(FileViewModel fileVM)
        {
            Dispatch (() =>CloseFile(fileVM));
        }

        #endregion Open Command

        #region Save Command
        private RelayCommand _SaveCmd;
        public ICommand SaveCmd
        {
            get
            {
                if (_SaveCmd == null)
                {
                    _SaveCmd = new RelayCommand(ExecuteSaveCmd, CanExecuteSaveCmd);
                }
                return _SaveCmd;
            }
        }

        private void ExecuteSaveCmd(object obj)
        {
            if (ActiveFile == null) { return; }
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "TIF|*.tif|TIFF|*.tiff";
            if (saveDialog.ShowDialog() == true)
            {
                ActiveFile.FilePath = saveDialog.FileName;
                try
                {
                    WriteableBitmap imageToBeSaved = null;
                    string filePath = ActiveFile.FilePath;
                    imageToBeSaved = ActiveFile.SourceImage;  // Save the source image
                    //imageToBeSaved = Sequlite.Image.Processing.ImageProcessing.Rotate(ActiveFile.SourceImage, 4);
                    ImageProcessing.Save(filePath, imageToBeSaved, ActiveFile.ImageInfo, false);
                    ActiveFile.IsDirty = false;
                    ActiveFile.Title = saveDialog.FileName;
                    Thread QC = new Thread(() => new FindQCFromImage(filePath));
                    QC.Name = "QC Save";
                    QC.Start();
                }
                catch(Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.ToString());
                    throw;
                }
            }
        }

        private bool CanExecuteSaveCmd(object obj)
        {
            return true;
        }
        #endregion Save Command

        #region CloseAll Command
        private RelayCommand _CloseAllCmd;
        public RelayCommand CloseAllCmd
        {
            get
            {
                if (_CloseAllCmd == null)
                {
                    _CloseAllCmd = new RelayCommand(ExecuteCloseAllCmd, CanExecuteCloseAllCmd);
                }
                return _CloseAllCmd;
            }
        }

        private void ExecuteCloseAllCmd(object obj)
        {
            if (Files.Count == 0)
            {
                return;
            }

            foreach(var img in Files)
            {
                if (img.IsDirty)
                {
                    var close = System.Windows.MessageBox.Show("There are images unsaved, are you sure to close all?", "Close Image...", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if(close != MessageBoxResult.OK)
                    {
                        return;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            while (Files.Count > 0)
            {
                var img = Files[0];
                img.IsDirty = false;
                CloseFile(img);
            }
        }

        private bool CanExecuteCloseAllCmd(object obj)
        {
            return true;
        }
        #endregion CloseAll Command

        #region Compress Command
        private RelayCommand _CompressCmd;
        public ICommand CompressCmd
        {
            get
            {
                if (_CompressCmd == null)
                {
                    _CompressCmd = new RelayCommand(p => ExecuteCompressCmd(p));
                }
                return _CompressCmd;
            }
        }

        private void ExecuteCompressCmd(object p)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();
                
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    string[] files = Directory.GetFiles(fbd.SelectedPath);
                    foreach (string filepath in files)
                    {
                        if (filepath.Contains(".tif"))
                        {
                            var fileViewModel = new FileViewModel(filepath);
                            WriteableBitmap imageToBeSaved = null;
                            string nfilePath = fileViewModel.FilePath;
                            nfilePath = fileViewModel.FilePath.Replace(fileViewModel.FilePath.Substring(fileViewModel.FilePath.Length - 4), string.Format("({0}){1}", "comp", fileViewModel.FilePath.Substring(fileViewModel.FilePath.Length - 4)));
                            imageToBeSaved = fileViewModel.SourceImage;  // Save the source image
                                                                         //imageToBeSaved = Sequlite.Image.Processing.ImageProcessing.Rotate(ActiveFile.SourceImage, 4);
                            ImageProcessing.Save(nfilePath, imageToBeSaved, fileViewModel.ImageInfo, true);
                        }
                        
                    }

                }
            }
        }

        #endregion Compress Command

        #region Public Functions
        public bool CloseFile(FileViewModel file)
        {
            if (file.IsDirty)
            {
                var option = System.Windows.MessageBox.Show("Are you sure to close unsaved image?", "Image unsaved...", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (option == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }

            int nextItem = Files.IndexOf(file) - 1;
            bool bIsRemoveActiveDoc = (file == ActiveFile);
            
            Files.Remove(file);
            file.OnClosingFile -= FileViewModel_OnClosingFile;
            if (Files.Count == 0)
            {
                ActiveFile = null;
            }
            else
            {
                if (bIsRemoveActiveDoc)
                {
                    if (nextItem < 0)
                    {
                        nextItem = 0;
                    }
                    ActiveFile = Files[nextItem];
                }
            }
            file.Dispose();
            file = null;

            return true;
        }
        public bool NewDocument(WriteableBitmap image, Image.Processing.ImageInfo imageInfo, string title, bool isDirty = true)
        {
            bool result = false;
            FileViewModel newFile = new FileViewModel( image, imageInfo, title);
            newFile.IsDirty = isDirty;
            if (newFile != null)
            {
                newFile.IsDirty = isDirty;
                newFile.OnClosingFile += FileViewModel_OnClosingFile;
                Files.Add(newFile);
                ActiveFile = newFile;
                result = true;
            }
            return result;
        }
        #endregion Public Functions

        #region SliderManualContrastCommand

        private RelayCommand _SliderManualContrastCommand = null;
        public ICommand SliderManualContrastCommand
        {
            get
            {
                if (_SliderManualContrastCommand == null)
                {
                    _SliderManualContrastCommand = new RelayCommand(ExecuteSliderManualContrastCommand, CanExecuteSliderManualContrastCommand);
                }

                return _SliderManualContrastCommand;
            }
        }

        protected void ExecuteSliderManualContrastCommand(object parameter)
        {
            ActiveFile.ImageInfo.RedChannel.IsAutoChecked = false;
            ActiveFile.ImageInfo.GreenChannel.IsAutoChecked = false;
            //ActiveFile.ImageInfo.BlueChannel.IsAutoChecked = false;

            ActiveFile.UpdateDisplayImage();
        }

        protected bool CanExecuteSliderManualContrastCommand(object parameter)
        {
            return true;
        }

        #endregion

    }
}
