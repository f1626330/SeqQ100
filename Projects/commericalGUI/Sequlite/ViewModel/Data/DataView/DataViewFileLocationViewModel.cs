using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class DataViewFileLocationViewModel : DataViewDefaultViewModel
    {
        public DataInfoFileModel DataInfoFile { get; }
        public DataViewFileLocationViewModel(ISeqApp seqApp, UserPageModel userModel, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) : base(seqApp, _PageNavigator, dialogs)
        {
            Description = "Select Data File Location";
            DataInfoFile = new DataInfoFileModel();
            _PageNavigator.AddPageModel("DataInfoFileName", DataInfoFile);
            
            _PageNavigator.AddPageModel("UserModel", userModel);
        }

        public override void OnUpdateCurrentPageChanged()
        {
            SeqApp.UpdateAppMessage("Select Data Info File");
            DataInfoFile.ValidateDataInfoFileName();
            PageNavigator.CanMoveToNextPage = !DataInfoFile.HasError;
        }
        public override string DisplayName => "Data Location";
        static string InitialDataInfoDir = "";
        

        private ICommand _SelectDataInfoFileCmd = null;
        public ICommand SelectDataInfoFileCmd
        {
            get
            {
                if (_SelectDataInfoFileCmd == null)
                {
                    _SelectDataInfoFileCmd = new RelayCommand(o => SelectDataInfoFile(o), o => CanSelectDataInfoFile);
                }
                return _SelectDataInfoFileCmd;
            }
        }

        bool _CanSelectDataInfoFile = true;
        public bool CanSelectDataInfoFile
        {
            get
            {
                return _CanSelectDataInfoFile;
            }
            set
            {
                SetProperty(ref _CanSelectDataInfoFile, value, nameof(CanSelectDataInfoFile), true);
            }
        }

        internal override bool IsPageDone()
        {
            return !DataInfoFile.HasError;
        }

        void SelectDataInfoFile(object o)
        {
            try
            {
                CanSelectDataInfoFile = false;
                var dlg = new OpenFileDialogViewModel()
                {
                    Filter = "Data Info File (*.*json)|*.*json|All files (*.*)|*.*",
                    Multiselect = false

                };
                if (!string.IsNullOrEmpty(InitialDataInfoDir))
                {
                    dlg.InitialDirectory = InitialDataInfoDir;
                }

                if (dlg.Show(DialogService.Dialogs))
                {
                    string fileName = dlg.FileName;

                    if (File.Exists(fileName))
                    {
                        DataInfoFile.DataInfoFileName = fileName;
                        InitialDataInfoDir = Path.GetDirectoryName(fileName);
                        //_IsPageDone = true;
                        
                    }
                    else
                    {
                        //_IsPageDone = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to select a data info file with error: {0}", ex.Message));
                //_IsPageDone = false;
                
            }
            finally
            {
                CanSelectDataInfoFile = true;
            }
        }

        
       
    }
}
