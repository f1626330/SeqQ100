using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.SQLite;
using System.Data.SQLite.EF6;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    //. Keep your SQLiteConfiguration.cs file in same folder where your context class is.
    class SQLiteConfiguration : DbConfiguration
    {
        public SQLiteConfiguration()
        {
            SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);
            SetProviderFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance);
            SetProviderServices("System.Data.SQLite", (DbProviderServices)SQLiteProviderFactory.Instance.GetService(typeof(DbProviderServices)));
        }
    }


    class DatabaseContext : DbContext
    {
        ISeqLog Logger { get;  }
        static Dictionary<string, DatabaseContext> _Instances = new Dictionary<string, DatabaseContext>();
        public static DatabaseContext GetSeqDB(string dbFileName, ISeqLog logger)
        {
            DatabaseContext it = null;
            lock (_Instances)
            {
                if (_Instances.ContainsKey(dbFileName))
                {
                    it = _Instances[dbFileName];
                }
                else
                {
                    it = new DatabaseContext(dbFileName, logger);

                    _Instances.Add(dbFileName, it);
                }
            }
            return it;
        }
        //Example: dataSource = "D:\\samples\\DBTest2\\SQLiteWithEF.db"
        private DatabaseContext(string dataSource, ISeqLog logger) :
            base( new SQLiteConnection()
            {
                ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = dataSource, ForeignKeys = true }.ConnectionString
            }, true)
        {
            Logger = logger;
            SQLiteConnection conn = (SQLiteConnection)this.Database.Connection;

            if (!CheckIfTableExists(conn, "DatabaseVersion"))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = @"CREATE TABLE DatabaseVersion(ID INTEGER PRIMARY KEY,Version INT)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO DatabaseVersion(ID, Version) VALUES(1,1)";
                    cmd.ExecuteNonQuery();
                    //cmd.CommandText = "UPDATE DatabaseVersion SET version = 1 WHERE id = 1";
                    //cmd.ExecuteNonQuery();
                }
            }  
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //If you will remove this line you will get  
            //System.Data.Entity.Infrastructure.DbUpdateException
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            // Configure Login & User entity
            modelBuilder.Entity<Login>()
                        .HasOptional(s => s.User) // Mark User property optional in Student entity
                        .WithRequired(ad => ad.Login); // mark Login property as required in User entity. Cannot save User without Login
            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Login> Login { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<DatabaseVersion> DatabaseVersion { get; set; }
        //public DbSet<BarcodeID> BarcodeID { get; set; }
        //public DbSet<RFID> RFID { get; set; }
        public DbSet<IDHistory> IDHistory { get; set; }
        public int DataBaseVersion
        {
            get
            {
                var v = DatabaseVersion.Where(it => it.ID == 1).FirstOrDefault();
                if (v != default(DatabaseVersion))
                {
                    return v.Version;
                }
                else
                {
                    return 0;
                }
            }
        }

        public bool CheckIDHistoryTables(int dbVersion)
        {
            SQLiteConnection conn = (SQLiteConnection)this.Database.Connection;
            bool idTableIsOk = true;
            //bool barcodeTableIsOk = true;
            bool updateDbVersion = false;
            bool bUpdateDB = dbVersion <= 6;
            string str;
            if (bUpdateDB)
            {
                if (CheckIfTableExists(conn, "BarcodeIDHistory"))
                {
                    str = "DROP TABLE BarcodeIDHistory";
                    Logger.Log(str);
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = str;
                        int n = cmd.ExecuteNonQuery();
                    }
                }

                if (CheckIfTableExists(conn, "RFIDHistory"))
                {
                    str = "DROP TABLE RFIDHistory";
                    Logger.Log(str);
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = str;
                        int n = cmd.ExecuteNonQuery();
                    }
                }

                if (CheckIfTableExists(conn, "IDHistory"))
                {
                    str = "DROP TABLE IDHistory";
                    Logger.Log(str);
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = str;
                        int n = cmd.ExecuteNonQuery();
                    }
                }
            }
            

            if (bUpdateDB)
            {
                
               // str = "(Type INTEGER PRIMARY KEY,Description VARCHAR)";

                str = "(Id INTEGER,IdString VARCHAR NOT NULL, IdType INTEGER NOT NULL, TimeUsed DATETIME, Description VARCHAR,PRIMARY KEY(Id))";

                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "CREATE TABLE IDHistory" + str;
                    int n = cmd.ExecuteNonQuery();
                    if (n < 0)
                    {
                        Logger.LogError("Failed to create IDHistory table");
                        idTableIsOk = false;
                    }
                    else
                    {
                        Logger.Log("IDHistory table is created");
                        updateDbVersion = true;
                    }
                }
            }

            //str = "(Id VARCHAR,Instrument VARCHAR, Version INTEGER, Output INTEGER, FCID INTEGER, ExpirationDate DATETIME, PairedEnd INTEGER,ReadLength INTEGER,Description VARCHAR, PRIMARY KEY(Id), FOREIGN KEY(Id) REFERENCES IDHistory(Id) )";
            //if (idTableIsOk && bUpdateDB)
            //{
            //    using (SQLiteCommand cmd = new SQLiteCommand(conn))
            //    {
            //        cmd.CommandText = "CREATE TABLE BarcodeID" + str;
            //        int n = cmd.ExecuteNonQuery();
            //        if (n < 0)
            //        {
            //            Logger.LogError("Failed to create BarcodeID table");
            //            barcodeTableIsOk = false;
            //        }
            //        else
            //        {
            //            Logger.Log("BarcodeID table is created");
            //            updateDbVersion = true;
            //        }
            //    }
            //}

            //bool rfidTableIsOk = true;
            //if (idTableIsOk && bUpdateDB)
            //{
            //    using (SQLiteCommand cmd = new SQLiteCommand(conn))
            //    {
            //        cmd.CommandText = "CREATE TABLE RFID" + str;
            //        int n = cmd.ExecuteNonQuery();
            //        if (n < 0)
            //        {
            //            Logger.LogError("Failed to create RFID table");
            //            rfidTableIsOk = false;
            //        }
            //        else
            //        {
            //            Logger.Log("RFID table is created");
            //            updateDbVersion = true;
            //        }
            //    }
            //}

            //if (idTableIsOk && barcodeTableIsOk && rfidTableIsOk && updateDbVersion)
              if (idTableIsOk  && updateDbVersion)
                {
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    int newVersion = 7;
                    if (newVersion != dbVersion)
                    {
                        Logger.Log($"updating the current DB version {dbVersion} to version {newVersion}");
                        cmd.CommandText = $"UPDATE DatabaseVersion SET version = {newVersion} WHERE id = 1";
                        if (cmd.ExecuteNonQuery() <= 0)
                        {
                            Logger.LogError($"Failed to update the current DB version {dbVersion} to version {newVersion}");
                        }
                    }
                }
            }

            return idTableIsOk;// barcodeTableIsOk && rfidTableIsOk;
        }

        //https://zetcode.com/csharp/sqlite/
        private bool CheckIfTableExists(SQLiteConnection conn, string tableName)
        {
            if (conn.State == System.Data.ConnectionState.Closed)
                conn.Open();

            using (SQLiteCommand cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = $"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='{tableName}';";
                object result = cmd.ExecuteScalar();
                int resultCount = Convert.ToInt32(result);
                if (resultCount > 0)
                    return true;

            }
            return false;
        }
    }

   
}
