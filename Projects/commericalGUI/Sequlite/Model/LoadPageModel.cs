using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class LoadPageModel : ModelBase
    {
        string _BarcodeId = "";
        public string FCBarcodeId
        {
            get
            {
                return _BarcodeId;
            }
            set
            {
                SetProperty(ref _BarcodeId, value, nameof(FCBarcodeId));
            }
        }

        string _RFID = "";
        public string ReagentRFID
        {
            get
            {
                return _RFID;
            }
            set
            {
                SetProperty(ref _RFID, value, nameof(ReagentRFID));
            }
        }
    }
}
