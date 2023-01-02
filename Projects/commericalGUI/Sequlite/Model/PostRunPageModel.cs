using Sequlite.ALF.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.Model
{
    public class PostRunPageModel : ModelBase
    {
        
        bool _IsWashing;
        public bool IsWashing
        {
            get => _IsWashing;
            set => SetProperty(ref _IsWashing, value);
        }

        bool _IsWashDone;
        public bool IsWashDone
        {
            get => _IsWashDone;
            set => SetProperty(ref _IsWashDone, value);
        }


        public void UpdateWashingStatus(AppRunWashStatus appStatus)
        {
            lock (_commonLock)
            {
                ProgressTypeEnum status = appStatus.WashStatus;
                switch (status)
                {
                    case ProgressTypeEnum.InProgress:
                        IsWashing = true;
                        IsWashDone = false;
                        break;

                    case ProgressTypeEnum.Aborted:
                    case ProgressTypeEnum.Failed:
                        IsWashing = false;
                        IsWashDone = false;
                        break;
                    case ProgressTypeEnum.Completed:
                        IsWashing = false;
                        IsWashDone = true;
                        break;
                }
            }
        }


    }
}
