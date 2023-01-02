using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.UI.Model
{
    public class RunSetupPageModel : ModelBase
    {
        public RunSetupPageModel()
        {
            SampleSheet = _NoSampleSheet;
            CurrentlyLoadedFile = "";
        }

        public SampleSheetDataInfo SampleSheetData { get; set; }
        public IRunSetup RunSetupInterface { get; set; }
        string _RunName;
        public string RunName
        {
            get => _RunName;
            set
            {
                //if (string.IsNullOrEmpty(value))
                //{
                //    throw new ArgumentException("Run Name is missing.");
                //}
                if (!value.Any(Char.IsWhiteSpace))
                {
                    SetProperty(ref _RunName, value, nameof(RunName));
                }
                else
                {
                    MessageBox.Show("Please do not include whitespace in run name.");
                }
                
            }
        }

        string _Description;
        public string Description
        {
            get => _Description;
            set
            {
                SetProperty(ref _Description, value, nameof(Description));
            }
        }

        bool _EnableRead1 = true;
        public bool EnableRead1
        {
            get => _EnableRead1;
            set
            {
                SetProperty(ref _EnableRead1, value, nameof(EnableRead1));
            }
        }

        bool _EnableRead2;
        public bool EnableRead2
        {
            get => _EnableRead2;
            set
            {
                SetProperty(ref _EnableRead2, value, nameof(EnableRead2));
            }
        }

        bool _EnableIndex1;
        public bool EnableIndex1
        {
            get => _EnableIndex1;
            set
            {
                SetProperty(ref _EnableIndex1, value, nameof(EnableIndex1));
            }
        }

        bool _EnableIndex2;
        public bool EnableIndex2
        {
            get => _EnableIndex2;
            set
            {
                SetProperty(ref _EnableIndex2, value, nameof(EnableIndex2));
            }
        }

        int _Read1Value;
        public int Read1Value
        {
            get => _Read1Value;
            set
            {
                SetProperty(ref _Read1Value, value, nameof(Read1Value));
            }
        }

        ////int _Read2Value;
        //public int Read2Value
        //{
        //    get => Read1Value;
        //    set => Read1Value = value;
           
        //}

        int _Index1Value;
        public int Index1Value
        {
            get => _Index1Value;
            set
            {
                SetProperty(ref _Index1Value, value, nameof(Index1Value));
            }
        }

        int _Index2Value;
        public int Index2Value
        {
            get => _Index2Value;
            set
            {
                SetProperty(ref _Index2Value, value, nameof(Index2Value));
            }
        }

        string _SampleID = "N/A";
        public string SampleID
        {
            get => _SampleID;
            set
            {
                SetProperty(ref _SampleID, value);
            }
        }

        readonly string _NoSampleSheet = "N/A";
        string _previousSampleSheet;
        string _SampleSheet ;
        public string SampleSheet
        {
            get => _SampleSheet;
            set => SetProperty(ref _SampleSheet, value);
              
        }


        

        bool _UseSampleSheet = false;
        public bool UseSampleSheet
        {
            get => _UseSampleSheet;
            set
            {
                if (SetProperty(ref _UseSampleSheet, value))
                {
                    if (_UseSampleSheet) //enable
                    {
                        
                       SampleSheet = _previousSampleSheet;
                            
                    }
                    else //disable
                    {
                        if (SampleSheet != _NoSampleSheet)
                        {
                            _previousSampleSheet = SampleSheet;
                            SampleSheet = _NoSampleSheet;
                            
                        }
                        else
                        {
                            OnPropertyChanged(nameof(SampleSheet));
                        }
                    }
                }
            }
        }

        bool _UseCustomPrimers;
        public bool UseCustomPrimers
        {
            get => _UseCustomPrimers;
            set
            {
                SetProperty(ref _UseCustomPrimers, value, nameof(UseCustomPrimers));
            }
        }

        bool _isEnableOLA = true;
        public bool IsEnableOLA
        {
            get => _isEnableOLA;
            set
            {
                SetProperty(ref _isEnableOLA, value);
            }
        }

       
        bool _isEnablePP;
        public bool IsEnablePP
        {
            get => _isEnablePP;
            set
            {
                SetProperty(ref _isEnablePP, value);
            }
        }

        bool _isCG;
        public bool IsCG
        {
            get => _isCG;
            set
            {
                SetProperty(ref _isCG, value);
            }
        }
        public List<TemplateOptions> Templateoptions { get; set; }
        public List<TemplateOptions> IndexTemplateoptions { get; set; }
        TemplateOptions _SelectedTemplate;
        public TemplateOptions SelectedTemplate
        {
            get => _SelectedTemplate;
            set
            {
                SetProperty(ref _SelectedTemplate, value);
            }
        }
        TemplateOptions _SelectedIndTemplate;
        public TemplateOptions SelectedIndTemplate
        {
            get => _SelectedIndTemplate;
            set
            {
                SetProperty(ref _SelectedIndTemplate, value);
            }
        }

        public string CurrentlyLoadedFile { get; set; }

        public override string this[string columnName]
        {
            get
            {
                string error = string.Empty; 
                switch (columnName)
                {
                    case "RunName":
                        if (string.IsNullOrEmpty(RunName))
                        {

                            error =  "Input Your Run Name.";
                        }
                        break;
                    case "Description":
                        if (string.IsNullOrEmpty(Description))
                        {

                            error =  "Input Your Description.";
                        }
                        break;
                    case "Read1Value":
                        if (Read1Value <= 0)
                        {

                            error =  "Must be a positive number.";
                        }
                        break;
                    case "_Index1Value":
                        if (Index1Value <= 0)
                        {

                            error =  "Must be a positive number.";
                        }
                        break;
                    case "_Index2Value":
                        if (Index2Value <= 0)
                        {

                            error =  "Must be a positive number.";
                        }
                        break;
                    
                    case "SampleSheet":
                        if (UseSampleSheet)
                        {
                            if (string.IsNullOrEmpty(SampleSheet))
                            {

                                error = "Input Sample Sheet File Name.";
                            }
                            else if (!File.Exists(SampleSheet))
                            {

                                error = "Sample sheet file doesn't exist.";
                            }
                            else if (string.Compare(SampleSheet, CurrentlyLoadedFile, true) != 0)
                            {
                                error = "Sample sheet file hasn't been loaded.";
                            }
                            else
                            {
                                string errMsg= string.Empty;
                                if (RunSetupInterface?.ValidateSampleSheetData(SampleSheetData, out errMsg) == false)
                                {
                                    if (!string.IsNullOrEmpty(errMsg))
                                    {
                                        error = $"Invalid Sample Sheet: {errMsg}";
                                    }
                                }
                            }

                        }
                        else
                        {
                            error = string.Empty;
                        }
                        break;
                }

                UpdateErrorBits(columnName, !string.IsNullOrEmpty(error));
                return error;
            }
        }
    }
}
