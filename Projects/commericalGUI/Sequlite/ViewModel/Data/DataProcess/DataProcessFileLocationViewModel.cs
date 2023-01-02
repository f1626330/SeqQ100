using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class DataProcessFileLocationViewModel : PageViewBaseViewModel
    {
        public DataProcessInfoModel DataProcessInfo { get; }
        // ISequence SequenceApp { get; set; }
        UserPageModel UserModel { get; }
        public ISequence SequenceApp { get; set; }
        List<TileItem> SelectedTiles { get; set; }

        HashSet<SeqTile> _TileIntersection;
        HashSet<SeqTile> _TileUnion;
        public DataProcessFileLocationViewModel(ISeqApp seqApp, UserPageModel userModel, IPageNavigator _PageNavigator = null, IDialogService dialogs = null) :
            base(seqApp, _PageNavigator, dialogs)
        {
            Description = "Select Run Info File and Output Directory";
            UserModel = userModel;
            _PageNavigator.AddPageModel("UserModel", UserModel);
            DataProcessInfo = new DataProcessInfoModel(seqApp);
            _PageNavigator.AddPageModel("DataProcessInfo", DataProcessInfo);

        }

        string _Instruction = "Instruction for Off-line Data Processing";
        public override string Instruction { get => HtmlDecorator.CSS1 + _Instruction; protected set => SetProperty(ref _Instruction, value, true); }

        //public string DataDirectoryDescription => "A folder that contains a valid sequence info json file and a list of sequence images to be processed";
        //public string OuputDirectoryDescription => "A folder that will creates sub-folder to save outputs from data processing";
        public override void OnUpdateCurrentPageChanged()
        {
            base.OnUpdateCurrentPageChanged();
            DataProcessInfo.SessionId = UserModel.GetNewSessionId();
            SeqApp.UpdateAppMessage("Select settings for data processing");
        }

        public override string DisplayName => "Input/Output";

        internal override bool IsPageDone()
        {
            return !DataProcessInfo.HasError;
        }

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

        void SelectDataInfoFile(object o)
        {
            try
            {
                CanSelectDataInfoFile = false;
                var dlg = new OpenFileDialogViewModel()
                {
                    Title = "Select a run info file",
                    Filter = "Run Info File (*.*json)|*.*json|All files (*.*)|*.*",
                    Multiselect = false,
                    //FilterIndex = 2,
                    //FileName="info.json",
                    //CheckFileExists = true,

                };
                if (!string.IsNullOrEmpty(InitialInputDir))
                {
                    dlg.InitialDirectory = InitialInputDir;
                }

                if (dlg.Show(DialogService.Dialogs))
                {
                    string fileName = dlg.FileName;

                    if (fileName != DataProcessInfo.DataInputPath)
                    {
                        DataProcessInfo.SetDataInfo(fileName);

                        InitialInputDir = Path.GetDirectoryName(fileName);
                        IsAllTitleselected = true;
                        DataProcessInfo.SelectedTiles = null;
                        CheckTiles();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to select a run info file with error: {0}", ex.Message));
            }
            finally
            {
                CanSelectDataInfoFile = true;
            }
        }

        public string DataOutputDir
        {
            get => DataProcessInfo?.DataOutputDir;
            set
            {

                if (DataProcessInfo != null)
                {
                    DataProcessInfo.SetDataOutputDir(value);
                }
                InitialOutputDir = value;
            }
        }

        static string _InitialOutputDir;
        public string InitialOutputDir
        {
            get => _InitialOutputDir;
            set => SetProperty(ref _InitialOutputDir, value);
        }

        static string _InitialInputDir;
        public string InitialInputDir
        {
            get => _InitialInputDir;
            set => SetProperty(ref _InitialInputDir, value);
        }


        private ICommand _SelectTilesCmd = null;
        public ICommand SelectTilesCmd
        {
            get
            {
                if (_SelectTilesCmd == null)
                {
                    _SelectTilesCmd = new RelayCommand(o => SelectTiles(), o => CanSelectTilesCmd);
                }
                return _SelectTilesCmd;
            }
        }

        bool _CanSelectTilesCmd = true;
        public bool CanSelectTilesCmd
        {
            get
            {
                return _CanSelectTilesCmd;
            }
            set
            {
                SetProperty(ref _CanSelectTilesCmd, value, nameof(CanSelectTilesCmd), true);
            }
        }

        void CheckTiles()
        {
            if (SequenceApp == null)
            {
                SequenceApp = SeqApp.CreateSequenceInterface(false); //do not need hardware to be initialized
            }

            if (SequenceApp != null)
            {
                string baseDir = Path.GetDirectoryName(DataProcessInfo.DataInputPath);
                Dictionary<SequenceDataTypeEnum, List<SeqTile>> dseqList = SequenceApp.GetOfflineTileList(baseDir, DataProcessInfo.SeqInfo);

                var listofListTiles = dseqList.Values;
                _TileIntersection = listofListTiles.Skip(1).Aggregate(new HashSet<SeqTile>(listofListTiles.First()),
                             (h, e) => { h.IntersectWith(e); return h; });
                _TileUnion = listofListTiles.Skip(1).Aggregate(new HashSet<SeqTile>(listofListTiles.First()),
                             (h, e) => { h.UnionWith(e); return h; });

                CanSelectTilesCmd = _TileIntersection?.Count > 0;
            }
        }

        void SelectTiles()
        {
            //List<string> tileNamesList = null;
            //List<SeqTile> allAvailableSeqTilesList = null;
            List<TileItem> tileList = new List<TileItem>();
            if (SequenceApp == null)
            {
                SequenceApp = SeqApp.CreateSequenceInterface(false); //do not need hardware to be initialized
            }

            if (SequenceApp != null)
            {
                //string baseDir = Path.GetDirectoryName(DataProcessInfo.DataInputPath);
                //Dictionary<SequenceDataTypeEnum, List<SeqTile>> dseqList = SequenceApp.GetOfflineTileList(baseDir, DataProcessInfo.SeqInfo);
               
                //var listofListTiles = dseqList.Values;
                //var intersection = listofListTiles.Skip(1).Aggregate(new HashSet<SeqTile>(listofListTiles.First()),
                //             (h, e) => { h.IntersectWith(e); return h; } );
                //var union = listofListTiles.Skip(1).Aggregate(new HashSet<SeqTile>(listofListTiles.First()),
                //             (h, e) => { h.UnionWith(e); return h; });

                tileList = new List<TileItem>();
                bool isInAllRun;
                foreach (var it in _TileUnion)
                {
                    isInAllRun = _TileIntersection.Contains(it);
                    tileList.Add(new TileItem() { Name = it.Name, IsInAllRun = isInAllRun });
                }

                //allAvailableSeqTilesList = new List<SeqTile>();
                //foreach (var it in allTiles)
                //{
                //    allAvailableSeqTilesList.Add(it.Value);
                //}
            }
            

            //if(allAvailableSeqTilesList != null && allAvailableSeqTilesList.Count > 0)
            if(tileList.Count > 0)
            {
                TileSelectionViewModel vm = new TileSelectionViewModel(Logger, tileList, DataProcessInfo.SelectedTiles);
                //SequenceInfo seqInfo = DataProcessInfo.SeqInfo;
                //vm.BuildHeatMaps(allAvailableSeqTilesList,seqInfo.Rows, seqInfo.Column, seqInfo.Lanes.Length); 
                vm.Show(this.DialogService.Dialogs);
                //backup tile list in case cancel
                ObservableCollection<TileItem> preTileList = new ObservableCollection<TileItem>(vm.TileList);
                if (vm.IsOk)
                {
                    List<string> selectedTiles = new List<string>();
                    foreach (var it in vm.TileList)
                    {
                        if (it.UseTile)
                        {
                            selectedTiles.Add(it.Name);
                        }
                    }
                    if (selectedTiles.Count < vm.TileList.Count)
                    {
                        IsAllTitleselected = false;
                        DataProcessInfo.SelectedTiles = selectedTiles;
                    }
                }
                else
                {
                    vm.TileList = preTileList;
                }
            }
        }

        bool _IsAllTitleselected;
        public bool IsAllTitleselected
        {
            get => _IsAllTitleselected;
            set
            {
                if (SetProperty(ref _IsAllTitleselected, value))
                {
                    if (value && DataProcessInfo != null)
                    {
                        DataProcessInfo.SelectedTiles = null;
                    }
                }
            }
        }


    }
}
