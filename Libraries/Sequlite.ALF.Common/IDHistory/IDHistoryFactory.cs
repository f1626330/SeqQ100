using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
   
    public static class IDHistoryFactory
    {
        static Dictionary<string, IIDHistory> _Interfaces = new Dictionary<string, IIDHistory>();
        public static IIDHistory CreaeteIDHistoryInterfaceFromSeqDB(string dbFileName, ISeqLog logger)
        {
            IIDHistory it = null;
            lock (_Interfaces)
            {
                if (_Interfaces.ContainsKey(dbFileName))
                {
                    it = _Interfaces[dbFileName];
                }
                else
                {
                    //DatabaseContext db = new DatabaseContext(dbFileName, logger);
                    DatabaseContext db = DatabaseContext.GetSeqDB(dbFileName, logger);
                    
                    int n = db.DataBaseVersion;
                    logger.Log($"Database Version is: {n}");
                    if (!db.CheckIDHistoryTables(n))
                    {
                        //logger.LogError("ID history tables are not available");
                        throw new Exception("ID history tables are not available");
                    }
                    else
                    {
                        it = new IDHistoryImpl(db, logger);
                        _Interfaces.Add(dbFileName, it);
                    }
                }
            }
            return it;
        }
    }
}
