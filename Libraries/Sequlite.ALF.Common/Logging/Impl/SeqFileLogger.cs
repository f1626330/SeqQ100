using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Sequlite.ALF.Common
{
    internal class SeqFileLogger
    {
        public event MessageLoggedEvent OnMessageLogged;
        public string LogFileFullName { get; private set; }
        public SeqLogFlagEnum FilterOutFlags { get; set; }

        public static string LogTimeStamp => DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture);
        private static Dictionary<SeqLogMessageTypeEnum, string> MessageTypeNames
            = new Dictionary<SeqLogMessageTypeEnum, string>{
                {SeqLogMessageTypeEnum.INFO, "INFO" },
                {SeqLogMessageTypeEnum.WARNING, "WARNING" },
                {SeqLogMessageTypeEnum.ERROR, "ERROR" }
            };

        private static string GetMessageTypeString(SeqLogMessageTypeEnum messageType) => MessageTypeNames[messageType];
        //time stamp|flags|SsubsystemName|MessageType| message
        private static string LogMessageFormat => "{0}|{1:X4}|{2}|{3}|{4}|{5}";
        private static string ClassName = typeof(SeqFileLogger).Name;
        // private Queue<string> MessageQ { get; set; }
        private AutoSetQueue<string> MessageQ { get; set; }
        private bool RunLogProcess { get; set; }
        private object QLock = new object();
        //private static string _AppName;
        public string AppName { get; private set; }
        private int _LaneLogged;

        private int _warningCount;
        private int _errorCount;
        public int WarningCount { get => _warningCount;} //< get the total number of warnings logged since the initialization of this logger
        public int ErrorCount { get => _errorCount;}  //< get the total number of errors logged since the initialization of this logger
        public ISeqLogMessageHeaderParser LogMessageHeaderParser { get; private set; }
       
        private SeqFileLogger()
        {
            LogMessageHeaderParser = new SeqLogMessageHeaderParser();
        }

        ~SeqFileLogger()
        {
            string message = $"Logging session has finished. Total warnings:{_warningCount}. Total errors:{_errorCount}";
            string logMsg = CreateLogMessage(message, SeqLogMessageTypeEnum.INFO, SeqLogFlagEnum.NONE, ClassName);
            WriteLogMessages(new string[] { logMsg });
        }

        public static SeqFileLogger CreateSeqFileLogger(string appName)
        {
            SeqFileLogger logger = new SeqFileLogger();
            logger.InitializeLogger(appName);
            //AppName = appName;
            return logger;
        }
        
        // todo: rethink encapsulation here; this method should only be called from SeqFileLogProxy.
        public void AddLogMesage(string message, SeqLogMessageTypeEnum messageType, SeqLogFlagEnum logFlags, string subsystemName)
        {
            if (Initialized)
            {
                // flags are filtered out in SeqFileLogProxy.IsNotFilteredOut
                //if (FilterOutFlgas == SeqLogFlagEnum.NONE || FilterOutFlgas != logFlags)
                //if (FilterOutFlags == SeqLogFlagEnum.NONE || (logFlags & FilterOutFlags) != logFlags)
                //{
                    string logMsg = CreateLogMessage(message, messageType, logFlags, subsystemName);
                    MessageQ.Enqueue(logMsg);
                //}

                if(messageType == SeqLogMessageTypeEnum.WARNING)
                {
                    _warningCount++;
                }
                if(messageType == SeqLogMessageTypeEnum.ERROR)
                {
                    _errorCount++;
                }    
            }
        }

        public bool InitializeLogger(string appName)
        {
            AppName = appName;
            FilterOutFlags = SeqLogFlagEnum.NONE;
            _LaneLogged = 0;
            _errorCount = 0;
            _warningCount = 0;
            LogFileFullName = String.Empty;
            RunLogProcess = false;
            bool ret = false;
            Initialized = false;
            Assembly assem = Assembly.GetEntryAssembly();
            AssemblyName assemName = assem.GetName();
            string logFileFullPathName = GetLogFile(appName);
            if (!string.IsNullOrEmpty(logFileFullPathName))
            {
               LogFileFullName = logFileFullPathName;
               Version version = assemName.Version;
               string localPath = new Uri(assemName.CodeBase).LocalPath;
               string productVersion = string.Format("{0}.{1}.{2}.{3}",
                                                   version.Major,
                                                   version.Minor,
                                                   version.Build,
                                                   version.Revision.ToString("D4"));
                string message = string.Format(CultureInfo.InvariantCulture, $"{localPath} started, version: {productVersion}");
               string logMsg = CreateLogMessage(message, SeqLogMessageTypeEnum.INFO, SeqLogFlagEnum.NONE, ClassName);
               WriteLogMessages(new string[] { logMsg });
               //MessageQ = new Queue<string>();
               MessageQ = new AutoSetQueue<string>();
               try
               {  
                    Thread receiver = new Thread(new ThreadStart(this.ProcessMessageQ));
                    receiver.Name = ClassName + "-LogProcess";
                    receiver.IsBackground = true;
                    receiver.Start();
                    ret = true;
                }
                catch (Exception ex)
                {
                    LogInternalMessage("Exception in start a log message processing thread", SeqLogMessageTypeEnum.ERROR, ex);
                }
            }
            Initialized = ret;
            return ret;
        }

        private bool Initialized { get; set; }
        private string GetLogDir()
        {
            string logDir = string.Empty;
            try
            {
                string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                logDir = Path.Combine(commonAppData, "Sequlite\\Log\\SysLog"); //to do read it from config file
                if (!Directory.Exists(logDir))
                {
                    DirectoryInfo di = Directory.CreateDirectory(logDir);
                    if (Directory.Exists(logDir))
                    {
                        logDir = di.FullName;
                    }
                    else
                    {
                        logDir = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                LogInternalMessage("Failed to create file log directory at: " + logDir, SeqLogMessageTypeEnum.ERROR, ex);
            }
            return logDir;
        }

        private string GetLogFile(string appName)
        {
            string path = string.Empty;
            string logDir = GetLogDir();
            if (string.IsNullOrEmpty(logDir))
            {
                return path;
            }

            string logFilePrefix = appName + "-";
            ulong index = 0;
            ulong tempIndex;
            
            string fileName;
            foreach (var it in Directory.EnumerateFiles(logDir, "*.log", SearchOption.TopDirectoryOnly))
            {
                fileName = Path.GetFileNameWithoutExtension(it);
                if (fileName.StartsWith(logFilePrefix))
                {
                    if (UInt64.TryParse(fileName.Substring(logFilePrefix.Length), out tempIndex))
                    {
                        if (index < tempIndex)
                        {
                            index = tempIndex;
                        }
                    }
                }
            }
            index++;
            path = logFilePrefix + index + ".log";
            path = Path.Combine(logDir, path);
            return path;
        }

        private static void LogInternalMessage(string message, SeqLogMessageTypeEnum messageType = SeqLogMessageTypeEnum.ERROR, Exception ex = null)
        {
            if (Console.Out != null)
            {
                string logMsg = CreateLogMessage(message, messageType, SeqLogFlagEnum.NONE, ClassName);
                if (ex != null)
                {
                    logMsg += " | Exception " + ex.ToString();
                }
                Console.Out.WriteLine(logMsg);
            }
        }

        private static string CreateLogMessage(string message, SeqLogMessageTypeEnum messageType, SeqLogFlagEnum logFlags, string subsystemName)
        {
            string threadName = string.IsNullOrEmpty(Thread.CurrentThread.Name) ? Thread.CurrentThread.ManagedThreadId.ToString() : Thread.CurrentThread.Name;
            string logMsg = string.Format(LogMessageFormat, SeqFileLogger.LogTimeStamp, (int)logFlags, subsystemName, SeqFileLogger.GetMessageTypeString(messageType), threadName, message);
            return logMsg;
        }

        private void ProcessMessageQ()
        {
            RunLogProcess = true;
            //int CHECKINTERVAL = 500; // ms

            while (RunLogProcess)
            {
                string[] messages = MessageQ.WaitForNextItems();
                if (messages != null && messages.Length > 0)
                {
                    WriteLogMessages(messages);
                    if (OnMessageLogged != null)
                    {
                        OnMessageLogged = (MessageLoggedEvent)EventRaiser.RaiseEventAsync(OnMessageLogged, new object[] { messages });
                    }
                }
                //try
                //{
                //    Thread.Sleep(CHECKINTERVAL);
                //    if (MessageQ.Count > 0)
                //    {
                //        Queue<string> tmp;
                //        lock (QLock)
                //        {
                //            tmp = MessageQ;
                //            MessageQ = new Queue<string>();
                //        }

                //        string[] messages = tmp.ToArray();
                //        if (messages != null && messages.Length > 0)
                //        {
                //            WriteLogMessages(messages);
                //        }
                //    }
                //}
                //catch (Exception exp)
                //{
                //    LogInternalMessage("Exception in processing log messages thread: ", SeqLogMessageTypeEnum.ERROR, exp);
                //} 
            }
            LogInternalMessage("Log message processing thread exited.", SeqLogMessageTypeEnum.INFO);
        }

        private void WriteLogMessages(string[] logMessages)
        {
            try
            {
                using (StreamWriter stream = new StreamWriter(LogFileFullName, true))
                {
                    foreach (var logMsg in logMessages)
                    {
                        stream.WriteLine(logMsg);
                        _LaneLogged++;
                    }
                    stream.Close();
                }
                if(_LaneLogged >= 1000000) //create new log file if size too large
                {
                    LogFileFullName = GetLogFile(AppName);
                    _LaneLogged = 0;
                }
            }
            catch (Exception ex)
            {
                LogInternalMessage("Exception in writing log messages to: " + LogFileFullName, SeqLogMessageTypeEnum.ERROR, ex);
            }
        }
    }
}
