using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class DataInfoFileModel : ModelBase
    {
        string _DataInfoFileName;
        public string DataInfoFileName { get => _DataInfoFileName; set => SetProperty(ref _DataInfoFileName, value); }
        public void ValidateDataInfoFileName()
        {
            OnPropertyChanged(nameof(DataInfoFileName));
        }

        public  override string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {
                    case "DataInfoFileName":
                        {
                            if (string.IsNullOrEmpty(DataInfoFileName))
                            {

                                error = "Input Data Info File Name.";
                            }
                            else if (!File.Exists(DataInfoFileName))
                            {

                                error = "Data Info file doesn't exist.";
                            }
                        }
                        break;
                }

                UpdateErrorBits(columnName, !string.IsNullOrEmpty(error));
                return error;
            }
        }
    }
}
