using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public static class SeqLogExtensions
    {
        public static void LogMessage(this ISeqLog Logger, 
            string msg, SeqLogMessageTypeEnum msgType = SeqLogMessageTypeEnum.INFO)
        {
            string msgOut = ": [" + CurrentThreadName + "]: " + msg;
            if (msgType == SeqLogMessageTypeEnum.ERROR)
            {
                Logger.LogError(msgOut);
            }
            else if (msgType == SeqLogMessageTypeEnum.WARNING)
            {
                Logger.LogWarning(msgOut);
            }
            else
            {
                Logger.Log(msgOut);
            }
        }

        public static string ThreadName(this ISeqLog Logger) => CurrentThreadName;
        private static string CurrentThreadName
        {
            get
            {
                string name = Thread.CurrentThread.Name;
                if (string.IsNullOrEmpty(name))
                {
                    name = "ID" + Thread.CurrentThread.ManagedThreadId;
                }
                return name;
            }
        }
    }
}
