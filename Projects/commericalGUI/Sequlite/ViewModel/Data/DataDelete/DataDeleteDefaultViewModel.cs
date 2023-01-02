using Sequlite.ALF.App;
using Sequlite.ALF.Common;
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
    public class DataDeleteDefaultViewModel : PageViewBaseViewModel
    {
        public string SelectedExp { get; set; }
        private string ImageBaseDir = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig.GetRecipeRunImagingBaseDir();
        public List<string> ExpOptions { get; set; }
        public DataDeleteDefaultViewModel(ISeqApp seqApp, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            Show_WizardView_Button_MoverPrevious = false;
            Description = "Default Data Delete Page";
            
            ExpOptions = new List<string>();
            string[] files = Directory.GetDirectories(ImageBaseDir);
            foreach (string filepath in files)
            {
                ExpOptions.Add(Path.GetFileName(filepath));
            }
            if (ExpOptions.Count > 0) { SelectedExp = ExpOptions[0]; }
        }

        string _Instruction = "Data Delete Instruction";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }

        public override string DisplayName => "Data Delete";

        internal override bool IsPageDone()
        {
            return true;
        }

        #region Delete cmd
        private ICommand _DeleteCmd = null;
        public ICommand DeleteCmd
        {
            get
            {
                if (_DeleteCmd == null)
                {
                    _DeleteCmd = new RelayCommand(o => Delete(o), o => CanDelete);
                }
                return _DeleteCmd;
            }
        }

        bool _CanDelete = true;
        public bool CanDelete
        {
            get
            {
                return _CanDelete;
            }
            set
            {
                SetProperty(ref _CanDelete, value, nameof(CanDelete), true);
            }
        }


        private void Delete(object o)
        {
            string expfolder = Path.Combine(ImageBaseDir, SelectedExp);
            var msgResult = MessageBox.Show(string.Format("Delete experiment data: {0} ?", SelectedExp), "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (msgResult == MessageBoxResult.OK)
            {
                var msgResult1 = MessageBox.Show("Delete Fastq and Summary report?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (msgResult1 == MessageBoxResult.Yes)
                {
                    Directory.Delete(expfolder, true);
                    MessageBox.Show(string.Format("Experiment deleted: {0}", SelectedExp));
                }
                else if (msgResult1 == MessageBoxResult.No)
                {
                    DirectoryInfo dir = new DirectoryInfo(expfolder);
                    List<DirectoryInfo> dirs = dir.GetDirectories().ToList<DirectoryInfo>();
                    dirs.RemoveAll(x => string.Compare(x.Name, "fastq", false) == 0);
                    FileInfo[] files = dir.GetFiles();
                    foreach (FileInfo file in files)
                    {
                        if (!file.Name.Contains("Report")) { File.Delete(Path.Combine(expfolder, file.Name)); }
                    }
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        Directory.Delete(Path.Combine(expfolder, subdir.Name), true); 
                    }
                }
            }
            else if (msgResult == MessageBoxResult.Cancel)
            {
                MessageBox.Show("Deletion is cancelled.");
                return;
            }
        }
        #endregion delete cmd
    }
}
