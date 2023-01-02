using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public enum IdTypeEnum
    {
        Unknow  = -1,
        Barcode = 0,
        RFID = 1,
    }

    public enum PairedEndEnum
    {
        Unknow = -1,
        SE = 0,
        PE = 1,
    }


    //example 
    //Barcode:  SeqQ100-v01-100M-1234567-072021
    //RFID:     SeqQ100-v01-100M-1234567-072021-SE-75
    public class SeqId
    {
        //SeqQ100-v01-100M-1234567-072021-SE-75 for example
        public string Id { get; set; }

        //SeqQ100
        public string Instrument { get; set; }

        
        public int Version { get; set; }

       //100 (M)
        public int Output { get; set; }

        //Lot #
        public int FCID { get; set; }

        //example: 072021
        public DateTime ExpirationDate { get; set; }

        //SE -- 0 or PE -- 1
        public PairedEndEnum PairedEnd { get; set; }

        //36, 75, or 150 
        public int ReadLength { get; set; }

       
        public string Description { get; set; }
        
    }

    public interface IIDHistory
    {
        bool AddIDHistory(string id, IdTypeEnum idtype, string desp = "");
        bool MatchId(string id, IdTypeEnum idtype);
        SeqId ParseId(string id, IdTypeEnum idtype);
    }
}
