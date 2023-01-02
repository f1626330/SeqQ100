using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public class SummaryDataItem 
    {
        public SummaryDataItem()
        {
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }

    

    public class RunSummaryViewModel : ViewModelBase
    {

        string _Summary = "Summary";
        public string Summary { get => _Summary; set => SetProperty(ref _Summary, value); }
        public RunSummaryViewModel()
        {
            //temp  test
            //ObservableCollection<SummaryDataItem> lst = new ObservableCollection<SummaryDataItem>()
            //{
            //    new SummaryDataItem() { Name = "Date Completed",    Value = "2020 - 11 - 09" },
            //    new SummaryDataItem() { Name = "Run Name",          Value ="Test Run" },
            //    new SummaryDataItem() { Name = "Run Status",        Value ="Complete" },
            //    new SummaryDataItem() { Name = "Date Started",     Value ="2020 - 11 - 30 05:30" },
            //    new SummaryDataItem() { Name = "Date Completed",    Value ="2020 - 12 - 01 17:25"},
            //    new SummaryDataItem() { Name = "Duration",         Value = "23 hrs, 5 mins"},
            //    new SummaryDataItem() { Name = "Description",      Value = "Test run format for demo unit"},
            //    new SummaryDataItem() { Name = "User",              Value = "User2020"},
            //    new SummaryDataItem() { Name = "Instrument",       Value = "SeqQ100 Analyzer"},
            //    new SummaryDataItem() { Name = "Instrument ID",     Value = "ALF2.3"},
            //    new SummaryDataItem() { Name = "Sample ID",         Value = "XXXXXXXXX"},
            //    new SummaryDataItem() { Name = "Flow Cell ID",      Value = "XXXXXXXXX"},
            //    new SummaryDataItem() { Name = "Reagent ID",       Value = "XXXXXXXXX"},
            //    new SummaryDataItem() { Name = "Cycles",            Value = "50 | 0 | 0 | 0"},
            //    new SummaryDataItem() { Name = "Lanes",             Value = "1"},
            //    new SummaryDataItem() { Name = "Rows per Lane",     Value = "4"},
            //    new SummaryDataItem() { Name = "Columns per Lane",  Value = "40"},
            //    new SummaryDataItem() { Name = "Total Yield",       Value = "40.23 Gb"},
            //    new SummaryDataItem() { Name = "File Size",         Value = "450.72 Gb"},
            //};
            //SummaryData = lst;
        }

        public RunSummaryViewModel(List<SummaryDataItem> list)
        {
            SummaryData = new ObservableCollection<SummaryDataItem>(list);
        }
        ObservableCollection<SummaryDataItem> _SummaryData;
        public ObservableCollection<SummaryDataItem> SummaryData { get => _SummaryData; set => SetProperty(ref _SummaryData, value); }

       
    }
}
