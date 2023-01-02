using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    internal class SeqLogMessageHeaderParser : ISeqLogMessageHeaderParser
    {
        private static readonly string s_messageHeaderPattern = @"(?<header>\[(?<timestamp>(\d{4}-\d{2}-\d{2}.*?))\]\|(?<flags>\d+)\|(?<subsys>.*?)\|(?<msg_type>\w+)\|s?)";

        public SeqLogMessageHeader ParseMessageHeader(string logMsg, bool getHeader)
        {

            Match m = Regex.Match(logMsg, s_messageHeaderPattern);
            if (m.Success && m.Groups.Count >= 4)
            {
                string timestampe = m.Groups["timestamp"].Value;
                string subsys = m.Groups["subsys"].Value;
                string flags = m.Groups["flags"].Value;
                uint n = 0;
                bool b = uint.TryParse(flags, out n);
                SeqLogFlagEnum flagsEnum = (SeqLogFlagEnum)n;
                string msg_type = m.Groups["msg_type"].Value;
                SeqLogMessageTypeEnum msgTypeEnum;
                b = Enum.TryParse(msg_type, out msgTypeEnum);
                SeqLogMessageHeader msgHeader = new SeqLogMessageHeader
                { TimeStamp = timestampe, SubsystemName = subsys, MessageType = msgTypeEnum, Flags = flagsEnum };
                if (getHeader )
                {
                    msgHeader.HeaderMessage = m.Groups["header"].Value;
                }
                return msgHeader;
            }
            else
            {
                return new SeqLogMessageHeader();
            }
        }
    }
}
