using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public static  class UserAccountFactory
    {
        static Dictionary<string, IUser> _Interfaces = new Dictionary<string, IUser>();
        public static IUser CreaeteUserAccountInterfaceFromSeqDB(string dbFileName, ISeqLog logger)
        {
            IUser it = null;
            lock (_Interfaces)
            {
                if (_Interfaces.ContainsKey(dbFileName))
                {
                    it = _Interfaces[dbFileName];
                }
                else
                {
                    //DatabaseContext db = new DatabaseContext(dbFileName, logger);
                    DatabaseContext db =  DatabaseContext.GetSeqDB(dbFileName, logger);
                    it = new UserAccountImpl(db, logger);
                    _Interfaces.Add(dbFileName, it);
                    
                }
            }
            return it;
        }
    }
}
