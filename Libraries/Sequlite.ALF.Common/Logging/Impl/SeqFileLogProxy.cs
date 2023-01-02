using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Sequlite.ALF.Common
{
    internal class SeqFileLogProxy : ISeqFileLog
    {
        private static Object s_lockLoggerCreation = new Object();
        private static Object s_lockInstanceCreation = new Object();
        private static Dictionary<KeyValuePair<string, string>, SeqFileLogProxy> s_fileLogProxyList = new Dictionary<KeyValuePair<string, string>, SeqFileLogProxy>();
        private static Dictionary<string, SeqFileLogger> s_fileLogList = new Dictionary<string, SeqFileLogger>();
        private static SeqLogFlagEnum GlobalFilterOutFlags { get; set; } = SeqLogFlagEnum.NONE;
        private SeqFileLogger Logger { get; set; }
        private string SubsystemName { get; set; }

        public static void AddFilterOutFlags(SeqLogFlagEnum flags)
        {
            if (flags != SeqLogFlagEnum.NONE)
            {
                GlobalFilterOutFlags |= flags;
            }
        }

        /// <summary>
        /// Checks if a flag should be passed through to the log file
        /// </summary>
        /// <param name="flag">A bitmask specifying the subtype of the log message</param>
        /// <returns>True if flag is NONE or is not contained in GlobalFilterOutFlags </returns>
        static bool IsNotFilteredOut(SeqLogFlagEnum flag)
        {
            return (flag == SeqLogFlagEnum.NONE || (flag & GlobalFilterOutFlags) != flag);
        }

        public int ErrorCount { get => Logger.ErrorCount; }
        public int WarningCount { get => Logger.WarningCount; }

        public string LogFilePath
        {
            get
            {
                return Path.GetDirectoryName(Logger.LogFileFullName);
            }
        }

        public string LogFileName
        {
            get
            {
                return Path.GetFileName(Logger.LogFileFullName);
            }
        }

        public string LogFileFullName
        {
            get
            {
                return Logger.LogFileFullName;
            }
        }

        public SeqLogFlagEnum FilterOutFlags
        {
            get
            {
                return Logger.FilterOutFlags;
            }
            set
            {
                Logger.FilterOutFlags = value;
            }
        }

        public ISeqLogMessageHeaderParser LogMessageHeaderParser
        {
            get
            { return Logger.LogMessageHeaderParser; }
        }
        public static SeqFileLogProxy GetInstance(string appName, string subsystemName)
        {
            SeqFileLogProxy logProxy = null;
            lock (s_lockInstanceCreation)
            {
                KeyValuePair<string, string> key = new KeyValuePair<string, string>(appName, subsystemName);
                if (s_fileLogProxyList.ContainsKey(key))
                {
                    logProxy = s_fileLogProxyList[key];
                }
                else
                {
                    logProxy = new SeqFileLogProxy(appName, subsystemName);
                    s_fileLogProxyList.Add(key, logProxy);
                }
            }
            return logProxy;
        }

        private SeqFileLogProxy(string appName, string subsystemName)
        {
            SubsystemName = subsystemName;
            lock (s_lockLoggerCreation)
            {
                if (s_fileLogList.ContainsKey(appName))
                {
                    Logger = s_fileLogList[appName];
                }
                else
                {
                    Logger = SeqFileLogger.CreateSeqFileLogger(appName);
                    s_fileLogList.Add(appName, Logger);
                }
            }
        }

        event MessageLoggedEvent ISeqLog.OnMessageLogged
        {
            add
            {
                Logger.OnMessageLogged += value;
            }
            remove
            {
                Logger.OnMessageLogged -= value;
            }
        }

        public void Log(string message, SeqLogFlagEnum flags = SeqLogFlagEnum.NONE)
        {
            if (IsNotFilteredOut(flags)) { Logger.AddLogMesage(message, SeqLogMessageTypeEnum.INFO, flags, SubsystemName); }
        }

        public void LogWarning(string message, SeqLogFlagEnum flags = SeqLogFlagEnum.NONE)
        {
            if (IsNotFilteredOut(flags)) { Logger.AddLogMesage(message, SeqLogMessageTypeEnum.WARNING, flags, SubsystemName); }

        }

        public void LogError(string message, SeqLogFlagEnum flags = SeqLogFlagEnum.NONE)
        {
            if (IsNotFilteredOut(flags)) { Logger.AddLogMesage(message, SeqLogMessageTypeEnum.ERROR, flags, SubsystemName); }
        }

        public void LogDebug(string message, SeqLogFlagEnum flags = SeqLogFlagEnum.DEBUG)
        {
            if (IsNotFilteredOut(flags)) { Logger.AddLogMesage(message, SeqLogMessageTypeEnum.INFO, flags, SubsystemName); }
        }
    }
}
