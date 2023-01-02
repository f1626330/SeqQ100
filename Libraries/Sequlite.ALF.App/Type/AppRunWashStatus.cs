using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public class AppRunWashStatus : AppStatus
    {
        public string Message { get; set; }
        public ProgressTypeEnum WashStatus { get; set; }
        public AppRunWashStatus()
        {
            AppStatusType = AppStatusTypeEnum.AppRunWashStatus;
            WashStatus = ProgressTypeEnum.None;
        }
    }
}
