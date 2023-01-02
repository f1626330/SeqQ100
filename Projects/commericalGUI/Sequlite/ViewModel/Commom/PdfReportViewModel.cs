using PdfSharp.Pdf;
using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sequlite.UI.ViewModel
{
    public class PdfReportViewModel : ViewModelBase, IUserDialogBoxViewModel
    {
        public PdfReportViewModel(ISeqLog logger, SequenceStatusModel seqInfo,
            bool isModal = true, bool autoClose = false)
        {
            IsModal = isModal;
            IsAutoClose = autoClose;
            WinWidth = 610;
            WinHeight = 600;
            //DataFeeder = dataFeeder;
            SequenceInformation = seqInfo.SequenceInformation;
            SequenceStatus = seqInfo;
            Logger = logger;
            Status = $"Export to: {DataOutputPath}";
            //List<SummaryDataItem> lst = GetnerateReportItems(seqInfo);
            //RunSummaryViewModel vm = new RunSummaryViewModel(lst);
            //CurrentPage = vm;
            List<SequenceDataTypeInfo> seqDataTypeList = SequenceDataTypeInfo.GetListSequenceDataTypeInfos(seqInfo.SequenceInformation);
            if (seqDataTypeList.Count > 0)
            {
                foreach (var it in seqDataTypeList)
                {
                    _ExportIndexList.Add(it.SequenceDataType, -2);
                }
                CurrentDataType = seqDataTypeList[0].SequenceDataType;
                _CurrentDrawIndex = _ExportIndexList[CurrentDataType];
            }
            _pdfHelper = PdfHelper.Instance;
            _doc = _pdfHelper.StartSaving();
        }

        Dictionary<SequenceDataTypeEnum, int> _ExportIndexList = new Dictionary<SequenceDataTypeEnum, int>();
        SequenceDataTypeEnum CurrentDataType {get;set;}

        public static List<SummaryDataItem> GenerateReportItems(SequenceStatusModel seqInfo, SequenceDataTypeEnum sequenceDataType)
        {
            SequenceInfo sequenceInformation = seqInfo.SequenceInformation;
            string completeStatus = "";
            switch (seqInfo.OLAMessageType)
            {
                case ProgressTypeEnum.Completed:
                    completeStatus = "Completed";
                    break;
                case ProgressTypeEnum.Failed:
                    completeStatus = "Failed";
                    break;
                case ProgressTypeEnum.Aborted:
                    completeStatus = "Aborted";
                    break;
                default:
                    completeStatus = "Unknown";
                    break;
            }
            //get file size:

            string fileSize = "N/A";
            string dirName = sequenceInformation.WorkingDir;// Path.Combine(SequenceInformation.WorkingDir, "fastq");
            if (Directory.Exists(dirName))
            {
                double size = 0;
                DirectoryInfo dir = new DirectoryInfo(Path.Combine(sequenceInformation.WorkingDir, "fastq"));
                if (dir.Exists)
                {
                    size = dir.GetFiles("*.fastq", SearchOption.AllDirectories).Sum(fi => fi.Length);
                }
                
                long unitKb = 1024;
                long unitMb = unitKb * unitKb;
                long unitGb = unitMb * unitKb;
                long unitTb = unitGb * unitKb;
                string unitName;
                if (size < unitKb)
                {
                    if (size > 0)
                    {
                        size = 1;
                    }
                    unitName = "KB";
                }
                else if (size >= unitKb && size < unitMb)
                {
                    size /= unitKb;
                    unitName = "KB";
                }
                else if (size >= unitMb && size < unitGb)
                {
                    size /= unitMb;
                    unitName = "MB";
                }
                else if (size >= unitGb && size < unitTb)
                {
                    size /= unitGb;
                    unitName = "GB";
                }
                else
                {
                    size /= unitTb;
                    unitName = "TB";
                }
                fileSize = $"{size:F2} {unitName}";
            }

            //a list for run summary table
            List<SummaryDataItem> lst = new List<SummaryDataItem>()
            {
                //new SummaryDataItem() { Name = "Data Type",    Value = sequenceDataType.Description() },
                //new SummaryDataItem() { Name = "Date Completed",    Value = seqInfo.EndDateTime.ToString("yyyy-MM-dd") },
                new SummaryDataItem() { Name = "Run Name",          Value =seqInfo.RunName },
                new SummaryDataItem() { Name = "Run Status",        Value =completeStatus},
                new SummaryDataItem() { Name = "Date Started",     Value =seqInfo.StartDateTime.ToString("yyyy-MM-dd hh:mm:ss" )},
                new SummaryDataItem() { Name = "Date Completed",    Value =seqInfo.EndDateTime.ToString("yyyy-MM-dd hh:mm:ss" )},
                new SummaryDataItem() { Name = "Duration",         Value = (seqInfo.EndDateTime - seqInfo.StartDateTime).ToString(@"d\ \d\a\y\s\ hh\ \h\r\s\,\ mm\ \m\i\n\s\,\ ss\ \s\e\c\s") },
                new SummaryDataItem() { Name = "Description", Value = seqInfo.RunDescription },
                new SummaryDataItem() { Name = "User",              Value = seqInfo.UserName},
                new SummaryDataItem() { Name = "Instrument",       Value = sequenceInformation.Instrument},
                new SummaryDataItem() { Name = "Instrument ID",     Value = sequenceInformation.InstrumentID},
                new SummaryDataItem() { Name = "Sample ID",         Value = sequenceInformation.SampleID},
                new SummaryDataItem() { Name = "Flow Cell ID",      Value = sequenceInformation.FlowCellID},
                new SummaryDataItem() { Name = "Reagent ID",       Value = sequenceInformation.ReagentID},
                new SummaryDataItem() { Name = "Cycles",            Value = $"{sequenceInformation.Cycles } |{sequenceInformation.Index1Cycle } | {sequenceInformation.Index2Cycle } | {sequenceInformation.Read2Cycle }"}, //"50 | 0 | 0 | 0"},
                new SummaryDataItem() { Name = "Lanes",             Value = $"{sequenceInformation.Lanes.Length }"},
                new SummaryDataItem() { Name = "Rows per Lane",     Value = $"{sequenceInformation.Rows }"},
                new SummaryDataItem() { Name = "Columns per Lane",  Value = $"{sequenceInformation.Column }"},
                new SummaryDataItem() { Name = "Total Yield",       Value = seqInfo.TotalYieldString},
                new SummaryDataItem() { Name = "File Size",         Value = fileSize }, 
            };
            return lst;
        }
        #region IUserDialogBoxViewModel Implementation
        public Action<PdfReportViewModel> OnCloseRequest { get; set; }
        public bool IsModal { get; private set; }
        public virtual void RequestClose()
        {
            if (this.OnCloseRequest != null)
                this.OnCloseRequest(this);
            else
                Close();
        }
        public event EventHandler DialogBoxClosing;
        public void Close()
        {
            if (this.DialogBoxClosing != null)
            {
                this.DialogBoxClosing(this, new EventArgs());
            }
        }
        public bool Contains(IList<IDialogBoxViewModel> collection)
        {
            return collection.Contains(this);
        }
        public void Show(IList<IDialogBoxViewModel> collection)
        {
            collection.Add(this);
        }
        #endregion IUserDialogBoxViewModel Implementation
        ISeqLog Logger { get; }
        string _Title;
        public string Title { get => _Title; set => SetProperty(ref _Title, value); }
        int _WinWidth ;
        public int WinWidth { get => _WinWidth; set => SetProperty(ref _WinWidth, value); }

        int _WinHeight ;
        public int WinHeight { get => _WinHeight; set => SetProperty(ref _WinHeight, value); }

        bool _IsAutoClose;
        public bool IsAutoClose { get => _IsAutoClose; set => SetProperty(ref _IsAutoClose, value); }

        bool _IsExportDone;
        public bool IsExportDone { get => _IsExportDone; set => SetProperty(ref _IsExportDone, value); }


        string _DataOutputPath;
        public string DataOutputPath { get => _DataOutputPath; set => SetProperty(ref _DataOutputPath, value); }
        //string __TempMessage;
        //public string TempMessage { get => __TempMessage; set => SetProperty(ref __TempMessage, value); }
        SequenceInfo SequenceInformation { get; }
        SequenceStatusModel SequenceStatus { get; }
        private Visual _VS;
        PdfDocument _doc;
        PdfHelper _pdfHelper;
        string _TempPath;
        string TempPath //off line temp file for PDF rendering 
        {
            get
            {
                if (string.IsNullOrEmpty(_TempPath))
                {
                    try
                    {
                        _TempPath = Path.GetTempPath();
                    }
                    catch (Exception ex)
                    {
                        _TempPath = Path.GetDirectoryName(DataOutputPath);
                        Logger.LogWarning($"Failed to get Windows temp path: {ex.Message}, use current selected path {_TempPath} instead.");
                    }
                }
                return _TempPath;
            }
        }
        

        string _Status;
        public string Status { get => _Status; set => SetProperty(ref _Status, value); }
        ISequenceDataFeeder DataFeeder { get;  set; }

        ViewModelBase _CurrentPage;
        public ViewModelBase CurrentPage { get => _CurrentPage; set => SetProperty(ref _CurrentPage, value); }

        
        void FillDataTable(DataInTableViewModel vm)
        {
            List<SequenceDataTableItems> tableItems = null;
            Task.Run(() =>
            {
                tableItems = vm?.FillDataTable(false);
                //Task.Delay(5000);
            }).ContinueWith((o) =>
            {
                
                Dispatch(() =>
                {
                  
                    //update table
                    vm?.UpdateDataTable(tableItems);
                     });
            });
        }

        void FillCycleData(DataByCycleViewModel vm)
        {
            //List<SequenceDataTableItems> tableItems = null;
            Task.Run(() =>
            {
                //if (DataByCycleVM != null)
                {
                    //DataByCycleVM.WaitForLastLineDataFilled();
                    vm.FillLineData(false);
                    //Task.Delay(5000);
                }
                //tableItems = DataInTableVM?.FillDataTbale();
            }).ContinueWith((o) =>
            {
                vm?.SetLineDataFilled();
                Dispatch(() =>
                {
                    //update line graphs 
                    vm?.UpdateLineDataDisplayOnSelectedKey();
                    //update table
                   // DataInTableVM?.UpdateDataTable(tableItems);
                    //heat map update on cycle
                    //DataByTileVM?.UpdateHeatMapOnCycles(_SequenceOLADataProcess.GetCurrentMaxCycle(), SequenceApp.SequenceInformation.Cycles);
                });
            });
        }

        //for view to begin rendering
        private ICommand _ContentRenderedCommand = null;
        public ICommand ContentRenderedCommand
        {
            get
            {
                if (_ContentRenderedCommand == null)
                {
                    _ContentRenderedCommand = new RelayCommand(o => OnContentRendered(o));
                }
                return _ContentRenderedCommand;
            }
        }

        //beginning of rendering
        void OnContentRendered(object obj)
        {
            _VS = obj as Visual; //rending object
            OnAfterRendered();
        }
        
        
        int _CurrentDrawIndex = -2;
        DataByCycleViewModel _DataByCycleVM;
        int delayTime = 500;//3500;
        string _DatatypeStr = "";// CurrentDataType.Description();
        
        //rendering loop function
        async void OnAfterRendered()
        {
            try
            {
                List<MetricsDataEnum> sels = DataByCycleViewModel.GetMetricsDataItemListForLineGraph();
                {
                    if (_CurrentDrawIndex == -2) //table
                    {
                        DataFeeder = SequenceStatus.SequenceDataFeeder(CurrentDataType);
                        List<SummaryDataItem> lst;
                        RunSummaryViewModel vm;
                        if (CurrentDataType == SequenceDataTypeEnum.Read1)
                        {
                            lst = GenerateReportItems(SequenceStatus, CurrentDataType);
                             vm = new RunSummaryViewModel(lst);
                            _DatatypeStr = CurrentDataType.Description();
                            vm.Summary = $"Summary for all runs and {_DatatypeStr}";
                        }
                        else
                        {
                            lst = new List<SummaryDataItem>();
                            vm = new RunSummaryViewModel(lst);
                            _DatatypeStr = CurrentDataType.Description();
                            vm.Summary = $"Summary for {_DatatypeStr}";
                        }
                        CurrentPage = vm;
                        _CurrentDrawIndex++;
                        
                    }

                    await Task.Delay(delayTime);
            
                    RenderTargetBitmap bmp = new RenderTargetBitmap(WinWidth + 30, WinHeight + 30, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(_VS);
                   
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));

                    string fileName = Path.Combine(TempPath, $"test{_CurrentDrawIndex}{_DatatypeStr}");
                    using (Stream stm = File.Create($"{fileName}.png")) { encoder.Save(stm); stm.Close(); }
                    _pdfHelper.AddImageToPdf(_doc, $"{fileName}.png", 15, 15, WinWidth, 0, true);
                }

                if (_CurrentDrawIndex == -1) //table
                {
                    //output summary
                    Status = $"Output summary for {_DatatypeStr}";
                    Logger.Log(Status);

                    DataInTableViewModel vm = new DataInTableViewModel(Logger, DataFeeder);

                    FillDataTable(vm);
                    _CurrentDrawIndex++;
                   
                    CurrentPage = vm;
                }
                else if (_CurrentDrawIndex == 0) //line graph
                {
                    //output table
                    Status = $"Output data table for {_DatatypeStr}";
                    Logger.Log(Status);

                    _DataByCycleVM = new DataByCycleViewModel(Logger, DataFeeder);
                    _DataByCycleVM.EnableSelections = false;
                    
                    

                    _DataByCycleVM.BuildLines();
                    _DataByCycleVM.SetTotalCycles(SequenceInformation.Cycles);
                    int[] lanes = SequenceInformation.Lanes;
                    _DataByCycleVM.InitLanes(lanes);
                    _DataByCycleVM.UsingDynamicRangeOnLineData = true;
                    _DataByCycleVM.ChannelItem2 = ChannelDataEnum.All;
                    _DataByCycleVM.SelectedLane = _DataByCycleVM.Lanes[_DataByCycleVM.Lanes.Count - 1];
                    _DataByCycleVM.MetricsDataItem2 = sels[sels.Count - 1];
                    _DataByCycleVM.MetricsDataItem2 = sels[_CurrentDrawIndex];
                    _DataByCycleVM.SurfaceDataItem2 = SurfaceDataEnum.Both;
                    _DataByCycleVM.ShowTitle = true;
                    FillCycleData(_DataByCycleVM);
                    _CurrentDrawIndex++;
                    CurrentPage = _DataByCycleVM;
                }
                else if (_CurrentDrawIndex < sels.Count) //line graph
                {
                    //output line
                    Status = $"Output line graph {_CurrentDrawIndex} for {_DatatypeStr}";
                    Logger.Log(Status);

                    _DataByCycleVM.MetricsDataItem2 = sels[_CurrentDrawIndex];
                    _CurrentDrawIndex++;
                }
                else if (_CurrentDrawIndex == sels.Count)
                {
                    //output last line
                    Status = $"Output the last line graph {_CurrentDrawIndex} for {_DatatypeStr}";
                    Logger.Log(Status);
                    _ExportIndexList.Remove(CurrentDataType);
                    if (_ExportIndexList.Count == 0)
                    {
                        IsExportDone = true;
                        _pdfHelper.EndSaving(_doc, DataOutputPath);

                        Status = $"Finished exporting to: {DataOutputPath}";
                        Logger.Log(Status);
                    }
                    else
                    {
                        CurrentDataType = _ExportIndexList.Keys.First();
                        _CurrentDrawIndex = _ExportIndexList.Values.First();
                    }
                    
                }

                if (!IsExportDone)
                {
                    //post event for the next rendering 
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.ContextIdle, (Action)OnAfterRendered);

                }
               
            }
            catch (Exception ex)
            {
                Status = "Failed to export Pdf";
                Logger.LogError($"{Status}: {ex.Message}");
                IsExportDone = true;
            }
            finally
            {
                if (IsExportDone && IsAutoClose)
                {
                    RequestClose();
                }
            }
        }

        //this function has not usage, could be removed.
        void ViewPdf()
        {
            try
            {
                Process pro = Process.Start(DataOutputPath);
                WinHelper.SetWindowTopMost(pro.MainWindowHandle);
                pro.WaitForExit();
                pro.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to view the Pdf file {ex.Message}");
            }
        }
    }
}
