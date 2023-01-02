using Fasterflect;
using Omu.ValueInjecter;
using Sequlite.ALF.App;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{

    public class SampleSheetWindowViewModel : DialogViewModelBase
    {
        readonly string ReadKey = "[Reads]";
        readonly string LaneKey = "Lane";
        readonly string IndexKey = "index";
        readonly string ExpKey = "Experiment Name";
        readonly string DepKey = "Description";
        IDialogService DialogService { get; }
        public bool IsOK { get; set; }
        public SampleSheetWindowViewModel(bool isModal = false, IDialogService dialogs = null )
        {
            IsModal = isModal;
            DialogService = dialogs;
        }

        #region IUserDialogBoxViewModel Implementation

        //exitcode: 1=== OK, 0 -- cancel
        public void Exit(int exitcode)
        {
            if (exitcode == 1)
            {
                if (SampleSheetVM.IsTextChanged)
                {
                    MessageBoxViewModel dlg = new MessageBoxViewModel()
                    {
                        Message = "Do you want to save changes?",
                        Caption = "Save Changes",
                        Image = MessageBoxImage.Question,
                        Buttons = MessageBoxButton.YesNo
                    };

                    if (dlg.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
                    {
                        Save();
                    }
                }
                IsOK = true;
            }
            Close();
            //if (this.DialogBoxClosing != null)
            //{
            //    this.DialogBoxClosing(this, new EventArgs());
            //}
        }
        
        #endregion IUserDialogBoxViewModel Implementation

        CSVFileViewModel _SampleSheetVM;
        public CSVFileViewModel SampleSheetVM
        {
            get
            {
                if (_SampleSheetVM == null)
                {
                    _SampleSheetVM = new CSVFileViewModel();
                }
                return _SampleSheetVM;
            }
        }

        public bool EnableEdit
        {
            get
            {
                return !SampleSheetVM.IsReadonly;
            }
            set
            {
               
                SampleSheetVM.IsReadonly = !value;
                RaisePropertyChanged(nameof(EnableEdit));
                
            }
        }

        private string _Title = "Sample Sheet";
        public string Title
        {
            get { return _Title; }
            set
            {
                SetProperty(ref _Title, value, nameof(Title));
            }
        }



        protected override void RunOKCommand(object o)
        {
            Exit(Convert.ToInt32(o));
        }

        ICommand _SaveSampleSheetCmd = null;
        public ICommand SaveSampleSheetCmd
        {
            get
            {
                if (_SaveSampleSheetCmd == null)
                {
                    _SaveSampleSheetCmd = new RelayCommand(o => Save(), o => SampleSheetVM.IsTextChanged && EnableEdit);
                }
                return _SaveSampleSheetCmd;
            }
        }

        void Save()
        {
            SampleSheetVM.SaveFile("", true);
        }

        public bool LoadSampleSheet(string fileName)
        {
            return  SampleSheetVM.LoadFile(fileName);
        }

        public SampleSheetDataInfo ParseSampleSheet()
        {
            bool? startParseRead = null;
            bool? startParseIndex = null;
            int indexCol = -1;
            //int indexId = 0;
            SampleSheetDataInfo sData = new SampleSheetDataInfo();
            foreach (var fields in SampleSheetVM.Lines)
            {

                if (fields.Count > 0)
                {
                    if(string.Compare(fields[0].Item, ExpKey, true) == 0)
                    {
                        sData.ExpName = fields[1].Item;
                    }
                    if (string.Compare(fields[0].Item, DepKey, true) == 0)
                    {
                        sData.Description = fields[1].Item;
                    }
                    if (startParseRead == null)
                    {
                        if (string.Compare(fields[0].Item, ReadKey, true) == 0)
                        {
                            startParseRead = true;
                        }
                    }
                    else if (startParseRead == true) //found read line
                    {
                        //find read value
                        int read = 0;
                        int.TryParse(fields[0].Item, out read);
                        sData.Reads = read;
                        startParseRead = false;
                    }

                    if (startParseIndex == null)
                    {
                        if (string.Compare(fields[0].Item, LaneKey, true) == 0)
                        {
                            indexCol = -1;
                            foreach (LineItem field in fields)
                            {
                                indexCol++;
                                if (string.Compare(field.Item, IndexKey, true) == 0)
                                {
                                    break;
                                }
                            }
                            startParseIndex = true;
                        }
                    }
                    else if (startParseIndex == true) //found index line
                    {

                        if (indexCol > 0 && fields?.Count > indexCol)//find index length
                        {
                            //sData.Index = fields[indexCol].Item.Length;

                            //parse lane#
                            int lane;
                            if (int.TryParse(fields[0].Item, out lane))
                            {
                                SampleLaneIndexDataInfo info = new SampleLaneIndexDataInfo()
                                {
                                    LaneNumber = lane,
                                    SampleId = fields[1].Item,
                                    SampleName = fields[2].Item,
                                    IndexSequnce = fields[indexCol].Item,
                                    IndexId = fields[5].Item,
                                };
                                
                                sData.AddSampleLaneDataInfo(info);
                            }
                        }
                        else
                        {
                            startParseIndex = false;
                        }
                    }
                }
            }//for
            sData.Index = sData.GetIndexLengthFromSampleSheet();
            return sData; 
        }
    }
}
