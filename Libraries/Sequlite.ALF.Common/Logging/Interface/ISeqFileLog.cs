using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.Common
{
	public interface ISeqFileLog : ISeqLog
	{
		string LogFilePath { get; }
		string LogFileName { get; }
		string LogFileFullName { get; }
	}
}
