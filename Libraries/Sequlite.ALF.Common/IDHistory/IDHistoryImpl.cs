using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    class IDHistoryImpl : DBTableBaseImpl,IIDHistory
    {
       
        public IDHistoryImpl(DatabaseContext db, ISeqLog logger) : base(db, logger)
        {
            
        }

        public bool AddIDHistory(string id, IdTypeEnum idtype, string desp = "")
        {
            bool b ;
            //if (MatchId(id, idtype))
            //{
            //    Logger.LogError($"{id} already in DB");
            //    b = false;
            //}
            //else
            {
                IDHistory idhistory = new IDHistory()
                {
                    
                    IdType = (int)idtype,
                    IdString = id,
                    TimeUsed = DateTime.Now,
                    Description = desp
                };
                _SqDb.IDHistory.Add(idhistory);
                 b = SaveChanges();
            }
            return b;
        }

        public bool MatchId(string idStr, IdTypeEnum idtype)
        {
            bool b = false;
            var item = _SqDb.IDHistory.Where(it => it.IdString.Equals(idStr) && ((int) idtype) == it.IdType).FirstOrDefault();
            if (item != default(IDHistory))
            {
                b = true;
            }
            return b;
        }

        public SeqId ParseId(string id, IdTypeEnum idtype)
        {
            //"SeqQ100-v01-100M-1234567-072021", IdTypeEnum.Barcode,
            //"SeqQ100-v01-100M-1234567-072021-SE-75", IdTypeEnum.RFID
            SeqId seqId = null;
            string[] strs = id.Split(new char[] { '-' });
            bool b = true;
            string instrument = "";
            int version = 0;
            int output = 0;
            int FCID = 0;
            PairedEndEnum end = PairedEndEnum.Unknow; ;
            DateTime expirationDate = default(DateTime);
            int readLength = 0;
            if (strs.Length >= 5)
            {
                instrument = strs[0];
                int temp;
                b = int.TryParse(strs[1].Substring(1), out temp);
                if (b)
                {
                    version = temp;
                    b = int.TryParse(strs[2].Substring(0, strs[2].Length - 1), out temp);
                }

                if (b)
                {
                    output = temp;
                    b = int.TryParse(strs[3], out temp);
                }

                if (b)
                {
                    FCID = temp;
                    DateTime dt;
                    b = DateTime.TryParseExact(strs[4], "MMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
                    if (b)
                    {
                        DateTime lastDate = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month));
                        expirationDate = lastDate;
                    }

                    if (idtype == IdTypeEnum.RFID)
                    {
                        b = strs.Length >= 7;
                    }

                    if (b)
                    {
                        if (strs[5] == "SE")
                        {
                            end = PairedEndEnum.SE;
                        }
                        else if (strs[5] == "PE")
                        {
                            end = PairedEndEnum.PE;

                        }
                        else
                        {
                            b = false;
                        }
                    }

                    if (b)
                    {
                        b = int.TryParse(strs[6], out temp);
                    }

                    if (b)
                    {
                        readLength = temp;
                        seqId = new SeqId()
                        {
                            Id = id,
                            Instrument = instrument,
                            Version = version,
                            Output = output,
                            FCID = FCID,
                            ExpirationDate = expirationDate,
                            PairedEnd = end,
                            ReadLength = readLength
                        };

                    }
                    else
                    {
                        Logger.LogError($"Wrong Id format: {id}");

                    }
                }
               
            }
            return seqId;
        }
    }
}
