using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    /// <summary>
	/// An enumeration of bit masks to be used for filtering log messages.
	/// Flags allow log messages to be sub-categories to the main message categories
	/// that are enumerated in <seealso cref="SeqLogMessageTypeEnum"/>.
	/// <br/><br/>
	/// Messages with specific flags can be ommitted from log files using
	/// the "FilterOutFlags" setting in the Config file.
	/// <br/>
	/// This enumeration makes the most sense when used with the INFO message type.
	/// <br/><br/>
	/// WARNING: messages that have been filtered with "FilterOutFlags" will not appear in log files.
	/// This can cause error or warning messages to be ommitted from log files.
	/// </summary>
	[Flags]
	public enum SeqLogFlagEnum : uint // allows up to 32 flags
	{
		NONE = 0, // messages with flag NONE are always logged
		NORMAL = 1,
		DEBUG = 2,
		TEST = 4, //For temporary test
		STARTUP = 8, //for application startup message
		
		OLAERROR = 16,
		OLAWARNING = 32,
		BENCHMARK = 64,
		//more flags to be added
	};

	/// <summary>
	/// An enumeration of message types. Used for coloring log messages in the log viewer ui.
	/// <br/><br/>
	/// Messages may be filtered further by using the flags in <seealso cref="SeqLogFlagEnum"/>.
	/// </summary>
	public enum SeqLogMessageTypeEnum : byte
	{
		INFO,
		WARNING,
		ERROR,
	}

	public delegate void MessageLoggedEvent(string[] logMessage);
	public interface ISeqLog
    {
		event MessageLoggedEvent OnMessageLogged;
		void Log(string strMessage, SeqLogFlagEnum flags = SeqLogFlagEnum.NORMAL);
		void LogWarning(string strMessage, SeqLogFlagEnum flags = SeqLogFlagEnum.NORMAL);
		void LogError(string strMessage, SeqLogFlagEnum flags = SeqLogFlagEnum.NORMAL);
		void LogDebug(string strMessage, SeqLogFlagEnum flags = SeqLogFlagEnum.DEBUG);
		SeqLogFlagEnum FilterOutFlags {get;set;} //filter out unwanted message
		ISeqLogMessageHeaderParser LogMessageHeaderParser { get; }

		int ErrorCount { get; }
        int WarningCount { get; }
	}
}
