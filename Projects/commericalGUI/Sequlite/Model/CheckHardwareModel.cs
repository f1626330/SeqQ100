using Sequlite.ALF.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class CheckHardwarePageModel : ModelBase
    {
        public HardwareCheckResults HardwareCheckData  { get;set;}
        public string DiskSpaceDispaly { get; set; }
    }
}
