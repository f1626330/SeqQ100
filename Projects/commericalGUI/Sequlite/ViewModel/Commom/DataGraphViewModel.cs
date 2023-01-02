using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    
    public class DataGraphViewModel : ViewModelBase
    {
        DataByCycleViewModel _DataByCycleVM;
        public DataByCycleViewModel DataByCycleVM { get => _DataByCycleVM; set => SetProperty(ref _DataByCycleVM, value); }

        DataByTileViewModel _DataByTileVM;
        public DataByTileViewModel DataByTileVM { get => _DataByTileVM; set => SetProperty(ref _DataByTileVM, value); }

        DataInTableViewModel _DataInTableVM;
        public DataInTableViewModel DataInTableVM { get => _DataInTableVM; set => SetProperty(ref _DataInTableVM, value); }
        public SequenceDataTypeEnum SequenceDataType  { get;}
        string _SequenceDataTypeName;
        public string SequenceDataTypeName { get => _SequenceDataTypeName; set => SetProperty(ref _SequenceDataTypeName, value); }

        public ISequenceDataFeeder SequenceOLADataProcess { get; set; }
        public DataGraphViewModel(SequenceDataTypeEnum sequenceDataType)
        {
            SequenceDataType = sequenceDataType;
            SequenceDataTypeName = SequenceDataType.GetDisplayAttributesFrom().Name;
        }
    }
}
