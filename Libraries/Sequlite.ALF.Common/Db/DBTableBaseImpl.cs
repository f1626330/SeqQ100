using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    class DBTableBaseImpl
    {
        protected DatabaseContext _SqDb;
        protected ISeqLog Logger { get; }
        protected DBTableBaseImpl(DatabaseContext db, ISeqLog logger)
        {
            _SqDb = db;
            Logger = logger;
        }

        protected bool SaveChanges()
        {
            var ret = Task<int>.Run(async () =>
            {
                return await _SqDb.SaveChangesAsync();
            });

            bool b = ret.Result > 0;
            if (!b)
            {
                Logger.LogError("Failed to save database changes");
            }
            return b;
        }
    }
}
