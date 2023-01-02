using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class SeqLogMessageHeader
    {
        public string TimeStamp { get; set; }
        public string SubsystemName { get; set; }
        public SeqLogMessageTypeEnum MessageType { get; set; }
        public SeqLogFlagEnum Flags { get; set; }
        public string HeaderMessage { get; set; }
    }
    public interface ISeqLogMessageHeaderParser
    {
        SeqLogMessageHeader ParseMessageHeader(string logMsg, bool getHeaderMessage = false );
    }
}
