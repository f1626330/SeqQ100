using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sequlite.UI.ViewModel
{
    public class DataInTableViewModel : ViewModelBase
    {
        ISeqLog Logger { get; }
        ISequenceDataFeeder _DataFeeder;
        public DataInTableViewModel(ISeqLog logger, ISequenceDataFeeder dataFeeder)
        {
            Logger = logger;
            _DataFeeder = dataFeeder;
        }
        public void Clear()
        {
            SequenceDatas = new ObservableCollection<SequenceDataTableItems>();
        }
        public string TotalYieldString { get; set; }
        #region DATA_TABLE
        ObservableCollection<SequenceDataTableItems> _SequenceDatas;
        public ObservableCollection<SequenceDataTableItems> SequenceDatas { get => _SequenceDatas; set => SetProperty(ref _SequenceDatas, value); }
        public void UpdateDataTable(List<SequenceDataTableItems> tableItems)
        {
            if (tableItems != null)
            {
                SequenceDatas = new ObservableCollection<SequenceDataTableItems>(tableItems);
            }
        }
        public List<SequenceDataTableItems> FillDataTable(bool checkCycleFinished) => _DataFeeder?.GetSequenceDataTableItems(checkCycleFinished);
        #endregion
    }
}
