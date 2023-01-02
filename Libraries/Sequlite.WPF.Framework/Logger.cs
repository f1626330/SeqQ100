using Sequlite.ALF.Common;
using System;
using System.Configuration;
using System.IO;
using System.Text;

namespace Sequlite.WPF.Framework
{

    /// <summary>
    /// Log updated delegate.
    /// </summary>
    /// <param name="logEntry"></param>
    public delegate void LogUpdatedDelegate(string logEntry);
    /// <summary>
    /// Logging exception delegate.
    /// </summary>
    /// <param name="ex"></param>
    public delegate void LoggingExceptionDelegate(Exception ex);


    /// <summary>
    /// This class provides a thread safe application logging to a flat file, log event 
    /// listeners, and/or database. 
    /// </summary>
    /// <remarks> 
    /// <para>If no flat-file has been specified for logging (<see cref="OpenFlatFile"/>)
    /// this class can still provide centralized "in-memory" logging via the <see cref="LogUpdated"/>
    /// event.
    /// </para>
    /// <para></para>
    /// <para>Created by Tony Jenniges</para>
    /// </remarks>
    public class Logger : ILogger
    {
        protected object _TextWriterSyncObject = new object();
        protected TextWriter _TextWriter = null;

        protected const string _MSG_FORMAT = "[MS] {0,-10:MM/dd/yyyy} {0,-13:hh:mm:ss.fff tt}  {1}";
        protected const string _EXC_FORMAT = "[EX] {0,-10:MM/dd/yyyy} {0,-13:hh:mm:ss.fff tt}  {1}";
        protected const string _WRN_FORMAT = "[WN] {0,-10:MM/dd/yyyy} {0,-13:hh:mm:ss.fff tt}  {1}";
        protected const string _PARAM_FORMAT = "[PV]                               {0,-20} = {1}";
        protected char[] _HEADER_SPLIT_CHAR = new char[] { '\n' };

        /// <summary>
        /// Notify any subscribers that the log file has been updated. Usually a UI component will 
        /// subscribe to this event.
        /// </summary>
        public event LogUpdatedDelegate LogUpdated;

        /// <summary>
        /// Notify any subscribers that logging has occured an exception. 
        /// </summary>
        public event LoggingExceptionDelegate LoggingException;


        #region public bool SuppressLoggingExceptionEvents
        /// <summary>
        /// Set this property to true to suppress any <see cref="LoggingException"/> events.
        /// </summary>
        public bool SuppressLoggingExceptionEvents
        {
            get;
            set;
        }
        #endregion

        #region internal TextWriter LogTextWriter
        /// <summary>
        /// Used by <see cref="CommandMediator"/> to log LINQ SQL queries.
        /// </summary>
        internal TextWriter LogTextWriter
        {
            get { return _TextWriter; }
        }
        #endregion


        #region Constructors...
        /// <summary>
        /// Default constructor.
        /// </summary>
        public Logger()
        {
        }

        ~Logger()
        {
            //Close();
        }
        #endregion


        #region public void Open(string logFilePath)
        /// <summary>
        /// Open a flat file for writing log output. This method will close
        /// the current log file if there is one open before opening another.
        /// </summary>
        /// <param name="logFilePath">The full path to the log file to be created.</param>
        public void Open(string logFilePath)
        {
            lock (_TextWriterSyncObject)
            {
                if (_TextWriter != null)
                {
                    //
                    // Attempt to close an open log text writer:
                    //
                    try
                    {
                        _TextWriter.Close();
                        _TextWriter = null;
                    }
                    catch { }
                }

                //
                // Verify log directory exists, if not create it:
                //
                FileInfo logFileInfo = new FileInfo(logFilePath);

                if (!logFileInfo.Directory.Exists)
                {
                    logFileInfo.Directory.Create();
                }


                //
                // Limit file to so many days before backup and starting new log:
                //
                if (logFileInfo.Exists)
                {
                    TimeSpan timeSpan = DateTime.Now - logFileInfo.CreationTime;
                    int numDaysToKeep = 7;

                    try
                    {
                        string daysToKeepErrorLog = ConfigurationManager.AppSettings["DaysBeforeOverWritingLog"];

                        if (daysToKeepErrorLog != null && daysToKeepErrorLog.Length >= 1)
                        {
                            numDaysToKeep = int.Parse(daysToKeepErrorLog);
                        }
                    }
                    catch
                    {
                    }

                    if (timeSpan.Days > numDaysToKeep)
                    {
                        try
                        {
                            string oldLogPath = Path.Combine(logFileInfo.Directory.FullName, "OLD.log");

                            if (File.Exists(oldLogPath))
                            {
                                File.Delete(oldLogPath);
                            }

                            File.Move(logFilePath, oldLogPath);
                        }
                        catch { }
                    }
                }

                _TextWriter = new StreamWriter(logFilePath, true);

            }
        }
        #endregion

