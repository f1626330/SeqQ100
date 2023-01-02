
using System.Reflection;

namespace Sequlite.ALF.Common
{
	public static class SeqLogFactory
	{
		/// <summary>
		/// Gets a logging interface where the log file name is automatically named using the Assembly name
		/// followed by '-#' where # in a unique positive integer.
		/// example: Sequlite-1
		/// </summary>
		/// <param name="subsystemName">The name of the subsystem using this logging interface.
		/// This will be included as part of each message that is logged.</param>
		/// <returns>An interface to an SeqFileLog that can be used within a subsystem.</returns>
		public static ISeqFileLog GetSeqFileLog(string subsystemName)
		{
			Assembly assem = Assembly.GetEntryAssembly();
			AssemblyName assemName = assem.GetName();
			return GetSeqFileLog(assemName.Name, subsystemName);
		}

		/// <summary>
		/// Gets a logging interface with a custom log file name
		/// </summary>
		/// <param name="appName">The name of the log file. This name will be appended by '-#' where # is a unique positive integer.</param>
		/// <param name="subsystemName"></param>
		/// <returns></returns>
		public static ISeqFileLog GetSeqFileLog(string appName, string subsystemName) => SeqFileLogProxy.GetInstance(appName, subsystemName);
		public static void AddFilterOutFlags(SeqLogFlagEnum flags) => SeqFileLogProxy.AddFilterOutFlags(flags);


	}
}
