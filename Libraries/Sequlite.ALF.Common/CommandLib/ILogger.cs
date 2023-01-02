using System;

namespace Sequlite.ALF.Common
{
    /// <summary>
    /// The ILogger interface definition.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log an empty line with no date/time stamp.
        /// </summary>
        void LogEmptyLine();

        /// <summary>
        /// Log a 1-line message of the following format:
        ///   [MS] MM/dd/YYYY   hh:mm:ss.fff PM   msg
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogMessage(string message);

        /// <summary>
        /// Log an indented parameter value pair of the following format:
        /// [PV]    paramName = value
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="value"></param>
        void LogParamValuePair(string paramName, string value);

        /// <summary>
        /// Log an exception.
        /// </summary>
        /// <param name="header">A message describing the current context. Can be null or empty</param>
        /// <param name="ex">The exception that was caught</param>
        void LogException(string header, Exception ex);

    }


}
