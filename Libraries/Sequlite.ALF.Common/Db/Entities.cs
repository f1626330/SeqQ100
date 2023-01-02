using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.Entity.ModelConfiguration.Conventions;
//using System.Data.Linq.Mapping;
using System.Data.SQLite;
using System.Data.SQLite.EF6;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    [Table("Login")]
    public class Login
    {
        [Column("ID", TypeName = "INTEGER")]
        [Key]
        public int ID { get; set; }

        [Column("UserName", TypeName = "VARCHAR")]
        public string UserName { get; set; }

        //salt
        [Column("Password1", TypeName = "VARCHAR")]
        public string Password1 { get; set; }

        //hash
        [Column("Password2", TypeName = "VARCHAR")]
        public string Password2 { get; set; }

        //hash and use password1 as salt
        [Column("AccessRight", TypeName = "INTEGER")]
        public int AccessRight { get; set; }

        [Column("CreateTime", TypeName = "DATETIME")]
        public DateTime CreateTime { get; set; }

        [Column("Retired", TypeName = "INTEGER")]
        public int Retired { get; set; }

        [Column("AccessRightCode", TypeName = "VARCHAR")]
        public string AccessRightCode { get; set; }

        [Column("UpdateTime", TypeName = "DATETIME")]
        public DateTime UpdateTime { get; set; }

        public virtual User User { get; set; }
    }

    [Table("User")]
    public class User
    {
        [Column("ID", TypeName = "INTEGER")]
        [Key]
        [ForeignKey("Login")]
        public int ID { get; set; }

        [Column("LastName", TypeName = "VARCHAR")]
        public string LastName { get; set; }

        //salt
        [Column("FirstName", TypeName = "VARCHAR")]
        public string FirstName { get; set; }

        //hash
        [Column("Email", TypeName = "VARCHAR")]
        public string Email { get; set; }

        [Column("PhoneNumber", TypeName = "VARCHAR")]
        public string PhoneNumber { get; set; }

        [Column("Company", TypeName = "VARCHAR")]
        public string Company { get; set; }

        [Column("Address", TypeName = "VARCHAR")]
        public string Address { get; set; }

        [Column("WeChatID", TypeName = "VARCHAR")]
        public string WeChatID { get; set; }

        [Column("UpdateTime", TypeName = "DATETIME")]
        public DateTime UpdateTime { get; set; }
        public virtual Login Login { get; set; }
    }

    [Table("DatabaseVersion")]
    public class DatabaseVersion
    {
        [Column("ID", TypeName = "INTEGER")]
        [Key]
        public int ID { get; set; }

        [Column("Version", TypeName = "INTEGER")]
        public int Version { get; set; }
    }


    [Table("IDHistory")]
    public class IDHistory
    {
        [Column("Id", TypeName = "INTEGER")]
        [Key]
        public int Id { get; set; }

        //0 -- Barcode ID   //1 --- RFID
        [Column("IdType", TypeName = "INTEGER")]
        public int IdType { get; set; }

        [Column("IdString", TypeName = "VARCHAR")]
        public string IdString { get; set; }

        [Column("TimeUsed", TypeName = "DATETIME")]
        public DateTime TimeUsed { get; set; }

        [Column("Description", TypeName = "VARCHAR")]
        public string Description { get; set; }

    }

    //[Table("BarcodeID")]
    //public class BarcodeID
    //{
    //    [Column("Id", TypeName = "VARCHAR")]
    //    [Key]
    //    [ForeignKey("IDHistory")]
    //    public string Id {get;set;}

    //    [Column("Instrument", TypeName = "VARCHAR")]
    //    public string Instrument { get; set; }

    //    [Column("Version", TypeName = "INTEGER")]
    //    public int Version { get; set; }

    //    [Column("Output", TypeName = "INTEGER")]
    //    public int Output { get; set; }

    //    [Column("FCID", TypeName = "INTEGER")]
    //    public int FCID { get; set; }

    //    [Column("ExpirationDate", TypeName = "DATETIME")]
    //    public DateTime ExpirationDate { get; set; }

    //    [Column("PairedEnd", TypeName = "INTEGER")]
    //    public int PairedEnd { get; set; }

    //    [Column("ReadLength", TypeName = "INTEGER")]
    //    public int ReadLength { get; set; }

    //    //[Column("TimeUsed", TypeName = "DATETIME")]
    //    //public DateTime TimeUsed { get; set; }

    //    [Column("Description", TypeName = "VARCHAR")]
    //    public string Description { get; set; }

    //    public virtual IDHistory IDHistory { get; set; }
    //}


    //[Table("RFID")]
    //public class RFID
    //{
    //    [Column("Id", TypeName = "VARCHAR")]
    //    [Key]
    //    [ForeignKey("IDHistory")]
    //    public string Id { get; set; }

    //    [Column("Instrument", TypeName = "VARCHAR")]
    //    public string Instrument { get; set; }

    //    [Column("Version", TypeName = "INTEGER")]
    //    public int Version { get; set; }

    //    [Column("Output", TypeName = "INTEGER")]
    //    public int Output { get; set; }

    //    [Column("FCID", TypeName = "INTEGER")]
    //    public int FCID { get; set; }

    //    [Column("ExpirationDate", TypeName = "DATETIME")]
    //    public DateTime ExpirationDate { get; set; }

    //    [Column("PairedEnd", TypeName = "INTEGER")]
    //    public int PairedEnd { get; set; }

    //    [Column("ReadLength", TypeName = "INTEGER")]
    //    public int ReadLength { get; set; }

    //    //[Column("TimeUsed", TypeName = "DATETIME")]
    //    //public DateTime TimeUsed { get; set; }

    //    [Column("Description", TypeName = "VARCHAR")]
    //    public string Description { get; set; }
    //    public virtual IDHistory IDHistory { get; set; }
    //}


}
