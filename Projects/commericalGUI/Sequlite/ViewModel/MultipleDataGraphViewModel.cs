using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public class MultipleDataGraphViewModel : ViewModelBase
    {

        ISeqLog Logger { get; }
        SequenceStatusModel SequenceStatus { get; }
        //ISequence SequenceApp { get; }
        SequenceInfo SequenceInformation { get;  set; }

        ObservableCollection<DataGraphViewModel> _OLADataGraphs;
        public ObservableCollection<DataGraphViewModel> OLADataGraphs { get => _OLADataGraphs; set => SetProperty(ref _OLADataGraphs, value); }
        public event EventHandler<EventArgs> OnDataProcessStopped;
        int _SelectedOLADataGraph = 0;

       

        public int SelectedOLADataGraph { get => _SelectedOLADataGraph; set => SetProperty(ref _SelectedOLADataGraph, value); }

        public MultipleDataGraphViewModel(ISeqLog logger, SequenceStatusModel sequenceStatus, SequenceInfo sequenceInformation)
        {
            Logger = logger;
            SequenceStatus = sequenceStatus;
            // SequenceApp = sequenceApp;
            SequenceInformation = sequenceInformation;// SequenceApp.SequenceInformation;
            OLADataGraphs = new ObservableCollection<DataGraphViewModel>();
        }

        DataGraphViewModel DataGraphVM(SequenceDataTypeEnum sequenceDataType)
        {
            return OLADataGraphs.Where(x => x.SequenceDataType == sequenceDataType).FirstOrDefault();
        }

        bool HasDataGraphVM(SequenceDataTypeEnum sequenceDataType)
        {
            return DataGraphVM(sequenceDataType) != default(DataGraphViewModel);
        }

        public void CleanAll()
        {
            foreach (var it in OLADataGraphs)
            {
                it.SequenceOLADataProcess?.ClearQ();
                it.DataByCycleVM?.Clear();
                it.DataByTileVM?.Clear();
                it.DataInTableVM?.Clear();
            }
            OLADataGraphs?.Clear();
            SelectedOLADataGraph = -1;
        }

        DataGraphViewModel CreateGraphView(SequenceDataTypeEnum sequenceDataType, ISequenceDataFeeder sequenceDataFeeder )
        {

            ISequenceDataFeeder sequenceOLADataProcess;
            if (sequenceDataFeeder != null)
            {
                sequenceOLADataProcess = sequenceDataFeeder;
            }
            else
            {
                sequenceOLADataProcess = new SequenceOLADataProcess(Logger, sequenceDataType);
               
            }
            DataByCycleViewModel dataByCycleVM = new DataByCycleViewModel(Logger, sequenceOLADataProcess);
            DataInTableViewModel dataInTableVM = new DataInTableViewModel(Logger, sequenceOLADataProcess);
            DataByTileViewModel dataByTileVM = new DataByTileViewModel(Logger, sequenceOLADataProcess);
            
            DataGraphViewModel dataGrphMV = new DataGraphViewModel(sequenceDataType)
            {
                DataByCycleVM = dataByCycleVM,
                DataByTileVM = dataByTileVM,
                DataInTableVM = dataInTableVM,
                SequenceOLADataProcess = sequenceOLADataProcess,
            };
           
            dataByCycleVM.UsingDynamicRangeOnLineData = true;
            dataByTileVM.UsingDynamicRange = true;
            sequenceOLADataProcess.OLADataProcessed += OnOLADataProcessed;
            sequenceOLADataProcess.StartOLADataProcessQueue();
            //SequenceStatus.SequenceDataFeeder = sequenceOLADataProcess;
            
            dataByTileVM?.InitCycles();
            return dataGrphMV;
        }

        public void OnOLAResultsUpdated(ISequenceDataFeeder sequenceOLADataProcess, SequenceOLADataInfo seqOLAData)
        {
            //SequenceOLADataInfo seqOLAData = SequenceApp.SequenceOLAData;
            sequenceOLADataProcess.NewOLAData(seqOLAData);
        }

        void OnOLADataProcessed(object sender, EventArgs e)
        {
            SequenceOLADataProcess sequenceOLADataProces = sender as SequenceOLADataProcess;
            FillOLAData(sequenceOLADataProces.SequenceDataType, sequenceOLADataProces.GetCurrentMaxCycle(), SequenceInformation.Cycles);// _SequenceOLADataProcess);
        }
        public void FillOLAData(SequenceDataTypeEnum sequenceDataType, int currentMaxCycle, int totalCycles) //SequenceOLADataProcess sequenceOLADataProces)
        {
            List<SequenceDataTableItems> tableItems = null;
            Task.Run(() =>
            {
            var dVM = DataGraphVM(sequenceDataType);// sequenceOLADataProces.SequenceDataType);
                DataByCycleViewModel dataByCycleVM = dVM?.DataByCycleVM;
                if (dataByCycleVM != null)
                {
                    dataByCycleVM.WaitForLastLineDataFilled();
                    dataByCycleVM.FillLineData(true);
                }
                tableItems = dVM?.DataInTableVM?.FillDataTable(true);
                if (tableItems != null && tableItems.Count > 0)
                {
                    dVM.DataInTableVM.TotalYieldString = tableItems[tableItems.Count - 1].YieldString; 
                    SequenceStatus.TotalYieldString = tableItems[tableItems.Count - 1].YieldString;
                }
            }).ContinueWith((o) =>
            {
                var dVM = DataGraphVM(sequenceDataType); //sequenceOLADataProces.SequenceDataType);
                DataByCycleViewModel dataByCycleVM = dVM?.DataByCycleVM;
                dataByCycleVM?.SetLineDataFilled();
                Dispatch(() =>
                {
                    //update line graphs 
                    dataByCycleVM?.UpdateLineDataDisplayOnSelectedKey();
                    //update table
                    dVM?.DataInTableVM?.UpdateDataTable(tableItems);
                //heat map update on cycle
                dVM?.DataByTileVM?.UpdateHeatMapOnCycles(currentMaxCycle, totalCycles);// sequenceOLADataProces.GetCurrentMaxCycle(), SequenceInformation.Cycles);
                });
            });
        }

         void InitDataVisualization(ISequenceDataFeeder sequenceDataFeeder, SequenceInfo sequenceInformation)
        {
            Logger.Log("Start InitDataVisualization");
            SequenceInformation = sequenceInformation;
            var dVM = DataGraphVM(sequenceDataFeeder.SequenceDataType);
            DataByCycleViewModel dataByCycleVM = dVM?.DataByCycleVM;
            DataByTileViewModel dataByTileVM = dVM?.DataByTileVM;
            dataByCycleVM.BuildLines();
            dataByCycleVM.SetTotalCycles(sequenceInformation.Cycles);
            int[] lanes = sequenceInformation.Lanes;
            dataByCycleVM.InitLanes(lanes);
            dataByCycleVM.MetricsDataItem2 = MetricsDataEnum.Intensity;
            dataByTileVM.BuildHeatMaps(sequenceInformation.Rows, sequenceInformation.Column,
                sequenceInformation.Lanes.Length);
            dataByTileVM.MetricsDataItem = MetricsDataEnum.Intensity;
            Logger.Log("End InitDataVisualization");
        }

        public bool AddDataGraph(SequenceDataTypeEnum sequenceDataType, SequenceInfo sequenceInformatio, ISequenceDataFeeder sequenceDataFeeder )
        {
            if (!HasDataGraphVM(sequenceDataType))
            {
                DataGraphViewModel dataGrphMV = CreateGraphView(sequenceDataType, sequenceDataFeeder);
                OLADataGraphs.Add(dataGrphMV);
                SelectedOLADataGraph = OLADataGraphs.Count - 1;

                InitDataVisualization(sequenceDataFeeder, sequenceInformatio);// SequenceStatus.SequenceDataFeeder, sequenceInformatio);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void StopOLADataProcessQueue()
        {
            // SequenceStatus?.SequenceDataFeeder?.StopOLADataProcessQueue();
            foreach (var it in OLADataGraphs)
            {
                it.SequenceOLADataProcess?.StopOLADataProcessQueue();
            }
            OnDataProcessStopped?.Invoke(this, new EventArgs());
        }

        public bool HasItemInQ()
        {
            //SequenceStatus?.SequenceDataFeeder?.HasItemInQ();
            bool bRet = false;
            foreach (var it in OLADataGraphs)
            {
                if (it.SequenceOLADataProcess?.HasItemInQ() == true)
                {
                    bRet = true;
                    break;
                }
            }
            return bRet;
        }

    }
}
