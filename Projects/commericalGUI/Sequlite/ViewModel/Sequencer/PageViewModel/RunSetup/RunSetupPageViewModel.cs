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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class RunSetupPageViewModel : PageViewBaseViewModel
    {
        static int TestNumber = 1;
        public RunSetupPageModel RunSetupModel { get; }
       
        static string InitialSampleSheetDir = "";
        IRunSetup _RunSetup;
        public RunSetupPageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) :
            base(seqApp,_PageNavigator, dialogs)
        {
            
            Description = Descriptions.RunSetupPage_Description;
            _RunSetup = seqApp.CreateRunSetupInterface();
            List<TemplateOptions> templateOptions = new List<TemplateOptions>();
            List<TemplateOptions> indexTemplateOptions = new List<TemplateOptions>();
            foreach (TemplateOptions template in Enum.GetValues(typeof(TemplateOptions)))
            {
                if (TemplateOptionsHelper.IsIndexTemplate(template))
                {
                    indexTemplateOptions.Add(template);
                }
                else
                {
                    templateOptions.Add(template);
                }
            }
            RunSetupModel = new RunSetupPageModel()
            {
                Read1Value = 50,
                //EnableRead1 = true,
                EnableIndex1 = false,
                Index1Value = 0,
                Index2Value = 0,
                RunName = $"test{TestNumber}",
                Description = $"test{TestNumber++}",
                IsEnableOLA = true,
                IsEnablePP = false,
                IsCG = true,
                Templateoptions = templateOptions,
                IndexTemplateoptions = indexTemplateOptions,
                //SelectedTemplate = templateOptions[0],
                //SelectedIndTemplate = templateOptions[7],
                RunSetupInterface = _RunSetup,
            };
            if (templateOptions.Count > 0)
            {
                RunSetupModel.SelectedTemplate = templateOptions[0];
            }

            if (indexTemplateOptions.Count > 0)
            {
                RunSetupModel.SelectedIndTemplate = indexTemplateOptions[0];
            }
           

            _PageNavigator.AddPageModel(SequencePageTypeEnum.RunSetup, RunSetupModel);
        }

        public override string DisplayName => Strings.PageDisplayName_RunSetup;

        internal override bool IsPageDone()
        {
            return !RunSetupModel.HasError;
        }

        private string _Instruction = Instructions.RunSetupPage_Instruction;



        public override string Instruction
        {
            get
            {
                return HtmlDecorator.CSS1 + _Instruction;
            }
            protected  set
            {
                _Instruction = value;
                RaisePropertyChanged(nameof(Instruction));
            }
        }

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

        private ICommand _SelectSampleSheetCmd = null;
        public ICommand SelectSampleSheetCmd
        {
            get
            {
                if (_SelectSampleSheetCmd == null)
                {
                    _SelectSampleSheetCmd = new RelayCommand(o => SelectSampleSheet(o), o => CanSelectSampleSheet);
                }
                return _SelectSampleSheetCmd;
            }
        }

        bool _CanSelectSampleSheet = true;
        public bool CanSelectSampleSheet
        {
            get
            {
                return _CanSelectSampleSheet;
            }
            set
            {
                SetProperty(ref _CanSelectSampleSheet, value, nameof(CanSelectSampleSheet), true);
            }
        }

        void SelectSampleSheet(object o)
        {
            try
            {
                CanSelectSampleSheet = false;
                var dlg = new OpenFileDialogViewModel
                {
                    Filter = "Sample Sheet Files (*.*csv)|*.*csv",//|Sample Sheet Files (*.txt)|*.txt",
                    Multiselect = false

                };
                if (!string.IsNullOrEmpty(InitialSampleSheetDir))
                {
                    dlg.InitialDirectory = InitialSampleSheetDir;
                }
                if (File.Exists(RunSetupModel.SampleSheet))
                {
                    dlg.FileName = RunSetupModel.SampleSheet;
                }

                if (dlg.Show(DialogService.Dialogs))
                {
                    string fileName = dlg.FileName;

                    if (File.Exists(fileName))
                    {
                        //RunSetupModel.SampleSheet = fileName;
                        InitialSampleSheetDir = Path.GetDirectoryName(fileName);

                       ViewSampleSheet(fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to select a sample sheet file with error: {0}", ex.Message));
            }
            finally
            {
                CanSelectSampleSheet = true;
            }
        }

        void ViewSampleSheet(string fileName)
        {
            SampleSheetWindowViewModel vm = new SampleSheetWindowViewModel(true, DialogService);
            vm.DialogBoxClosing += Vm_DialogBoxClosing;
            
            if (vm.LoadSampleSheet(fileName))
            {
                vm.Title = fileName;
                vm.Show(DialogService.Dialogs);
                if (vm.IsOK )
                {
                    SampleSheetDataInfo sData = vm.ParseSampleSheet();
                    if (sData != null)
                    {
                        RunSetupModel.SampleSheetData = sData;
                    }
                    RunSetupModel.CurrentlyLoadedFile = fileName;
                    RunSetupModel.SampleSheet = "";
                    RunSetupModel.SampleSheet = fileName;
                    if (!RunSetupModel.HasError && sData != null)
                    {
                        RunSetupModel.Read1Value = sData.Reads;
                        RunSetupModel.Description = sData.Description;
                        RunSetupModel.RunName = sData.ExpName;
                        if (sData.Index > 0)
                        {
                            RunSetupModel.EnableIndex1 = true;
                            RunSetupModel.Index1Value = sData.Index;
                            //RunSetupModel.SampleSheetData = sData;
                        }
                    }
                }
            }
            else
            {
                Logger.LogError(string.Format("Failed to load sample sheet file: {0}", fileName));
            }
            vm.DialogBoxClosing -= Vm_DialogBoxClosing;
        }
       

        private void Vm_DialogBoxClosing(object sender, EventArgs e)
        {
            SampleSheetWindowViewModel vm = sender as SampleSheetWindowViewModel;
            if (vm?.IsOK == true)
            {
                SampleSheetDataInfo sData = vm?.ParseSampleSheet();
                if (sData != null)
                {
                    RunSetupModel.Read1Value = sData.Reads;
                    if (sData.Index > 0)
                    {
                        RunSetupModel.EnableIndex1 = true;
                        RunSetupModel.Index1Value = sData.Index;
                        RunSetupModel.SampleSheetData = sData;
                    }
                }


            }
        }
    }
}