        #region public void Close()
        /// <summary>
        /// Close the logger.
        /// </summary>
        public void Close()
        {
            lock (_TextWriterSyncObject)
            {
                if (_TextWriter != null)
                {
                    //
                    // Attempt to close an open log text writer:
                    //
                    try
                    {
                        _TextWriter.Close();
                        _TextWriter = null;
                    }
                    catch { }
                }
            }
        }
        #endregion


        #region public void LogAppConfigSettings()
        /// <summary>
        /// This method logs the App.config file's "appSettings" section to the open flat file.
        /// </summary>
        public void LogAppConfigSettings()
        {
            lock (_TextWriterSyncObject)
            {
                StringBuilder logEntryString = new StringBuilder();

                logEntryString.AppendLine("\t\t***************** Application Configuration Settings *****************");

                foreach (string key in ConfigurationManager.AppSettings.AllKeys)
                {
                    logEntryString.AppendLine(string.Format("\t\t\t{0,-25} = {1}", key, ConfigurationManager.AppSettings[key]));
                }

                logEntryString.AppendLine("\t\t**********************************************************************");

                string logEntry = logEntryString.ToString();

                if (_TextWriter != null)
                {
                    _TextWriter.WriteLine(logEntry);
                    _TextWriter.Flush();
                }

                FireLogUpdatedEvent(logEntry);
            }
        }
        #endregion

        #region public void LogEmptyLine()
        /// <summary>
        /// Log an empty line with no date/time stamp.
        /// </summary>
        public void LogEmptyLine()
        {
            lock (_TextWriterSyncObject)
            {
                if (_TextWriter != null)
                {
                    _TextWriter.WriteLine("");
                    _TextWriter.Flush();
                }

                FireLogUpdatedEvent("");
            }
        }
        #endregion

        #region public void LogMessage(string message)
        /// <summary>
        /// Log a 1-line message of the following format:
        ///   [MS] MM/dd/YYYY   hh:mm:ss.fff PM   msg
        /// </summary>
        /// <param name="message">The message to log</param>
        public void LogMessage(string message)
        {
            try
            {
                lock (_TextWriterSyncObject)
                {
                    string logEntry = string.Format(_MSG_FORMAT, DateTime.Now, message);

                    if (_TextWriter != null)
                    {
                        _TextWriter.WriteLine(logEntry);
                        _TextWriter.Flush();
                    }

                    FireLogUpdatedEvent(logEntry);
                }
            }
            catch (Exception ex)
            {
                FireLoggingExceptionEvent(ex);
            }
        }
        #endregion

        #region public void LogParamValuePair(string paramName, string value)
        /// <summary>
        /// Log an indented parameter value pair of the following format:
        /// [PV]    paramName = value
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="value"></param>
        public void LogParamValuePair(string paramName, string value)
        {
            try
            {
                lock (_TextWriterSyncObject)
                {
                    string paramEntry = string.Format(_PARAM_FORMAT, paramName, value);

                    if (_TextWriter != null)
                    {
                        _TextWriter.WriteLine(paramEntry);
                        _TextWriter.Flush();
                    }

                    FireLogUpdatedEvent(paramEntry);
                }
            }
            catch (Exception ex)
            {
                FireLoggingExceptionEvent(ex);
            }
        }
        #endregion


