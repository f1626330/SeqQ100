using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface IAppMessage
    {
        IObservable<AppMessage> ObservableAppMessage { get; }
        void UpdateAppMessage(object msg, AppMessageTypeEnum msgType = AppMessageTypeEnum.Normal, bool bLog = true);
        void SendMessageToApp(object msg, AppMessageTypeEnum msgType = AppMessageTypeEnum.Normal);

    }
}