        #region public void LogException(string header, Exception ex)
        /// <summary>
        /// Log an exception.
        /// </summary>
        /// <param name="header">A message describing the current context. Can be null or empty</param>
        /// <param name="ex">The exception that was caught</param>
        public void LogException(string header, Exception ex)
        {
            try
            {
                lock (_TextWriterSyncObject)
                {
                    StringBuilder logEntryString = new StringBuilder();

                    logEntryString.AppendLine("************************** EXCEPTION *********************************************************************************************");
                    logEntryString.AppendLine(string.Format(_EXC_FORMAT, DateTime.Now, ""));

                    if (header != null || header.Length > 0)
                    {
                        string[] headerLines = header.Split(_HEADER_SPLIT_CHAR);
                        foreach (string headerLine in headerLines)
                        {
                            logEntryString.AppendLine(headerLine.Trim());
                        }

                        logEntryString.AppendLine(" ");
                    }

                    //logEntryString.AppendLine("\t\t\t  Model:          " + Environment.UserName.ToString());
                    //logEntryString.AppendLine("\t\t\t  Serial Number:  " + Environment.MachineName.ToString());

                    //
                    // Log Exception data Key/Value pairs (if any):
                    //
                    if (ex.Data.Count > 0)
                    {
                        logEntryString.AppendLine("Exception Data:  ");
                        logEntryString.AppendLine("---------------  ");

                        foreach (object key in ex.Data.Keys)
                        {
                            string strKey = key as string;
                            if (strKey == null) continue;

                            object val = ex.Data[key];
                            if (val != null)
                            {
                                logEntryString.AppendLine(string.Format("\t\t\t     {0,-25} = {1} ", strKey, val.ToString()));
                            }
                        }

                        logEntryString.AppendLine(" ");
                    }

                    logEntryString.AppendLine("Exception Message:     ");
                    logEntryString.AppendLine("------------------  ");
                    logEntryString.AppendLine(ex.Message.ToString());
                    logEntryString.AppendLine(" ");

                    logEntryString.AppendLine("Exception Stack Trace:     ");
                    logEntryString.AppendLine("----------------------  ");
                    logEntryString.AppendLine(ex.StackTrace.ToString());

                    logEntryString.AppendLine("[/EX]");
                    logEntryString.AppendLine("************************** END EXCEPTION *********************************************************************************************");

                    string logEntry = logEntryString.ToString();

                    if (_TextWriter != null)
                    {
                        _TextWriter.WriteLine(logEntry);
                        _TextWriter.Flush();
                    }

                    FireLogUpdatedEvent(logEntry);
                }
            }
            catch (Exception loggingEx)
            {
                FireLoggingExceptionEvent(loggingEx);
            }
        }
        #endregion


        #region protected void FireLoggingExceptionEvent(Exception ex)
        /// <summary>
        /// If logging is failing send notification to any event handlers. The event
        /// UI event handler can then choose to disable exception logging or just turn 
        /// off the any subsequent event calls.
        /// </summary>
        /// <param name="ex"></param>
        protected void FireLoggingExceptionEvent(Exception ex)
        {
            //
            // A hander can choose not to get notified again if logging has failed 
            // by setting the IgnoreLoggingExceptionEvents property to true.
            //
            if (this.SuppressLoggingExceptionEvents) return;

            if (LoggingException != null)
            {
                LoggingException(ex);
            }
        }
        #endregion

        #region protected void FireLogUpdatedEvent(string logEntry)
        /// <summary>
        /// Notify any subscribers that the log file has been updated. Usually a UI component will 
        /// subscribe to this event.
        /// </summary>
        /// <param name="logEntry"></param>
        protected void FireLogUpdatedEvent(string logEntry)
        {
            if (LogUpdated != null)
            {
                LogUpdated(logEntry);
            }
        }
        #endregion

    }




}
